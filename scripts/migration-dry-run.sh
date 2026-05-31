#!/usr/bin/env bash
set -euo pipefail

infrastructure_project="${INFRASTRUCTURE_PROJECT:-src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj}"
startup_project="${STARTUP_PROJECT:-src/SeoIntelligence.Api/SeoIntelligence.Api.csproj}"
output_path="${MIGRATION_SCRIPT_OUTPUT:-artifacts/migrations/migration.sql}"
infrastructure_dir="$(dirname "$infrastructure_project")"

if ! find "$infrastructure_dir" \
  -type f \
  -name '*.cs' \
  ! -path '*/bin/*' \
  ! -path '*/obj/*' \
  -exec grep -q 'DbContext' {} +; then
  echo "No EF Core DbContext found; migration dry-run skipped for scaffold."
  exit 0
fi

if [[ -f ".config/dotnet-tools.json" ]]; then
  dotnet tool restore >/dev/null
fi

if ! dotnet ef --version >/dev/null 2>&1; then
  echo "dotnet-ef is required when EF Core DbContext exists."
  exit 1
fi

mkdir -p "$(dirname "$output_path")"

dotnet ef migrations script \
  --idempotent \
  --no-build \
  --project "$infrastructure_project" \
  --startup-project "$startup_project" \
  --output "$output_path"
