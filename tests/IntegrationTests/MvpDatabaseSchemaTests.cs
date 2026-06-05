using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace IntegrationTests;

public sealed class MvpDatabaseSchemaTests
{
    private static readonly string[] RequiredMvpTables =
    [
        "workspaces",
        "projects",
        "sites",
        "api_credentials",
        "api_contract_scopes",
        "notification_channels",
        "notification_deliveries",
        "audit_logs",
        "locations",
        "languages",
        "external_api_calls",
        "jobs",
        "job_external_requests",
        "keyword_seeds",
        "keywords",
        "keyword_suggestions",
        "related_keywords",
        "questions",
        "lsi_paa_items",
        "ranking_keywords",
        "search_volume_jobs",
        "search_volume_results",
        "keyword_metrics",
        "keyword_monthly_volumes",
        "project_keyword_scores",
        "data_exports"
    ];

    private static readonly string[] RequiredPhase2Tables =
    [
        "competitor_sites",
        "influx_keyword_results",
        "influx_page_results",
        "competitive_results",
        "content_search_results",
        "serp_headline_pages",
        "serp_headlines",
        "co_occurrence_words",
        "co_occurrence_page_details",
        "topic_clusters",
        "cluster_keywords",
        "article_briefs",
        "artifact_versions",
        "rank_check_jobs",
        "rank_check_targets",
        "rank_results",
        "alerts",
        "alert_events"
    ];

    private static readonly string[] RequiredPhase3Tables =
    [
        "rewrite_tasks",
        "cannibalization_candidates",
        "reports",
        "data_imports",
        "external_connector_settings",
        "external_connector_runs",
        "ai_sessions",
        "ai_messages"
    ];

    [Fact]
    [Trait("Category", "Integration")]
    public void ModelContainsMvpTablesAndSeedData()
    {
        using var context = CreateContext();
        var model = context.Model;

        foreach (var tableName in RequiredMvpTables)
        {
            Assert.Contains(model.GetEntityTypes(), entityType => entityType.GetTableName() == tableName);
        }

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var workspaceSeed = designTimeModel.FindEntityType(typeof(WorkspaceEntity))!.GetSeedData();
        Assert.Contains(workspaceSeed, row =>
            Equals(row["Id"], SeoIntelligenceSeedData.DefaultWorkspaceId) &&
            Equals(row["Name"], SeoIntelligenceSeedData.DefaultWorkspaceName) &&
            Equals(row["Status"], "active"));

        var contractScopeSeed = designTimeModel.FindEntityType(typeof(ApiContractScopeEntity))!.GetSeedData();
        Assert.Contains(contractScopeSeed, row =>
            Equals(row["Id"], SeoIntelligenceSeedData.DefaultRakkoContractScopeId) &&
            Equals(row["ScopeKey"], SeoIntelligenceSeedData.RakkoKeywordScopeKey) &&
            Equals(row["Provider"], SeoIntelligenceSeedData.RakkoKeywordProvider) &&
            Equals(row["ApiKeyLimit"], 5) &&
            Equals(row["Status"], "active"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MigrationSqlContainsPostgresExtensionCriticalIndexesAndSeedInserts()
    {
        using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();

        var sql = migrator.GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("pg_trgm", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ux_keywords_language_text_hash", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gin_trgm_ops", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ux_jobs_workspace_project_type_idempotency_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COALESCE(project_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_jobs_status_next_run_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_external_api_calls_contract_scope_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO workspaces", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO api_contract_scopes", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ModelAndMigrationSqlContainPhase2TablesAndIndexes()
    {
        using var context = CreateContext();
        var model = context.Model;

        foreach (var tableName in RequiredPhase2Tables)
        {
            Assert.Contains(model.GetEntityTypes(), entityType => entityType.GetTableName() == tableName);
        }

        var clusterKeyword = model.FindEntityType(typeof(ClusterKeywordEntity));
        Assert.NotNull(clusterKeyword);
        Assert.Equal(
            ["ClusterId", "KeywordId"],
            clusterKeyword!.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());

        var migrator = context.GetService<IMigrator>();
        var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE competitor_sites", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE rank_results", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_rank_results_project_id_keyword_id_target_checked_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_rank_results_contract_scope_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_alert_events_project_id_triggered_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ux_artifact_versions_artifact_type_artifact_id_version_no", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_influx_keyword_results_project_id_target", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_content_search_results_title_description_fts", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to_tsvector('simple', title || ' ' || description)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ModelAndMigrationSqlContainPhase3TablesIndexesAndShareTokenControls()
    {
        using var context = CreateContext();
        var model = context.Model;

        foreach (var tableName in RequiredPhase3Tables)
        {
            Assert.Contains(model.GetEntityTypes(), entityType => entityType.GetTableName() == tableName);
        }

        var report = model.FindEntityType(typeof(ReportEntity));
        Assert.NotNull(report);
        Assert.Contains(report!.GetProperties(), property => property.GetColumnName() == "share_token_hash");
        Assert.Contains(report.GetProperties(), property => property.GetColumnName() == "share_revoked_at");

        var migrator = context.GetService<IMigrator>();
        var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE rewrite_tasks", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE cannibalization_candidates", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE reports", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE data_imports", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE external_connector_settings", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE external_connector_runs", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE ai_sessions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE ai_messages", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("share_revoked_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ux_reports_share_token_hash", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("share_token_hash IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_cannibalization_candidates_project_id_status_severity_score", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_external_connector_settings_workspace_id_project_id_connector_type_status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ix_ai_messages_session_id_created_at", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static SeoIntelligenceDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SeoIntelligenceDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=seo;Username=seo;Password=seo_dev_password");
        return new SeoIntelligenceDbContext(optionsBuilder.Options);
    }
}
