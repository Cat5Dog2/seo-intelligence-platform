param(
    [string]$Configuration = "Debug",
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

$arguments = @(
    "test",
    "SeoIntelligence.sln",
    "--configuration",
    $Configuration,
    "--no-build"
)

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $arguments += @("--filter", $Filter)
}

& dotnet @arguments
