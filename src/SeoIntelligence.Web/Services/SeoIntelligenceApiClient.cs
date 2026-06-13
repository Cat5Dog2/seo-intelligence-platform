using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Web.Services;

public sealed partial class SeoIntelligenceApiClient : ISeoIntelligenceApiClient
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

    public Task<ApiClientResult<DashboardSnapshot>> GetDashboardAsync(Guid projectId, CancellationToken cancellationToken = default)
        => SendAsync<DashboardSnapshot>(HttpMethod.Get, $"/api/projects/{projectId:D}/dashboard", cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> GenerateTopicClustersAsync(
        Guid projectId,
        TopicClusterGenerateRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/clusters/generate", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<TopicClusterSummary>>> GetTopicClustersAsync(
        Guid projectId,
        string? q = null,
        string sortBy = "score",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        string? intentLabel = null,
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<TopicClusterSummary>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/clusters",
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize),
                ("intentLabel", intentLabel),
                ("parentId", parentId)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<TopicClusterDetails>> GetTopicClusterAsync(
        Guid projectId,
        Guid clusterId,
        CancellationToken cancellationToken = default)
        => SendAsync<TopicClusterDetails>(HttpMethod.Get, $"/api/projects/{projectId:D}/clusters/{clusterId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> AnalyzeCompetitorsAsync(
        Guid projectId,
        CompetitorAnalyzeRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/competitors/analyze", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<CompetitorResultRow>>> GetCompetitorsAsync(
        Guid projectId,
        string? q = null,
        string? domain = null,
        string sortBy = "duplicateRate",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompetitorResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/competitors",
                ("q", q),
                ("domain", domain),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<InfluxKeywordResultRow>>> GetInfluxKeywordsAsync(
        Guid projectId,
        string? q = null,
        string? target = null,
        int? minRank = null,
        int? maxRank = null,
        string sortBy = "rank",
        string orderBy = "asc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<InfluxKeywordResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/influx-keywords",
                ("q", q),
                ("target", target),
                ("minRank", minRank),
                ("maxRank", maxRank),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<InfluxPageResultRow>>> GetInfluxPagesAsync(
        Guid projectId,
        string? q = null,
        string? target = null,
        string sortBy = "estimatedTraffic",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<InfluxPageResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/influx-pages",
                ("q", q),
                ("target", target),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> AnalyzeContentAsync(
        Guid projectId,
        ContentAnalyzeRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/content/analyze", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ContentAnalysisResultRow>>> GetContentAnalysesAsync(
        Guid projectId,
        string? q = null,
        Guid? keywordId = null,
        string sortBy = "lastAnalyzedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ContentAnalysisResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/content-analyses",
                ("q", q),
                ("keywordId", keywordId),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> GenerateBriefAsync(
        Guid projectId,
        GenerateBriefRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/briefs/generate", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ArticleBriefSummary>>> GetBriefsAsync(
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
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ArticleBriefSummary>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/briefs",
                ("q", q),
                ("targetKeywordId", targetKeywordId),
                ("clusterId", clusterId),
                ("reviewStatus", reviewStatus),
                ("status", status),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<ArticleBriefDetails>> GetBriefAsync(
        Guid projectId,
        Guid briefId,
        CancellationToken cancellationToken = default)
        => SendAsync<ArticleBriefDetails>(HttpMethod.Get, $"/api/projects/{projectId:D}/briefs/{briefId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ArticleBriefDetails>> UpdateBriefAsync(
        Guid projectId,
        Guid briefId,
        ArticleBriefUpdateRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<ArticleBriefDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}/briefs/{briefId:D}", request, cancellationToken);

    public Task<ApiClientResult<AiChatResponse>> ChatWithAiAsync(
        Guid projectId,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<AiChatResponse>(HttpMethod.Post, $"/api/projects/{projectId:D}/ai/chat", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ArticleBriefVersionDetails>>> GetBriefVersionsAsync(
        Guid projectId,
        Guid briefId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ArticleBriefVersionDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/briefs/{briefId:D}/versions",
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> ExportBriefAsync(
        Guid projectId,
        Guid briefId,
        ArticleBriefExportRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/briefs/{briefId:D}/export", request, cancellationToken);

    public Task<ApiClientResult<JobReference>> RegisterRankCheckAsync(
        Guid projectId,
        RankCheckJobRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/rank-check/jobs", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<RankResultRow>>> GetRankCheckJobResultsAsync(
        Guid projectId,
        Guid jobId,
        string? q = null,
        string sortBy = "checkedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RankResultRow>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/rank-check/jobs/{jobId:D}/results",
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<RankResultList>> SearchRankResultsAsync(
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
        CancellationToken cancellationToken = default)
        => SendAsync<RankResultList>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/rank-results",
                ("q", q),
                ("jobId", jobId),
                ("keywordId", keywordId),
                ("target", target),
                ("minPosition", minPosition),
                ("maxPosition", maxPosition),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<RankAlertDetails>>> SearchRankAlertsAsync(
        Guid projectId,
        string status = "active",
        string? alertType = null,
        string? q = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RankAlertDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/alerts",
                ("status", status),
                ("alertType", alertType),
                ("q", q),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<RankAlertDetails>> CreateRankAlertAsync(
        Guid projectId,
        RankAlertCreateRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<RankAlertDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/alerts", request, cancellationToken);

    public Task<ApiClientResult<RankAlertDetails>> UpdateRankAlertAsync(
        Guid projectId,
        Guid alertId,
        RankAlertUpdateRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<RankAlertDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}/alerts/{alertId:D}", request, cancellationToken);

    public Task<ApiClientResult<RankAlertDetails>> DisableRankAlertAsync(
        Guid projectId,
        Guid alertId,
        CancellationToken cancellationToken = default)
        => SendAsync<RankAlertDetails>(HttpMethod.Delete, $"/api/projects/{projectId:D}/alerts/{alertId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<RankAlertDetails>> EnableRankAlertAsync(
        Guid projectId,
        Guid alertId,
        CancellationToken cancellationToken = default)
        => SendAsync<RankAlertDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/alerts/{alertId:D}/enable", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<RankAlertEventDetails>>> SearchRankAlertEventsAsync(
        Guid projectId,
        string? eventType = null,
        Guid? alertId = null,
        string? q = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RankAlertEventDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/alert-events",
                ("eventType", eventType),
                ("alertId", alertId),
                ("q", q),
                ("from", from),
                ("to", to),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<RewriteTaskDetails>>> SearchRewriteTasksAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "priorityScore",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<RewriteTaskDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/rewrite/tasks",
                ("status", status),
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<RewriteTaskDetails>> GetRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
        => SendAsync<RewriteTaskDetails>(HttpMethod.Get, $"/api/projects/{projectId:D}/rewrite/tasks/{taskId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<RewriteTaskDetails>> UpdateRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        RewriteTaskUpdateRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<RewriteTaskDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}/rewrite/tasks/{taskId:D}", request, cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<CannibalizationCandidateDetails>>> SearchCannibalizationCandidatesAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "severityScore",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CannibalizationCandidateDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/cannibalization/candidates",
                ("status", status),
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> RefreshCannibalizationAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/cannibalization/refresh", cancellationToken: cancellationToken);

    public Task<ApiClientResult<JobReference>> CreateReportAsync(
        Guid projectId,
        ReportRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<JobReference>(HttpMethod.Post, $"/api/projects/{projectId:D}/reports", request, cancellationToken);

    public Task<ApiClientResult<ReportDetails>> GetReportAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendAsync<ReportDetails>(HttpMethod.Get, $"/api/projects/{projectId:D}/reports/{reportId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ReportDownload>> CreateReportDownloadAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendAsync<ReportDownload>(HttpMethod.Get, $"/api/projects/{projectId:D}/reports/{reportId:D}/download", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ReportShareDetails>> ShareReportAsync(
        Guid projectId,
        Guid reportId,
        ReportShareRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<ReportShareDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/reports/{reportId:D}/share", request, cancellationToken);

    public Task<ApiClientResult<ReportShareDetails>> RevokeReportShareAsync(
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendAsync<ReportShareDetails>(HttpMethod.Delete, $"/api/projects/{projectId:D}/reports/{reportId:D}/share", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ConnectorSettingsDetails>>> SearchConnectorsAsync(
        Guid projectId,
        string status = "active",
        string? q = null,
        string sortBy = "updatedAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ConnectorSettingsDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/connectors",
                ("status", status),
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

    public Task<ApiClientResult<ConnectorSettingsDetails>> CreateConnectorAsync(
        Guid projectId,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<ConnectorSettingsDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/connectors", request, cancellationToken);

    public Task<ApiClientResult<ConnectorSettingsDetails>> UpdateConnectorAsync(
        Guid projectId,
        Guid connectorId,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<ConnectorSettingsDetails>(HttpMethod.Put, $"/api/projects/{projectId:D}/connectors/{connectorId:D}", request, cancellationToken);

    public Task<ApiClientResult<ConnectorSettingsDetails>> DisableConnectorAsync(
        Guid projectId,
        Guid connectorId,
        CancellationToken cancellationToken = default)
        => SendAsync<ConnectorSettingsDetails>(HttpMethod.Delete, $"/api/projects/{projectId:D}/connectors/{connectorId:D}", cancellationToken: cancellationToken);

    public Task<ApiClientResult<ConnectorRunDetails>> TestConnectorAsync(
        Guid projectId,
        Guid connectorId,
        CancellationToken cancellationToken = default)
        => SendAsync<ConnectorRunDetails>(HttpMethod.Post, $"/api/projects/{projectId:D}/connectors/{connectorId:D}/test", cancellationToken: cancellationToken);

    public Task<ApiClientResult<IReadOnlyList<ConnectorRunDetails>>> GetConnectorRunsAsync(
        Guid projectId,
        Guid connectorId,
        string status = "all",
        string? q = null,
        string sortBy = "createdAt",
        string orderBy = "desc",
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ConnectorRunDetails>>(
            HttpMethod.Get,
            WithQuery(
                $"/api/projects/{projectId:D}/connectors/{connectorId:D}/runs",
                ("status", status),
                ("q", q),
                ("sortBy", sortBy),
                ("orderBy", orderBy),
                ("page", page),
                ("pageSize", pageSize)),
            cancellationToken: cancellationToken);

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
