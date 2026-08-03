using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class DashboardIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DashboardIncludesPhase2CompetitorContentBriefRankAndAlertSummaries()
    {
        await using var factory = new DashboardApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Dashboard Phase2");
        var noisyProjectId = await SeedProjectAsync(factory, "Dashboard Phase2 Other");
        var emptyProjectId = await SeedProjectAsync(factory, "Dashboard Phase2 Empty");

        try
        {
            await SeedPhase2DashboardDataAsync(factory, projectId);
            await SeedOtherProjectNoiseAsync(factory, noisyProjectId);

            using (var response = await client.GetAsync($"/api/projects/{projectId}/dashboard"))
            using (var document = await ReadJsonAsync(response))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var data = document.RootElement.GetProperty("data");

                Assert.Equal(12, data.GetProperty("consumedCredit").GetInt32());
                Assert.Equal(1, data.GetProperty("runningJobCount").GetInt32());
                Assert.Equal(1, data.GetProperty("failedJobCount").GetInt32());
                Assert.Equal(1, data.GetProperty("notificationFailureCount").GetInt32());

                var competitors = data.GetProperty("competitorSummary");
                Assert.Equal(2, competitors.GetProperty("competitorCount").GetInt32());
                Assert.Equal(1, competitors.GetProperty("savedCompetitorCount").GetInt32());
                Assert.Equal(0.3m, competitors.GetProperty("averageDuplicateRate").GetDecimal());
                Assert.Equal(300m, competitors.GetProperty("estimatedTraffic").GetDecimal());
                Assert.Equal(30m, competitors.GetProperty("trafficValue").GetDecimal());

                var influx = data.GetProperty("influxSummary");
                Assert.Equal(2, influx.GetProperty("keywordCount").GetInt32());
                Assert.Equal(1, influx.GetProperty("gapKeywordCount").GetInt32());
                Assert.Equal(2, influx.GetProperty("pageCount").GetInt32());
                Assert.Equal(215m, influx.GetProperty("estimatedTraffic").GetDecimal());
                Assert.Equal(350m, influx.GetProperty("trafficValue").GetDecimal());

                var content = data.GetProperty("contentAnalysisSummary");
                Assert.Equal(1, content.GetProperty("keywordCount").GetInt32());
                Assert.Equal(1, content.GetProperty("contentResultCount").GetInt32());
                Assert.Equal(1, content.GetProperty("headlinePageCount").GetInt32());
                Assert.Equal(1, content.GetProperty("coOccurrenceWordCount").GetInt32());

                var briefs = data.GetProperty("briefSummary");
                Assert.Equal(2, briefs.GetProperty("briefCount").GetInt32());
                Assert.Equal(1, briefs.GetProperty("draftCount").GetInt32());
                Assert.Equal(1, briefs.GetProperty("pendingReviewCount").GetInt32());
                Assert.Equal(1, briefs.GetProperty("reviewedCount").GetInt32());

                var ranks = data.GetProperty("rankSummary");
                Assert.Equal(1, ranks.GetProperty("rankCheckJobCount").GetInt32());
                Assert.Equal(5, ranks.GetProperty("rankResultCount").GetInt32());
                var distribution = ranks.GetProperty("distribution");
                Assert.Equal(1, distribution.GetProperty("top3").GetInt32());
                Assert.Equal(1, distribution.GetProperty("top10").GetInt32());
                Assert.Equal(1, distribution.GetProperty("top20").GetInt32());
                Assert.Equal(0, distribution.GetProperty("top50").GetInt32());
                Assert.Equal(1, distribution.GetProperty("top100").GetInt32());
                Assert.Equal(1, distribution.GetProperty("outOfRange").GetInt32());

                var alerts = data.GetProperty("rankAlertSummary");
                Assert.Equal(1, alerts.GetProperty("activeAlertCount").GetInt32());
                Assert.Equal(1, alerts.GetProperty("unresolvedEventCount").GetInt32());
                Assert.Equal(1, alerts.GetProperty("rankAlertNotificationCount").GetInt32());
            }

            using (var emptyResponse = await client.GetAsync($"/api/projects/{emptyProjectId}/dashboard"))
            using (var emptyDocument = await ReadJsonAsync(emptyResponse))
            {
                Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
                var emptyData = emptyDocument.RootElement.GetProperty("data");
                Assert.Equal(0, emptyData.GetProperty("competitorSummary").GetProperty("competitorCount").GetInt32());
                Assert.Equal(0, emptyData.GetProperty("contentAnalysisSummary").GetProperty("keywordCount").GetInt32());
                Assert.Equal(0, emptyData.GetProperty("briefSummary").GetProperty("briefCount").GetInt32());
                Assert.Equal(0, emptyData.GetProperty("rankSummary").GetProperty("rankResultCount").GetInt32());
                Assert.Equal(0, emptyData.GetProperty("rankAlertSummary").GetProperty("unresolvedEventCount").GetInt32());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> SeedProjectAsync(DashboardApiFactory factory, string prefix)
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

    private static async Task SeedOtherProjectNoiseAsync(DashboardApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.CompetitiveResults.Add(new CompetitiveResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SiteDomain = "noise.example",
            DuplicateRate = 0.99m,
            EstimatedTraffic = 999m,
            TrafficValue = 999m,
            KeywordCount = 999,
            UniqueCountsJson = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPhase2DashboardDataAsync(DashboardApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var influxKeywordId = Keyword("phase2 influx keyword", now);
        var ownKeywordId = Keyword("phase2 own keyword", now);
        var contentKeywordId = Keyword("phase2 content keyword", now);
        var rankKeywordId = Keyword("phase2 rank keyword", now);
        var rankJobId = Guid.NewGuid();
        var rankChannelId = Guid.NewGuid();
        var rankAlertId = Guid.NewGuid();
        var rankDeliveryId = Guid.NewGuid();

        dbContext.Keywords.AddRange(influxKeywordId, ownKeywordId, contentKeywordId, rankKeywordId);
        dbContext.Sites.Add(new SiteEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Domain = "own.example",
            CanonicalUrl = "https://own.example/",
            Type = "own",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.CompetitiveResults.AddRange(
            new CompetitiveResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SiteDomain = "competitor-a.example",
                DuplicateRate = 0.4m,
                EstimatedTraffic = 100m,
                TrafficValue = 10m,
                KeywordCount = 10,
                UniqueCountsJson = "{}",
                CreatedAt = now
            },
            new CompetitiveResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SiteDomain = "competitor-b.example",
                DuplicateRate = 0.2m,
                EstimatedTraffic = 200m,
                TrafficValue = 20m,
                KeywordCount = 20,
                UniqueCountsJson = "{}",
                CreatedAt = now
            });
        dbContext.CompetitorSites.Add(new CompetitorSiteEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Domain = "competitor-a.example",
            Source = "competitive",
            DuplicateRate = 0.4m,
            EstimatedTraffic = 100m,
            CreatedAt = now
        });
        dbContext.InfluxKeywordResults.AddRange(
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "competitor-a.example",
                KeywordId = influxKeywordId.Id,
                Rank = 4,
                RankedUrl = "https://competitor-a.example/guide",
                EstimatedTraffic = 50m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "own.example",
                KeywordId = ownKeywordId.Id,
                Rank = 2,
                RankedUrl = "https://own.example/guide",
                EstimatedTraffic = 25m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            });
        dbContext.InfluxPageResults.AddRange(
            new InfluxPageResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "competitor-a.example",
                PageUrl = "https://competitor-a.example/guide",
                Title = "Competitor guide",
                KeywordCount = 30,
                EstimatedTraffic = 100m,
                TrafficValue = 250m,
                TopKeywordId = influxKeywordId.Id,
                CreatedAt = now
            },
            new InfluxPageResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "own.example",
                PageUrl = "https://own.example/guide",
                Title = "Own guide",
                KeywordCount = 15,
                EstimatedTraffic = 40m,
                TrafficValue = 100m,
                TopKeywordId = ownKeywordId.Id,
                CreatedAt = now
            });
        dbContext.ContentSearchResults.Add(new ContentSearchResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = contentKeywordId.Id,
            Url = "https://content.example/seo",
            Domain = "content.example",
            Title = "SEO Content Benchmark",
            Description = "Benchmark content",
            EstimatedTraffic = 90m,
            TrafficValue = 300m,
            CreatedAt = now
        });
        dbContext.SerpHeadlinePages.Add(new SerpHeadlinePageEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = contentKeywordId.Id,
            Rank = 1,
            Url = "https://content.example/seo",
            Title = "SEO Content Benchmark",
            Description = "Benchmark content",
            HeadlineCount = 3,
            WordCount = 1200,
            CreatedAt = now
        });
        dbContext.CoOccurrenceWords.Add(new CoOccurrenceWordEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = contentKeywordId.Id,
            Word = "search intent",
            OccurrenceCountsJson = """{"body":4}""",
            SiteCountsJson = """{"content.example":1}""",
            CreatedAt = now
        });
        dbContext.ArticleBriefs.AddRange(
            new ArticleBriefEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Draft brief",
                TargetKeywordId = contentKeywordId.Id,
                CurrentVersion = 1,
                ContentJson = """{"targetKeyword":"phase2 content keyword"}""",
                ReviewStatus = StatusValues.Pending,
                Status = "draft",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ArticleBriefEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Reviewed brief",
                TargetKeywordId = contentKeywordId.Id,
                CurrentVersion = 1,
                ContentJson = """{"targetKeyword":"phase2 content keyword"}""",
                ReviewStatus = "reviewed",
                Status = StatusValues.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        dbContext.Jobs.AddRange(
            new JobEntity
            {
                Id = rankJobId,
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                ProjectId = projectId,
                JobType = "RegisterRankCheckJob",
                Status = StatusValues.Succeeded,
                Progress = 100,
                RetryCount = 0,
                RequestedBy = "developer",
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = now
            },
            new JobEntity
            {
                Id = Guid.NewGuid(),
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                ProjectId = projectId,
                JobType = "ContentAnalyzeJob",
                Status = StatusValues.Running,
                Progress = 25,
                RetryCount = 0,
                RequestedBy = "developer",
                CreatedAt = now,
                UpdatedAt = now
            },
            new JobEntity
            {
                Id = Guid.NewGuid(),
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                ProjectId = projectId,
                JobType = "CompetitorRefreshJob",
                Status = StatusValues.FailedRetryable,
                Progress = 50,
                RetryCount = 1,
                RequestedBy = "developer",
                CreatedAt = now,
                UpdatedAt = now
            });
        dbContext.RankCheckJobs.Add(new RankCheckJobEntity
        {
            JobId = rankJobId,
            Depth = 100,
            MatchType = "domain",
            WithMetrics = true,
            RequestOptionsJson = "{}",
            StatusJson = "{}"
        });
        dbContext.RankResults.AddRange(
            RankResult(rankJobId, projectId, rankKeywordId.Id, 2, now),
            RankResult(rankJobId, projectId, rankKeywordId.Id, 7, now),
            RankResult(rankJobId, projectId, rankKeywordId.Id, 15, now),
            RankResult(rankJobId, projectId, rankKeywordId.Id, 55, now),
            RankResult(rankJobId, projectId, rankKeywordId.Id, 101, now));
        dbContext.NotificationChannels.Add(new NotificationChannelEntity
        {
            Id = rankChannelId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelType = "discord",
            Name = "Rank alerts",
            WebhookSecretRef = "rank-alert-webhook",
            EventTypesJson = """["rank_alert"]""",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.NotificationDeliveries.AddRange(
            new NotificationDeliveryEntity
            {
                Id = rankDeliveryId,
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                ProjectId = projectId,
                ChannelId = rankChannelId,
                JobId = rankJobId,
                ResourceType = "alert_event",
                ResourceId = "pending-alert-event-id",
                EventType = "rank_alert",
                PayloadHash = "rank-alert-payload",
                Status = StatusValues.Succeeded,
                RetryCount = 0,
                CreatedAt = now,
                SentAt = now,
                DeliveredAt = now
            },
            new NotificationDeliveryEntity
            {
                Id = Guid.NewGuid(),
                WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
                ProjectId = projectId,
                ChannelId = rankChannelId,
                EventType = "job_failed",
                PayloadHash = "job-failed-payload",
                Status = StatusValues.Retrying,
                RetryCount = 1,
                CreatedAt = now,
                NextRetryAt = now.AddMinutes(5)
            });
        dbContext.Alerts.AddRange(
            new AlertEntity
            {
                Id = rankAlertId,
                ProjectId = projectId,
                AlertType = "rank_drop",
                ConditionJson = """{"minDrop":3}""",
                NotificationChannelId = rankChannelId,
                Status = StatusValues.Active,
                LastTriggeredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            },
            new AlertEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AlertType = "entered_top10",
                ConditionJson = "{}",
                Status = StatusValues.Disabled,
                CreatedAt = now,
                UpdatedAt = now
            });
        dbContext.AlertEvents.AddRange(
            new AlertEventEntity
            {
                Id = Guid.NewGuid(),
                AlertId = rankAlertId,
                ProjectId = projectId,
                JobId = rankJobId,
                KeywordId = rankKeywordId.Id,
                EventType = "rank_drop",
                PreviousValueJson = """{"position":3}""",
                CurrentValueJson = """{"position":8}""",
                EvidenceJson = "{}",
                NotificationDeliveryId = rankDeliveryId,
                TriggeredAt = now
            },
            new AlertEventEntity
            {
                Id = Guid.NewGuid(),
                AlertId = rankAlertId,
                ProjectId = projectId,
                JobId = rankJobId,
                KeywordId = rankKeywordId.Id,
                EventType = "rank_drop",
                PreviousValueJson = """{"position":6}""",
                CurrentValueJson = """{"position":7}""",
                EvidenceJson = "{}",
                TriggeredAt = now.AddDays(-1),
                ResolvedAt = now
            });
        dbContext.ExternalApiCalls.Add(new ExternalApiCallEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            JobId = rankJobId,
            Provider = "rakko_keyword",
            Endpoint = "/v1/search-rank/rank-request-1/results",
            RequestHash = "request-hash",
            RequestUri = "storage://local/request.json.gz",
            ResponseHash = "response-hash",
            ResponseUri = "storage://local/response.json.gz",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CacheHit = false,
            StatusCode = 200,
            ConsumedCredit = 12.8m,
            DurationMs = 42,
            Actor = "developer",
            RetainedUntil = now.AddDays(30),
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync();
    }

    private static KeywordEntity Keyword(string text, DateTime createdAt)
    {
        var normalized = text.ToLowerInvariant();
        return new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = normalized,
            Language = "ja",
            TextHash = HashText(normalized),
            CreatedAt = createdAt
        };
    }

    private static RankResultEntity RankResult(
        Guid jobId,
        Guid projectId,
        Guid keywordId,
        int position,
        DateTime checkedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ProjectId = projectId,
            KeywordId = keywordId,
            Target = "example.com",
            Position = position,
            RankedUrl = $"https://example.com/rank-{position}",
            EstimatedTraffic = 10m,
            MetricsSnapshotJson = "{}",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CheckedAt = checkedAt
        };

    private static HttpClient CreateClient(DashboardApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-dashboard-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class DashboardApiFactory : ServiceKeyApiFactory
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

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }
}
