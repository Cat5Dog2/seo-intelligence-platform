using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed partial class SeoIntelligenceApiClient
{
    public Task<ApiClientResult<WorkspaceDetails>> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => SendAsync<WorkspaceDetails>(HttpMethod.Get, "/api/admin/workspace", cancellationToken: cancellationToken);

    public Task<ApiClientResult<WorkspaceDetails>> UpdateWorkspaceAsync(WorkspaceUpdateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<WorkspaceDetails>(HttpMethod.Put, "/api/admin/workspace", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ProjectDetails>>> SearchProjectsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ProjectDetails>>(
            HttpMethod.Get,
            WithQuery("/api/projects", ("status", status), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<ProjectDetails>> CreateProjectAsync(ProjectCreateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ProjectDetails>(HttpMethod.Post, "/api/projects", request, cancellationToken);

    public Task<ApiClientResult<ProjectDetails>> UpdateProjectAsync(Guid projectId, ProjectUpdateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ProjectDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}", request, cancellationToken);

    public Task<ApiClientResult<ProjectDetails>> ArchiveProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => SendAsync<ProjectDetails>(HttpMethod.Delete, $"/api/projects/{projectId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ProjectDetails>> RestoreProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => SendAsync<ProjectDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/restore", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<SiteDetails>>> SearchSitesAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<SiteDetails>>(
            HttpMethod.Get,
            WithQuery($"/api/projects/{projectId:D}/sites", ("status", status), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<SiteDetails>> CreateSiteAsync(Guid projectId, SiteCreateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<SiteDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/sites", request, cancellationToken);

    public Task<ApiClientResult<SiteDetails>> UpdateSiteAsync(Guid projectId, Guid siteId, SiteUpdateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<SiteDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}/sites/{siteId:D}", request, cancellationToken);

    public Task<ApiClientResult<SiteDetails>> ArchiveSiteAsync(Guid projectId, Guid siteId, CancellationToken cancellationToken = default)
        => SendAsync<SiteDetails>(HttpMethod.Delete, $"/api/projects/{projectId:D}/sites/{siteId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<SiteDetails>> RestoreSiteAsync(Guid projectId, Guid siteId, CancellationToken cancellationToken = default)
        => SendAsync<SiteDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/sites/{siteId:D}/restore", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ApiCredentialDetails>>> SearchApiCredentialsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ApiCredentialDetails>>(
            HttpMethod.Get,
            WithQuery("/api/admin/api-credentials", ("status", status), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<ApiCredentialDetails>> CreateApiCredentialAsync(ApiCredentialCreateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ApiCredentialDetails>(HttpMethod.Post, "/api/admin/api-credentials", request, cancellationToken);

    public Task<ApiClientResult<ApiCredentialDetails>> UpdateApiCredentialAsync(Guid credentialId, ApiCredentialUpdateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ApiCredentialDetails>(HttpMethod.Put, $"/api/admin/api-credentials/{credentialId:D}", request, cancellationToken);

    public Task<ApiClientResult<ApiCredentialDetails>> DisableApiCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default)
        => SendAsync<ApiCredentialDetails>(HttpMethod.Delete, $"/api/admin/api-credentials/{credentialId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ApiCredentialDetails>> EnableApiCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default)
        => SendAsync<ApiCredentialDetails>(HttpMethod.Post, $"/api/admin/api-credentials/{credentialId:D}/enable", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ApiCredentialDetails>> RotateApiCredentialAsync(Guid credentialId, ApiCredentialRotateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<ApiCredentialDetails>(HttpMethod.Post, $"/api/admin/api-credentials/{credentialId:D}/rotate", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<NotificationChannelDetails>>> SearchNotificationChannelsAsync(
        string status = "active",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<NotificationChannelDetails>>(
            HttpMethod.Get,
            WithQuery("/api/admin/notification-channels", ("status", status), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<NotificationChannelDetails>> CreateNotificationChannelAsync(NotificationChannelCreateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<NotificationChannelDetails>(HttpMethod.Post, "/api/admin/notification-channels", request, cancellationToken);

    public Task<ApiClientResult<NotificationChannelDetails>> UpdateNotificationChannelAsync(Guid channelId, NotificationChannelUpdateRequest request, CancellationToken cancellationToken = default)
        => SendAsync<NotificationChannelDetails>(HttpMethod.Put, $"/api/admin/notification-channels/{channelId:D}", request, cancellationToken);

    public Task<ApiClientResult<NotificationChannelDetails>> DisableNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
        => SendAsync<NotificationChannelDetails>(HttpMethod.Delete, $"/api/admin/notification-channels/{channelId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<NotificationChannelDetails>> EnableNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
        => SendAsync<NotificationChannelDetails>(HttpMethod.Post, $"/api/admin/notification-channels/{channelId:D}/enable", cancellationToken: cancellationToken);

    public Task<ApiClientResult<NotificationDeliveryDetails>> TestNotificationChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
        => SendAsync<NotificationDeliveryDetails>(HttpMethod.Post, $"/api/admin/notification-channels/{channelId:D}/test", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<NotificationDeliveryDetails>>> SearchNotificationDeliveriesAsync(
        string status = "all",
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<NotificationDeliveryDetails>>(
            HttpMethod.Get,
            WithQuery("/api/admin/notification-deliveries", ("status", status), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<NotificationDeliveryDetails>> RetryNotificationDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        => SendAsync<NotificationDeliveryDetails>(HttpMethod.Post, $"/api/admin/notification-deliveries/{deliveryId:D}/retry", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ExternalApiCallDetails>>> SearchExternalApiCallsAsync(
        string? q = null,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ExternalApiCallDetails>>(
            HttpMethod.Get,
            WithQuery("/api/admin/external-api-calls", ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<AuditLogDetails>>> SearchAuditLogsAsync(
        AuditLogSearchParameters parameters,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<AuditLogDetails>>(
            HttpMethod.Get,
            WithQuery(
                "/api/admin/audit-logs",
                ("q", parameters.Q),
                ("actor", parameters.Actor),
                ("resourceType", parameters.ResourceType),
                ("resourceId", parameters.ResourceId),
                ("correlation_id", parameters.CorrelationId),
                ("from", parameters.From),
                ("to", parameters.To),
                ("page", parameters.Page),
                ("pageSize", parameters.PageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<JobDetails>>> SearchJobsAsync(
        string status = "all",
        string? jobType = null,
        Guid? projectId = null,
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<JobDetails>>(
            HttpMethod.Get,
            WithQuery("/api/jobs", ("status", status), ("job_type", jobType), ("project_id", projectId), ("q", q), ("page", page), ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobDetails>> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => SendAsync<JobDetails>(HttpMethod.Post, $"/api/jobs/{jobId:D}/cancel", cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobDetails>> RetryJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => SendAsync<JobDetails>(HttpMethod.Post, $"/api/jobs/{jobId:D}/retry", cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobDetails>> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => SendAsync<JobDetails>(HttpMethod.Get, $"/api/jobs/{jobId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<LocationSummary>>> ListLocationsAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<LocationSummary>>(HttpMethod.Get, "/api/master-data/locations", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<LanguageSummary>>(HttpMethod.Get, "/api/master-data/languages", cancellationToken: cancellationToken);
}
