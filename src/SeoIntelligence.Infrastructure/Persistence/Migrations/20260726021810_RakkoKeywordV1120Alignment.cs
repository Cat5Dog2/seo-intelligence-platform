using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ラッコキーワードAPI v1.12.0対応の第1段: 契約スコープの世代交代と、
    /// シード既定値(JP/ja)の正準名(Japan/Japanese)への変換。
    /// 旧マスタコード値・非終端ジョブの移行は後続の RakkoKeywordV1120DataBackfill が行う
    /// (本マイグレーションは適用済み環境が存在するため内容を変更しない)。
    /// </remarks>
    public partial class RakkoKeywordV1120Alignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "api_contract_scopes",
                keyColumn: "id",
                keyValue: new Guid("018f3f12-0002-7000-8000-000000000001"),
                columns: new[] { "effective_to", "status" },
                values: new object[] { new DateOnly(2026, 7, 25), "archived" });

            migrationBuilder.InsertData(
                table: "api_contract_scopes",
                columns: new[] { "id", "api_key_limit", "confirmed_at", "confirmed_by", "created_at", "data_usage_scope", "effective_from", "effective_to", "plan_name", "provider", "scope_key", "status", "workspace_id" },
                values: new object[] { new Guid("018f3f12-0002-7000-8000-000000000002"), 5, new DateTime(2026, 7, 26, 0, 0, 0, 0, DateTimeKind.Utc), "developer", new DateTime(2026, 7, 26, 0, 0, 0, 0, DateTimeKind.Utc), "internal", new DateOnly(2026, 7, 26), null, "standard", "rakko_keyword", "rakko_keyword:standard:internal:2026-07-26", "active", new Guid("018f3f12-0001-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "workspaces",
                keyColumn: "id",
                keyValue: new Guid("018f3f12-0001-7000-8000-000000000001"),
                columns: new[] { "default_language", "default_location" },
                values: new object[] { "Japanese", "Japan" });

            // ラッコキーワードAPI v1.12.0以降、location/languageはmetadata一覧の名前が正準値。
            // シード行以外の既存workspace/projectの旧コード値も名前へ移行する。
            migrationBuilder.Sql("UPDATE workspaces SET default_location = 'Japan' WHERE default_location = 'JP';");
            migrationBuilder.Sql("UPDATE workspaces SET default_language = 'Japanese' WHERE default_language = 'ja';");
            migrationBuilder.Sql("UPDATE projects SET default_location = 'Japan' WHERE default_location = 'JP';");
            migrationBuilder.Sql("UPDATE projects SET default_language = 'Japanese' WHERE default_language = 'ja';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            // 本マイグレーションは不可逆:
            // - 新契約スコープはexternal_api_calls(Restrict FK)から参照され得るため削除できない。
            // - location/languageの正準名変換は、Upで変換した行と元から正しかった行を区別できないため
            //   逆変換するとデータを破壊する(旧API仕様もリクエストには名前を要求していた)。
            => throw new NotSupportedException(
                "RakkoKeywordV1120Alignment is irreversible: the new api_contract_scope may already be referenced by " +
                "external_api_calls, and the canonical location/language name conversion cannot be reversed without " +
                "corrupting rows that were already correct.");
    }
}
