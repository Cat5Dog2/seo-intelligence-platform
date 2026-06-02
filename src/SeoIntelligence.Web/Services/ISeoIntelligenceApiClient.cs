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

    Task<ApiClientResult<IReadOnlyList<LocationSummary>>> ListLocationsAsync(CancellationToken cancellationToken = default);

    Task<ApiClientResult<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(CancellationToken cancellationToken = default);
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
