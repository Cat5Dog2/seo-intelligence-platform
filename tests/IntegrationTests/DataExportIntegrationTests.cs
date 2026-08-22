using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
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

using IntegrationTests.Support;

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

                // The issued URL has to be something a caller can actually fetch. A storage://
                // URI is not: no HTTP client resolves it, which left every generated file
                // unreachable from the browser.
                Assert.Equal(
                    $"/api/projects/{projectId}/exports/{exportId}/content",
                    data.GetProperty("downloadUrl").GetString());
                // No expiry is promised: the URL is an authenticated API path, not a pre-signed
                // one, so a deadline on it would control nothing and used to go unenforced.
                Assert.False(data.TryGetProperty("expiresAt", out _));
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

                // Issuing a URL is not a download. The "downloaded" record belongs to the request
                // that actually reads the bytes, below.
                Assert.DoesNotContain(AuditLogActionNames.CsvDownloaded, csvAuditActions);
            }

            using (var contentResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
                Assert.Equal("text/csv", contentResponse.Content.Headers.ContentType?.MediaType);
                Assert.Equal("attachment", contentResponse.Content.Headers.ContentDisposition?.DispositionType);
                Assert.Equal(
                    $"keyword_metrics-{exportId:N}.csv",
                    contentResponse.Content.Headers.ContentDisposition?.FileNameStar
                        ?? contentResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

                var downloaded = await contentResponse.Content.ReadAsStringAsync();
                Assert.Equal(csv, downloaded);
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExportContentIsScopedToItsProjectAndRefusesUnfinishedExports()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithKeywordMetricsAsync(factory);
        var otherProjectId = await SeedProjectAsync(factory, "Other export content project");

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

            // The export is still queued: there is no file to hand out yet.
            using (var pendingResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}/content"))
            using (var pendingDocument = await ReadJsonAsync(pendingResponse))
            {
                Assert.Equal(HttpStatusCode.Conflict, pendingResponse.StatusCode);
                Assert.Equal(
                    "Resource.Conflict",
                    pendingDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            await DispatchAsync(factory, jobId);

            // A finished export must still not be readable through another project's URL.
            using (var wrongProjectResponse = await client.GetAsync($"/api/projects/{otherProjectId}/exports/{exportId}/content"))
            using (var wrongProjectDocument = await ReadJsonAsync(wrongProjectResponse))
            {
                Assert.Equal(HttpStatusCode.NotFound, wrongProjectResponse.StatusCode);
                Assert.Equal(
                    "Resource.NotFound",
                    wrongProjectDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            using (var ownProjectResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, ownProjectResponse.StatusCode);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AFileThatCannotBeOpenedAfterTheReadinessCheckIsNotRecordedAsDownloaded()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithKeywordMetricsAsync(factory);

        try
        {
            using var registerResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/exports/csv",
                new { exportType = "keyword_metrics" });
            using var registerDocument = await ReadJsonAsync(registerResponse);
            var jobId = registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            Guid exportId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                exportId = (await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId)).ResultResourceId!.Value;
            }

            await DispatchAsync(factory, jobId);

            // Storage now reports the object as present but fails to open it. Deleting the real
            // file instead would stop the request at the existence check and never exercise the
            // window this test is about - the one where a premature audit entry would be written.
            factory.MakeStorageUnreadable = true;

            using (var contentResponse = await client.GetAsync($"/api/projects/{projectId}/exports/{exportId}/content"))
            using (var contentDocument = await ReadJsonAsync(contentResponse))
            {
                Assert.Equal(HttpStatusCode.Conflict, contentResponse.StatusCode);
                Assert.Equal(
                    "Resource.Conflict",
                    contentDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var actions = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(entity =>
                        entity.ResourceType == AuditLogResourceTypes.CsvExport &&
                        entity.ResourceId == exportId.ToString("D"))
                    .Select(entity => entity.Action)
                    .ToArrayAsync();

                Assert.DoesNotContain(AuditLogActionNames.CsvDownloaded, actions);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExcelExportAndExcelKeywordImportUseGenericPhase3Endpoints()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithKeywordMetricsAsync(factory);

        try
        {
            using var registerResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/exports",
                new
                {
                    exportType = "keyword_metrics",
                    format = "excel",
                    columns = new[] { "keyword", "searchVolume", "opportunityScore" }
                });
            using var registerDocument = await ReadJsonAsync(registerResponse);

            Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
            var exportJobId = registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            Guid exportId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                exportId = (await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == exportJobId)).ResultResourceId!.Value;
            }

            await DispatchAsync(factory, exportJobId);

            string fileUri;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var export = await dbContext.DataExports.AsNoTracking().SingleAsync(entity => entity.Id == exportId);
                Assert.Equal(StatusValues.Succeeded, export.Status);
                Assert.Equal("excel", export.Format);
                Assert.EndsWith(".xlsx", export.FileUri, StringComparison.OrdinalIgnoreCase);
                fileUri = export.FileUri!;
            }

            await AssertXlsxContainsAsync(factory, fileUri, "content marketing");

            using var importResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/imports",
                new
                {
                    importType = "keywords",
                    format = "excel",
                    sourceFileUri = fileUri,
                    validationMode = "strict"
                });
            using var importDocument = await ReadJsonAsync(importResponse);

            Assert.Equal(HttpStatusCode.Accepted, importResponse.StatusCode);
            var importJobId = importDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            await DispatchAsync(factory, importJobId);

            Guid importId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var import = await dbContext.DataImports.AsNoTracking().SingleAsync(entity => entity.SourceFileUri == fileUri);
                importId = import.Id;
                Assert.Equal(StatusValues.Succeeded, import.Status);
                Assert.Equal("excel", import.Format);
                Assert.Equal("[]", import.ValidationErrorsJson);
                Assert.True(await dbContext.KeywordSeeds.AnyAsync(entity =>
                    entity.ProjectId == projectId &&
                    entity.Seed == "content marketing"));

                var actions = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Select(entity => entity.Action)
                    .ToArrayAsync();
                Assert.Contains(AuditLogActionNames.DataExportCreated, actions);
                Assert.Contains(AuditLogActionNames.DataImportCompleted, actions);
            }

            using (var importDetailResponse = await client.GetAsync($"/api/projects/{projectId}/imports/{importId}"))
            using (var importDetailDocument = await ReadJsonAsync(importDetailResponse))
            {
                Assert.Equal(HttpStatusCode.OK, importDetailResponse.StatusCode);
                var data = importDetailDocument.RootElement.GetProperty("data");
                Assert.Equal(importId, data.GetProperty("importId").GetGuid());
                Assert.Equal("keywords", data.GetProperty("importType").GetString());
                Assert.Equal("excel", data.GetProperty("format").GetString());
                Assert.Equal(StatusValues.Succeeded, data.GetProperty("status").GetString());
                Assert.Equal(fileUri, data.GetProperty("sourceFileUri").GetString());
                Assert.Equal(0, data.GetProperty("validationErrors").GetArrayLength());
                Assert.Equal("developer", data.GetProperty("requestedBy").GetString());
                Assert.Equal(projectId, data.GetProperty("projectId").GetGuid());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CsvImportsPersistSupportedDataTypesAndHistory()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "CSV import project");

        try
        {
            var imports = new[]
            {
                new ImportScenario(
                    "keywords",
                    "keyword,language,source,memo\r\nImported SEO,ja,csv,first\r\n"),
                new ImportScenario(
                    "rankings",
                    "keyword,target,position,rankedUrl,estimatedTraffic,checkedAt\r\nImported Rank,example.com,3,https://example.com/page,12.5,2026-06-01T00:00:00Z\r\n"),
                new ImportScenario(
                    "competitors",
                    "domain,duplicateRate,estimatedTraffic,trafficValue,keywordCount\r\nhttps://competitor.example/path,0.42,1200,300,12\r\n"),
                new ImportScenario(
                    "briefs",
                    "title,targetKeyword,contentJson,reviewStatus,status\r\nImported Brief,Brief Keyword,\"{\"\"outline\"\":\"\"ok\"\"}\",pending,draft\r\n"),
                new ImportScenario(
                    "tasks",
                    "targetUrl,priorityScore,reasonJson,status,assigneeActor,memo\r\nhttps://example.com/rewrite,81.2,\"{\"\"reason\"\":\"\"import\"\"}\",active,developer,Imported task\r\n")
            };

            foreach (var import in imports)
            {
                var jobId = await UploadAndRegisterImportAsync(client, factory, projectId, import.ImportType, import.Csv);
                await DispatchAsync(factory, jobId);
            }

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();

            Assert.Equal(5, await dbContext.DataImports.CountAsync(entity => entity.ProjectId == projectId));
            Assert.All(await dbContext.DataImports.AsNoTracking().Where(entity => entity.ProjectId == projectId).ToArrayAsync(), import =>
            {
                Assert.Equal(StatusValues.Succeeded, import.Status);
                Assert.Equal("[]", import.ValidationErrorsJson);
            });
            Assert.True(await dbContext.KeywordSeeds.AnyAsync(entity => entity.ProjectId == projectId && entity.Seed == "Imported SEO"));
            Assert.True(await dbContext.RankResults.AnyAsync(entity => entity.ProjectId == projectId && entity.Position == 3));
            Assert.True(await dbContext.CompetitorSites.AnyAsync(entity => entity.ProjectId == projectId && entity.Domain == "competitor.example"));
            Assert.True(await dbContext.ArticleBriefs.AnyAsync(entity => entity.ProjectId == projectId && entity.Title == "Imported Brief"));
            Assert.True(await dbContext.ArtifactVersions.AnyAsync(entity => entity.ProjectId == projectId && entity.ArtifactType == "article_brief"));
            Assert.True(await dbContext.RewriteTasks.AnyAsync(entity => entity.ProjectId == projectId && entity.TargetUrl == "https://example.com/rewrite"));

            var importAuditCount = await dbContext.AuditLogs.CountAsync(entity =>
                entity.ResourceType == AuditLogResourceTypes.DataImport &&
                entity.Action == AuditLogActionNames.DataImportCompleted);
            Assert.Equal(5, importAuditCount);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ImportValidationErrorsArePersistedAndPaged()
    {
        await using var factory = new DataExportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "CSV import validation project");

        try
        {
            var jobId = await UploadAndRegisterImportAsync(
                client,
                factory,
                projectId,
                "rankings",
                "keyword,target,position\r\nBroken Rank,example.com,not-a-number\r\n");
            await DispatchAsync(factory, jobId);

            Guid importId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var import = await dbContext.DataImports.AsNoTracking().SingleAsync(entity => entity.ProjectId == projectId);
                importId = import.Id;
                Assert.Equal(StatusValues.FailedFatal, import.Status);
                Assert.Contains("position", import.ValidationErrorsJson, StringComparison.Ordinal);
                Assert.False(await dbContext.RankResults.AnyAsync(entity => entity.ProjectId == projectId));
            }

            using var errorsResponse = await client.GetAsync($"/api/projects/{projectId}/imports/{importId}/errors?page=1&pageSize=10&q=position");
            using var errorsDocument = await ReadJsonAsync(errorsResponse);

            Assert.Equal(HttpStatusCode.OK, errorsResponse.StatusCode);
            var items = errorsDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal("rows[2].position", items[0].GetProperty("target").GetString());
            Assert.Contains("integer", items[0].GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

            using var detailResponse = await client.GetAsync($"/api/projects/{projectId}/imports/{importId}");
            using var detailDocument = await ReadJsonAsync(detailResponse);
            var importData = detailDocument.RootElement.GetProperty("data");

            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            Assert.Equal(importId, importData.GetProperty("importId").GetGuid());
            Assert.Equal(StatusValues.FailedFatal, importData.GetProperty("status").GetString());
            Assert.Equal(1, importData.GetProperty("validationErrors").GetArrayLength());
            Assert.Equal("rows[2].position", importData.GetProperty("validationErrors")[0].GetProperty("target").GetString());
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

    private static async Task<Guid> UploadAndRegisterImportAsync(
        HttpClient client,
        DataExportApiFactory factory,
        Guid projectId,
        string importType,
        string csv)
    {
        using var uploadResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/imports/upload-url",
            new
            {
                importType,
                format = "csv",
                fileName = $"{importType}.csv"
            });
        using var uploadDocument = await ReadJsonAsync(uploadResponse);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var sourceFileUri = uploadDocument.RootElement.GetProperty("data").GetProperty("sourceFileUri").GetString()!;
        Assert.StartsWith("storage://local/imports/", sourceFileUri, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes(csv), writable: false);
            await storage.PutAsync(
                new StoragePutRequest(
                    ResolveStorageObjectKey(sourceFileUri),
                    content,
                    "text/csv; charset=utf-8"));
        }

        using var registerResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/imports",
            new
            {
                importType,
                format = "csv",
                sourceFileUri,
                validationMode = "strict"
            });
        using var registerDocument = await ReadJsonAsync(registerResponse);

        Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
        return registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
    }

    private static async Task DispatchAsync(DataExportApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task AssertXlsxContainsAsync(
        DataExportApiFactory factory,
        string fileUri,
        string expectedText)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await using var stream = await storage.OpenReadAsync(ResolveStorageObjectKey(fileUri));
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(worksheet);
        using var worksheetStream = worksheet!.Open();
        using var reader = new StreamReader(worksheetStream);
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(expectedText, xml, StringComparison.Ordinal);
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

    private sealed record ImportScenario(string ImportType, string Csv);

    /// <summary>
    /// Reports every object as present but refuses to open it, which is the state left by a file
    /// deleted between the readiness check and the read. Deleting the file from real storage would
    /// not reach that path: the request would stop at the existence check instead.
    /// </summary>
    private sealed class UnreadableObjectStorage(IObjectStorage inner, Func<bool> isUnreadable) : IObjectStorage
    {
        public Task<StoredObjectReference> PutAsync(StoragePutRequest request, CancellationToken cancellationToken = default)
            => inner.PutAsync(request, cancellationToken);

        // Reports the object as present even while reads fail, which is the state left by a file
        // removed between the readiness check and the read.
        public Task<bool> ExistsAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
            => isUnreadable() ? Task.FromResult(true) : inner.ExistsAsync(key, cancellationToken);

        public Task<Stream> OpenReadAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
            => isUnreadable()
                ? throw new IOException($"Simulated read failure for {key.Value}.")
                : inner.OpenReadAsync(key, cancellationToken);

        public Task DeleteAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(key, cancellationToken);

        public Task<StorageConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
            => inner.CheckConnectivityAsync(cancellationToken);
    }

    private sealed class DataExportApiFactory : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Replaces storage with one whose reads always fail, after the export has been written.
        /// </summary>
        public bool MakeStorageUnreadable { get; set; }

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

                // Always wraps the real storage; the wrapper consults the flag on each call.
                // Deciding here instead would freeze the answer, because IObjectStorage is a
                // singleton that is first resolved while the export is being written.
                var storageDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IObjectStorage));
                services.Remove(storageDescriptor);
                services.Add(ServiceDescriptor.Describe(
                    typeof(IObjectStorage),
                    serviceProvider =>
                    {
                        var inner = (IObjectStorage)(storageDescriptor.ImplementationFactory?.Invoke(serviceProvider)
                            ?? ActivatorUtilities.CreateInstance(serviceProvider, storageDescriptor.ImplementationType!));
                        return new UnreadableObjectStorage(inner, () => MakeStorageUnreadable);
                    },
                    storageDescriptor.Lifetime));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }
}
