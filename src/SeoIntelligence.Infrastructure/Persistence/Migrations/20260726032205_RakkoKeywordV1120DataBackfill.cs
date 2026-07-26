using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ラッコキーワードAPI v1.12.0対応の第2段(RakkoKeywordV1120Alignmentの続き)。
    /// - 旧マスタ(locations/languages)のコード→名前対応を使い、旧UIが保存し得た
    ///   あらゆるコード値(例: 2392、en)をworkspace/projectの既定値から名前へ変換する。
    /// - 旧コード値を保持する非終端の検索ボリューム登録ジョブだけをキャンセルする。
    ///   保存済みリクエストを書き換えるとrequestHash/idempotencyKeyと実内容が不整合になるため、
    ///   リクエストは変更せず、正準名での再登録を促す方針とする。
    /// </remarks>
    public partial class RakkoKeywordV1120DataBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧マスタのコード→名前対応で、workspace/projectに残る旧コード値を正準名へ変換する。
            // (旧マスタ行は再同期後もarchivedで残るため、status条件は付けない)
            migrationBuilder.Sql("""
                UPDATE workspaces w SET default_location = l.location_name
                FROM locations l
                WHERE l.provider = 'rakko_keyword'
                  AND l.location_code = w.default_location
                  AND l.location_code <> l.location_name;
                """);
            migrationBuilder.Sql("""
                UPDATE workspaces w SET default_language = lg.language_name
                FROM languages lg
                WHERE lg.provider = 'rakko_keyword'
                  AND lg.language_code = w.default_language
                  AND lg.language_code <> lg.language_name;
                """);
            migrationBuilder.Sql("""
                UPDATE projects p SET default_location = l.location_name
                FROM locations l
                WHERE l.provider = 'rakko_keyword'
                  AND l.location_code = p.default_location
                  AND l.location_code <> l.location_name;
                """);
            migrationBuilder.Sql("""
                UPDATE projects p SET default_language = lg.language_name
                FROM languages lg
                WHERE lg.provider = 'rakko_keyword'
                  AND lg.language_code = p.default_language
                  AND lg.language_code <> lg.language_name;
                """);

            // RakkoKeywordV1120Alignment適用後に旧値(JP/ja)で作成された行の取りこぼしを掃除する
            // (マスタ未同期でコード対応が引けないDB向けのフォールバックを兼ねる)。
            migrationBuilder.Sql("UPDATE workspaces SET default_location = 'Japan' WHERE default_location = 'JP';");
            migrationBuilder.Sql("UPDATE workspaces SET default_language = 'Japanese' WHERE default_language = 'ja';");
            migrationBuilder.Sql("UPDATE projects SET default_location = 'Japan' WHERE default_location = 'JP';");
            migrationBuilder.Sql("UPDATE projects SET default_language = 'Japanese' WHERE default_language = 'ja';");

            // 旧コード値を保持する非終端の検索ボリューム登録ジョブだけをキャンセルする。
            // 対象集合を一時表へ固定し、監査ログ、子request、業務status、親jobを同じ集合で更新する。
            // runningの更新競合と旧APIからの再登録を防ぐため、API/Web/Worker停止中に適用する。
            migrationBuilder.Sql("""
                CREATE TEMP TABLE rakko_v1120_legacy_jobs ON COMMIT DROP AS
                SELECT
                    j.id,
                    j.workspace_id,
                    j.status,
                    j.error_json
                FROM jobs j
                INNER JOIN search_volume_jobs svj ON svj.job_id = j.id
                WHERE j.job_type = 'RegisterSearchVolumeJob'
                  AND j.status IN ('queued', 'running', 'waiting_external', 'failed_retryable')
                  AND (
                      lower(svj.location) = 'jp'
                      OR lower(svj.language) = 'ja'
                      OR EXISTS (
                          SELECT 1
                          FROM locations l
                          WHERE l.provider = 'rakko_keyword'
                            AND lower(l.location_code) = lower(svj.location)
                            AND lower(l.location_code) <> lower(l.location_name)
                      )
                      OR EXISTS (
                          SELECT 1
                          FROM languages lg
                          WHERE lg.provider = 'rakko_keyword'
                            AND lower(lg.language_code) = lower(svj.language)
                            AND lower(lg.language_code) <> lower(lg.language_name)
                      )
                  );

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
                            'status', target.status,
                            'error', target.error_json),
                        'after', jsonb_build_object(
                            'status', 'canceled',
                            'errorCode', 'rakko_v1_12_0_canonical_migration'),
                        'reason', 'rakko_v1_12_0_canonical_migration'),
                    NULL,
                    NULL,
                    'EF migration RakkoKeywordV1120DataBackfill',
                    now()
                FROM rakko_v1120_legacy_jobs target
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM audit_logs audit
                    WHERE audit.action = 'job.canceled'
                      AND audit.resource_type = 'job'
                      AND audit.resource_id = target.id::text
                      AND audit.before_after_json ->> 'reason' = 'rakko_v1_12_0_canonical_migration'
                );

                UPDATE job_external_requests request
                SET status = 'canceled',
                    error_json = '{"kind":"Canceled","httpStatusCode":null,"errorCode":"rakko_v1_12_0_canonical_migration","message":"ラッコキーワードAPI v1.12.0対応: 旧location/language値の正準名移行に伴いキャンセルされました。","status":"canceled","retryable":false}'::jsonb,
                    updated_at = now(),
                    completed_at = now()
                FROM rakko_v1120_legacy_jobs target
                WHERE request.job_id = target.id
                  AND request.status IN ('queued', 'running', 'waiting_external', 'failed_retryable');

                UPDATE search_volume_jobs search_volume
                SET status_json = jsonb_set(
                    jsonb_set(
                        COALESCE(search_volume.status_json, '{}'::jsonb),
                        '{status}',
                        '"canceled"'::jsonb,
                        true),
                    '{message}',
                    to_jsonb('ラッコキーワードAPI v1.12.0対応: 正準名で再登録してください。'::text),
                    true)
                FROM rakko_v1120_legacy_jobs target
                WHERE search_volume.job_id = target.id;

                UPDATE jobs job
                SET status = 'canceled',
                    error_json = '{"kind":"Canceled","httpStatusCode":null,"errorCode":"rakko_v1_12_0_canonical_migration","message":"ラッコキーワードAPI v1.12.0対応: 旧location/language値の正準名移行に伴いキャンセルされました。正準名(例: Japan / Japanese)で再登録してください。","status":"canceled","retryable":false}'::jsonb,
                    next_run_at = NULL,
                    completed_at = now(),
                    updated_at = now()
                FROM rakko_v1120_legacy_jobs target
                WHERE job.id = target.id;

                DROP TABLE rakko_v1120_legacy_jobs;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            // 本マイグレーションは不可逆:
            // - 正準名変換は、変換した行と元から正しかった行を区別できないため逆変換できない。
            // - キャンセルしたジョブの旧status/error_jsonは保持していない。
            => throw new NotSupportedException(
                "RakkoKeywordV1120DataBackfill is irreversible: the canonical name conversion cannot distinguish " +
                "converted rows from rows that were already correct, and the original status of canceled jobs is not preserved.");
    }
}
