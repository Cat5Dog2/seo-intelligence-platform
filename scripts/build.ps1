param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

dotnet build SeoIntelligence.sln --configuration $Configuration
