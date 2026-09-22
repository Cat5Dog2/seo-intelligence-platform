#!/usr/bin/env bash
# Vulnerability gate for the container images this stack runs.
#
#   bash scripts/scan-container-images.sh app     # api / web / worker / migrate (must exist locally)
#   bash scripts/scan-container-images.sh runtime # postgres / redis at the digests in image-digests.lock
#   bash scripts/scan-container-images.sh dev     # RustFS, reported only
#   bash scripts/scan-container-images.sh unfixed # postgres / redis including unfixed, reported only
#   bash scripts/scan-container-images.sh drift   # has the image behind the upstream tags changed? reported only
#
# Application images are ours to rebuild, so any fixable HIGH or CRITICAL fails.
#
# Runtime images are third-party but run in production, so they are gated too, minus the individual
# findings accepted in docs/operations_runbook.md section 7.3. Acceptances are listed one CVE at a
# time: excusing a whole component would also hide a future CVE in that component that IS reachable.
# An acceptance that no longer matches any finding fails the gate as well, so the list is pruned
# when a finding disappears instead of lying in wait to excuse it if it comes back.
#
# Runtime images are scanned at the digest compose.yaml deploys - the one in image-digests.lock -
# not at whatever the tag points to today. The gate answers one question: does the image running in
# production carry a fixable HIGH or CRITICAL that nobody has judged? When upstream publishes a fix
# for one of its findings, the vulnerability database gains a fixed version and this scan fails on
# its own, without watching the tag. Whether the tag has moved is reported separately by the `drift`
# mode, which never gates and tells an index that merely moved from an image that changed for this
# platform: upstream rebuilds these tags every few days, usually without changing a single byte of
# the image this stack runs, and failing on that blocked every pull request until someone re-pinned
# an identical image.
#
# Development-only images (the RustFS profile) are reported and never gate: they are opt-in for local
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
DEV_IMAGES=(
  rustfs/rustfs:1.0.0-rc.6@sha256:97171b3d72cd47dc81000f92ea84de25608bfc35a94c965501afaeb5d99f6035
)

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

  # No OS-package acceptance is in force. When one is added, its target carries the Alpine
  # version, so a base image bump stops it from matching and the gate asks for the judgement
  # again rather than carrying it over.
)

# Digests the acceptances above were judged against, read from the lock file that compose.yaml and
# the runbook also point at. The runtime scans pull exactly these, so what is scanned, what was
# judged and what production deploys are one image by construction rather than three values that
# have to be kept equal.
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

# Prints the digest image-digests.lock records for a tag.
reviewed_digest_of() {
  local image="$1" entry
  for entry in "${RUNTIME_REVIEWED_DIGESTS[@]}"; do
    if [[ "${entry%%$'\t'*}" == "$image" ]]; then
      printf '%s\n' "${entry##*$'\t'}"
      return 0
    fi
  done
  echo "ERROR: $image has no reviewed digest recorded. Add one to image-digests.lock and judge its findings per docs/operations_runbook.md section 7.3." >&2
  return 1
}

# Pulls a runtime image at its reviewed digest and prints the image ID to export. Pulling the
# digest rather than the tag is what makes "the image that was scanned" and "the image compose.yaml
# deploys" one claim: a tag can be repointed at anything, a digest cannot.
pull_reviewed_image() {
  local image="$1" digest="$2" ref
  ref="${image}@${digest}"
  docker pull --quiet "$ref" > /dev/null || { echo "ERROR: could not pull $ref." >&2; return 1; }
  docker image inspect --format '{{.Id}}' "$ref"
}

# The digest a tag resolves to in the registry right now. Asked of the registry, not read off the
# local image store: RepoDigests accumulate. Once the gate has pulled the reviewed digest, a tag
# whose index carries the same platform image lands on that same local image, and the first
# RepoDigest listed is the old one - the check would report "not moved" for a tag that had.
resolve_tag_digest() {
  docker buildx imagetools inspect --format '{{.Manifest.Digest}}' "$1"
}

# The manifest digest of one platform's image inside an index. Equal digests are equal bits for
# that platform, whatever else in the index changed.
platform_manifest_of() {
  local ref="$1" platform="$2"
  docker buildx imagetools inspect --raw "$ref" | "$python_bin" -c '
import json
import sys

os_name, arch = sys.argv[1].split("/", 1)
index = json.load(sys.stdin)
for entry in index.get("manifests") or []:
    platform = entry.get("platform") or {}
    if platform.get("os") == os_name and platform.get("architecture") == arch:
        print(entry["digest"])
        break
' "$platform"
}

# Reports whether the image behind an upstream tag still is the reviewed one. Returns 2 when the
# image for this platform has changed, so a caller can annotate the run; 1 is kept for real errors.
# Not a finding against anything: production keeps deploying the reviewed digest either way, and
# what a new image contains is only known once it is pulled into the lock and scanned.
#
# The lock pins a multi-platform index, whose digest changes when any platform or attestation in it
# is rebuilt. Most upstream rebuilds change nothing for the platform this stack runs - the
# 2026-09-21 rebuilds of both images left the linux/amd64 manifests byte-identical - so an index
# that moved while this platform's image did not is reported but is nothing to act on.
report_tag_drift() {
  local image="$1" reviewed current platform reviewed_manifest current_manifest
  reviewed="$(reviewed_digest_of "$image")" || return 1
  current="$(resolve_tag_digest "$image")" || { echo "ERROR: could not resolve what $image points at." >&2; return 1; }
  if [[ "$current" == "$reviewed" ]]; then
    echo "$image: the tag still points at the reviewed digest $reviewed."
    return 0
  fi

  platform="$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}' 2> /dev/null || true)"
  platform="${platform:-linux/amd64}"
  reviewed_manifest="$(platform_manifest_of "${image}@${reviewed}" "$platform")"
  current_manifest="$(platform_manifest_of "${image}@${current}" "$platform")"
  if [[ -z "$reviewed_manifest" || -z "$current_manifest" ]]; then
    echo "ERROR: could not find a $platform image in the index of $image." >&2
    return 1
  fi
  if [[ "$reviewed_manifest" == "$current_manifest" ]]; then
    echo "$image: the tag now resolves to $current (reviewed: $reviewed), but the $platform image is"
    echo "  identical (manifest $reviewed_manifest). The index changed for another platform or an"
    echo "  attestation; there is nothing to re-judge."
    return 0
  fi
  echo "$image: the tag has moved to $current; the reviewed digest is $reviewed."
  echo "  The $platform image differs ($reviewed_manifest -> $current_manifest)."
  echo "  Not a failure - production deploys the reviewed digest. To adopt the new image, follow the"
  echo "  update procedure in docs/operations_runbook.md section 7.3."
  return 2
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

  # mktemp -d creates 0700, owned by whoever runs this. The scanner runs as root inside the
  # container with every capability dropped, and root without DAC_OVERRIDE cannot traverse a
  # directory owned by another user - the scan fails with "open /scan/image.tar: permission
  # denied". Granting the capability back to read one file would undo the point, so the directory
  # is made traversable instead. It holds an exported copy of an image that is about to be
  # published anyway, and it exists for the length of one scan.
  chmod 0755 "$dir"

  # Exported first so the scanner never touches the Docker daemon. Callers that already resolved
  # the image ID pass it, so what is scanned is the artifact they resolved even if something
  # repoints the tag between the two commands.
  docker save "${3:-$image}" -o "$dir/image.tar"
  chmod 0644 "$dir/image.tar"

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

# Prints the findings that are not accepted, and exits non-zero when there are any. The gating mode
# also sets stale_acceptances=fail, so an acceptance for the image that matched no finding is
# reported and fails the run.
stale_acceptances=ignore

report() {
  local image="$1" json="$2"
  shift 2
  SCAN_STALE_ACCEPTANCES="$stale_acceptances" "$python_bin" - "$image" "$json" "$@" <<'PY'
import json
import os
import sys

image, path = sys.argv[1], sys.argv[2]
accepted = {tuple(argument.split("\t", 3)) for argument in sys.argv[3:]}
fail_on_stale = os.environ.get("SCAN_STALE_ACCEPTANCES") == "fail"

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

gated, excused, matched = [], [], set()
for result in report["Results"]:
    target = result.get("Target") or ""
    for vulnerability in result.get("Vulnerabilities") or []:
        identifier = vulnerability.get("VulnerabilityID") or ""
        package = vulnerability.get("PkgName") or ""
        key = (image, identifier, target, package)
        if key in accepted:
            matched.add(key)
            excused.append(identifier)
            continue
        gated.append((vulnerability.get("Severity"), identifier, target, package))

if excused:
    print(f"{image}: {len(excused)} finding(s) accepted per the runbook.")

# An acceptance that matches nothing is a judgement about a finding that is no longer there. Kept,
# it would excuse that finding without anyone looking if it came back - a rebuild that reverted a
# package, a fix that was withdrawn. It goes when the finding goes, together with its entry in the
# runbook, rather than being carried along in case it is needed again.
stale = sorted(key for key in accepted if key[0] == image and key not in matched)
stale_fails = bool(stale) and fail_on_stale
if stale_fails:
    print(f"{image}: {len(stale)} acceptance(s) match no finding on this image:", file=sys.stderr)
    for _, identifier, target, package in stale:
        print(f"  {identifier} {target} ({package})", file=sys.stderr)
    print(
        "  Remove them from RUNTIME_ACCEPTED and from docs/operations_runbook.md section 7.3.",
        file=sys.stderr,
    )

if gated:
    print(f"{image}: {len(gated)} gated finding(s):", file=sys.stderr)
    for severity, identifier, target, package in sorted(gated):
        print(f"  {severity} {identifier} {target} ({package})", file=sys.stderr)
elif not stale_fails:
    print(f"{image}: no gated HIGH or CRITICAL findings.")

sys.exit(1 if gated or stale_fails else 0)
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
#
# Skipped for drift, which compares digests and scans nothing.
mode="${1:-app}"
status=0

if [[ "$mode" != drift ]]; then
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
fi

case "$mode" in
  app)
    # APP_SCAN_MANIFEST records which image ID each name resolved to at scan time, so the
    # deployment can start those IDs rather than resolving the tags again. Between the build and
    # the `up` a tag can be repointed; the manifest is what makes "the image that was scanned" and
    # "the image that started" the same claim rather than two.
    #
    # Cleared first. A failed scan must not leave a previous run's manifest for the deployment to
    # read, and the deployment refuses to start without one.
    manifest="${APP_SCAN_MANIFEST:-}"
    if [[ -n "$manifest" ]]; then
      rm -f -- "$manifest"
      mkdir -p "$(dirname "$manifest")"
    fi

    scanned=()
    for image in "${APP_IMAGES[@]}"; do
      if ! image_id="$(docker image inspect --format '{{.Id}}' "$image" 2> /dev/null)"; then
        echo "ERROR: $image does not exist locally. Build it before scanning." >&2
        status=1
        continue
      fi

      scan_to_json "$image" "$scratch/report.json" "$image_id"
      if report "$image" "$scratch/report.json"; then
        scanned+=("${image}"$'\t'"${image_id}")
      else
        status=1
      fi
    done

    # Written only when every image passed, and published by rename so a reader never sees a
    # partial file.
    if [[ -n "$manifest" && "$status" -eq 0 ]]; then
      printf '%s\n' "${scanned[@]}" > "$manifest.tmp"
      mv "$manifest.tmp" "$manifest"
      echo "Recorded ${#scanned[@]} scanned image IDs in $manifest."
    fi
    ;;
  runtime)
    read_reviewed_digests || exit 1
    stale_acceptances=fail
    for image in "${RUNTIME_IMAGES[@]}"; do
      digest="$(reviewed_digest_of "$image")" || { status=1; continue; }
      image_id="$(pull_reviewed_image "$image" "$digest")" || { status=1; continue; }
      echo "$image: scanning the reviewed digest $digest."
      scan_to_json "$image" "$scratch/report.json" "$image_id"
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
    # Same digests as the gate: the report is about the image production runs, not about the tag.
    read_reviewed_digests || exit 1
    ignore_unfixed=()
    for image in "${RUNTIME_IMAGES[@]}"; do
      digest="$(reviewed_digest_of "$image")" || { status=1; continue; }
      image_id="$(pull_reviewed_image "$image" "$digest")" || { status=1; continue; }
      scan_to_json "$image" "$scratch/report.json" "$image_id"
      report "$image" "$scratch/report.json" "${RUNTIME_ACCEPTED[@]}" || true
    done
    ;;
  drift)
    # Reported, never gated - see the header. Exit 2 means the image behind at least one tag has
    # changed for this platform; exit 1 is reserved for errors, so a caller can tell "there is
    # something newer" from "could not check". An index that moved with this platform's image
    # unchanged is printed and exits 0.
    read_reviewed_digests || exit 1
    moved=0
    for image in "${RUNTIME_IMAGES[@]}"; do
      rc=0
      report_tag_drift "$image" || rc=$?
      case "$rc" in
        0) ;;
        2) moved=1 ;;
        *) status=1 ;;
      esac
    done
    if [[ "$status" -eq 0 && "$moved" -eq 1 ]]; then
      exit 2
    fi
    ;;
  *)
    echo "ERROR: unknown mode '$mode'. Use app, runtime, dev, unfixed, or drift." >&2
    exit 1
    ;;
esac

exit "$status"
