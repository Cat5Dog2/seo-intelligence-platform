#!/usr/bin/env bash
# Tests scripts/lib/production-lock.sh.
#
#   bash scripts/verify-production-lock.sh
#
# The lock is what keeps two deployments from migrating the same database at once, so the ways it
# can be defeated matter as much as the way it works. Needs a real flock and Linux stat and /proc
# semantics; it skips elsewhere rather than pretending to have checked.
#
# Each case runs from a small helper script rather than a nested `bash -c`. The quoting in a
# three-level nesting is where mistakes hide, and a test that silently tests nothing is worse here
# than no test at all.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"

if ! command -v flock > /dev/null 2>&1 || [[ ! -d /proc/self/fd ]]; then
  echo "skip: the production lock needs a real flock and /proc (this is $(uname -s))."
  exit 0
fi

work="artifacts/lock-test-$$-${RANDOM}"
mkdir -p "$work"
trap 'rm -rf "$work"' EXIT

failures=0
project="lock-test-project"

# Owned by this user and not world-writable, which is what the library requires.
lock_dir="$repo_root/$work/lockdir"
mkdir -p "$lock_dir"
chmod 700 "$lock_dir"
lock_file="$lock_dir/$project.deploy.lock"

check() {
  local description="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    echo "ok: $description"
  else
    echo "FAIL: $description (expected '$expected', got '$actual')" >&2
    failures=$((failures + 1))
  fi
}

# --- helpers ---------------------------------------------------------------------------------------

cat > "$work/acquire.sh" <<'ACQUIRE'
#!/usr/bin/env bash
source scripts/lib/production-lock.sh
acquire_production_lock "$1" > /dev/null 2>&1 && echo ACQUIRED || echo REFUSED
ACQUIRE

cat > "$work/child.sh" <<'CHILD'
#!/usr/bin/env bash
source scripts/lib/production-lock.sh
acquire_production_lock "$1" > /dev/null 2>&1 && echo REENTERED || echo BLOCKED
CHILD

cat > "$work/parent.sh" <<'PARENT'
#!/usr/bin/env bash
source scripts/lib/production-lock.sh
acquire_production_lock "$1" > /dev/null 2>&1 || { echo PARENT_FAILED; exit 0; }
bash "$2" "$1"
PARENT

# Both of the next two need another process to be holding the lock while the case runs. The holder
# is started as a direct background child and announces itself by touching a ready file once flock
# has returned; the case waits for that file rather than for a fixed interval. A fixed wait can
# expire before the holder has the lock on a loaded CI runner, and the case would then pass for the
# wrong reason - nothing was holding anything, so of course the acquisition was refused... or not.
cat > "$work/forge.sh" <<'FORGE'
#!/usr/bin/env bash
# Holds the lock in another process, then presents an unlocked descriptor on the same path together
# with the three environment variables. This is the forgery an implementation that re-opens the
# path and tests that instead would accept: the path really is locked - by someone else.
lock_path="$2" ready="$2.ready"
rm -f "$ready"
: > "$lock_path"
( flock 9 && touch "$ready" && sleep 20 ) 9> "$lock_path" &
holder=$!
for _ in $(seq 1 100); do
  [[ -e "$ready" ]] && break
  sleep 0.1
done
if [[ ! -e "$ready" ]]; then
  kill "$holder" 2>/dev/null || true
  echo HOLDER_FAILED
  exit 0
fi
exec {forged}> "$lock_path"
PRODUCTION_LOCK_HELD="$1" PRODUCTION_LOCK_FD="$forged" PRODUCTION_LOCK_PATH="$lock_path" \
  bash "$3" "$1" > "$4" 2>/dev/null
kill "$holder" 2>/dev/null || true
FORGE

cat > "$work/second-holder.sh" <<'SECOND'
#!/usr/bin/env bash
lock_path="$2" ready="$2.ready"
rm -f "$ready"
: > "$lock_path"
( flock 9 && touch "$ready" && sleep 20 ) 9> "$lock_path" &
holder=$!
for _ in $(seq 1 100); do
  [[ -e "$ready" ]] && break
  sleep 0.1
done
if [[ ! -e "$ready" ]]; then
  kill "$holder" 2>/dev/null || true
  echo HOLDER_FAILED
  exit 0
fi
bash "$3" "$1" > "$4" 2>/dev/null
kill "$holder" 2>/dev/null || true
SECOND

cat > "$work/where.sh" <<'WHERE'
#!/usr/bin/env bash
source scripts/lib/production-lock.sh
chosen="$(production_lock_path "$1" 2>/dev/null || echo NONE)"
case "$chosen" in
  /tmp/*) echo IN_TMP ;;
  NONE) echo NONE ;;
  *) echo NOT_IN_TMP ;;
esac
WHERE

cat > "$work/path.sh" <<'PATHOF'
#!/usr/bin/env bash
source scripts/lib/production-lock.sh
production_lock_path "$1" 2>/dev/null || echo NONE
PATHOF

# --- acquiring ---------------------------------------------------------------------------------

check "a first acquisition succeeds" ACQUIRED \
  "$(PRODUCTION_LOCK_DIR="$lock_dir" bash "$work/acquire.sh" "$project")"

check "the lock file is owner-only" 600 "$(stat -c '%a' "$lock_file")"

# --- re-entry ------------------------------------------------------------------------------------

# A child of a process that really holds the lock has to proceed, or the deployment would deadlock
# calling its own backup.
check "a genuine child re-enters" REENTERED \
  "$(PRODUCTION_LOCK_DIR="$lock_dir" bash "$work/parent.sh" "$project" "$work/child.sh" | tail -1)"

PRODUCTION_LOCK_DIR="$lock_dir" bash "$work/forge.sh" "$project" "$lock_file" "$work/acquire.sh" "$work/forged.out"
check "a forged marker over someone else's lock is rejected" REFUSED "$(cat "$work/forged.out")"

check "a marker with no descriptor behind it is rejected" REFUSED \
  "$(PRODUCTION_LOCK_HELD="$project" PRODUCTION_LOCK_DIR="$lock_dir" bash "$work/acquire.sh" "$project")"

# --- mutual exclusion ------------------------------------------------------------------------------

PRODUCTION_LOCK_DIR="$lock_dir" bash "$work/second-holder.sh" "$project" "$lock_file" "$work/acquire.sh" "$work/second.out"
check "a second holder is refused" REFUSED "$(cat "$work/second.out")"

# --- unsafe directories --------------------------------------------------------------------------

world_writable="$repo_root/$work/world"
mkdir -p "$world_writable"
chmod 777 "$world_writable"
check "a world-writable PRODUCTION_LOCK_DIR is rejected" REFUSED \
  "$(PRODUCTION_LOCK_DIR="$world_writable" bash "$work/acquire.sh" "$project")"

# /tmp exists and is writable, so a check that only tests -d and -w would take it. What must never
# happen is the lock landing there. Refusing outright is an equally correct outcome and on some
# hosts the only one available: Debian's /var/lock is a 1777 symlink to /run/lock, so with
# XDG_RUNTIME_DIR unusable there is no safe directory left and fail-closed is the designed answer.
# Demanding a directory here would fail the test on exactly the hosts the refusal protects.
chosen="$(env -u PRODUCTION_LOCK_DIR XDG_RUNTIME_DIR=/tmp bash "$work/where.sh" "$project")"
if [[ "$chosen" == "IN_TMP" ]]; then
  echo "FAIL: the lock was placed in /tmp." >&2
  failures=$((failures + 1))
else
  echo "ok: /tmp is not used for the lock (resolved to: $chosen)."
fi

# The other half of the same behaviour: refusing everything would also satisfy the check above, so
# a directory that *is* safe has to be accepted, or the fallback would be dead code.
safe_runtime="$repo_root/$work/runtime"
mkdir -p "$safe_runtime"
chmod 700 "$safe_runtime"
check "a safe XDG_RUNTIME_DIR is used" "$safe_runtime/$project.deploy.lock" \
  "$(env -u PRODUCTION_LOCK_DIR XDG_RUNTIME_DIR="$safe_runtime" bash "$work/path.sh" "$project")"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures lock check(s) failed." >&2
  exit 1
fi

echo "The production lock cannot be acquired twice or claimed without holding it."
