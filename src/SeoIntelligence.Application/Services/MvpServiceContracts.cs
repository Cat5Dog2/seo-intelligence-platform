using System.Text.Json;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Domain.Common;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Application.Services;

public interface IAdministrationService
{
    Task<Result<WorkspaceDetails>> GetWorkspaceAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);

    Task<Result<WorkspaceDetails>> UpdateWorkspaceAsync(ProjectExecutionContext context, WorkspaceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ApiCredentialDetails>>> SearchApiCredentialsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> CreateApiCredentialAsync(ProjectExecutionContext context, ApiCredentialCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> GetApiCredentialAsync(ProjectExecutionContext context, Guid credentialId, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> UpdateApiCredentialAsync(ProjectExecutionContext context, Guid credentialId, ApiCredentialUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> DisableApiCredentialAsync(ProjectExecutionContext context, Guid credentialId, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> EnableApiCredentialAsync(ProjectExecutionContext context, Guid credentialId, CancellationToken cancellationToken = default);

    Task<Result<ApiCredentialDetails>> RotateApiCredentialAsync(ProjectExecutionContext context, Guid credentialId, ApiCredentialRotateRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<NotificationChannelDetails>>> SearchNotificationChannelsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<NotificationChannelDetails>> CreateNotificationChannelAsync(ProjectExecutionContext context, NotificationChannelCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<NotificationChannelDetails>> GetNotificationChannelAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationChannelDetails>> UpdateNotificationChannelAsync(ProjectExecutionContext context, Guid channelId, NotificationChannelUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<NotificationChannelDetails>> DisableNotificationChannelAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationChannelDetails>> EnableNotificationChannelAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationDeliveryDetails>> SendNotificationChannelTestAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<NotificationDeliveryDetails>>> SearchNotificationDeliveriesAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<NotificationDeliveryDetails>> GetNotificationDeliveryAsync(ProjectExecutionContext context, Guid deliveryId, CancellationToken cancellationToken = default);

    Task<Result<NotificationDeliveryDetails>> RetryNotificationDeliveryAsync(ProjectExecutionContext context, Guid deliveryId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ExternalApiCallDetails>>> SearchExternalApiCallsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AuditLogDetails>>> SearchAuditLogsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<AuditLogDetails>> GetAuditLogAsync(ProjectExecutionContext context, Guid auditLogId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ProjectDetails>>> SearchProjectsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<ProjectDetails>> CreateProjectAsync(ProjectExecutionContext context, ProjectCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProjectDetails>> GetProjectAsync(ProjectExecutionContext context, Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<ProjectDetails>> UpdateProjectAsync(ProjectExecutionContext context, Guid projectId, ProjectUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProjectDetails>> ArchiveProjectAsync(ProjectExecutionContext context, Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<ProjectDetails>> RestoreProjectAsync(ProjectExecutionContext context, Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SiteDetails>>> SearchSitesAsync(ProjectExecutionContext context, Guid projectId, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<SiteDetails>> CreateSiteAsync(ProjectExecutionContext context, Guid projectId, SiteCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<SiteDetails>> GetSiteAsync(ProjectExecutionContext context, Guid projectId, Guid siteId, CancellationToken cancellationToken = default);

    Task<Result<SiteDetails>> UpdateSiteAsync(ProjectExecutionContext context, Guid projectId, Guid siteId, SiteUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<SiteDetails>> ArchiveSiteAsync(ProjectExecutionContext context, Guid projectId, Guid siteId, CancellationToken cancellationToken = default);

    Task<Result<SiteDetails>> RestoreSiteAsync(ProjectExecutionContext context, Guid projectId, Guid siteId, CancellationToken cancellationToken = default);
}

public interface IMasterDataService
{
    Task<Result<JobReference>> SyncAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LocationSummary>>> ListLocationsAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(CancellationToken cancellationToken = default);
}

public interface IKeywordDiscoveryService
{
    Task<Result<KeywordDiscoveryResult>> DiscoverAsync(ProjectExecutionContext context, KeywordDiscoveryRequest request, CancellationToken cancellationToken = default);
}

public interface ISearchVolumeService
{
    Task<Result<JobReference>> RegisterAsync(ProjectExecutionContext context, SearchVolumeJobRequest request, CancellationToken cancellationToken = default);

    Task<Result<JobReference>> GetJobAsync(ProjectExecutionContext context, Guid jobId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SearchVolumeResultRow>>> GetResultsAsync(ProjectExecutionContext context, Guid jobId, SearchQuery query, CancellationToken cancellationToken = default);
}

public interface IScoringService
{
    Task<Result<OpportunityScoreResult>> CalculateOpportunityScoresAsync(ProjectExecutionContext context, OpportunityScoreRequest request, CancellationToken cancellationToken = default);
}

public interface IDataTransferService
{
    Task<Result<DataExportReference>> CreateCsvExportAsync(ProjectExecutionContext context, DataExportRequest request, CancellationToken cancellationToken = default);

    Task<Result<DataExportReference>> GetExportAsync(ProjectExecutionContext context, Guid exportId, CancellationToken cancellationToken = default);
}

public interface IExternalApiUsageService
{
    Task<Result<ExternalApiUsageSummary>> GetUsageSummaryAsync(ProjectExecutionContext context, ExternalApiUsageQuery query, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<Result<NotificationResult>> SendTestAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationResult>> EnqueueAsync(ProjectExecutionContext context, NotificationRequest request, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<Result<DashboardSnapshot>> GetDashboardAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);
}

public sealed record WorkspaceUpdateRequest(
    string? Name,
    string? DefaultLocation,
    string? DefaultLanguage,
    JsonElement? RetentionSettings,
    JsonElement? NotificationDefaults);

public sealed record WorkspaceDetails(
    Guid WorkspaceId,
    string Name,
    string DefaultLocation,
    string DefaultLanguage,
    JsonElement RetentionSettings,
    JsonElement NotificationDefaults,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ApiCredentialCreateRequest(
    string? Provider,
    string? KeyRef,
    string? SecretValue);

public sealed record ApiCredentialUpdateRequest(string? Provider);

public sealed record ApiCredentialRotateRequest(
    string? NewKeyRef,
    string? NewSecretValue);

public sealed record ApiCredentialDetails(
    Guid CredentialId,
    Guid WorkspaceId,
    string Provider,
    string KeyRef,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DisabledAt);

public sealed record NotificationChannelCreateRequest(
    Guid? ProjectId,
    string? ChannelType,
    string? Name,
    string? WebhookSecretRef,
    IReadOnlyList<string>? EventTypes);

public sealed record NotificationChannelUpdateRequest(
    Guid? ProjectId,
    string? ChannelType,
    string? Name,
    string? WebhookSecretRef,
    IReadOnlyList<string>? EventTypes);

public sealed record NotificationChannelDetails(
    Guid ChannelId,
    Guid WorkspaceId,
    Guid? ProjectId,
    string ChannelType,
    string Name,
    string WebhookSecretRef,
    IReadOnlyList<string> EventTypes,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DisabledAt);

public sealed record NotificationDeliveryDetails(
    Guid DeliveryId,
    Guid WorkspaceId,
    Guid? ProjectId,
    Guid ChannelId,
    Guid? JobId,
    string? ResourceType,
    string? ResourceId,
    string EventType,
    string PayloadHash,
    string Status,
    string? ErrorMessage,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    string? CorrelationId,
    DateTime CreatedAt);

public sealed record ExternalApiCallDetails(
    Guid CallId,
    Guid WorkspaceId,
    Guid? ProjectId,
    Guid? JobId,
    Guid? ApiCredentialId,
    string Provider,
    string Endpoint,
    string? ResponseHash,
    string ContractScopeKey,
    bool CacheHit,
    int StatusCode,
    decimal ConsumedCredit,
    int DurationMs,
    string? ErrorCode,
    string? CorrelationId,
    string Actor,
    DateTime CreatedAt);

public sealed record AuditLogDetails(
    Guid AuditLogId,
    Guid WorkspaceId,
    string Actor,
    string Action,
    string ResourceType,
    string ResourceId,
    JsonElement BeforeAfter,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

public sealed record ProjectCreateRequest(
    string? Name,
    string? DefaultLocation,
    string? DefaultLanguage,
    JsonElement? Kpi,
    string? Memo);

public sealed record ProjectUpdateRequest(
    string? Name,
    string? DefaultLocation,
    string? DefaultLanguage,
    JsonElement? Kpi,
    string? Memo);

public sealed record ProjectDetails(
    Guid ProjectId,
    Guid WorkspaceId,
    string Name,
    string DefaultLocation,
    string DefaultLanguage,
    JsonElement Kpi,
    string? Memo,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt);

public sealed record SiteCreateRequest(
    string? Domain,
    string? CanonicalUrl,
    string? Type,
    string? Memo);

public sealed record SiteUpdateRequest(
    string? Domain,
    string? CanonicalUrl,
    string? Type,
    string? Memo);

public sealed record SiteDetails(
    Guid SiteId,
    Guid ProjectId,
    string Domain,
    string CanonicalUrl,
    string Type,
    string? Memo,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt);

public sealed record LocationSummary(string Provider, string Code, string Name, string? CountryCode, LifecycleStatus Status);

public sealed record LanguageSummary(string Provider, string Code, string Name, LifecycleStatus Status);

public sealed record KeywordDiscoveryRequest(
    IReadOnlyList<string> Seeds,
    IReadOnlyList<string> Engines,
    string Location,
    string Language);

public sealed record KeywordDiscoveryResult(IReadOnlyList<KeywordCandidate> Candidates);

public sealed record KeywordCandidate(string Keyword, string Source, string? SuggestClass, decimal? OpportunityScore);

public sealed record SearchVolumeJobRequest(
    IReadOnlyList<string> Keywords,
    string Location,
    string Language,
    int AggregationPeriodMonths = 12);

public sealed record SearchVolumeResultRow(
    string Keyword,
    int? SearchVolume,
    decimal? SeoDifficulty,
    decimal? Cpc,
    decimal? Competition);

public sealed record OpportunityScoreRequest(IReadOnlyList<Guid> KeywordIds, string Location, string Language);

public sealed record OpportunityScoreResult(IReadOnlyList<OpportunityScoreRow> Scores);

public sealed record OpportunityScoreRow(Guid KeywordId, decimal Score, IReadOnlyDictionary<string, decimal> Components);

public sealed record DataExportRequest(string ExportType, SearchQuery Query);

public sealed record DataExportReference(Guid ExportId, JobStatus Status, string? FileUri);

public sealed record ExternalApiUsageQuery(DateOnly? From, DateOnly? To, string? Provider);

public sealed record ExternalApiUsageSummary(int CallCount, int ConsumedCredit, int RetryableFailureCount, int FatalFailureCount);

public sealed record NotificationRequest(string EventType, string ResourceType, Guid? ResourceId, string Message);

public sealed record NotificationResult(Guid? DeliveryId, NotificationDeliveryStatus Status);

public sealed record DashboardSnapshot(
    int KeywordCandidateCount,
    int RunningJobCount,
    int FailedJobCount,
    int ConsumedCredit);

public sealed record JobReference(Guid JobId, JobStatus Status);
