#!/usr/bin/env bash
# Regression test for how scripts/scan-container-images.sh runs the scanner.
#
#   bash scripts/verify-scanner-isolation.sh
#
# The scanner is third-party code that reads an artifact about to run in production, on a VPS
# shared with another production stack. Its confinement is the only thing between a compromised
# scanner image and everything else on that host, and confinement that quietly stops applying
# still reads as protection. Every condition below is asserted from the arguments the script
# actually passes to docker, with docker replaced at the process boundary - nothing is pulled,
# scanned or started.
set -euo pipefail

cd "$(dirname "$0")/.."

# Unique per run for the same reason the script under test now uses a unique scratch directory.
work="artifacts/scanner-isolation-$$-${RANDOM}"
mkdir -p "$work/bin"
trap 'rm -rf "$work"' EXIT

log="$PWD/$work/docker.log"

# Records every invocation and answers the few queries the script makes. `docker save` has to
# create the file the scan then mounts, or the run would fail for an unrelated reason.
cat > "$work/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"
case "$1 ${2:-}" in
  "save "*|"save")
    # -o <path>
    out=""
    while [ "$#" -gt 0 ]; do
      if [ "$1" = "-o" ]; then out="$2"; fi
      shift
    done
    [ -n "$out" ] && printf 'fake image tar\n' > "$out"
    exit 0
    ;;
  "image inspect")
    printf 'sha256:%064d\n' 1
    exit 0
    ;;
  "pull "*|"pull")
    exit 0
    ;;
esac

# The scan itself: emit a report with no findings so the script's own reporting path runs.
if [ "$1" = run ]; then
  case " $* " in
    *" --download-db-only "*) exit 0 ;;
    *) printf '{"Results": []}\n'; exit 0 ;;
  esac
fi
exit 0
DOCKER
chmod +x "$work/bin/docker"

failures=0

fail() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

pass() {
  echo "PASS: $1"
}

# assert_every <description> <pattern> - every scan invocation must carry the pattern.
assert_every() {
  local description="$1" pattern="$2" total matching
  total="$(grep -c '^run .*--input /scan/image.tar' "$log" || true)"
  matching="$(grep '^run .*--input /scan/image.tar' "$log" | grep -cF -- "$pattern" || true)"
  if [ "$total" -eq 0 ]; then
    fail "$description (no scan invocations were recorded, so the check would pass vacuously)"
    return
  fi
  if [ "$matching" -ne "$total" ]; then
    fail "$description ($matching of $total invocations)"
    return
  fi
  pass "$description"
}

: > "$log"
FAKE_DOCKER_LOG="$log" PATH="$PWD/$work/bin:$PATH" \
  bash scripts/scan-container-images.sh app > /dev/null 2>&1 ||
  fail "the scanner did not run to completion against a fake docker"

# The findings the acceptances in docs/operations_runbook.md section 7.3 were judged against carry
# the container-side path in their target. Changing it would not fail anything - it would silently
# un-accept nine findings and gate them again, or worse, accept different ones.
assert_every "the scanned artifact is mounted at the recorded path" "--input /scan/image.tar"

assert_every "the scanner gets no network" "--network none"
assert_every "the scanner gets no writable root filesystem" "--read-only"
assert_every "the scanner gets no capabilities" "--cap-drop ALL"

# The scans read the database and write nothing to it, so they need no capability back. Only the
# download step does, and only DAC_OVERRIDE - see the comment on it in scan-container-images.sh.
if grep '^run .*--input /scan/image.tar' "$log" | grep -q -- '--cap-add'; then
  fail "a scan was given a capability back"
else
  pass "no capability is added back for a scan"
fi

refresh="$(grep -- '--download-db-only' "$log" || true)"
if ! printf '%s' "$refresh" | grep -q -- '--cap-drop ALL'; then
  fail "the database refresh does not drop capabilities"
elif [ "$(printf '%s' "$refresh" | grep -oE '\--cap-add [A-Z_]+' | sort -u | wc -l)" -ne 1 ] ||
     ! printf '%s' "$refresh" | grep -q -- '--cap-add DAC_OVERRIDE'; then
  fail "the database refresh adds a capability other than DAC_OVERRIDE"
else
  pass "the database refresh keeps DAC_OVERRIDE and nothing else"
fi
assert_every "the scanner cannot gain privileges" "--security-opt no-new-privileges"
assert_every "the scanner has a memory limit" "--memory 2g"
assert_every "the scanner has a process limit" "--pids-limit 256"
assert_every "the scanner has a CPU limit" "--cpus 2"

# A scan that can write to the database directory can change what a later scan finds.
assert_every "the vulnerability database is read-only during a scan" ":/root/.cache/trivy:ro"
assert_every "the exported image is read-only during a scan" ":/scan:ro"

# The one thing that would undo all of the above.
if grep -q 'docker.sock' "$log"; then
  fail "the scanner was given the Docker socket"
else
  pass "the Docker socket is never mounted"
fi

# Exactly one step may reach the network, and it is the only one that may write to the cache.
db_runs="$(grep -c -- '--download-db-only' "$log" || true)"
if [ "$db_runs" -ne 1 ]; then
  fail "expected exactly one database refresh, found $db_runs"
elif grep -- '--download-db-only' "$log" | grep -q -- '--network none'; then
  fail "the database refresh cannot download with --network none"
else
  pass "one database refresh, and it is the only step with network access"
fi

# Two runs at once must not share an export path. A fixed name meant the second run overwrote the
# first run's tar between `docker save` and the scan that reads it.
first_log="$PWD/$work/first.log"
second_log="$PWD/$work/second.log"
FAKE_DOCKER_LOG="$first_log" PATH="$PWD/$work/bin:$PATH" \
  bash scripts/scan-container-images.sh app > /dev/null 2>&1 &
first_pid=$!
FAKE_DOCKER_LOG="$second_log" PATH="$PWD/$work/bin:$PATH" \
  bash scripts/scan-container-images.sh app > /dev/null 2>&1 &
second_pid=$!
wait "$first_pid" || fail "the first concurrent scan failed"
wait "$second_pid" || fail "the second concurrent scan failed"

host_dirs() {
  grep -oE '\--volume [^ ]*trivy-scan[^ ]*:/scan:ro' "$1" | sort -u
}
if [ -z "$(host_dirs "$first_log")" ] || [ -z "$(host_dirs "$second_log")" ]; then
  fail "could not determine the export directories the concurrent runs used"
elif [ -n "$(comm -12 <(host_dirs "$first_log") <(host_dirs "$second_log"))" ]; then
  fail "two concurrent scans shared an export directory"
else
  pass "concurrent scans use separate export directories"
fi

# Nothing may be left in the repository afterwards.
if find artifacts -maxdepth 1 -name 'trivy-scan-*' 2>/dev/null | grep -q .; then
  fail "a scratch directory was left behind"
else
  pass "no scratch directory is left behind"
fi

if [ "$failures" -ne 0 ]; then
  echo "$failures scanner isolation check(s) failed." >&2
  exit 1
fi

echo "Scanner isolation checks passed."
