using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Infrastructure.RakkoKeyword.Generated;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal sealed class RakkoKeywordMockClient(
    IRakkoKeywordCallRecorder recorder,
    IOptions<RakkoKeywordOptions> options)
    : IRakkoKeywordClient
{
    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoSuggestKeywordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SuggestKeywordsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 1m },
            Data = new RakkoKeywordItemsDataDto<SuggestKeywordItemDto>
            {
                Items =
                [
                    new SuggestKeywordItemDto
                    {
                        Keyword = $"{request.Keyword} guide",
                        SuggestClass = "+",
                        Metrics = Metrics(35, 1200, 0.7m, 12, "last_30_days"),
                        SuggestEngines = new SuggestEnginesDto { Count = 2, Active = request.Modes ?? ["google", "bing"] }
                    }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SuggestKeywordsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRelatedKeywordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new RelatedKeywordsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 1m },
            Data = new RakkoKeywordItemsDataDto<RelatedKeywordItemDto>
            {
                Items =
                [
                    new RelatedKeywordItemDto
                    {
                        Keyword = $"{request.Keyword} comparison",
                        Metrics = Metrics(42, 900, 1.2m, 18, "within_6_months")
                    }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.RelatedKeywordsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoOtherKeywordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new OtherKeywordsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 1m },
            Data = new RakkoKeywordItemsDataDto<OtherKeywordItemDto>
            {
                Items =
                [
                    new OtherKeywordItemDto
                    {
                        Type = "lsi",
                        Keyword = $"{request.Keyword} examples",
                        Importance = "high",
                        SourceKeyword = request.Keyword,
                        Metrics = Metrics(30, 600, 0.4m, 10, "last_90_days")
                    },
                    new OtherKeywordItemDto
                    {
                        Type = "paa",
                        Question = $"What is {request.Keyword}?",
                        Importance = "medium",
                        SourceKeyword = request.Keyword
                    }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.OtherKeywordsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
        RakkoKeywordClientContext context,
        RakkoQuestionSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SearchQuestionResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 1m },
            Data = new RakkoKeywordItemsDataDto<QuestionItemDto>
            {
                Items =
                [
                    new QuestionItemDto { Question = $"How do I use {request.Keyword}?" }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.QuestionSearchEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoRankingKeywordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new RankingKeywordsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 2m },
            Data = new RakkoKeywordItemsDataDto<RankingKeywordItemDto>
            {
                Items =
                [
                    new RankingKeywordItemDto
                    {
                        Keyword = $"{request.Keyword} ranking",
                        WordCount = 2,
                        Metrics = new RankingKeywordMetricsDto
                        {
                            SeoDifficulty = 28,
                            SearchVolume = 500,
                            Cpc = 0.3m,
                            Competition = 8,
                            Relevance = 78
                        }
                    }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.RankingKeywordsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
        RakkoKeywordClientContext context,
        RakkoSearchVolumeRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SearchVolumeHistoryResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new SearchVolumeHistoryDataDto { RequestId = 1000001 },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchVolumeEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
        RakkoKeywordClientContext context,
        long requestId,
        CancellationToken cancellationToken = default)
    {
        var response = new SearchVolumeStatusResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new SearchVolumeStatusDataDto
            {
                IsCompleted = true,
                Statuses = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["overall"] = "completed"
                }
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchVolumeStatusEndpoint,
            "GET",
            requestBody: null,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
        RakkoKeywordClientContext context,
        long requestId,
        RakkoSearchVolumeResultsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SearchVolumeResultsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 5m },
            Data = new RakkoKeywordItemsDataDto<SearchVolumeResultItemDto>
            {
                Items =
                [
                    new SearchVolumeResultItemDto
                    {
                        Keyword = "sample keyword",
                        DataSource = "Mock",
                        Metrics = new SearchVolumeMetricsDto
                        {
                            SeoDifficulty = 32,
                            SearchVolume = 1300,
                            Cpc = 0.8m,
                            Competition = 14
                        },
                        Trends = new SearchVolumeTrendsDto
                        {
                            MonthlySearchVolume = new Dictionary<string, decimal?>(StringComparer.Ordinal)
                            {
                                ["2026-04"] = 1200,
                                ["2026-05"] = 1300
                            }
                        }
                    }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchVolumeResultsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoLocationCatalog>> ListLocationsAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default)
    {
        var response = new LocationsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new LocationsDataDto
            {
                Locations =
                [
                    new LocationItemDto { Name = "Japan", Code = 2392, CountryIsoCode = "JP" }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.LocationsEndpoint,
            "GET",
            requestBody: null,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoLanguageCatalog>> ListLanguagesAsync(
        RakkoKeywordClientContext context,
        CancellationToken cancellationToken = default)
    {
        var response = new LanguagesResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new LanguagesDataDto
            {
                Languages =
                [
                    new LanguageItemDto { Name = "Japanese", Code = "ja" }
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.LanguagesEndpoint,
            "GET",
            requestBody: null,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxKeywordsAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxKeywordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new InfluxKeywordsKeywordResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 3m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        target = request.Targets.FirstOrDefault()?.Url ?? "example.com",
                        keyword = "seo competitor keyword",
                        metrics = new { seoDifficulty = 34, searchVolume = 2400, cpc = 1.4m, competition = 18 },
                        ranking = new { position = 4, estimatedTraffic = 120.5m, url = "https://example.com/guide" }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.InfluxKeywordsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxPagesAsync(
        RakkoKeywordClientContext context,
        RakkoInfluxPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new InfluxPagesResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 3m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        target = request.Targets.FirstOrDefault()?.Url ?? "example.com",
                        page = new { title = "SEO Guide", url = "https://example.com/guide" },
                        performance = new { rankingKeywordCount = 25, estimatedTraffic = 340.5m, trafficValue = 1200.25m },
                        topKeyword = new { keyword = "seo guide", position = 3, metrics = new { searchVolume = 1900 } }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.InfluxPagesEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCompetitiveSitesAsync(
        RakkoKeywordClientContext context,
        RakkoCompetitiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new CompetitiveResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 2m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        site = new { domain = "competitor.example", title = "Competitor Example" },
                        metrics = new
                        {
                            estimatedTraffic = 980.5m,
                            trafficValue = 4200.75m,
                            keywordCount = 120,
                            pageCount = 18,
                            duplicateKeywordCount = 35,
                            duplicateRate = 0.42m,
                            competitorUniqueKeywordCount = 85,
                            targetUniqueKeywordCount = 40
                        }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.CompetitiveEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetContentSearchAsync(
        RakkoKeywordClientContext context,
        RakkoContentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new ContentSearchResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 2m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        page = new
                        {
                            domain = "content.example",
                            url = "https://content.example/seo",
                            title = "SEO Content Benchmark",
                            description = "Benchmark content for SEO planning."
                        },
                        metrics = new { estimatedTraffic = 640.5m, trafficValue = 3100.25m, rankingKeywordCount = 44 },
                        topKeyword = new { keyword = request.Keyword, wordCount = 2, position = 2, metrics = new { searchVolume = 2200 } }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.ContentSearchEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetHeadlinesAsync(
        RakkoKeywordClientContext context,
        RakkoHeadlineRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new HeadlineResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 2m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        page = new
                        {
                            url = "https://content.example/seo",
                            title = "SEO Content Benchmark",
                            description = "Benchmark content for SEO planning."
                        },
                        metrics = new { position = 2, headlineCount = 6, wordCount = 3200 },
                        headlines = new[]
                        {
                            new { level = "h1", text = "SEO Content Benchmark" },
                            new { level = "h2", text = "Search intent" }
                        }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.HeadlineEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCoOccurrencesAsync(
        RakkoKeywordClientContext context,
        RakkoCoOccurrenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new CoOccurrenceResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 2m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        word = "search intent",
                        metrics = new
                        {
                            occurrencePageCount = 8,
                            occurrenceTitleCount = 4,
                            occurrenceHeadingCount = 5,
                            siteCountTotal = 7,
                            siteCountHeading = 4
                        },
                        pageDetails = new[]
                        {
                            new
                            {
                                rank = 1,
                                title = "SEO Content Benchmark",
                                url = "https://content.example/seo",
                                count = 3,
                                countInHeadline = 1,
                                countInTitle = 1,
                                pageCount = 1,
                                pageCountInHeadline = 1
                            }
                        }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.CoOccurrenceEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoSearchRankRegistration>> RegisterSearchRankAsync(
        RakkoKeywordClientContext context,
        RakkoSearchRankRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SearchRankHistoryResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new SearchRankHistoryDataDto { RequestId = "rank-request-1000001" },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchRankEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoSearchRankStatus>> GetSearchRankStatusAsync(
        RakkoKeywordClientContext context,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var response = new SearchRankStatusResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 0m },
            Data = new SearchRankStatusDataDto
            {
                IsCompleted = true,
                Statuses = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["overall"] = "completed"
                }
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchRankStatusEndpoint,
            "GET",
            requestBody: null,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetSearchRankResultsAsync(
        RakkoKeywordClientContext context,
        string requestId,
        RakkoSearchRankResultsRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = RakkoKeywordDtoMapper.ToDto(request);
        var response = new SearchRankResultsResponseDto
        {
            Result = true,
            Meta = new RakkoKeywordMetaDto { ConsumedCredit = 4m },
            Data = new RakkoKeywordItemsDataDto<JsonElement>
            {
                Items =
                [
                    JsonItem(new
                    {
                        keyword = "seo",
                        metrics = new { seoDifficulty = 31, searchVolume = 1200, cpc = 0.9m, competition = 12 },
                        rankings = new[]
                        {
                            new
                            {
                                target = "https://example.com",
                                position = 5,
                                rankedUrl = "https://example.com/seo",
                                estimatedTraffic = 80.5m
                            }
                        }
                    })
                ]
            },
            Errors = []
        };

        return ExecuteAsync(
            context,
            RakkoKeywordClientSupport.SearchRankResultsEndpoint,
            "POST",
            dto,
            response,
            RakkoKeywordDtoMapper.ToApplication,
            cancellationToken);
    }

    private async Task<RakkoKeywordCallResult<TApplication>> ExecuteAsync<TResponse, TApplication>(
        RakkoKeywordClientContext context,
        string endpoint,
        string method,
        object? requestBody,
        TResponse response,
        Func<TResponse, TApplication> map,
        CancellationToken cancellationToken)
        where TResponse : IRakkoKeywordResponseDto
    {
        var stopwatch = Stopwatch.StartNew();
        var configuredStatusCode = options.Value.MockStatusCode ?? 200;
        if (!RakkoKeywordClientSupport.IsSuccessStatusCode(configuredStatusCode))
        {
            return await CompleteFailureAsync<TApplication>(
                context,
                endpoint,
                method,
                requestBody,
                configuredStatusCode,
                stopwatch,
                cancellationToken);
        }

        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, RakkoKeywordJson.SerializerOptions);
        stopwatch.Stop();
        var externalCall = await recorder.RecordAsync(
            new RakkoKeywordCallRecordRequest(
                context,
                endpoint,
                method,
                requestBody,
                responseBytes,
                configuredStatusCode,
                response.Meta.ConsumedCredit,
                Convert.ToInt32(stopwatch.ElapsedMilliseconds),
                CacheHit: false,
                ErrorCode: null),
            cancellationToken);

        return RakkoKeywordCallResult<TApplication>.Success(
            map(response),
            response.Meta.ConsumedCredit,
            configuredStatusCode,
            externalCall);
    }

    private async Task<RakkoKeywordCallResult<TApplication>> CompleteFailureAsync<TApplication>(
        RakkoKeywordClientContext context,
        string endpoint,
        string method,
        object? requestBody,
        int statusCode,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var errors = new[] { RakkoKeywordClientSupport.DefaultErrorMessage(statusCode) };
        var response = new RakkoKeywordErrorResponseDto
        {
            Result = false,
            Errors = errors
        };
        var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, RakkoKeywordJson.SerializerOptions);
        stopwatch.Stop();

        var externalCall = await recorder.RecordAsync(
            new RakkoKeywordCallRecordRequest(
                context,
                endpoint,
                method,
                requestBody,
                responseBytes,
                statusCode,
                ConsumedCredit: 0m,
                Convert.ToInt32(stopwatch.ElapsedMilliseconds),
                CacheHit: false,
                RakkoKeywordClientSupport.ToErrorCode(statusCode)),
            cancellationToken);

        return RakkoKeywordCallResult<TApplication>.Failure(
            statusCode,
            errors,
            RakkoKeywordClientSupport.ToFailureKind(statusCode),
            externalCall);
    }

    private static KeywordMetricsDto Metrics(
        decimal seoDifficulty,
        decimal searchVolume,
        decimal cpc,
        decimal competition,
        string firstSeenRange)
        => new()
        {
            SeoDifficulty = seoDifficulty,
            SearchVolume = searchVolume,
            Cpc = cpc,
            Competition = competition,
            FirstSeenRange = firstSeenRange
        };

    private static JsonElement JsonItem(object value)
        => JsonSerializer.SerializeToElement(value, RakkoKeywordJson.SerializerOptions);
}
