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

    private static SeoIntelligenceDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SeoIntelligenceDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=seo;Username=seo;Password=seo_dev_password");
        return new SeoIntelligenceDbContext(optionsBuilder.Options);
    }
}
