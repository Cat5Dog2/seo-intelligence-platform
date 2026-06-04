using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2DbApiExternalApiFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "text", nullable: false),
                    condition_json = table.Column<string>(type: "jsonb", nullable: false),
                    notification_channel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                    table.CheckConstraint("ck_alerts_status", "status IN ('active', 'disabled')");
                    table.ForeignKey(
                        name: "FK_alerts_notification_channels_notification_channel_id",
                        column: x => x.notification_channel_id,
                        principalTable: "notification_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alerts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artifact_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artifact_type = table.Column<string>(type: "text", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    content_uri = table.Column<string>(type: "text", nullable: true),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    review_status = table.Column<string>(type: "text", nullable: false),
                    change_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifact_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_artifact_versions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artifact_versions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "co_occurrence_words",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "text", nullable: false),
                    occurrence_counts_json = table.Column<string>(type: "jsonb", nullable: false),
                    site_counts_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_co_occurrence_words", x => x.id);
                    table.ForeignKey(
                        name: "FK_co_occurrence_words_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_co_occurrence_words_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competitive_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_domain = table.Column<string>(type: "text", nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    traffic_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    keyword_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_rate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    unique_counts_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competitive_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_competitive_results_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competitor_sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    duplicate_rate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competitor_sites", x => x.id);
                    table.ForeignKey(
                        name: "FK_competitor_sites_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_search_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    traffic_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    top_keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_search_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_content_search_results_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_search_results_keywords_top_keyword_id",
                        column: x => x.top_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_search_results_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "influx_keyword_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    ranked_url = table.Column<string>(type: "text", nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    metrics_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_influx_keyword_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_influx_keyword_results_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_influx_keyword_results_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "influx_page_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    page_url = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    keyword_count = table.Column<int>(type: "integer", nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    traffic_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    top_keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_influx_page_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_influx_page_results_keywords_top_keyword_id",
                        column: x => x.top_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_influx_page_results_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rank_check_jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    match_type = table.Column<string>(type: "text", nullable: false),
                    with_metrics = table.Column<bool>(type: "boolean", nullable: false),
                    request_options_json = table.Column<string>(type: "jsonb", nullable: false),
                    status_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rank_check_jobs", x => x.job_id);
                    table.ForeignKey(
                        name: "FK_rank_check_jobs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rank_check_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rank_check_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_rank_check_targets_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rank_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    ranked_url = table.Column<string>(type: "text", nullable: false),
                    estimated_traffic = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    metrics_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_scope_key = table.Column<string>(type: "text", nullable: false),
                    checked_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rank_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_rank_results_external_api_calls_source_call_id",
                        column: x => x.source_call_id,
                        principalTable: "external_api_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_results_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_results_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_results_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "serp_headline_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    headline_count = table.Column<int>(type: "integer", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serp_headline_pages", x => x.id);
                    table.ForeignKey(
                        name: "FK_serp_headline_pages_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_serp_headline_pages_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "topic_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    representative_keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_clusters", x => x.id);
                    table.ForeignKey(
                        name: "FK_topic_clusters_keywords_representative_keyword_id",
                        column: x => x.representative_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_topic_clusters_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_topic_clusters_topic_clusters_parent_id",
                        column: x => x.parent_id,
                        principalTable: "topic_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    previous_value_json = table.Column<string>(type: "jsonb", nullable: false),
                    current_value_json = table.Column<string>(type: "jsonb", nullable: false),
                    evidence_json = table.Column<string>(type: "jsonb", nullable: false),
                    notification_delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    triggered_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_events_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_events_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_events_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_events_notification_deliveries_notification_delivery_~",
                        column: x => x.notification_delivery_id,
                        principalTable: "notification_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_events_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "co_occurrence_page_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    co_word_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    count_in_headline = table.Column<int>(type: "integer", nullable: false),
                    count_in_title = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_co_occurrence_page_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_co_occurrence_page_details_co_occurrence_words_co_word_id",
                        column: x => x.co_word_id,
                        principalTable: "co_occurrence_words",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "serp_headlines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    order_no = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serp_headlines", x => x.id);
                    table.ForeignKey(
                        name: "FK_serp_headlines_serp_headline_pages_page_id",
                        column: x => x.page_id,
                        principalTable: "serp_headline_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "article_briefs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    target_keyword_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    review_status = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_briefs", x => x.id);
                    table.CheckConstraint("ck_article_briefs_status", "status IN ('draft', 'active', 'archived', 'completed')");
                    table.ForeignKey(
                        name: "FK_article_briefs_keywords_target_keyword_id",
                        column: x => x.target_keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_article_briefs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_article_briefs_topic_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "topic_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cluster_keywords",
                columns: table => new
                {
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    opportunity_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    intent_label = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_keywords", x => new { x.cluster_id, x.keyword_id });
                    table.ForeignKey(
                        name: "FK_cluster_keywords_keywords_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "keywords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cluster_keywords_topic_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "topic_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_events_alert_id_triggered_at",
                table: "alert_events",
                columns: new[] { "alert_id", "triggered_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_job_id",
                table: "alert_events",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_keyword_id",
                table: "alert_events",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_events_notification_delivery_id",
                table: "alert_events",
                column: "notification_delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_events_project_id_triggered_at",
                table: "alert_events",
                columns: new[] { "project_id", "triggered_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_notification_channel_id",
                table: "alerts",
                column: "notification_channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_project_id_status",
                table: "alerts",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_article_briefs_cluster_id",
                table: "article_briefs",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_article_briefs_project_id_status",
                table: "article_briefs",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_article_briefs_target_keyword_id",
                table: "article_briefs",
                column: "target_keyword_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_versions_project_id",
                table: "artifact_versions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_artifact_versions_workspace_id_project_id_created_at",
                table: "artifact_versions",
                columns: new[] { "workspace_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_artifact_versions_artifact_type_artifact_id_version_no",
                table: "artifact_versions",
                columns: new[] { "artifact_type", "artifact_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cluster_keywords_keyword_id",
                table: "cluster_keywords",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_co_occurrence_page_details_co_word_id_rank",
                table: "co_occurrence_page_details",
                columns: new[] { "co_word_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "IX_co_occurrence_words_keyword_id",
                table: "co_occurrence_words",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_co_occurrence_words_project_id_keyword_id",
                table: "co_occurrence_words",
                columns: new[] { "project_id", "keyword_id" });

            migrationBuilder.CreateIndex(
                name: "ix_co_occurrence_words_word",
                table: "co_occurrence_words",
                column: "word");

            migrationBuilder.CreateIndex(
                name: "ix_competitive_results_project_id_site_domain",
                table: "competitive_results",
                columns: new[] { "project_id", "site_domain" });

            migrationBuilder.CreateIndex(
                name: "ix_competitor_sites_project_id_domain",
                table: "competitor_sites",
                columns: new[] { "project_id", "domain" });

            migrationBuilder.CreateIndex(
                name: "ix_content_search_results_domain",
                table: "content_search_results",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "IX_content_search_results_keyword_id",
                table: "content_search_results",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_search_results_project_id_keyword_id",
                table: "content_search_results",
                columns: new[] { "project_id", "keyword_id" });

            migrationBuilder.CreateIndex(
                name: "ix_content_search_results_top_keyword_id",
                table: "content_search_results",
                column: "top_keyword_id");

            migrationBuilder.Sql("""
                CREATE INDEX ix_content_search_results_title_description_fts
                ON content_search_results
                USING gin (to_tsvector('simple', concat_ws(' ', title, description)));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_influx_keyword_results_keyword_id",
                table: "influx_keyword_results",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_influx_keyword_results_project_id_target",
                table: "influx_keyword_results",
                columns: new[] { "project_id", "target" });

            migrationBuilder.CreateIndex(
                name: "ix_influx_keyword_results_rank",
                table: "influx_keyword_results",
                column: "rank");

            migrationBuilder.CreateIndex(
                name: "ix_influx_page_results_project_id_target",
                table: "influx_page_results",
                columns: new[] { "project_id", "target" });

            migrationBuilder.CreateIndex(
                name: "ix_influx_page_results_top_keyword_id",
                table: "influx_page_results",
                column: "top_keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_rank_check_targets_job_id",
                table: "rank_check_targets",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_rank_results_contract_scope_key",
                table: "rank_results",
                column: "contract_scope_key");

            migrationBuilder.CreateIndex(
                name: "IX_rank_results_job_id",
                table: "rank_results",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_rank_results_keyword_id",
                table: "rank_results",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_rank_results_position",
                table: "rank_results",
                column: "position");

            migrationBuilder.CreateIndex(
                name: "ix_rank_results_project_id_keyword_id_target_checked_at",
                table: "rank_results",
                columns: new[] { "project_id", "keyword_id", "target", "checked_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_rank_results_source_call_id",
                table: "rank_results",
                column: "source_call_id");

            migrationBuilder.CreateIndex(
                name: "IX_serp_headline_pages_keyword_id",
                table: "serp_headline_pages",
                column: "keyword_id");

            migrationBuilder.CreateIndex(
                name: "ix_serp_headline_pages_project_id_keyword_id",
                table: "serp_headline_pages",
                columns: new[] { "project_id", "keyword_id" });

            migrationBuilder.CreateIndex(
                name: "ix_serp_headline_pages_rank",
                table: "serp_headline_pages",
                column: "rank");

            migrationBuilder.CreateIndex(
                name: "ix_serp_headlines_page_id_order_no",
                table: "serp_headlines",
                columns: new[] { "page_id", "order_no" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_clusters_parent_id",
                table: "topic_clusters",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_topic_clusters_project_id_parent_id",
                table: "topic_clusters",
                columns: new[] { "project_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_topic_clusters_representative_keyword_id",
                table: "topic_clusters",
                column: "representative_keyword_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_content_search_results_title_description_fts;");

            migrationBuilder.DropTable(
                name: "alert_events");

            migrationBuilder.DropTable(
                name: "article_briefs");

            migrationBuilder.DropTable(
                name: "artifact_versions");

            migrationBuilder.DropTable(
                name: "cluster_keywords");

            migrationBuilder.DropTable(
                name: "co_occurrence_page_details");

            migrationBuilder.DropTable(
                name: "competitive_results");

            migrationBuilder.DropTable(
                name: "competitor_sites");

            migrationBuilder.DropTable(
                name: "content_search_results");

            migrationBuilder.DropTable(
                name: "influx_keyword_results");

            migrationBuilder.DropTable(
                name: "influx_page_results");

            migrationBuilder.DropTable(
                name: "rank_check_jobs");

            migrationBuilder.DropTable(
                name: "rank_check_targets");

            migrationBuilder.DropTable(
                name: "rank_results");

            migrationBuilder.DropTable(
                name: "serp_headlines");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "topic_clusters");

            migrationBuilder.DropTable(
                name: "co_occurrence_words");

            migrationBuilder.DropTable(
                name: "serp_headline_pages");
        }
    }
}
