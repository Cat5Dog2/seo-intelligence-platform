#!/usr/bin/env bash
# Tests scripts/backup-production.sh against a stubbed Docker.
#
#   bash scripts/verify-backup-production.sh
#
# The backup runs at the one moment where getting it wrong is unrecoverable: the application is
# stopped and migrations are about to change the schema. Every check it makes exists because the
# corresponding failure looks like success until a restore is attempted, so each one is asserted
# here rather than trusted.
#
# `docker` is replaced with a stub on PATH that records the arguments it was called with. That
# keeps the test about the script's own logic - path handling, artifact validation, refusal
# conditions - and lets assertions look at what would actually have been run.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"

work="artifacts/backup-test-$$-${RANDOM}"
mkdir -p "$work/bin"
trap 'rm -rf "$work"' EXIT

failures=0

# Behaviour is driven by files the test creates, so each case configures the world rather than
# editing the stub. Every invocation is appended to $DOCKER_STUB_STATE/argv.
cat > "$work/bin/docker" <<'STUB'
#!/usr/bin/env bash
set -uo pipefail
state="${DOCKER_STUB_STATE:?DOCKER_STUB_STATE is required}"
printf '%s\n' "$*" >> "$state/argv"

case "$1" in
  compose)
    # `compose ps --status running --services` or the pg_dump.
    if [[ "$*" == *" ps "* ]]; then
      cat "$state/running" 2>/dev/null || true
      exit 0
    fi
    cat "$state/pgdump"
    exit 0
    ;;
  volume)
    grep -qxF "$3" "$state/volumes" && exit 0
    exit 1
    ;;
  run)
    for argument in "$@"; do
      case "$argument" in
        pg_restore) mode=restore ;;
        -czf) mode=create ;;
        -tzvf) mode=list ;;
      esac
    done

    output_dir="$(grep -oE '\-\-volume [^ ]*:/backup(:ro)?' <<< "$*" | head -1 | sed -e 's|--volume ||' -e 's|:/backup||' -e 's|:ro$||')"

    case "${mode:-}" in
      restore)
        cat "$state/restore-list" 2>/dev/null
        exit "$(cat "$state/restore-status" 2>/dev/null || echo 0)"
        ;;
      create)
        archive="$(sed -E 's|.*-czf /backup/([^ ]+).*|\1|' <<< "$*")"
        # A real tar of an empty directory is a valid, non-empty file; mimic that.
        printf 'archive-of-%s' "$archive" > "$output_dir/$archive"
        exit 0
        ;;
      list)
        archive="$(sed -E 's|.*-tzvf /backup/([^ ]+).*|\1|' <<< "$*")"
        cat "$state/listing-$archive" 2>/dev/null
        exit "$(cat "$state/list-status-$archive" 2>/dev/null || echo 0)"
        ;;
    esac
    exit 0
    ;;
esac
exit 0
STUB
chmod +x "$work/bin/docker"

# A world where everything is healthy; each case then breaks one thing.
new_state() {
  local state="$repo_root/$work/state-$1"
  rm -rf "$state"
  mkdir -p "$state"
  : > "$state/argv"
  : > "$state/running"
  printf 'PGDMP\x00fake dump body' > "$state/pgdump"
  printf 'seo-intelligence-prod_seo-storage\nseo-intelligence-prod_web-data-protection\n' > "$state/volumes"
  printf ';\n; Archive created at 2026-08-22\n;\n215; 1259 16480 TABLE public projects seo\n216; 1259 16490 TABLE public jobs seo\n' > "$state/restore-list"
  printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n-rw-r--r-- 0/0 120 2026-08-22 00:00 ./exports/report.pdf\n' > "$state/listing-seo-storage.tar.gz"
  printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n-rw-r--r-- 0/0 900 2026-08-22 00:00 ./key-8f3c.xml\n' > "$state/listing-web-data-protection.tar.gz"
  echo "$state"
}

run_backup() {
  local state="$1" output="$2"
  PATH="$repo_root/$work/bin:$PATH" \
  DOCKER_STUB_STATE="$state" \
  ENV_FILE=.env.production.example \
    bash scripts/backup-production.sh "$output" 2>&1
}

expect_success() {
  local description="$1" state="$2" output="$3"
  if run_backup "$state" "$output" > "$work/out" 2>&1; then
    echo "ok: $description"
  else
    echo "FAIL: $description" >&2
    sed 's/^/      /' "$work/out" >&2
    failures=$((failures + 1))
  fi
}

expect_refusal() {
  local description="$1" expected="$2" state="$3" output="$4"
  local out
  if out="$(run_backup "$state" "$output")"; then
    echo "FAIL: the backup succeeded despite $description." >&2
    failures=$((failures + 1))
    return
  fi

  if ! grep -qF -- "$expected" <<< "$out"; then
    echo "FAIL: $description was refused, but not for the expected reason." >&2
    echo "      expected to contain: $expected" >&2
    sed 's/^/      /' <<< "$out" >&2
    failures=$((failures + 1))
    return
  fi

  echo "ok: $description is refused."
}

# --- the healthy path ----------------------------------------------------------------------------

state="$(new_state healthy)"
expect_success "a healthy backup completes" "$state" "$repo_root/$work/out-healthy"
for artifact in postgres.dump seo-storage.tar.gz web-data-protection.tar.gz; do
  if [[ ! -s "$work/out-healthy/$artifact" ]]; then
    echo "FAIL: the healthy backup did not produce $artifact." >&2
    failures=$((failures + 1))
  fi
done

# The dump is read back with pg_restore, not sniffed for a magic string.
if ! grep -q -- "pg_restore" "$state/argv"; then
  echo "FAIL: the backup did not verify the dump with pg_restore." >&2
  failures=$((failures + 1))
else
  echo "ok: the dump is read back with pg_restore."
fi

# --- path handling -------------------------------------------------------------------------------

# `docker run -v` treats a source that does not start with / or ./ as a named volume, and rejects
# one containing a slash. Asserted on the arguments the stub recorded, because the script's own
# output would look identical either way.
state="$(new_state relative)"
if run_backup "$state" "$work/out-relative" > "$work/out" 2>&1; then
  if grep -oE -- '--volume [^ ]*:/backup' "$state/argv" | grep -qvE -- '--volume /'; then
    echo "FAIL: a relative output directory reached docker run as a relative path:" >&2
    grep -oE -- '--volume [^ ]*:/backup' "$state/argv" | grep -vE -- '--volume /' | sed 's/^/        /' >&2
    failures=$((failures + 1))
  else
    echo "ok: a relative output directory is made absolute before it reaches docker."
  fi
else
  echo "FAIL: the backup failed with a relative output directory." >&2
  sed 's/^/      /' "$work/out" >&2
  failures=$((failures + 1))
fi

# --- refusals ------------------------------------------------------------------------------------

state="$(new_state running)"
printf 'api\nworker\n' > "$state/running"
expect_refusal "the application still running" \
  "still running" "$state" "$repo_root/$work/out-running"

state="$(new_state existing)"
mkdir -p "$work/out-existing"
expect_refusal "an output directory that already exists" \
  "already exists" "$state" "$repo_root/$work/out-existing"

state="$(new_state missing-volume)"
printf 'seo-intelligence-prod_seo-storage\n' > "$state/volumes"
expect_refusal "a missing volume" \
  "does not exist" "$state" "$repo_root/$work/out-missing-volume"

# A truncated custom-format dump still starts with PGDMP, so only a real read catches it.
state="$(new_state unreadable-dump)"
echo 1 > "$state/restore-status"
printf 'pg_restore: error: did not find magic string in file header' > "$state/restore-list"
expect_refusal "a dump pg_restore cannot read" \
  "cannot be read by pg_restore" "$state" "$repo_root/$work/out-unreadable-dump"

state="$(new_state empty-dump)"
printf ';\n; Archive created at 2026-08-22\n;\n' > "$state/restore-list"
expect_refusal "a dump that contains no objects" \
  "lists no objects" "$state" "$repo_root/$work/out-empty-dump"

# tar failing has to fail the backup. Storage accepts zero files, so a counter that treated a failed
# listing as "no entries" would let a corrupt archive through.
state="$(new_state corrupt-storage)"
echo 2 > "$state/list-status-seo-storage.tar.gz"
printf 'tar: Unexpected EOF in archive' > "$state/listing-seo-storage.tar.gz"
expect_refusal "a storage archive that cannot be read back" \
  "could not be read back" "$state" "$repo_root/$work/out-corrupt-storage"

state="$(new_state corrupt-keys)"
echo 2 > "$state/list-status-web-data-protection.tar.gz"
expect_refusal "a Data Protection archive that cannot be read back" \
  "could not be read back" "$state" "$repo_root/$work/out-corrupt-keys"

# An archive of an empty volume is a valid, non-empty file, and the Data Protection keys are
# exactly the case where an empty backup is worthless: without them no existing session survives.
state="$(new_state empty-keys)"
printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n' > "$state/listing-web-data-protection.tar.gz"
expect_refusal "an empty Data Protection key archive" \
  "expected at least 1" "$state" "$repo_root/$work/out-empty-keys"

# A directory entry is not a key. Counting entries rather than files would accept this.
state="$(new_state keys-dir-only)"
printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\ndrwxr-xr-x 0/0 0 2026-08-22 00:00 ./nested/\n' \
  > "$state/listing-web-data-protection.tar.gz"
expect_refusal "a Data Protection archive holding only directories" \
  "expected at least 1" "$state" "$repo_root/$work/out-keys-dir-only"

# Files, but not key files.
state="$(new_state keys-wrong-file)"
printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n-rw-r--r-- 0/0 12 2026-08-22 00:00 ./readme.txt\n' \
  > "$state/listing-web-data-protection.tar.gz"
expect_refusal "a Data Protection archive with no key file" \
  "no file matching" "$state" "$repo_root/$work/out-keys-wrong-file"

# --- what must NOT be refused ---------------------------------------------------------------------

# Storage may legitimately be empty on a fresh deployment.
state="$(new_state empty-storage)"
printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n' > "$state/listing-seo-storage.tar.gz"
expect_success "an empty storage volume is accepted" "$state" "$repo_root/$work/out-empty-storage"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures backup check(s) failed." >&2
  exit 1
fi

echo "The production backup refuses every failure it is meant to catch."
