using Microsoft.EntityFrameworkCore.Migrations;

using System;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// RakkoKeywordV1120DataBackfillの初版を適用済みの環境を補正する。
    /// 初版が正準値の非終端ジョブもキャンセルしていた場合、元の状態は復元せず、
    /// 既存ジョブを監査付きで保持したままidempotency keyを退避して再登録可能にする。
    /// </remarks>
    public partial class RakkoKeywordV1120BackfillCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE rakko_v1120_canceled_jobs ON COMMIT DROP AS
                SELECT
                    job.id,
                    job.workspace_id,
                    job.idempotency_key,
                    job.error_json,
                    (
                        EXISTS (
                            SELECT 1
                            FROM locations location
                            WHERE location.provider = 'rakko_keyword'
                              AND location.status = 'active'
                              AND lower(location.location_name) = lower(search_volume.location)
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM languages language
                            WHERE language.provider = 'rakko_keyword'
                              AND language.status = 'active'
                              AND lower(language.language_name) = lower(search_volume.language)
                        )
                    ) AS was_canonical
                FROM jobs job
                INNER JOIN search_volume_jobs search_volume ON search_volume.job_id = job.id
                WHERE job.job_type = 'RegisterSearchVolumeJob'
                  AND job.status = 'canceled'
                  AND job.error_json ->> 'errorCode' = 'rakko_v1_12_0_canonical_migration';

                INSERT INTO audit_logs (
                    id,
                    workspace_id,
                    actor,
                    action,
                    resource_type,
                    resource_id,
                    before_after_json,
                    correlation_id,
                    ip_address,
                    user_agent,
                    created_at)
                SELECT
                    gen_random_uuid(),
                    target.workspace_id,
                    'developer',
                    'job.canceled',
                    'job',
                    target.id::text,
                    jsonb_build_object(
                        'before', jsonb_build_object(
                            'status', 'nonterminal_status_not_preserved_by_prior_migration',
                            'idempotencyKey', target.idempotency_key),
                        'after', jsonb_build_object(
                            'status', 'canceled',
                            'idempotencyKey',
                                CASE
                                    WHEN target.was_canonical AND target.idempotency_key IS NOT NULL
                                        THEN target.idempotency_key || ':superseded-by-20260726053518:' || target.id::text
                                    ELSE target.idempotency_key
                                END,
                            'canonicalReRegistrationEnabled', target.was_canonical),
                        'reason', 'rakko_v1_12_0_canonical_migration'),
                    NULL,
                    NULL,
                    'EF migration RakkoKeywordV1120BackfillCorrection',
                    now()
                FROM rakko_v1120_canceled_jobs target
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM audit_logs audit
                    WHERE audit.action = 'job.canceled'
                      AND audit.resource_type = 'job'
                      AND audit.resource_id = target.id::text
                      AND audit.before_after_json ->> 'reason' = 'rakko_v1_12_0_canonical_migration'
                );

                UPDATE jobs job
                SET idempotency_key =
                        job.idempotency_key || ':superseded-by-20260726053518:' || job.id::text,
                    error_json = jsonb_set(
                        COALESCE(job.error_json, '{}'::jsonb),
                        '{remediation}',
                        '"canonical_re_registration_enabled"'::jsonb,
                        true),
                    updated_at = now()
                FROM rakko_v1120_canceled_jobs target
                WHERE job.id = target.id
                  AND target.was_canonical
                  AND job.idempotency_key IS NOT NULL
                  AND job.idempotency_key NOT LIKE '%:superseded-by-20260726053518:%';

                DROP TABLE rakko_v1120_canceled_jobs;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            // 元の非終端statusは初版Migrationで失われており、安全に復元できない。
            => throw new NotSupportedException(
                "RakkoKeywordV1120BackfillCorrection is irreversible because the original status of jobs " +
                "canceled by the prior migration is unavailable.");
    }
}
