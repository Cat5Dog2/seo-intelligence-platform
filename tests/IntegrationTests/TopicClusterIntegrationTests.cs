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

public sealed class TopicClusterIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicClusterGenerateJobPersistsHierarchyAndApiReturnsEvidence()
    {
        await using var factory = new TopicClusterApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectEvidenceAsync(factory, "Topic Cluster");
        var otherProjectId = await SeedProjectAsync(factory, "Topic Cluster Other");

        try
        {
            using var generateResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/clusters/generate",
                new { regenerate = true });
            using var generateDocument = await ReadJsonAsync(generateResponse);

            Assert.Equal(HttpStatusCode.Accepted, generateResponse.StatusCode);
            var jobId = generateDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, generateDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

            await DispatchAsync(factory, jobId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.True(await dbContext.TopicClusters.CountAsync(entity => entity.ProjectId == projectId) >= 2);
                Assert.True(await dbContext.ClusterKeywords.CountAsync() >= 3);
            }

            Guid rootClusterId;
            using (var clustersResponse = await client.GetAsync(
                $"/api/projects/{projectId}/clusters?sortBy=score&orderBy=desc&pageSize=20"))
            using (var clustersDocument = await ReadJsonAsync(clustersResponse))
            {
                Assert.Equal(HttpStatusCode.OK, clustersResponse.StatusCode);
                Assert.True(clustersDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64() >= 2);
                var clusters = clustersDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
                Assert.Contains(clusters, cluster => cluster.GetProperty("parentId").ValueKind == JsonValueKind.Null);
                Assert.Contains(clusters, cluster => cluster.GetProperty("parentId").ValueKind == JsonValueKind.String);

                var root = clusters.First(cluster => cluster.GetProperty("parentId").ValueKind == JsonValueKind.Null);
                rootClusterId = root.GetProperty("clusterId").GetGuid();
                Assert.Equal("SEO", root.GetProperty("name").GetString());
                Assert.True(root.GetProperty("keywordCount").GetInt32() >= 3);
                Assert.True(root.GetProperty("score").GetDecimal() > 0m);
                Assert.True(root.GetProperty("childCount").GetInt32() >= 1);
                Assert.Equal("SEO guide", root.GetProperty("representativeKeyword").GetString());
                Assert.NotEmpty(root.GetProperty("articleCandidates").EnumerateArray());
            }

            using (var detailResponse = await client.GetAsync($"/api/projects/{projectId}/clusters/{rootClusterId}"))
            using (var detailDocument = await ReadJsonAsync(detailResponse))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var detail = detailDocument.RootElement.GetProperty("data");
                var keywords = detail.GetProperty("keywords").EnumerateArray().ToArray();
                Assert.Contains(keywords, keyword => keyword.GetProperty("keyword").GetString() == "SEO guide");
                Assert.Contains(keywords, keyword => keyword.GetProperty("keyword").GetString() == "SEO pricing");
                Assert.Contains(
                    keywords,
                    keyword =>
                        keyword.GetProperty("evidence").GetProperty("lexicalSimilarity").GetDecimal() > 0m &&
                        keyword.GetProperty("evidence").GetProperty("faqCount").GetInt32() > 0);
                Assert.Contains(
                    keywords,
                    keyword => keyword.GetProperty("evidence").GetProperty("coRankingScore").GetDecimal() > 0m);
                Assert.NotEmpty(detail.GetProperty("children").EnumerateArray());
                Assert.NotEmpty(detail.GetProperty("articleCandidates").EnumerateArray());
                Assert.NotEmpty(detail.GetProperty("internalLinkCandidates").EnumerateArray());
            }

            using (var otherProjectResponse = await client.GetAsync($"/api/projects/{otherProjectId}/clusters"))
            using (var otherProjectDocument = await ReadJsonAsync(otherProjectResponse))
            {
                Assert.Equal(HttpStatusCode.OK, otherProjectResponse.StatusCode);
                Assert.Equal(0, otherProjectDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                Assert.Empty(otherProjectDocument.RootElement.GetProperty("data").EnumerateArray());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicClusterListRejectsInvalidSort()
    {
        await using var factory = new TopicClusterApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Topic Cluster Validation");

        try
        {
            using var response = await client.GetAsync($"/api/projects/{projectId}/clusters?sortBy=unknown");
            using var document = await ReadJsonAsync(response);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "sortBy");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task DispatchAsync(TopicClusterApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectEvidenceAsync(TopicClusterApiFactory factory, string prefix)
    {
        var projectId = await SeedProjectAsync(factory, prefix);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;

        var seedKeyword = Keyword("SEO", "ja", now);
        var guideKeyword = Keyword("SEO guide", "ja", now);
        var pricingKeyword = Keyword("SEO pricing", "ja", now);
        var tutorialKeyword = Keyword("SEO tutorial", "ja", now);
        var otherKeyword = Keyword("Other project keyword", "ja", now);
        dbContext.Keywords.AddRange(seedKeyword, guideKeyword, pricingKeyword, tutorialKeyword, otherKeyword);

        var seed = new KeywordSeedEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Seed = "SEO",
            Source = "test",
            Memo = """
            {
              "request": {
                "language": "ja"
              }
            }
            """,
            CreatedAt = now
        };
        dbContext.KeywordSeeds.Add(seed);
        dbContext.KeywordSuggestions.Add(new KeywordSuggestionEntity
        {
            Id = Guid.NewGuid(),
            SeedId = seed.Id,
            KeywordId = guideKeyword.Id,
            Engine = "google",
            SuggestClass = "guide",
            EngineCount = 1,
            CreatedAt = now
        });
        dbContext.RelatedKeywords.Add(new RelatedKeywordEntity
        {
            Id = Guid.NewGuid(),
            SeedId = seed.Id,
            KeywordId = pricingKeyword.Id,
            MatchType = "partialMatch",
            MetricsSnapshotJson = "{}",
            CreatedAt = now
        });
        dbContext.RankingKeywords.AddRange(
            new RankingKeywordEntity
            {
                Id = Guid.NewGuid(),
                SeedKeywordId = seedKeyword.Id,
                KeywordId = guideKeyword.Id,
                WordCount = 2,
                Relevance = 88m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new RankingKeywordEntity
            {
                Id = Guid.NewGuid(),
                SeedKeywordId = seedKeyword.Id,
                KeywordId = pricingKeyword.Id,
                WordCount = 2,
                Relevance = 72m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new RankingKeywordEntity
            {
                Id = Guid.NewGuid(),
                SeedKeywordId = seedKeyword.Id,
                KeywordId = tutorialKeyword.Id,
                WordCount = 2,
                Relevance = 81m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            });
        dbContext.LsiPaaItems.Add(new LsiPaaItemEntity
        {
            Id = Guid.NewGuid(),
            SeedKeywordId = seedKeyword.Id,
            Type = "paa",
            KeywordId = tutorialKeyword.Id,
            QuestionText = "How does SEO work?",
            Importance = 0.8m,
            CreatedAt = now
        });
        dbContext.Questions.Add(new QuestionEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SeedKeywordId = seedKeyword.Id,
            QuestionText = "SEOとは何ですか",
            Source = "question",
            Importance = 0.7m,
            CreatedAt = now
        });
        dbContext.ProjectKeywordScores.AddRange(
            Score(projectId, guideKeyword.Id, 74m, now),
            Score(projectId, pricingKeyword.Id, 62m, now),
            Score(projectId, tutorialKeyword.Id, 55m, now));
        dbContext.InfluxKeywordResults.AddRange(
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "own.example",
                KeywordId = guideKeyword.Id,
                Rank = 3,
                RankedUrl = "https://own.example/seo-guide",
                EstimatedTraffic = 120m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "own.example",
                KeywordId = tutorialKeyword.Id,
                Rank = 4,
                RankedUrl = "https://own.example/seo-guide",
                EstimatedTraffic = 80m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "other.example",
                KeywordId = otherKeyword.Id,
                Rank = 1,
                RankedUrl = "https://other.example/leak",
                EstimatedTraffic = 999m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            });
        dbContext.CoOccurrenceWords.Add(new CoOccurrenceWordEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = guideKeyword.Id,
            Word = "search intent",
            OccurrenceCountsJson = "{}",
            SiteCountsJson = "{}",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static async Task<Guid> SeedProjectAsync(TopicClusterApiFactory factory, string prefix)
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

    private static KeywordEntity Keyword(string text, string language, DateTime createdAt)
    {
        var normalized = KeywordNormalizer.Normalize(text);
        return new KeywordEntity
        {
            Id = Guid.NewGuid(),
            NormalizedText = normalized,
            Language = language,
            TextHash = HashText(normalized),
            CreatedAt = createdAt
        };
    }

    private static ProjectKeywordScoreEntity Score(Guid projectId, Guid keywordId, decimal score, DateTime scoredAt)
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

    private static HttpClient CreateClient(TopicClusterApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-topic-cluster-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class TopicClusterApiFactory : WebApplicationFactory<Program>
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
