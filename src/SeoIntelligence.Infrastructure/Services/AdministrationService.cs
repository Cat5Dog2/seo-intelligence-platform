using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

public static class AdministrationServiceCollectionExtensions
{
    public static IServiceCollection AddSeoIntelligenceAdministration(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IProjectContextService, ProjectContextService>();
        services.TryAddScoped<IAuditLogWriter, AuditLogWriter>();
        services.TryAddScoped<IAdministrationService, AdministrationService>();
        services.TryAddScoped<IMasterDataService, MasterDataService>();
        services.TryAddScoped<KeywordDiscoveryService>();
        services.TryAddScoped<IKeywordDiscoveryService>(serviceProvider => serviceProvider.GetRequiredService<KeywordDiscoveryService>());
        services.TryAddScoped<SearchVolumeService>();
        services.TryAddScoped<ISearchVolumeService>(serviceProvider => serviceProvider.GetRequiredService<SearchVolumeService>());
        services.TryAddScoped<CompetitiveAnalysisService>();
        services.TryAddScoped<ICompetitiveAnalysisService>(serviceProvider => serviceProvider.GetRequiredService<CompetitiveAnalysisService>());
        services.TryAddScoped<ContentAnalysisService>();
        services.TryAddScoped<IContentAnalysisService>(serviceProvider => serviceProvider.GetRequiredService<ContentAnalysisService>());
        services.TryAddScoped<RankMonitoringService>();
        services.TryAddScoped<IRankMonitoringService>(serviceProvider => serviceProvider.GetRequiredService<RankMonitoringService>());
        services.TryAddScoped<TopicClusterService>();
        services.TryAddScoped<ITopicClusterService>(serviceProvider => serviceProvider.GetRequiredService<TopicClusterService>());
        services.TryAddScoped<RewriteManagementService>();
        services.TryAddScoped<IRewriteManagementService>(serviceProvider => serviceProvider.GetRequiredService<RewriteManagementService>());
        services.TryAddScoped<ReportService>();
        services.TryAddScoped<IReportService>(serviceProvider => serviceProvider.GetRequiredService<ReportService>());
        services.TryAddScoped<IScoringService, ScoringService>();
        services.TryAddScoped<DataTransferService>();
        services.TryAddScoped<IDataTransferService>(serviceProvider => serviceProvider.GetRequiredService<DataTransferService>());
        services.TryAddScoped<IDataImportService>(serviceProvider => serviceProvider.GetRequiredService<DataTransferService>());
        services.TryAddScoped<IDashboardService, DashboardService>();
        services.TryAddScoped<INotificationService, NotificationService>();
        services.TryAddScoped<NotificationDeliveryJob>();
        services.TryAddScoped<INotificationDeliveryScheduler, HangfireNotificationDeliveryScheduler>();
        services.TryAddSingleton<IDiscordWebhookClient>(serviceProvider => new DiscordWebhookClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DiscordWebhookClient>>()));
        services.TryAddScoped<IJobService, JobService>();
        services.TryAddScoped<IJobQueueClient, HangfireJobQueueClient>();
        services.TryAddScoped<IJobDispatcher, JobDispatcher>();
        services.TryAddScoped<ISearchVolumeJobScheduler, SearchVolumeHangfireJobScheduler>();
        services.TryAddScoped<IRankCheckJobScheduler, RankCheckHangfireJobScheduler>();
        services.TryAddSingleton<OperationalMetricsObserver>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OperationalMetricsObserver>());
        services.TryAddScoped<MasterDataSyncJob>();
        services.TryAddScoped<KeywordDiscoveryJob>();
        services.TryAddScoped<RegisterSearchVolumeJob>();
        services.TryAddScoped<PollSearchVolumeStatusJob>();
        services.TryAddScoped<FetchSearchVolumeResultsJob>();
        services.TryAddScoped<RegisterRankCheckJob>();
        services.TryAddScoped<PollRankStatusJob>();
        services.TryAddScoped<FetchRankResultsJob>();
        services.TryAddScoped<RankAlertEvaluateJob>();
        services.TryAddScoped<CompetitorRefreshJob>();
        services.TryAddScoped<ContentAnalyzeJob>();
        services.TryAddScoped<GenerateBriefJob>();
        services.TryAddScoped<TopicClusterGenerateJob>();
        services.TryAddScoped<RewriteScoringJob>();
        services.TryAddScoped<CannibalizationDetectionJob>();
        services.TryAddScoped<MonthlyReportJob>();
        services.TryAddScoped<ArticleBriefExportJob>();
        services.TryAddScoped<OpportunityScoringJob>();
        services.TryAddScoped<DataExportJob>();
        services.TryAddScoped<DataImportJob>();
        return services;
    }
}

internal sealed class AdministrationService(
    SeoIntelligenceDbContext dbContext,
    TimeProvider timeProvider,
    ISecretStore secretStore,
    IAuditLogWriter auditLogWriter,
    INotificationService notificationService)
    : IAdministrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<WorkspaceDetails>> GetWorkspaceAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var workspace = await dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == context.WorkspaceId, cancellationToken);

        return workspace is null
            ? Failure<WorkspaceDetails>(ErrorCode.NotFound, "Workspace was not found.")
            : Result<WorkspaceDetails>.Success(MapWorkspace(workspace));
    }

    public async Task<Result<WorkspaceDetails>> UpdateWorkspaceAsync(
        ProjectExecutionContext context,
        WorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var name = RequireText(request.Name, nameof(request.Name), errors);
        var defaultLocation = RequireText(request.DefaultLocation, nameof(request.DefaultLocation), errors);
        var defaultLanguage = RequireText(request.DefaultLanguage, nameof(request.DefaultLanguage), errors);
        var retentionSettings = OptionalJsonObject(request.RetentionSettings, nameof(request.RetentionSettings), errors);
        var notificationDefaults = OptionalJsonObject(request.NotificationDefaults, nameof(request.NotificationDefaults), errors);

        if (errors.HasErrors)
        {
            return ValidationFailure<WorkspaceDetails>(errors);
        }

        var workspace = await dbContext.Workspaces
            .FirstOrDefaultAsync(entity => entity.Id == context.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Failure<WorkspaceDetails>(ErrorCode.NotFound, "Workspace was not found.");
        }

        workspace.Name = name!;
        workspace.DefaultLocation = defaultLocation!;
        workspace.DefaultLanguage = defaultLanguage!;
        workspace.RetentionSettingsJson = retentionSettings ?? workspace.RetentionSettingsJson;
        workspace.NotificationDefaultsJson = notificationDefaults ?? workspace.NotificationDefaultsJson;
        workspace.UpdatedAt = NowUtc();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<WorkspaceDetails>.Success(MapWorkspace(workspace));
    }

    public async Task<Result<PagedResult<ApiCredentialDetails>>> SearchApiCredentialsAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.ApiCredentials
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        source = ApplyLifecycleStatusFilter(source, query.Status);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.Provider.ToLower().Contains(q) ||
                entity.KeyRef.ToLower().Contains(q));
        }

        source = SortApiCredentials(source, query.Sort);
        return Result<PagedResult<ApiCredentialDetails>>.Success(
            await ToPagedResultAsync(source, query, MapApiCredential, cancellationToken));
    }

    public async Task<Result<ApiCredentialDetails>> CreateApiCredentialAsync(
        ProjectExecutionContext context,
        ApiCredentialCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var provider = RequireText(request.Provider, nameof(request.Provider), errors);
        var keyRef = OptionalText(request.KeyRef);
        var secretValue = OptionalText(request.SecretValue);
        ValidateSecretReferenceInput(keyRef, secretValue, nameof(request.KeyRef), nameof(request.SecretValue), errors);

        if (errors.HasErrors)
        {
            return ValidationFailure<ApiCredentialDetails>(errors);
        }

        var now = NowUtc();
        var credentialId = UuidV7.New();
        if (secretValue is not null)
        {
            var secretResult = await StoreApiCredentialSecretAsync(
                BuildApiCredentialSecretName(provider!, credentialId),
                secretValue,
                cancellationToken);
            if (!secretResult.IsSuccess)
            {
                return Failure<ApiCredentialDetails>(secretResult.Error!.Code, secretResult.Error.Message);
            }

            keyRef = secretResult.Value!.Name;
        }

        var credential = new ApiCredentialEntity
        {
            Id = credentialId,
            WorkspaceId = context.WorkspaceId,
            Provider = provider!,
            KeyRef = keyRef!,
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ApiCredentials.Add(credential);
        AddApiCredentialAudit(context, AuditLogActionNames.ApiCredentialCreated, credential, before: null);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ApiCredentialDetails>.Success(MapApiCredential(credential));
    }

    public async Task<Result<ApiCredentialDetails>> GetApiCredentialAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await FindApiCredentialAsync(context.WorkspaceId, credentialId, cancellationToken);
        return credential is null
            ? Failure<ApiCredentialDetails>(ErrorCode.NotFound, "API credential was not found.")
            : Result<ApiCredentialDetails>.Success(MapApiCredential(credential));
    }

    public async Task<Result<ApiCredentialDetails>> UpdateApiCredentialAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        ApiCredentialUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var provider = RequireText(request.Provider, nameof(request.Provider), errors);
        if (errors.HasErrors)
        {
            return ValidationFailure<ApiCredentialDetails>(errors);
        }

        var credential = await FindApiCredentialAsync(context.WorkspaceId, credentialId, cancellationToken);
        if (credential is null)
        {
            return Failure<ApiCredentialDetails>(ErrorCode.NotFound, "API credential was not found.");
        }

        var before = ToApiCredentialAuditSnapshot(credential);
        credential.Provider = provider!;
        credential.UpdatedAt = NowUtc();
        AddApiCredentialAudit(context, AuditLogActionNames.ApiCredentialUpdated, credential, before);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ApiCredentialDetails>.Success(MapApiCredential(credential));
    }

    public Task<Result<ApiCredentialDetails>> DisableApiCredentialAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        CancellationToken cancellationToken = default)
        => SetApiCredentialStatusAsync(context, credentialId, StatusValues.Disabled, cancellationToken);

    public Task<Result<ApiCredentialDetails>> EnableApiCredentialAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        CancellationToken cancellationToken = default)
        => SetApiCredentialStatusAsync(context, credentialId, StatusValues.Active, cancellationToken);

    public async Task<Result<ApiCredentialDetails>> RotateApiCredentialAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        ApiCredentialRotateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var keyRef = OptionalText(request.NewKeyRef);
        var secretValue = OptionalText(request.NewSecretValue);
        ValidateSecretReferenceInput(keyRef, secretValue, nameof(request.NewKeyRef), nameof(request.NewSecretValue), errors);
        if (errors.HasErrors)
        {
            return ValidationFailure<ApiCredentialDetails>(errors);
        }

        var credential = await FindApiCredentialAsync(context.WorkspaceId, credentialId, cancellationToken);
        if (credential is null)
        {
            return Failure<ApiCredentialDetails>(ErrorCode.NotFound, "API credential was not found.");
        }

        var before = ToApiCredentialAuditSnapshot(credential);
        var now = NowUtc();
        if (secretValue is not null)
        {
            var secretResult = await StoreApiCredentialSecretAsync(
                BuildRotatedApiCredentialSecretName(credential.Provider, credential.Id, now),
                secretValue,
                cancellationToken);
            if (!secretResult.IsSuccess)
            {
                return Failure<ApiCredentialDetails>(secretResult.Error!.Code, secretResult.Error.Message);
            }

            keyRef = secretResult.Value!.Name;
        }

        credential.KeyRef = keyRef!;
        credential.UpdatedAt = now;
        AddApiCredentialAudit(context, AuditLogActionNames.ApiCredentialRotated, credential, before);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ApiCredentialDetails>.Success(MapApiCredential(credential));
    }

    public async Task<Result<PagedResult<NotificationChannelDetails>>> SearchNotificationChannelsAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.NotificationChannels
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        source = ApplyLifecycleStatusFilter(source, query.Status);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.ChannelType.ToLower().Contains(q) ||
                entity.Name.ToLower().Contains(q) ||
                entity.WebhookSecretRef.ToLower().Contains(q));
        }

        source = SortNotificationChannels(source, query.Sort);
        return Result<PagedResult<NotificationChannelDetails>>.Success(
            await ToPagedResultAsync(source, query, MapNotificationChannel, cancellationToken));
    }

    public async Task<Result<NotificationChannelDetails>> CreateNotificationChannelAsync(
        ProjectExecutionContext context,
        NotificationChannelCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateNotificationChannelRequest(request.ProjectId, request.ChannelType, request.Name, request.WebhookSecretRef, request.EventTypes);
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<NotificationChannelDetails>(validation.Errors);
        }

        var projectCheck = await ValidateOptionalProjectAsync(context.WorkspaceId, validation.ProjectId, cancellationToken);
        if (projectCheck is not null)
        {
            return Failure<NotificationChannelDetails>(projectCheck.Code, projectCheck.Message);
        }

        var now = NowUtc();
        var channel = new NotificationChannelEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = validation.ProjectId,
            ChannelType = validation.ChannelType!,
            Name = validation.Name!,
            WebhookSecretRef = validation.WebhookSecretRef!,
            EventTypesJson = SerializeStringArray(validation.EventTypes),
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.NotificationChannels.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<NotificationChannelDetails>.Success(MapNotificationChannel(channel));
    }

    public async Task<Result<NotificationChannelDetails>> GetNotificationChannelAsync(
        ProjectExecutionContext context,
        Guid channelId,
        CancellationToken cancellationToken = default)
    {
        var channel = await FindNotificationChannelAsync(context.WorkspaceId, channelId, cancellationToken);
        return channel is null
            ? Failure<NotificationChannelDetails>(ErrorCode.NotFound, "Notification channel was not found.")
            : Result<NotificationChannelDetails>.Success(MapNotificationChannel(channel));
    }

    public async Task<Result<NotificationChannelDetails>> UpdateNotificationChannelAsync(
        ProjectExecutionContext context,
        Guid channelId,
        NotificationChannelUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateNotificationChannelRequest(request.ProjectId, request.ChannelType, request.Name, request.WebhookSecretRef, request.EventTypes);
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<NotificationChannelDetails>(validation.Errors);
        }

        var projectCheck = await ValidateOptionalProjectAsync(context.WorkspaceId, validation.ProjectId, cancellationToken);
        if (projectCheck is not null)
        {
            return Failure<NotificationChannelDetails>(projectCheck.Code, projectCheck.Message);
        }

        var channel = await FindNotificationChannelAsync(context.WorkspaceId, channelId, cancellationToken);
        if (channel is null)
        {
            return Failure<NotificationChannelDetails>(ErrorCode.NotFound, "Notification channel was not found.");
        }

        channel.ProjectId = validation.ProjectId;
        channel.ChannelType = validation.ChannelType!;
        channel.Name = validation.Name!;
        channel.WebhookSecretRef = validation.WebhookSecretRef!;
        channel.EventTypesJson = SerializeStringArray(validation.EventTypes);
        channel.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<NotificationChannelDetails>.Success(MapNotificationChannel(channel));
    }

    public Task<Result<NotificationChannelDetails>> DisableNotificationChannelAsync(
        ProjectExecutionContext context,
        Guid channelId,
        CancellationToken cancellationToken = default)
        => SetNotificationChannelStatusAsync(context, channelId, StatusValues.Disabled, cancellationToken);

    public Task<Result<NotificationChannelDetails>> EnableNotificationChannelAsync(
        ProjectExecutionContext context,
        Guid channelId,
        CancellationToken cancellationToken = default)
        => SetNotificationChannelStatusAsync(context, channelId, StatusValues.Active, cancellationToken);

    public async Task<Result<NotificationDeliveryDetails>> SendNotificationChannelTestAsync(
        ProjectExecutionContext context,
        Guid channelId,
        CancellationToken cancellationToken = default)
        => await notificationService.SendTestAsync(context, channelId, cancellationToken);

    public async Task<Result<PagedResult<NotificationDeliveryDetails>>> SearchNotificationDeliveriesAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        source = ApplyDeliveryStatusFilter(source, query.Status);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.EventType.ToLower().Contains(q) ||
                (entity.ResourceType != null && entity.ResourceType.ToLower().Contains(q)) ||
                (entity.ResourceId != null && entity.ResourceId.ToLower().Contains(q)) ||
                (entity.CorrelationId != null && entity.CorrelationId.ToLower().Contains(q)));
        }

        source = SortNotificationDeliveries(source, query.Sort);
        return Result<PagedResult<NotificationDeliveryDetails>>.Success(
            await ToPagedResultAsync(source, query, MapNotificationDelivery, cancellationToken));
    }

    public async Task<Result<NotificationDeliveryDetails>> GetNotificationDeliveryAsync(
        ProjectExecutionContext context,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var delivery = await FindNotificationDeliveryAsync(context.WorkspaceId, deliveryId, cancellationToken);
        return delivery is null
            ? Failure<NotificationDeliveryDetails>(ErrorCode.NotFound, "Notification delivery was not found.")
            : Result<NotificationDeliveryDetails>.Success(MapNotificationDelivery(delivery));
    }

    public async Task<Result<NotificationDeliveryDetails>> RetryNotificationDeliveryAsync(
        ProjectExecutionContext context,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
        => await notificationService.RetryAsync(context, deliveryId, cancellationToken);

    public async Task<Result<PagedResult<ExternalApiCallDetails>>> SearchExternalApiCallsAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.ExternalApiCalls
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.Provider.ToLower().Contains(q) ||
                entity.Endpoint.ToLower().Contains(q) ||
                (entity.CorrelationId != null && entity.CorrelationId.ToLower().Contains(q)) ||
                (entity.ErrorCode != null && entity.ErrorCode.ToLower().Contains(q)));
        }

        source = SortExternalApiCalls(source, query.Sort);
        return Result<PagedResult<ExternalApiCallDetails>>.Success(
            await ToPagedResultAsync(source, query, MapExternalApiCall, cancellationToken));
    }

    public async Task<Result<PagedResult<AuditLogDetails>>> SearchAuditLogsAsync(
        ProjectExecutionContext context,
        AuditLogSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.AuditLogs
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        var q = NormalizeSearchText(query.Query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.Actor.ToLower().Contains(q) ||
                entity.Action.ToLower().Contains(q) ||
                entity.ResourceType.ToLower().Contains(q) ||
                entity.ResourceId.ToLower().Contains(q) ||
                (entity.CorrelationId != null && entity.CorrelationId.ToLower().Contains(q)));
        }

        var actor = NormalizeSearchText(query.Actor);
        if (actor is not null)
        {
            source = source.Where(entity => entity.Actor.ToLower() == actor);
        }

        var resourceType = NormalizeSearchText(query.ResourceType);
        if (resourceType is not null)
        {
            source = source.Where(entity => entity.ResourceType.ToLower() == resourceType);
        }

        var resourceId = OptionalText(query.ResourceId);
        if (resourceId is not null)
        {
            source = source.Where(entity => entity.ResourceId == resourceId);
        }

        var correlationId = NormalizeSearchText(query.CorrelationId);
        if (correlationId is not null)
        {
            source = source.Where(entity => entity.CorrelationId != null && entity.CorrelationId.ToLower() == correlationId);
        }

        if (query.CreatedFrom.HasValue)
        {
            var createdFromUtc = query.CreatedFrom.Value.UtcDateTime;
            source = source.Where(entity => entity.CreatedAt >= createdFromUtc);
        }

        if (query.CreatedTo.HasValue)
        {
            var createdToUtc = query.CreatedTo.Value.UtcDateTime;
            source = source.Where(entity => entity.CreatedAt <= createdToUtc);
        }

        source = SortAuditLogs(source, query.Query.Sort);
        return Result<PagedResult<AuditLogDetails>>.Success(
            await ToPagedResultAsync(source, query.Query, MapAuditLog, cancellationToken));
    }

    public async Task<Result<AuditLogDetails>> GetAuditLogAsync(
        ProjectExecutionContext context,
        Guid auditLogId,
        CancellationToken cancellationToken = default)
    {
        var auditLog = await dbContext.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == auditLogId, cancellationToken);

        return auditLog is null
            ? Failure<AuditLogDetails>(ErrorCode.NotFound, "Audit log was not found.")
            : Result<AuditLogDetails>.Success(MapAuditLog(auditLog));
    }

    public async Task<Result<PagedResult<ProjectDetails>>> SearchProjectsAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.Projects
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        source = ApplyLifecycleStatusFilter(source, query.Status);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.Name.ToLower().Contains(q) ||
                entity.DefaultLocation.ToLower().Contains(q) ||
                entity.DefaultLanguage.ToLower().Contains(q) ||
                (entity.Memo != null && entity.Memo.ToLower().Contains(q)));
        }

        source = SortProjects(source, query.Sort);
        return Result<PagedResult<ProjectDetails>>.Success(
            await ToPagedResultAsync(source, query, MapProject, cancellationToken));
    }

    public async Task<Result<ProjectDetails>> CreateProjectAsync(
        ProjectExecutionContext context,
        ProjectCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var name = RequireText(request.Name, nameof(request.Name), errors);
        var defaultLocation = RequireText(request.DefaultLocation, nameof(request.DefaultLocation), errors);
        var defaultLanguage = RequireText(request.DefaultLanguage, nameof(request.DefaultLanguage), errors);
        var kpi = OptionalJsonObject(request.Kpi, nameof(request.Kpi), errors) ?? "{}";

        if (errors.HasErrors)
        {
            return ValidationFailure<ProjectDetails>(errors);
        }

        if (await ProjectNameExistsAsync(context.WorkspaceId, name!, null, cancellationToken))
        {
            return Failure<ProjectDetails>(ErrorCode.Conflict, "Project name already exists in this workspace.");
        }

        var now = NowUtc();
        var project = new ProjectEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            Name = name!,
            DefaultLocation = defaultLocation!,
            DefaultLanguage = defaultLanguage!,
            KpiJson = kpi,
            Memo = OptionalText(request.Memo),
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProjectDetails>.Success(MapProject(project));
    }

    public async Task<Result<ProjectDetails>> GetProjectAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(context.WorkspaceId, projectId, cancellationToken);
        return project is null
            ? Failure<ProjectDetails>(ErrorCode.NotFound, "Project was not found.")
            : Result<ProjectDetails>.Success(MapProject(project));
    }

    public async Task<Result<ProjectDetails>> UpdateProjectAsync(
        ProjectExecutionContext context,
        Guid projectId,
        ProjectUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var name = RequireText(request.Name, nameof(request.Name), errors);
        var defaultLocation = RequireText(request.DefaultLocation, nameof(request.DefaultLocation), errors);
        var defaultLanguage = RequireText(request.DefaultLanguage, nameof(request.DefaultLanguage), errors);
        var kpi = OptionalJsonObject(request.Kpi, nameof(request.Kpi), errors) ?? "{}";

        if (errors.HasErrors)
        {
            return ValidationFailure<ProjectDetails>(errors);
        }

        var project = await FindProjectAsync(context.WorkspaceId, projectId, cancellationToken);
        if (project is null)
        {
            return Failure<ProjectDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        if (await ProjectNameExistsAsync(context.WorkspaceId, name!, projectId, cancellationToken))
        {
            return Failure<ProjectDetails>(ErrorCode.Conflict, "Project name already exists in this workspace.");
        }

        project.Name = name!;
        project.DefaultLocation = defaultLocation!;
        project.DefaultLanguage = defaultLanguage!;
        project.KpiJson = kpi;
        project.Memo = OptionalText(request.Memo);
        project.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProjectDetails>.Success(MapProject(project));
    }

    public Task<Result<ProjectDetails>> ArchiveProjectAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => SetProjectStatusAsync(context, projectId, StatusValues.Archived, cancellationToken);

    public Task<Result<ProjectDetails>> RestoreProjectAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => SetProjectStatusAsync(context, projectId, StatusValues.Active, cancellationToken);

    public async Task<Result<PagedResult<SiteDetails>>> SearchSitesAsync(
        ProjectExecutionContext context,
        Guid projectId,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(context.WorkspaceId, projectId, cancellationToken))
        {
            return Failure<PagedResult<SiteDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var source = dbContext.Sites
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId);

        source = ApplyLifecycleStatusFilter(source, query.Status);

        var q = NormalizeSearchText(query.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.Domain.ToLower().Contains(q) ||
                entity.CanonicalUrl.ToLower().Contains(q) ||
                entity.Type.ToLower().Contains(q) ||
                (entity.Memo != null && entity.Memo.ToLower().Contains(q)));
        }

        source = SortSites(source, query.Sort);
        return Result<PagedResult<SiteDetails>>.Success(
            await ToPagedResultAsync(source, query, MapSite, cancellationToken));
    }

    public async Task<Result<SiteDetails>> CreateSiteAsync(
        ProjectExecutionContext context,
        Guid projectId,
        SiteCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(context.WorkspaceId, projectId, cancellationToken))
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var validation = ValidateSiteRequest(request.Domain, request.CanonicalUrl, request.Type);
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<SiteDetails>(validation.Errors);
        }

        var now = NowUtc();
        var site = new SiteEntity
        {
            Id = UuidV7.New(),
            ProjectId = projectId,
            Domain = validation.Domain!,
            CanonicalUrl = validation.CanonicalUrl!,
            Type = validation.Type!,
            Memo = OptionalText(request.Memo),
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<SiteDetails>.Success(MapSite(site));
    }

    public async Task<Result<SiteDetails>> GetSiteAsync(
        ProjectExecutionContext context,
        Guid projectId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(context.WorkspaceId, projectId, cancellationToken))
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var site = await FindSiteAsync(projectId, siteId, cancellationToken);
        return site is null
            ? Failure<SiteDetails>(ErrorCode.NotFound, "Site was not found.")
            : Result<SiteDetails>.Success(MapSite(site));
    }

    public async Task<Result<SiteDetails>> UpdateSiteAsync(
        ProjectExecutionContext context,
        Guid projectId,
        Guid siteId,
        SiteUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ProjectExistsAsync(context.WorkspaceId, projectId, cancellationToken))
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var validation = ValidateSiteRequest(request.Domain, request.CanonicalUrl, request.Type);
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<SiteDetails>(validation.Errors);
        }

        var site = await FindSiteAsync(projectId, siteId, cancellationToken);
        if (site is null)
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Site was not found.");
        }

        site.Domain = validation.Domain!;
        site.CanonicalUrl = validation.CanonicalUrl!;
        site.Type = validation.Type!;
        site.Memo = OptionalText(request.Memo);
        site.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<SiteDetails>.Success(MapSite(site));
    }

    public Task<Result<SiteDetails>> ArchiveSiteAsync(
        ProjectExecutionContext context,
        Guid projectId,
        Guid siteId,
        CancellationToken cancellationToken = default)
        => SetSiteStatusAsync(context, projectId, siteId, StatusValues.Archived, cancellationToken);

    public Task<Result<SiteDetails>> RestoreSiteAsync(
        ProjectExecutionContext context,
        Guid projectId,
        Guid siteId,
        CancellationToken cancellationToken = default)
        => SetSiteStatusAsync(context, projectId, siteId, StatusValues.Active, cancellationToken);

    private async Task<Result<ApiCredentialDetails>> SetApiCredentialStatusAsync(
        ProjectExecutionContext context,
        Guid credentialId,
        string status,
        CancellationToken cancellationToken)
    {
        var credential = await FindApiCredentialAsync(context.WorkspaceId, credentialId, cancellationToken);
        if (credential is null)
        {
            return Failure<ApiCredentialDetails>(ErrorCode.NotFound, "API credential was not found.");
        }

        var before = ToApiCredentialAuditSnapshot(credential);
        var now = NowUtc();
        credential.Status = status;
        credential.DisabledAt = status == StatusValues.Disabled ? now : null;
        credential.UpdatedAt = now;
        AddApiCredentialAudit(
            context,
            status == StatusValues.Disabled ? AuditLogActionNames.ApiCredentialDisabled : AuditLogActionNames.ApiCredentialEnabled,
            credential,
            before);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ApiCredentialDetails>.Success(MapApiCredential(credential));
    }

    private async Task<Result<NotificationChannelDetails>> SetNotificationChannelStatusAsync(
        ProjectExecutionContext context,
        Guid channelId,
        string status,
        CancellationToken cancellationToken)
    {
        var channel = await FindNotificationChannelAsync(context.WorkspaceId, channelId, cancellationToken);
        if (channel is null)
        {
            return Failure<NotificationChannelDetails>(ErrorCode.NotFound, "Notification channel was not found.");
        }

        var now = NowUtc();
        channel.Status = status;
        channel.DisabledAt = status == StatusValues.Disabled ? now : null;
        channel.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<NotificationChannelDetails>.Success(MapNotificationChannel(channel));
    }

    private async Task<Result<SecretReference>> StoreApiCredentialSecretAsync(
        string secretName,
        string secretValue,
        CancellationToken cancellationToken)
    {
        try
        {
            var reference = new SecretReference(secretName);
            await secretStore.PutAsync(reference, new SecretValue(secretValue), cancellationToken);
            return Result<SecretReference>.Success(reference);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<SecretReference>.Failure(
                new Error(ErrorCode.SecretUnavailable, "Secret Store could not save the API credential secret."));
        }
    }

    private void AddApiCredentialAudit(
        ProjectExecutionContext context,
        string action,
        ApiCredentialEntity credential,
        object? before)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.ApiCredential,
                credential.Id.ToString("D"),
                new
                {
                    before,
                    after = ToApiCredentialAuditSnapshot(credential)
                }));

    private async Task<Result<ProjectDetails>> SetProjectStatusAsync(
        ProjectExecutionContext context,
        Guid projectId,
        string status,
        CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(context.WorkspaceId, projectId, cancellationToken);
        if (project is null)
        {
            return Failure<ProjectDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var now = NowUtc();
        project.Status = status;
        project.ArchivedAt = status == StatusValues.Archived ? now : null;
        project.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProjectDetails>.Success(MapProject(project));
    }

    private async Task<Result<SiteDetails>> SetSiteStatusAsync(
        ProjectExecutionContext context,
        Guid projectId,
        Guid siteId,
        string status,
        CancellationToken cancellationToken)
    {
        if (!await ProjectExistsAsync(context.WorkspaceId, projectId, cancellationToken))
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var site = await FindSiteAsync(projectId, siteId, cancellationToken);
        if (site is null)
        {
            return Failure<SiteDetails>(ErrorCode.NotFound, "Site was not found.");
        }

        var now = NowUtc();
        site.Status = status;
        site.ArchivedAt = status == StatusValues.Archived ? now : null;
        site.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<SiteDetails>.Success(MapSite(site));
    }

    private Task<ApiCredentialEntity?> FindApiCredentialAsync(Guid workspaceId, Guid credentialId, CancellationToken cancellationToken)
        => dbContext.ApiCredentials
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == credentialId, cancellationToken);

    private Task<NotificationChannelEntity?> FindNotificationChannelAsync(Guid workspaceId, Guid channelId, CancellationToken cancellationToken)
        => dbContext.NotificationChannels
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == channelId, cancellationToken);

    private Task<NotificationDeliveryEntity?> FindNotificationDeliveryAsync(Guid workspaceId, Guid deliveryId, CancellationToken cancellationToken)
        => dbContext.NotificationDeliveries
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == deliveryId, cancellationToken);

    private Task<ProjectEntity?> FindProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
        => dbContext.Projects
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == projectId, cancellationToken);

    private Task<SiteEntity?> FindSiteAsync(Guid projectId, Guid siteId, CancellationToken cancellationToken)
        => dbContext.Sites
            .FirstOrDefaultAsync(entity => entity.ProjectId == projectId && entity.Id == siteId, cancellationToken);

    private Task<bool> ProjectExistsAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
        => dbContext.Projects
            .AsNoTracking()
            .AnyAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == projectId, cancellationToken);

    private Task<bool> ProjectNameExistsAsync(Guid workspaceId, string name, Guid? excludedProjectId, CancellationToken cancellationToken)
        => dbContext.Projects
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.WorkspaceId == workspaceId &&
                entity.Name == name &&
                (!excludedProjectId.HasValue || entity.Id != excludedProjectId.Value),
                cancellationToken);

    private async Task<Error?> ValidateOptionalProjectAsync(Guid workspaceId, Guid? projectId, CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return null;
        }

        return await ProjectExistsAsync(workspaceId, projectId.Value, cancellationToken)
            ? null
            : new Error(ErrorCode.NotFound, "Project was not found.");
    }

    private static IQueryable<T> ApplyLifecycleStatusFilter<T>(IQueryable<T> source, string? status)
        where T : class
    {
        var normalized = NormalizeStatus(status, StatusValues.Active);
        if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        return source switch
        {
            IQueryable<ApiCredentialEntity> credentials => (IQueryable<T>)credentials.Where(entity => entity.Status == normalized),
            IQueryable<NotificationChannelEntity> channels => (IQueryable<T>)channels.Where(entity => entity.Status == normalized),
            IQueryable<ProjectEntity> projects => (IQueryable<T>)projects.Where(entity => entity.Status == normalized),
            IQueryable<SiteEntity> sites => (IQueryable<T>)sites.Where(entity => entity.Status == normalized),
            _ => source
        };
    }

    private static IQueryable<NotificationDeliveryEntity> ApplyDeliveryStatusFilter(
        IQueryable<NotificationDeliveryEntity> source,
        string? status)
    {
        var normalized = NormalizeStatus(status, "all");
        return string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase)
            ? source
            : source.Where(entity => entity.Status == normalized);
    }

    private static IQueryable<ApiCredentialEntity> SortApiCredentials(IQueryable<ApiCredentialEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("provider", query => query.OrderBy(entity => entity.Provider), query => query.OrderByDescending(entity => entity.Provider)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("updatedAt", query => query.OrderBy(entity => entity.UpdatedAt), query => query.OrderByDescending(entity => entity.UpdatedAt)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<NotificationChannelEntity> SortNotificationChannels(IQueryable<NotificationChannelEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("name", query => query.OrderBy(entity => entity.Name), query => query.OrderByDescending(entity => entity.Name)),
            ("channelType", query => query.OrderBy(entity => entity.ChannelType), query => query.OrderByDescending(entity => entity.ChannelType)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("updatedAt", query => query.OrderBy(entity => entity.UpdatedAt), query => query.OrderByDescending(entity => entity.UpdatedAt)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<NotificationDeliveryEntity> SortNotificationDeliveries(IQueryable<NotificationDeliveryEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("eventType", query => query.OrderBy(entity => entity.EventType), query => query.OrderByDescending(entity => entity.EventType)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("retryCount", query => query.OrderBy(entity => entity.RetryCount), query => query.OrderByDescending(entity => entity.RetryCount)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<ExternalApiCallEntity> SortExternalApiCalls(IQueryable<ExternalApiCallEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("provider", query => query.OrderBy(entity => entity.Provider), query => query.OrderByDescending(entity => entity.Provider)),
            ("endpoint", query => query.OrderBy(entity => entity.Endpoint), query => query.OrderByDescending(entity => entity.Endpoint)),
            ("statusCode", query => query.OrderBy(entity => entity.StatusCode), query => query.OrderByDescending(entity => entity.StatusCode)),
            ("consumedCredit", query => query.OrderBy(entity => entity.ConsumedCredit), query => query.OrderByDescending(entity => entity.ConsumedCredit)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<AuditLogEntity> SortAuditLogs(IQueryable<AuditLogEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("actor", query => query.OrderBy(entity => entity.Actor), query => query.OrderByDescending(entity => entity.Actor)),
            ("action", query => query.OrderBy(entity => entity.Action), query => query.OrderByDescending(entity => entity.Action)),
            ("resourceType", query => query.OrderBy(entity => entity.ResourceType), query => query.OrderByDescending(entity => entity.ResourceType)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<ProjectEntity> SortProjects(IQueryable<ProjectEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("name", query => query.OrderBy(entity => entity.Name), query => query.OrderByDescending(entity => entity.Name)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("updatedAt", query => query.OrderBy(entity => entity.UpdatedAt), query => query.OrderByDescending(entity => entity.UpdatedAt)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<SiteEntity> SortSites(IQueryable<SiteEntity> source, SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("domain", query => query.OrderBy(entity => entity.Domain), query => query.OrderByDescending(entity => entity.Domain)),
            ("type", query => query.OrderBy(entity => entity.Type), query => query.OrderByDescending(entity => entity.Type)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("updatedAt", query => query.OrderBy(entity => entity.UpdatedAt), query => query.OrderByDescending(entity => entity.UpdatedAt)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<T> SortBy<T>(
        IQueryable<T> source,
        SortRequest? sort,
        string defaultSortBy,
        params (string SortBy, Func<IQueryable<T>, IOrderedQueryable<T>> Asc, Func<IQueryable<T>, IOrderedQueryable<T>> Desc)[] choices)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? defaultSortBy;
        var choice = choices.FirstOrDefault(item => string.Equals(item.SortBy, sortBy, StringComparison.OrdinalIgnoreCase));
        if (choice.SortBy is null)
        {
            choice = choices.First(item => string.Equals(item.SortBy, defaultSortBy, StringComparison.OrdinalIgnoreCase));
        }

        return sort?.Direction == SortDirection.Asc
            ? choice.Asc(source)
            : choice.Desc(source);
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        IQueryable<TEntity> source,
        SearchQuery query,
        Func<TEntity, TResponse> map,
        CancellationToken cancellationToken)
    {
        var page = query.EffectivePage;
        var totalCount = await source.LongCountAsync(cancellationToken);
        var entities = await source
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResponse>(
            entities.Select(map).ToArray(),
            page.Page,
            page.PageSize,
            totalCount);
    }

    private static NotificationChannelValidationResult ValidateNotificationChannelRequest(
        Guid? projectId,
        string? channelTypeValue,
        string? nameValue,
        string? webhookSecretRefValue,
        IReadOnlyList<string>? eventTypeValues)
    {
        var errors = new ValidationErrors();

        if (projectId == Guid.Empty)
        {
            errors.Add(nameof(projectId), "projectId must not be empty when provided.");
        }

        var channelType = RequireText(channelTypeValue, nameof(channelTypeValue), errors);
        if (channelType is not null && !string.Equals(channelType, "discord", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(nameof(channelTypeValue), "channelType must be discord.");
        }

        var name = RequireText(nameValue, nameof(nameValue), errors);
        var webhookSecretRef = RequireText(webhookSecretRefValue, nameof(webhookSecretRefValue), errors);
        var eventTypes = eventTypeValues?
            .Select(OptionalText)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (eventTypes.Length == 0)
        {
            errors.Add(nameof(eventTypeValues), "eventTypes must contain at least one event type.");
        }

        return new NotificationChannelValidationResult(
            errors,
            projectId,
            channelType?.ToLowerInvariant(),
            name,
            webhookSecretRef,
            eventTypes);
    }

    private static SiteValidationResult ValidateSiteRequest(string? domainValue, string? canonicalUrlValue, string? typeValue)
    {
        var errors = new ValidationErrors();
        string? domain = null;
        string? canonicalUrl = null;
        var type = RequireText(typeValue, nameof(typeValue), errors);

        try
        {
            domain = UrlNormalizer.NormalizeDomain(RequireText(domainValue, nameof(domainValue), errors) ?? string.Empty);
        }
        catch (ArgumentException)
        {
            errors.Add(nameof(domainValue), "domain must be a valid domain or URL.");
        }

        try
        {
            canonicalUrl = UrlNormalizer.NormalizeUrl(RequireText(canonicalUrlValue, nameof(canonicalUrlValue), errors) ?? string.Empty);
        }
        catch (ArgumentException)
        {
            errors.Add(nameof(canonicalUrlValue), "canonicalUrl must be a valid URL.");
        }

        if (type is not null && !IsAllowedSiteType(type))
        {
            errors.Add(nameof(typeValue), "type must be own, competitor, or reference.");
        }

        return new SiteValidationResult(errors, domain, canonicalUrl, type?.ToLowerInvariant());
    }

    private static bool IsAllowedSiteType(string type)
        => string.Equals(type, "own", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "competitor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "reference", StringComparison.OrdinalIgnoreCase);

    private static void ValidateSecretReferenceInput(
        string? keyRef,
        string? secretValue,
        string keyRefTarget,
        string secretValueTarget,
        ValidationErrors errors)
    {
        if (!string.IsNullOrWhiteSpace(keyRef) && !string.IsNullOrWhiteSpace(secretValue))
        {
            errors.Add(secretValueTarget, "Specify either keyRef or secretValue, not both.");
        }

        if (string.IsNullOrWhiteSpace(keyRef) && string.IsNullOrWhiteSpace(secretValue))
        {
            errors.Add(keyRefTarget, "Specify either keyRef or secretValue.");
        }
    }

    private static string BuildApiCredentialSecretName(string provider, Guid credentialId)
        => $"api-credential-{NormalizeSecretNameSegment(provider)}-{credentialId:N}";

    private static string BuildRotatedApiCredentialSecretName(string provider, Guid credentialId, DateTime rotatedAtUtc)
        => $"{BuildApiCredentialSecretName(provider, credentialId)}-rotated-{rotatedAtUtc.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}";

    private static string NormalizeSecretNameSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "provider" : normalized;
    }

    private static string? OptionalJsonObject(JsonElement? value, string target, ValidationErrors errors)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (value.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add(target, $"{target} must be a JSON object.");
            return null;
        }

        return value.Value.GetRawText();
    }

    private static string? RequireText(string? value, string target, ValidationErrors errors, int maxLength = 500)
    {
        var trimmed = OptionalText(value);
        if (trimmed is null)
        {
            errors.Add(target, $"{ToCamelCase(target)} is required.");
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors.Add(target, $"{ToCamelCase(target)} must be {maxLength} characters or fewer.");
            return null;
        }

        return trimmed;
    }

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSearchText(string? value)
        => OptionalText(value)?.ToLowerInvariant();

    private static string NormalizeStatus(string? value, string defaultValue)
        => OptionalText(value)?.ToLowerInvariant() ?? defaultValue;

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static object ToApiCredentialAuditSnapshot(ApiCredentialEntity entity)
        => new
        {
            provider = entity.Provider,
            keyRef = entity.KeyRef,
            status = entity.Status,
            disabledAt = entity.DisabledAt
        };

    private static WorkspaceDetails MapWorkspace(WorkspaceEntity entity)
        => new(
            entity.Id,
            entity.Name,
            entity.DefaultLocation,
            entity.DefaultLanguage,
            ParseJson(entity.RetentionSettingsJson),
            ParseJson(entity.NotificationDefaultsJson),
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static ApiCredentialDetails MapApiCredential(ApiCredentialEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.Provider,
            entity.KeyRef,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.DisabledAt);

    private static NotificationChannelDetails MapNotificationChannel(NotificationChannelEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.ChannelType,
            entity.Name,
            entity.WebhookSecretRef,
            DeserializeStringArray(entity.EventTypesJson),
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.DisabledAt);

    private static NotificationDeliveryDetails MapNotificationDelivery(NotificationDeliveryEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.ChannelId,
            entity.JobId,
            entity.ResourceType,
            entity.ResourceId,
            entity.EventType,
            entity.PayloadHash,
            entity.Status,
            entity.ErrorMessage,
            entity.RetryCount,
            entity.NextRetryAt,
            entity.SentAt,
            entity.DeliveredAt,
            entity.CorrelationId,
            entity.CreatedAt);

    private static ExternalApiCallDetails MapExternalApiCall(ExternalApiCallEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.JobId,
            entity.ApiCredentialId,
            entity.Provider,
            entity.Endpoint,
            entity.ResponseHash,
            entity.ContractScopeKey,
            entity.CacheHit,
            entity.StatusCode,
            entity.ConsumedCredit,
            entity.DurationMs,
            entity.ErrorCode,
            entity.CorrelationId,
            entity.Actor,
            entity.CreatedAt);

    private static AuditLogDetails MapAuditLog(AuditLogEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.Actor,
            entity.Action,
            entity.ResourceType,
            entity.ResourceId,
            ParseJson(entity.BeforeAfterJson),
            entity.CorrelationId,
            entity.IpAddress?.ToString(),
            entity.UserAgent,
            entity.CreatedAt);

    private static ProjectDetails MapProject(ProjectEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.Name,
            entity.DefaultLocation,
            entity.DefaultLanguage,
            ParseJson(entity.KpiJson),
            entity.Memo,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ArchivedAt);

    private static SiteDetails MapSite(SiteEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.Domain,
            entity.CanonicalUrl,
            entity.Type,
            entity.Memo,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ArchivedAt);

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static string SerializeStringArray(IReadOnlyList<string> values)
        => JsonSerializer.Serialize(values, JsonOptions);

    private static IReadOnlyList<string> DeserializeStringArray(string json)
        => JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];

    private static string ToCamelCase(string value)
    {
        var sanitized = value.Trim();
        return string.IsNullOrEmpty(sanitized)
            ? sanitized
            : char.ToLowerInvariant(sanitized[0]) + sanitized[1..];
    }

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => _errors.Count > 0;

        public void Add(string target, string message)
        {
            var camelTarget = ToCamelCase(target);
            if (!_errors.TryGetValue(camelTarget, out var messages))
            {
                messages = [];
                _errors[camelTarget] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => _errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record NotificationChannelValidationResult(
        ValidationErrors Errors,
        Guid? ProjectId,
        string? ChannelType,
        string? Name,
        string? WebhookSecretRef,
        IReadOnlyList<string> EventTypes);

    private sealed record SiteValidationResult(
        ValidationErrors Errors,
        string? Domain,
        string? CanonicalUrl,
        string? Type);
}
