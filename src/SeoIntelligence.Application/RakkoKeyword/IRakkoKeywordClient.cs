using SeoIntelligence.Domain.Common;

namespace SeoIntelligence.Application.RakkoKeyword;

public interface IRakkoKeywordClient
{
    Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoSuggestKeywordsRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRelatedKeywordsRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoOtherKeywordsRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
        RakkoKeywordClientContext context,
        RakkoQuestionSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRankingKeywordsRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
        RakkoKeywordClientContext context,
        RakkoSearchVolumeRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
        RakkoKeywordClientContext context,
        long requestId,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
        RakkoKeywordClientContext context,
        long requestId,
        RakkoSearchVolumeResultsRequest request,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoLocationCatalog>> ListLocationsAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoLanguageCatalog>> ListLanguagesAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default);

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxKeywordsRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko influx keywords API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxPagesAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxPagesRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko influx pages API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCompetitiveSitesAsync(
        RakkoKeywordClientContext context,
        RakkoCompetitiveRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko competitive API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetContentSearchAsync(
        RakkoKeywordClientContext context,
        RakkoContentSearchRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko content search API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetHeadlinesAsync(
        RakkoKeywordClientContext context,
        RakkoHeadlineRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko headline API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCoOccurrencesAsync(
        RakkoKeywordClientContext context,
        RakkoCoOccurrenceRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko co-occurrence API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoSearchRankRegistration>> RegisterSearchRankAsync(
        RakkoKeywordClientContext context,
        RakkoSearchRankRegistrationRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko search rank register API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoSearchRankStatus>> GetSearchRankStatusAsync(
        RakkoKeywordClientContext context,
        string requestId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko search rank status API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetSearchRankResultsAsync(
        RakkoKeywordClientContext context,
        string requestId,
        RakkoSearchRankResultsRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko search rank results API is not implemented by this client.");

    Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetSearchRankSerpAsync(
        RakkoKeywordClientContext context,
        string requestId,
        int entryNo,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Phase 2 Rakko search rank SERP API is not implemented by this client.");
}

public sealed record RakkoKeywordClientContext(
    Guid WorkspaceId,
    Guid? ProjectId = null,
    Guid? JobId = null,
    Guid? ApiCredentialId = null,
    string? ApiKeySecretRef = null,
    Guid? ApiContractScopeId = null,
    string ContractScopeKey = "",
    string? CorrelationId = null,
    string Actor = SystemActor.Developer);

public sealed record RakkoSuggestKeywordsRequest(
    string Keyword,
    IReadOnlyList<string>? Modes = null,
    bool IncreaseKeyword = false,
    int? Limit = null,
    string SortBy = "searchVolume",
    string OrderBy = "desc");

public sealed record RakkoRelatedKeywordsRequest(
    string Keyword,
    string MatchType = "partialMatch",
    int? Limit = null,
    string SortBy = "searchVolume",
    string OrderBy = "desc");

public sealed record RakkoOtherKeywordsRequest(
    string Keyword,
    string SortBy = "importance",
    string OrderBy = "desc");

public sealed record RakkoQuestionSearchRequest(
    string Keyword,
    int? Limit = null,
    string SortBy = "relativeDemand",
    string OrderBy = "desc",
    IReadOnlyDictionary<string, object?>? Filter = null);

public sealed record RakkoRankingKeywordsRequest(
    string Keyword,
    int SearchTop = 20,
    int SearchRange = 50,
    int? Limit = null,
    string SortBy = "relevance",
    string OrderBy = "desc");

public sealed record RakkoSearchVolumeRegistrationRequest(
    IReadOnlyList<string> Keywords,
    bool SeoDifficulty = false,
    bool DataCompletion = true,
    string Location = "Japan",
    string Language = "Japanese",
    bool Deduplicate = true,
    int AggregationPeriodMonths = 12);

public sealed record RakkoSearchVolumeResultsRequest(
    bool NoiseReduction = true,
    int Limit = 100,
    string SortBy = "searchVolume",
    string OrderBy = "desc");

public sealed record RakkoApiTargetRequest(
    string Url,
    string MatchType = "domain");

public sealed record RakkoInfluxKeywordsRequest(
    IReadOnlyList<RakkoApiTargetRequest> Targets,
    bool KeywordCollapse = true,
    IReadOnlyDictionary<string, object?>? Filter = null,
    string SortBy = "estimatedTraffic",
    string OrderBy = "desc",
    int? Limit = 100);

public sealed record RakkoInfluxPagesRequest(
    IReadOnlyList<RakkoApiTargetRequest> Targets,
    bool TopKeywordCollapse = true,
    IReadOnlyDictionary<string, object?>? Filter = null,
    string SortBy = "estimatedTraffic",
    string OrderBy = "desc",
    int? Limit = 100);

public sealed record RakkoCompetitiveRequest(
    string Url,
    string SortBy = "duplicateRate",
    string OrderBy = "desc");

public sealed record RakkoContentSearchRequest(
    string Keyword,
    string SearchTarget = "google",
    bool IsAdvancedSearch = false,
    bool TopKeywordCollapse = true,
    IReadOnlyDictionary<string, object?>? Filter = null,
    string SortBy = "estimatedTraffic",
    string OrderBy = "desc",
    int? Limit = 100);

public sealed record RakkoHeadlineRequest(
    string Keyword,
    bool LessHeadlines = false,
    bool LessCharacters = false,
    bool H1 = true,
    bool H2 = true,
    bool H3 = true,
    bool H4 = true,
    bool H5 = true,
    bool H6 = true,
    string SortBy = "rank",
    string OrderBy = "asc",
    int? Limit = 10);

public sealed record RakkoCoOccurrenceRequest(
    string Keyword,
    bool GetDetails = true,
    string SortBy = "occurrencePageCount",
    string OrderBy = "desc",
    int? Limit = 100);

public sealed record RakkoSearchRankRegistrationRequest(
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Urls,
    string MatchType = "domain",
    int Depth = 100,
    bool WithMetrics = true,
    bool Deduplicate = true,
    string? Location = null,
    string? Language = null,
    string? Device = null,
    string? Os = null);

public sealed record RakkoSearchRankResultsRequest(
    IReadOnlyDictionary<string, object?>? Filter = null,
    string SortBy = "keyword",
    string OrderBy = "asc",
    int Limit = 100,
    bool WithAggregation = true);

public sealed record RakkoKeywordCallResult<T>(
    bool IsSuccess,
    T? Data,
    decimal ConsumedCredit,
    int StatusCode,
    IReadOnlyList<string> Errors,
    RakkoKeywordFailureKind FailureKind,
    RakkoKeywordExternalCallRecord ExternalCall)
{
    public bool IsRetryable => FailureKind == RakkoKeywordFailureKind.Retryable;

    public static RakkoKeywordCallResult<T> Success(
        T data,
        decimal consumedCredit,
        int statusCode,
        RakkoKeywordExternalCallRecord externalCall)
        => new(true, data, consumedCredit, statusCode, [], RakkoKeywordFailureKind.None, externalCall);

    public static RakkoKeywordCallResult<T> Failure(
        int statusCode,
        IReadOnlyList<string> errors,
        RakkoKeywordFailureKind failureKind,
        RakkoKeywordExternalCallRecord externalCall)
        => new(false, default, 0m, statusCode, errors, failureKind, externalCall);
}

public enum RakkoKeywordFailureKind
{
    None,
    Fatal,
    Retryable
}

public sealed record RakkoKeywordExternalCallRecord(
    Guid? CallId,
    string RequestHash,
    string RequestUri,
    string? ResponseHash,
    string? ResponseUri,
    bool CacheHit,
    string? ErrorCode);

public sealed record RakkoKeywordCandidates(
    string Source,
    IReadOnlyList<RakkoKeywordCandidate> Items);

public sealed record RakkoKeywordCandidate(
    string? Keyword,
    string Source,
    string? SuggestClass,
    string? Type,
    string? Question,
    string? Importance,
    string? SourceKeyword,
    decimal? WordCount,
    decimal? Relevance,
    RakkoKeywordMetrics? Metrics,
    IReadOnlyList<string> Engines);

public sealed record RakkoKeywordMetrics(
    decimal? SeoDifficulty,
    decimal? SearchVolume,
    decimal? Cpc,
    decimal? Competition,
    string? FirstSeenRange);

public sealed record RakkoQuestions(IReadOnlyList<RakkoQuestion> Items);

public sealed record RakkoQuestion(
    string Question,
    decimal? RelativeDemand = null,
    string? FirstSeenRange = null);

public sealed record RakkoSearchVolumeRegistration(long RequestId);

public sealed record RakkoSearchVolumeStatus(
    bool IsCompleted,
    IReadOnlyDictionary<string, string> Statuses);

public sealed record RakkoSearchRankRegistration(string RequestId);

public sealed record RakkoSearchRankStatus(
    bool IsCompleted,
    IReadOnlyDictionary<string, string> Statuses);

public sealed record RakkoSearchVolumeResults(
    IReadOnlyList<RakkoSearchVolumeResultItem> Items);

public sealed record RakkoExternalSearchResults(
    string Source,
    IReadOnlyList<RakkoExternalSearchResultItem> Items,
    string? QueryJson,
    string? SummaryJson);

public sealed record RakkoExternalSearchResultItem(
    string? Keyword,
    string? Target,
    string? Url,
    string? Domain,
    string? Title,
    decimal? Position,
    decimal? EstimatedTraffic,
    decimal? TrafficValue,
    string RawJson,
    decimal? EntryNo = null);

public sealed record RakkoSearchVolumeResultItem(
    string Keyword,
    string? DataSource,
    RakkoKeywordMetrics Metrics,
    IReadOnlyDictionary<string, int> MonthlySearchVolume);

public sealed record RakkoLocationCatalog(IReadOnlyList<RakkoLocation> Locations);

public sealed record RakkoLocation(
    string Name,
    string CountryIsoCode);

public sealed record RakkoLanguageCatalog(IReadOnlyList<RakkoLanguage> Languages);

public sealed record RakkoLanguage(string Name);
