#!/usr/bin/env bash
# Takes a production backup: the database, the shared storage volume, and the Data Protection keys.
#
#   bash scripts/backup-production.sh [output directory]
#
# Called by scripts/deploy-production.sh while the application is stopped and before migrations
# run, and usable on its own for an ad-hoc backup. The three artifacts belong to the same point in
# time, so they are taken together: restoring a database without its storage, or without the Data
# Protection keys, leaves an application that starts but cannot read its own artifacts or cookies.
#
# Every artifact is read back after it is written. A backup that cannot be restored is worse than
# no backup, because it is only discovered to be worthless at the moment it is needed.
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT_NAME="seo-intelligence-prod"
ENV_FILE="${ENV_FILE:-.env.production}"

# --project-name is passed explicitly because a COMPOSE_PROJECT_NAME in the environment overrides
# `name:` in compose.production.yaml, and would send this at another stack's database.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f compose.yaml -f compose.production.yaml)

# The image used as a tar and pg_restore host. Pinned from the lock file so this uses an image whose
# vulnerabilities were reviewed, rather than pulling a fresh one.
tar_image() {
  local entry
  # Carriage returns are stripped because a Windows checkout stores the lock file with CRLF, and a
  # trailing CR would produce an image reference no registry recognises.
  entry="$(tr -d '\r' < image-digests.lock | grep -E "^postgres$(printf '\t')")"
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

# The dump and the storage archive have to describe the same moment. Taken while the application is
# running they will not: a job can write a file between the two, or commit a row after the dump.
# deploy-production.sh stops the services first; a direct invocation has to have done the same.
running="$("${COMPOSE[@]}" ps --status running --services 2>/dev/null | grep -E '^(web|api|worker)$' || true)"
if [[ -n "$running" ]]; then
  echo "ERROR: these services are still running: $(tr '\n' ' ' <<< "$running")" >&2
  echo "       Stop web, api and worker first, or the dump and the storage archive will describe" >&2
  echo "       different points in time." >&2
  exit 1
fi

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

# The listing is captured before it is counted. Piping straight into a counter would report zero
# both for an empty archive and for a `tar` that failed on a truncated one - and zero is an
# acceptable count for storage, so a corrupt archive would pass.
list_archive() {
  local archive="$1" listing status=0

  listing="$(MSYS_NO_PATHCONV=1 docker run --rm --network none --entrypoint tar \
    --volume "${output_dir}:/backup:ro" \
    "$(tar_image)" \
    -tzvf "/backup/${archive}" 2>&1)" || status=$?

  if [[ "$status" -ne 0 ]]; then
    echo "ERROR: ${archive} could not be read back (tar exited ${status}). It is not a backup." >&2
    sed 's/^/       /' <<< "$listing" >&2
    return 1
  fi

  printf '%s' "$listing"
}

# Regular files only: `tar -tzv` prefixes a directory line with 'd', and an archive holding nothing
# but an empty subdirectory would otherwise look like it had contents.
assert_archive_has_files() {
  local archive="$1" minimum="$2" pattern="${3:-}" listing files

  listing="$(list_archive "$archive")" || return 1
  files="$(grep -cE '^-' <<< "$listing" || true)"

  if [[ "$files" -lt "$minimum" ]]; then
    echo "ERROR: ${archive} contains ${files} file(s), expected at least ${minimum}." >&2
    echo "       An archive of an empty volume is not a backup." >&2
    return 1
  fi

  if [[ -n "$pattern" ]] && ! grep -E '^-' <<< "$listing" | grep -qE "$pattern"; then
    echo "ERROR: ${archive} contains no file matching ${pattern}." >&2
    return 1
  fi

  echo "  ${archive}: ${files} file(s)"
}

echo "Backing up ${PROJECT_NAME} to ${output_dir}"

"${COMPOSE[@]}" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$output_dir/postgres.dump"

# Read back with pg_restore rather than checked for a magic string. `PGDMP` is also the first five
# bytes of a truncated dump, and a dump that cannot be listed cannot be restored - which is the
# only thing this file is for. --network none because nothing here should reach a database.
dump_status=0
dump_toc="$(MSYS_NO_PATHCONV=1 docker run --rm --network none --entrypoint pg_restore \
  --volume "${output_dir}:/backup:ro" \
  "$(tar_image)" \
  --list /backup/postgres.dump 2>&1)" || dump_status=$?

if [[ "$dump_status" -ne 0 ]]; then
  echo "ERROR: $output_dir/postgres.dump cannot be read by pg_restore (exit ${dump_status})." >&2
  sed 's/^/       /' <<< "$dump_toc" >&2
  exit 1
fi

# A dump of nothing lists no objects. Every deployment has schema, so an empty table of contents
# means the dump did not capture the database.
dump_objects="$(grep -cvE '^;|^[[:space:]]*$' <<< "$dump_toc" || true)"
if [[ "$dump_objects" -lt 1 ]]; then
  echo "ERROR: $output_dir/postgres.dump lists no objects; it captured nothing." >&2
  exit 1
fi
echo "  postgres.dump: $(wc -c < "$output_dir/postgres.dump") bytes, ${dump_objects} object(s)"

archive_volume "${PROJECT_NAME}_seo-storage" seo-storage.tar.gz
archive_volume "${PROJECT_NAME}_web-data-protection" web-data-protection.tar.gz

# Storage can legitimately be empty on a fresh deployment; the Data Protection keys cannot, because
# the Web host writes one on first start and every signed-in session depends on them. The key
# repository stores each key as its own XML file, so that is what has to be in there.
assert_archive_has_files seo-storage.tar.gz 0
assert_archive_has_files web-data-protection.tar.gz 1 '[.]xml$'

echo "Backup complete: ${output_dir}"
echo "Transfer it off the VPS to encrypted storage; it is not a backup while it lives on the same host."
