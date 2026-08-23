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
# Every artifact is read back in full after it is written. A backup that cannot be restored is
# worse than no backup, because it is only discovered to be worthless at the moment it is needed.
set -euo pipefail

# Captured before the cd so a relative output directory resolves against the caller's working
# directory, which is where they meant it, rather than against the repository root.
invocation_dir="$PWD"

cd "$(dirname "$0")/.."

# The dump contains the entire database and the key archive contains the Data Protection keys,
# which this deployment stores unencrypted on the filesystem. Neither may be world-readable.
umask 077

PROJECT_NAME="seo-intelligence-prod"
ENV_FILE="${ENV_FILE:-.env.production}"

# --project-name is passed explicitly because a COMPOSE_PROJECT_NAME in the environment overrides
# `name:` in compose.production.yaml, and would send this at another stack's database.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f compose.yaml -f compose.production.yaml)

# The same lock the deployment takes. Run on its own this stops a backup from interleaving with a
# deployment; run from deploy-production.sh the marker makes it a no-op rather than a deadlock.
# shellcheck source=scripts/lib/production-lock.sh
source scripts/lib/production-lock.sh
acquire_production_lock "$PROJECT_NAME" || exit 1

# The image used as a tar and pg_restore host. Pinned from the lock file so this uses an image whose
# vulnerabilities were reviewed, rather than pulling a fresh one.
tar_image() {
  local entry
  # Carriage returns are stripped because a Windows checkout stores the lock file with CRLF, and a
  # trailing CR would produce an image reference no registry recognises.
  entry="$(tr -d '\r' < image-digests.lock | grep -E "^postgres$(printf '\t')")"
  printf '%s@%s' "$(cut -f2 <<< "$entry")" "$(cut -f3 <<< "$entry")"
}

# Resolved to an absolute path against the caller's directory. This script cd's to the repository
# root, so a relative path left as-is would silently land somewhere the caller did not choose, and
# the "Backing up to ..." line would not say where the backup actually went.
output_dir="${1:-$PWD/backups/$(date -u +%Y%m%dT%H%M%SZ)}"
case "$output_dir" in
  /*) ;;
  *) output_dir="$invocation_dir/$output_dir" ;;
esac

# The dump and the storage archive have to describe the same moment. Taken while the application is
# running they will not: a job can write a file between the two, or commit a row after the dump.
# deploy-production.sh stops the services first; a direct invocation has to have done the same.
#
# Fail closed: if the query itself fails there is no evidence the application is stopped, and
# treating that as "nothing running" would be the wrong way to be wrong.
ps_status=0
running="$("${COMPOSE[@]}" ps --status running --services 2>&1)" || ps_status=$?
if [[ "$ps_status" -ne 0 ]]; then
  echo "ERROR: could not determine which services are running (compose ps exited ${ps_status})." >&2
  sed 's/^/       /' <<< "$running" >&2
  exit 1
fi

# migrate is included: a migration in flight means the schema is changing under the dump.
running="$(grep -E '^(web|api|worker|migrate)$' <<< "$running" || true)"
if [[ -n "$running" ]]; then
  echo "ERROR: these services are still running: $(tr '\n' ' ' <<< "$running")" >&2
  echo "       Stop web, api, worker and any running migrate first, or the dump and the storage" >&2
  echo "       archive will describe different points in time." >&2
  exit 1
fi

# `mkdir -p` on the parent, plain `mkdir` on the leaf: the leaf has to fail if it already exists.
# A test-then-create would let two deployments started in the same second both pass the test and
# then write over each other's artifacts.
mkdir -p "$(dirname "$output_dir")"
if ! mkdir "$output_dir" 2>/dev/null; then
  echo "ERROR: $output_dir already exists or could not be created." >&2
  echo "       Backups are never written over an existing directory." >&2
  exit 1
fi

# Written through the container's stdout rather than a bind mount. A container writing into a bind
# mount creates the file as root with the container's umask; redirecting here keeps it owned by the
# deploying user and covered by the umask set above.
archive_volume() {
  local volume="$1" archive="$2"

  if ! docker volume inspect "$volume" > /dev/null 2>&1; then
    echo "ERROR: volume $volume does not exist. Refusing to write an empty archive that would look" >&2
    echo "       like a backup. Check --project-name and that the stack has been deployed." >&2
    return 1
  fi

  MSYS_NO_PATHCONV=1 docker run --rm --network none --entrypoint tar \
    --volume "${volume}:/source:ro" \
    "$(tar_image)" \
    -C /source -czf - . > "${output_dir}/${archive}"
}

# The listing is captured before it is counted. Piping straight into a counter would report zero
# both for an empty archive and for a `tar` that failed on a truncated one - and zero is an
# acceptable count for storage, so a corrupt archive would pass.
list_archive() {
  local archive="$1" listing status=0

  listing="$(MSYS_NO_PATHCONV=1 docker run --rm -i --network none --entrypoint tar \
    "$(tar_image)" \
    -tzvf - < "${output_dir}/${archive}" 2>&1)" || status=$?

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

# Two passes, because they answer different questions.
#
# --list reads only the table of contents. A dump truncated anywhere after the TOC still lists its
# objects and exits 0 - measured against a real dump cut to 3 kB, which reported three objects and
# exited 0. --file=/dev/null decompresses and walks every data member, so that is what proves the
# archive is complete. --network none because nothing here should reach a database.
dump_status=0
dump_toc="$(MSYS_NO_PATHCONV=1 docker run --rm -i --network none --entrypoint pg_restore \
  "$(tar_image)" \
  --list < "$output_dir/postgres.dump" 2>&1)" || dump_status=$?

if [[ "$dump_status" -ne 0 ]]; then
  echo "ERROR: $output_dir/postgres.dump has no readable table of contents (exit ${dump_status})." >&2
  sed 's/^/       /' <<< "$dump_toc" >&2
  exit 1
fi

dump_objects="$(grep -cvE '^;|^[[:space:]]*$' <<< "$dump_toc" || true)"
if [[ "$dump_objects" -lt 1 ]]; then
  echo "ERROR: $output_dir/postgres.dump lists no objects; it captured nothing." >&2
  exit 1
fi

scan_status=0
scan_output="$(MSYS_NO_PATHCONV=1 docker run --rm -i --network none --entrypoint pg_restore \
  "$(tar_image)" \
  --file=/dev/null < "$output_dir/postgres.dump" 2>&1)" || scan_status=$?

if [[ "$scan_status" -ne 0 ]]; then
  echo "ERROR: $output_dir/postgres.dump is incomplete; pg_restore could not read it through to" >&2
  echo "       the end (exit ${scan_status}). It would fail partway through a restore." >&2
  sed 's/^/       /' <<< "$scan_output" >&2
  exit 1
fi
echo "  postgres.dump: $(wc -c < "$output_dir/postgres.dump") bytes, ${dump_objects} object(s), fully readable"

archive_volume "${PROJECT_NAME}_seo-storage" seo-storage.tar.gz
archive_volume "${PROJECT_NAME}_web-data-protection" web-data-protection.tar.gz

# Storage can legitimately be empty on a fresh deployment; the Data Protection keys cannot, because
# the Web host writes one on first start and every signed-in session depends on them. The key
# repository names each file key-{guid}.xml, so that is what has to be in there - any .xml would
# also match a stray file that is not a key.
assert_archive_has_files seo-storage.tar.gz 0
assert_archive_has_files web-data-protection.tar.gz 1 '(^|/)key-[^/]+[.]xml$'

echo "Backup complete: ${output_dir}"
echo "Transfer it off the VPS to encrypted storage; it is not a backup while it lives on the same host."
