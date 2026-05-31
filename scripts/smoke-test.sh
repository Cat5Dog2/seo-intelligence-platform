#!/usr/bin/env bash
set -euo pipefail

api_url="${API_URL:-http://127.0.0.1:5080}"
project="${API_PROJECT:-src/SeoIntelligence.Api/SeoIntelligence.Api.csproj}"
configuration="${CONFIGURATION:-Debug}"
log_path="${SMOKE_TEST_LOG:-artifacts/smoke/api.log}"
timeout_seconds="${SMOKE_TEST_TIMEOUT_SECONDS:-30}"

mkdir -p "$(dirname "$log_path")"

ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}" \
  dotnet run --project "$project" --configuration "$configuration" --no-build --urls "$api_url" \
  > "$log_path" 2>&1 &

api_pid=$!

cleanup() {
  kill "$api_pid" >/dev/null 2>&1 || true
  wait "$api_pid" >/dev/null 2>&1 || true
}

trap cleanup EXIT

deadline=$((SECONDS + timeout_seconds))
until curl -fsS "$api_url/healthz" >/dev/null 2>&1; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "API health check did not become ready in ${timeout_seconds}s."
    cat "$log_path"
    exit 1
  fi

  sleep 1
done

curl -fsS "$api_url/healthz" >/dev/null
curl -fsS "$api_url/readyz" >/dev/null
