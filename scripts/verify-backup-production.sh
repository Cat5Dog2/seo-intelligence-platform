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
# `docker` is replaced with a stub on PATH. That keeps the test about the script's own logic - path
# handling, artifact validation, refusal conditions - and needs no database.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"

work="artifacts/backup-test-$$-${RANDOM}"
mkdir -p "$work/bin"
trap 'rm -rf "$work"' EXIT

failures=0

# Writes a `docker` stub. Behaviour is driven by files the test creates, so each case configures
# the world rather than editing the stub.
cat > "$work/bin/docker" <<'STUB'
#!/usr/bin/env bash
set -uo pipefail
state="${DOCKER_STUB_STATE:?DOCKER_STUB_STATE is required}"

case "$1" in
  compose)
    # The only compose call is the pg_dump; emit whatever the case asked for.
    cat "$state/pgdump"
    exit 0
    ;;
  volume)
    # `volume inspect <name>`
    grep -qxF "$3" "$state/volumes" && exit 0
    exit 1
    ;;
  run)
    # Either creating an archive or listing one. The last argument distinguishes them.
    for argument in "$@"; do
      case "$argument" in
        -czf) mode=create ;;
        -tzf) mode=list ;;
      esac
    done

    output_dir="$(grep -oE '\-\-volume [^ ]*:/backup' <<< "$*" | head -1 | sed -e 's|--volume ||' -e 's|:/backup||')"
    archive="$(sed -E 's|.*/backup/([^ ]+).*|\1|' <<< "$*")"

    if [[ "${mode:-}" == "create" ]]; then
      # A real tar of an empty directory is a valid, non-empty file; mimic that.
      printf 'archive-of-%s' "$archive" > "$output_dir/$archive"
      exit 0
    fi

    cat "$state/entries-$archive" 2>/dev/null || true
    exit 0
    ;;
esac
exit 0
STUB
chmod +x "$work/bin/docker"

# Sets up a world where everything is healthy, then lets the caller break one thing.
new_state() {
  local state="$repo_root/$work/state-$1"
  rm -rf "$state"
  mkdir -p "$state"
  printf 'PGDMP\x00fake dump body' > "$state/pgdump"
  printf 'seo-intelligence-prod_seo-storage\nseo-intelligence-prod_web-data-protection\n' > "$state/volumes"
  printf './\nexports/report.pdf\n' > "$state/entries-seo-storage.tar.gz"
  printf './\nkey-abc.xml\n' > "$state/entries-web-data-protection.tar.gz"
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

# A healthy run, and the artifacts it must leave behind.
state="$(new_state healthy)"
expect_success "a healthy backup completes" "$state" "$repo_root/$work/out-healthy"
for artifact in postgres.dump seo-storage.tar.gz web-data-protection.tar.gz; do
  if [[ ! -s "$work/out-healthy/$artifact" ]]; then
    echo "FAIL: the healthy backup did not produce $artifact." >&2
    failures=$((failures + 1))
  fi
done

# A relative output directory has to become absolute before it reaches `docker run -v`, which
# treats a source that does not start with / or ./ as a named volume and rejects one with a slash.
state="$(new_state relative)"
relative_output="$work/out-relative"
if run_backup "$state" "$relative_output" > "$work/out" 2>&1; then
  if grep -qE -- "--volume [^ /][^ ]*:/backup" "$work/out"; then
    echo "FAIL: a relative output directory reached docker run as a relative path." >&2
    failures=$((failures + 1))
  else
    echo "ok: a relative output directory is made absolute."
  fi
else
  echo "FAIL: the backup failed with a relative output directory." >&2
  sed 's/^/      /' "$work/out" >&2
  failures=$((failures + 1))
fi

# Refusals.
state="$(new_state existing)"
mkdir -p "$work/out-existing"
expect_refusal "an output directory that already exists" \
  "already exists" "$state" "$repo_root/$work/out-existing"

state="$(new_state missing-volume)"
printf 'seo-intelligence-prod_seo-storage\n' > "$state/volumes"
expect_refusal "a missing volume" \
  "does not exist" "$state" "$repo_root/$work/out-missing-volume"

state="$(new_state bad-dump)"
printf 'ERROR:  relation does not exist' > "$state/pgdump"
expect_refusal "a dump that is not in PostgreSQL custom format" \
  "is not a PostgreSQL custom-format dump" "$state" "$repo_root/$work/out-bad-dump"

# An archive of an empty volume is a valid, non-empty file. Size alone would accept it, and the
# Data Protection keys are exactly the case where an empty backup is worthless: without them no
# existing session or antiforgery token survives a restore.
state="$(new_state empty-keys)"
printf './\n' > "$state/entries-web-data-protection.tar.gz"
expect_refusal "an empty Data Protection key archive" \
  "expected at least 1" "$state" "$repo_root/$work/out-empty-keys"

# Storage may legitimately be empty on a fresh deployment, so that must NOT be refused.
state="$(new_state empty-storage)"
printf './\n' > "$state/entries-seo-storage.tar.gz"
expect_success "an empty storage volume is accepted" "$state" "$repo_root/$work/out-empty-storage"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures backup check(s) failed." >&2
  exit 1
fi

echo "The production backup refuses every failure it is meant to catch."
