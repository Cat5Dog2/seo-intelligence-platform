using Microsoft.EntityFrameworkCore;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace SeoIntelligence.Infrastructure.Persistence;

public sealed class SeoIntelligenceDbContext(DbContextOptions<SeoIntelligenceDbContext> options)
    : DbContext(options)
{
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<SiteEntity> Sites => Set<SiteEntity>();
    public DbSet<ApiCredentialEntity> ApiCredentials => Set<ApiCredentialEntity>();
    public DbSet<ApiContractScopeEntity> ApiContractScopes => Set<ApiContractScopeEntity>();
    public DbSet<NotificationChannelEntity> NotificationChannels => Set<NotificationChannelEntity>();
    public DbSet<NotificationDeliveryEntity> NotificationDeliveries => Set<NotificationDeliveryEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<LocationEntity> Locations => Set<LocationEntity>();
    public DbSet<LanguageEntity> Languages => Set<LanguageEntity>();
    public DbSet<ExternalApiCallEntity> ExternalApiCalls => Set<ExternalApiCallEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<JobExternalRequestEntity> JobExternalRequests => Set<JobExternalRequestEntity>();
    public DbSet<KeywordSeedEntity> KeywordSeeds => Set<KeywordSeedEntity>();
    public DbSet<KeywordEntity> Keywords => Set<KeywordEntity>();
    public DbSet<KeywordSuggestionEntity> KeywordSuggestions => Set<KeywordSuggestionEntity>();
    public DbSet<RelatedKeywordEntity> RelatedKeywords => Set<RelatedKeywordEntity>();
    public DbSet<QuestionEntity> Questions => Set<QuestionEntity>();
    public DbSet<LsiPaaItemEntity> LsiPaaItems => Set<LsiPaaItemEntity>();
    public DbSet<RankingKeywordEntity> RankingKeywords => Set<RankingKeywordEntity>();
    public DbSet<SearchVolumeJobEntity> SearchVolumeJobs => Set<SearchVolumeJobEntity>();
    public DbSet<SearchVolumeResultEntity> SearchVolumeResults => Set<SearchVolumeResultEntity>();
    public DbSet<KeywordMetricEntity> KeywordMetrics => Set<KeywordMetricEntity>();
    public DbSet<KeywordMonthlyVolumeEntity> KeywordMonthlyVolumes => Set<KeywordMonthlyVolumeEntity>();
    public DbSet<ProjectKeywordScoreEntity> ProjectKeywordScores => Set<ProjectKeywordScoreEntity>();
    public DbSet<DataExportEntity> DataExports => Set<DataExportEntity>();
    public DbSet<CompetitorSiteEntity> CompetitorSites => Set<CompetitorSiteEntity>();
    public DbSet<InfluxKeywordResultEntity> InfluxKeywordResults => Set<InfluxKeywordResultEntity>();
    public DbSet<InfluxPageResultEntity> InfluxPageResults => Set<InfluxPageResultEntity>();
    public DbSet<CompetitiveResultEntity> CompetitiveResults => Set<CompetitiveResultEntity>();
    public DbSet<ContentSearchResultEntity> ContentSearchResults => Set<ContentSearchResultEntity>();
    public DbSet<SerpHeadlinePageEntity> SerpHeadlinePages => Set<SerpHeadlinePageEntity>();
    public DbSet<SerpHeadlineEntity> SerpHeadlines => Set<SerpHeadlineEntity>();
    public DbSet<CoOccurrenceWordEntity> CoOccurrenceWords => Set<CoOccurrenceWordEntity>();
    public DbSet<CoOccurrencePageDetailEntity> CoOccurrencePageDetails => Set<CoOccurrencePageDetailEntity>();
    public DbSet<TopicClusterEntity> TopicClusters => Set<TopicClusterEntity>();
    public DbSet<ClusterKeywordEntity> ClusterKeywords => Set<ClusterKeywordEntity>();
    public DbSet<ArticleBriefEntity> ArticleBriefs => Set<ArticleBriefEntity>();
    public DbSet<ArtifactVersionEntity> ArtifactVersions => Set<ArtifactVersionEntity>();
    public DbSet<RankCheckJobEntity> RankCheckJobs => Set<RankCheckJobEntity>();
    public DbSet<RankCheckTargetEntity> RankCheckTargets => Set<RankCheckTargetEntity>();
    public DbSet<RankResultEntity> RankResults => Set<RankResultEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<AlertEventEntity> AlertEvents => Set<AlertEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        ConfigureWorkspaces(modelBuilder);
        ConfigureProjects(modelBuilder);
        ConfigureSites(modelBuilder);
        ConfigureApiCredentials(modelBuilder);
        ConfigureApiContractScopes(modelBuilder);
        ConfigureNotificationChannels(modelBuilder);
        ConfigureNotificationDeliveries(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureLocations(modelBuilder);
        ConfigureLanguages(modelBuilder);
        ConfigureExternalApiCalls(modelBuilder);
        ConfigureJobs(modelBuilder);
        ConfigureJobExternalRequests(modelBuilder);
        ConfigureKeywordSeeds(modelBuilder);
        ConfigureKeywords(modelBuilder);
        ConfigureKeywordSuggestions(modelBuilder);
        ConfigureRelatedKeywords(modelBuilder);
        ConfigureQuestions(modelBuilder);
        ConfigureLsiPaaItems(modelBuilder);
        ConfigureRankingKeywords(modelBuilder);
        ConfigureSearchVolumeJobs(modelBuilder);
        ConfigureSearchVolumeResults(modelBuilder);
        ConfigureKeywordMetrics(modelBuilder);
        ConfigureKeywordMonthlyVolumes(modelBuilder);
        ConfigureProjectKeywordScores(modelBuilder);
        ConfigureDataExports(modelBuilder);
        ConfigureCompetitorSites(modelBuilder);
        ConfigureInfluxKeywordResults(modelBuilder);
        ConfigureInfluxPageResults(modelBuilder);
        ConfigureCompetitiveResults(modelBuilder);
        ConfigureContentSearchResults(modelBuilder);
        ConfigureSerpHeadlinePages(modelBuilder);
        ConfigureSerpHeadlines(modelBuilder);
        ConfigureCoOccurrenceWords(modelBuilder);
        ConfigureCoOccurrencePageDetails(modelBuilder);
        ConfigureTopicClusters(modelBuilder);
        ConfigureClusterKeywords(modelBuilder);
        ConfigureArticleBriefs(modelBuilder);
        ConfigureArtifactVersions(modelBuilder);
        ConfigureRankCheckJobs(modelBuilder);
        ConfigureRankCheckTargets(modelBuilder);
        ConfigureRankResults(modelBuilder);
        ConfigureAlerts(modelBuilder);
        ConfigureAlertEvents(modelBuilder);
    }

    private static void ConfigureWorkspaces(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspaceEntity>(entity =>
        {
            entity.ToTable("workspaces", table =>
                table.HasCheckConstraint("ck_workspaces_status", "status IN ('active')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.DefaultLocation).HasColumnName("default_location").IsRequired();
            entity.Property(e => e.DefaultLanguage).HasColumnName("default_language").IsRequired();
            entity.Property(e => e.RetentionSettingsJson).HasColumnName("retention_settings_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.NotificationDefaultsJson).HasColumnName("notification_defaults_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_workspaces_name");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_workspaces_status");

            entity.HasData(new WorkspaceEntity
            {
                Id = SeoIntelligenceSeedData.DefaultWorkspaceId,
                Name = SeoIntelligenceSeedData.DefaultWorkspaceName,
                DefaultLocation = SeoIntelligenceSeedData.DefaultLocation,
                DefaultLanguage = SeoIntelligenceSeedData.DefaultLanguage,
                RetentionSettingsJson = "{\"externalApiRawDataMonths\":24,\"processedDataMonths\":24,\"auditLogMonths\":36}",
                NotificationDefaultsJson = "{\"discordEnabled\":false}",
                Status = "active",
                CreatedAt = SeoIntelligenceSeedData.SeedCreatedAt,
                UpdatedAt = SeoIntelligenceSeedData.SeedCreatedAt
            });
        });
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.ToTable("projects", table =>
                table.HasCheckConstraint("ck_projects_status", "status IN ('active', 'archived')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.DefaultLocation).HasColumnName("default_location").IsRequired();
            entity.Property(e => e.DefaultLanguage).HasColumnName("default_language").IsRequired();
            entity.Property(e => e.KpiJson).HasColumnName("kpi_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Memo).HasColumnName("memo");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.Status }).HasDatabaseName("ix_projects_workspace_id_status");
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique().HasDatabaseName("ux_projects_workspace_id_name");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteEntity>(entity =>
        {
            entity.ToTable("sites", table =>
                table.HasCheckConstraint("ck_sites_status", "status IN ('active', 'archived')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Domain).HasColumnName("domain").IsRequired();
            entity.Property(e => e.CanonicalUrl).HasColumnName("canonical_url").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Memo).HasColumnName("memo");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Status }).HasDatabaseName("ix_sites_project_id_status");
            entity.HasIndex(e => e.Domain).HasDatabaseName("ix_sites_domain");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApiCredentials(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiCredentialEntity>(entity =>
        {
            entity.ToTable("api_credentials", table =>
                table.HasCheckConstraint("ck_api_credentials_status", "status IN ('active', 'disabled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.Provider).HasColumnName("provider").IsRequired();
            entity.Property(e => e.KeyRef).HasColumnName("key_ref").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.DisabledAt).HasColumnName("disabled_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.Provider, e.Status }).HasDatabaseName("ix_api_credentials_workspace_id_provider_status");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApiContractScopes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiContractScopeEntity>(entity =>
        {
            entity.ToTable("api_contract_scopes", table =>
                table.HasCheckConstraint("ck_api_contract_scopes_status", "status IN ('active', 'archived')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.Provider).HasColumnName("provider").IsRequired();
            entity.Property(e => e.PlanName).HasColumnName("plan_name").IsRequired();
            entity.Property(e => e.ApiKeyLimit).HasColumnName("api_key_limit");
            entity.Property(e => e.DataUsageScope).HasColumnName("data_usage_scope").IsRequired();
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by").IsRequired();
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from").HasColumnType("date");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to").HasColumnType("date");
            entity.Property(e => e.ScopeKey).HasColumnName("scope_key").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.ScopeKey).IsUnique().HasDatabaseName("ux_api_contract_scopes_scope_key");
            entity.HasIndex(e => new { e.WorkspaceId, e.Provider, e.Status }).HasDatabaseName("ix_api_contract_scopes_workspace_id_provider_status");
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo }).HasDatabaseName("ix_api_contract_scopes_effective_from_effective_to");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);

            entity.HasData(new ApiContractScopeEntity
            {
                Id = SeoIntelligenceSeedData.DefaultRakkoContractScopeId,
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                PlanName = SeoIntelligenceSeedData.RakkoKeywordPlanName,
                ApiKeyLimit = 5,
                DataUsageScope = SeoIntelligenceSeedData.RakkoKeywordDataUsageScope,
                ConfirmedAt = SeoIntelligenceSeedData.SeedCreatedAt,
                ConfirmedBy = "developer",
                EffectiveFrom = SeoIntelligenceSeedData.ContractEffectiveFrom,
                EffectiveTo = null,
                ScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                Status = "active",
                CreatedAt = SeoIntelligenceSeedData.SeedCreatedAt
            });
        });
    }

    private static void ConfigureNotificationChannels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationChannelEntity>(entity =>
        {
            entity.ToTable("notification_channels", table =>
                table.HasCheckConstraint("ck_notification_channels_status", "status IN ('active', 'disabled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ChannelType).HasColumnName("channel_type").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.WebhookSecretRef).HasColumnName("webhook_secret_ref").IsRequired();
            entity.Property(e => e.EventTypesJson).HasColumnName("event_types_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.DisabledAt).HasColumnName("disabled_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.Status }).HasDatabaseName("ix_notification_channels_workspace_id_project_id_status");
            entity.HasIndex(e => e.ChannelType).HasDatabaseName("ix_notification_channels_channel_type");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureNotificationDeliveries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationDeliveryEntity>(entity =>
        {
            entity.ToTable("notification_deliveries", table =>
                table.HasCheckConstraint("ck_notification_deliveries_status", "status IN ('pending', 'retrying', 'succeeded', 'failed')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ResourceType).HasColumnName("resource_type");
            entity.Property(e => e.ResourceId).HasColumnName("resource_id");
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.PayloadHash).HasColumnName("payload_hash").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.NextRetryAt).HasColumnName("next_retry_at").HasColumnType("timestamptz");
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasColumnType("timestamptz");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at").HasColumnType("timestamptz");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.CreatedAt }).HasDatabaseName("ix_notification_deliveries_workspace_id_project_id_created_at");
            entity.HasIndex(e => new { e.Status, e.NextRetryAt }).HasDatabaseName("ix_notification_deliveries_status_next_retry_at");
            entity.HasIndex(e => e.JobId).HasDatabaseName("ix_notification_deliveries_job_id");
            entity.HasIndex(e => new { e.ResourceType, e.ResourceId }).HasDatabaseName("ix_notification_deliveries_resource_type_resource_id");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_notification_deliveries_correlation_id");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NotificationChannelEntity>().WithMany().HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.Actor).HasColumnName("actor").IsRequired();
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").IsRequired();
            entity.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
            entity.Property(e => e.BeforeAfterJson).HasColumnName("before_after_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.CreatedAt }).HasDatabaseName("ix_audit_logs_workspace_id_created_at");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_audit_logs_correlation_id");
            entity.HasIndex(e => e.Actor).HasDatabaseName("ix_audit_logs_actor");
            entity.HasIndex(e => new { e.ResourceType, e.ResourceId }).HasDatabaseName("ix_audit_logs_resource_type_resource_id");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLocations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocationEntity>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Provider).HasColumnName("provider").IsRequired();
            entity.Property(e => e.LocationCode).HasColumnName("location_code").IsRequired();
            entity.Property(e => e.LocationName).HasColumnName("location_name").IsRequired();
            entity.Property(e => e.CountryCode).HasColumnName("country_code").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.Provider, e.LocationCode }).IsUnique().HasDatabaseName("ux_locations_provider_location_code");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_locations_status");
        });
    }

    private static void ConfigureLanguages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LanguageEntity>(entity =>
        {
            entity.ToTable("languages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Provider).HasColumnName("provider").IsRequired();
            entity.Property(e => e.LanguageCode).HasColumnName("language_code").IsRequired();
            entity.Property(e => e.LanguageName).HasColumnName("language_name").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.Provider, e.LanguageCode }).IsUnique().HasDatabaseName("ux_languages_provider_language_code");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_languages_status");
        });
    }

    private static void ConfigureExternalApiCalls(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExternalApiCallEntity>(entity =>
        {
            entity.ToTable("external_api_calls");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ApiCredentialId).HasColumnName("api_credential_id");
            entity.Property(e => e.ApiContractScopeId).HasColumnName("api_contract_scope_id");
            entity.Property(e => e.Provider).HasColumnName("provider").IsRequired();
            entity.Property(e => e.Endpoint).HasColumnName("endpoint").IsRequired();
            entity.Property(e => e.RequestHash).HasColumnName("request_hash").IsRequired();
            entity.Property(e => e.RequestUri).HasColumnName("request_uri").IsRequired();
            entity.Property(e => e.ResponseHash).HasColumnName("response_hash");
            entity.Property(e => e.ResponseUri).HasColumnName("response_uri");
            entity.Property(e => e.ContractScopeKey).HasColumnName("contract_scope_key").IsRequired();
            entity.Property(e => e.CacheHit).HasColumnName("cache_hit");
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.ConsumedCredit).HasColumnName("consumed_credit").HasPrecision(18, 4);
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.ErrorCode).HasColumnName("error_code");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.Actor).HasColumnName("actor").IsRequired();
            entity.Property(e => e.RetainedUntil).HasColumnName("retained_until").HasColumnType("timestamptz");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.Provider, e.Endpoint, e.CreatedAt }).HasDatabaseName("ix_external_api_calls_provider_endpoint_created_at");
            entity.HasIndex(e => e.StatusCode).HasDatabaseName("ix_external_api_calls_status_code");
            entity.HasIndex(e => e.ContractScopeKey).HasDatabaseName("ix_external_api_calls_contract_scope_key");
            entity.HasIndex(e => e.ResponseHash).HasDatabaseName("ix_external_api_calls_response_hash");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_external_api_calls_correlation_id");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiCredentialEntity>().WithMany().HasForeignKey(e => e.ApiCredentialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiContractScopeEntity>().WithMany().HasForeignKey(e => e.ApiContractScopeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureJobs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.ToTable("jobs", table =>
            {
                table.HasCheckConstraint("ck_jobs_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')");
                table.HasCheckConstraint("ck_jobs_progress", "progress >= 0 AND progress <= 100");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.JobType).HasColumnName("job_type").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.Progress).HasColumnName("progress").HasDefaultValue(0);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.NextRunAt).HasColumnName("next_run_at").HasColumnType("timestamptz");
            entity.Property(e => e.ResultResourceType).HasColumnName("result_resource_type");
            entity.Property(e => e.ResultResourceId).HasColumnName("result_resource_id");
            entity.Property(e => e.ErrorJson).HasColumnName("error_json").HasColumnType("jsonb");
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key");
            entity.Property(e => e.RequestHash).HasColumnName("request_hash");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.Status, e.NextRunAt }).HasDatabaseName("ix_jobs_status_next_run_at");
            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.CreatedAt }).HasDatabaseName("ix_jobs_workspace_id_project_id_created_at");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureJobExternalRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobExternalRequestEntity>(entity =>
        {
            entity.ToTable("job_external_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.Endpoint).HasColumnName("endpoint").IsRequired();
            entity.Property(e => e.ExternalRequestId).HasColumnName("external_request_id").IsRequired();
            entity.Property(e => e.SequenceNo).HasColumnName("sequence_no");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.ConsumedCredit).HasColumnName("consumed_credit").HasPrecision(18, 4);
            entity.Property(e => e.ErrorJson).HasColumnName("error_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.JobId, e.SequenceNo }).HasDatabaseName("ix_job_external_requests_job_id_sequence_no");
            entity.HasIndex(e => e.ExternalRequestId).HasDatabaseName("ix_job_external_requests_external_request_id");
            entity.HasIndex(e => new { e.Status, e.UpdatedAt }).HasDatabaseName("ix_job_external_requests_status_updated_at");
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureKeywordSeeds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordSeedEntity>(entity =>
        {
            entity.ToTable("keyword_seeds");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Seed).HasColumnName("seed").IsRequired();
            entity.Property(e => e.Source).HasColumnName("source").IsRequired();
            entity.Property(e => e.Memo).HasColumnName("memo");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.ProjectId).HasDatabaseName("ix_keyword_seeds_project_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureKeywords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordEntity>(entity =>
        {
            entity.ToTable("keywords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.NormalizedText).HasColumnName("normalized_text").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").IsRequired();
            entity.Property(e => e.TextHash).HasColumnName("text_hash").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.Language, e.TextHash }).IsUnique().HasDatabaseName("ux_keywords_language_text_hash");
            entity.HasIndex(e => e.NormalizedText)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops")
                .HasDatabaseName("ix_keywords_normalized_text_trgm");
        });
    }

    private static void ConfigureKeywordSuggestions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordSuggestionEntity>(entity =>
        {
            entity.ToTable("keyword_suggestions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SeedId).HasColumnName("seed_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Engine).HasColumnName("engine").IsRequired();
            entity.Property(e => e.SuggestClass).HasColumnName("suggest_class").IsRequired();
            entity.Property(e => e.EngineCount).HasColumnName("engine_count");
            entity.Property(e => e.FirstSeenRange).HasColumnName("first_seen_range");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.SeedId).HasDatabaseName("ix_keyword_suggestions_seed_id");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_keyword_suggestions_keyword_id");
            entity.HasOne<KeywordSeedEntity>().WithMany().HasForeignKey(e => e.SeedId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRelatedKeywords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RelatedKeywordEntity>(entity =>
        {
            entity.ToTable("related_keywords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SeedId).HasColumnName("seed_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.MatchType).HasColumnName("match_type").IsRequired();
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.SeedId).HasDatabaseName("ix_related_keywords_seed_id");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_related_keywords_keyword_id");
            entity.HasOne<KeywordSeedEntity>().WithMany().HasForeignKey(e => e.SeedId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureQuestions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionEntity>(entity =>
        {
            entity.ToTable("questions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SeedKeywordId).HasColumnName("seed_keyword_id");
            entity.Property(e => e.QuestionText).HasColumnName("question_text").IsRequired();
            entity.Property(e => e.Source).HasColumnName("source").IsRequired();
            entity.Property(e => e.Importance).HasColumnName("importance").HasPrecision(8, 4);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.ProjectId).HasDatabaseName("ix_questions_project_id");
            entity.HasIndex(e => e.SeedKeywordId).HasDatabaseName("ix_questions_seed_keyword_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.SeedKeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLsiPaaItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LsiPaaItemEntity>(entity =>
        {
            entity.ToTable("lsi_paa_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SeedKeywordId).HasColumnName("seed_keyword_id");
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.Importance).HasColumnName("importance").HasPrecision(8, 4);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.SeedKeywordId).HasDatabaseName("ix_lsi_paa_items_seed_keyword_id");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_lsi_paa_items_keyword_id");
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.SeedKeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRankingKeywords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RankingKeywordEntity>(entity =>
        {
            entity.ToTable("ranking_keywords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SeedKeywordId).HasColumnName("seed_keyword_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.WordCount).HasColumnName("word_count");
            entity.Property(e => e.Relevance).HasColumnName("relevance").HasPrecision(8, 4);
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.SeedKeywordId).HasDatabaseName("ix_ranking_keywords_seed_keyword_id");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_ranking_keywords_keyword_id");
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.SeedKeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSearchVolumeJobs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchVolumeJobEntity>(entity =>
        {
            entity.ToTable("search_volume_jobs");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasColumnName("job_id").ValueGeneratedNever();
            entity.Property(e => e.Location).HasColumnName("location").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").IsRequired();
            entity.Property(e => e.AggregationMonths).HasColumnName("aggregation_months");
            entity.Property(e => e.RequestOptionsJson).HasColumnName("request_options_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.StatusJson).HasColumnName("status_json").HasColumnType("jsonb").IsRequired();

            entity.HasOne<JobEntity>().WithOne().HasForeignKey<SearchVolumeJobEntity>(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSearchVolumeResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchVolumeResultEntity>(entity =>
        {
            entity.ToTable("search_volume_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.DataSource).HasColumnName("data_source").IsRequired();
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.CacheHit).HasColumnName("cache_hit");
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.TrendsJson).HasColumnName("trends_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => e.JobId).HasDatabaseName("ix_search_volume_results_job_id");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_search_volume_results_keyword_id");
            entity.HasIndex(e => e.CacheHit).HasDatabaseName("ix_search_volume_results_cache_hit");
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureKeywordMetrics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordMetricEntity>(entity =>
        {
            entity.ToTable("keyword_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Location).HasColumnName("location").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").IsRequired();
            entity.Property(e => e.ContractScopeKey).HasColumnName("contract_scope_key").IsRequired();
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.SearchVolume).HasColumnName("search_volume");
            entity.Property(e => e.SeoDifficulty).HasColumnName("seo_difficulty").HasPrecision(8, 4);
            entity.Property(e => e.Cpc).HasColumnName("cpc").HasPrecision(18, 4);
            entity.Property(e => e.Competition).HasColumnName("competition").HasPrecision(8, 4);
            entity.Property(e => e.FirstSeenRange).HasColumnName("first_seen_range");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.KeywordId, e.Location, e.Language, e.ContractScopeKey, e.FetchedAt })
                .IsDescending(false, false, false, false, true)
                .HasDatabaseName("ix_keyword_metrics_keyword_id_location_language_contract_scope_key_fetched_at");
            entity.HasIndex(e => e.SourceCallId).HasDatabaseName("ix_keyword_metrics_source_call_id");
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureKeywordMonthlyVolumes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordMonthlyVolumeEntity>(entity =>
        {
            entity.ToTable("keyword_monthly_volumes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Location).HasColumnName("location").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").IsRequired();
            entity.Property(e => e.ContractScopeKey).HasColumnName("contract_scope_key").IsRequired();
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.YearMonth).HasColumnName("year_month").HasColumnType("char(7)").HasMaxLength(7).IsRequired();
            entity.Property(e => e.SearchVolume).HasColumnName("search_volume");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.KeywordId, e.Location, e.Language, e.ContractScopeKey, e.YearMonth, e.FetchedAt })
                .IsDescending(false, false, false, false, false, true)
                .HasDatabaseName("ix_keyword_monthly_volumes_keyword_location_language_scope_month_fetched_at");
            entity.HasIndex(e => e.SourceCallId).HasDatabaseName("ix_keyword_monthly_volumes_source_call_id");
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectKeywordScores(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectKeywordScoreEntity>(entity =>
        {
            entity.ToTable("project_keyword_scores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Location).HasColumnName("location").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").IsRequired();
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.OpportunityScore).HasColumnName("opportunity_score").HasPrecision(8, 4);
            entity.Property(e => e.ScoreComponentsJson).HasColumnName("score_components_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.ScoredAt).HasColumnName("scored_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.KeywordId, e.Location, e.Language })
                .IsUnique()
                .HasDatabaseName("ux_project_keyword_scores_project_id_keyword_id_location_language");
            entity.HasIndex(e => new { e.ProjectId, e.OpportunityScore })
                .IsDescending(false, true)
                .HasDatabaseName("ix_project_keyword_scores_project_id_opportunity_score");
            entity.HasIndex(e => e.SourceCallId).HasDatabaseName("ix_project_keyword_scores_source_call_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDataExports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataExportEntity>(entity =>
        {
            entity.ToTable("data_exports", table =>
                table.HasCheckConstraint("ck_data_exports_status", "status IN ('queued', 'running', 'waiting_external', 'succeeded', 'failed_retryable', 'failed_fatal', 'canceled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ExportType).HasColumnName("export_type").IsRequired();
            entity.Property(e => e.Format).HasColumnName("format").IsRequired();
            entity.Property(e => e.FilterJson).HasColumnName("filter_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.FileUri).HasColumnName("file_uri");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.CreatedAt }).HasDatabaseName("ix_data_exports_workspace_id_project_id_created_at");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_data_exports_status_created_at");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompetitorSites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompetitorSiteEntity>(entity =>
        {
            entity.ToTable("competitor_sites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Domain).HasColumnName("domain").IsRequired();
            entity.Property(e => e.Source).HasColumnName("source").IsRequired();
            entity.Property(e => e.DuplicateRate).HasColumnName("duplicate_rate").HasPrecision(8, 4);
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Domain }).HasDatabaseName("ix_competitor_sites_project_id_domain");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInfluxKeywordResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfluxKeywordResultEntity>(entity =>
        {
            entity.ToTable("influx_keyword_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.RankedUrl).HasColumnName("ranked_url").IsRequired();
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Target }).HasDatabaseName("ix_influx_keyword_results_project_id_target");
            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_influx_keyword_results_keyword_id");
            entity.HasIndex(e => e.Rank).HasDatabaseName("ix_influx_keyword_results_rank");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInfluxPageResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfluxPageResultEntity>(entity =>
        {
            entity.ToTable("influx_page_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.PageUrl).HasColumnName("page_url").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.KeywordCount).HasColumnName("keyword_count");
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.TrafficValue).HasColumnName("traffic_value").HasPrecision(18, 4);
            entity.Property(e => e.TopKeywordId).HasColumnName("top_keyword_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Target }).HasDatabaseName("ix_influx_page_results_project_id_target");
            entity.HasIndex(e => e.TopKeywordId).HasDatabaseName("ix_influx_page_results_top_keyword_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.TopKeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompetitiveResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompetitiveResultEntity>(entity =>
        {
            entity.ToTable("competitive_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SiteDomain).HasColumnName("site_domain").IsRequired();
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.TrafficValue).HasColumnName("traffic_value").HasPrecision(18, 4);
            entity.Property(e => e.KeywordCount).HasColumnName("keyword_count");
            entity.Property(e => e.DuplicateRate).HasColumnName("duplicate_rate").HasPrecision(8, 4);
            entity.Property(e => e.UniqueCountsJson).HasColumnName("unique_counts_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.SiteDomain }).HasDatabaseName("ix_competitive_results_project_id_site_domain");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContentSearchResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentSearchResultEntity>(entity =>
        {
            entity.ToTable("content_search_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Url).HasColumnName("url").IsRequired();
            entity.Property(e => e.Domain).HasColumnName("domain").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").IsRequired();
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.TrafficValue).HasColumnName("traffic_value").HasPrecision(18, 4);
            entity.Property(e => e.TopKeywordId).HasColumnName("top_keyword_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.KeywordId }).HasDatabaseName("ix_content_search_results_project_id_keyword_id");
            entity.HasIndex(e => e.Domain).HasDatabaseName("ix_content_search_results_domain");
            entity.HasIndex(e => e.TopKeywordId).HasDatabaseName("ix_content_search_results_top_keyword_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.TopKeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSerpHeadlinePages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SerpHeadlinePageEntity>(entity =>
        {
            entity.ToTable("serp_headline_pages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.Url).HasColumnName("url").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").IsRequired();
            entity.Property(e => e.HeadlineCount).HasColumnName("headline_count");
            entity.Property(e => e.WordCount).HasColumnName("word_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.KeywordId }).HasDatabaseName("ix_serp_headline_pages_project_id_keyword_id");
            entity.HasIndex(e => e.Rank).HasDatabaseName("ix_serp_headline_pages_rank");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSerpHeadlines(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SerpHeadlineEntity>(entity =>
        {
            entity.ToTable("serp_headlines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.Level).HasColumnName("level").HasColumnType("smallint");
            entity.Property(e => e.Text).HasColumnName("text").IsRequired();
            entity.Property(e => e.OrderNo).HasColumnName("order_no");

            entity.HasIndex(e => new { e.PageId, e.OrderNo }).HasDatabaseName("ix_serp_headlines_page_id_order_no");
            entity.HasOne<SerpHeadlinePageEntity>().WithMany().HasForeignKey(e => e.PageId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCoOccurrenceWords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoOccurrenceWordEntity>(entity =>
        {
            entity.ToTable("co_occurrence_words");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Word).HasColumnName("word").IsRequired();
            entity.Property(e => e.OccurrenceCountsJson).HasColumnName("occurrence_counts_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.SiteCountsJson).HasColumnName("site_counts_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.KeywordId }).HasDatabaseName("ix_co_occurrence_words_project_id_keyword_id");
            entity.HasIndex(e => e.Word).HasDatabaseName("ix_co_occurrence_words_word");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCoOccurrencePageDetails(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoOccurrencePageDetailEntity>(entity =>
        {
            entity.ToTable("co_occurrence_page_details");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.CoWordId).HasColumnName("co_word_id");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.Url).HasColumnName("url").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Count).HasColumnName("count");
            entity.Property(e => e.CountInHeadline).HasColumnName("count_in_headline");
            entity.Property(e => e.CountInTitle).HasColumnName("count_in_title");

            entity.HasIndex(e => new { e.CoWordId, e.Rank }).HasDatabaseName("ix_co_occurrence_page_details_co_word_id_rank");
            entity.HasOne<CoOccurrenceWordEntity>().WithMany().HasForeignKey(e => e.CoWordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTopicClusters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TopicClusterEntity>(entity =>
        {
            entity.ToTable("topic_clusters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.RepresentativeKeywordId).HasColumnName("representative_keyword_id");
            entity.Property(e => e.Score).HasColumnName("score").HasPrecision(8, 4);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.ParentId }).HasDatabaseName("ix_topic_clusters_project_id_parent_id");
            entity.HasIndex(e => e.RepresentativeKeywordId).HasDatabaseName("ix_topic_clusters_representative_keyword_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TopicClusterEntity>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.RepresentativeKeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureClusterKeywords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClusterKeywordEntity>(entity =>
        {
            entity.ToTable("cluster_keywords");
            entity.HasKey(e => new { e.ClusterId, e.KeywordId });
            entity.Property(e => e.ClusterId).HasColumnName("cluster_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Role).HasColumnName("role").IsRequired();
            entity.Property(e => e.OpportunityScore).HasColumnName("opportunity_score").HasPrecision(8, 4);
            entity.Property(e => e.IntentLabel).HasColumnName("intent_label");

            entity.HasIndex(e => e.KeywordId).HasDatabaseName("ix_cluster_keywords_keyword_id");
            entity.HasOne<TopicClusterEntity>().WithMany().HasForeignKey(e => e.ClusterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureArticleBriefs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArticleBriefEntity>(entity =>
        {
            entity.ToTable("article_briefs", table =>
                table.HasCheckConstraint("ck_article_briefs_status", "status IN ('draft', 'active', 'archived', 'completed')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ClusterId).HasColumnName("cluster_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.TargetKeywordId).HasColumnName("target_keyword_id");
            entity.Property(e => e.CurrentVersion).HasColumnName("current_version");
            entity.Property(e => e.ContentJson).HasColumnName("content_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.ReviewStatus).HasColumnName("review_status").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Status }).HasDatabaseName("ix_article_briefs_project_id_status");
            entity.HasIndex(e => e.ClusterId).HasDatabaseName("ix_article_briefs_cluster_id");
            entity.HasIndex(e => e.TargetKeywordId).HasDatabaseName("ix_article_briefs_target_keyword_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TopicClusterEntity>().WithMany().HasForeignKey(e => e.ClusterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.TargetKeywordId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureArtifactVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArtifactVersionEntity>(entity =>
        {
            entity.ToTable("artifact_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ArtifactType).HasColumnName("artifact_type").IsRequired();
            entity.Property(e => e.ArtifactId).HasColumnName("artifact_id");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");
            entity.Property(e => e.ContentHash).HasColumnName("content_hash").IsRequired();
            entity.Property(e => e.ContentUri).HasColumnName("content_uri");
            entity.Property(e => e.ContentJson).HasColumnName("content_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(e => e.ReviewStatus).HasColumnName("review_status").IsRequired();
            entity.Property(e => e.ChangeSummary).HasColumnName("change_summary");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ArtifactType, e.ArtifactId, e.VersionNo }).IsUnique().HasDatabaseName("ux_artifact_versions_artifact_type_artifact_id_version_no");
            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.CreatedAt }).HasDatabaseName("ix_artifact_versions_workspace_id_project_id_created_at");
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRankCheckJobs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RankCheckJobEntity>(entity =>
        {
            entity.ToTable("rank_check_jobs");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasColumnName("job_id").ValueGeneratedNever();
            entity.Property(e => e.Depth).HasColumnName("depth");
            entity.Property(e => e.MatchType).HasColumnName("match_type").IsRequired();
            entity.Property(e => e.WithMetrics).HasColumnName("with_metrics");
            entity.Property(e => e.RequestOptionsJson).HasColumnName("request_options_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.StatusJson).HasColumnName("status_json").HasColumnType("jsonb").IsRequired();

            entity.HasOne<JobEntity>().WithOne().HasForeignKey<RankCheckJobEntity>(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRankCheckTargets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RankCheckTargetEntity>(entity =>
        {
            entity.ToTable("rank_check_targets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.TargetType).HasColumnName("target_type").IsRequired();

            entity.HasIndex(e => e.JobId).HasDatabaseName("ix_rank_check_targets_job_id");
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRankResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RankResultEntity>(entity =>
        {
            entity.ToTable("rank_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.RankedUrl).HasColumnName("ranked_url").IsRequired();
            entity.Property(e => e.EstimatedTraffic).HasColumnName("estimated_traffic").HasPrecision(18, 4);
            entity.Property(e => e.MetricsSnapshotJson).HasColumnName("metrics_snapshot_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.SourceCallId).HasColumnName("source_call_id");
            entity.Property(e => e.ContractScopeKey).HasColumnName("contract_scope_key").IsRequired();
            entity.Property(e => e.CheckedAt).HasColumnName("checked_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.KeywordId, e.Target, e.CheckedAt })
                .IsDescending(false, false, false, true)
                .HasDatabaseName("ix_rank_results_project_id_keyword_id_target_checked_at");
            entity.HasIndex(e => e.Position).HasDatabaseName("ix_rank_results_position");
            entity.HasIndex(e => e.SourceCallId).HasDatabaseName("ix_rank_results_source_call_id");
            entity.HasIndex(e => e.ContractScopeKey).HasDatabaseName("ix_rank_results_contract_scope_key");
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExternalApiCallEntity>().WithMany().HasForeignKey(e => e.SourceCallId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAlerts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlertEntity>(entity =>
        {
            entity.ToTable("alerts", table =>
                table.HasCheckConstraint("ck_alerts_status", "status IN ('active', 'disabled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.AlertType).HasColumnName("alert_type").IsRequired();
            entity.Property(e => e.ConditionJson).HasColumnName("condition_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.NotificationChannelId).HasColumnName("notification_channel_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.LastTriggeredAt).HasColumnName("last_triggered_at").HasColumnType("timestamptz");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.Status }).HasDatabaseName("ix_alerts_project_id_status");
            entity.HasIndex(e => e.NotificationChannelId).HasDatabaseName("ix_alerts_notification_channel_id");
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NotificationChannelEntity>().WithMany().HasForeignKey(e => e.NotificationChannelId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAlertEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlertEventEntity>(entity =>
        {
            entity.ToTable("alert_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.AlertId).HasColumnName("alert_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.KeywordId).HasColumnName("keyword_id");
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.PreviousValueJson).HasColumnName("previous_value_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CurrentValueJson).HasColumnName("current_value_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.NotificationDeliveryId).HasColumnName("notification_delivery_id");
            entity.Property(e => e.TriggeredAt).HasColumnName("triggered_at").HasColumnType("timestamptz");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");

            entity.HasIndex(e => new { e.ProjectId, e.TriggeredAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_alert_events_project_id_triggered_at");
            entity.HasIndex(e => new { e.AlertId, e.TriggeredAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_alert_events_alert_id_triggered_at");
            entity.HasIndex(e => e.NotificationDeliveryId).HasDatabaseName("ix_alert_events_notification_delivery_id");
            entity.HasOne<AlertEntity>().WithMany().HasForeignKey(e => e.AlertId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JobEntity>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<KeywordEntity>().WithMany().HasForeignKey(e => e.KeywordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NotificationDeliveryEntity>().WithMany().HasForeignKey(e => e.NotificationDeliveryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
