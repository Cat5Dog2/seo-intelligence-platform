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
      exit "$(cat "$state/ps-status" 2>/dev/null || echo 0)"
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
        --list) restore_mode=list ;;
        --file=/dev/null) restore_mode=scan ;;
      esac
    done

    case "${mode:-}" in
      restore)
        # The dump arrives on stdin; drain it so the writer does not block.
        cat > /dev/null
        if [[ "${restore_mode:-}" == "scan" ]]; then
          cat "$state/scan-output" 2>/dev/null
          exit "$(cat "$state/scan-status" 2>/dev/null || echo 0)"
        fi
        cat "$state/restore-list" 2>/dev/null
        exit "$(cat "$state/restore-status" 2>/dev/null || echo 0)"
        ;;
      create)
        # The real command writes the archive to stdout; the script redirects it to the file.
        volume="$(grep -oE -- '--volume [^ ]+:/source' <<< "$*" | sed -e 's|--volume ||' -e 's|:/source||')"
        printf 'archive-of-%s' "${volume##*_}"
        exit 0
        ;;
      list)
        # Listing reads the archive from stdin; its body says which archive it is.
        body="$(cat)"
        case "$body" in
          *seo-storage*) archive=seo-storage.tar.gz ;;
          *web-data-protection*) archive=web-data-protection.tar.gz ;;
          *) archive=unknown ;;
        esac
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

# The backup takes the shared production lock, which needs flock. Git Bash on Windows does not ship
# it, and these cases are about artifact validation rather than mutual exclusion - the real lock is
# exercised in verify-deployment-guards.sh, which skips where flock is missing.
if ! command -v flock > /dev/null 2>&1; then
  printf '#!/usr/bin/env bash
exit 0
' > "$work/bin/flock"
  chmod +x "$work/bin/flock"
fi

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
  : > "$state/scan-output"
  echo "$state"
}

run_backup() {
  local state="$1" output="$2"
  # A lock inside the work directory, so these runs cannot collide with a real deployment on the
  # same host. Cases that need two independent locks override it.
  PATH="$repo_root/$work/bin:$PATH" \
  DOCKER_STUB_STATE="$state" \
  PRODUCTION_LOCK_DIR="${PRODUCTION_LOCK_DIR:-$repo_root/$work/lock}" \
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

# The output directory is no longer bind mounted - archives are streamed through the container's
# stdout - so nothing in the docker arguments reveals where the backup went. The check has to read
# the path the script reports instead. It was previously grepping for a `--volume ...:/backup`
# argument that this design no longer produces, which made it pass no matter what.
#
# Absolute matters because the script cd's to the repository root: a relative path left alone would
# land somewhere the caller did not choose.
state="$(new_state relative)"
# Relative, but inside the work directory the trap removes: an earlier version wrote to the
# repository root and left the artifacts behind.
if run_backup "$state" "$work/out-relative-$$" > "$work/out" 2>&1; then
  reported="$(sed -nE 's|^Backing up [^ ]+ to (.*)$|\1|p' "$work/out" | head -1)"
  if [[ "$reported" != /* ]]; then
    echo "FAIL: a relative output directory was not made absolute (reported '$reported')." >&2
    failures=$((failures + 1))
  elif [[ ! -d "$reported" ]]; then
    echo "FAIL: the reported backup directory does not exist: $reported" >&2
    failures=$((failures + 1))
  else
    echo "ok: a relative output directory is resolved to an absolute path that exists."
  fi
else
  echo "FAIL: the backup failed with a relative output directory." >&2
  sed 's/^/      /' "$work/out" >&2
  failures=$((failures + 1))
fi

# Called from somewhere other than the repository root. The case above runs from the repository
# root, where resolving against $PWD after the script's own cd would look identical - so it would
# pass on the very bug it exists to catch.
elsewhere="$repo_root/$work/elsewhere"
mkdir -p "$elsewhere"
# The output lands under $elsewhere, which is inside the work directory the trap removes. An
# earlier version of the neighbouring case wrote into the repository root and left it there.
state="$(new_state relative-elsewhere)"
if (cd "$elsewhere" && PATH="$repo_root/$work/bin:$PATH" DOCKER_STUB_STATE="$state" PRODUCTION_LOCK_DIR="$repo_root/$work/lock"       ENV_FILE=.env.production.example bash "$repo_root/scripts/backup-production.sh"       "from-caller-cwd" > "$repo_root/$work/out" 2>&1); then
  reported="$(sed -nE 's|^Backing up [^ ]+ to (.*)$|\1|p' "$work/out" | head -1)"
  if [[ "$reported" != "$elsewhere/from-caller-cwd" ]]; then
    echo "FAIL: a relative path was not resolved against the caller's directory." >&2
    echo "      expected: $elsewhere/from-caller-cwd" >&2
    echo "      actual:   $reported" >&2
    failures=$((failures + 1))
  else
    echo "ok: a relative output directory resolves against the caller's working directory."
  fi
  # Nothing is deleted by path here. An earlier version removed whatever the script reported, which
  # meant a path-handling regression could send `rm -rf` anywhere the reported string pointed - the
  # cleanup for "it wrote somewhere unexpected" was itself an unconditional delete of somewhere
  # unexpected. The correct path is inside $work and the trap removes it; anything else is named so
  # it can be dealt with deliberately.
  if [[ -n "$reported" && "$reported" != "$repo_root/$work/"* ]]; then
    echo "note: the backup wrote outside the test work directory and was left in place: $reported" >&2
  fi
else
  echo "FAIL: the backup failed when called from another directory." >&2
  sed 's/^/      /' "$work/out" >&2
  failures=$((failures + 1))
fi


# --- refusals ------------------------------------------------------------------------------------

state="$(new_state running)"
printf 'api\nworker\n' > "$state/running"
expect_refusal "the application still running" \
  "still running" "$state" "$repo_root/$work/out-running"

# migrate on its own. Without this case the filter could drop migrate and every other check would
# still pass, while a dump taken during a migration would capture a half-changed schema.
state="$(new_state migrating)"
printf 'migrate
' > "$state/running"
expect_refusal "a migration in flight"   "still running: migrate" "$state" "$repo_root/$work/out-migrating"

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
expect_refusal "a dump with no readable table of contents" \
  "no readable table of contents" "$state" "$repo_root/$work/out-unreadable-dump"

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

# --list reads only the table of contents, so a dump truncated anywhere after it still lists its
# objects and exits 0 - measured against a real dump cut to 3 kB. Only the full scan catches it.
state="$(new_state truncated-data)"
echo 1 > "$state/scan-status"
printf 'pg_restore: error: could not read from input file: end of file' > "$state/scan-output"
expect_refusal "a dump whose data section is truncated after a valid table of contents"   "is incomplete" "$state" "$repo_root/$work/out-truncated-data"

# Fail closed: if the running-services query itself fails there is no evidence the application is
# stopped, and treating that as "nothing running" is the wrong way to be wrong.
state="$(new_state ps-fails)"
echo 1 > "$state/ps-status"
printf 'cannot connect to the Docker daemon' > "$state/running"
expect_refusal "a failure to determine which services are running"   "could not determine which services are running" "$state" "$repo_root/$work/out-ps-fails"

# Two deployments started in the same second resolve to the same directory name. A test-then-create
# would let both through; an exclusive mkdir lets exactly one win.
state="$(new_state concurrent)"
shared_output="$repo_root/$work/out-concurrent"
run_backup "$state" "$shared_output" > "$work/concurrent-a" 2>&1 & a=$!
run_backup "$(new_state concurrent-b)" "$shared_output" > "$work/concurrent-b" 2>&1 & b=$!
wait "$a" && status_a=0 || status_a=$?
wait "$b" && status_b=0 || status_b=$?
if [[ "$status_a" -eq 0 && "$status_b" -eq 0 ]]; then
  echo "FAIL: two concurrent backups both claimed the same output directory." >&2
  failures=$((failures + 1))
elif [[ "$status_a" -ne 0 && "$status_b" -ne 0 ]]; then
  echo "FAIL: neither concurrent backup succeeded." >&2
  sed 's/^/      /' "$work/concurrent-a" "$work/concurrent-b" >&2
  failures=$((failures + 1))
else
  loser="$work/concurrent-a"
  [[ "$status_a" -eq 0 ]] && loser="$work/concurrent-b"
  # Two defences, and which fires depends on the environment: with a real flock the lock stops the
  # loser first, with the stub it reaches the exclusive mkdir. Either is a refusal; the mkdir is
  # isolated on its own below.
  if grep -qF "another operation is already working on" "$loser"; then
    echo "ok: only one of two concurrent backups runs; the other is refused by the lock."
  elif grep -qF "already exists or could not be created" "$loser"; then
    echo "ok: only one of two concurrent backups claims the output directory."
  else
    echo "FAIL: the losing concurrent backup was refused, but not by the lock or the mkdir." >&2
    sed 's/^/      /' "$loser" >&2
    failures=$((failures + 1))
  fi

# The exclusive mkdir on its own, with the lock taken out of the picture by giving each run its own
# lock file. It is the second line of defence: if the lock is ever bypassed, two runs must still
# not write over each other's artifacts.
state_c="$(new_state mkdir-race)"
state_d="$(new_state mkdir-race-b)"
shared_output2="$repo_root/$work/out-mkdir-race"
PRODUCTION_LOCK_DIR="$repo_root/$work/lock-a" run_backup "$state_c" "$shared_output2" > "$work/mkdir-a" 2>&1 & c=$!
PRODUCTION_LOCK_DIR="$repo_root/$work/lock-b" run_backup "$state_d" "$shared_output2" > "$work/mkdir-b" 2>&1 & d=$!
wait "$c" && status_c=0 || status_c=$?
wait "$d" && status_d=0 || status_d=$?
if [[ "$status_c" -eq 0 && "$status_d" -eq 0 ]]; then
  echo "FAIL: two backups holding different locks both claimed the same output directory." >&2
  failures=$((failures + 1))
else
  mkdir_loser="$work/mkdir-a"
  [[ "$status_c" -eq 0 ]] && mkdir_loser="$work/mkdir-b"
  if ! grep -qF "already exists or could not be created" "$mkdir_loser"; then
    echo "FAIL: the losing backup did not fail on the exclusive mkdir." >&2
    sed 's/^/      /' "$mkdir_loser" >&2
    failures=$((failures + 1))
  else
    echo "ok: the exclusive mkdir refuses a second backup even without the lock."
  fi
fi
fi

# The dump holds the whole database and the key archive holds unencrypted Data Protection keys.
# Checked on Linux only: a Windows filesystem does not carry these modes.
if [[ "$(uname -s)" == Linux ]]; then
  state="$(new_state permissions)"
  run_backup "$state" "$repo_root/$work/out-permissions" > /dev/null 2>&1
  bad=""
  [[ "$(stat -c '%a' "$work/out-permissions")" == "700" ]] || bad="directory=$(stat -c '%a' "$work/out-permissions")"
  for artifact in postgres.dump seo-storage.tar.gz web-data-protection.tar.gz; do
    mode="$(stat -c '%a' "$work/out-permissions/$artifact")"
    [[ "$mode" == "600" ]] || bad="$bad $artifact=$mode"
  done
  if [[ -n "$bad" ]]; then
    echo "FAIL: backup artifacts are not owner-only:$bad" >&2
    failures=$((failures + 1))
  else
    echo "ok: the backup directory is 700 and every artifact is 600."
  fi
else
  echo "skip: artifact permissions are only meaningful on Linux (this is $(uname -s))."
fi

# --- what must NOT be refused ---------------------------------------------------------------------

# Storage may legitimately be empty on a fresh deployment.
state="$(new_state empty-storage)"
printf 'drwxr-xr-x 0/0 0 2026-08-22 00:00 ./\n' > "$state/listing-seo-storage.tar.gz"
expect_success "an empty storage volume is accepted" "$state" "$repo_root/$work/out-empty-storage"

# The marker alone must not be enough. Anyone able to set an environment variable could otherwise
# skip the lock entirely, which is the thing the lock exists to prevent.
state="$(new_state forged-marker)"
forged="$(PRODUCTION_LOCK_HELD=seo-intelligence-prod   PATH="$repo_root/$work/bin:$PATH"   DOCKER_STUB_STATE="$state"   PRODUCTION_LOCK_DIR="$repo_root/$work/lock"   ENV_FILE=.env.production.example   bash scripts/backup-production.sh "$repo_root/$work/out-forged" 2>&1)" && forged_status=0 || forged_status=$?
if [[ "$forged_status" -eq 0 ]]; then
  echo "FAIL: a forged PRODUCTION_LOCK_HELD was accepted as holding the lock." >&2
  failures=$((failures + 1))
elif ! grep -qF "no inherited lock backs it" <<< "$forged"; then
  echo "FAIL: a forged PRODUCTION_LOCK_HELD was rejected, but not as a forged marker." >&2
  sed 's/^/      /' <<< "$forged" >&2
  failures=$((failures + 1))
else
  echo "ok: a forged lock marker is rejected."
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures backup check(s) failed." >&2
  exit 1
fi

echo "The production backup refuses every failure it is meant to catch."
