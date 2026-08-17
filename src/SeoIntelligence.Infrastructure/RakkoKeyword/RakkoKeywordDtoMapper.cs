using System.Globalization;
using System.Text.Json;
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
            Filter = request.Filter,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
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

    public static InfluxKeywordsKeywordDto ToDto(RakkoInfluxKeywordsRequest request)
        => new()
        {
            Targets = ToDto(request.Targets),
            KeywordCollapse = request.KeywordCollapse,
            Filter = request.Filter,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit
        };

    public static InfluxPagesDto ToDto(RakkoInfluxPagesRequest request)
        => new()
        {
            Targets = ToDto(request.Targets),
            TopKeywordCollapse = request.TopKeywordCollapse,
            Filter = request.Filter,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit
        };

    public static CompetitiveDto ToDto(RakkoCompetitiveRequest request)
        => new()
        {
            Url = request.Url,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy
        };

    public static ContentSearchDto ToDto(RakkoContentSearchRequest request)
        => new()
        {
            Keyword = request.Keyword,
            SearchTarget = request.SearchTarget,
            IsAdvancedSearch = request.IsAdvancedSearch,
            TopKeywordCollapse = request.TopKeywordCollapse,
            Filter = request.Filter,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit
        };

    public static HeadlineDto ToDto(RakkoHeadlineRequest request)
        => new()
        {
            Keyword = request.Keyword,
            LessHeadlines = request.LessHeadlines,
            LessCharacters = request.LessCharacters,
            H1 = request.H1,
            H2 = request.H2,
            H3 = request.H3,
            H4 = request.H4,
            H5 = request.H5,
            H6 = request.H6,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit
        };

    public static CoOccurrenceDto ToDto(RakkoCoOccurrenceRequest request)
        => new()
        {
            Keyword = request.Keyword,
            GetDetails = request.GetDetails,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit
        };

    public static SearchRankHistoryDto ToDto(RakkoSearchRankRegistrationRequest request)
        => new()
        {
            Keywords = request.Keywords,
            Urls = request.Urls,
            MatchType = request.MatchType,
            Depth = request.Depth,
            IsSearchVolumeAndSeoDifficultyEnabled = request.WithMetrics,
            Deduplicate = request.Deduplicate,
            Location = request.Location,
            Language = request.Language,
            Device = request.Device,
            Os = request.Os
        };

    public static SearchRankResultsDto ToDto(RakkoSearchRankResultsRequest request)
        => new()
        {
            Filter = request.Filter,
            SortBy = request.SortBy,
            OrderBy = request.OrderBy,
            Limit = request.Limit,
            WithAggregation = request.WithAggregation
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
        => new(response.Data.Items
            .Select(item => new RakkoQuestion(
                item.Question,
                item.Metrics?.RelativeDemand,
                item.Metrics?.FirstSeenRange))
            .ToArray());

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
        => new(ToSearchVolumeRequestId(response.Data.RequestId));

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
                    item.Metrics.FirstSeenRange);

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

    public static RakkoExternalSearchResults ToApplication(InfluxKeywordsKeywordResponseDto response)
        => ToExternalSearchResults("influx_keywords", response.Data);

    public static RakkoExternalSearchResults ToApplication(InfluxPagesResponseDto response)
        => ToExternalSearchResults("influx_pages", response.Data);

    public static RakkoExternalSearchResults ToApplication(CompetitiveResponseDto response)
        => ToExternalSearchResults("competitive", response.Data);

    public static RakkoExternalSearchResults ToApplication(ContentSearchResponseDto response)
        => ToExternalSearchResults("content_search", response.Data);

    public static RakkoExternalSearchResults ToApplication(HeadlineResponseDto response)
        => ToExternalSearchResults("headline", response.Data);

    public static RakkoExternalSearchResults ToApplication(CoOccurrenceResponseDto response)
        => ToExternalSearchResults("co_occurrence", response.Data);

    public static RakkoExternalSearchResults ToApplication(SearchRankSerpCacheResponseDto response)
        => ToExternalSearchResults("search_rank_serp", response.Data);

    public static RakkoSearchRankRegistration ToApplication(SearchRankHistoryResponseDto response)
        => new(response.Data.RequestId);

    public static RakkoSearchRankStatus ToApplication(SearchRankStatusResponseDto response)
        => new(response.Data.IsCompleted, response.Data.Statuses);

    public static RakkoExternalSearchResults ToApplication(SearchRankResultsResponseDto response)
        => ToExternalSearchResults("search_rank_results", response.Data);

    public static RakkoLocationCatalog ToApplication(MetadataLocationsResponseDto response)
        => new(response.Data.Locations
            .Select(item => new RakkoLocation(item.Name, item.CountryIsoCode))
            .ToArray());

    public static RakkoLanguageCatalog ToApplication(MetadataLanguagesResponseDto response)
        => new(response.Data.Languages
            .Select(item => new RakkoLanguage(item.Name))
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

    private static IReadOnlyList<RakkoKeywordTargetDto> ToDto(IReadOnlyList<RakkoApiTargetRequest> targets)
        => targets
            .Select(target => new RakkoKeywordTargetDto
            {
                Url = target.Url,
                MatchType = target.MatchType
            })
            .ToArray();

    private static RakkoExternalSearchResults ToExternalSearchResults(
        string source,
        RakkoKeywordItemsDataDto<JsonElement> data)
        => new(
            source,
            data.Items.Select(ToExternalSearchResultItem).ToArray(),
            ToRawJson(data.Query),
            ToRawJson(data.Summary));

    private static RakkoExternalSearchResultItem ToExternalSearchResultItem(JsonElement item)
    {
        var page = GetObject(item, "page");
        var site = GetObject(item, "site");
        var metrics = GetObject(item, "metrics");
        var ranking = GetObject(item, "ranking");
        var performance = GetObject(item, "performance");
        var topKeyword = GetObject(item, "topKeyword");
        var firstRanking = GetFirstObject(item, "rankings");

        return new RakkoExternalSearchResultItem(
            Keyword: GetString(item, "keyword") ?? GetString(topKeyword, "keyword") ?? GetString(item, "word"),
            Target: GetString(item, "target") ?? GetString(firstRanking, "target"),
            Url: GetString(item, "url") ?? GetString(page, "url") ?? GetString(ranking, "url") ?? GetString(firstRanking, "rankedUrl"),
            Domain: GetString(item, "domain") ?? GetString(page, "domain") ?? GetString(site, "domain"),
            Title: GetString(item, "title") ?? GetString(page, "title") ?? GetString(site, "title"),
            Position: GetDecimal(item, "position") ?? GetDecimal(ranking, "position") ?? GetDecimal(topKeyword, "position") ?? GetDecimal(metrics, "position") ?? GetDecimal(firstRanking, "position"),
            EstimatedTraffic: GetDecimal(item, "estimatedTraffic") ?? GetDecimal(metrics, "estimatedTraffic") ?? GetDecimal(ranking, "estimatedTraffic") ?? GetDecimal(performance, "estimatedTraffic") ?? GetDecimal(firstRanking, "estimatedTraffic"),
            TrafficValue: GetDecimal(item, "trafficValue") ?? GetDecimal(metrics, "trafficValue") ?? GetDecimal(performance, "trafficValue"),
            RawJson: item.GetRawText(),
            EntryNo: GetDecimal(item, "entryNo"));
    }

    private static JsonElement? GetObject(JsonElement? element, string propertyName)
    {
        if (element is null ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static JsonElement? GetFirstObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                return item;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement? element, string propertyName)
    {
        if (element is null ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement? element, string propertyName)
    {
        if (element is null ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long ToSearchVolumeRequestId(decimal requestId)
    {
        if (requestId <= 0m ||
            requestId != decimal.Truncate(requestId) ||
            requestId > long.MaxValue)
        {
            throw new JsonException(
                "Rakko Keyword API returned an invalid search-volume requestId. " +
                "requestId must be a positive Int64 integer.");
        }

        return decimal.ToInt64(requestId);
    }

    private static string? ToRawJson(JsonElement? element)
        => element is null || element.Value.ValueKind == JsonValueKind.Undefined
            ? null
            : element.Value.GetRawText();
}
