#!/usr/bin/env bash
# Regression test for development-only third-party image references.
#
#   bash scripts/verify-development-image-pins.sh
#
# RustFS is still pre-stable, so keep the Compose profile and vulnerability scanner on the same
# reviewed release manifest. A floating tag or scanner/Compose drift must fail this check.
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
    --profile rustfs \
    config --images |
    grep -E '(^|/)rustfs/rustfs:' |
    sort -u
)

if [ "${#scanner_images[@]}" -ne 1 ]; then
  echo "FAIL: expected one development image from the scanner, found ${#scanner_images[@]}." >&2
  exit 1
fi

if [ "${scanner_images[*]}" != "${compose_images[*]}" ]; then
  echo "FAIL: Compose and the vulnerability scanner use different development images." >&2
  printf '  scanner: %s\n' "${scanner_images[*]}" >&2
  printf '  compose: %s\n' "${compose_images[*]}" >&2
  exit 1
fi

for image in "${scanner_images[@]}"; do
  if [[ ! "$image" =~ ^rustfs/rustfs:1\.0\.0-rc\.[0-9]+@sha256:[0-9a-f]{64}$ ]]; then
    echo "FAIL: RustFS is not an explicit release candidate pinned by digest: $image" >&2
    exit 1
  fi
done

mapfile -t rustfs_services < <(
  "$docker_bin" compose \
    --file compose.yaml \
    --file compose.override.yaml \
    --profile rustfs \
    config --services |
    grep -E '^rustfs(-volume-init|-init)?$' |
    sort -u
)

expected_services=(rustfs rustfs-init rustfs-volume-init)
if [ "${rustfs_services[*]}" != "${expected_services[*]}" ]; then
  echo "FAIL: RustFS profile services do not match the required server, volume, and bucket bootstrap services." >&2
  printf '  expected: %s\n' "${expected_services[*]}" >&2
  printf '  actual:   %s\n' "${rustfs_services[*]}" >&2
  exit 1
fi

if "$docker_bin" compose \
  --file compose.yaml \
  --file compose.override.yaml \
  config --services | grep -Eq '^rustfs(-volume-init|-init)?$'; then
  echo "FAIL: RustFS must remain opt-in and must not start in the default Compose profile." >&2
  exit 1
fi

if "$docker_bin" compose \
  --file compose.yaml \
  --file compose.override.yaml \
  --profile '*' \
  config --images | grep -Eq '(^|/)(minio/minio|minio/mc):'; then
  echo "FAIL: the development Compose stack still references a MinIO image." >&2
  exit 1
fi

if POSTGRES_PASSWORD=compose-validation-only \
  API_SERVICE_KEY=compose-validation-only \
  CADDY_NETWORK_SUBNET=10.89.0.0/28 \
  "$docker_bin" compose \
  --env-file .env.production.example \
  --file compose.yaml \
  --file compose.production.yaml \
  config --services | grep -Eq '^rustfs(-volume-init|-init)?$'; then
  echo "FAIL: RustFS must not be present in the production Compose stack." >&2
  exit 1
fi

echo "Development image pin checks passed."
