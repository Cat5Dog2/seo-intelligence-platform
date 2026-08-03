using System.Net;
using System.Net.Http.Json;
using System.Globalization;
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

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class CompetitiveAnalysisIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompetitorRefreshJobPersistsCompetitorsInfluxKeywordsPagesAndGapViews()
    {
        await using var factory = new CompetitiveAnalysisApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Competitive Analysis");
        var otherProjectId = await SeedProjectAsync(factory, "Competitive Analysis Other");

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/competitors/analyze",
                new
                {
                    target = "https://own.example/"
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

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
                Assert.Equal(1, await dbContext.CompetitiveResults.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(1, await dbContext.CompetitorSites.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(2, await dbContext.InfluxKeywordResults.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(2, await dbContext.InfluxPageResults.CountAsync(entity => entity.ProjectId == projectId));
                Assert.True(await dbContext.Sites.AnyAsync(entity =>
                    entity.ProjectId == projectId &&
                    entity.Domain == "own.example" &&
                    entity.Type == "own" &&
                    entity.Status == StatusValues.Active));
            }

            using (var competitorsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/competitors?page=1&pageSize=10&sortBy=gapKeywordCount&orderBy=desc"))
            using (var competitorsDocument = await ReadJsonAsync(competitorsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, competitorsResponse.StatusCode);
                Assert.Equal(1, competitorsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                var competitor = Assert.Single(competitorsDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("competitor.example", competitor.GetProperty("domain").GetString());
                Assert.Equal(0.42m, competitor.GetProperty("duplicateRate").GetDecimal());
                Assert.Equal(85, competitor.GetProperty("competitorUniqueKeywordCount").GetInt32());
                Assert.Equal(35, competitor.GetProperty("duplicateKeywordCount").GetInt32());
                Assert.True(competitor.GetProperty("saved").GetBoolean());
            }

            using (var keywordsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/influx-keywords?target=competitor.example&maxRank=10&sortBy=rank&orderBy=asc"))
            using (var keywordsDocument = await ReadJsonAsync(keywordsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, keywordsResponse.StatusCode);
                Assert.Equal(1, keywordsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                var keyword = Assert.Single(keywordsDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("gap keyword", keyword.GetProperty("keyword").GetString());
                Assert.Equal(3, keyword.GetProperty("rank").GetInt32());
                Assert.Equal("https://competitor.example/gap", keyword.GetProperty("rankedUrl").GetString());
                Assert.True(keyword.GetProperty("isGap").GetBoolean());
                Assert.Equal("competitor_unique", keyword.GetProperty("gapType").GetString());
                Assert.True(keyword.GetProperty("metrics").TryGetProperty("ranking", out _));
            }

            using (var pagesResponse = await client.GetAsync(
                $"/api/projects/{projectId}/influx-pages?q=competitor&sortBy=trafficValue&orderBy=desc"))
            using (var pagesDocument = await ReadJsonAsync(pagesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, pagesResponse.StatusCode);
                Assert.Equal(1, pagesDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                var page = Assert.Single(pagesDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("https://competitor.example/gap", page.GetProperty("pageUrl").GetString());
                Assert.Equal("Competitor Gap Page", page.GetProperty("title").GetString());
                Assert.Equal(44, page.GetProperty("keywordCount").GetInt32());
                Assert.Equal("gap keyword", page.GetProperty("topKeyword").GetString());
            }

            using (var otherProjectResponse = await client.GetAsync($"/api/projects/{otherProjectId}/competitors"))
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
    public async Task CompetitorAnalyzeRejectsMissingTarget()
    {
        await using var factory = new CompetitiveAnalysisApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Competitive Validation");

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/competitors/analyze",
                new { target = "" });
            using var document = await ReadJsonAsync(response);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "target");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> SeedProjectAsync(CompetitiveAnalysisApiFactory factory, string prefix)
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

    private static HttpClient CreateClient(CompetitiveAnalysisApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-competitive-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class CompetitiveAnalysisApiFactory : ServiceKeyApiFactory
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
                services.RemoveAll<IRakkoKeywordClient>();
                services.AddScoped<IRakkoKeywordClient, CompetitiveAnalysisRakkoKeywordClient>();

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class CompetitiveAnalysisRakkoKeywordClient : IRakkoKeywordClient
    {
        public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetCompetitiveSitesAsync(
            RakkoKeywordClientContext context,
            RakkoCompetitiveRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoExternalSearchResults>.Success(
                new RakkoExternalSearchResults(
                    "competitive",
                    [
                        new RakkoExternalSearchResultItem(
                            Keyword: null,
                            Target: request.Url,
                            Url: "https://competitor.example/",
                            Domain: "competitor.example",
                            Title: "Competitor Example",
                            Position: null,
                            EstimatedTraffic: 980.5m,
                            TrafficValue: 4200.75m,
                            RawJson: """
                            {
                              "site": { "domain": "competitor.example", "title": "Competitor Example" },
                              "metrics": {
                                "estimatedTraffic": 980.5,
                                "trafficValue": 4200.75,
                                "keywordCount": 120,
                                "pageCount": 18,
                                "duplicateKeywordCount": 35,
                                "duplicateRate": 0.42,
                                "competitorUniqueKeywordCount": 85,
                                "targetUniqueKeywordCount": 40
                              }
                            }
                            """)
                    ],
                    QueryJson: null,
                    SummaryJson: null),
                consumedCredit: 2m,
                statusCode: 200,
                ExternalCall("/v1/competitive", null)));

        public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoInfluxKeywordsRequest request,
            CancellationToken cancellationToken = default)
        {
            var items = request.Targets.Select(target =>
            {
                var isCompetitor = string.Equals(target.Url, "competitor.example", StringComparison.OrdinalIgnoreCase);
                var keyword = isCompetitor ? "gap keyword" : "owned keyword";
                var url = isCompetitor ? "https://competitor.example/gap" : "https://own.example/guide";
                var position = isCompetitor ? 3m : 2m;
                var traffic = isCompetitor ? 240.5m : 150.25m;
                return new RakkoExternalSearchResultItem(
                    keyword,
                    target.Url,
                    url,
                    Domain: null,
                    Title: null,
                    Position: position,
                    EstimatedTraffic: traffic,
                    TrafficValue: null,
                    RawJson: $$"""
                    {
                      "target": "{{target.Url}}",
                      "keyword": "{{keyword}}",
                      "metrics": { "seoDifficulty": 31, "searchVolume": 1200, "cpc": 0.9, "competition": 12 },
                      "ranking": { "position": {{position.ToString(CultureInfo.InvariantCulture)}}, "estimatedTraffic": {{traffic.ToString(CultureInfo.InvariantCulture)}}, "url": "{{url}}" }
                    }
                    """);
            }).ToArray();

            return Task.FromResult(RakkoKeywordCallResult<RakkoExternalSearchResults>.Success(
                new RakkoExternalSearchResults("influx_keywords", items, QueryJson: null, SummaryJson: null),
                consumedCredit: 3m,
                statusCode: 200,
                ExternalCall("/v1/influx-keywords", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoExternalSearchResults>> GetInfluxPagesAsync(
            RakkoKeywordClientContext context,
            RakkoInfluxPagesRequest request,
            CancellationToken cancellationToken = default)
        {
            var items = request.Targets.Select(target =>
            {
                var isCompetitor = string.Equals(target.Url, "competitor.example", StringComparison.OrdinalIgnoreCase);
                var keyword = isCompetitor ? "gap keyword" : "owned keyword";
                var url = isCompetitor ? "https://competitor.example/gap" : "https://own.example/guide";
                var title = isCompetitor ? "Competitor Gap Page" : "Owned Guide";
                var keywordCount = isCompetitor ? 44 : 18;
                var traffic = isCompetitor ? 360.5m : 220.25m;
                var value = isCompetitor ? 1900.75m : 850.5m;
                return new RakkoExternalSearchResultItem(
                    Keyword: keyword,
                    Target: target.Url,
                    Url: url,
                    Domain: null,
                    Title: title,
                    Position: null,
                    EstimatedTraffic: traffic,
                    TrafficValue: value,
                    RawJson: $$"""
                    {
                      "target": "{{target.Url}}",
                      "page": { "title": "{{title}}", "url": "{{url}}" },
                      "performance": { "rankingKeywordCount": {{keywordCount}}, "estimatedTraffic": {{traffic.ToString(CultureInfo.InvariantCulture)}}, "trafficValue": {{value.ToString(CultureInfo.InvariantCulture)}} },
                      "topKeyword": { "keyword": "{{keyword}}", "position": 3, "metrics": { "searchVolume": 1200 } }
                    }
                    """);
            }).ToArray();

            return Task.FromResult(RakkoKeywordCallResult<RakkoExternalSearchResults>.Success(
                new RakkoExternalSearchResults("influx_pages", items, QueryJson: null, SummaryJson: null),
                consumedCredit: 3m,
                statusCode: 200,
                ExternalCall("/v1/influx-pages", null)));
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
