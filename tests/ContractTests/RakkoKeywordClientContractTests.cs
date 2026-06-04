using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Infrastructure;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.RakkoKeyword;
using SeoIntelligence.Infrastructure.RakkoKeyword.Generated;

namespace ContractTests;

public sealed class RakkoKeywordClientContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task GeneratedDtoMetadataMatchesVendorOpenApiSpec()
    {
        var specPath = Path.Combine(GetRepositoryRoot(), RakkoKeywordOpenApiMetadata.SourcePath);
        await using var specStream = File.OpenRead(specPath);
        using var document = await JsonDocument.ParseAsync(specStream);
        var version = document.RootElement.GetProperty("info").GetProperty("version").GetString();
        var schemaNames = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var normalizedSpec = (await File.ReadAllTextAsync(specPath)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSpec))).ToLowerInvariant();

        Assert.Equal(RakkoKeywordOpenApiMetadata.OpenApiVersion, version);
        Assert.Equal(RakkoKeywordOpenApiMetadata.SourceSha256, hash);
        Assert.All(RakkoKeywordOpenApiMetadata.MvpSchemaNames, schemaName => Assert.Contains(schemaName, schemaNames));
        Assert.Equal(RakkoKeywordOpenApiMetadata.OpenApiVersion, RakkoKeywordPhase2OpenApiMetadata.OpenApiVersion);
        Assert.Equal(RakkoKeywordOpenApiMetadata.SourceSha256, RakkoKeywordPhase2OpenApiMetadata.SourceSha256);
        Assert.All(RakkoKeywordPhase2OpenApiMetadata.SchemaNames, schemaName => Assert.Contains(schemaName, schemaNames));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task MockClientReturnsApplicationDtosAndRawCallReferences()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();

            var result = await client.GetSuggestKeywordsAsync(
                CreateContext(),
                new RakkoSuggestKeywordsRequest("seo", ["google", "bing"]));

            Assert.True(result.IsSuccess);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal(1m, result.ConsumedCredit);
            Assert.NotNull(result.Data);
            var item = Assert.Single(result.Data!.Items);
            Assert.Equal("suggest", result.Data.Source);
            Assert.Equal("SeoIntelligence.Application.RakkoKeyword", item.GetType().Namespace);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", result.ExternalCall.RequestUri, StringComparison.Ordinal);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", result.ExternalCall.ResponseUri, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(result.ExternalCall.RequestHash));
            Assert.False(string.IsNullOrWhiteSpace(result.ExternalCall.ResponseHash));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task MockClientMapsAllKeywordDiscoveryEndpoints()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();
            var context = CreateContext(correlationId: "corr-contract-discovery");

            var suggest = await client.GetSuggestKeywordsAsync(
                context,
                new RakkoSuggestKeywordsRequest("seo", ["google"]));
            var related = await client.GetRelatedKeywordsAsync(
                context,
                new RakkoRelatedKeywordsRequest("seo"));
            var other = await client.GetOtherKeywordsAsync(
                context,
                new RakkoOtherKeywordsRequest("seo"));
            var questions = await client.GetQuestionsAsync(
                context,
                new RakkoQuestionSearchRequest("seo"));
            var ranking = await client.GetRankingKeywordsAsync(
                context,
                new RakkoRankingKeywordsRequest("seo"));

            Assert.All(new[] { suggest, related, other, ranking }, result =>
            {
                Assert.True(result.IsSuccess);
                Assert.Equal(200, result.StatusCode);
                Assert.NotEmpty(result.Data!.Items);
                Assert.StartsWith("storage://local/raw/rakko-keyword/", result.ExternalCall.RequestUri, StringComparison.Ordinal);
            });
            Assert.Equal("suggest", suggest.Data!.Source);
            Assert.Equal("related", related.Data!.Source);
            Assert.Equal("other", other.Data!.Source);
            Assert.Equal("ranking", ranking.Data!.Source);
            Assert.True(questions.IsSuccess);
            Assert.NotEmpty(questions.Data!.Items);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task MockClientMapsSearchVolumeRegisterStatusAndResults()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();
            var context = CreateContext(correlationId: "corr-contract-search-volume");

            var registration = await client.RegisterSearchVolumeAsync(
                context,
                new RakkoSearchVolumeRegistrationRequest(
                    ["seo", "content marketing"],
                    SeoDifficulty: true,
                    Location: "JP",
                    Language: "ja"));
            var status = await client.GetSearchVolumeStatusAsync(context, registration.Data!.RequestId);
            var results = await client.GetSearchVolumeResultsAsync(
                context,
                registration.Data.RequestId,
                new RakkoSearchVolumeResultsRequest(Limit: 50_000));

            Assert.True(registration.IsSuccess);
            Assert.Equal(1000001, registration.Data.RequestId);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", registration.ExternalCall.RequestUri, StringComparison.Ordinal);

            Assert.True(status.IsSuccess);
            Assert.True(status.Data!.IsCompleted);
            Assert.Equal("completed", status.Data.Statuses["overall"]);

            Assert.True(results.IsSuccess);
            Assert.Equal(5m, results.ConsumedCredit);
            var item = Assert.Single(results.Data!.Items);
            Assert.Equal("sample keyword", item.Keyword);
            Assert.Equal(1300, item.Metrics.SearchVolume);
            Assert.Equal(2, item.MonthlySearchVolume.Count);
            Assert.Equal(1300, item.MonthlySearchVolume["2026-05"]);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", results.ExternalCall.ResponseUri, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task MockClientMapsPhase2ExternalApiEndpoints()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();
            var context = CreateContext(correlationId: "corr-contract-phase2");
            var targets = new[] { new RakkoApiTargetRequest("https://example.com", "domain") };

            var influxKeywords = await client.GetInfluxKeywordsAsync(
                context,
                new RakkoInfluxKeywordsRequest(targets));
            var influxPages = await client.GetInfluxPagesAsync(
                context,
                new RakkoInfluxPagesRequest(targets));
            var competitive = await client.GetCompetitiveSitesAsync(
                context,
                new RakkoCompetitiveRequest("https://example.com"));
            var contentSearch = await client.GetContentSearchAsync(
                context,
                new RakkoContentSearchRequest("seo"));
            var headlines = await client.GetHeadlinesAsync(
                context,
                new RakkoHeadlineRequest("seo"));
            var coOccurrences = await client.GetCoOccurrencesAsync(
                context,
                new RakkoCoOccurrenceRequest("seo"));
            var rankRegistration = await client.RegisterSearchRankAsync(
                context,
                new RakkoSearchRankRegistrationRequest(["seo"], ["https://example.com"]));
            var rankStatus = await client.GetSearchRankStatusAsync(context, rankRegistration.Data!.RequestId);
            var rankResults = await client.GetSearchRankResultsAsync(
                context,
                rankRegistration.Data.RequestId,
                new RakkoSearchRankResultsRequest());

            Assert.All(
                new[] { influxKeywords, influxPages, competitive, contentSearch, headlines, coOccurrences, rankResults },
                result =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(200, result.StatusCode);
                    Assert.NotEmpty(result.Data!.Items);
                    Assert.False(string.IsNullOrWhiteSpace(result.Data.Items[0].RawJson));
                    Assert.StartsWith("storage://local/raw/rakko-keyword/", result.ExternalCall.RequestUri, StringComparison.Ordinal);
                });

            Assert.Equal("influx_keywords", influxKeywords.Data!.Source);
            Assert.Equal("https://example.com", influxKeywords.Data.Items[0].Target);
            Assert.Equal("seo competitor keyword", influxKeywords.Data.Items[0].Keyword);
            Assert.Equal(4m, influxKeywords.Data.Items[0].Position);
            Assert.Equal("influx_pages", influxPages.Data!.Source);
            Assert.Equal("https://example.com/guide", influxPages.Data.Items[0].Url);
            Assert.Equal("competitive", competitive.Data!.Source);
            Assert.Equal("competitor.example", competitive.Data.Items[0].Domain);
            Assert.Equal("content_search", contentSearch.Data!.Source);
            Assert.Equal("SEO Content Benchmark", contentSearch.Data.Items[0].Title);
            Assert.Equal("headline", headlines.Data!.Source);
            Assert.Equal("https://content.example/seo", headlines.Data.Items[0].Url);
            Assert.Equal("co_occurrence", coOccurrences.Data!.Source);
            Assert.Equal("search intent", coOccurrences.Data.Items[0].Keyword);
            Assert.True(rankRegistration.IsSuccess);
            Assert.Equal("rank-request-1000001", rankRegistration.Data.RequestId);
            Assert.True(rankStatus.IsSuccess);
            Assert.True(rankStatus.Data!.IsCompleted);
            Assert.Equal("completed", rankStatus.Data.Statuses["overall"]);
            Assert.Equal("search_rank_results", rankResults.Data!.Source);
            Assert.Equal("seo", rankResults.Data.Items[0].Keyword);
            Assert.Equal("https://example.com", rankResults.Data.Items[0].Target);
            Assert.Equal(5m, rankResults.Data.Items[0].Position);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Theory]
    [Trait("Category", "Contract")]
    [InlineData(429, RakkoKeywordFailureKind.Retryable, "rate_limited")]
    [InlineData(402, RakkoKeywordFailureKind.Fatal, "credit_insufficient")]
    [InlineData(403, RakkoKeywordFailureKind.Fatal, "forbidden")]
    [InlineData(500, RakkoKeywordFailureKind.Retryable, "external_500")]
    [InlineData(503, RakkoKeywordFailureKind.Retryable, "external_503")]
    public async Task MockClientClassifiesMajorExternalErrors(int statusCode, RakkoKeywordFailureKind failureKind, string errorCode)
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(
            storagePath,
            new Dictionary<string, string?>
            {
                ["RakkoKeyword:MockStatusCode"] = statusCode.ToString(CultureInfo.InvariantCulture)
            });

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();

            var result = await client.GetSuggestKeywordsAsync(
                CreateContext(),
                new RakkoSuggestKeywordsRequest("seo"));

            Assert.False(result.IsSuccess);
            Assert.Equal(statusCode, result.StatusCode);
            Assert.Equal(failureKind, result.FailureKind);
            Assert.Equal(errorCode, result.ExternalCall.ErrorCode);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task RealClientUsesSecretStoreHeadersAndMapsResponse()
    {
        var handler = new CapturingHandler("""
            {
              "result": true,
              "meta": { "consumedCredit": 1 },
              "data": {
                "query": {},
                "summary": {},
                "items": [
                  {
                    "keyword": "seo guide",
                    "suggestClass": "+",
                    "metrics": {
                      "seoDifficulty": 10,
                      "searchVolume": 100,
                      "cpc": 1.2,
                      "competition": 5,
                      "firstSeenRange": "last_30_days"
                    },
                    "suggestEngines": {
                      "count": 1,
                      "active": ["google"]
                    }
                  }
                ]
              },
              "errors": []
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test")
        };
        var recorder = new CapturingRecorder();
        var options = Options.Create(new RakkoKeywordOptions
        {
            Mode = RakkoKeywordOptions.RealMode,
            BaseUrl = "https://api.example.test",
            ApiKeySecretRef = "rakko-keyword-api-key-dev",
            EnvironmentName = "Testing"
        });
        var client = new RakkoKeywordRealClient(
            httpClient,
            new FakeSecretStore("secret-value"),
            recorder,
            options,
            NullLogger<RakkoKeywordRealClient>.Instance);

        var result = await client.GetSuggestKeywordsAsync(
            CreateContext(apiKeySecretRef: "rakko-keyword-api-key-dev", correlationId: "corr-real-1"),
            new RakkoSuggestKeywordsRequest("seo"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1m, result.ConsumedCredit);
        Assert.Equal("/v1/suggest-keywords", handler.RequestUri!.AbsolutePath);
        Assert.Equal("secret-value", Assert.Single(handler.Headers["X-API-Key"]));
        Assert.Equal("corr-real-1", Assert.Single(handler.Headers["X-Correlation-Id"]));
        Assert.Contains("SeoIntelligence/0.1.0", handler.UserAgent);
        Assert.Contains("\"keyword\":\"seo\"", handler.RequestBody);
        Assert.DoesNotContain("secret-value", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal(RakkoKeywordClientSupport.SuggestKeywordsEndpoint, recorder.LastRequest!.Endpoint);
        Assert.Null(recorder.LastRequest.ErrorCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task RealClientUsesSecretStoreForPhase2SearchRankRegister()
    {
        var handler = new CapturingHandler("""
            {
              "result": true,
              "meta": { "consumedCredit": 0 },
              "data": { "requestId": "rank-request-real-1" },
              "errors": []
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test")
        };
        var recorder = new CapturingRecorder();
        var options = Options.Create(new RakkoKeywordOptions
        {
            Mode = RakkoKeywordOptions.RealMode,
            BaseUrl = "https://api.example.test",
            ApiKeySecretRef = "rakko-keyword-api-key-dev",
            EnvironmentName = "Testing"
        });
        var client = new RakkoKeywordRealClient(
            httpClient,
            new FakeSecretStore("secret-value"),
            recorder,
            options,
            NullLogger<RakkoKeywordRealClient>.Instance);

        var result = await client.RegisterSearchRankAsync(
            CreateContext(apiKeySecretRef: "rakko-keyword-api-key-dev", correlationId: "corr-rank-real-1"),
            new RakkoSearchRankRegistrationRequest(["seo"], ["https://example.com"], Depth: 50));

        Assert.True(result.IsSuccess);
        Assert.Equal("rank-request-real-1", result.Data!.RequestId);
        Assert.Equal("/v1/search-rank", handler.RequestUri!.AbsolutePath);
        Assert.Equal("secret-value", Assert.Single(handler.Headers["X-API-Key"]));
        Assert.Equal("corr-rank-real-1", Assert.Single(handler.Headers["X-Correlation-Id"]));
        Assert.Contains("\"keywords\":[\"seo\"]", handler.RequestBody);
        Assert.Contains("\"urls\":[\"https://example.com\"]", handler.RequestBody);
        Assert.Contains("\"depth\":50", handler.RequestBody);
        Assert.DoesNotContain("secret-value", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal(RakkoKeywordClientSupport.SearchRankEndpoint, recorder.LastRequest!.Endpoint);
        Assert.Null(recorder.LastRequest.ErrorCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void DefaultInfrastructureRegistrationUsesMockClient()
    {
        var storagePath = CreateTempStoragePath();
        using var provider = BuildProvider(storagePath);

        try
        {
            using var scope = provider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();

            Assert.IsType<RakkoKeywordMockClient>(client);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    private static ServiceProvider BuildProvider(
        string storagePath,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "",
            ["Redis:ConnectionString"] = "",
            ["Storage:Provider"] = "Local",
            ["Storage:BasePath"] = storagePath,
            ["Storage:BucketName"] = "seo-intelligence",
            ["SecretStore:Provider"] = "Configuration",
            ["SecretStore:ConfigurationPrefix"] = "Secrets",
            ["Hangfire:Storage"] = "PostgreSQL",
            ["OpenTelemetry:ServiceName"] = "ContractTests"
        };

        if (additionalConfiguration is not null)
        {
            foreach (var (key, value) in additionalConfiguration)
            {
                configurationValues[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSeoIntelligenceInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static RakkoKeywordClientContext CreateContext(
        string? apiKeySecretRef = null,
        string? correlationId = null)
        => new(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            ApiKeySecretRef: apiKeySecretRef,
            ApiContractScopeId: SeoIntelligenceSeedData.DefaultRakkoContractScopeId,
            ContractScopeKey: SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CorrelationId: correlationId);

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeoIntelligence.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-contract-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public Dictionary<string, IReadOnlyList<string>> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string UserAgent { get; private set; } = string.Empty;

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = header.Value.ToArray();
            }

            UserAgent = request.Headers.UserAgent.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class CapturingRecorder : IRakkoKeywordCallRecorder
    {
        public RakkoKeywordCallRecordRequest? LastRequest { get; private set; }

        public Task<RakkoKeywordExternalCallRecord> RecordAsync(
            RakkoKeywordCallRecordRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new RakkoKeywordExternalCallRecord(
                Guid.Parse("018f3f12-0003-7000-8000-000000000001"),
                "request-hash",
                "storage://local/request.json.gz",
                "response-hash",
                "storage://local/response.json.gz",
                request.CacheHit,
                request.ErrorCode));
        }
    }

    private sealed class FakeSecretStore(string secretValue) : ISecretStore
    {
        public Task<SecretValue?> GetAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult<SecretValue?>(new SecretValue(secretValue));

        public Task<SecretReference> PutAsync(
            SecretReference reference,
            SecretValue value,
            CancellationToken cancellationToken = default)
            => Task.FromResult(reference);

        public Task<bool> ExistsAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<SecretStoreConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SecretStoreConnectivityResult(true, "ok"));
    }
}
