#!/usr/bin/env bash
set -euo pipefail

api_url="${API_URL:-http://127.0.0.1:5080}"
project="${API_PROJECT:-src/SeoIntelligence.Api/SeoIntelligence.Api.csproj}"
configuration="${CONFIGURATION:-Debug}"
log_path="${SMOKE_TEST_LOG:-artifacts/smoke/api.log}"
timeout_seconds="${SMOKE_TEST_TIMEOUT_SECONDS:-30}"
smoke_project_id="${SMOKE_PROJECT_ID:-}"
discord_channel_id="${SMOKE_DISCORD_CHANNEL_ID:-}"

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

request_json() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local response

  if [ -n "$body" ]; then
    response="$(curl -fsS -X "$method" -H "Content-Type: application/json" -d "$body" "$api_url$path")"
  else
    response="$(curl -fsS -X "$method" "$api_url$path")"
  fi

  case "$response" in
    *'"result":true'*)
      printf '%s' "$response"
      ;;
    *)
      echo "Smoke request failed envelope validation: $method $path"
      echo "$response"
      exit 1
      ;;
  esac
}

extract_json_string() {
  local key="$1"
  sed -n "s/.*\"${key}\":\"\\([^\"]*\\)\".*/\\1/p"
}

request_json GET "/api/projects?page=1&pageSize=5" >/dev/null
request_json GET "/api/admin/audit-logs?page=1&pageSize=5" >/dev/null
request_json POST "/api/admin/master-data/sync" >/dev/null

if [ -z "$smoke_project_id" ]; then
  stamp="$(date -u +%Y%m%d%H%M%S)"
  project_body="{\"name\":\"Runbook smoke ${stamp}\",\"defaultLocation\":\"JP\",\"defaultLanguage\":\"ja\",\"kpi\":{},\"memo\":\"Created by scripts/smoke-test.sh\"}"
  project_response="$(request_json POST "/api/projects" "$project_body")"
  smoke_project_id="$(printf '%s' "$project_response" | extract_json_string "projectId")"
  if [ -z "$smoke_project_id" ]; then
    echo "Smoke project creation response did not include projectId."
    echo "$project_response"
    exit 1
  fi
fi

export_body='{"exportType":"external_api_calls","filter":{},"columns":["provider","endpoint","statusCode","consumedCredit","cacheHit","errorCode","createdAt"]}'
request_json POST "/api/projects/${smoke_project_id}/exports/csv" "$export_body" >/dev/null

if [ -n "$discord_channel_id" ]; then
  request_json POST "/api/admin/notification-channels/${discord_channel_id}/test" >/dev/null
  request_json GET "/api/admin/notification-deliveries?page=1&pageSize=5" >/dev/null
fi
