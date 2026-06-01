using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

namespace IntegrationTests;

public sealed class DataExportIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CsvExportJobPersistsStorageFileAndAuditsDownload()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithKeywordMetricsAsync(factory);

        try
        {
            using var registerResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/exports/csv",
                new
                {
                    exportType = "keyword_metrics",
                    filter = new
                    {
                        minSearchVolume = 100,
                        minOpportunityScore = 50
                    },
                    columns = new[]
                    {
                        "keyword",
                        "searchVolume",
                        "opportunityScore"
                    }
                });
            using var registerDocument = await ReadJsonAsync(registerResponse);

            Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
            var jobId = registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, registerDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

            Guid exportId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                exportId = job.ResultResourceId!.Value;

                var export = await dbContext.DataExports.AsNoTracking().SingleAsync(entity => entity.Id == exportId);
                Assert.Equal(StatusValues.Queued, export.Status);
                Assert.Equal("keyword_metrics", export.ExportType);
                Assert.Equal("csv", export.Format);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            using (var detailResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}"))
            using (var detailDocument = await ReadJsonAsync(detailResponse))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var data = detailDocument.RootElement.GetProperty("data");
                Assert.Equal(exportId, data.GetProperty("exportId").GetGuid());
                Assert.Equal(StatusValues.Succeeded, data.GetProperty("status").GetString());
                Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("fileUri").GetString()));
            }

            string fileUri;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var export = await dbContext.DataExports.AsNoTracking().SingleAsync(entity => entity.Id == exportId);

                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.Equal(StatusValues.Succeeded, export.Status);
                Assert.NotNull(export.CompletedAt);
                fileUri = export.FileUri!;
            }

            string csv;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var key = ResolveStorageObjectKey(fileUri);
                Assert.True(await storage.ExistsAsync(key), $"CSV file was not found in storage: {fileUri}");

                await using var stream = await storage.OpenReadAsync(key);
                using var reader = new StreamReader(stream);
                csv = await reader.ReadToEndAsync();
            }

            Assert.Contains("keyword,searchVolume,opportunityScore", csv, StringComparison.Ordinal);
            Assert.Contains("content marketing,1200,72.5", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("low volume", csv, StringComparison.Ordinal);

            using (var downloadResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}/download"))
            using (var downloadDocument = await ReadJsonAsync(downloadResponse))
            {
                Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
                var data = downloadDocument.RootElement.GetProperty("data");
                Assert.Equal(exportId, data.GetProperty("exportId").GetGuid());
                Assert.StartsWith("storage://local/exports/", data.GetProperty("downloadUrl").GetString(), StringComparison.Ordinal);
                Assert.True(data.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var csvAuditActions = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(entity =>
                        entity.ResourceType == AuditLogResourceTypes.CsvExport &&
                        entity.ResourceId == exportId.ToString("D"))
                    .Select(entity => entity.Action)
                    .ToArrayAsync();

                Assert.Contains(AuditLogActionNames.CsvExportCreated, csvAuditActions);
                Assert.Contains(AuditLogActionNames.CsvDownloadUrlIssued, csvAuditActions);
                Assert.Contains(AuditLogActionNames.CsvDownloaded, csvAuditActions);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CsvExportRejectsCrossProjectStateLookup()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithKeywordMetricsAsync(factory);
        var otherProjectId = await SeedProjectAsync(factory, "Other export project");

        try
        {
            using var registerResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/exports/csv",
                new
                {
                    exportType = "keyword_metrics"
                });
            using var registerDocument = await ReadJsonAsync(registerResponse);
            var jobId = registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            Guid exportId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                exportId = (await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId)).ResultResourceId!.Value;
            }

            using var wrongProjectResponse = await client.GetAsync($"/api/projects/{otherProjectId}/exports/{exportId}");
            using var wrongProjectDocument = await ReadJsonAsync(wrongProjectResponse);

            Assert.Equal(HttpStatusCode.NotFound, wrongProjectResponse.StatusCode);
            Assert.Equal("Resource.NotFound", wrongProjectDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> SeedProjectWithKeywordMetricsAsync(DataExportApiFactory factory)
    {
        var projectId = await SeedProjectAsync(factory, "CSV export project");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var contentKeywordId = Guid.NewGuid();
        var lowKeywordId = Guid.NewGuid();

        dbContext.Keywords.AddRange(
            new KeywordEntity
            {
                Id = contentKeywordId,
                NormalizedText = "content marketing",
                Language = "ja",
                TextHash = "content-marketing-hash",
                CreatedAt = now
            },
            new KeywordEntity
            {
                Id = lowKeywordId,
                NormalizedText = "low volume",
                Language = "ja",
                TextHash = "low-volume-hash",
                CreatedAt = now
            });
        dbContext.KeywordMetrics.AddRange(
            new KeywordMetricEntity
            {
                Id = Guid.NewGuid(),
                KeywordId = contentKeywordId,
                Location = "JP",
                Language = "ja",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                SearchVolume = 1200,
                SeoDifficulty = 31.2m,
                Cpc = 2.5m,
                Competition = 0.61m,
                FetchedAt = now
            },
            new KeywordMetricEntity
            {
                Id = Guid.NewGuid(),
                KeywordId = lowKeywordId,
                Location = "JP",
                Language = "ja",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                SearchVolume = 20,
                SeoDifficulty = 8m,
                Cpc = 0.1m,
                Competition = 0.05m,
                FetchedAt = now
            });
        dbContext.ProjectKeywordScores.AddRange(
            new ProjectKeywordScoreEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = contentKeywordId,
                Location = "JP",
                Language = "ja",
                OpportunityScore = 72.5m,
                ScoreComponentsJson = "{}",
                ScoredAt = now
            },
            new ProjectKeywordScoreEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = lowKeywordId,
                Location = "JP",
                Language = "ja",
                OpportunityScore = 30m,
                ScoreComponentsJson = "{}",
                ScoredAt = now
            });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static async Task<Guid> SeedProjectAsync(DataExportApiFactory factory, string name)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"{name} {projectId:N}",
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

    private static StorageObjectKey ResolveStorageObjectKey(string fileUri)
    {
        var uri = new Uri(fileUri, UriKind.Absolute);
        return new StorageObjectKey(uri.AbsolutePath.Trim('/'));
    }

    private static HttpClient CreateClient(DataExportApiFactory factory)
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
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-data-export-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class DataExportApiFactory : WebApplicationFactory<Program>
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
