#!/usr/bin/env bash
# Runs a production deployment on the VPS.
#
#   bash scripts/deploy-production.sh initial
#   bash scripts/deploy-production.sh update
#   bash scripts/deploy-production.sh backup
#
# The ordering here is the control, not a convention: `set -euo pipefail` means a failing
# vulnerability scan aborts before anything starts. Pasting the same commands into a shell one by
# one does not do that - the shell happily runs the next line after a non-zero exit - which is why
# the procedure lives in this file and docs/docker_deployment.md points at it.
#
# The rules the ordering encodes:
#   - the scan runs after the build, or it reports on the previous release's images;
#   - the scan runs before anything starts, or it reports on images that are already serving;
#   - on an update the backup is taken while the application is stopped and before migrations run,
#     because a failed migration has nowhere to go back to otherwise.
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT_NAME="seo-intelligence-prod"
ENV_FILE="${ENV_FILE:-.env.production}"

# --project-name is passed explicitly rather than relying on `name:` in compose.production.yaml.
# A COMPOSE_PROJECT_NAME in the environment overrides that key, and the difference is not cosmetic:
# it decides which volumes the migration writes to and which containers get stopped.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f compose.yaml -f compose.production.yaml)

usage() {
  echo "Usage: bash scripts/deploy-production.sh <initial|update|backup>" >&2
  echo "  initial  First deployment: build, scan, migrate, start." >&2
  echo "  update   Update: build, scan, stop, back up, migrate, recreate." >&2
  echo "  backup   Ad-hoc backup: stop, back up, start again." >&2
}

mode="${1:-}"
shift || true

if [[ $# -gt 0 ]]; then
  usage
  exit 1
fi

case "$mode" in
  initial|update|backup) ;;
  *) usage; exit 1 ;;
esac

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: $ENV_FILE not found. Copy .env.production.example and fill it in first." >&2
  exit 1
fi

# One operation at a time on this Compose project, for the whole run rather than for any single
# step. Two updates started more than a second apart get different backup directories, so nothing
# collides there - but the second would be dumping the database while the first is already running
# its migration, and both would recreate the containers. The lock covers build through recreate,
# and backup-production.sh joins the same lock so a hand-run backup cannot interleave either.
# shellcheck source=scripts/lib/production-lock.sh
source scripts/lib/production-lock.sh
acquire_production_lock "$PROJECT_NAME" || exit 1

# Stamped into the images as org.opencontainers.image.revision. Without it there is no way to tell
# which commit a running container came from: the VPS rebuilds from source rather than pulling a
# tagged artifact, so nothing else records it. Git wins over any inherited SOURCE_REVISION - see
# the library for why - and a working tree with changes is stamped <sha>-dirty rather than refused.
# shellcheck source=scripts/lib/source-revision.sh
source scripts/lib/source-revision.sh
export SOURCE_REVISION
SOURCE_REVISION="$(resolve_source_revision)" || {
  echo "       Refusing to build: an image whose source cannot be identified is one nobody can" >&2
  echo "       trace back to a commit when it misbehaves." >&2
  exit 1
}
case "$SOURCE_REVISION" in
  *-dirty)
    echo "NOTE: the working tree has uncommitted changes; images will be labelled ${SOURCE_REVISION}." >&2
    ;;
  unknown)
    echo "NOTE: this is not a git checkout; images will be labelled 'unknown'." >&2
    ;;
esac

# Which image ID each application image resolved to when it was scanned. compose.yaml names these
# four by variable so exactly those IDs start, rather than whatever the tags point at by then.
SCAN_MANIFEST="artifacts/scanned-images.tsv"

# Both paths run these first, in this order.
build_and_scan() {
  "${COMPOSE[@]}" config --quiet
  "${COMPOSE[@]}" build api web worker migrate
  # Scans what this host just built. The images CI scanned are not these images: the VPS rebuilds
  # from source, and the .NET base images, apt and NuGet restore are all mutable.
  #
  # The scanner writes the manifest only when every image passes, and clears it first, so a failed
  # scan leaves nothing for the pinning below to read.
  APP_SCAN_MANIFEST="$SCAN_MANIFEST" bash scripts/scan-container-images.sh app
  pin_scanned_images
}

# Exported here rather than trusted from the environment. Compose lets an exported variable win
# over --env-file, so setting them unconditionally is what stops an inherited value choosing the
# image - the same reason --project-name is passed explicitly above.
pin_scanned_images() {
  if [[ ! -s "$SCAN_MANIFEST" ]]; then
    echo "ERROR: $SCAN_MANIFEST is missing or empty, so there is no record of what passed the scan." >&2
    echo "       It is written only when every application image passes. Re-run the scan." >&2
    exit 1
  fi

  local image id
  while IFS=$'\t' read -r image id; do
    [[ -n "$image" ]] || continue
    if [[ ! "$id" =~ ^sha256:[0-9a-f]{64}$ ]]; then
      echo "ERROR: $SCAN_MANIFEST records '$id' for $image, which is not an image ID." >&2
      exit 1
    fi

    case "$image" in
      seo-intelligence-api) export API_IMAGE="$id" ;;
      seo-intelligence-web) export WEB_IMAGE="$id" ;;
      seo-intelligence-worker) export WORKER_IMAGE="$id" ;;
      seo-intelligence-migrate) export MIGRATE_IMAGE="$id" ;;
      *)
        echo "ERROR: $SCAN_MANIFEST names an unexpected image '$image'." >&2
        exit 1
        ;;
    esac
  done < "$SCAN_MANIFEST"

  local missing=()
  [[ -n "${API_IMAGE:-}" ]] || missing+=(api)
  [[ -n "${WEB_IMAGE:-}" ]] || missing+=(web)
  [[ -n "${WORKER_IMAGE:-}" ]] || missing+=(worker)
  [[ -n "${MIGRATE_IMAGE:-}" ]] || missing+=(migrate)
  if [[ "${#missing[@]}" -ne 0 ]]; then
    echo "ERROR: $SCAN_MANIFEST has no scanned image for: ${missing[*]}." >&2
    exit 1
  fi
}

# Read back rather than trusted. Starting from a pinned ID cannot produce the wrong image, but this
# also catches a container an earlier, unpinned run left behind - `up` reuses a container whose
# configuration it considers unchanged.
assert_running_images() {
  local service container running expected
  for service in "$@"; do
    case "$service" in
      api) expected="$API_IMAGE" ;;
      web) expected="$WEB_IMAGE" ;;
      worker) expected="$WORKER_IMAGE" ;;
      *) echo "ERROR: no scanned image recorded for service '$service'." >&2; exit 1 ;;
    esac

    container="$("${COMPOSE[@]}" ps --quiet "$service")"
    if [[ -z "$container" ]]; then
      echo "ERROR: $service has no running container after up." >&2
      exit 1
    fi

    running="$(docker inspect --format '{{.Image}}' "$container")"
    if [[ "$running" != "$expected" ]]; then
      echo "ERROR: $service is running $running but the scan passed $expected." >&2
      exit 1
    fi
  done

  echo "api, worker and web are running the image IDs that passed the scan."
}

case "$mode" in
  initial)
    build_and_scan
    "${COMPOSE[@]}" up -d postgres redis
    "${COMPOSE[@]}" --profile tools run --rm migrate
    "${COMPOSE[@]}" up -d --wait api worker web
    assert_running_images api worker web
    "${COMPOSE[@]}" ps
    ;;
  update)
    build_and_scan
    # Stopped before the backup and the migration so no old Worker overwrites job state, no old API
    # registers jobs in the previous format, and the backup is a consistent point in time.
    "${COMPOSE[@]}" stop web api worker
    # Passed explicitly so a BACKUP_PROJECT_NAME left in the environment by a restore rehearsal
    # cannot redirect this. Inherited, it would send the backup at another stack, and the child
    # would then fail to re-enter the lock this script is holding - after the application is
    # already stopped, and just before the migration.
    BACKUP_PROJECT_NAME="$PROJECT_NAME" bash scripts/backup-production.sh
    "${COMPOSE[@]}" --profile tools run --rm migrate
    "${COMPOSE[@]}" up -d --wait --force-recreate api worker web
    assert_running_images api worker web
    "${COMPOSE[@]}" ps
    "${COMPOSE[@]}" logs --tail 200 web api worker
    ;;
  backup)
    # Stopping and starting again is part of the backup, so it belongs inside the lock rather than
    # being three commands an operator runs by hand around an unlocked script.
    #
    # The restart is a trap, not the next line: `set -e` would otherwise leave the application down
    # if the backup failed. Nothing has changed at that point - no migration has run - so the right
    # answer is to bring the previous version back up and report the failure. This is the opposite
    # of the update path, where a failure must leave the stack stopped rather than restart old code
    # against a database a migration may already have touched.
    backup_taken=false
    restart_after_failed_backup() {
      local status=$?
      # Only when the backup itself failed. A failure in the restart or in `ps` afterwards is a
      # different problem, and reporting it as a backup failure - then running `up` again - would
      # send the operator looking in the wrong place.
      if [[ "$status" -ne 0 && "$backup_taken" != "true" ]]; then
        echo "The backup failed; restarting the services it stopped." >&2
        "${COMPOSE[@]}" up -d --wait api worker web || true
      fi
      return "$status"
    }
    trap restart_after_failed_backup EXIT

    "${COMPOSE[@]}" stop web api worker
    # Pinned for the same reason as the update path above.
    BACKUP_PROJECT_NAME="$PROJECT_NAME" bash scripts/backup-production.sh
    backup_taken=true
    "${COMPOSE[@]}" up -d --wait api worker web
    "${COMPOSE[@]}" ps
    trap - EXIT
    ;;
esac

echo "Deployment (${mode}) completed."
