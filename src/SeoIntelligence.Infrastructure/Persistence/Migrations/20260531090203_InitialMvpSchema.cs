using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMvpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "keywords",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_text = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    text_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keywords", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    language_code = table.Column<string>(type: "text", nullable: false),
                    language_name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    location_code = table.Column<string>(type: "text", nullable: false),
                    location_name = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    default_location = table.Column<string>(type: "text", nullable: false),
                    default_language = table.Column<string>(type: "text", nullable: false),
                    retention_settings_json = table.Column<string>(type: "jsonb", nullable: false),
                    notification_defaults_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.CheckConstraint("ck_workspaces_status", "status IN ('active')");
                });

            migrationBuilder.CreateTable(
                name: "lsi_paa_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: true),
                    importance = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lsi_paa_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_lsi_paa_items_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lsi_paa_items_keywords_seed_keyword_id",
                        column: x => x.seed_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ranking_keywords",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    relevance = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    metrics_snapshot_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranking_keywords", x => x.id);
                    table.ForeignKey(
                        name: "FK_ranking_keywords_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ranking_keywords_keywords_seed_keyword_id",
                        column: x => x.seed_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_contract_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    plan_name = table.Column<string>(type: "text", nullable: false),
                    api_key_limit = table.Column<int>(type: "integer", nullable: false),
                    data_usage_scope = table.Column<string>(type: "text", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    confirmed_by = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_contract_scopes", x => x.id);
                    table.CheckConstraint("ck_api_contract_scopes_status", "status IN ('active', 'archived')");
                    table.ForeignKey(
                        name: "FK_api_contract_scopes_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    key_ref = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    disabled_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_credentials", x => x.id);
                    table.CheckConstraint("ck_api_credentials_status", "status IN ('active', 'disabled')");
                    table.ForeignKey(
                        name: "FK_api_credentials_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<string>(type: "text", nullable: false),
                    before_after_json = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    default_location = table.Column<string>(type: "text", nullable: false),
                    default_language = table.Column<string>(type: "text", nullable: false),
                    kpi_json = table.Column<string>(type: "jsonb", nullable: false),
                    memo = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint("ck_projects_status", "status IN ('active', 'archived')");
                    table.ForeignKey(
                        name: "FK_projects_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_exports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    export_type = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    filter_json = table.Column<string>(type: "jsonb", nullable: false),
                    file_uri = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_exports", x => x.id);
                    table.CheckConstraint("ck_data_exports_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')");
                    table.ForeignKey(
                        name: "FK_data_exports_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_exports_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_run_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    result_resource_type = table.Column<string>(type: "text", nullable: true),
                    result_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_json = table.Column<string>(type: "jsonb", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    request_hash = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.id);
                    table.CheckConstraint("ck_jobs_progress", "progress >= 0 AND progress <= 100");
                    table.CheckConstraint("ck_jobs_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')");
                    table.ForeignKey(
                        name: "FK_jobs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "keyword_seeds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    memo = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_seeds", x => x.id);
                    table.ForeignKey(
                        name: "FK_keyword_seeds_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    webhook_secret_ref = table.Column<string>(type: "text", nullable: false),
                    event_types_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    disabled_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_channels", x => x.id);
                    table.CheckConstraint("ck_notification_channels_status", "status IN ('active', 'disabled')");
                    table.ForeignKey(
                        name: "FK_notification_channels_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_channels_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    importance = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_questions_keywords_seed_keyword_id",
                        column: x => x.seed_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_questions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    canonical_url = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    memo = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.id);
                    table.CheckConstraint("ck_sites_status", "status IN ('active', 'archived')");
                    table.ForeignKey(
                        name: "FK_sites_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_api_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    api_credential_id = table.Column<Guid>(type: "uuid", nullable: true),
                    api_contract_scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    request_uri = table.Column<string>(type: "text", nullable: false),
                    response_hash = table.Column<string>(type: "text", nullable: true),
                    response_uri = table.Column<string>(type: "text", nullable: true),
                    contract_scope_key = table.Column<string>(type: "text", nullable: false),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    consumed_credit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    actor = table.Column<string>(type: "text", nullable: false),
                    retained_until = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_api_calls", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_api_calls_api_contract_scopes_api_contract_scope_id",
                        column: x => x.api_contract_scope_id,
                        principalTable: "api_contract_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_api_calls_api_credentials_api_credential_id",
                        column: x => x.api_credential_id,
                        principalTable: "api_credentials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_api_calls_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_api_calls_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_api_calls_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "search_volume_jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    aggregation_months = table.Column<int>(type: "integer", nullable: false),
                    request_options_json = table.Column<string>(type: "jsonb", nullable: false),
                    status_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_volume_jobs", x => x.job_id);
                    table.ForeignKey(
                        name: "FK_search_volume_jobs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "keyword_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engine = table.Column<string>(type: "text", nullable: false),
                    suggest_class = table.Column<string>(type: "text", nullable: false),
                    engine_count = table.Column<int>(type: "integer", nullable: false),
                    first_seen_range = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_keyword_suggestions_keyword_seeds_seed_id",
                        column: x => x.seed_id,
                        principalTable: "keyword_seeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_keyword_suggestions_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "related_keywords",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_type = table.Column<string>(type: "text", nullable: false),
                    metrics_snapshot_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_related_keywords", x => x.id);
                    table.ForeignKey(
                        name: "FK_related_keywords_keyword_seeds_seed_id",
                        column: x => x.seed_id,
                        principalTable: "keyword_seeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_related_keywords_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resource_type = table.Column<string>(type: "text", nullable: true),
                    resource_id = table.Column<string>(type: "text", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_retry_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                    table.CheckConstraint("ck_notification_deliveries_status", "status IN ('pending', 'retrying', 'succeeded', 'failed')");
                    table.ForeignKey(
                        name: "FK_notification_deliveries_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notification_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "notification_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_external_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    external_request_id = table.Column<string>(type: "text", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consumed_credit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    error_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_external_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_external_requests_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_external_requests_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "keyword_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    contract_scope_key = table.Column<string>(type: "text", nullable: false),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    search_volume = table.Column<int>(type: "integer", nullable: false),
                    seo_difficulty = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    cpc = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    competition = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    first_seen_range = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_keyword_metrics_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_keyword_metrics_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "keyword_monthly_volumes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    contract_scope_key = table.Column<string>(type: "text", nullable: false),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    year_month = table.Column<string>(type: "char(7)", maxLength: 7, nullable: false),
                    search_volume = table.Column<int>(type: "integer", nullable: false),
                    fetched_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_monthly_volumes", x => x.id);
                    table.ForeignKey(
                        name: "FK_keyword_monthly_volumes_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_keyword_monthly_volumes_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_keyword_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opportunity_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    score_components_json = table.Column<string>(type: "jsonb", nullable: false),
                    scored_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_keyword_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_keyword_scores_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_keyword_scores_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_keyword_scores_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "search_volume_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_source = table.Column<string>(type: "text", nullable: false),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false),
                    metrics_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    trends_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_volume_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_search_volume_results_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_search_volume_results_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_search_volume_results_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "workspaces",
                columns: new[] { "id", "created_at", "default_language", "default_location", "name", "notification_defaults_json", "retention_settings_json", "status", "updated_at" },
                values: new object[] { new Guid("018f3f12-0001-7000-8000-000000000001"), new DateTime(2026, 5, 30, 0, 0, 0, 0, DateTimeKind.Utc), "ja", "JP", "Default Workspace", "{\"discordEnabled\":false}", "{\"externalApiRawDataMonths\":24,\"processedDataMonths\":24,\"auditLogMonths\":36}", "active", new DateTime(2026, 5, 30, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "api_contract_scopes",
                columns: new[] { "id", "api_key_limit", "confirmed_at", "confirmed_by", "created_at", "data_usage_scope", "effective_from", "effective_to", "plan_name", "provider", "scope_key", "status", "workspace_id" },
                values: new object[] { new Guid("018f3f12-0002-7000-8000-000000000001"), 5, new DateTime(2026, 5, 30, 0, 0, 0, 0, DateTimeKind.Utc), "developer", new DateTime(2026, 5, 30, 0, 0, 0, 0, DateTimeKind.Utc), "internal", new DateOnly(2026, 5, 30), null, "standard", "rakko_keyword", "rakko_keyword:standard:internal:2026-05-30", "active", new Guid("018f3f12-0001-7000-8000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "ix_api_contract_scopes_effective_from_effective_to",
                table: "api_contract_scopes",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "ix_api_contract_scopes_workspace_id_provider_status",
                table: "api_contract_scopes",
                columns: new[] { "workspace_id", "provider", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_api_contract_scopes_scope_key",
                table: "api_contract_scopes",
                column: "scope_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_credentials_workspace_id_provider_status",
                table: "api_credentials",
                columns: new[] { "workspace_id", "provider", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor",
                table: "audit_logs",
                column: "actor");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_correlation_id",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_resource_type_resource_id",
                table: "audit_logs",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_workspace_id_created_at",
                table: "audit_logs",
                columns: new[] { "workspace_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_exports_project_id",
                table: "data_exports",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_status_created_at",
                table: "data_exports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_workspace_id_project_id_created_at",
                table: "data_exports",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_external_api_calls_api_contract_scope_id",
                table: "external_api_calls",
                column: "api_contract_scope_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_api_calls_api_credential_id",
                table: "external_api_calls",
                column: "api_credential_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_api_calls_contract_scope_key",
                table: "external_api_calls",
                column: "contract_scope_key");

            migrationBuilder.CreateIndex(
                name: "ix_external_api_calls_correlation_id",
                table: "external_api_calls",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_api_calls_job_id",
                table: "external_api_calls",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_api_calls_project_id",
                table: "external_api_calls",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_api_calls_provider_endpoint_created_at",
                table: "external_api_calls",
                columns: new[] { "provider", "endpoint", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_api_calls_response_hash",
                table: "external_api_calls",
                column: "response_hash");

            migrationBuilder.CreateIndex(
                name: "ix_external_api_calls_status_code",
                table: "external_api_calls",
                column: "status_code");

            migrationBuilder.CreateIndex(
                name: "IX_external_api_calls_workspace_id",
                table: "external_api_calls",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_external_requests_external_request_id",
                table: "job_external_requests",
                column: "external_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_external_requests_job_id_sequence_no",
                table: "job_external_requests",
                columns: new[] { "job_id", "sequence_no" });

            migrationBuilder.CreateIndex(
                name: "IX_job_external_requests_source_call_id",
                table: "job_external_requests",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_external_requests_status_updated_at",
                table: "job_external_requests",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_project_id",
                table: "jobs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_status_next_run_at",
                table: "jobs",
                columns: new[] { "status", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_workspace_id_project_id_created_at",
                table: "jobs",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_jobs_workspace_project_type_idempotency_key
                ON jobs (
                    workspace_id,
                    COALESCE(project_id, '00000000-0000-0000-0000-000000000000'::uuid),
                    job_type,
                    idempotency_key
                )
                WHERE idempotency_key IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_keyword_metrics_keyword_id_location_language_contract_scope_key_fetched_at",
                table: "keyword_metrics",
                columns: new[] { "keyword_id", "location", "language", "contract_scope_key", "fetched_at" },
                descending: new[] { false, false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_keyword_metrics_source_call_id",
                table: "keyword_metrics",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_monthly_volumes_keyword_location_language_scope_month_fetched_at",
                table: "keyword_monthly_volumes",
                columns: new[] { "keyword_id", "location", "language", "contract_scope_key", "year_month", "fetched_at" },
                descending: new[] { false, false, false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_keyword_monthly_volumes_source_call_id",
                table: "keyword_monthly_volumes",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_seeds_project_id",
                table: "keyword_seeds",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_suggestions_keyword_id",
                table: "keyword_suggestions",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_suggestions_seed_id",
                table: "keyword_suggestions",
                column: "seed_id");

            migrationBuilder.CreateIndex(
                name: "ix_keywords_normalized_text_trgm",
                table: "keywords",
                column: "normalized_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ux_keywords_language_text_hash",
                table: "keywords",
                columns: new[] { "language", "text_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_languages_status",
                table: "languages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_languages_provider_language_code",
                table: "languages",
                columns: new[] { "provider", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_status",
                table: "locations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_locations_provider_location_code",
                table: "locations",
                columns: new[] { "provider", "location_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lsi_paa_items_keyword_id",
                table: "lsi_paa_items",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_lsi_paa_items_seed_keyword_id",
                table: "lsi_paa_items",
                column: "seed_keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_channels_channel_type",
                table: "notification_channels",
                column: "channel_type");

            migrationBuilder.CreateIndex(
                name: "IX_notification_channels_project_id",
                table: "notification_channels",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_channels_workspace_id_project_id_status",
                table: "notification_channels",
                columns: new[] { "workspace_id", "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_channel_id",
                table: "notification_deliveries",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_correlation_id",
                table: "notification_deliveries",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_job_id",
                table: "notification_deliveries",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_project_id",
                table: "notification_deliveries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_resource_type_resource_id",
                table: "notification_deliveries",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_status_next_retry_at",
                table: "notification_deliveries",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_workspace_id_project_id_created_at",
                table: "notification_deliveries",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_project_keyword_scores_keyword_id",
                table: "project_keyword_scores",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_keyword_scores_project_id_opportunity_score",
                table: "project_keyword_scores",
                columns: new[] { "project_id", "opportunity_score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_project_keyword_scores_source_call_id",
                table: "project_keyword_scores",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "ux_project_keyword_scores_project_id_keyword_id_location_language",
                table: "project_keyword_scores",
                columns: new[] { "project_id", "keyword_id", "location", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_workspace_id_status",
                table: "projects",
                columns: new[] { "workspace_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_projects_workspace_id_name",
                table: "projects",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_project_id",
                table: "questions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_seed_keyword_id",
                table: "questions",
                column: "seed_keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_ranking_keywords_keyword_id",
                table: "ranking_keywords",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_ranking_keywords_seed_keyword_id",
                table: "ranking_keywords",
                column: "seed_keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_related_keywords_keyword_id",
                table: "related_keywords",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_related_keywords_seed_id",
                table: "related_keywords",
                column: "seed_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_volume_results_cache_hit",
                table: "search_volume_results",
                column: "cache_hit");

            migrationBuilder.CreateIndex(
                name: "ix_search_volume_results_job_id",
                table: "search_volume_results",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_volume_results_keyword_id",
                table: "search_volume_results",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "IX_search_volume_results_source_call_id",
                table: "search_volume_results",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_sites_domain",
                table: "sites",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "ix_sites_project_id_status",
                table: "sites",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_status",
                table: "workspaces",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_name",
                table: "workspaces",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_jobs_workspace_project_type_idempotency_key;");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "data_exports");

            migrationBuilder.DropTable(
                name: "job_external_requests");

            migrationBuilder.DropTable(
                name: "keyword_metrics");

            migrationBuilder.DropTable(
                name: "keyword_monthly_volumes");

            migrationBuilder.DropTable(
                name: "keyword_suggestions");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "lsi_paa_items");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "project_keyword_scores");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "ranking_keywords");

            migrationBuilder.DropTable(
                name: "related_keywords");

            migrationBuilder.DropTable(
                name: "search_volume_jobs");

            migrationBuilder.DropTable(
                name: "search_volume_results");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "notification_channels");

            migrationBuilder.DropTable(
                name: "keyword_seeds");

            migrationBuilder.DropTable(
                name: "external_api_calls");

            migrationBuilder.DropTable(
                name: "keywords");

            migrationBuilder.DropTable(
                name: "api_contract_scopes");

            migrationBuilder.DropTable(
                name: "api_credentials");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}
