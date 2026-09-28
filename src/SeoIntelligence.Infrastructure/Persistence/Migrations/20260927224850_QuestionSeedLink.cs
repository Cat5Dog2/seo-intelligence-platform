using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// よくある質問を、保存したキーワード探索のシード(keyword_seeds)へ直接紐づける。
    /// 従来はシードキーワード(seed_keyword_id)にしか紐づかず、同じシードキーワードの探索が重なると
    /// 候補語CSVや再試行時の取得済み判定で質問を取り違えた。nullable列の追加のみで、既存行はNULLのまま。
    /// 既存行はジョブの実行期間から推定して帰属させるため、推測によるバックフィルは行わない。
    /// </remarks>
    public partial class QuestionSeedLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "seed_id",
                table: "questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_seed_id",
                table: "questions",
                column: "seed_id");

            migrationBuilder.AddForeignKey(
                name: "FK_questions_keyword_seeds_seed_id",
                table: "questions",
                column: "seed_id",
                principalTable: "keyword_seeds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_questions_keyword_seeds_seed_id",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_questions_seed_id",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "seed_id",
                table: "questions");
        }
    }
}
