using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Infrastructure.RakkoKeyword.Generated;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal sealed class RakkoKeywordRealClient(
    HttpClient httpClient,
    ISecretStore secretStore,
    IRakkoKeywordCallRecorder recorder,
    IOptions<RakkoKeywordOptions> options,
    ILogger<RakkoKeywordRealClient> logger)
    : IRakkoKeywordClient
{
    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoSuggestKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SuggestKeywordsDto, SuggestKeywordsResponseDto, RakkoKeywordCandidates>(
            context,
            RakkoKeywordClientSupport.SuggestKeywordsEndpoint,
            RakkoKeywordClientSupport.SuggestKeywordsEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRelatedKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<RelatedKeywordsDto, RelatedKeywordsResponseDto, RakkoKeywordCandidates>(
            context,
            RakkoKeywordClientSupport.RelatedKeywordsEndpoint,
            RakkoKeywordClientSupport.RelatedKeywordsEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoOtherKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<OtherKeywordsDto, OtherKeywordsResponseDto, RakkoKeywordCandidates>(
            context,
            RakkoKeywordClientSupport.OtherKeywordsEndpoint,
            RakkoKeywordClientSupport.OtherKeywordsEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
        RakkoKeywordClientContext context,
        RakkoQuestionSearchRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SearchQuestionDto, SearchQuestionResponseDto, RakkoQuestions>(
            context,
            RakkoKeywordClientSupport.QuestionSearchEndpoint,
            RakkoKeywordClientSupport.QuestionSearchEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRankingKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<RankingKeywordsDto, RankingKeywordsResponseDto, RakkoKeywordCandidates>(
            context,
            RakkoKeywordClientSupport.RankingKeywordsEndpoint,
            RakkoKeywordClientSupport.RankingKeywordsEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
        RakkoKeywordClientContext context,
        RakkoSearchVolumeRegistrationRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SearchVolumeHistoryDto, SearchVolumeHistoryResponseDto, RakkoSearchVolumeRegistration>(
            context,
            RakkoKeywordClientSupport.SearchVolumeEndpoint,
            RakkoKeywordClientSupport.SearchVolumeEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
        RakkoKeywordClientContext context,
        long requestId,
        CancellationToken cancellationToken = default)
        => SendAsync<object, SearchVolumeStatusResponseDto, RakkoSearchVolumeStatus>(
            context,
            RakkoKeywordClientSupport.SearchVolumeStatusEndpoint,
            RakkoKeywordClientSupport.SearchVolumeStatusPath(requestId),
            HttpMethod.Get,
            requestBody: null,
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
        RakkoKeywordClientContext context,
        long requestId,
        RakkoSearchVolumeResultsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SearchVolumeResultsDto, SearchVolumeResultsResponseDto, RakkoSearchVolumeResults>(
            context,
            RakkoKeywordClientSupport.SearchVolumeResultsEndpoint,
            RakkoKeywordClientSupport.SearchVolumeResultsPath(requestId),
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoLocationCatalog>> ListLocationsAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default)
        => SendAsync<object, MetadataLocationsResponseDto, RakkoLocationCatalog>(
            context,
            RakkoKeywordClientSupport.LocationsEndpoint,
            RakkoKeywordClientSupport.LocationsEndpoint,
            HttpMethod.Get,
            requestBody: null,
            requiresApiKey: false,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoLanguageCatalog>> ListLanguagesAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default)
        => SendAsync<object, MetadataLanguagesResponseDto, RakkoLanguageCatalog>(
            context,
            RakkoKeywordClientSupport.LanguagesEndpoint,
            RakkoKeywordClientSupport.LanguagesEndpoint,
            HttpMethod.Get,
            requestBody: null,
            requiresApiKey: false,
            useLongTimeout: false,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<InfluxKeywordsKeywordDto, InfluxKeywordsKeywordResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.InfluxKeywordsEndpoint,
            RakkoKeywordClientSupport.InfluxKeywordsEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxPagesAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxPagesRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<InfluxPagesDto, InfluxPagesResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.InfluxPagesEndpoint,
            RakkoKeywordClientSupport.InfluxPagesEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCompetitiveSitesAsync(
        RakkoKeywordClientContext context,
        RakkoCompetitiveRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CompetitiveDto, CompetitiveResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.CompetitiveEndpoint,
            RakkoKeywordClientSupport.CompetitiveEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetContentSearchAsync(
        RakkoKeywordClientContext context,
        RakkoContentSearchRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<ContentSearchDto, ContentSearchResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.ContentSearchEndpoint,
            RakkoKeywordClientSupport.ContentSearchEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetHeadlinesAsync(
        RakkoKeywordClientContext context,
        RakkoHeadlineRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<HeadlineDto, HeadlineResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.HeadlineEndpoint,
            RakkoKeywordClientSupport.HeadlineEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCoOccurrencesAsync(
        RakkoKeywordClientContext context,
        RakkoCoOccurrenceRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CoOccurrenceDto, CoOccurrenceResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.CoOccurrenceEndpoint,
            RakkoKeywordClientSupport.CoOccurrenceEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoSearchRankRegistration>> RegisterSearchRankAsync(
        RakkoKeywordClientContext context,
        RakkoSearchRankRegistrationRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SearchRankHistoryDto, SearchRankHistoryResponseDto, RakkoSearchRankRegistration>(
            context,
            RakkoKeywordClientSupport.SearchRankEndpoint,
            RakkoKeywordClientSupport.SearchRankEndpoint,
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoSearchRankStatus>> GetSearchRankStatusAsync(
        RakkoKeywordClientContext context,
        string requestId,
        CancellationToken cancellationToken = default)
        => SendAsync<object, SearchRankStatusResponseDto, RakkoSearchRankStatus>(
            context,
            RakkoKeywordClientSupport.SearchRankStatusEndpoint,
            RakkoKeywordClientSupport.SearchRankStatusPath(requestId),
            HttpMethod.Get,
            requestBody: null,
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetSearchRankResultsAsync(
        RakkoKeywordClientContext context,
        string requestId,
        RakkoSearchRankResultsRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SearchRankResultsDto, SearchRankResultsResponseDto, RakkoExternalSearchResults>(
            context,
            RakkoKeywordClientSupport.SearchRankResultsEndpoint,
            RakkoKeywordClientSupport.SearchRankResultsPath(requestId),
            HttpMethod.Post,
            RakkoKeywordDtoMapper.ToDto(request),
            requiresApiKey: true,
            useLongTimeout: true,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);

    private async Task<RakkoKeywordCallResult<TApplication>> SendAsync<TRequest, TResponse, TApplication>(
        RakkoKeywordClientContext context,
        string endpoint,
        string path,
        HttpMethod method,
        TRequest? requestBody,
        bool requiresApiKey,
        bool useLongTimeout,
        Func<TResponse, TApplication> map,
        CancellationToken cancellationToken)
        where TResponse : class, IRakkoKeywordResponseDto
    {
        var stopwatch = Stopwatch.StartNew();

        if (requiresApiKey)
        {
            var secretReference = new SecretReference(context.ApiKeySecretRef ?? options.Value.ApiKeySecretRef);
            var secret = await secretStore.GetAsync(secretReference, cancellationToken);
            if (secret is null)
            {
                return await CompleteFailureAsync<TApplication>(
                    context,
                    endpoint,
                    method.Method,
                    requestBody,
                    statusCode: 403,
                    ["Rakko Keyword API key secret is unavailable."],
                    "secret_unavailable",
                    stopwatch,
                    responseBody: null,
                    cancellationToken);
            }

            using var httpRequest = CreateHttpRequest(method, path, requestBody);
            httpRequest.Headers.Add("X-API-Key", secret.Value);
            return await SendHttpRequestAsync(context, endpoint, method, requestBody, httpRequest, useLongTimeout, map, stopwatch, cancellationToken);
        }

        using var unauthenticatedRequest = CreateHttpRequest(method, path, requestBody);
        return await SendHttpRequestAsync(context, endpoint, method, requestBody, unauthenticatedRequest, useLongTimeout, map, stopwatch, cancellationToken);
    }

    private async Task<RakkoKeywordCallResult<TApplication>> SendHttpRequestAsync<TRequest, TResponse, TApplication>(
        RakkoKeywordClientContext context,
        string endpoint,
        HttpMethod method,
        TRequest? requestBody,
        HttpRequestMessage httpRequest,
        bool useLongTimeout,
        Func<TResponse, TApplication> map,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
        where TResponse : class, IRakkoKeywordResponseDto
    {
        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", context.CorrelationId);
        }

        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.UserAgent.ParseAdd(BuildUserAgent());

        // 監査(external_api_calls)は実際の通信結果を正本として残す。
        // レスポンス受信後に解析・変換で失敗した場合も、外部APIが返したHTTPステータスと
        // 消費クレジットを記録し、内部的な失敗分類とは切り離す。
        byte[]? responseBytes = null;
        int? httpStatusCode = null;
        var consumedCredit = 0m;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(useLongTimeout ? options.Value.LongTimeoutSeconds : options.Value.TimeoutSeconds));

            using var response = await httpClient.SendAsync(httpRequest, timeoutCts.Token);
            responseBytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
            stopwatch.Stop();
            httpStatusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var errors = DeserializeErrors(responseBytes);
                return await CompleteFailureAsync<TApplication>(
                    context,
                    endpoint,
                    method.Method,
                    requestBody,
                    (int)response.StatusCode,
                    errors,
                    RakkoKeywordClientSupport.ToErrorCode((int)response.StatusCode),
                    stopwatch,
                    responseBytes,
                    cancellationToken);
            }

            var responseDto = JsonSerializer.Deserialize<TResponse>(responseBytes, RakkoKeywordJson.SerializerOptions);
            if (responseDto is null)
            {
                return await CompleteFailureAsync<TApplication>(
                    context,
                    endpoint,
                    method.Method,
                    requestBody,
                    statusCode: 500,
                    ["Rakko Keyword API returned an empty or invalid response."],
                    "invalid_response",
                    stopwatch,
                    responseBytes,
                    cancellationToken,
                    httpStatusCode);
            }

            consumedCredit = responseDto.Meta.ConsumedCredit;
            var applicationData = map(responseDto);
            var externalCall = await recorder.RecordAsync(
                new RakkoKeywordCallRecordRequest(
                    context,
                    endpoint,
                    method.Method,
                    requestBody,
                    responseBytes,
                    (int)response.StatusCode,
                    responseDto.Meta.ConsumedCredit,
                    Convert.ToInt32(stopwatch.ElapsedMilliseconds),
                    CacheHit: false,
                    ErrorCode: null),
                cancellationToken);

            return RakkoKeywordCallResult<TApplication>.Success(
                applicationData,
                responseDto.Meta.ConsumedCredit,
                (int)response.StatusCode,
                externalCall);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "Rakko Keyword API request timed out for {endpoint} with correlation_id {correlation_id}.",
                endpoint,
                context.CorrelationId);

            return await CompleteFailureAsync<TApplication>(
                context,
                endpoint,
                method.Method,
                requestBody,
                statusCode: 503,
                ["Rakko Keyword API request timed out."],
                "timeout",
                stopwatch,
                responseBody: null,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(
                exception,
                "Rakko Keyword API returned an invalid response for {endpoint} with correlation_id {correlation_id}.",
                endpoint,
                context.CorrelationId);

            return await CompleteFailureAsync<TApplication>(
                context,
                endpoint,
                method.Method,
                requestBody,
                statusCode: 500,
                ["Rakko Keyword API returned an invalid response."],
                "invalid_response",
                stopwatch,
                responseBytes,
                cancellationToken,
                httpStatusCode,
                consumedCredit);
        }
    }

    private HttpRequestMessage CreateHttpRequest<TRequest>(
        HttpMethod method,
        string path,
        TRequest? requestBody)
    {
        var baseUri = httpClient.BaseAddress ?? new Uri(options.Value.BaseUrl, UriKind.Absolute);
        var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        if (requestBody is not null)
        {
            var json = JsonSerializer.Serialize(requestBody, RakkoKeywordJson.SerializerOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task<RakkoKeywordCallResult<TApplication>> CompleteFailureAsync<TApplication>(
        RakkoKeywordClientContext context,
        string endpoint,
        string method,
        object? requestBody,
        int statusCode,
        IReadOnlyList<string> errors,
        string errorCode,
        Stopwatch stopwatch,
        byte[]? responseBody,
        CancellationToken cancellationToken,
        int? auditStatusCode = null,
        decimal consumedCredit = 0m)
    {
        // statusCodeは内部の失敗分類(再試行可否)に使い、監査には外部APIが返した
        // 実際のHTTPステータスと消費クレジットを残す。通信前に失敗した場合は両者が一致する。
        var externalCall = await recorder.RecordAsync(
            new RakkoKeywordCallRecordRequest(
                context,
                endpoint,
                method,
                requestBody,
                responseBody,
                auditStatusCode ?? statusCode,
                consumedCredit,
                Convert.ToInt32(stopwatch.ElapsedMilliseconds),
                CacheHit: false,
                errorCode),
            cancellationToken);

        return RakkoKeywordCallResult<TApplication>.Failure(
            statusCode,
            errors,
            RakkoKeywordClientSupport.ToFailureKind(statusCode),
            externalCall);
    }

    private static IReadOnlyList<string> DeserializeErrors(byte[] responseBytes)
    {
        if (responseBytes.Length == 0)
        {
            return ["Rakko Keyword API returned an error response."];
        }

        try
        {
            var errorResponse = JsonSerializer.Deserialize<RakkoKeywordErrorResponseDto>(
                responseBytes,
                RakkoKeywordJson.SerializerOptions);
            return errorResponse?.Errors.Count > 0
                ? errorResponse.Errors
                : ["Rakko Keyword API returned an error response."];
        }
        catch (JsonException)
        {
            return ["Rakko Keyword API returned an invalid error response."];
        }
    }

    private string BuildUserAgent()
        => $"{options.Value.UserAgentProduct}/{options.Value.UserAgentVersion} ({options.Value.EnvironmentName})";
}
