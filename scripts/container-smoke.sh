#!/usr/bin/env bash
# Container smoke test for the Docker Compose stack (development files).
# Verifies migration, HTTP readiness, non-root execution, shared storage, and
# Data Protection key persistence, then removes its own containers and volumes.
#
# Runs in an isolated Compose project so it never touches a development stack:
#   bash scripts/container-smoke.sh
# CI invokes the same script; set CONTAINER_SMOKE_SKIP_BUILD=true when the
# images were already built (for example by docker/bake-action).
set -euo pipefail

# Git Bash (MSYS) rewrites POSIX-looking arguments such as /data/storage into
# Windows paths before docker sees them; disable that so the script also runs
# on Windows. The variable is ignored on Linux/macOS.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")/.."

export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-seo-intelligence-container-smoke}"
export APP_ENV_FILE="${APP_ENV_FILE:-.env.example}"
export POSTGRES_DB="${POSTGRES_DB:-seo_container_smoke}"
export POSTGRES_USER="${POSTGRES_USER:-seo_container_smoke}"
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-seo_container_smoke_password}"
export POSTGRES_PORT="${POSTGRES_PORT:-35432}"
export REDIS_PORT="${REDIS_PORT:-36379}"
export API_PORT="${API_PORT:-35251}"
export WEB_PORT="${WEB_PORT:-35295}"
# Compose injects this as Secrets__ApiServiceKey for both api and web.
export API_SERVICE_KEY="${API_SERVICE_KEY:-container_smoke_service_key}"
# The Web host fails closed when no Admin user exists and no seed is configured.
export ADMIN_SEED_EMAIL="${ADMIN_SEED_EMAIL:-container-smoke@localhost}"
export ADMIN_SEED_PASSWORD="${ADMIN_SEED_PASSWORD:-ContainerSmoke!Pass1}"

DATA_PROTECTION_KEYS_PATH="/app/.data/data-protection-keys"

compose() {
  docker compose -f compose.yaml -f compose.override.yaml "$@"
}

cleanup() {
  compose down --volumes --remove-orphans
}
trap cleanup EXIT

dump_logs() {
  compose logs --no-color --tail 200
}

wait_for_http() {
  local attempt url ok
  for attempt in $(seq 1 30); do
    ok=true
    for url in "$@"; do
      # Redirect instead of `--output /dev/null`: with MSYS_NO_PATHCONV set,
      # Windows curl would receive the literal path and fail to create it.
      if ! curl --fail --silent --header "X-Service-Key: ${API_SERVICE_KEY}" "$url" > /dev/null; then
        ok=false
        break
      fi
    done

    if [[ "$ok" == "true" ]]; then
      return 0
    fi

    sleep 2
  done

  echo "Timed out waiting for: $*" >&2
  dump_logs
  return 1
}

if [[ "${CONTAINER_SMOKE_SKIP_BUILD:-false}" != "true" ]]; then
  compose build api web worker migrate
fi

compose up -d postgres redis
compose --profile tools run --rm migrate
compose up -d --wait --wait-timeout 180 api worker web || { dump_logs; exit 1; }

wait_for_http \
  "http://127.0.0.1:${API_PORT}/readyz" \
  "http://127.0.0.1:${API_PORT}/api/projects?page=1&pageSize=5" \
  "http://127.0.0.1:${WEB_PORT}/healthz" \
  "http://127.0.0.1:${WEB_PORT}/login"

# The API rejects calls without the service key, and the health probes stay open.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:${API_PORT}/api/projects")" = "401"
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:${API_PORT}/healthz")" = "200"

# The Web host sends anonymous visitors to the sign-in page.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:${WEB_PORT}/dashboard")" = "302"

# The download route is how generated files reach the browser, so the image has to carry it and
# it has to be behind the sign-in. A 404 here would mean the route is missing from the build.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "http://127.0.0.1:${WEB_PORT}/downloads/projects/00000000-0000-0000-0000-000000000001/exports/00000000-0000-0000-0000-000000000002")" = "302"

# The API file endpoint exists and stays behind the service key.
test "$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "http://127.0.0.1:${API_PORT}/api/projects/00000000-0000-0000-0000-000000000001/exports/00000000-0000-0000-0000-000000000002/content")" = "401"

# Non-root execution.
test "$(compose exec -T api id -u | tr -d '\r')" != "0"
test "$(compose exec -T web id -u | tr -d '\r')" != "0"
test "$(compose exec -T worker id -u | tr -d '\r')" != "0"

# api and worker share the same storage volume.
api_storage="$(compose exec -T api stat -c '%d:%i' /data/storage | tr -d '\r')"
worker_storage="$(compose exec -T worker stat -c '%d:%i' /data/storage | tr -d '\r')"
test "$api_storage" = "$worker_storage"

# Data Protection keys survive a web/worker restart.
keys_before="$(compose exec -T web find "$DATA_PROTECTION_KEYS_PATH" -maxdepth 1 -type f | sort | tr -d '\r')"
test -n "$keys_before"
compose restart web worker

wait_for_http "http://127.0.0.1:${WEB_PORT}/healthz"

keys_after="$(compose exec -T web find "$DATA_PROTECTION_KEYS_PATH" -maxdepth 1 -type f | sort | tr -d '\r')"
test "$keys_before" = "$keys_after"

echo "Container smoke test succeeded."
