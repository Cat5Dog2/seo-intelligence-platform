param(
    [string]$ApiUrl = "http://127.0.0.1:5080",
    [string]$Project = "src/SeoIntelligence.Api/SeoIntelligence.Api.csproj",
    [string]$Configuration = "Debug",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

$logDirectory = "artifacts/smoke"
$stdoutPath = Join-Path $logDirectory "api.out.log"
$stderrPath = Join-Path $logDirectory "api.err.log"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$env:ASPNETCORE_ENVIRONMENT = if ($env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT } else { "Development" }

$arguments = @(
    "run",
    "--project",
    $Project,
    "--configuration",
    $Configuration,
    "--no-build",
    "--urls",
    $ApiUrl
)

$process = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList $arguments `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath

try {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $isHealthy = $false

    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri "$ApiUrl/healthz" -UseBasicParsing -TimeoutSec 2 | Out-Null
            $isHealthy = $true
            break
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $isHealthy) {
        Get-Content -Path $stdoutPath -ErrorAction SilentlyContinue
        Get-Content -Path $stderrPath -ErrorAction SilentlyContinue
        throw "API health check did not become ready in $TimeoutSeconds seconds."
    }

    Invoke-WebRequest -Uri "$ApiUrl/healthz" -UseBasicParsing -TimeoutSec 5 | Out-Null
    Invoke-WebRequest -Uri "$ApiUrl/readyz" -UseBasicParsing -TimeoutSec 5 | Out-Null
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
