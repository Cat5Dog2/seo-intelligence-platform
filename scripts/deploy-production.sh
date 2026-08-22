#!/usr/bin/env bash
# Runs a production deployment on the VPS.
#
#   bash scripts/deploy-production.sh initial
#   bash scripts/deploy-production.sh update
#
# The ordering here is the control, not a convention: `set -euo pipefail` means a failing
# vulnerability scan aborts before anything starts. Pasting the same commands into a shell one by
# one does not do that - the shell happily runs the next line after a non-zero exit - which is why
# the procedure lives in this file and docs/docker_deployment.md points at it.
#
# The rules the ordering encodes:
#   - the scan runs after the build, or it reports on the previous release's images;
#   - the scan runs before anything starts, or it reports on images that are already serving;
#   - on an update the backup is taken while the application is stopped and before migrations run,
#     because a failed migration has nowhere to go back to otherwise.
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT_NAME="seo-intelligence-prod"
ENV_FILE="${ENV_FILE:-.env.production}"

# --project-name is passed explicitly rather than relying on `name:` in compose.production.yaml.
# A COMPOSE_PROJECT_NAME in the environment overrides that key, and the difference is not cosmetic:
# it decides which volumes the migration writes to and which containers get stopped.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" --env-file "$ENV_FILE" -f compose.yaml -f compose.production.yaml)

usage() {
  echo "Usage: bash scripts/deploy-production.sh <initial|update>" >&2
  echo "  initial  First deployment: build, scan, migrate, start." >&2
  echo "  update   Update: build, scan, stop, back up, migrate, recreate." >&2
}

mode="${1:-}"
shift || true

if [[ $# -gt 0 ]]; then
  usage
  exit 1
fi

case "$mode" in
  initial|update) ;;
  *) usage; exit 1 ;;
esac

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: $ENV_FILE not found. Copy .env.production.example and fill it in first." >&2
  exit 1
fi

# Both paths run these first, in this order.
build_and_scan() {
  "${COMPOSE[@]}" config --quiet
  "${COMPOSE[@]}" build api web worker migrate
  # Scans what this host just built. The images CI scanned are not these images: the VPS rebuilds
  # from source, and the .NET base images, apt and NuGet restore are all mutable.
  bash scripts/scan-container-images.sh app
}

case "$mode" in
  initial)
    build_and_scan
    "${COMPOSE[@]}" up -d postgres redis
    "${COMPOSE[@]}" --profile tools run --rm migrate
    "${COMPOSE[@]}" up -d --wait api worker web
    "${COMPOSE[@]}" ps
    ;;
  update)
    build_and_scan
    # Stopped before the backup and the migration so no old Worker overwrites job state, no old API
    # registers jobs in the previous format, and the backup is a consistent point in time.
    "${COMPOSE[@]}" stop web api worker
    bash scripts/backup-production.sh
    "${COMPOSE[@]}" --profile tools run --rm migrate
    "${COMPOSE[@]}" up -d --wait --force-recreate api worker web
    "${COMPOSE[@]}" ps
    "${COMPOSE[@]}" logs --tail 200 web api worker
    ;;
esac

echo "Deployment (${mode}) completed."
