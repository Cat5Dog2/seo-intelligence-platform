using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ラッコキーワードAPI v1.14.0対応: よくある質問検索が返す出現時期(firstSeenRange)と、
    /// 検索順位チェック結果のキーワード登録順(entryNo)を保存するための列追加。
    /// entryNoは GET /v1/search-rank/{requestId}/results/{entryNo}/serp のパスに使う。
    /// いずれもnullable列の追加のみで、既存行の再取得や補正は不要。
    /// </remarks>
    public partial class RakkoKeywordV1140Alignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "entry_no",
                table: "rank_results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_seen_range",
                table: "questions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "entry_no",
                table: "rank_results");

            migrationBuilder.DropColumn(
                name: "first_seen_range",
                table: "questions");
        }
    }
}
