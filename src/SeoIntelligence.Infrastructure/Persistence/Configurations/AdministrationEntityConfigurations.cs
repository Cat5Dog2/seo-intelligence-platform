using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace SeoIntelligence.Infrastructure.Persistence;

internal static class AdministrationEntityConfigurations
{
    public static ModelBuilder ApplyAdministrationConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WorkspaceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SiteEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ApiCredentialEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ApiContractScopeEntityConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationChannelEntityConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationDeliveryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());

        return modelBuilder;
    }
}

internal sealed class WorkspaceEntityConfiguration : IEntityTypeConfiguration<WorkspaceEntity>
{
    public void Configure(EntityTypeBuilder<WorkspaceEntity> entity)
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
    }
}

internal sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> entity)
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
    }
}

internal sealed class SiteEntityConfiguration : IEntityTypeConfiguration<SiteEntity>
{
    public void Configure(EntityTypeBuilder<SiteEntity> entity)
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
    }
}

internal sealed class ApiCredentialEntityConfiguration : IEntityTypeConfiguration<ApiCredentialEntity>
{
    public void Configure(EntityTypeBuilder<ApiCredentialEntity> entity)
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
    }
}

internal sealed class ApiContractScopeEntityConfiguration : IEntityTypeConfiguration<ApiContractScopeEntity>
{
    public void Configure(EntityTypeBuilder<ApiContractScopeEntity> entity)
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
    }
}

internal sealed class NotificationChannelEntityConfiguration : IEntityTypeConfiguration<NotificationChannelEntity>
{
    public void Configure(EntityTypeBuilder<NotificationChannelEntity> entity)
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
    }
}

internal sealed class NotificationDeliveryEntityConfiguration : IEntityTypeConfiguration<NotificationDeliveryEntity>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryEntity> entity)
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
    }
}

internal sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> entity)
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
    }
}
