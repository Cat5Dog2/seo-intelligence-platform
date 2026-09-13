#!/usr/bin/env bash
# Regression test for development-only third-party image references.
#
#   bash scripts/verify-development-image-pins.sh
#
# MinIO's Docker Hub repositories disappeared after the upstream project reached EOL. Keep the
# Compose profile and vulnerability scanner on the same explicit Quay release manifests so a
# floating tag or an accidental registry rollback cannot silently reintroduce that failure.
set -euo pipefail

cd "$(dirname "$0")/.."

docker_bin="$(command -v docker)"
work="$(mktemp -d)"
trap 'rm -rf -- "$work"' EXIT

log="$work/docker.log"
mkdir -p "$work/bin" "$work/trivy-cache"

cat > "$work/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FAKE_DOCKER_LOG"

if [ "$1" = "save" ]; then
  output=""
  while [ "$#" -gt 0 ]; do
    if [ "$1" = "-o" ]; then output="$2"; fi
    shift
  done
  [ -n "$output" ] && printf 'fake image tar\n' > "$output"
  exit 0
fi

if [ "$1" = "run" ]; then
  case " $* " in
    *" --download-db-only "*) exit 0 ;;
    *) printf '{"Results": []}\n'; exit 0 ;;
  esac
fi

exit 0
DOCKER
chmod +x "$work/bin/docker"

FAKE_DOCKER_LOG="$log" \
TRIVY_CACHE_DIR="$work/trivy-cache" \
PATH="$work/bin:$PATH" \
  bash scripts/scan-container-images.sh dev > /dev/null

mapfile -t scanner_images < <(awk '$1 == "pull" { print $NF }' "$log" | sort -u)
mapfile -t compose_images < <(
  "$docker_bin" compose \
    --file compose.yaml \
    --file compose.override.yaml \
    --profile minio \
    config --images |
    grep -E '(^|/)minio/(minio|mc):' |
    sort -u
)

if [ "${#scanner_images[@]}" -ne 2 ]; then
  echo "FAIL: expected two development images from the scanner, found ${#scanner_images[@]}." >&2
  exit 1
fi

if [ "${scanner_images[*]}" != "${compose_images[*]}" ]; then
  echo "FAIL: Compose and the vulnerability scanner use different development images." >&2
  printf '  scanner: %s\n' "${scanner_images[*]}" >&2
  printf '  compose: %s\n' "${compose_images[*]}" >&2
  exit 1
fi

for image in "${scanner_images[@]}"; do
  if [[ ! "$image" =~ ^quay\.io/minio/(minio|mc):RELEASE\.[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}-[0-9]{2}-[0-9]{2}Z@sha256:[0-9a-f]{64}$ ]]; then
    echo "FAIL: development image is not an explicit Quay release pinned by digest: $image" >&2
    exit 1
  fi
done

echo "Development image pin checks passed."
