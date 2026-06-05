using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3DbApiFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_sessions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_sessions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cannibalization_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_url = table.Column<string>(type: "text", nullable: false),
                    competing_urls_json = table.Column<string>(type: "jsonb", nullable: false),
                    severity_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    evidence_json = table.Column<string>(type: "jsonb", nullable: false),
                    recommendation_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cannibalization_candidates", x => x.id);
                    table.CheckConstraint("ck_cannibalization_candidates_status", "status IN ('draft', 'active', 'archived', 'completed')");
                    table.ForeignKey(
                        name: "FK_cannibalization_candidates_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cannibalization_candidates_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    import_type = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    source_file_uri = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    validation_errors_json = table.Column<string>(type: "jsonb", nullable: false),
                    requested_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_imports", x => x.id);
                    table.CheckConstraint("ck_data_imports_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')");
                    table.ForeignKey(
                        name: "FK_data_imports_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_imports_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_connector_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    connector_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    auth_ref = table.Column<string>(type: "text", nullable: true),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    disabled_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_connector_settings", x => x.id);
                    table.CheckConstraint("ck_external_connector_settings_status", "status IN ('active', 'disabled')");
                    table.ForeignKey(
                        name: "FK_external_connector_settings_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_connector_settings_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "text", nullable: false),
                    period = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    file_uri = table.Column<string>(type: "text", nullable: true),
                    share_token_hash = table.Column<string>(type: "text", nullable: true),
                    share_expires_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    share_revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    generated_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                    table.CheckConstraint("ck_reports_format", "format IN ('pdf', 'excel')");
                    table.CheckConstraint("ck_reports_status", "status IN ('draft', 'active', 'archived', 'completed')");
                    table.ForeignKey(
                        name: "FK_reports_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rewrite_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_url = table.Column<string>(type: "text", nullable: false),
                    priority_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    reason_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assignee_actor = table.Column<string>(type: "text", nullable: false),
                    memo = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rewrite_tasks", x => x.id);
                    table.CheckConstraint("ck_rewrite_tasks_status", "status IN ('draft', 'active', 'archived', 'completed')");
                    table.ForeignKey(
                        name: "FK_rewrite_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_role = table.Column<string>(type: "text", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: false),
                    tool_calls_json = table.Column<string>(type: "jsonb", nullable: false),
                    reference_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    redaction_status = table.Column<string>(type: "text", nullable: false),
                    review_status = table.Column<string>(type: "text", nullable: false),
                    token_usage = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_messages_ai_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "ai_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_connector_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_setting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    result_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    error_json = table.Column<string>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_connector_runs", x => x.id);
                    table.CheckConstraint("ck_external_connector_runs_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')");
                    table.ForeignKey(
                        name: "FK_external_connector_runs_external_connector_settings_connect~",
                        column: x => x.connector_setting_id,
                        principalTable: "external_connector_settings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_connector_runs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_connector_runs_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_messages_session_id_created_at",
                table: "ai_messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_sessions_actor",
                table: "ai_sessions",
                column: "actor");

            migrationBuilder.CreateIndex(
                name: "IX_ai_sessions_project_id",
                table: "ai_sessions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_sessions_workspace_id_project_id_created_at",
                table: "ai_sessions",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cannibalization_candidates_keyword_id",
                table: "cannibalization_candidates",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_cannibalization_candidates_project_id_keyword_id_detected_at",
                table: "cannibalization_candidates",
                columns: new[] { "project_id", "keyword_id", "detected_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cannibalization_candidates_project_id_status_severity_score",
                table: "cannibalization_candidates",
                columns: new[] { "project_id", "status", "severity_score" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_data_imports_project_id",
                table: "data_imports",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_imports_status_created_at",
                table: "data_imports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_imports_workspace_id_project_id_created_at",
                table: "data_imports",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_connector_runs_connector_setting_id_created_at",
                table: "external_connector_runs",
                columns: new[] { "connector_setting_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_external_connector_runs_project_id",
                table: "external_connector_runs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_connector_runs_workspace_id_project_id_status",
                table: "external_connector_runs",
                columns: new[] { "workspace_id", "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_external_connector_settings_project_id",
                table: "external_connector_settings",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_connector_settings_workspace_id_project_id_connector_type_status",
                table: "external_connector_settings",
                columns: new[] { "workspace_id", "project_id", "connector_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_project_id_report_type_period_format",
                table: "reports",
                columns: new[] { "project_id", "report_type", "period", "format" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_share_expires_at",
                table: "reports",
                column: "share_expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_reports_share_revoked_at",
                table: "reports",
                column: "share_revoked_at");

            migrationBuilder.CreateIndex(
                name: "ux_reports_share_token_hash",
                table: "reports",
                column: "share_token_hash",
                unique: true,
                filter: "share_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_rewrite_tasks_project_id_status_priority_score",
                table: "rewrite_tasks",
                columns: new[] { "project_id", "status", "priority_score" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_messages");

            migrationBuilder.DropTable(
                name: "cannibalization_candidates");

            migrationBuilder.DropTable(
                name: "data_imports");

            migrationBuilder.DropTable(
                name: "external_connector_runs");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "rewrite_tasks");

            migrationBuilder.DropTable(
                name: "ai_sessions");

            migrationBuilder.DropTable(
                name: "external_connector_settings");
        }
    }
}
