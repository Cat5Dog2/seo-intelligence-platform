#!/usr/bin/env bash
# Runs the StackExchange.Redis adapter against the same pinned Redis 7 image used in production.
# A unique Compose project and an ephemeral host port keep this isolated from development stacks.
set -euo pipefail

cd "$(dirname "$0")/.."

configuration="${CONFIGURATION:-Debug}"
project_name="seo-intelligence-redis-integration-$$"

compose() {
  REDIS_PORT=0 docker compose \
    --project-name "$project_name" \
    --file compose.yaml \
    --file compose.override.yaml \
    "$@"
}

cleanup() {
  local status=$?
  trap - EXIT

  if ! compose down --volumes --remove-orphans; then
    echo "Failed to remove the Redis integration-test Compose project." >&2
    if [[ "$status" -eq 0 ]]; then
      status=1
    fi
  fi

  exit "$status"
}
trap cleanup EXIT

compose up -d --wait --wait-timeout 60 redis

redis_endpoint="$(compose port redis 6379 | tr -d '\r' | tail -n 1)"
if [[ ! "$redis_endpoint" =~ ^127\.0\.0\.1:[0-9]+$ ]]; then
  echo "Could not resolve the isolated Redis endpoint: ${redis_endpoint:-<empty>}" >&2
  exit 1
fi

REDIS_INTEGRATION_CONNECTION_STRING="${redis_endpoint},connectTimeout=5000,syncTimeout=5000" \
  dotnet test tests/IntegrationTests/IntegrationTests.csproj \
    --configuration "$configuration" \
    --no-build \
    --filter 'FullyQualifiedName=IntegrationTests.RedisCoordinatorIntegrationTests.RealRedisSupportsStringLifecycleAndExclusiveLease' \
    -- RunConfiguration.TreatNoTestsAsError=true
