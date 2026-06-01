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

public sealed class SearchVolumeIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeJobRegistersPollsAndPersistsMoreThanThousandResults()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            var keywords = Enumerable.Range(1, 1005)
                .Select(index => $" keyword {index:D4} ")
                .Concat(["keyword 0001", "", "keyword 0002"])
                .ToArray();

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords,
                    location = "JP",
                    language = "ja",
                    aggregationPeriodMonths = 12,
                    seoDifficulty = true
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var searchVolumeJob = await dbContext.SearchVolumeJobs.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                using var options = JsonDocument.Parse(searchVolumeJob.RequestOptionsJson);
                Assert.Equal(1005, options.RootElement.GetProperty("normalizedKeywordCount").GetInt32());
                Assert.Equal(1005m, options.RootElement.GetProperty("estimatedCredit").GetDecimal());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);

                Assert.Equal(StatusValues.WaitingExternal, job.Status);
                Assert.Equal("7000001", externalRequest.ExternalRequestId);
                Assert.Equal(StatusValues.WaitingExternal, externalRequest.Status);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);

                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.Equal(StatusValues.Succeeded, externalRequest.Status);
                Assert.Equal(1005, await dbContext.SearchVolumeResults.CountAsync(entity => entity.JobId == jobId));
                Assert.Equal(1005, await dbContext.KeywordMetrics.CountAsync());
                Assert.Equal(2010, await dbContext.KeywordMonthlyVolumes.CountAsync());
            }

            using var resultsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/search-volume/jobs/{jobId}/results?page=1&pageSize=5&sortBy=searchVolume&orderBy=desc");
            using var resultsDocument = await ReadJsonAsync(resultsResponse);

            Assert.Equal(HttpStatusCode.OK, resultsResponse.StatusCode);
            Assert.Equal(1005, resultsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            var rows = resultsDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Assert.Equal(5, rows.Length);
            Assert.Equal("keyword 1005", rows[0].GetProperty("keyword").GetString());
            Assert.Equal(2005, rows[0].GetProperty("searchVolume").GetInt32());
            Assert.True(rows[0].GetProperty("monthlySearchVolume").TryGetProperty("2026-05", out _));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeCanceledWaitingExternalJobDoesNotIngestResults()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo", "content marketing" },
                    location = "JP",
                    language = "ja",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            using (var cancelResponse = await client.PostAsync($"/api/jobs/{jobId}/cancel", content: null))
            using (var cancelDocument = await ReadJsonAsync(cancelResponse))
            {
                Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
                Assert.Equal(StatusValues.Canceled, cancelDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                Assert.Equal(0, await dbContext.SearchVolumeResults.CountAsync(entity => entity.JobId == jobId));
                Assert.Equal(0, await dbContext.KeywordMetrics.CountAsync());
                Assert.Equal(0, factory.RakkoKeywordClient.ResultsCallCount);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> SeedProjectAsync(SearchVolumeApiFactory factory)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"Search Volume {projectId:N}",
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

    private static HttpClient CreateClient(SearchVolumeApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-search-volume-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class SearchVolumeApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public string StoragePath { get; } = CreateTempStoragePath();

        public SearchVolumeRakkoKeywordClient RakkoKeywordClient { get; } = new();

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
                services.RemoveAll<IRakkoKeywordClient>();
                services.AddSingleton<IRakkoKeywordClient>(RakkoKeywordClient);

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class SearchVolumeRakkoKeywordClient : IRakkoKeywordClient
    {
        private readonly Dictionary<long, IReadOnlyList<string>> requests = new();
        private long nextRequestId = 7000001;

        public int ResultsCallCount { get; private set; }

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
            RakkoKeywordClientContext context,
            RakkoSearchVolumeRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            var requestId = nextRequestId++;
            requests[requestId] = request.Keywords.ToArray();
            return Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeRegistration>.Success(
                new RakkoSearchVolumeRegistration(requestId),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall("/v1/search-volume", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
            RakkoKeywordClientContext context,
            long requestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeStatus>.Success(
                new RakkoSearchVolumeStatus(
                    IsCompleted: true,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["overall"] = "completed"
                    }),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall($"/v1/search-volume/{requestId}/status", null)));

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
            RakkoKeywordClientContext context,
            long requestId,
            RakkoSearchVolumeResultsRequest request,
            CancellationToken cancellationToken = default)
        {
            ResultsCallCount++;
            var keywords = requests[requestId];
            var items = keywords
                .Select((keyword, index) => new RakkoSearchVolumeResultItem(
                    keyword,
                    "IntegrationFake",
                    new RakkoKeywordMetrics(
                        SeoDifficulty: 10 + (index % 50),
                        SearchVolume: 1000 + index + 1,
                        Cpc: 0.5m + (index % 10) / 10m,
                        Competition: 5 + (index % 20),
                        FirstSeenRange: "last_30_days"),
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["2026-04"] = 900 + index + 1,
                        ["2026-05"] = 1000 + index + 1
                    }))
                .Take(request.Limit)
                .ToArray();

            return Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeResults>.Success(
                new RakkoSearchVolumeResults(items),
                consumedCredit: 5m,
                statusCode: 200,
                ExternalCall($"/v1/search-volume/{requestId}/results", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoSuggestKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRelatedKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoOtherKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
            RakkoKeywordClientContext context,
            RakkoQuestionSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRankingKeywordsRequest request,
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
