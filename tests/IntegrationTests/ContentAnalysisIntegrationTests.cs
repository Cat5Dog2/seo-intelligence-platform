using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

namespace IntegrationTests;

public sealed class ContentAnalysisIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ContentAnalyzeAndBriefGenerationPersistEvidenceVersionsAndExport()
    {
        await using var factory = new ContentAnalysisApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Content Analysis");
        var otherProjectId = await SeedProjectAsync(factory, "Content Analysis Other");

        try
        {
            using var analyzeResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/content/analyze",
                new
                {
                    keyword = "seo content",
                    includeContentSearch = true,
                    includeHeadline = true,
                    includeCoOccurrence = true,
                    limit = 5
                });
            using var analyzeDocument = await ReadJsonAsync(analyzeResponse);

            Assert.Equal(HttpStatusCode.Accepted, analyzeResponse.StatusCode);
            var analyzeJobId = analyzeDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            await DispatchAsync(factory, analyzeJobId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == analyzeJobId);
                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(1, await dbContext.ContentSearchResults.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(1, await dbContext.SerpHeadlinePages.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(2, await dbContext.SerpHeadlines.CountAsync());
                Assert.Equal(1, await dbContext.CoOccurrenceWords.CountAsync(entity => entity.ProjectId == projectId));
                Assert.Equal(1, await dbContext.CoOccurrencePageDetails.CountAsync());
            }

            using (var analysesResponse = await client.GetAsync(
                $"/api/projects/{projectId}/content-analyses?q=seo&sortBy=lastAnalyzedAt&orderBy=desc"))
            using (var analysesDocument = await ReadJsonAsync(analysesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, analysesResponse.StatusCode);
                Assert.Equal(1, analysesDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                var analysis = Assert.Single(analysesDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("seo content", analysis.GetProperty("keyword").GetString());
                Assert.Single(analysis.GetProperty("contentResults").EnumerateArray());
                Assert.Single(analysis.GetProperty("headlinePages").EnumerateArray());
                Assert.Single(analysis.GetProperty("coOccurrences").EnumerateArray());
            }

            using var generateResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/briefs/generate",
                new
                {
                    targetKeyword = "seo content",
                    competitorUrls = new[] { "https://competitor.example/seo" }
                });
            using var generateDocument = await ReadJsonAsync(generateResponse);

            Assert.Equal(HttpStatusCode.Accepted, generateResponse.StatusCode);
            var generateJobId = generateDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            await DispatchAsync(factory, generateJobId);

            Guid briefId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == generateJobId);
                Assert.Equal(StatusValues.Succeeded, job.Status);
                var brief = await dbContext.ArticleBriefs.SingleAsync(entity => entity.ProjectId == projectId);
                briefId = brief.Id;
                Assert.Equal(1, brief.CurrentVersion);
                Assert.Equal("draft", brief.Status);
                Assert.Equal(StatusValues.Pending, brief.ReviewStatus);
                Assert.Equal(1, await dbContext.ArtifactVersions.CountAsync(entity =>
                    entity.ProjectId == projectId &&
                    entity.ArtifactType == "article_brief" &&
                    entity.ArtifactId == briefId));
            }

            using (var briefResponse = await client.GetAsync($"/api/projects/{projectId}/briefs/{briefId}"))
            using (var briefDocument = await ReadJsonAsync(briefResponse))
            {
                Assert.Equal(HttpStatusCode.OK, briefResponse.StatusCode);
                var brief = briefDocument.RootElement.GetProperty("data");
                Assert.Equal(1, brief.GetProperty("currentVersion").GetInt32());
                Assert.Equal("seo content", brief.GetProperty("targetKeyword").GetString());
                var content = brief.GetProperty("content");
                Assert.Equal("seo content", content.GetProperty("targetKeyword").GetString());
                Assert.Contains(
                    content.GetProperty("requiredTerms").EnumerateArray(),
                    term => term.GetString() == "search intent");
                Assert.Contains(
                    content.GetProperty("competitorUrls").EnumerateArray(),
                    url => url.GetString() == "https://competitor.example/seo");
            }

            using (var updateResponse = await client.PutAsJsonAsync(
                $"/api/projects/{projectId}/briefs/{briefId}",
                new
                {
                    title = "Updated SEO Brief",
                    reviewStatus = "reviewed",
                    content = new
                    {
                        targetKeyword = "seo content",
                        notes = "reviewed update"
                    },
                    changeSummary = "Reviewed test update."
                }))
            using (var updateDocument = await ReadJsonAsync(updateResponse))
            {
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
                var updated = updateDocument.RootElement.GetProperty("data");
                Assert.Equal(2, updated.GetProperty("currentVersion").GetInt32());
                Assert.Equal("reviewed", updated.GetProperty("reviewStatus").GetString());
                Assert.Equal("Updated SEO Brief", updated.GetProperty("title").GetString());
            }

            using (var versionsResponse = await client.GetAsync($"/api/projects/{projectId}/briefs/{briefId}/versions"))
            using (var versionsDocument = await ReadJsonAsync(versionsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
                Assert.Equal(2, versionsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
                Assert.Contains(
                    versionsDocument.RootElement.GetProperty("data").EnumerateArray(),
                    version => version.GetProperty("versionNo").GetInt32() == 2);
            }

            using var exportResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/briefs/{briefId}/export",
                new { format = "markdown" });
            using var exportDocument = await ReadJsonAsync(exportResponse);

            Assert.Equal(HttpStatusCode.Accepted, exportResponse.StatusCode);
            var exportJobId = exportDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            await DispatchAsync(factory, exportJobId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var exportJob = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == exportJobId);
                Assert.Equal(StatusValues.Succeeded, exportJob.Status);
                var export = await dbContext.DataExports.AsNoTracking().SingleAsync(entity =>
                    entity.ProjectId == projectId &&
                    entity.ExportType == "article_brief");
                Assert.Equal("markdown", export.Format);
                Assert.Equal(StatusValues.Succeeded, export.Status);
                Assert.False(string.IsNullOrWhiteSpace(export.FileUri));
            }

            using (var otherProjectAnalysesResponse = await client.GetAsync($"/api/projects/{otherProjectId}/content-analyses"))
            using (var otherProjectAnalysesDocument = await ReadJsonAsync(otherProjectAnalysesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, otherProjectAnalysesResponse.StatusCode);
                Assert.Equal(0, otherProjectAnalysesDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            }

            using (var otherProjectBriefsResponse = await client.GetAsync($"/api/projects/{otherProjectId}/briefs"))
            using (var otherProjectBriefsDocument = await ReadJsonAsync(otherProjectBriefsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, otherProjectBriefsResponse.StatusCode);
                Assert.Equal(0, otherProjectBriefsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ContentAnalyzeRejectsMissingKeyword()
    {
        await using var factory = new ContentAnalysisApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Content Validation");

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/content/analyze",
                new { keyword = "" });
            using var document = await ReadJsonAsync(response);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "keyword");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task DispatchAsync(ContentAnalysisApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectAsync(ContentAnalysisApiFactory factory, string prefix)
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

    private static HttpClient CreateClient(ContentAnalysisApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-content-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class ContentAnalysisApiFactory : WebApplicationFactory<Program>
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
