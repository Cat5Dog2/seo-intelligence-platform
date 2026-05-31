#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"

dotnet test SeoIntelligence.sln --configuration "$configuration" --no-build "$@"
