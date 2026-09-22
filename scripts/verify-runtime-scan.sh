#!/usr/bin/env bash
# Regression test for what scripts/scan-container-images.sh scans in its runtime modes.
#
#   bash scripts/verify-runtime-scan.sh
#
# The runtime gate is only a statement about production if it scans the image production deploys.
# It used to pull the tag and fail whenever the tag no longer matched image-digests.lock, which
# turned every upstream rebuild - most of them changing nothing this stack runs - into a red
# required check on every pull request. These cases pin the replacement: the gate pulls the locked
# digest and never the bare tag, a moved tag is reported by `drift` and by nothing else - asked of
# the registry, and telling an index that merely moved from an image that changed for this
# platform - and an acceptance that matches no finding fails the gate so the list cannot excuse a
# finding nobody is looking at. docker is replaced at the process boundary; nothing is pulled or
# scanned.
set -euo pipefail

cd "$(dirname "$0")/.."

# CI runners expose python3; Git Bash on Windows exposes python.
if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  python_bin=python3
elif command -v python > /dev/null 2>&1; then
  python_bin=python
else
  echo "ERROR: python3 or python is required to build the fixture reports." >&2
  exit 1
fi

# Relative on purpose: on Git Bash for Windows the Python that builds the fixtures is the Windows
# build, which cannot open an MSYS-style absolute path.
work="artifacts/runtime-scan-$$-${RANDOM}"
mkdir -p "$work/bin"
trap 'rm -rf "$work"' EXIT

log="$PWD/$work/docker.log"
state="$PWD/$work/state"
mkdir -p "$state"

# Records every invocation. `pull` remembers what was pulled so the scan that follows can answer
# with that image's fixture report - the scan command itself only names /scan/image.tar.
# `buildx imagetools inspect` plays the registry: where a tag points (the locked digest, or
# FAKE_MOVED_DIGEST when FAKE_TAG_MOVED is set) and what an index holds for linux/amd64 (the same
# platform manifest for every index, unless FAKE_PLATFORM_CHANGED is set and the index is the
# moved one).
cat > "$work/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"
case "$1 ${2:-}" in
  "pull "*|"pull")
    printf '%s\n' "${@: -1}" > "$FAKE_STATE/last-pull"
    exit 0
    ;;
  "save "*|"save")
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
  "version "*|"version")
    printf 'linux/amd64\n'
    exit 0
    ;;
  "buildx imagetools")
    ref="${@: -1}"
    case "$*" in
      *" --raw "*)
        manifest="sha256:$(printf '%064d' 3)"
        if [ -n "${FAKE_PLATFORM_CHANGED:-}" ] && [ "${ref##*@}" = "$FAKE_MOVED_DIGEST" ]; then
          manifest="sha256:$(printf '%064d' 4)"
        fi
        printf '{"manifests":[{"digest":"%s","platform":{"os":"linux","architecture":"amd64"}}]}\n' "$manifest"
        ;;
      *)
        # --format '{{.Manifest.Digest}}' prints no trailing newline, and neither does this.
        if [ -n "${FAKE_TAG_MOVED:-}" ]; then
          printf '%s' "$FAKE_MOVED_DIGEST"
        else
          tr -d '\r' < "$FAKE_LOCK" | awk -F'\t' -v tag="$ref" '$2 == tag { printf "%s", $3 }'
        fi
        ;;
    esac
    exit 0
    ;;
esac

if [ "$1" = run ]; then
  case " $* " in
    *" --download-db-only "*) exit 0 ;;
  esac
  pulled="$(cat "$FAKE_STATE/last-pull" 2> /dev/null || true)"
  name="${pulled%%:*}"
  name="${name%%@*}"
  if [ -f "$FAKE_REPORT_DIR/$name.json" ]; then
    cat "$FAKE_REPORT_DIR/$name.json"
  else
    printf '{"Results": []}\n'
  fi
  exit 0
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

# The acceptances the script carries, as <image>\t<CVE>\t<target>\t<package>. Read from the script
# rather than duplicated here, so the fixtures follow the list as it is judged and re-judged.
accepted_tsv="$work/accepted.tsv"
sed -n '/^RUNTIME_ACCEPTED=(/,/^)/p' scripts/scan-container-images.sh \
  | grep -E '^[[:space:]]*"' | sed -E 's/^[[:space:]]*"//; s/"$//' > "$accepted_tsv"

# Two fixture sets: every acceptance present as a finding, and the same minus the first acceptance
# - which is then stale. Prints the CVE that was dropped.
dropped="$("$python_bin" - "$accepted_tsv" "$work/reports-complete" "$work/reports-stale" <<'PY'
import json
import os
import sys
from collections import defaultdict

tsv, complete_dir, stale_dir = sys.argv[1:4]
rows = []
with open(tsv, encoding="utf-8") as handle:
    for line in handle:
        line = line.rstrip("\n")
        if line:
            rows.append(tuple(line.split("\t", 3)))


def write(directory, rows):
    os.makedirs(directory, exist_ok=True)
    by_image = defaultdict(lambda: defaultdict(list))
    for image, cve, target, package in rows:
        by_image[image][target].append(
            {"VulnerabilityID": cve, "PkgName": package, "Severity": "HIGH", "FixedVersion": "fixed"}
        )
    for image, targets in by_image.items():
        results = [{"Target": target, "Vulnerabilities": found} for target, found in targets.items()]
        with open(os.path.join(directory, image.split(":")[0] + ".json"), "w", encoding="utf-8") as handle:
            json.dump({"Results": results}, handle)


write(complete_dir, rows)
write(stale_dir, rows[1:])
print(rows[0][1] if rows else "")
PY
)"

locked_tags() {
  tr -d '\r' < image-digests.lock | awk -F'\t' '!/^#/ && NF == 3 { print $2 }'
}

locked_digest() {
  tr -d '\r' < image-digests.lock | awk -F'\t' -v tag="$1" '$2 == tag { print $3 }'
}

moved_digest="sha256:$(printf '%064d' 2)"

# run_scan <mode> <report dir> [VAR=value ...] - runs the script against the fake docker with the
# given fake-registry settings, leaving stdout, stderr and the exit status in $work.
run_scan() {
  local mode="$1" reports="$2"
  shift 2
  : > "$log"
  rm -f "$state/last-pull"
  set +e
  env FAKE_DOCKER_LOG="$log" FAKE_STATE="$state" FAKE_LOCK="$PWD/image-digests.lock" \
    FAKE_REPORT_DIR="$PWD/$reports" FAKE_MOVED_DIGEST="$moved_digest" "$@" PATH="$PWD/$work/bin:$PATH" \
    bash scripts/scan-container-images.sh "$mode" > "$work/stdout" 2> "$work/stderr"
  scan_status=$?
  set -e
}

# Every pull in the log must name the locked digest of its tag, and the bare tag must never be
# pulled: a scan of the tag is a scan of whatever upstream published last, not of production.
assert_pulls_locked_digests() {
  local mode="$1" tag digest
  for tag in $(locked_tags); do
    digest="$(locked_digest "$tag")"
    if ! grep -qxF -- "pull --quiet ${tag}@${digest}" "$log"; then
      fail "$mode did not pull ${tag} at its locked digest"
      return
    fi
  done
  if grep -E '^pull ' "$log" | grep -qv '@sha256:'; then
    fail "$mode pulled a bare tag:"
    grep -E '^pull ' "$log" | grep -v '@sha256:' | sed 's/^/      /' >&2
    return
  fi
  pass "$mode pulls every runtime image at its locked digest and never the tag"
}

# --- runtime: scans the locked digest, passes with the tag moved, and pins the reviewed image ---

run_scan runtime "$work/reports-complete" FAKE_TAG_MOVED=1 FAKE_PLATFORM_CHANGED=1
if [ "$scan_status" -ne 0 ]; then
  fail "runtime failed although every finding is accepted (exit $scan_status):"
  sed 's/^/      /' "$work/stderr" >&2
else
  pass "runtime does not fail because the upstream tag has moved"
fi
assert_pulls_locked_digests runtime

for tag in $(locked_tags); do
  if ! grep -qF -- "${tag}: scanning the reviewed digest $(locked_digest "$tag")" "$work/stdout"; then
    fail "runtime does not say which digest of ${tag} it scanned"
  fi
done

# --- unfixed: the report is about the same image as the gate ---

run_scan unfixed "$work/reports-complete"
if [ "$scan_status" -ne 0 ]; then
  fail "unfixed exited $scan_status:"
  sed 's/^/      /' "$work/stderr" >&2
fi
assert_pulls_locked_digests unfixed

# --- runtime: an acceptance that matches no finding fails the gate and is named ---

if [ -z "$dropped" ]; then
  echo "SKIP: RUNTIME_ACCEPTED is empty, so no acceptance can go stale."
else
  run_scan runtime "$work/reports-stale"
  if [ "$scan_status" -eq 0 ]; then
    fail "runtime passed with an acceptance ($dropped) that matches no finding"
  elif ! grep -q 'match no finding' "$work/stderr" || ! grep -qF -- "$dropped" "$work/stderr"; then
    fail "runtime failed on the stale acceptance but did not name $dropped:"
    sed 's/^/      /' "$work/stderr" >&2
  else
    pass "runtime fails on an acceptance that matches no finding and names it"
  fi
fi

# --- drift: the only mode that looks at the tag, and it reports rather than gates ---

run_scan drift "$work/reports-complete"
if [ "$scan_status" -ne 0 ]; then
  fail "drift exited $scan_status while every tag still points at its locked digest:"
  sed 's/^/      /' "$work/stderr" >&2
elif ! grep -q 'still points at the reviewed digest' "$work/stdout"; then
  fail "drift did not report that the tags still point at the locked digests"
else
  pass "drift exits 0 while the tags point at the locked digests"
fi

# The index moved but this platform's image inside it did not - the shape of the 2026-09-21
# rebuilds. Reported, and nothing to act on.
run_scan drift "$work/reports-complete" FAKE_TAG_MOVED=1
if [ "$scan_status" -ne 0 ]; then
  fail "drift exited $scan_status for an index that moved with the platform image unchanged, expected 0:"
  sed 's/^/      /' "$work/stderr" >&2
else
  identical_ok=1
  for tag in $(locked_tags); do
    if ! grep -qF -- "${tag}: the tag now resolves to ${moved_digest}" "$work/stdout" ||
       ! grep -q 'image is' "$work/stdout" || ! grep -q 'identical' "$work/stdout"; then
      identical_ok=0
    fi
  done
  if [ "$identical_ok" -eq 1 ]; then
    pass "drift exits 0 and says so when the index moved but the platform image is identical"
  else
    fail "drift did not report the moved index as identical for every tag:"
    sed 's/^/      /' "$work/stdout" >&2
  fi
fi

run_scan drift "$work/reports-complete" FAKE_TAG_MOVED=1 FAKE_PLATFORM_CHANGED=1
if [ "$scan_status" -ne 2 ]; then
  fail "drift exited $scan_status with the platform image changed, expected 2"
else
  moved_ok=1
  for tag in $(locked_tags); do
    if ! grep -qF -- "${tag}: the tag has moved to ${moved_digest}" "$work/stdout" ||
       ! grep -qF -- "the reviewed digest is $(locked_digest "$tag")" "$work/stdout" ||
       ! grep -q 'image differs' "$work/stdout"; then
      moved_ok=0
    fi
  done
  if [ "$moved_ok" -eq 1 ]; then
    pass "drift exits 2 and names both digests when the platform image behind a tag has changed"
  else
    fail "drift did not report both digests for every changed tag:"
    sed 's/^/      /' "$work/stdout" >&2
  fi
fi

# Asked of the registry, never of the local image store: after the gate has pulled the reviewed
# digest, a tag resolving to the same platform image lands on the same local image, whose first
# RepoDigest is the old one - which is how the runner reported "not moved" for tags that had.
if grep -qE '^pull ' "$log"; then
  fail "drift pulled an image; it must ask the registry instead"
elif grep -q 'RepoDigests' "$log"; then
  fail "drift read the local image store's RepoDigests, which cannot tell a moved tag from an earlier digest pull"
elif grep -q -- '--input /scan/image.tar' "$log"; then
  fail "drift scanned an image; it must only compare digests"
elif grep -q -- '--download-db-only' "$log"; then
  fail "drift refreshed the vulnerability database although it scans nothing"
else
  pass "drift pulls nothing, reads no local image store, scans nothing and refreshes no database"
fi

if [ "$failures" -ne 0 ]; then
  echo "$failures runtime scan check(s) failed." >&2
  exit 1
fi

echo "Runtime scan checks passed."
