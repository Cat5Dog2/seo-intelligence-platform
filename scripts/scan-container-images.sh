#!/usr/bin/env bash
# Vulnerability gate for the container images this stack runs.
#
#   bash scripts/scan-container-images.sh app     # api / web / worker / migrate (must exist locally)
#   bash scripts/scan-container-images.sh runtime # postgres / redis (pulled)
#   bash scripts/scan-container-images.sh dev     # minio / mc, reported only
#   bash scripts/scan-container-images.sh unfixed # postgres / redis including unfixed, reported only
#
# Application images are ours to rebuild, so any fixable HIGH or CRITICAL fails.
#
# Runtime images are third-party but run in production, so they are gated too, minus the individual
# findings accepted in docs/operations_runbook.md section 7.3. Acceptances are listed one CVE at a
# time: excusing a whole component would also hide a future CVE in that component that IS reachable.
#
# Development-only images (the MinIO profile) are reported and never gate: they are opt-in for local
# storage experiments, no Compose file used on the VPS starts them, and gating on them would block
# unrelated work for vulnerabilities that cannot reach production.
#
# Trivy never gets the Docker socket. Each image is exported with `docker save` and scanned from the
# tar with `--input`, so the scanner is handed one file instead of control of the Docker daemon -
# which is root-equivalent on the host, and would mean trusting a third-party image with the
# developer machine and the CI runner. The scanner image is pinned by digest for the same reason.
set -euo pipefail

export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."

# Pinned by digest, not by tag: a tag can be repointed at a different image, and this one is handed
# our source tree's build output. Update deliberately, together with the version comment.
TRIVY_VERSION="0.74.0"
TRIVY_IMAGE="${TRIVY_IMAGE:-aquasec/trivy@sha256:62b1e65e8869bc4b4c6aa4fa2b21595256c7c2f6018a9d9ad61caf87187c1969}"
CACHE_DIR="${TRIVY_CACHE_DIR:-$PWD/artifacts/trivy-cache}"

APP_IMAGES=(
  seo-intelligence-api
  seo-intelligence-web
  seo-intelligence-worker
  seo-intelligence-migrate
)
RUNTIME_IMAGES=(postgres:16-alpine redis:7-alpine)
DEV_IMAGES=(minio/minio:latest minio/mc:latest)

# Accepted findings on the runtime images, one line per CVE:
#   <image>\t<CVE id>\t<target>\t<package>
# All four fields must match, so an acceptance cannot silently widen to another image or binary.
# The reasoning and the review conditions are in docs/operations_runbook.md section 7.3.
RUNTIME_ACCEPTED=(
  "postgres:16-alpine	CVE-2025-61726	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2025-61729	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2025-68121	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-25679	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-27145	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-32280	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-32281	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-32283	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-33811	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-33814	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-33818	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-39820	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-39821	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-39822	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-39836	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-42499	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-42504	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-56853	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-56858	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-56859	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-56860	usr/local/bin/gosu	stdlib"
  "postgres:16-alpine	CVE-2026-56862	usr/local/bin/gosu	stdlib"

  # OS packages. The target carries the Alpine version, so a base image bump stops these
  # from matching and the gate asks for the judgement again rather than carrying it over.
  "postgres:16-alpine	CVE-2026-14456	/scan/image.tar (alpine 3.24.1)	libcrypto3"
  "postgres:16-alpine	CVE-2026-14456	/scan/image.tar (alpine 3.24.1)	libssl3"
  "postgres:16-alpine	CVE-2026-53612	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-53613	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-53614	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-76642	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-78408	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-78409	/scan/image.tar (alpine 3.24.1)	libuuid"
  "postgres:16-alpine	CVE-2026-78410	/scan/image.tar (alpine 3.24.1)	libuuid"
)

# Digests the acceptances above were judged against, read from the lock file that compose.yaml and
# the runbook also point at. A mismatch means the upstream tag moved and the judgement has to be
# redone, so the scan fails rather than carrying old acceptances forward.
DIGEST_LOCK_FILE="image-digests.lock"

read_reviewed_digests() {
  if [[ ! -f "$DIGEST_LOCK_FILE" ]]; then
    echo "ERROR: $DIGEST_LOCK_FILE is missing; it is the source of truth for the reviewed digests." >&2
    return 1
  fi

  RUNTIME_REVIEWED_DIGESTS=()
  local line service tag digest
  # Carriage returns are stripped because a Windows checkout stores the lock file with CRLF, and
  # a trailing CR makes every digest comparison fail while printing two identical-looking values.
  while IFS= read -r line || [[ -n "$line" ]]; do
    [[ -z "${line// }" || "$line" == \#* ]] && continue
    # <service><TAB><tag><TAB><digest>; only the tag and digest matter here, the service name is
    # what verify-production-compose.sh binds each image to.
    IFS=$'	' read -r service tag digest <<< "$line"
    RUNTIME_REVIEWED_DIGESTS+=("${tag}"$'	'"${digest}")
  done < <(tr -d '\r' < "$DIGEST_LOCK_FILE")

  if [[ "${#RUNTIME_REVIEWED_DIGESTS[@]}" -eq 0 ]]; then
    echo "ERROR: $DIGEST_LOCK_FILE lists no images." >&2
    return 1
  fi
}

if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  python_bin=python3
elif command -v python > /dev/null 2>&1; then
  python_bin=python
else
  echo "ERROR: python3 or python is required to evaluate the scan results." >&2
  exit 1
fi

mkdir -p "$CACHE_DIR"

# Kept inside the repository rather than under /tmp: on Git Bash for Windows the Python that
# evaluates the report is the Windows build, which cannot open an MSYS /tmp path.
scratch="artifacts/trivy-scan-$$-${RANDOM}"
mkdir -p "$scratch"
trap 'rm -rf "$scratch"' EXIT

assert_reviewed_digest() {
  local image="$1" actual expected=""
  actual="$(docker image inspect --format '{{index .RepoDigests 0}}' "$image" 2>/dev/null | cut -d@ -f2 || true)"

  local entry
  for entry in "${RUNTIME_REVIEWED_DIGESTS[@]}"; do
    if [[ "${entry%%$'\t'*}" == "$image" ]]; then
      expected="${entry##*$'\t'}"
      break
    fi
  done

  if [[ -z "$expected" ]]; then
    echo "ERROR: $image has no reviewed digest recorded. Add one to image-digests.lock and re-judge the acceptances in docs/operations_runbook.md section 7.3." >&2
    return 1
  fi

  if [[ -z "$actual" ]]; then
    echo "ERROR: could not read the digest of $image." >&2
    return 1
  fi

  if [[ "$actual" != "$expected" ]]; then
    echo "ERROR: $image is now $actual but the accepted findings were judged against $expected." >&2
    echo "       Re-review the acceptances in docs/operations_runbook.md section 7.3, then update image-digests.lock and compose.yaml." >&2
    return 1
  fi
}

# Findings without a fix are excluded from the gate: there is nothing to do about them in this
# repository, and failing on them would mean disabling the gate. They are not excluded from
# view - the "unfixed" mode clears this so they can be read and judged. A finding nobody ever
# sees is a finding nobody judged.
ignore_unfixed=(--ignore-unfixed)

scan_to_json() {
  local image="$1" output="$2"

  # A directory per scan, so two runs cannot overwrite each other's export. The path *inside* the
  # container stays /scan/image.tar on purpose: the acceptance target recorded in
  # docs/operations_runbook.md section 7.3 contains it, and a unique name in there would silently
  # invalidate every OS-package acceptance.
  local dir
  dir="$(mktemp -d "$scratch/scan.XXXXXX")"

  # Exported first so the scanner never touches the Docker daemon.
  docker save "$image" -o "$dir/image.tar"

  # The scanner is third-party code reading an artifact that is about to run in production, on a
  # host it shares with both production stacks. It gets no network, no capabilities, no writable
  # filesystem, and a bounded share of the machine.
  #
  # The vulnerability database is mounted read-only. Only the download step below has network, and
  # it is the only step that writes there, so one scan cannot poison the database another reads.
  # Trivy still needs somewhere writable for its own scan cache: --cache-backend memory keeps that
  # inside the container instead of in the shared directory.
  docker run --rm \
    --network none \
    --read-only \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --memory 2g \
    --memory-swap 2g \
    --cpus 2 \
    --pids-limit 256 \
    --tmpfs /tmp:rw,noexec,nosuid,nodev,size=1g \
    --volume "$CACHE_DIR:/root/.cache/trivy:ro" \
    --volume "$(cd "$dir" && pwd):/scan:ro" \
    "$TRIVY_IMAGE" image \
    --input /scan/image.tar \
    --cache-backend memory \
    --scanners vuln \
    "${ignore_unfixed[@]}" \
    --pkg-types os,library \
    --severity HIGH,CRITICAL \
    --skip-db-update \
    --quiet \
    --format json > "$output"
  rm -rf "$dir"
}

# Prints the findings that are not accepted, and exits non-zero when there are any.
report() {
  local image="$1" json="$2"
  shift 2
  "$python_bin" - "$image" "$json" "$@" <<'PY'
import json
import sys

image, path = sys.argv[1], sys.argv[2]
accepted = {tuple(argument.split("\t", 3)) for argument in sys.argv[3:]}

with open(path, encoding="utf-8") as handle:
    report = json.load(handle)

# Fail closed on a report that is not shaped like a scan. Treating a missing "Results" as "no
# findings" would turn a scanner error, a changed output schema, or a truncated file into a pass.
if not isinstance(report, dict) or "Results" not in report:
    print(
        f"{image}: the scan report has no Results section, so the scan cannot be trusted.",
        file=sys.stderr,
    )
    sys.exit(1)

if not isinstance(report["Results"], list):
    print(f"{image}: the scan report's Results section is not a list.", file=sys.stderr)
    sys.exit(1)

gated, excused = [], []
for result in report["Results"]:
    target = result.get("Target") or ""
    for vulnerability in result.get("Vulnerabilities") or []:
        identifier = vulnerability.get("VulnerabilityID") or ""
        package = vulnerability.get("PkgName") or ""
        if (image, identifier, target, package) in accepted:
            excused.append(identifier)
            continue
        gated.append((vulnerability.get("Severity"), identifier, target, package))

if excused:
    print(f"{image}: {len(excused)} finding(s) accepted per the runbook.")

if not gated:
    print(f"{image}: no gated HIGH or CRITICAL findings.")
    sys.exit(0)

print(f"{image}: {len(gated)} gated finding(s):", file=sys.stderr)
for severity, identifier, target, package in sorted(gated):
    print(f"  {severity} {identifier} {target} ({package})", file=sys.stderr)
sys.exit(1)
PY
}

# One database refresh for the whole run, so the per-image scans need no network. This is the only
# step that is given network access and the only one that writes to the cache; every scan above
# mounts the same directory read-only.
# DAC_OVERRIDE is the one capability this step keeps. The cache directory is created on the host by
# whatever user runs this - uid 1001 on a GitHub runner - and Trivy runs as root inside the
# container. Root only writes into a directory owned by someone else by virtue of DAC_OVERRIDE, so
# dropping every capability makes the download fail with "mkdir /root/.cache/trivy/db: permission
# denied". The scans below need no such thing: they mount the same directory read-only.
docker run --rm \
  --read-only \
  --cap-drop ALL \
  --cap-add DAC_OVERRIDE \
  --security-opt no-new-privileges \
  --memory 2g \
  --memory-swap 2g \
  --pids-limit 256 \
  --tmpfs /tmp:rw,nosuid,nodev,size=2g \
  --volume "$CACHE_DIR:/root/.cache/trivy" \
  "$TRIVY_IMAGE" image --download-db-only > /dev/null

mode="${1:-app}"
status=0

case "$mode" in
  app)
    for image in "${APP_IMAGES[@]}"; do
      scan_to_json "$image" "$scratch/report.json"
      report "$image" "$scratch/report.json" || status=1
    done
    ;;
  runtime)
    read_reviewed_digests || exit 1
    for image in "${RUNTIME_IMAGES[@]}"; do
      docker pull --quiet "$image" > /dev/null
      assert_reviewed_digest "$image" || { status=1; continue; }
      scan_to_json "$image" "$scratch/report.json"
      report "$image" "$scratch/report.json" "${RUNTIME_ACCEPTED[@]}" || status=1
    done
    ;;
  dev)
    # Reported only; see the header for why these never gate.
    for image in "${DEV_IMAGES[@]}"; do
      docker pull --quiet "$image" > /dev/null
      scan_to_json "$image" "$scratch/report.json"
      report "$image" "$scratch/report.json" || true
    done
    ;;
  unfixed)
    # Reported, never gated. The same acceptances are applied so the output is the difference
    # between what is judged and what is merely unfixable, rather than a wall of known findings.
    ignore_unfixed=()
    for image in "${RUNTIME_IMAGES[@]}"; do
      docker pull --quiet "$image" > /dev/null
      scan_to_json "$image" "$scratch/report.json"
      report "$image" "$scratch/report.json" "${RUNTIME_ACCEPTED[@]}" || true
    done
    ;;
  *)
    echo "ERROR: unknown mode '$mode'. Use app, runtime, dev, or unfixed." >&2
    exit 1
    ;;
esac

exit "$status"
