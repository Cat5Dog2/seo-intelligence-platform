using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public interface ISeoIntelligenceApiClient
{
    Task<ApiClientResult<WorkspaceDetails>> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    Task<ApiClientResult<WorkspaceDetails>> UpdateWorkspaceAsync(WorkspaceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ProjectDetails>>> SearchProjectsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ProjectDetails>> CreateProjectAsync(ProjectCreateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ProjectDetails>> UpdateProjectAsync(Guid projectId, ProjectUpdateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ProjectDetails>> ArchiveProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ProjectDetails>> RestoreProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<SiteDetails>>> SearchSitesAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<SiteDetails>> CreateSiteAsync(Guid projectId, SiteCreateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<SiteDetails>> UpdateSiteAsync(Guid projectId, Guid siteId, SiteUpdateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<SiteDetails>> ArchiveSiteAsync(Guid projectId, Guid siteId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<SiteDetails>> RestoreSiteAsync(Guid projectId, Guid siteId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ApiCredentialDetails>>> SearchApiCredentialsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ApiCredentialDetails>> CreateApiCredentialAsync(ApiCredentialCreateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ApiCredentialDetails>> UpdateApiCredentialAsync(Guid credentialId, ApiCredentialUpdateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ApiCredentialDetails>> DisableApiCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ApiCredentialDetails>> EnableApiCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<ApiCredentialDetails>> RotateApiCredentialAsync(Guid credentialId, ApiCredentialRotateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<NotificationChannelDetails>>> SearchNotificationChannelsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationChannelDetails>> CreateNotificationChannelAsync(NotificationChannelCreateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationChannelDetails>> UpdateNotificationChannelAsync(Guid channelId, NotificationChannelUpdateRequest request, CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationChannelDetails>> DisableNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationChannelDetails>> EnableNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationDeliveryDetails>> TestNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<NotificationDeliveryDetails>>> SearchNotificationDeliveriesAsync(
        string status = "all",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<NotificationDeliveryDetails>> RetryNotificationDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ExternalApiCallDetails>>> SearchExternalApiCallsAsync(
        string? q = null,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<AuditLogDetails>>> SearchAuditLogsAsync(
        AuditLogSearchParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<JobDetails>>> SearchJobsAsync(
        string status = "all",
        string? jobType = null,
        Guid? projectId = null,
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobDetails>> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobDetails>> RetryJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobDetails>> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<LocationSummary>>> ListLocationsAsync(CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(CancellationToken cancellationToken = default);

    Task<ApiClientResult<DashboardSnapshot>> GetDashboardAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> GenerateTopicClustersAsync(
        Guid projectId,
        TopicClusterGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<TopicClusterSummary>>> GetTopicClustersAsync(
        Guid projectId,
        string? q = null,
        string sortBy = "score",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        string? intentLabel = null,
        Guid? parentId = null,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<TopicClusterDetails>> GetTopicClusterAsync(
        Guid projectId,
        Guid clusterId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> AnalyzeCompetitorsAsync(
        Guid projectId,
        CompetitorAnalyzeRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<CompetitorResultRow>>> GetCompetitorsAsync(
        Guid projectId,
        string? q = null,
        string? domain = null,
        string sortBy = "duplicateRate",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<InfluxKeywordResultRow>>> GetInfluxKeywordsAsync(
        Guid projectId,
        string? q = null,
        string? target = null,
        int? minRank = null,
        int? maxRank = null,
        string sortBy = "rank",
        string orderBy = "asc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<InfluxPageResultRow>>> GetInfluxPagesAsync(
        Guid projectId,
        string? q = null,
        string? target = null,
        string sortBy = "estimatedTraffic",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> AnalyzeContentAsync(
        Guid projectId,
        ContentAnalyzeRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ContentAnalysisResultRow>>> GetContentAnalysesAsync(
        Guid projectId,
        string? q = null,
        Guid? keywordId = null,
        string sortBy = "lastAnalyzedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> GenerateBriefAsync(
        Guid projectId,
        GenerateBriefRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ArticleBriefSummary>>> GetBriefsAsync(
        Guid projectId,
        string? q = null,
        Guid? targetKeywordId = null,
        Guid? clusterId = null,
        string? reviewStatus = null,
        string status = "all",
        string sortBy = "updatedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ArticleBriefDetails>> GetBriefAsync(
        Guid projectId,
        Guid briefId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ArticleBriefDetails>> UpdateBriefAsync(
        Guid projectId,
        Guid briefId,
        ArticleBriefUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<AiChatResponse>> ChatWithAiAsync(
        Guid projectId,
        AiChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ArticleBriefVersionDetails>>> GetBriefVersionsAsync(
        Guid projectId,
        Guid briefId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> ExportBriefAsync(
        Guid projectId,
        Guid briefId,
        ArticleBriefExportRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> RegisterRankCheckAsync(
        Guid projectId,
        RankCheckJobRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<RankResultRow>>> GetRankCheckJobResultsAsync(
        Guid projectId,
        Guid jobId,
        string? q = null,
        string sortBy = "checkedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RankResultList>> SearchRankResultsAsync(
        Guid projectId,
        string? q = null,
        Guid? jobId = null,
        Guid? keywordId = null,
        string? target = null,
        int? minPosition = null,
        int? maxPosition = null,
        string sortBy = "checkedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<RankAlertDetails>>> SearchRankAlertsAsync(
        Guid projectId,
        string status = "active",
        string? alertType = null,
        string? q = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RankAlertDetails>> CreateRankAlertAsync(
        Guid projectId,
        RankAlertCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RankAlertDetails>> UpdateRankAlertAsync(
        Guid projectId,
        Guid alertId,
        RankAlertUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RankAlertDetails>> DisableRankAlertAsync(
        Guid projectId,
        Guid alertId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RankAlertDetails>> EnableRankAlertAsync(
        Guid projectId,
        Guid alertId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<RankAlertEventDetails>>> SearchRankAlertEventsAsync(
        Guid projectId,
        string? eventType = null,
        Guid? alertId = null,
        string? q = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<RewriteTaskDetails>>> SearchRewriteTasksAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "priorityScore",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RewriteTaskDetails>> GetRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<RewriteTaskDetails>> UpdateRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        RewriteTaskUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<CannibalizationCandidateDetails>>> SearchCannibalizationCandidatesAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "severityScore",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> RefreshCannibalizationAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> CreateReportAsync(
        Guid projectId,
        ReportRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ReportDetails>> GetReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ReportDownload>> CreateReportDownloadAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ReportShareDetails>> ShareReportAsync(
        Guid projectId,
        Guid reportId,
        ReportShareRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ReportShareDetails>> RevokeReportShareAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ConnectorSettingsDetails>>> SearchConnectorsAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "updatedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ConnectorSettingsDetails>> CreateConnectorAsync(
        Guid projectId,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ConnectorSettingsDetails>> UpdateConnectorAsync(
        Guid projectId,
        Guid connectorId,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ConnectorSettingsDetails>> DisableConnectorAsync(
        Guid projectId,
        Guid connectorId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<ConnectorRunDetails>> TestConnectorAsync(
        Guid projectId,
        Guid connectorId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<ConnectorRunDetails>>> GetConnectorRunsAsync(
        Guid projectId,
        Guid connectorId,
        string status = "all",
        string? q = null,
        string sortBy = "createdAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<KeywordDiscoveryResult>> DiscoverKeywordsAsync(
        Guid projectId,
        KeywordDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> RegisterSearchVolumeJobAsync(
        Guid projectId,
        SearchVolumeJobRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> GetSearchVolumeJobAsync(
        Guid projectId,
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<SearchVolumeResultRow>>> GetSearchVolumeResultsAsync(
        Guid projectId,
        Guid jobId,
        string? q = null,
        string sortBy = "keyword",
        string orderBy = "asc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<JobReference>> CreateCsvExportAsync(
        Guid projectId,
        DataExportRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<DataExportDetails>> GetExportAsync(
        Guid projectId,
        Guid exportId,
        CancellationToken cancellationToken = default);

    Task<ApiClientResult<DataExportDownload>> CreateExportDownloadAsync(
        Guid projectId,
        Guid exportId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the generated export file. The caller owns the returned response and must dispose
    /// it: the body is still being streamed from the API when this returns.
    /// </summary>
    Task<ApiClientResult<ApiFileResponse>> DownloadExportAsync(
        Guid projectId,
        Guid exportId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the generated report file. The caller owns the returned response and must dispose
    /// it: the body is still being streamed from the API when this returns.
    /// </summary>
    Task<ApiClientResult<ApiFileResponse>> DownloadReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A file streamed straight from the API. <see cref="Response"/> is kept so the caller can hold
/// the connection open while copying; disposing it releases both the response and
/// <see cref="Content"/>.
/// </summary>
public sealed record ApiFileResponse(
    HttpResponseMessage Response,
    Stream Content,
    string ContentType,
    string FileName) : IDisposable
{
    public void Dispose() => Response.Dispose();
}

public sealed record AuditLogSearchParameters(
    string? Q = null,
    string? Actor = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? CorrelationId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);
