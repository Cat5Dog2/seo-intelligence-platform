using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class RankMonitoringIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RankCheckPersistsResultsDistributionAlertEventsAndRankAlertDelivery()
    {
        await using var factory = new RankMonitoringApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Rank Monitoring");
        var otherProjectId = await SeedProjectAsync(factory, "Rank Monitoring Other");
        var channelId = await SeedRankAlertChannelAsync(factory, projectId);
        await SeedPreviousRankResultAsync(factory, projectId, "seo", "example.com", position: 3);

        try
        {
            using (var alertResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/alerts",
                new
                {
                    alertType = "rank_drop",
                    condition = new { minDrop = 3 },
                    notificationChannelId = channelId
                }))
            using (var alertDocument = await ReadJsonAsync(alertResponse))
            {
                Assert.Equal(HttpStatusCode.Created, alertResponse.StatusCode);
                Assert.Equal("rank_drop", alertDocument.RootElement.GetProperty("data").GetProperty("alertType").GetString());
            }

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/rank-check/jobs",
                new
                {
                    keywords = new[] { " seo ", "seo" },
                    targets = new[]
                    {
                        new { target = "https://example.com", targetType = "domain" }
                    },
                    matchType = "domain",
                    depth = 100,
                    withMetrics = true,
                    deduplicate = true
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

            await DispatchAsync(factory, jobId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);

                Assert.Equal(StatusValues.WaitingExternal, job.Status);
                Assert.Equal(StatusValues.WaitingExternal, externalRequest.Status);
                Assert.Equal("rank-request-9000001", externalRequest.ExternalRequestId);
                Assert.Single(await dbContext.RankCheckTargets.AsNoTracking().Where(entity => entity.JobId == jobId).ToArrayAsync());
            }

            await DispatchAsync(factory, jobId);

            Guid alertEventId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                var evaluationJob = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.JobType == "RankAlertEvaluateJob");
                var currentResult = await dbContext.RankResults.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                var alertEvent = await dbContext.AlertEvents.AsNoTracking().SingleAsync(entity => entity.ProjectId == projectId);
                var delivery = await dbContext.NotificationDeliveries.AsNoTracking().SingleAsync(entity => entity.EventType == "rank_alert");

                alertEventId = alertEvent.Id;
                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.Equal(StatusValues.Succeeded, externalRequest.Status);
                Assert.Equal(StatusValues.Succeeded, evaluationJob.Status);
                Assert.Equal("seo", (await dbContext.Keywords.AsNoTracking().SingleAsync(entity => entity.Id == currentResult.KeywordId)).NormalizedText);
                Assert.Equal("example.com", currentResult.Target);
                Assert.Equal(8, currentResult.Position);
                Assert.Equal("https://example.com/seo", currentResult.RankedUrl);
                Assert.Equal("rank_drop", alertEvent.EventType);
                Assert.Equal(jobId, alertEvent.JobId);
                Assert.Equal(delivery.Id, alertEvent.NotificationDeliveryId);
                Assert.Equal("alert_event", delivery.ResourceType);
                Assert.Equal(alertEvent.Id.ToString("D"), delivery.ResourceId);
                Assert.Equal(jobId, delivery.JobId);
            }

            using (var resultsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/rank-check/jobs/{jobId}/results?page=1&pageSize=10&sortBy=position&orderBy=asc"))
            using (var resultsDocument = await ReadJsonAsync(resultsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, resultsResponse.StatusCode);
                var row = Assert.Single(resultsDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("seo", row.GetProperty("keyword").GetString());
                Assert.Equal("example.com", row.GetProperty("target").GetString());
                Assert.Equal(8, row.GetProperty("position").GetInt32());
                Assert.Equal(3, row.GetProperty("previousPosition").GetInt32());
                Assert.Equal(5, row.GetProperty("positionDelta").GetInt32());
            }

            using (var rankResultsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/rank-results?jobId={jobId}&page=1&pageSize=10"))
            using (var rankResultsDocument = await ReadJsonAsync(rankResultsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, rankResultsResponse.StatusCode);
                var data = rankResultsDocument.RootElement.GetProperty("data");
                Assert.Equal(1, data.GetProperty("totalCount").GetInt64());
                Assert.Equal(1, data.GetProperty("distribution").GetProperty("top10").GetInt32());
                Assert.Equal(0, data.GetProperty("distribution").GetProperty("top3").GetInt32());
            }

            using (var alertEventsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/alert-events?eventType=rank_drop&sortBy=triggeredAt&orderBy=desc"))
            using (var alertEventsDocument = await ReadJsonAsync(alertEventsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, alertEventsResponse.StatusCode);
                var alertEvent = Assert.Single(alertEventsDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("rank_drop", alertEvent.GetProperty("eventType").GetString());
                Assert.Equal("seo", alertEvent.GetProperty("keyword").GetString());
                Assert.Equal(3, alertEvent.GetProperty("previousValue").GetProperty("position").GetInt32());
                Assert.Equal(8, alertEvent.GetProperty("currentValue").GetProperty("position").GetInt32());
                Assert.True(alertEvent.GetProperty("notificationDeliveryId").GetGuid() != Guid.Empty);
            }

            using (var otherProjectResultsResponse = await client.GetAsync($"/api/projects/{otherProjectId}/rank-results"))
            using (var otherProjectResultsDocument = await ReadJsonAsync(otherProjectResultsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, otherProjectResultsResponse.StatusCode);
                Assert.Equal(0, otherProjectResultsDocument.RootElement.GetProperty("data").GetProperty("totalCount").GetInt64());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RankCheckRejectsInvalidTargetsAndDepth()
    {
        await using var factory = new RankMonitoringApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Rank Validation");

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/rank-check/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    targets = Array.Empty<object>(),
                    depth = 25
                });
            using var document = await ReadJsonAsync(response);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "targets");
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "depth");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task DispatchAsync(RankMonitoringApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectAsync(RankMonitoringApiFactory factory, string prefix)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"{prefix} {projectId:N}",
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

    private static async Task<Guid> SeedRankAlertChannelAsync(RankMonitoringApiFactory factory, Guid projectId)
    {
        var channelId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        dbContext.NotificationChannels.Add(new NotificationChannelEntity
        {
            Id = channelId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelType = "discord",
            Name = "Rank alerts",
            WebhookSecretRef = "missing-rank-alert-webhook",
            EventTypesJson = """["rank_alert"]""",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        return channelId;
    }

    private static async Task SeedPreviousRankResultAsync(
        RankMonitoringApiFactory factory,
        Guid projectId,
        string keywordText,
        string target,
        int position)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow.AddDays(-1);
        var keyword = new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = KeywordNormalizer.Normalize(keywordText),
            Language = "ja",
            TextHash = HashText(KeywordNormalizer.Normalize(keywordText)),
            CreatedAt = now
        };
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            JobType = "RegisterRankCheckJob",
            Status = StatusValues.Succeeded,
            Progress = 100,
            RetryCount = 0,
            ResultResourceType = "rank_check_job",
            RequestedBy = "developer",
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = now
        };
        job.ResultResourceId = job.Id;
        dbContext.Keywords.Add(keyword);
        dbContext.Jobs.Add(job);
        dbContext.RankCheckJobs.Add(new RankCheckJobEntity
        {
            JobId = job.Id,
            Depth = 100,
            MatchType = "domain",
            WithMetrics = true,
            RequestOptionsJson = "{}",
            StatusJson = "{}"
        });
        dbContext.RankResults.Add(new RankResultEntity
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            ProjectId = projectId,
            KeywordId = keyword.Id,
            Target = target,
            Position = position,
            RankedUrl = "https://example.com/previous",
            EstimatedTraffic = 100m,
            MetricsSnapshotJson = "{}",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CheckedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static HttpClient CreateClient(RankMonitoringApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-rank-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class RankMonitoringApiFactory : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public string StoragePath { get; } = CreateTempStoragePath();

        public RankMonitoringRakkoKeywordClient RakkoKeywordClient { get; } = new();

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

    private sealed class RankMonitoringRakkoKeywordClient : IRakkoKeywordClient
    {
        private readonly Dictionary<string, RakkoSearchRankRegistrationRequest> requests = new(StringComparer.Ordinal);
        private int nextRequestId = 9000001;

        public Task<RakkoKeywordCallResult<RakkoSearchRankRegistration>> RegisterSearchRankAsync(
            RakkoKeywordClientContext context,
            RakkoSearchRankRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            var requestId = $"rank-request-{nextRequestId.ToString(CultureInfo.InvariantCulture)}";
            nextRequestId++;
            requests[requestId] = request;
            return Task.FromResult(RakkoKeywordCallResult<RakkoSearchRankRegistration>.Success(
                new RakkoSearchRankRegistration(requestId),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall("/v1/search-rank", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoSearchRankStatus>> GetSearchRankStatusAsync(
            RakkoKeywordClientContext context,
            string requestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoSearchRankStatus>.Success(
                new RakkoSearchRankStatus(
                    IsCompleted: true,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["overall"] = "completed"
                    }),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall($"/v1/search-rank/{requestId}/status", null)));

        public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetSearchRankResultsAsync(
            RakkoKeywordClientContext context,
            string requestId,
            RakkoSearchRankResultsRequest request,
            CancellationToken cancellationToken = default)
        {
            var registration = requests[requestId];
            var target = registration.Urls.Single();
            const decimal position = 8m;
            const decimal traffic = 75.5m;
            var rawJson = $$"""
            {
              "keyword": "seo",
              "metrics": { "seoDifficulty": 31, "searchVolume": 1200, "cpc": 0.9, "competition": 12 },
              "rankingPositionDistribution": { "top10": 1 },
              "rankings": [
                {
                  "target": "{{target}}",
                  "position": {{position.ToString(CultureInfo.InvariantCulture)}},
                  "rankedUrl": "https://example.com/seo",
                  "estimatedTraffic": {{traffic.ToString(CultureInfo.InvariantCulture)}}
                }
              ]
            }
            """;
            return Task.FromResult(RakkoKeywordCallResult<RakkoExternalSearchResults>.Success(
                new RakkoExternalSearchResults(
                    "search_rank_results",
                    [
                        new RakkoExternalSearchResultItem(
                            Keyword: "seo",
                            Target: target,
                            Url: "https://example.com/seo",
                            Domain: null,
                            Title: null,
                            Position: position,
                            EstimatedTraffic: traffic,
                            TrafficValue: null,
                            RawJson: rawJson)
                    ],
                    QueryJson: null,
                    SummaryJson: """{"rankingPositionDistribution":{"top10":1}}"""),
                consumedCredit: 4m,
                statusCode: 200,
                ExternalCall($"/v1/search-rank/{requestId}/results", null)));
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
