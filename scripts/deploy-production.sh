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

# Both paths run these first, in this order.
build_and_scan() {
  "${COMPOSE[@]}" config --quiet
  "${COMPOSE[@]}" build api web worker migrate
  # Scans what this host just built. The images CI scanned are not these images: the VPS rebuilds
  # from source, and the .NET base images, apt and NuGet restore are all mutable.
  bash scripts/scan-container-images.sh app
}

case "$mode" in
  initial)
    build_and_scan
    "${COMPOSE[@]}" up -d postgres redis
    "${COMPOSE[@]}" --profile tools run --rm migrate
    "${COMPOSE[@]}" up -d --wait api worker web
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
