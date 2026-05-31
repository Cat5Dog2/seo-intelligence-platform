param(
    [string]$InfrastructureProject = "src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj",
    [string]$StartupProject = "src/SeoIntelligence.Api/SeoIntelligence.Api.csproj",
    [string]$OutputPath = "artifacts/migrations/migration.sql"
)

$ErrorActionPreference = "Stop"

$infrastructureDirectory = Split-Path -Parent $InfrastructureProject
$dbContextMatch = Get-ChildItem -Path $infrastructureDirectory -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-String -Pattern "DbContext" -Quiet

if (-not $dbContextMatch) {
    Write-Host "No EF Core DbContext found; migration dry-run skipped for scaffold."
    exit 0
}

if (Test-Path ".config/dotnet-tools.json") {
    dotnet tool restore | Out-Null
}

dotnet ef --version | Out-Null

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

dotnet ef migrations script `
    --idempotent `
    --no-build `
    --project $InfrastructureProject `
    --startup-project $StartupProject `
    --output $OutputPath
