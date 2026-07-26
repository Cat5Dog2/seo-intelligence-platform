param(
    [string]$InfrastructureProject = "src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj",
    [string]$StartupProject = "src/SeoIntelligence.Api/SeoIntelligence.Api.csproj",
    [string]$Configuration = "Debug",
    [string]$DatabaseName = "seo_rakko_v1120_migration_test"
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -notmatch '^[a-z0-9_]+$') {
    throw "DatabaseName must contain only lowercase ASCII letters, digits, and underscores."
}

. (Join-Path $PSScriptRoot "load-dotenv.ps1")
Import-DotEnvFile -Path (Join-Path $PSScriptRoot "..\.env")

$postgresUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "seo" }
$postgresPassword = if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { "seo_dev_password" }
$postgresPort = if ($env:POSTGRES_PORT) { $env:POSTGRES_PORT } else { "5432" }
$adminDatabase = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { "seo" }
$alignmentMigration = "20260726021810_RakkoKeywordV1120Alignment"
$backfillMigration = "20260726032205_RakkoKeywordV1120DataBackfill"

$previousDatabaseEnvironment = @{
    Database__Host = $env:Database__Host
    Database__Port = $env:Database__Port
    Database__Name = $env:Database__Name
    Database__Username = $env:Database__Username
    Database__Password = $env:Database__Password
    Database__GssEncryptionMode = $env:Database__GssEncryptionMode
}

function Invoke-PostgresSql {
    param(
        [string]$Database,
        [string]$Sql
    )

    $Sql | docker compose exec -T postgres psql `
        -v ON_ERROR_STOP=1 `
        -U $postgresUser `
        -d $Database
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL verification command failed for database '$Database'."
    }
}

function Wait-Postgres {
    foreach ($attempt in 1..30) {
        docker compose exec -T postgres pg_isready `
            -U $postgresUser `
            -d $adminDatabase | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL did not become ready for migration verification."
}

function Update-VerificationDatabase {
    param([string]$Migration)

    dotnet tool run dotnet-ef database update $Migration `
        --no-build `
        --configuration $Configuration `
        --project $InfrastructureProject `
        --startup-project $StartupProject
    if ($LASTEXITCODE -ne 0) {
        throw "Migration update failed at '$Migration'."
    }
}

$resetDatabaseSql = @"
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '$DatabaseName'
  AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "$DatabaseName";
CREATE DATABASE "$DatabaseName";
"@

try {
    Wait-Postgres
    Invoke-PostgresSql -Database $adminDatabase -Sql $resetDatabaseSql

    $env:Database__Host = "127.0.0.1"
    $env:Database__Port = $postgresPort
    $env:Database__Name = $DatabaseName
    $env:Database__Username = $postgresUser
    $env:Database__Password = $postgresPassword
    $env:Database__GssEncryptionMode = "Disable"

    Update-VerificationDatabase -Migration $alignmentMigration

    $seedSql = @'
INSERT INTO locations (id, provider, location_code, location_name, country_code, status, synced_at)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'rakko_keyword', 'Japan', 'Japan', 'JP', 'active', now()),
    ('10000000-0000-0000-0000-000000000002', 'rakko_keyword', '2392', 'Japan', 'JP', 'archived', now());

INSERT INTO languages (id, provider, language_code, language_name, status, synced_at)
VALUES
    ('20000000-0000-0000-0000-000000000001', 'rakko_keyword', 'Japanese', 'Japanese', 'active', now()),
    ('20000000-0000-0000-0000-000000000002', 'rakko_keyword', 'ja', 'Japanese', 'archived', now());

INSERT INTO jobs (
    id, workspace_id, project_id, job_type, status, progress, retry_count,
    next_run_at, idempotency_key, request_hash, requested_by, created_at, updated_at, completed_at)
VALUES
    ('30000000-0000-0000-0000-000000000001', '018f3f12-0001-7000-8000-000000000001', NULL, 'RegisterSearchVolumeJob', 'queued', 0, 0, now(), 'legacy-queued-key', 'legacy-queued-hash', 'developer', now(), now(), NULL),
    ('30000000-0000-0000-0000-000000000002', '018f3f12-0001-7000-8000-000000000001', NULL, 'RegisterSearchVolumeJob', 'running', 20, 0, now(), 'canonical-key', 'canonical-hash', 'developer', now(), now(), NULL),
    ('30000000-0000-0000-0000-000000000003', '018f3f12-0001-7000-8000-000000000001', NULL, 'RegisterSearchVolumeJob', 'succeeded', 100, 0, NULL, 'legacy-terminal-key', 'legacy-terminal-hash', 'developer', now(), now(), now()),
    ('30000000-0000-0000-0000-000000000004', '018f3f12-0001-7000-8000-000000000001', NULL, 'OtherJobType', 'running', 10, 0, now(), 'other-key', 'other-hash', 'developer', now(), now(), NULL),
    ('30000000-0000-0000-0000-000000000005', '018f3f12-0001-7000-8000-000000000001', NULL, 'RegisterSearchVolumeJob', 'waiting_external', 40, 0, now(), 'legacy-waiting-key', 'legacy-waiting-hash', 'developer', now(), now(), NULL);

INSERT INTO search_volume_jobs (
    job_id, location, language, aggregation_months, request_options_json, status_json)
VALUES
    ('30000000-0000-0000-0000-000000000001', 'JP', 'ja', 12, '{"location":"JP","language":"ja"}', '{"status":"queued","externalRequestCount":1,"completedExternalRequestCount":0,"estimatedCredit":15}'),
    ('30000000-0000-0000-0000-000000000002', 'Japan', 'Japanese', 12, '{"location":"Japan","language":"Japanese"}', '{"status":"running","externalRequestCount":1,"completedExternalRequestCount":0,"estimatedCredit":15}'),
    ('30000000-0000-0000-0000-000000000003', '2392', 'ja', 12, '{"location":"2392","language":"ja"}', '{"status":"succeeded","externalRequestCount":1,"completedExternalRequestCount":1,"estimatedCredit":15}'),
    ('30000000-0000-0000-0000-000000000004', 'JP', 'ja', 12, '{"location":"JP","language":"ja"}', '{"status":"running","externalRequestCount":1,"completedExternalRequestCount":0,"estimatedCredit":15}'),
    ('30000000-0000-0000-0000-000000000005', '2392', 'Japanese', 12, '{"location":"2392","language":"Japanese"}', '{"status":"waiting_external","externalRequestCount":1,"completedExternalRequestCount":0,"estimatedCredit":15}');

INSERT INTO job_external_requests (
    id, job_id, endpoint, external_request_id, sequence_no, status, retry_count,
    consumed_credit, created_at, updated_at, completed_at)
VALUES
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', '/v1/search-volume', 'legacy-queued', 1, 'queued', 0, 0, now(), now(), NULL),
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000002', '/v1/search-volume', 'canonical-running', 1, 'running', 0, 15, now(), now(), NULL),
    ('40000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000003', '/v1/search-volume', 'legacy-terminal', 1, 'succeeded', 0, 15, now(), now(), now()),
    ('40000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000005', '/v1/search-volume', 'legacy-waiting', 1, 'waiting_external', 0, 15, now(), now(), NULL);
'@
    Invoke-PostgresSql -Database $DatabaseName -Sql $seedSql
    Update-VerificationDatabase -Migration $backfillMigration

    $verifyBackfillSql = @'
DO $verification$
BEGIN
    IF (SELECT status FROM jobs WHERE id = '30000000-0000-0000-0000-000000000001') <> 'canceled' THEN
        RAISE EXCEPTION 'legacy queued job was not canceled';
    END IF;
    IF (SELECT status FROM jobs WHERE id = '30000000-0000-0000-0000-000000000005') <> 'canceled' THEN
        RAISE EXCEPTION 'legacy waiting_external job was not canceled';
    END IF;
    IF (SELECT status FROM jobs WHERE id = '30000000-0000-0000-0000-000000000002') <> 'running' THEN
        RAISE EXCEPTION 'canonical nonterminal job was changed';
    END IF;
    IF (SELECT status FROM jobs WHERE id = '30000000-0000-0000-0000-000000000003') <> 'succeeded' THEN
        RAISE EXCEPTION 'terminal job was changed';
    END IF;
    IF (SELECT status FROM jobs WHERE id = '30000000-0000-0000-0000-000000000004') <> 'running' THEN
        RAISE EXCEPTION 'different job type was changed';
    END IF;
    IF (SELECT status FROM job_external_requests WHERE job_id = '30000000-0000-0000-0000-000000000001') <> 'canceled'
       OR (SELECT error_json ->> 'errorCode' FROM job_external_requests WHERE job_id = '30000000-0000-0000-0000-000000000001') <> 'rakko_v1_12_0_canonical_migration' THEN
        RAISE EXCEPTION 'legacy child request is inconsistent';
    END IF;
    IF (SELECT status FROM job_external_requests WHERE job_id = '30000000-0000-0000-0000-000000000002') <> 'running' THEN
        RAISE EXCEPTION 'canonical child request was changed';
    END IF;
    IF (SELECT status_json ->> 'status' FROM search_volume_jobs WHERE job_id = '30000000-0000-0000-0000-000000000001') <> 'canceled' THEN
        RAISE EXCEPTION 'search volume status_json is inconsistent';
    END IF;
    IF (
        SELECT count(*)
        FROM audit_logs
        WHERE action = 'job.canceled'
          AND before_after_json ->> 'reason' = 'rakko_v1_12_0_canonical_migration'
    ) <> 2 THEN
        RAISE EXCEPTION 'migration cancellation audit count is inconsistent';
    END IF;
    IF (
        SELECT before_after_json -> 'before' ->> 'status'
        FROM audit_logs
        WHERE resource_id = '30000000-0000-0000-0000-000000000001'
          AND action = 'job.canceled'
    ) <> 'queued'
       OR (
        SELECT before_after_json -> 'after' ->> 'status'
        FROM audit_logs
        WHERE resource_id = '30000000-0000-0000-0000-000000000001'
          AND action = 'job.canceled'
    ) <> 'canceled' THEN
        RAISE EXCEPTION 'migration cancellation audit before/after state is inconsistent';
    END IF;
END
$verification$;
'@
    Invoke-PostgresSql -Database $DatabaseName -Sql $verifyBackfillSql

    $simulateOldBackfillSql = @'
UPDATE job_external_requests
SET status = 'canceled', completed_at = now(), updated_at = now()
WHERE job_id = '30000000-0000-0000-0000-000000000002';
UPDATE jobs
SET status = 'canceled',
    error_json = '{"errorCode":"rakko_v1_12_0_canonical_migration","status":"canceled"}',
    completed_at = now(),
    updated_at = now()
WHERE id = '30000000-0000-0000-0000-000000000002';
'@
    Invoke-PostgresSql -Database $DatabaseName -Sql $simulateOldBackfillSql
    Update-VerificationDatabase -Migration "20260726053518_RakkoKeywordV1120BackfillCorrection"

    $verifyCorrectionSql = @'
DO $verification$
BEGIN
    IF (SELECT idempotency_key FROM jobs WHERE id = '30000000-0000-0000-0000-000000000002')
       NOT LIKE 'canonical-key:superseded-by-20260726053518:%' THEN
        RAISE EXCEPTION 'canonical job idempotency key was not released';
    END IF;
    IF (SELECT error_json ->> 'remediation' FROM jobs WHERE id = '30000000-0000-0000-0000-000000000002')
       <> 'canonical_re_registration_enabled' THEN
        RAISE EXCEPTION 'canonical job remediation marker is missing';
    END IF;
    IF NOT EXISTS (
        SELECT 1
        FROM audit_logs
        WHERE resource_id = '30000000-0000-0000-0000-000000000002'
          AND action = 'job.canceled'
          AND before_after_json ->> 'reason' = 'rakko_v1_12_0_canonical_migration'
    ) THEN
        RAISE EXCEPTION 'correction audit is missing';
    END IF;
END
$verification$;

INSERT INTO jobs (
    id, workspace_id, project_id, job_type, status, progress, retry_count,
    next_run_at, idempotency_key, request_hash, requested_by, created_at, updated_at)
VALUES (
    '30000000-0000-0000-0000-000000000006',
    '018f3f12-0001-7000-8000-000000000001',
    NULL,
    'RegisterSearchVolumeJob',
    'queued',
    0,
    0,
    now(),
    'canonical-key',
    'canonical-hash',
    'developer',
    now(),
    now());
'@
    Invoke-PostgresSql -Database $DatabaseName -Sql $verifyCorrectionSql
    Write-Host "Rakko Keyword API v1.12.0 migration verification passed."
}
finally {
    try {
        $cleanupSql = @"
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '$DatabaseName'
  AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "$DatabaseName";
"@
        Invoke-PostgresSql -Database $adminDatabase -Sql $cleanupSql
    }
    finally {
        foreach ($entry in $previousDatabaseEnvironment.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
        }
    }
}
