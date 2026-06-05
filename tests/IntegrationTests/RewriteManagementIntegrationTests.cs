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
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

namespace IntegrationTests;

public sealed class RewriteManagementIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CannibalizationRefreshDetectsCandidatesAndCreatesPrioritizedRewriteTasks()
    {
        await using var factory = new RewriteManagementApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Rewrite Management");
        var otherProjectId = await SeedProjectAsync(factory, "Rewrite Management Other");
        await SeedRewriteEvidenceAsync(factory, projectId);
        await SeedOtherProjectNoiseAsync(factory, otherProjectId);

        using var refreshResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/cannibalization/refresh", new { });
        using var refreshDocument = await ReadJsonAsync(refreshResponse);

        Assert.Equal(HttpStatusCode.Accepted, refreshResponse.StatusCode);
        var jobId = refreshDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
        Assert.Equal(jobId, refreshDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

        await DispatchAsync(factory, jobId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
            Assert.Equal(StatusValues.Succeeded, job.Status);
            Assert.Equal(1, await dbContext.CannibalizationCandidates.CountAsync(entity => entity.ProjectId == projectId && entity.Status == StatusValues.Active));
            Assert.Equal(2, await dbContext.RewriteTasks.CountAsync(entity => entity.ProjectId == projectId && entity.Status == StatusValues.Active));
        }

        using (var candidatesResponse = await client.GetAsync(
            $"/api/projects/{projectId}/cannibalization/candidates?q=seo&sortBy=severityScore&orderBy=desc"))
        using (var candidatesDocument = await ReadJsonAsync(candidatesResponse))
        {
            Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);
            Assert.Equal(1, candidatesDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            var candidate = Assert.Single(candidatesDocument.RootElement.GetProperty("data").EnumerateArray());
            Assert.Equal("seo rewrite", candidate.GetProperty("keyword").GetString());
            Assert.Equal("https://example.com/seo-guide", candidate.GetProperty("primaryUrl").GetString());
            Assert.True(candidate.GetProperty("severityScore").GetDecimal() > 0m);
            Assert.Contains(
                candidate.GetProperty("competingUrls").EnumerateArray(),
                item => item.GetProperty("url").GetString() == "https://example.com/seo-tips");
            Assert.Equal(2, candidate.GetProperty("evidence").GetProperty("detectedUrlCount").GetInt32());
            Assert.Contains(
                candidate.GetProperty("evidence").GetProperty("rankingHistory").EnumerateArray(),
                item => item.GetProperty("positions").GetArrayLength() >= 1);
            Assert.Equal("consolidate_or_canonicalize", candidate.GetProperty("recommendation").GetProperty("action").GetString());
        }

        Guid taskId;
        using (var tasksResponse = await client.GetAsync(
            $"/api/projects/{projectId}/rewrite/tasks?q=seo&sortBy=priorityScore&orderBy=desc"))
        using (var tasksDocument = await ReadJsonAsync(tasksResponse))
        {
            Assert.Equal(HttpStatusCode.OK, tasksResponse.StatusCode);
            Assert.Equal(2, tasksDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            var topTask = tasksDocument.RootElement.GetProperty("data").EnumerateArray().First();
            taskId = topTask.GetProperty("taskId").GetGuid();
            Assert.True(topTask.GetProperty("priorityScore").GetDecimal() > 0m);
            Assert.Equal("active", topTask.GetProperty("status").GetString());
            Assert.Equal("developer", topTask.GetProperty("assigneeActor").GetString());
            var reason = topTask.GetProperty("reason");
            Assert.True(reason.GetProperty("hasCannibalization").GetBoolean());
            Assert.Contains(
                reason.GetProperty("keywords").EnumerateArray(),
                item => item.GetProperty("keyword").GetString() == "seo rewrite");
        }

        using (var taskResponse = await client.GetAsync($"/api/projects/{projectId}/rewrite/tasks/{taskId}"))
        using (var taskDocument = await ReadJsonAsync(taskResponse))
        {
            Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
            Assert.Equal(taskId, taskDocument.RootElement.GetProperty("data").GetProperty("taskId").GetGuid());
        }

        using (var updateResponse = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/rewrite/tasks/{taskId}",
            new
            {
                status = "completed",
                priorityScore = 91.25m,
                assigneeActor = "developer",
                memo = "Reviewed cannibalization rewrite."
            }))
        using (var updateDocument = await ReadJsonAsync(updateResponse))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = updateDocument.RootElement.GetProperty("data");
            Assert.Equal("completed", updated.GetProperty("status").GetString());
            Assert.Equal(91.25m, updated.GetProperty("priorityScore").GetDecimal());
            Assert.Equal("Reviewed cannibalization rewrite.", updated.GetProperty("memo").GetString());
        }

        using (var otherCandidatesResponse = await client.GetAsync($"/api/projects/{otherProjectId}/cannibalization/candidates"))
        using (var otherCandidatesDocument = await ReadJsonAsync(otherCandidatesResponse))
        {
            Assert.Equal(HttpStatusCode.OK, otherCandidatesResponse.StatusCode);
            Assert.Equal(0, otherCandidatesDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
        }

        using (var otherTasksResponse = await client.GetAsync($"/api/projects/{otherProjectId}/rewrite/tasks"))
        using (var otherTasksDocument = await ReadJsonAsync(otherTasksResponse))
        {
            Assert.Equal(HttpStatusCode.OK, otherTasksResponse.StatusCode);
            Assert.Equal(0, otherTasksDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RewriteTaskUpdateRejectsInvalidStatusAndPriorityScore()
    {
        await using var factory = new RewriteManagementApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Rewrite Validation");
        var taskId = await SeedRewriteTaskAsync(factory, projectId);

        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/rewrite/tasks/{taskId}",
            new
            {
                status = "disabled",
                priorityScore = 120m
            });
        using var document = await ReadJsonAsync(response);
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errors, error => error.GetProperty("target").GetString() == "status");
        Assert.Contains(errors, error => error.GetProperty("target").GetString() == "priorityScore");
        Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
    }

    private static async Task DispatchAsync(RewriteManagementApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectAsync(RewriteManagementApiFactory factory, string prefix)
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

    private static async Task SeedRewriteEvidenceAsync(RewriteManagementApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var keyword = new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = "seo rewrite",
            Language = "ja",
            TextHash = HashText("seo rewrite"),
            CreatedAt = now
        };
        var secondaryKeyword = new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = "seo content update",
            Language = "ja",
            TextHash = HashText("seo content update"),
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

        dbContext.Keywords.AddRange(keyword, secondaryKeyword);
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
        dbContext.RankResults.AddRange(
            RankResult(job.Id, projectId, keyword.Id, "example.com", 8, "https://example.com/seo-guide", 120m, now),
            RankResult(job.Id, projectId, keyword.Id, "example.com", 10, "https://example.com/seo-tips", 80m, now),
            RankResult(job.Id, projectId, keyword.Id, "example.com", 11, "https://example.com/seo-guide", 95m, now.AddDays(-7)),
            RankResult(job.Id, projectId, keyword.Id, "example.com", 7, "https://example.com/seo-tips", 85m, now.AddDays(-7)),
            RankResult(job.Id, projectId, secondaryKeyword.Id, "example.com", 12, "https://example.com/seo-guide", 60m, now));
        dbContext.KeywordMetrics.AddRange(
            Metric(keyword.Id, 2400, 34m, now),
            Metric(secondaryKeyword.Id, 900, 28m, now));
        dbContext.ProjectKeywordScores.AddRange(
            ProjectScore(projectId, keyword.Id, 87m, now),
            ProjectScore(projectId, secondaryKeyword.Id, 70m, now));
        dbContext.ContentSearchResults.Add(new ContentSearchResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keyword.Id,
            Url = "https://example.com/seo-guide",
            Domain = "example.com",
            Title = "SEO guide",
            Description = "SEO guide",
            EstimatedTraffic = 120m,
            TrafficValue = 300m,
            CreatedAt = now
        });
        dbContext.SerpHeadlinePages.AddRange(
            new SerpHeadlinePageEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = keyword.Id,
                Rank = 8,
                Url = "https://example.com/seo-guide",
                Title = "SEO guide",
                Description = "SEO guide",
                HeadlineCount = 2,
                WordCount = 1500,
                CreatedAt = now
            },
            new SerpHeadlinePageEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = keyword.Id,
                Rank = 1,
                Url = "https://competitor.example/seo",
                Title = "Competitor SEO",
                Description = "Competitor SEO",
                HeadlineCount = 8,
                WordCount = 3000,
                CreatedAt = now
            });
        var coWord = new CoOccurrenceWordEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keyword.Id,
            Word = "search intent",
            OccurrenceCountsJson = "{}",
            SiteCountsJson = "{}",
            CreatedAt = now
        };
        dbContext.CoOccurrenceWords.Add(coWord);
        dbContext.CoOccurrencePageDetails.AddRange(
            new CoOccurrencePageDetailEntity
            {
                Id = Guid.NewGuid(),
                CoWordId = coWord.Id,
                Rank = 1,
                Url = "https://competitor.example/seo",
                Title = "Competitor SEO",
                Count = 10,
                CountInHeadline = 2,
                CountInTitle = 1
            },
            new CoOccurrencePageDetailEntity
            {
                Id = Guid.NewGuid(),
                CoWordId = coWord.Id,
                Rank = 8,
                Url = "https://example.com/seo-guide",
                Title = "SEO guide",
                Count = 1,
                CountInHeadline = 0,
                CountInTitle = 0
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedOtherProjectNoiseAsync(RewriteManagementApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var keyword = new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = "noise keyword",
            Language = "ja",
            TextHash = HashText("noise keyword"),
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
        dbContext.RankResults.Add(RankResult(job.Id, projectId, keyword.Id, "noise.example", 6, "https://noise.example/a", 50m, now));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedRewriteTaskAsync(RewriteManagementApiFactory factory, Guid projectId)
    {
        var taskId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        dbContext.RewriteTasks.Add(new RewriteTaskEntity
        {
            Id = taskId,
            ProjectId = projectId,
            TargetUrl = "https://example.com/existing",
            PriorityScore = 30m,
            ReasonJson = "{}",
            Status = StatusValues.Active,
            AssigneeActor = "developer",
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        return taskId;
    }

    private static RankResultEntity RankResult(
        Guid jobId,
        Guid projectId,
        Guid keywordId,
        string target,
        int position,
        string rankedUrl,
        decimal estimatedTraffic,
        DateTime checkedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ProjectId = projectId,
            KeywordId = keywordId,
            Target = target,
            Position = position,
            RankedUrl = rankedUrl,
            EstimatedTraffic = estimatedTraffic,
            MetricsSnapshotJson = "{}",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CheckedAt = checkedAt
        };

    private static KeywordMetricEntity Metric(Guid keywordId, int searchVolume, decimal seoDifficulty, DateTime fetchedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            KeywordId = keywordId,
            Location = "JP",
            Language = "ja",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            SearchVolume = searchVolume,
            SeoDifficulty = seoDifficulty,
            Cpc = 1.0m,
            Competition = 10m,
            FetchedAt = fetchedAt
        };

    private static ProjectKeywordScoreEntity ProjectScore(Guid projectId, Guid keywordId, decimal score, DateTime scoredAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            Location = "JP",
            Language = "ja",
            OpportunityScore = score,
            ScoreComponentsJson = "{}",
            ScoredAt = scoredAt
        };

    private static HttpClient CreateClient(RewriteManagementApiFactory factory)
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

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(KeywordNormalizer.Normalize(value)))).ToLowerInvariant();

    private sealed class RewriteManagementApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

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
                    ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "seo-intelligence-rewrite-tests", databaseName),
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
