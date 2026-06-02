using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BlazorMvpUiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "UI")]
    public void KeywordInputParserParsesCsvNormalizesAndDeduplicatesKeywords()
    {
        var result = KeywordInputParser.Parse("""
             SEO 
            "content marketing"
            SEO

            """);

        Assert.Equal(["SEO", "content marketing"], result.Keywords);
        Assert.Equal(1, result.DuplicateCount);
        Assert.True(result.BlankCount >= 1);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task KeywordDiscoveryClientSendsFilterSortSourcesAndSyncPreference()
    {
        var projectId = Guid.NewGuid();
        using var handler = new RecordingHandler((request, _) =>
            JsonResponse(
                HttpStatusCode.OK,
                ApiResponseEnvelope<KeywordDiscoveryResult>.Success(
                    "req-ui-keyword",
                    new KeywordDiscoveryResult(
                        [
                            new KeywordCandidate(
                                "SEO guide",
                                "suggest",
                                "+",
                                72.5m,
                                SearchVolume: 1200,
                                SeoDifficulty: 18,
                                Cpc: 1.2m,
                                Competition: 22,
                                FirstSeenRange: "last_30_days")
                        ],
                        SeedKeyword: "SEO",
                        Location: "JP",
                        Language: "ja",
                        Sources: ["suggest", "related"],
                        ConsumedCredit: 2m))));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = CreateClient(httpClient);

        var result = await client.DiscoverKeywordsAsync(
            projectId,
            new KeywordDiscoveryRequest(
                SeedKeyword: "SEO",
                Sources: ["suggest", "related"],
                Engines: ["google", "bing"],
                Location: "JP",
                Language: "ja",
                Limit: 100,
                Filter: new KeywordDiscoveryFilter(
                    MinSearchVolume: 100,
                    Include: ["guide"],
                    Exclude: ["free"]),
                SortBy: "searchVolume",
                OrderBy: "desc",
                SyncPreferred: true));

        Assert.True(result.IsSuccess, result.ErrorSummary);
        Assert.Equal($"/api/projects/{projectId:D}/keyword-discovery/suggest", handler.RequestUris.Single());

        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = body.RootElement;
        Assert.Equal("SEO", root.GetProperty("seedKeyword").GetString());
        Assert.True(root.GetProperty("syncPreferred").GetBoolean());
        Assert.Equal("searchVolume", root.GetProperty("sortBy").GetString());
        Assert.Equal("desc", root.GetProperty("orderBy").GetString());
        Assert.Equal(["suggest", "related"], root.GetProperty("sources").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(["google", "bing"], root.GetProperty("engines").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(100, root.GetProperty("filter").GetProperty("minSearchVolume").GetDecimal());
        Assert.Equal("guide", root.GetProperty("filter").GetProperty("include")[0].GetString());
        Assert.Equal("SEO guide", result.Data!.Candidates.Single().Keyword);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SearchVolumeClientPostsJsonKeywordsArrayWithoutMultipartCsvUpload()
    {
        var projectId = Guid.NewGuid();
        using var handler = new RecordingHandler((_, _) =>
            JsonResponse(
                HttpStatusCode.Accepted,
                ApiResponseEnvelope<JobReference>.Success(
                    "req-ui-search-volume",
                    new JobReference(Guid.NewGuid(), "queued"))));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = CreateClient(httpClient);
        var parsed = KeywordInputParser.Parse("""
            SEO
            "content marketing"
            SEO
            """);

        var result = await client.RegisterSearchVolumeJobAsync(
            projectId,
            new SearchVolumeJobRequest(parsed.Keywords, "JP", "ja", 12, SeoDifficulty: true));

        Assert.True(result.IsSuccess, result.ErrorSummary);
        Assert.Equal($"/api/projects/{projectId:D}/search-volume/jobs", handler.RequestUris.Single());
        Assert.Equal("application/json", handler.ContentTypes.Single());
        Assert.DoesNotContain("multipart/form-data", handler.RequestBodies.Single(), StringComparison.OrdinalIgnoreCase);

        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        var keywords = body.RootElement.GetProperty("keywords").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(["SEO", "content marketing"], keywords);
        Assert.Equal("JP", body.RootElement.GetProperty("location").GetString());
        Assert.Equal("ja", body.RootElement.GetProperty("language").GetString());
    }

    private static SeoIntelligenceApiClient CreateClient(HttpClient httpClient)
        => new(httpClient, NullLogger<SeoIntelligenceApiClient>.Instance);

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, ApiResponseEnvelope<T> envelope)
        => new(statusCode)
        {
            Content = JsonContent.Create(envelope, options: JsonOptions)
        };

    private sealed class RecordingHandler : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<string?> ContentTypes { get; } = [];

        public List<string> RequestBodies { get; } = [];

        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);
            RequestBodies.Add(body);
            RequestUris.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return _responseFactory(request, body);
        }
    }
}
