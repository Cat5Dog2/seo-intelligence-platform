using System.Globalization;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Infrastructure.RakkoKeyword.Generated;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal static class RakkoKeywordDtoMapper
{
    public static SuggestKeywordsDto ToDto(RakkoSuggestKeywordsRequest request)
        => new()
        {
            Keyword = request.Keyword,
            Modes = request.Modes,
            IncreaseKeyword = request.IncreaseKeyword,
            Limit = request.Limit,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static RelatedKeywordsDto ToDto(RakkoRelatedKeywordsRequest request)
        => new()
        {
            Keyword = request.Keyword,
            MatchType = request.MatchType,
            Limit = request.Limit,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static OtherKeywordsDto ToDto(RakkoOtherKeywordsRequest request)
        => new()
        {
            Keyword = request.Keyword,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static SearchQuestionDto ToDto(RakkoQuestionSearchRequest request)
        => new()
        {
            Keyword = request.Keyword,
            Limit = request.Limit
        };

    public static RankingKeywordsDto ToDto(RakkoRankingKeywordsRequest request)
        => new()
        {
            Keyword = request.Keyword,
            SearchTop = request.SearchTop,
            SearchRange = request.SearchRange,
            Limit = request.Limit,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static SearchVolumeHistoryDto ToDto(RakkoSearchVolumeRegistrationRequest request)
        => new()
        {
            Keywords = request.Keywords,
            SeoDifficulty = request.SeoDifficulty,
            DataCompletion = request.DataCompletion,
            Location = request.Location,
            Language = request.Language,
            Deduplicate = request.Deduplicate,
            AggregationPeriodMonths = request.AggregationPeriodMonths
        };

    public static SearchVolumeResultsDto ToDto(RakkoSearchVolumeResultsRequest request)
        => new()
        {
            NoiseReduction = request.NoiseReduction,
            Limit = request.Limit,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static RakkoKeywordCandidates ToApplication(SuggestKeywordsResponseDto response)
        => new(
            "suggest",
            response.Data.Items
                .Select(item => new RakkoKeywordCandidate(
                    item.Keyword,
                    "suggest",
                    item.SuggestClass,
                    Type: null,
                    Question: null,
                    Importance: null,
                    SourceKeyword: null,
                    WordCount: null,
                    Relevance: null,
                    ToApplication(item.Metrics),
                    item.SuggestEngines?.Active ?? []))
                .ToArray());

    public static RakkoKeywordCandidates ToApplication(RelatedKeywordsResponseDto response)
        => new(
            "related",
            response.Data.Items
                .Select(item => new RakkoKeywordCandidate(
                    item.Keyword,
                    "related",
                    SuggestClass: null,
                    Type: null,
                    Question: null,
                    Importance: null,
                    SourceKeyword: null,
                    WordCount: null,
                    Relevance: null,
                    ToApplication(item.Metrics),
                    Engines: []))
                .ToArray());

    public static RakkoKeywordCandidates ToApplication(OtherKeywordsResponseDto response)
        => new(
            "other",
            response.Data.Items
                .Select(item => new RakkoKeywordCandidate(
                    item.Keyword,
                    "other",
                    SuggestClass: null,
                    item.Type,
                    item.Question,
                    item.Importance,
                    item.SourceKeyword,
                    WordCount: null,
                    Relevance: null,
                    ToApplication(item.Metrics),
                    Engines: []))
                .ToArray());

    public static RakkoQuestions ToApplication(SearchQuestionResponseDto response)
        => new(response.Data.Items.Select(item => new RakkoQuestion(item.Question)).ToArray());

    public static RakkoKeywordCandidates ToApplication(RankingKeywordsResponseDto response)
        => new(
            "ranking",
            response.Data.Items
                .Select(item => new RakkoKeywordCandidate(
                    item.Keyword,
                    "ranking",
                    SuggestClass: null,
                    Type: null,
                    Question: null,
                    Importance: null,
                    SourceKeyword: null,
                    item.WordCount,
                    item.Metrics?.Relevance,
                    ToApplication(item.Metrics),
                    Engines: []))
                .ToArray());

    public static RakkoSearchVolumeRegistration ToApplication(SearchVolumeHistoryResponseDto response)
        => new(response.Data.RequestId);

    public static RakkoSearchVolumeStatus ToApplication(SearchVolumeStatusResponseDto response)
        => new(response.Data.IsCompleted, response.Data.Statuses);

    public static RakkoSearchVolumeResults ToApplication(SearchVolumeResultsResponseDto response)
        => new(response.Data.Items.Select(item =>
        {
            var metrics = item.Metrics is null
                ? new RakkoKeywordMetrics(null, null, null, null, null)
                : new RakkoKeywordMetrics(
                    item.Metrics.SeoDifficulty,
                    item.Metrics.SearchVolume,
                    item.Metrics.Cpc,
                    item.Metrics.Competition,
                    FirstSeenRange: null);

            var monthlySearchVolume = item.Trends?.MonthlySearchVolume?
                .Where(pair => pair.Value.HasValue)
                .ToDictionary(
                    pair => pair.Key,
                    pair => Convert.ToInt32(pair.Value!.Value),
                    StringComparer.Ordinal)
                ?? [];

            return new RakkoSearchVolumeResultItem(
                item.Keyword,
                item.DataSource,
                metrics,
                monthlySearchVolume);
        }).ToArray());

    public static RakkoLocationCatalog ToApplication(LocationsResponseDto response)
        => new(response.Data.Locations
            .Select(item => new RakkoLocation(item.Name, item.Code.ToString("D", CultureInfo.InvariantCulture), item.CountryIsoCode))
            .ToArray());

    public static RakkoLanguageCatalog ToApplication(LanguagesResponseDto response)
        => new(response.Data.Languages
            .Select(item => new RakkoLanguage(item.Name, item.Code))
            .ToArray());

    private static RakkoKeywordMetrics? ToApplication(KeywordMetricsDto? metrics)
        => metrics is null
            ? null
            : new RakkoKeywordMetrics(
                metrics.SeoDifficulty,
                metrics.SearchVolume,
                metrics.Cpc,
                metrics.Competition,
                metrics.FirstSeenRange);

    private static RakkoKeywordMetrics? ToApplication(RankingKeywordMetricsDto? metrics)
        => metrics is null
            ? null
            : new RakkoKeywordMetrics(
                metrics.SeoDifficulty,
                metrics.SearchVolume,
                metrics.Cpc,
                metrics.Competition,
                FirstSeenRange: null);
}
