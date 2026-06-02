param(
    [string]$ApiUrl = "http://127.0.0.1:5080",
    [string]$Project = "src/SeoIntelligence.Api/SeoIntelligence.Api.csproj",
    [string]$Configuration = "Debug",
    [int]$TimeoutSeconds = 30,
    [string]$SmokeProjectId = $env:SMOKE_PROJECT_ID,
    [string]$DiscordChannelId = $env:SMOKE_DISCORD_CHANNEL_ID
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

function Join-ProcessArguments {
    param([string[]]$Values)

    ($Values | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join " "
}

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = "dotnet"
$startInfo.Arguments = Join-ProcessArguments -Values $arguments
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$null = $process.Start()
$logsWritten = $false

function Stop-ApiProcess {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

function Write-ApiLogs {
    if ($script:logsWritten) {
        return
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    Set-Content -Path $stdoutPath -Value $stdout -Encoding UTF8
    Set-Content -Path $stderrPath -Value $stderr -Encoding UTF8
    $script:logsWritten = $true
}

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
        Stop-ApiProcess
        Write-ApiLogs
        Get-Content -Path $stdoutPath -ErrorAction SilentlyContinue
        Get-Content -Path $stderrPath -ErrorAction SilentlyContinue
        throw "API health check did not become ready in $TimeoutSeconds seconds."
    }

    Invoke-WebRequest -Uri "$ApiUrl/healthz" -UseBasicParsing -TimeoutSec 5 | Out-Null
    Invoke-WebRequest -Uri "$ApiUrl/readyz" -UseBasicParsing -TimeoutSec 5 | Out-Null

    function Invoke-SmokeJsonRequest {
        param(
            [string]$Method,
            [string]$Path,
            [object]$Body = $null
        )

        $parameters = @{
            Uri = "$ApiUrl$Path"
            Method = $Method
            UseBasicParsing = $true
            TimeoutSec = 10
        }

        if ($null -ne $Body) {
            $parameters.ContentType = "application/json"
            $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
        }

        $response = Invoke-WebRequest @parameters
        if ([string]::IsNullOrWhiteSpace($response.Content)) {
            throw "Smoke request returned an empty response: $Method $Path"
        }

        $json = $response.Content | ConvertFrom-Json
        if ($null -eq $json.result -or -not $json.result) {
            throw "Smoke request failed envelope validation: $Method $Path"
        }

        return $json
    }

    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/projects?page=1&pageSize=5" | Out-Null
    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/admin/audit-logs?page=1&pageSize=5" | Out-Null
    Invoke-SmokeJsonRequest -Method "POST" -Path "/api/admin/master-data/sync" | Out-Null

    if ([string]::IsNullOrWhiteSpace($SmokeProjectId)) {
        $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
        $projectResponse = Invoke-SmokeJsonRequest `
            -Method "POST" `
            -Path "/api/projects" `
            -Body @{
                name = "Runbook smoke $stamp"
                defaultLocation = "JP"
                defaultLanguage = "ja"
                kpi = @{}
                memo = "Created by scripts/smoke-test.ps1"
            }
        $SmokeProjectId = $projectResponse.data.projectId
    }

    Invoke-SmokeJsonRequest `
        -Method "POST" `
        -Path "/api/projects/$SmokeProjectId/exports/csv" `
        -Body @{
            exportType = "external_api_calls"
            filter = @{}
            columns = @("provider", "endpoint", "statusCode", "consumedCredit", "cacheHit", "errorCode", "createdAt")
        } | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($DiscordChannelId)) {
        Invoke-SmokeJsonRequest -Method "POST" -Path "/api/admin/notification-channels/$DiscordChannelId/test" | Out-Null
        Invoke-SmokeJsonRequest -Method "GET" -Path "/api/admin/notification-deliveries?page=1&pageSize=5" | Out-Null
    }
}
finally {
    if ($null -ne $process) {
        Stop-ApiProcess
        Write-ApiLogs
    }
}
