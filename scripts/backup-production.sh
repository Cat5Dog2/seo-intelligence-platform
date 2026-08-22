#!/usr/bin/env bash
# Takes a production backup: the database, the shared storage volume, and the Data Protection keys.
#
#   bash scripts/backup-production.sh [output directory]
#
# Called by scripts/deploy-production.sh while the application is stopped and before migrations
# run, and usable on its own for an ad-hoc backup. The three artifacts belong to the same point in
# time, so they are taken together: restoring a database without its storage, or without the Data
# Protection keys, leaves an application that starts but cannot read its own artifacts or cookies.
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT_NAME="seo-intelligence-prod"
ENV_FILE="${ENV_FILE:-.env.production}"

# --project-name is passed explicitly because a COMPOSE_PROJECT_NAME in the environment overrides
# `name:` in compose.production.yaml, and would send this at another stack's database.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f compose.yaml -f compose.production.yaml)

# The image used only as a tar host. Pinned from the lock file so this uses an image whose
# vulnerabilities were reviewed, rather than pulling a fresh one.
tar_image() {
  local entry
  entry="$(grep -E "^postgres$(printf '\t')" image-digests.lock)"
  printf '%s@%s' "$(cut -f2 <<< "$entry")" "$(cut -f3 <<< "$entry")"
}

# Absolute, because `docker run -v` treats a source that does not start with / or ./ as a named
# volume, and a name containing a slash is rejected outright. A relative path here would fail the
# backup - and on an update that happens after the application has already been stopped.
output_dir="${1:-$PWD/backups/$(date -u +%Y%m%dT%H%M%SZ)}"
case "$output_dir" in
  /*) ;;
  *) output_dir="$PWD/$output_dir" ;;
esac

if [[ -e "$output_dir" ]]; then
  echo "ERROR: $output_dir already exists. Backups are never written over an existing directory." >&2
  exit 1
fi

mkdir -p "$output_dir"

archive_volume() {
  local volume="$1" archive="$2"

  if ! docker volume inspect "$volume" > /dev/null 2>&1; then
    echo "ERROR: volume $volume does not exist. Refusing to write an empty archive that would look" >&2
    echo "       like a backup. Check --project-name and that the stack has been deployed." >&2
    return 1
  fi

  MSYS_NO_PATHCONV=1 docker run --rm --entrypoint tar \
    --volume "${volume}:/source:ro" \
    --volume "${output_dir}:/backup" \
    "$(tar_image)" \
    -C /source -czf "/backup/${archive}" .
}

# An archive of an empty directory is still a valid, non-empty file, so size alone would accept a
# backup with nothing in it. This counts real entries instead.
assert_archive_has_entries() {
  local archive="$1" minimum="$2" entries
  entries="$(MSYS_NO_PATHCONV=1 docker run --rm --entrypoint tar \
    --volume "${output_dir}:/backup:ro" \
    "$(tar_image)" \
    -tzf "/backup/${archive}" | grep -cvE '^\./?$' || true)"

  if [[ "$entries" -lt "$minimum" ]]; then
    echo "ERROR: ${archive} contains $entries file(s), expected at least $minimum." >&2
    echo "       An archive of an empty volume is not a backup." >&2
    return 1
  fi

  echo "  ${archive}: ${entries} entr(y|ies)"
}

echo "Backing up ${PROJECT_NAME} to ${output_dir}"

"${COMPOSE[@]}" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$output_dir/postgres.dump"

# pg_dump's custom format starts with the magic string PGDMP. A truncated or error-only dump does
# not, and would otherwise sit there looking like a backup until a restore was attempted.
if [[ "$(head -c 5 "$output_dir/postgres.dump")" != "PGDMP" ]]; then
  echo "ERROR: $output_dir/postgres.dump is not a PostgreSQL custom-format dump." >&2
  exit 1
fi
echo "  postgres.dump: $(wc -c < "$output_dir/postgres.dump") bytes"

archive_volume "${PROJECT_NAME}_seo-storage" seo-storage.tar.gz
archive_volume "${PROJECT_NAME}_web-data-protection" web-data-protection.tar.gz

# Storage can legitimately be empty on a fresh deployment; the Data Protection keys cannot, because
# the Web host writes one on first start and every signed-in session depends on them.
assert_archive_has_entries seo-storage.tar.gz 0
assert_archive_has_entries web-data-protection.tar.gz 1

echo "Backup complete: ${output_dir}"
echo "Transfer it off the VPS to encrypted storage; it is not a backup while it lives on the same host."
