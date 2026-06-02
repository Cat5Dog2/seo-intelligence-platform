using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Web.Services;

public sealed class SeoIntelligenceApiClient : ISeoIntelligenceApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<SeoIntelligenceApiClient> _logger;

    public SeoIntelligenceApiClient(HttpClient httpClient, ILogger<SeoIntelligenceApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

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

    public Task<ApiClientResult<DashboardSnapshot>> GetDashboardAsync(Guid projectId, CancellationToken cancellationToken = default)
        => SendAsync<DashboardSnapshot>(HttpMethod.Get, $"/api/projects/{projectId:D}/dashboard", cancellationToken: cancellationToken);

    public Task<ApiClientResult<KeywordDiscoveryResult>> DiscoverKeywordsAsync(
        Guid projectId,
        KeywordDiscoveryRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<KeywordDiscoveryResult>(HttpMethod.Post, $"/api/projects/{projectId:D}/keyword-discovery/suggest", request, cancellationToken);

    public Task<ApiClientResult<JobReference>> RegisterSearchVolumeJobAsync(
        Guid projectId,
        SearchVolumeJobRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/search-volume/jobs", request, cancellationToken);

    public Task<ApiClientResult<JobReference>> GetSearchVolumeJobAsync(
        Guid projectId,
        Guid jobId,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Get, $"/api/projects/{projectId:D}/search-volume/jobs/{jobId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<SearchVolumeResultRow>>> GetSearchVolumeResultsAsync(
        Guid projectId,
        Guid jobId,
        string? q = null,
        string sortBy = "keyword",
        string orderBy = "asc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<SearchVolumeResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/search-volume/jobs/{jobId:D}/results",
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> CreateCsvExportAsync(
        Guid projectId,
        DataExportRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/exports/csv", request, cancellationToken);

    public Task<ApiClientResult<DataExportDetails>> GetExportAsync(
        Guid projectId,
        Guid exportId,
        CancellationToken cancellationToken = default)
        => SendAsync<DataExportDetails>(HttpMethod.Get, $"/api/projects/{projectId:D}/exports/{exportId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<DataExportDownload>> CreateExportDownloadAsync(
        Guid projectId,
        Guid exportId,
        CancellationToken cancellationToken = default)
        => SendAsync<DataExportDownload>(HttpMethod.Get, $"/api/projects/{projectId:D}/exports/{exportId:D}/download", cancellationToken: cancellationToken);

    private async Task<ApiClientResult<T>> SendAsync<T>(
        HttpMethod method,
        string requestUri,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return ApiClientResult<T>.Failure(
                    [new ApiError("Api.EmptyResponse", "API response body was empty.")],
                    statusCode: response.StatusCode);
            }

            var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<T>>(content, SerializerOptions);
            if (envelope is null)
            {
                return ApiClientResult<T>.Failure(
                    [new ApiError("Api.InvalidResponse", "API response envelope could not be parsed.")],
                    statusCode: response.StatusCode);
            }

            return response.IsSuccessStatusCode && envelope.Result
                ? ApiClientResult<T>.Success(envelope.Data, envelope.Meta, response.StatusCode, envelope.RequestId)
                : ApiClientResult<T>.Failure(envelope.Errors, envelope.Meta, response.StatusCode, envelope.RequestId);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "API request failed: {Method} {RequestUri}", method, requestUri);
            return ApiClientResult<T>.Failure([new ApiError("Api.RequestFailed", exception.Message)]);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "API response parsing failed: {Method} {RequestUri}", method, requestUri);
            return ApiClientResult<T>.Failure([new ApiError("Api.InvalidJson", "API response JSON could not be parsed.")]);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "API request timed out: {Method} {RequestUri}", method, requestUri);
            return ApiClientResult<T>.Failure([new ApiError("Api.Timeout", "API request timed out.")]);
        }
    }

    private static string WithQuery(string path, params (string Key, object? Value)[] parameters)
    {
        var query = parameters
            .Select(parameter => (parameter.Key, Value: FormatQueryValue(parameter.Value)))
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}")
            .ToArray();

        return query.Length == 0 ? path : $"{path}?{string.Join('&', query)}";
    }

    private static string? FormatQueryValue(object? value)
        => value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text,
            Guid guid when guid == Guid.Empty => null,
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            DateTimeOffset dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
}
