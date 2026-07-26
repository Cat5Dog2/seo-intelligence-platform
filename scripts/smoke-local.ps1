param(
    [string]$ApiUrl = "http://127.0.0.1:5080",
    [string]$WebUrl = "http://127.0.0.1:5081",
    [string]$ApiProject = "src/SeoIntelligence.Api/SeoIntelligence.Api.csproj",
    [string]$WorkerProject = "src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj",
    [string]$WebProject = "src/SeoIntelligence.Web/SeoIntelligence.Web.csproj",
    [string]$InfrastructureProject = "src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj",
    [string]$Configuration = "Debug",
    [int]$StartupTimeoutSeconds = 60,
    [int]$JobTimeoutSeconds = 90,
    [string]$SmokeProjectId = $env:SMOKE_PROJECT_ID,
    [string]$DiscordChannelId = $env:SMOKE_DISCORD_CHANNEL_ID,
    [switch]$SkipBuild,
    [switch]$SkipMigration,
    [switch]$SkipMigrationVerification,
    [switch]$SkipDependencies,
    [switch]$SkipWeb,
    [switch]$RunBrowserTests,
    [switch]$InstallPlaywrightBrowsers,
    [switch]$StopDependencies,
    [switch]$RemoveDependencyVolumes
)

$ErrorActionPreference = "Stop"

if ($RemoveDependencyVolumes -and -not $StopDependencies) {
    throw "-RemoveDependencyVolumes requires -StopDependencies."
}

. (Join-Path $PSScriptRoot "load-dotenv.ps1")
Import-DotEnvFile -Path (Join-Path $PSScriptRoot "..\.env")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sharedStorageBasePath = Join-Path (Join-Path $repoRoot ".data") "storage"

if ([string]::IsNullOrWhiteSpace($SmokeProjectId)) {
    $SmokeProjectId = $env:SMOKE_PROJECT_ID
}

if ([string]::IsNullOrWhiteSpace($DiscordChannelId)) {
    $DiscordChannelId = $env:SMOKE_DISCORD_CHANNEL_ID
}

$logDirectory = "artifacts/smoke-local"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$managedProcesses = New-Object System.Collections.Generic.List[object]
$previousEnvironment = @{
    ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT
    RakkoKeyword__Mode = $env:RakkoKeyword__Mode
    Api__BaseUrl = $env:Api__BaseUrl
    Storage__Provider = $env:Storage__Provider
    Storage__BasePath = $env:Storage__BasePath
    E2E_BROWSER_ENABLED = $env:E2E_BROWSER_ENABLED
    E2E_WEB_URL = $env:E2E_WEB_URL
    E2E_API_URL = $env:E2E_API_URL
}

$env:ASPNETCORE_ENVIRONMENT = if ($env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT } else { "Development" }
$env:RakkoKeyword__Mode = "Mock"
$env:Api__BaseUrl = $ApiUrl
$env:Storage__Provider = "Local"
$env:Storage__BasePath = $sharedStorageBasePath
New-Item -ItemType Directory -Force -Path $sharedStorageBasePath | Out-Null

$standaloneCompose = Get-Command "docker-compose" -ErrorAction SilentlyContinue
if ($standaloneCompose) {
    $script:DockerComposeExecutable = $standaloneCompose.Source
    $script:DockerComposeStandalone = $true
}
else {
    $dockerCommand = Get-Command "docker" -ErrorAction Stop
    $script:DockerComposeExecutable = $dockerCommand.Source
    $script:DockerComposeStandalone = $false
}

function Invoke-DockerCompose {
    param([string[]]$Arguments)

    if ($script:DockerComposeStandalone) {
        & $script:DockerComposeExecutable @Arguments
        return
    }

    & $script:DockerComposeExecutable compose @Arguments
}

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

function Invoke-ExternalCommand {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    Write-Host ("Running: {0} {1}" -f $FileName, ($Arguments -join " "))
    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FileName $($Arguments -join ' ')"
    }
}

function Start-SmokeProcess {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    $stdoutPath = Join-Path $logDirectory "$Name.out.log"
    $stderrPath = Join-Path $logDirectory "$Name.err.log"
    $argumentString = Join-ProcessArguments -Values $Arguments

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.Arguments = $argumentString
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $false
    $startInfo.RedirectStandardError = $false
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo

    Write-Host ("Starting {0}: dotnet {1}" -f $Name, $argumentString)
    $null = $process.Start()

    $entry = [pscustomobject]@{
        Name = $Name
        Process = $process
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
        LogsWritten = $false
    }
    $managedProcesses.Add($entry) | Out-Null
    return $entry
}

function Write-ProcessLogs {
    param(
        [object]$Entry,
        [switch]$Force
    )

    if ($null -eq $Entry -or $Entry.LogsWritten) {
        return
    }

    if (-not $Force -and -not $Entry.Process.HasExited) {
        return
    }

    try {
        if (-not (Test-Path $Entry.StdoutPath)) {
            New-Item -ItemType File -Force -Path $Entry.StdoutPath | Out-Null
        }

        if (-not (Test-Path $Entry.StderrPath)) {
            New-Item -ItemType File -Force -Path $Entry.StderrPath | Out-Null
        }

        $Entry.LogsWritten = $true
    }
    catch {
        Write-Host "Could not write $($Entry.Name) logs: $($_.Exception.Message)"
    }
}

function Stop-SmokeProcess {
    param([object]$Entry)

    if ($null -eq $Entry) {
        return
    }

    try {
        if (-not $Entry.Process.HasExited) {
            $Entry.Process.Kill()
            $Entry.Process.WaitForExit()
        }
        Write-ProcessLogs -Entry $Entry -Force
    }
    catch {
        Write-Host "Could not stop $($Entry.Name) process: $($_.Exception.Message)"
    }
    finally {
        $Entry.Process.Dispose()
    }
}

function Show-ProcessLogs {
    param(
        [object]$Entry,
        [int]$Tail = 120
    )

    if ($null -eq $Entry) {
        return
    }

    Write-ProcessLogs -Entry $Entry
    Write-Host "---- $($Entry.Name) stdout ----"
    Get-Content -Path $Entry.StdoutPath -Tail $Tail -ErrorAction SilentlyContinue
    Write-Host "---- $($Entry.Name) stderr ----"
    Get-Content -Path $Entry.StderrPath -Tail $Tail -ErrorAction SilentlyContinue
}

function Wait-HttpEndpoint {
    param(
        [string]$Name,
        [string]$Url,
        [object]$ProcessEntry = $null
    )

    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($null -ne $ProcessEntry -and $ProcessEntry.Process.HasExited) {
            Show-ProcessLogs -Entry $ProcessEntry
            throw "$Name process exited before $Url became reachable."
        }

        try {
            Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    if ($null -ne $ProcessEntry) {
        Show-ProcessLogs -Entry $ProcessEntry
    }

    throw "$Name did not become reachable in $StartupTimeoutSeconds seconds: $Url"
}

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
        TimeoutSec = 15
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

function Wait-SmokeJob {
    param(
        [string]$JobId,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($JobId)) {
        throw "$Name response did not include a jobId."
    }

    $deadline = (Get-Date).AddSeconds($JobTimeoutSeconds)
    $lastStatus = ""

    while ((Get-Date) -lt $deadline) {
        $response = Invoke-SmokeJsonRequest -Method "GET" -Path "/api/jobs/$JobId"
        $status = [string]$response.data.status

        if ($status -ne $lastStatus) {
            Write-Host "$Name job $JobId status: $status"
            $lastStatus = $status
        }

        if ($status -eq "succeeded") {
            return $response
        }

        if ($status -eq "failed_retryable" -or $status -eq "failed_fatal" -or $status -eq "canceled") {
            $errorJson = $response.data.error | ConvertTo-Json -Depth 8 -Compress
            throw "$Name job $JobId ended with status '$status'. Error: $errorJson"
        }

        Start-Sleep -Seconds 2
    }

    throw "$Name job $JobId did not succeed in $JobTimeoutSeconds seconds."
}

function Wait-ComposeDependencies {
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        # Resolve the user/database inside the container so .env overrides of
        # POSTGRES_USER / POSTGRES_DB keep working.
        Invoke-DockerCompose -Arguments @("exec", "-T", "postgres", "sh", "-c", 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"') *> $null
        $postgresReady = $LASTEXITCODE -eq 0

        $redisOutput = Invoke-DockerCompose -Arguments @("exec", "-T", "redis", "redis-cli", "ping") 2>$null
        $redisReady = $LASTEXITCODE -eq 0 -and ($redisOutput -join "") -match "PONG"

        if ($postgresReady -and $redisReady) {
            return
        }

        Start-Sleep -Seconds 2
    }

    Invoke-DockerCompose -Arguments @("ps")
    Invoke-DockerCompose -Arguments @("logs", "postgres", "redis")
    throw "Docker Compose dependencies did not become ready in $StartupTimeoutSeconds seconds."
}

function Install-PlaywrightBrowsers {
    $playwrightOutputDirectory = "tests/E2ETests/bin/$Configuration/net10.0"
    $playwrightScript = Join-Path $playwrightOutputDirectory "playwright.ps1"
    if (-not (Test-Path $playwrightScript)) {
        throw "Playwright install script was not found: $playwrightScript. Run smoke-local without -SkipBuild first."
    }

    $arguments = @("install", "chromium")
    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        $arguments = @("install", "--with-deps", "chromium")
    }

    Write-Host ("Running: {0} {1}" -f $playwrightScript, ($arguments -join " "))
    & $playwrightScript @arguments
    if ($LASTEXITCODE -eq 0) {
        return
    }

    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        Write-Host "playwright.ps1 failed. Falling back to the bundled Playwright Node CLI."
        Invoke-PlaywrightNodeCli -OutputDirectory $playwrightOutputDirectory -Arguments $arguments
        return
    }

    throw "Playwright browser installation failed with exit code $LASTEXITCODE."
}

function Invoke-PlaywrightNodeCli {
    param(
        [string]$OutputDirectory,
        [string[]]$Arguments
    )

    $nodePath = Join-Path $OutputDirectory ".playwright/node/win32_x64/node.exe"
    $cliPath = Join-Path $OutputDirectory ".playwright/package/cli.js"
    if (-not (Test-Path $nodePath)) {
        throw "Playwright Node executable was not found: $nodePath"
    }

    if (-not (Test-Path $cliPath)) {
        throw "Playwright CLI script was not found: $cliPath"
    }

    Write-Host ("Running: {0} {1} {2}" -f $nodePath, $cliPath, ($Arguments -join " "))
    & $nodePath $cliPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Playwright browser installation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-BrowserE2ETests {
    $env:E2E_BROWSER_ENABLED = "true"
    $env:E2E_WEB_URL = $WebUrl
    $env:E2E_API_URL = $ApiUrl

    if ($InstallPlaywrightBrowsers) {
        Install-PlaywrightBrowsers
    }

    Invoke-ExternalCommand -FileName "dotnet" -Arguments @(
        "test",
        "tests/E2ETests/E2ETests.csproj",
        "--configuration",
        $Configuration,
        "--no-build",
        "--filter",
        "Category=BrowserE2E")
}

try {
    if (-not $SkipDependencies) {
        Invoke-DockerCompose -Arguments @("up", "-d", "postgres", "redis", "minio", "minio-init")
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose dependencies could not be started."
        }

        Wait-ComposeDependencies
    }

    if (-not $SkipBuild) {
        Invoke-ExternalCommand -FileName "dotnet" -Arguments @("build", "SeoIntelligence.sln", "--configuration", $Configuration)
    }

    if (-not $SkipMigration) {
        if (Test-Path ".config/dotnet-tools.json") {
            Invoke-ExternalCommand -FileName "dotnet" -Arguments @("tool", "restore")
        }

        if (-not $SkipMigrationVerification) {
            & (Join-Path $PSScriptRoot "verify-rakko-v1120-migration.ps1") `
                -InfrastructureProject $InfrastructureProject `
                -StartupProject $ApiProject `
                -Configuration $Configuration
        }

        Invoke-ExternalCommand -FileName "dotnet" -Arguments @(
            "tool",
            "run",
            "dotnet-ef",
            "database",
            "update",
            "--no-build",
            "--configuration",
            $Configuration,
            "--project",
            $InfrastructureProject,
            "--startup-project",
            $ApiProject)
    }

    $apiProcess = Start-SmokeProcess `
        -Name "api" `
        -Arguments @("run", "--project", $ApiProject, "--configuration", $Configuration, "--no-build", "--urls", $ApiUrl)

    $workerProcess = Start-SmokeProcess `
        -Name "worker" `
        -Arguments @("run", "--project", $WorkerProject, "--configuration", $Configuration, "--no-build")

    Wait-HttpEndpoint -Name "API health" -Url "$ApiUrl/healthz" -ProcessEntry $apiProcess
    Invoke-WebRequest -Uri "$ApiUrl/readyz" -UseBasicParsing -TimeoutSec 10 | Out-Null

    if (-not $SkipWeb) {
        $webProcess = Start-SmokeProcess `
            -Name "web" `
            -Arguments @("run", "--project", $WebProject, "--configuration", $Configuration, "--no-build", "--urls", $WebUrl)

        Wait-HttpEndpoint -Name "Web health" -Url "$WebUrl/healthz" -ProcessEntry $webProcess
        Invoke-WebRequest -Uri "$WebUrl/readyz" -UseBasicParsing -TimeoutSec 10 | Out-Null

        foreach ($path in @("/", "/keywords", "/search-volume", "/admin")) {
            Invoke-WebRequest -Uri "$WebUrl$path" -UseBasicParsing -TimeoutSec 10 | Out-Null
        }
    }

    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/projects?page=1&pageSize=5" | Out-Null
    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/admin/audit-logs?page=1&pageSize=5" | Out-Null

    $masterDataJob = Invoke-SmokeJsonRequest -Method "POST" -Path "/api/admin/master-data/sync"
    Wait-SmokeJob -JobId ([string]$masterDataJob.data.jobId) -Name "Master data sync" | Out-Null
    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/master-data/locations" | Out-Null
    Invoke-SmokeJsonRequest -Method "GET" -Path "/api/master-data/languages" | Out-Null

    if ([string]::IsNullOrWhiteSpace($SmokeProjectId)) {
        $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
        $projectResponse = Invoke-SmokeJsonRequest `
            -Method "POST" `
            -Path "/api/projects" `
            -Body @{
                name = "Comprehensive smoke $stamp"
                defaultLocation = "Japan"
                defaultLanguage = "Japanese"
                kpi = @{}
                memo = "Created by scripts/smoke-local.ps1"
            }
        $SmokeProjectId = $projectResponse.data.projectId
    }

    $exportJob = Invoke-SmokeJsonRequest `
        -Method "POST" `
        -Path "/api/projects/$SmokeProjectId/exports/csv" `
        -Body @{
            exportType = "external_api_calls"
            filter = @{}
            columns = @("provider", "endpoint", "statusCode", "consumedCredit", "cacheHit", "errorCode", "createdAt")
        }
    Wait-SmokeJob -JobId ([string]$exportJob.data.jobId) -Name "CSV export" | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($DiscordChannelId)) {
        Invoke-SmokeJsonRequest -Method "POST" -Path "/api/admin/notification-channels/$DiscordChannelId/test" | Out-Null
        Invoke-SmokeJsonRequest -Method "GET" -Path "/api/admin/notification-deliveries?page=1&pageSize=5" | Out-Null
    }

    if ($RunBrowserTests) {
        Invoke-BrowserE2ETests
    }

    Write-Host "Comprehensive local smoke test succeeded."
}
finally {
    for ($i = $managedProcesses.Count - 1; $i -ge 0; $i--) {
        Stop-SmokeProcess -Entry $managedProcesses[$i]
    }

    if ($StopDependencies -and -not $SkipDependencies) {
        $downArguments = @("down", "--remove-orphans")
        if ($RemoveDependencyVolumes) {
            $downArguments += "--volumes"
        }

        Invoke-DockerCompose -Arguments $downArguments
    }

    foreach ($name in $previousEnvironment.Keys) {
        if ($null -eq $previousEnvironment[$name]) {
            Remove-Item "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item "Env:$name" $previousEnvironment[$name]
        }
    }
}
