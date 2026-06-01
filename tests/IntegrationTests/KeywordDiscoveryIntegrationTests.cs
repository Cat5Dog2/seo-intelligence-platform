using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

namespace IntegrationTests;

public sealed class KeywordDiscoveryIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task KeywordDiscoverySyncCollectsSavesAndFiltersCandidates()
    {
        await using var factory = new KeywordDiscoveryApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/keyword-discovery/suggest")
            {
                Content = JsonContent.Create(new
                {
                    seedKeyword = " SEO ",
                    sources = new[] { "suggest", "related", "other", "question", "ranking" },
                    engines = new[] { "google", "bing" },
                    limit = 20,
                    syncPreferred = true,
                    language = "ja",
                    location = "JP",
                    filter = new
                    {
                        include = new[] { "guide" }
                    },
                    sortBy = "searchVolume",
                    orderBy = "desc"
                })
            };
            request.Headers.Add("X-Correlation-Id", "corr-keyword-sync");

            using var response = await client.SendAsync(request);
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = document.RootElement.GetProperty("data");
            Assert.False(data.GetProperty("isAccepted").GetBoolean());
            Assert.Equal("SEO", data.GetProperty("seedKeyword").GetString());
            var candidate = Assert.Single(data.GetProperty("candidates").EnumerateArray());
            Assert.Equal("SEO guide", candidate.GetProperty("keyword").GetString());
            Assert.Equal("suggest", candidate.GetProperty("source").GetString());
            Assert.Equal(1200m, candidate.GetProperty("searchVolume").GetDecimal());

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            Assert.Equal(1, await dbContext.KeywordSeeds.CountAsync(entity => entity.ProjectId == projectId));
            Assert.True(await dbContext.Keywords.AnyAsync(entity => entity.NormalizedText == "SEO" && entity.Language == "ja"));
            Assert.Equal(2, await dbContext.KeywordSuggestions.CountAsync());
            Assert.Equal(1, await dbContext.RelatedKeywords.CountAsync());
            Assert.Equal(1, await dbContext.Questions.CountAsync(entity => entity.ProjectId == projectId));
            Assert.Equal(2, await dbContext.LsiPaaItems.CountAsync());
            Assert.Equal(1, await dbContext.RankingKeywords.CountAsync());
            Assert.Equal(5, await dbContext.ExternalApiCalls.CountAsync(entity => entity.ProjectId == projectId));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task KeywordDiscoveryAsyncRegistersJobAndDispatcherPersistsResults()
    {
        await using var factory = new KeywordDiscoveryApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            var payload = new
            {
                seedKeyword = "content marketing",
                sources = new[] { "suggest", "related" },
                engines = new[] { "google" },
                limit = 10,
                syncPreferred = false,
                language = "ja",
                location = "JP"
            };

            using var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/keyword-discovery/suggest", payload);
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var data = document.RootElement.GetProperty("data");
            var jobId = data.GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());
            Assert.True(data.GetProperty("isAccepted").GetBoolean());
            Assert.Equal($"/api/jobs/{jobId:D}", data.GetProperty("statusUrl").GetString());

            using var duplicateResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/keyword-discovery/suggest", payload);
            using var duplicateDocument = await ReadJsonAsync(duplicateResponse);
            Assert.Equal(HttpStatusCode.Accepted, duplicateResponse.StatusCode);
            Assert.Equal(jobId, duplicateDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.NotNull(job.ResultResourceId);
                Assert.Equal(1, await dbContext.KeywordSuggestions.CountAsync());
                Assert.Equal(1, await dbContext.RelatedKeywords.CountAsync());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task KeywordDiscoveryJobKeepsFetchedSourcesAndMarksUnfetchedSourceRetryable()
    {
        await using var factory = new KeywordDiscoveryApiFactory(usePartialFailureClient: true);
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/keyword-discovery/suggest",
                new
                {
                    seedKeyword = "technical seo",
                    sources = new[] { "suggest", "related" },
                    engines = new[] { "google" },
                    limit = 10,
                    syncPreferred = false,
                    language = "ja",
                    location = "JP"
                });
            using var document = await ReadJsonAsync(response);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedRetryable, job.Status);
                Assert.Equal(1, await dbContext.KeywordSuggestions.CountAsync());
                Assert.Equal(0, await dbContext.RelatedKeywords.CountAsync());
                Assert.Contains("rate_limited", job.ErrorJson, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> SeedProjectAsync(KeywordDiscoveryApiFactory factory)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"Keyword Discovery {projectId:N}",
            DefaultLocation = "JP",
            DefaultLanguage = "ja",
            KpiJson = "{}",
            Status = StatusValues.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static HttpClient CreateClient(KeywordDiscoveryApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-keyword-discovery-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class KeywordDiscoveryApiFactory(bool usePartialFailureClient = false) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public string StoragePath { get; } = CreateTempStoragePath();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "",
                    ["Redis:ConnectionString"] = "",
                    ["Storage:Provider"] = "Local",
                    ["Storage:BasePath"] = StoragePath,
                    ["Storage:BucketName"] = "seo-intelligence",
                    ["SecretStore:Provider"] = "Configuration",
                    ["SecretStore:ConfigurationPrefix"] = "Secrets",
                    ["Hangfire:Storage"] = "PostgreSQL",
                    ["OpenTelemetry:ServiceName"] = "IntegrationTests",
                    ["RakkoKeyword:Mode"] = "Mock"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                if (usePartialFailureClient)
                {
                    services.RemoveAll<IRakkoKeywordClient>();
                    services.AddScoped<IRakkoKeywordClient, PartialFailureRakkoKeywordClient>();
                }

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class PartialFailureRakkoKeywordClient : IRakkoKeywordClient
    {
        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoSuggestKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoKeywordCandidates>.Success(
                new RakkoKeywordCandidates(
                    "suggest",
                    [
                        new RakkoKeywordCandidate(
                            $"{request.Keyword} guide",
                            "suggest",
                            "+",
                            Type: null,
                            Question: null,
                            Importance: null,
                            SourceKeyword: null,
                            WordCount: null,
                            Relevance: null,
                            new RakkoKeywordMetrics(20, 1000, 0.5m, 10, "last_30_days"),
                            ["google"])
                    ]),
                consumedCredit: 1m,
                statusCode: 200,
                ExternalCall("/v1/suggest-keywords", null)));

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRelatedKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoKeywordCandidates>.Failure(
                statusCode: 429,
                errors: ["Rate limit exceeded."],
                failureKind: RakkoKeywordFailureKind.Retryable,
                ExternalCall("/v1/related-keywords", "rate_limited")));

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoOtherKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoKeywordCandidates>.Success(
                new RakkoKeywordCandidates("other", []),
                0m,
                200,
                ExternalCall("/v1/other-keywords", null)));

        public Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
            RakkoKeywordClientContext context,
            RakkoQuestionSearchRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoQuestions>.Success(
                new RakkoQuestions([]),
                0m,
                200,
                ExternalCall("/v1/question-search", null)));

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRankingKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoKeywordCandidates>.Success(
                new RakkoKeywordCandidates("ranking", []),
                0m,
                200,
                ExternalCall("/v1/ranking-keywords", null)));

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
            RakkoKeywordClientContext context,
            RakkoSearchVolumeRegistrationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
            RakkoKeywordClientContext context,
            long requestId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
            RakkoKeywordClientContext context,
            long requestId,
            RakkoSearchVolumeResultsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoLocationCatalog>> ListLocationsAsync(
            RakkoKeywordClientContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoLanguageCatalog>> ListLanguagesAsync(
            RakkoKeywordClientContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static RakkoKeywordExternalCallRecord ExternalCall(string endpoint, string? errorCode)
            => new(
                Guid.NewGuid(),
                $"{endpoint}-request-hash",
                $"storage://local/{endpoint.Trim('/').Replace('/', '-')}-request.json.gz",
                errorCode is null ? $"{endpoint}-response-hash" : null,
                errorCode is null ? $"storage://local/{endpoint.Trim('/').Replace('/', '-')}-response.json.gz" : null,
                CacheHit: false,
                errorCode);
    }
}
