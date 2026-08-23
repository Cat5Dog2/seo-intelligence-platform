#!/usr/bin/env bash
# Restore rehearsal for the production backup.
#
#   bash scripts/verify-production-restore.sh
#
# docs/docker_deployment.md 5.2 describes how to restore. A described restore is not a tested one,
# and a backup nobody has restored is the kind of thing that is only discovered to be worthless at
# the moment it is needed. This runs the description end to end:
#
#   1. builds the production images and starts a real, isolated stack,
#   2. creates a project and a real exported artifact through the API,
#   3. takes a backup with scripts/backup-production.sh - the script production itself uses,
#   4. restores the three artifacts into a second, empty project,
#   5. reads the same artifact back through the API there.
#
# Step 5 is the point. Reading the file back proves the database row, the stored object and the
# Data Protection keys were restored consistently with each other. Checking that the archives
# unpack proves none of that: each one can be perfectly valid and still describe a different
# moment than the other two.
#
# It creates and removes its own Compose projects, volumes, networks and env files, and never
# touches seo-intelligence-prod.
set -euo pipefail

# Git Bash rewrites POSIX-looking arguments into Windows paths before docker sees them.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."
repo_root="$PWD"

# Skipping exits non-zero unless the caller asked for it. This is the quarterly restore check: an
# automated run that cannot run it has not verified anything, and recording that as a success is
# how a backup goes years without anyone noticing it never restored. Pass
# RESTORE_REHEARSAL_ALLOW_SKIP=true for an interactive run on a machine that cannot host it.
unavailable() {
  echo "$1" >&2
  if [[ "${RESTORE_REHEARSAL_ALLOW_SKIP:-false}" == "true" ]]; then
    echo "skipped: RESTORE_REHEARSAL_ALLOW_SKIP is set." >&2
    exit 0
  fi
  echo "       The restore has NOT been verified. Run it on Linux with a Docker daemon, or set" >&2
  echo "       RESTORE_REHEARSAL_ALLOW_SKIP=true to accept an unverified run." >&2
  exit 2
}

if ! command -v docker > /dev/null 2>&1 || ! docker info > /dev/null 2>&1; then
  unavailable "The restore rehearsal needs a reachable Docker daemon."
fi

# The backup it calls takes the production lock, which needs a real flock. Git Bash has none, so
# the rehearsal runs on Linux - the VPS, CI, or a Linux container with the daemon socket.
if ! command -v flock > /dev/null 2>&1; then
  unavailable "The restore rehearsal needs flock (this is $(uname -s)); run it on Linux."
fi

# The generated env files hold a password and a service key. They are short-lived, but they are
# still credentials on disk.
umask 077

work="artifacts/restore-test-$$-${RANDOM}"
mkdir -p "$work"

# The backup takes the production lock, and the rehearsal gives it one of its own. Without this it
# would fall back to the host's lock locations, which is both wrong - the rehearsal locks isolated
# projects, not production - and unavailable on hosts where those locations are rejected: Debian's
# /var/lock is a 1777 symlink, so a non-root run there has nowhere safe and refuses outright.
lock_dir="$PWD/$work/lock"
mkdir -p "$lock_dir"
chmod 700 "$lock_dir"

suffix="$$-${RANDOM}"
src_project="seo-restore-src-${suffix}"
dst_project="seo-restore-dst-${suffix}"
src_network="seo-restore-src-net-${suffix}"
dst_network="seo-restore-dst-net-${suffix}"

src_env="$work/.env.src"
dst_env="$work/.env.dst"
app_env="$work/.env.app"

keys_path="/app/.data/data-protection-keys"

# compose.yaml names the application images explicitly - seo-intelligence-api and the rest - and an
# explicit `image:` is NOT namespaced by the Compose project. Without this override the rehearsal
# would build over the very tags production runs from, and the next `deploy-production.sh backup`
# would recreate the production containers on an image nobody scanned. The override gives the
# rehearsal its own tags, shared between its two projects and removed afterwards.
PRODUCTION_IMAGES=(seo-intelligence-api seo-intelligence-web seo-intelligence-worker seo-intelligence-migrate)
REHEARSAL_IMAGES=()
for production_image in "${PRODUCTION_IMAGES[@]}"; do
  REHEARSAL_IMAGES+=("seo-restore-rehearsal-${production_image#seo-intelligence-}:${suffix}")
done

rehearsal_override="$work/compose.rehearsal.yaml"

src_compose() {
  docker compose --project-name "$src_project" --env-file "$src_env" \
    -f compose.yaml -f compose.production.yaml -f "$rehearsal_override" "$@"
}

dst_compose() {
  docker compose --project-name "$dst_project" --env-file "$dst_env" \
    -f compose.yaml -f compose.production.yaml -f "$rehearsal_override" "$@"
}

# Recorded before anything runs so the end of the rehearsal can prove the production tags were not
# touched. A tag that does not exist yet is recorded as absent, which is equally a thing that must
# not change: the rehearsal must not create it either.
production_image_state() {
  local image state
  for image in "${PRODUCTION_IMAGES[@]}"; do
    state="$(docker image inspect --format '{{.Id}}' "$image" 2>/dev/null || echo absent)"
    printf '%s %s\n' "$image" "$state"
  done
}
production_images_before="$(production_image_state)"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

# Removes a resource only if it is there. A blanket `|| true` cannot tell "there was nothing to
# remove", which is normal when the run failed early, from "it is still there", which is not.
remove_if_present() {
  local kind="$1" name="$2"
  docker "$kind" inspect "$name" > /dev/null 2>&1 || return 0
  docker "$kind" rm "$name" > /dev/null 2>&1 && return 0
  echo "WARNING: ${kind} ${name} could not be removed." >&2
  return 1
}

# Runs on every exit path, not just the successful one. Two things have to hold however the run
# ended: nothing of the rehearsal is left on the host, and the production image tags are exactly as
# they were. A failure partway through is precisely when a half-built rehearsal image could have
# landed on a production tag, so checking that only on success would check it only when it cannot
# have happened. Both are reported through the exit code; a cleanup that quietly failed would let
# the next run inherit the mess and blame it on something else.
cleanup() {
  local status=$?
  local problems=0 leftovers image

  src_compose down --volumes --remove-orphans > /dev/null 2>&1 || problems=$((problems + 1))
  dst_compose down --volumes --remove-orphans > /dev/null 2>&1 || problems=$((problems + 1))

  remove_if_present network "$src_network" || problems=$((problems + 1))
  remove_if_present network "$dst_network" || problems=$((problems + 1))

  # Only the rehearsal's own tags. When they were tagged from the production images this removes
  # the tag and leaves the image, which is still referenced by the production tag.
  for image in "${REHEARSAL_IMAGES[@]}"; do
    remove_if_present image "$image" || problems=$((problems + 1))
  done

  # Scoped to this run. Every name here carries this run's suffix, so a second rehearsal running
  # alongside is not mistaken for wreckage - and reporting its perfectly good images as leftovers
  # would fail a run that did nothing wrong.
  leftovers="$(
    docker ps -a --format '{{.Names}}' 2>/dev/null | grep -E "^(${src_project}|${dst_project})[-_]" || true
    docker volume ls --format '{{.Name}}' 2>/dev/null | grep -E "^(${src_project}|${dst_project})_" || true
    docker network ls --format '{{.Name}}' 2>/dev/null | grep -E "^(${src_network}|${dst_network})$" || true
    for image in "${REHEARSAL_IMAGES[@]}"; do
      docker image inspect "$image" > /dev/null 2>&1 && printf '%s\n' "$image"
    done
    true
  )"
  if [[ -n "$leftovers" ]]; then
    echo "WARNING: the rehearsal left these behind:" >&2
    sed 's/^/         /' <<< "$leftovers" >&2
    problems=$((problems + 1))
  fi

  if [[ "$(production_image_state)" != "$production_images_before" ]]; then
    echo "FAIL: the rehearsal changed the production image tags." >&2
    diff <(printf '%s\n' "$production_images_before") <(production_image_state) >&2 || true
    problems=$((problems + 1))
  fi

  rm -rf "$work"

  if [[ "$problems" -ne 0 && "$status" -eq 0 ]]; then
    status=1
  fi
  # `exit` rather than `return`: a value returned from an EXIT trap does not become the script's
  # exit status, so a cleanup failure would be reported as success.
  exit "$status"
}
trap cleanup EXIT

# The same pinned image the backup script uses, so the rehearsal restores with the tooling the
# backup was written by.
tar_image() {
  local entry
  entry="$(tr -d '\r' < image-digests.lock | grep -E "^postgres$(printf '\t')")"
  printf '%s@%s' "$(cut -f2 <<< "$entry")" "$(cut -f3 <<< "$entry")"
}

random_hex() {
  openssl rand -hex 16 2>/dev/null || printf 'rehearsal%s%s' "$$" "${RANDOM}"
}

# --- an isolated production-shaped stack ---------------------------------------------------------

postgres_db="seo_restore_rehearsal"
postgres_user="seo_restore"
postgres_password="$(random_hex)"
service_key="$(random_hex)"

write_env() {
  local target="$1" network="$2"
  sed -e "s|^APP_ENV_FILE=.*|APP_ENV_FILE=${app_env}|" \
      -e "s|^POSTGRES_DB=.*|POSTGRES_DB=${postgres_db}|" \
      -e "s|^POSTGRES_USER=.*|POSTGRES_USER=${postgres_user}|" \
      -e "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=${postgres_password}|" \
      -e "s|^CADDY_NETWORK=.*|CADDY_NETWORK=${network}|" \
      -e "s|^API_SERVICE_KEY=.*|API_SERVICE_KEY=${service_key}|" \
      -e "s|^ADMIN_SEED_EMAIL=.*|ADMIN_SEED_EMAIL=rehearsal@localhost|" \
      -e "s|^ADMIN_SEED_PASSWORD=.*|ADMIN_SEED_PASSWORD=Rehearsal!Pass1|" \
      .env.production.example > "$target"
}

cp .env.production.app.example "$app_env"
write_env "$src_env" "$src_network"
write_env "$dst_env" "$dst_network"

cat > "$rehearsal_override" <<OVERRIDE
# Generated by scripts/verify-production-restore.sh. Keeps the rehearsal off the production tags.
services:
  api:
    image: ${REHEARSAL_IMAGES[0]}
  web:
    image: ${REHEARSAL_IMAGES[1]}
  worker:
    image: ${REHEARSAL_IMAGES[2]}
  migrate:
    image: ${REHEARSAL_IMAGES[3]}
OVERRIDE

# Checked against the rendered configuration before anything is built, because after the build the
# damage is done: a `build` that still pointed at seo-intelligence-api would have replaced the tag
# production runs from, and no later assertion can put it back.
rendered_config="$(src_compose --profile tools config 2>&1)" \
  || fail "the rehearsal Compose configuration does not render: $rendered_config"

for production_image in "${PRODUCTION_IMAGES[@]}"; do
  if grep -qE "^[[:space:]]+image: ${production_image}[[:space:]]*$" <<< "$rendered_config"; then
    fail "the rehearsal override did not take effect: ${production_image} is still the build target."
  fi
done

for rehearsal_image in "${REHEARSAL_IMAGES[@]}"; do
  grep -qE "^[[:space:]]+image: ${rehearsal_image}[[:space:]]*$" <<< "$rendered_config" \
    || fail "the rehearsal override does not assign ${rehearsal_image} to any service."
done

# `caddy` is an external network in the production overlay: on the VPS the reverse proxy owns it.
docker network create "$src_network" > /dev/null
docker network create "$dst_network" > /dev/null

# Stamped into the images by the Dockerfile, so a reused image can be matched against the checkout
# being verified rather than eyeballed from a timestamp. Git wins over an inherited value wherever
# there is git to ask; an explicit SOURCE_REVISION is honoured only for an exported tree, which is
# how this is run from a tarball.
# shellcheck source=scripts/lib/source-revision.sh
source scripts/lib/source-revision.sh
source_revision="$(resolve_source_revision)"

image_revision() {
  docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$1" 2>/dev/null || true
}

if [[ "${RESTORE_REHEARSAL_SKIP_BUILD:-false}" == "true" ]]; then
  # Tagging does not modify the source image, so reusing them this way still leaves the production
  # tags alone. The revision each image was built from is checked, not just printed: a rehearsal
  # that passed on an image built from some other commit has verified that commit, not this one,
  # and looks exactly like a real pass.
  echo "Reusing already-built images under rehearsal tags:"
  stale=0
  for index in "${!PRODUCTION_IMAGES[@]}"; do
    source_image="${PRODUCTION_IMAGES[$index]}"
    docker image inspect "$source_image" > /dev/null 2>&1 \
      || fail "RESTORE_REHEARSAL_SKIP_BUILD is set but ${source_image} does not exist. Build first."
    docker image tag "$source_image" "${REHEARSAL_IMAGES[$index]}"
    built_from="$(image_revision "$source_image")"
    printf '  %-26s %s revision %s\n' "$source_image" \
      "$(docker image inspect --format '{{.Id}}' "$source_image" | cut -c1-19)" \
      "${built_from:-<unlabelled>}"
    # An inexact revision - unknown, or a dirty tree - cannot identify what these images hold, so
    # reuse is refused rather than compared: two different dirty trees produce the same string.
    if ! source_revision_is_exact "$source_revision"; then
      stale=$((stale + 1))
    elif [[ "$built_from" != "$source_revision" ]]; then
      stale=$((stale + 1))
    fi
  done

  if [[ "$stale" -ne 0 && "${RESTORE_REHEARSAL_ALLOW_STALE_IMAGES:-false}" != "true" ]]; then
    fail "these images cannot be shown to have been built from ${source_revision}, so this run
      would verify some other source state. Rebuild, commit the working tree, or set
      RESTORE_REHEARSAL_ALLOW_STALE_IMAGES=true to accept it deliberately."
  fi
else
  echo "Building the application images under rehearsal tags (revision ${source_revision})..."
  SOURCE_REVISION="$source_revision" src_compose build api web worker migrate > /dev/null

  # The build arg has to reach every service. If one service's wiring is dropped from the Dockerfile
  # or from compose.yaml, that image goes out unlabelled and the SKIP_BUILD check above silently
  # stops being able to identify it - while the build path itself would still pass.
  for rehearsal_image in "${REHEARSAL_IMAGES[@]}"; do
    built_from="$(image_revision "$rehearsal_image")"
    [[ "$built_from" == "$source_revision" ]] \
      || fail "${rehearsal_image} is labelled '${built_from:-<none>}' but was built from ${source_revision}; the SOURCE_REVISION build argument is not reaching it."
  done
  echo "  all four images carry revision ${source_revision}."
fi

echo "Starting the source stack (${src_project})..."
src_compose up -d postgres redis > /dev/null
src_compose --profile tools run --rm migrate > /dev/null
src_compose up -d --wait --wait-timeout 180 api worker web > /dev/null

# --- real data and a real artifact ----------------------------------------------------------------

# Called from inside the api container: the production overlay publishes no ports, because on the
# VPS only Caddy reaches the API. This is also what the runbook's restore check describes.
api_curl() {
  local project="$1" env_file="$2"
  shift 2
  # --fail-with-body rather than --fail: when the API refuses something the envelope says why, and
  # a rehearsal reporting only "curl exited 22" would send the reader looking in the wrong place.
  docker compose --project-name "$project" --env-file "$env_file" \
    -f compose.yaml -f compose.production.yaml -f "$rehearsal_override" \
    exec -T api curl --silent --show-error --fail-with-body \
    --header "X-Service-Key: ${service_key}" "$@"
}

# JSON calls: the body is captured so a failure can be reported together with it.
api_call() {
  local project="$1" env_file="$2"
  shift 2
  local body status=0
  body="$(api_curl "$project" "$env_file" "$@" 2>&1)" || status=$?
  if [[ "$status" -ne 0 ]]; then
    fail "the API call failed (curl exited ${status}): ${body}"
  fi
  printf '%s' "$body"
}

src_api() {
  api_call "$src_project" "$src_env" "$@"
}

dst_api() {
  api_call "$dst_project" "$dst_env" "$@"
}

# The artifact is streamed straight to a file, so the bytes are compared exactly as they were
# stored. curl's own exit code is checked rather than left to `set -e`: a bare non-zero exit here
# ends the run with nothing but "exited 22", which says nothing about which refusal it hit.
fetch_artifact() {
  local project="$1" env_file="$2" url="$3" target="$4" status=0
  api_curl "$project" "$env_file" "$url" > "$target" 2> "$work/fetch-error" || status=$?
  if [[ "$status" -ne 0 ]]; then
    fail "fetching the artifact failed (curl exited ${status}): $(cat "$work/fetch-error")"
  fi
}

first_guid() {
  local field="$1"
  sed -nE "s/.*\"${field}\":\"([0-9a-fA-F-]{36})\".*/\1/p" | head -1
}

project_json="$(src_api \
  --header 'Content-Type: application/json' \
  --data '{"name":"Restore rehearsal","defaultLocation":"JP","defaultLanguage":"ja"}' \
  'http://localhost:8080/api/projects')"
project_id="$(first_guid projectId <<< "$project_json")"
[[ -n "$project_id" ]] || fail "the rehearsal project was not created: $project_json"

# Runs a real export end to end and echoes the export id: the API accepts it, the Worker picks it
# up, and the file lands in storage. Used on the source stack to produce something worth backing
# up, and again on the restored stack, where it is the only thing that shows the Worker can still
# do its job against restored state rather than merely start.
run_export() {
  local project="$1" env_file="$2"
  local job_json job_id job export_id="" details

  job_json="$(api_call "$project" "$env_file" \
    --header 'Content-Type: application/json' \
    --data '{"exportType":"keyword_metrics","format":"csv"}' \
    "http://localhost:8080/api/projects/${project_id}/exports/csv")"
  job_id="$(first_guid jobId <<< "$job_json")"
  [[ -n "$job_id" ]] || fail "the export job was not accepted: $job_json"

  for _ in $(seq 1 60); do
    job="$(api_call "$project" "$env_file" "http://localhost:8080/api/jobs/${job_id}")"
    case "$job" in
      *'"failed_fatal"'*|*'"failed_retryable"'*|*'"canceled"'*)
        fail "the export job did not succeed: $job"
        ;;
    esac
    export_id="$(first_guid resourceId <<< "$job")"
    if [[ -n "$export_id" ]]; then
      break
    fi
    sleep 2
  done
  [[ -n "$export_id" ]] || fail "the export job produced no artifact within two minutes."

  # The job names the export as soon as the row exists, which is before the file has been written.
  # The export's own status is what says the artifact is there; fetching earlier answers 409.
  for _ in $(seq 1 60); do
    details="$(api_call "$project" "$env_file" \
      "http://localhost:8080/api/projects/${project_id}/exports/${export_id}")"
    case "$details" in
      *'"status":"succeeded"'*)
        printf '%s' "$export_id"
        return 0
        ;;
      *'"status":"failed'*)
        fail "the export did not succeed: $details"
        ;;
    esac
    sleep 2
  done

  fail "the export did not complete within two minutes."
}

export_id="$(run_export "$src_project" "$src_env")"

fetch_artifact "$src_project" "$src_env" \
  "http://localhost:8080/api/projects/${project_id}/exports/${export_id}/content" \
  "$work/artifact-source.bin"
[[ -s "$work/artifact-source.bin" ]] || fail "the exported artifact was empty before the backup."
artifact_hash="$(sha256sum < "$work/artifact-source.bin" | cut -d' ' -f1)"

# The keys are what decrypts existing sign-in cookies. Restoring a database without them leaves an
# application that starts and then rejects every session.
keys_hash="$(src_compose exec -T web sh -c "cat ${keys_path}/key-*.xml" | sha256sum | cut -d' ' -f1)"

echo "Source stack seeded: project ${project_id}, export ${export_id}."

# --- the backup, taken by the production script ----------------------------------------------------

src_compose stop web api worker > /dev/null
BACKUP_PROJECT_NAME="$src_project" ENV_FILE="$src_env" PRODUCTION_LOCK_DIR="$lock_dir" \
  bash scripts/backup-production.sh "$repo_root/$work/backup"
backup_dir="$work/backup"

# --- restore into a second, empty project -----------------------------------------------------------

echo "Restoring into an empty stack (${dst_project})..."

# `create` rather than `up`: it makes the volumes with the labels Compose expects, without starting
# an application against a database that has not been restored yet.
dst_compose create postgres redis api worker web > /dev/null

restore_volume() {
  local volume="$1" archive="$2"
  docker run --rm -i --network none --entrypoint tar \
    --volume "${volume}:/target" "$(tar_image)" \
    -C /target -xzf - < "${backup_dir}/${archive}"
}

restore_volume "${dst_project}_seo-storage" seo-storage.tar.gz
restore_volume "${dst_project}_web-data-protection" web-data-protection.tar.gz

dst_compose up -d --wait --wait-timeout 120 postgres redis > /dev/null
dst_compose exec -T postgres sh -c \
  'pg_restore --no-owner --no-privileges --exit-on-error --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"' \
  < "${backup_dir}/postgres.dump"

# The restored schema is already at the backup's version, so this has to be a no-op. `migrate`
# exits 0 either way - applying a pending migration is a success as far as it is concerned - so the
# history table is counted on both sides. A dump that needs migrating is a dump that has drifted
# from the code, and restoring it would silently change the schema during a recovery.
migration_count() {
  dst_compose exec -T postgres psql -tAq -U "$postgres_user" -d "$postgres_db" \
    -c 'select count(*) from "__EFMigrationsHistory"' | tr -d '[:space:]'
}

migrations_before="$(migration_count)"
[[ "$migrations_before" =~ ^[0-9]+$ && "$migrations_before" -gt 0 ]] \
  || fail "the restored database has no migration history (got '${migrations_before}')."

dst_compose --profile tools run --rm migrate > /dev/null

migrations_after="$(migration_count)"
[[ "$migrations_after" == "$migrations_before" ]] \
  || fail "migrating the restored database changed the history count from ${migrations_before} to ${migrations_after}; the dump and the code have drifted."

dst_compose up -d --wait --wait-timeout 180 api worker web > /dev/null

# --- what the restored stack has to be able to do -----------------------------------------------

# /readyz fails while migrations are pending, so this also covers the no-op migration above.
readyz_status=0
readyz="$(api_curl "$dst_project" "$dst_env" 'http://localhost:8080/readyz' 2>&1)" || readyz_status=$?
[[ "$readyz_status" -eq 0 ]] \
  || fail "the restored API never became ready (curl exited ${readyz_status}): ${readyz}"

restored_project="$(dst_api "http://localhost:8080/api/projects/${project_id}")"
grep -q 'Restore rehearsal' <<< "$restored_project" \
  || fail "the restored database does not have the rehearsal project: $restored_project"

fetch_artifact "$dst_project" "$dst_env" \
  "http://localhost:8080/api/projects/${project_id}/exports/${export_id}/content" \
  "$work/artifact-restored.bin"
restored_hash="$(sha256sum < "$work/artifact-restored.bin" | cut -d' ' -f1)"
[[ "$restored_hash" == "$artifact_hash" ]] \
  || fail "the restored artifact differs from the one that was backed up ($restored_hash != $artifact_hash)."

restored_keys_hash="$(dst_compose exec -T web sh -c "cat ${keys_path}/key-*.xml" | sha256sum | cut -d' ' -f1)"
[[ "$restored_keys_hash" == "$keys_hash" ]] \
  || fail "the Data Protection keys were not restored; existing sessions would be rejected."

# Reading an old artifact only shows the restored state can be read. A recovered deployment has to
# be able to work: this queues a new export on the restored stack and waits for the Worker to
# finish it, which exercises Hangfire's restored tables, the database and the storage volume.
new_export_id="$(run_export "$dst_project" "$dst_env")"
fetch_artifact "$dst_project" "$dst_env" \
  "http://localhost:8080/api/projects/${project_id}/exports/${new_export_id}/content" \
  "$work/artifact-new.bin"
[[ -s "$work/artifact-new.bin" ]] \
  || fail "the restored Worker produced an empty artifact."

echo "Restore rehearsal succeeded: the database, the stored artifact and the Data Protection keys"
echo "came back together, the artifact reads byte-for-byte through the API, the restored Worker"
echo "completed a new export, and the production image tags are unchanged."
