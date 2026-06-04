using System.Text.Json;
using SeoIntelligence.Application.Auditing;
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

    Task<Result<PagedResult<AuditLogDetails>>> SearchAuditLogsAsync(ProjectExecutionContext context, AuditLogSearchQuery query, CancellationToken cancellationToken = default);

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

public interface ICompetitiveAnalysisService
{
    Task<Result<JobReference>> AnalyzeAsync(ProjectExecutionContext context, CompetitorAnalyzeRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<CompetitorResultRow>>> GetCompetitorsAsync(ProjectExecutionContext context, CompetitorSearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<InfluxKeywordResultRow>>> GetInfluxKeywordsAsync(ProjectExecutionContext context, InfluxKeywordSearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<InfluxPageResultRow>>> GetInfluxPagesAsync(ProjectExecutionContext context, InfluxPageSearchQuery query, CancellationToken cancellationToken = default);
}

public interface IScoringService
{
    Task<Result<OpportunityScoreResult>> CalculateOpportunityScoresAsync(ProjectExecutionContext context, OpportunityScoreRequest request, CancellationToken cancellationToken = default);
}

public interface IDataTransferService
{
    Task<Result<JobReference>> CreateCsvExportAsync(ProjectExecutionContext context, DataExportRequest request, CancellationToken cancellationToken = default);

    Task<Result<DataExportDetails>> GetExportAsync(ProjectExecutionContext context, Guid exportId, CancellationToken cancellationToken = default);

    Task<Result<DataExportDownload>> CreateDownloadUrlAsync(ProjectExecutionContext context, Guid exportId, CancellationToken cancellationToken = default);
}

public interface IExternalApiUsageService
{
    Task<Result<ExternalApiUsageSummary>> GetUsageSummaryAsync(ProjectExecutionContext context, ExternalApiUsageQuery query, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<Result<NotificationDeliveryDetails>> SendTestAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationResult>> EnqueueAsync(ProjectExecutionContext context, NotificationRequest request, CancellationToken cancellationToken = default);

    Task<Result<NotificationDeliveryDetails>> RetryAsync(ProjectExecutionContext context, Guid deliveryId, CancellationToken cancellationToken = default);
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

public sealed record LocationSummary(string Provider, string Code, string Name, string? CountryCode, string Status);

public sealed record LanguageSummary(string Provider, string Code, string Name, string Status);

public sealed record KeywordDiscoveryRequest(
    IReadOnlyList<string>? Seeds = null,
    IReadOnlyList<string>? Engines = null,
    string? Location = null,
    string? Language = null,
    string? SeedKeyword = null,
    IReadOnlyList<string>? Sources = null,
    int? Limit = null,
    KeywordDiscoveryFilter? Filter = null,
    string? SortBy = null,
    string? OrderBy = null,
    bool? SyncPreferred = null);

public sealed record KeywordDiscoveryFilter(
    decimal? MinSearchVolume = null,
    decimal? MaxSearchVolume = null,
    decimal? MinSeoDifficulty = null,
    decimal? MaxSeoDifficulty = null,
    decimal? MinCpc = null,
    decimal? MaxCpc = null,
    decimal? MinCompetition = null,
    decimal? MaxCompetition = null,
    string? FirstSeenRange = null,
    string? SuggestClass = null,
    IReadOnlyList<string>? Include = null,
    IReadOnlyList<string>? Exclude = null);

public sealed record KeywordDiscoveryResult(
    IReadOnlyList<KeywordCandidate> Candidates,
    Guid? SeedId = null,
    Guid? SeedKeywordId = null,
    string? SeedKeyword = null,
    string? Location = null,
    string? Language = null,
    IReadOnlyList<string>? Sources = null,
    bool IsAccepted = false,
    Guid? JobId = null,
    string? StatusUrl = null,
    IReadOnlyList<KeywordDiscoverySourceStatus>? SourceStatuses = null,
    decimal ConsumedCredit = 0);

public sealed record KeywordDiscoverySourceStatus(
    string Source,
    string Status,
    int CandidateCount,
    decimal ConsumedCredit = 0,
    int? StatusCode = null,
    string? ErrorCode = null,
    string? Message = null);

public sealed record KeywordCandidate(
    string Keyword,
    string Source,
    string? SuggestClass,
    decimal? OpportunityScore,
    Guid? KeywordId = null,
    string? Type = null,
    string? Question = null,
    string? Engine = null,
    int? EngineCount = null,
    decimal? SearchVolume = null,
    decimal? SeoDifficulty = null,
    decimal? Cpc = null,
    decimal? Competition = null,
    string? FirstSeenRange = null,
    decimal? Importance = null,
    int? WordCount = null,
    decimal? Relevance = null);

public sealed record SearchVolumeJobRequest(
    IReadOnlyList<string> Keywords,
    string Location,
    string Language,
    int AggregationPeriodMonths = 12,
    bool SeoDifficulty = true);

public sealed record SearchVolumeResultRow(
    string Keyword,
    int? SearchVolume,
    decimal? SeoDifficulty,
    decimal? Cpc,
    decimal? Competition,
    IReadOnlyDictionary<string, int>? MonthlySearchVolume = null,
    string? DataSource = null,
    bool CacheHit = false,
    Guid? KeywordId = null);

public sealed record CompetitorAnalyzeRequest(
    string? Target,
    Guid? SiteId = null);

public sealed record CompetitorSearchQuery(
    SearchQuery Search,
    string? Domain = null);

public sealed record InfluxKeywordSearchQuery(
    SearchQuery Search,
    string? Target = null,
    int? MinRank = null,
    int? MaxRank = null);

public sealed record InfluxPageSearchQuery(
    SearchQuery Search,
    string? Target = null);

public sealed record CompetitorResultRow(
    Guid CompetitorResultId,
    string Domain,
    decimal DuplicateRate,
    decimal EstimatedTraffic,
    decimal TrafficValue,
    int KeywordCount,
    int DuplicateKeywordCount,
    int CompetitorUniqueKeywordCount,
    int TargetUniqueKeywordCount,
    bool Saved,
    DateTime CreatedAt);

public sealed record InfluxKeywordResultRow(
    Guid InfluxKeywordResultId,
    Guid KeywordId,
    string Target,
    string Keyword,
    int Rank,
    string RankedUrl,
    decimal EstimatedTraffic,
    JsonElement Metrics,
    bool IsGap,
    string GapType,
    DateTime CreatedAt);

public sealed record InfluxPageResultRow(
    Guid InfluxPageResultId,
    string Target,
    string PageUrl,
    string Title,
    int KeywordCount,
    decimal EstimatedTraffic,
    decimal TrafficValue,
    Guid? TopKeywordId,
    string? TopKeyword,
    DateTime CreatedAt);

public sealed record OpportunityScoreRequest(IReadOnlyList<Guid> KeywordIds, string Location, string Language);

public sealed record OpportunityScoreResult(IReadOnlyList<OpportunityScoreRow> Scores);

public sealed record OpportunityScoreRow(Guid KeywordId, decimal Score, IReadOnlyDictionary<string, decimal> Components);

public sealed record DataExportRequest(
    string? ExportType,
    JsonElement? Filter = null,
    IReadOnlyList<string>? Columns = null);

public sealed record DataExportDetails(
    Guid ExportId,
    Guid? ProjectId,
    string ExportType,
    string Format,
    string Status,
    string? FileUri,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record DataExportDownload(
    Guid ExportId,
    string DownloadUrl,
    DateTime ExpiresAt);

public sealed record ExternalApiUsageQuery(DateOnly? From, DateOnly? To, string? Provider);

public sealed record ExternalApiUsageSummary(int CallCount, int ConsumedCredit, int RetryableFailureCount, int FatalFailureCount);

public sealed record NotificationRequest(
    string EventType,
    string? ResourceType,
    Guid? ResourceId,
    string Message,
    Guid? JobId = null,
    Guid? ChannelId = null);

public sealed record NotificationResult(Guid? DeliveryId, NotificationDeliveryStatus Status);

public sealed record DashboardSnapshot(
    int KeywordCandidateCount,
    int RunningJobCount,
    int FailedJobCount,
    int ConsumedCredit,
    int KeywordDiscoveryCount = 0,
    int SearchVolumeJobCount = 0,
    int SearchVolumeResultCount = 0,
    int OpportunityScoreCount = 0,
    IReadOnlyList<DashboardOpportunityScoreRow>? TopOpportunityScores = null,
    int NotificationFailureCount = 0);

public sealed record DashboardOpportunityScoreRow(
    Guid KeywordId,
    string Keyword,
    decimal OpportunityScore,
    string Location,
    string Language,
    DateTime ScoredAt);

public sealed record JobReference(Guid JobId, string Status);
