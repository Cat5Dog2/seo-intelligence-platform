#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"

dotnet build SeoIntelligence.sln --configuration "$configuration"
