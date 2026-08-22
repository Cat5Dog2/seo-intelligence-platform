using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class ReportIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task MonthlyReportGeneratesPdfExcelShareUrlsAndAudits()
    {
        await using var discord = FakeDiscordWebhookServer.Start(HttpStatusCode.NoContent);
        await using var factory = new ReportApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString()
        });
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithReportDataAsync(factory);
        await SeedReportNotificationChannelAsync(factory, projectId);

        try
        {
            var (pdfJobId, pdfReportId) = await RegisterReportAsync(client, factory, projectId, "pdf");

            await DispatchAsync(factory, pdfJobId);

            using (var detailResponse = await client.GetAsync($"/api/projects/{projectId}/reports/{pdfReportId}"))
            using (var detailDocument = await ReadJsonAsync(detailResponse))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                var data = detailDocument.RootElement.GetProperty("data");
                Assert.Equal(pdfReportId, data.GetProperty("reportId").GetGuid());
                Assert.Equal("monthly", data.GetProperty("reportType").GetString());
                Assert.Equal("pdf", data.GetProperty("format").GetString());
                Assert.Equal("completed", data.GetProperty("status").GetString());
                Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("fileUri").GetString()));
            }

            string pdfFileUri;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == pdfJobId);
                var report = await dbContext.Reports.AsNoTracking().SingleAsync(entity => entity.Id == pdfReportId);
                var version = await dbContext.ArtifactVersions.AsNoTracking().SingleAsync(entity =>
                    entity.ArtifactType == "report" &&
                    entity.ArtifactId == pdfReportId);
                var delivery = await dbContext.NotificationDeliveries.AsNoTracking().SingleAsync(entity =>
                    entity.JobId == pdfJobId &&
                    entity.ResourceType == AuditLogResourceTypes.Report &&
                    entity.ResourceId == pdfReportId.ToString("D"));

                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.Equal("completed", report.Status);
                Assert.Equal(1, report.CurrentVersion);
                Assert.Equal(report.FileUri, version.ContentUri);
                Assert.Equal("report_completed", delivery.EventType);
                Assert.Equal(StatusValues.Succeeded, delivery.Status);
                pdfFileUri = report.FileUri!;
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var key = ResolveStorageObjectKey(pdfFileUri);
                Assert.True(await storage.ExistsAsync(key), $"PDF report was not found in storage: {pdfFileUri}");

                await using var stream = await storage.OpenReadAsync(key);
                using var reader = new StreamReader(stream, Encoding.ASCII);
                var pdf = await reader.ReadToEndAsync();
                Assert.StartsWith("%PDF-", pdf, StringComparison.Ordinal);
                Assert.Contains("SEO Intelligence Report", pdf, StringComparison.Ordinal);
            }

            using (var downloadResponse = await client.GetAsync($"/api/projects/{projectId}/reports/{pdfReportId}/download"))
            using (var downloadDocument = await ReadJsonAsync(downloadResponse))
            {
                Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
                var data = downloadDocument.RootElement.GetProperty("data");
                Assert.Equal(pdfReportId, data.GetProperty("reportId").GetGuid());

                // The issued URL has to be something a caller can actually fetch. A storage://
                // URI is not, which left every generated report unreachable from the browser.
                Assert.Equal(
                    $"/api/projects/{projectId}/reports/{pdfReportId}/content",
                    data.GetProperty("downloadUrl").GetString());
                // No expiry is promised: the URL is an authenticated API path, not a pre-signed
                // one, so a deadline on it would control nothing and used to go unenforced.
                Assert.False(data.TryGetProperty("expiresAt", out _));
            }

            using (var contentResponse = await client.GetAsync($"/api/projects/{projectId}/reports/{pdfReportId}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
                Assert.Equal("application/pdf", contentResponse.Content.Headers.ContentType?.MediaType);
                Assert.Equal("attachment", contentResponse.Content.Headers.ContentDisposition?.DispositionType);

                var bytes = await contentResponse.Content.ReadAsByteArrayAsync();
                Assert.StartsWith("%PDF-", Encoding.UTF8.GetString(bytes, 0, 5), StringComparison.Ordinal);
            }

            var shareUrl = await ShareReportAsync(client, projectId, pdfReportId);
            var token = shareUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            await AssertTokenIsStoredOnlyAsHashAsync(factory, pdfReportId, token);

            using (var sharedResponse = await client.GetAsync(shareUrl))
            using (var sharedDocument = await ReadJsonAsync(sharedResponse))
            {
                Assert.Equal(HttpStatusCode.OK, sharedResponse.StatusCode);
                var data = sharedDocument.RootElement.GetProperty("data");
                Assert.Equal(pdfReportId, data.GetProperty("reportId").GetGuid());
                Assert.Equal("pdf", data.GetProperty("format").GetString());

                // The share URL is handed to people outside the application, so what it points at
                // has to be fetchable anonymously rather than a storage:// URI only the server
                // could ever resolve.
                Assert.Equal($"{shareUrl}/content", data.GetProperty("downloadUrl").GetString());
            }

            // The recipient of a share link holds no service key, so the file endpoint has to
            // answer an unauthenticated request.
            using (var sharedContentResponse = await CreateAnonymousClient(factory).GetAsync($"{shareUrl}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, sharedContentResponse.StatusCode);
                Assert.Equal("application/pdf", sharedContentResponse.Content.Headers.ContentType?.MediaType);
                var bytes = await sharedContentResponse.Content.ReadAsByteArrayAsync();
                Assert.StartsWith("%PDF-", Encoding.UTF8.GetString(bytes, 0, 5), StringComparison.Ordinal);
            }

            await ExpireShareAsync(factory, pdfReportId);
            using (var expiredResponse = await client.GetAsync(shareUrl))
            using (var expiredDocument = await ReadJsonAsync(expiredResponse))
            {
                Assert.Equal(HttpStatusCode.Gone, expiredResponse.StatusCode);
                Assert.Equal("Resource.Gone", expiredDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            // Re-checked after the share has actually been used. The earlier call ran before any
            // access audit existed, so it could not have caught the token being written by one.
            await AssertTokenIsStoredOnlyAsHashAsync(factory, pdfReportId, token);

            // Revoking or expiring a share must stop the file too, not only the metadata lookup.
            using (var expiredContentResponse = await client.GetAsync($"{shareUrl}/content"))
            using (var expiredContentDocument = await ReadJsonAsync(expiredContentResponse))
            {
                Assert.Equal(HttpStatusCode.Gone, expiredContentResponse.StatusCode);
                Assert.Equal(
                    "Resource.Gone",
                    expiredContentDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            using (var tamperedResponse = await client.GetAsync($"/api/report-shares/{token}x"))
            using (var tamperedDocument = await ReadJsonAsync(tamperedResponse))
            {
                Assert.Equal(HttpStatusCode.NotFound, tamperedResponse.StatusCode);
                Assert.Equal("Resource.NotFound", tamperedDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            var revokedShareUrl = await ShareReportAsync(client, projectId, pdfReportId);
            using (var revokeResponse = await client.DeleteAsync($"/api/projects/{projectId}/reports/{pdfReportId}/share"))
            using (var revokeDocument = await ReadJsonAsync(revokeResponse))
            {
                Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
                Assert.Equal("revoked", revokeDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var revokedResponse = await client.GetAsync(revokedShareUrl))
            using (var revokedDocument = await ReadJsonAsync(revokedResponse))
            {
                Assert.Equal(HttpStatusCode.Gone, revokedResponse.StatusCode);
                Assert.Equal("Resource.Gone", revokedDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            var (excelJobId, excelReportId) = await RegisterReportAsync(client, factory, projectId, "excel");
            await DispatchAsync(factory, excelJobId);
            await AssertExcelReportWasWrittenAsync(factory, excelReportId);

            await AssertReportAuditActionsAsync(factory, pdfReportId);
            Assert.Contains(discord.RequestBodies, body => body.Contains("report_completed", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<(Guid JobId, Guid ReportId)> RegisterReportAsync(
        HttpClient client,
        ReportApiFactory factory,
        Guid projectId,
        string format)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/reports",
            new
            {
                reportType = "monthly",
                period = "2026-05",
                format,
                sections = new[] { "summary", "rankings" }
            });
        using var registerDocument = await ReadJsonAsync(registerResponse);

        Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);
        var jobId = registerDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
        Assert.Equal(jobId, registerDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
        Assert.Equal("report", job.ResultResourceType);
        Assert.True(job.ResultResourceId.HasValue);
        return (jobId, job.ResultResourceId!.Value);
    }

    private static async Task<string> ShareReportAsync(HttpClient client, Guid projectId, Guid reportId)
    {
        using var shareResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/reports/{reportId}/share",
            new
            {
                shareExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            });
        using var shareDocument = await ReadJsonAsync(shareResponse);

        Assert.Equal(HttpStatusCode.OK, shareResponse.StatusCode);
        var data = shareDocument.RootElement.GetProperty("data");
        Assert.Equal("active", data.GetProperty("status").GetString());
        var shareUrl = data.GetProperty("shareUrl").GetString();
        Assert.StartsWith("/api/report-shares/", shareUrl, StringComparison.Ordinal);
        return shareUrl!;
    }

    [Theory]
    [Trait("Category", "Security")]
    [InlineData("/api/report-shares/unknown-token-{0}")]
    [InlineData("/api/report-shares/unknown-token-{0}/content")]
    public async Task BothAnonymousShareEndpointsAllowThirtyRequestsAndRejectTheThirtyFirst(string pathFormat)
    {
        await using var factory = new ReportApiFactory();
        using var client = CreateAnonymousClient(factory);

        try
        {
            // Every one of these is rejected as an unknown token, and each rejection still costs a
            // database lookup and an audit row. The limit is what keeps that bounded: these are
            // the only endpoints an unauthenticated caller can reach the database through at all.
            var statuses = new List<HttpStatusCode>();
            for (var attempt = 0; attempt < 31; attempt++)
            {
                using var response = await client.GetAsync(
                    string.Format(CultureInfo.InvariantCulture, pathFormat, attempt));
                statuses.Add(response.StatusCode);
            }

            // The exact boundary, not just "a 429 appears somewhere": a limit that fired earlier
            // would turn away legitimate recipients, and one that fired later would not be the
            // limit this is documented as.
            Assert.All(statuses.Take(30), status => Assert.Equal(HttpStatusCode.NotFound, status));
            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[30]);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task TheRateLimitedResponseUsesTheCommonEnvelopeTheOpenApiDocumentPromises()
    {
        await using var factory = new ReportApiFactory();
        using var client = CreateAnonymousClient(factory);

        try
        {
            HttpStatusCode status;
            string body;
            string? contentType;
            string? retryAfter;
            var attempt = 0;
            do
            {
                using var response = await client.GetAsync($"/api/report-shares/rejected-{attempt}");
                status = response.StatusCode;
                body = await response.Content.ReadAsStringAsync();
                contentType = response.Content.Headers.ContentType?.MediaType;
                retryAfter = response.Headers.TryGetValues("Retry-After", out var values)
                    ? values.FirstOrDefault()
                    : null;
                attempt++;
            }
            while (status != HttpStatusCode.TooManyRequests && attempt < 60);

            Assert.Equal(HttpStatusCode.TooManyRequests, status);
            Assert.Equal("application/json", contentType);

            // The fixed window knows when it resets, so it supplies Retry-After. The concurrency
            // limiter cannot, and its rejection is asserted to omit the header instead.
            Assert.NotNull(retryAfter);

            // RejectionStatusCode on its own sends an empty body, which would leave callers with
            // a status the OpenAPI document describes as an envelope and nothing to parse.
            using var document = JsonDocument.Parse(body);
            Assert.False(document.RootElement.GetProperty("result").GetBoolean());
            Assert.Equal(
                "RateLimit.Exceeded",
                document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());

            // Correlates the rejection with the request, the same as every other error response.
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("requestId").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NoMoreThanEightShareDownloadsRunAtOnce()
    {
        // Storage holds each download open until released, so the permits are occupied on purpose
        // rather than by timing. No sleeping is involved: the test waits for the requests to have
        // arrived, checks the next one, and only then lets them finish.
        var arrived = new SemaphoreSlim(0);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var factory = new ReportApiFactory();
        factory.BlockStorageReads = (arrived, release);
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithReportDataAsync(factory);

        try
        {
            var (jobId, reportId) = await RegisterReportAsync(client, factory, projectId, "pdf");
            await DispatchAsync(factory, jobId);
            var shareUrl = await ShareReportAsync(client, projectId, reportId);

            // One client is enough: the concurrency cap is global rather than per caller, and a
            // single HttpClient issues these concurrently.
            using var anonymous = CreateAnonymousClient(factory);
            var blocked = new List<Task<HttpResponseMessage>>();
            for (var i = 0; i < 8; i++)
            {
                blocked.Add(anonymous.GetAsync($"{shareUrl}/content"));
            }

            for (var i = 0; i < 8; i++)
            {
                Assert.True(
                    await arrived.WaitAsync(TimeSpan.FromSeconds(30)),
                    "A share download did not reach storage.");
            }

            // The eight permits are all held. The ninth is refused rather than queued, so a
            // distributed attempt cannot tie up the database connection pool.
            var ninthRequest = anonymous.GetAsync($"{shareUrl}/content");

            // If the limit were ever raised, the ninth would reach storage instead of being
            // refused. Racing the two makes that fail here rather than hang until the client
            // times out.
            var reachedStorage = arrived.WaitAsync(TimeSpan.FromSeconds(20));
            var completedFirst = await Task.WhenAny(ninthRequest, reachedStorage);
            Assert.True(
                ReferenceEquals(completedFirst, ninthRequest),
                "The ninth concurrent download reached storage, so the concurrency limit did not refuse it.");

            using (var ninth = await ninthRequest)
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, ninth.StatusCode);

                // The envelope is the same as any other rejection.
                var body = await ninth.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(body);
                Assert.Equal(
                    "RateLimit.Exceeded",
                    document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
                Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("requestId").GetString()));

                // Retry-After is deliberately absent here. A concurrency limiter cannot say when a
                // permit will free, so it publishes no RetryAfter metadata and inventing a number
                // would be a guess. The header is documented as optional and only the fixed window
                // - which does know when it resets - supplies it.
                Assert.False(ninth.Headers.Contains("Retry-After"));
            }

            release.SetResult();

            foreach (var response in await Task.WhenAll(blocked))
            {
                using (response)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }
        }
        finally
        {
            release.TrySetResult();
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task TheShareTokenNeverReachesTheApplicationLog()
    {
        await using var factory = new ReportApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithReportDataAsync(factory);

        try
        {
            var (jobId, reportId) = await RegisterReportAsync(client, factory, projectId, "pdf");
            await DispatchAsync(factory, jobId);

            var shareUrl = await ShareReportAsync(client, projectId, reportId);
            var token = shareUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            using (var metadata = await CreateAnonymousClient(factory).GetAsync(shareUrl))
            {
                Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
            }

            using (var content = await CreateAnonymousClient(factory).GetAsync($"{shareUrl}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, content.StatusCode);
            }

            // The share token is a bearer credential. The request-logging middleware used to log
            // Request.Path verbatim, which put it into every log sink the host writes to - the same
            // secret the database deliberately keeps only a hash of.
            Assert.NotEmpty(factory.CapturedLogs.Messages);
            Assert.All(
                factory.CapturedLogs.Messages,
                message => Assert.DoesNotContain(token, message, StringComparison.Ordinal));

            // Proves the assertion above is looking at the right place: the route template for the
            // same requests is logged, so these messages really did pass through the middleware.
            Assert.Contains(
                factory.CapturedLogs.Messages,
                message => message.Contains("/api/report-shares/{token}", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task TheShareRateLimitDoesNotReachAuthenticatedEndpoints()
    {
        await using var factory = new ReportApiFactory();
        using var client = CreateClient(factory);

        try
        {
            // Guards against the limiter being widened to every endpoint: the share limit is far
            // below this count, so a shared limiter would start rejecting long before the end.
            for (var attempt = 0; attempt < 60; attempt++)
            {
                using var response = await client.GetAsync("/api/projects?page=1&pageSize=1");
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task AssertTokenIsStoredOnlyAsHashAsync(
        ReportApiFactory factory,
        Guid reportId,
        string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var report = await dbContext.Reports.AsNoTracking().SingleAsync(entity => entity.Id == reportId);
        Assert.False(string.IsNullOrWhiteSpace(report.ShareTokenHash));
        Assert.Equal(64, report.ShareTokenHash!.Length);
        Assert.NotEqual(token, report.ShareTokenHash);

        var auditPayloads = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entity => entity.ResourceType == AuditLogResourceTypes.Report && entity.ResourceId == reportId.ToString("D"))
            .Select(entity => entity.BeforeAfterJson)
            .ToArrayAsync();
        Assert.NotEmpty(auditPayloads);
        Assert.All(auditPayloads, payload => Assert.DoesNotContain(token, payload, StringComparison.Ordinal));

        // The share audit records where the file is fetched from. It has to be the route template:
        // the resolved URL embeds the token, which is exactly what must not be persisted.
        Assert.All(
            auditPayloads,
            payload => Assert.DoesNotContain("/api/report-shares/" + token, payload, StringComparison.Ordinal));
    }

    private static async Task ExpireShareAsync(ReportApiFactory factory, Guid reportId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var report = await dbContext.Reports.SingleAsync(entity => entity.Id == reportId);
        report.ShareExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        report.ShareRevokedAt = null;
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertExcelReportWasWrittenAsync(ReportApiFactory factory, Guid reportId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var report = await dbContext.Reports.AsNoTracking().SingleAsync(entity => entity.Id == reportId);
        Assert.Equal("excel", report.Format);
        Assert.Equal("completed", report.Status);

        var key = ResolveStorageObjectKey(report.FileUri!);
        await using var stream = await storage.OpenReadAsync(key);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var excel = await reader.ReadToEndAsync();
        Assert.Contains("<table>", excel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content marketing", excel, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertReportAuditActionsAsync(ReportApiFactory factory, Guid reportId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var audits = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entity => entity.ResourceType == AuditLogResourceTypes.Report && entity.ResourceId == reportId.ToString("D"))
            .Select(entity => new { entity.Action, entity.BeforeAfterJson })
            .ToArrayAsync();

        var actions = audits.Select(audit => audit.Action).ToArray();
        Assert.Contains(AuditLogActionNames.ReportGenerationQueued, actions);
        Assert.Contains(AuditLogActionNames.ReportCreated, actions);
        Assert.Contains(AuditLogActionNames.ReportDownloadUrlIssued, actions);
        Assert.Contains(AuditLogActionNames.ReportDownloaded, actions);
        Assert.Contains(AuditLogActionNames.ReportShareIssued, actions);
        Assert.Contains(AuditLogActionNames.ReportShareRevoked, actions);
        Assert.Contains(AuditLogActionNames.ReportShareAccessed, actions);
        Assert.Contains(AuditLogActionNames.ReportShareAccessRejected, actions);

        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportCreated &&
            audit.BeforeAfterJson.Contains("\"format\":\"pdf\"", StringComparison.Ordinal) &&
            audit.BeforeAfterJson.Contains("\"fileUri\":\"storage://local/reports/", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportDownloadUrlIssued &&
            audit.BeforeAfterJson.Contains("\"downloadUrl\":\"/api/projects/", StringComparison.Ordinal));

        // Both ways of reading the file are recorded, and each says which one it was.
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportDownloaded &&
            audit.BeforeAfterJson.Contains("\"via\":\"api_content_endpoint\"", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportDownloaded &&
            audit.BeforeAfterJson.Contains("\"via\":\"share_url\"", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportShareIssued &&
            audit.BeforeAfterJson.Contains("\"shareUrlReturnedOnce\":true", StringComparison.Ordinal) &&
            audit.BeforeAfterJson.Contains("\"hasShareToken\":true", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportShareRevoked &&
            audit.BeforeAfterJson.Contains("\"shareRevokedAt\":", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportShareAccessed &&
            audit.BeforeAfterJson.Contains("\"downloadExpiresAt\":", StringComparison.Ordinal));
        Assert.Contains(audits, audit =>
            audit.Action == AuditLogActionNames.ReportShareAccessRejected &&
            audit.BeforeAfterJson.Contains("\"reason\":\"expired\"", StringComparison.Ordinal));
    }

    private static async Task DispatchAsync(ReportApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectWithReportDataAsync(ReportApiFactory factory)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var keywordId = Guid.NewGuid();
        var secondaryKeywordId = Guid.NewGuid();
        var rankJobId = Guid.NewGuid();

        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"Report Project {projectId:N}",
            DefaultLocation = "JP",
            DefaultLanguage = "ja",
            KpiJson = "{}",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.Keywords.AddRange(
            new KeywordEntity
            {
                Id = keywordId,
                NormalizedText = "content marketing",
                Language = "ja",
                TextHash = HashText("content marketing"),
                CreatedAt = now
            },
            new KeywordEntity
            {
                Id = secondaryKeywordId,
                NormalizedText = "seo report",
                Language = "ja",
                TextHash = HashText("seo report"),
                CreatedAt = now
            });
        dbContext.ProjectKeywordScores.AddRange(
            new ProjectKeywordScoreEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = keywordId,
                Location = "JP",
                Language = "ja",
                OpportunityScore = 80m,
                ScoreComponentsJson = "{}",
                ScoredAt = now
            },
            new ProjectKeywordScoreEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                KeywordId = secondaryKeywordId,
                Location = "JP",
                Language = "ja",
                OpportunityScore = 65m,
                ScoreComponentsJson = "{}",
                ScoredAt = now
            });
        dbContext.Jobs.Add(new JobEntity
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
        });
        dbContext.RankResults.AddRange(
            new RankResultEntity
            {
                Id = Guid.NewGuid(),
                JobId = rankJobId,
                ProjectId = projectId,
                KeywordId = keywordId,
                Target = "example.com",
                Position = 4,
                RankedUrl = "https://example.com/content-marketing",
                EstimatedTraffic = 120m,
                MetricsSnapshotJson = "{}",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                CheckedAt = now
            },
            new RankResultEntity
            {
                Id = Guid.NewGuid(),
                JobId = rankJobId,
                ProjectId = projectId,
                KeywordId = secondaryKeywordId,
                Target = "example.com",
                Position = 8,
                RankedUrl = "https://example.com/seo-report",
                EstimatedTraffic = 75m,
                MetricsSnapshotJson = "{}",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                CheckedAt = now
            });
        dbContext.RewriteTasks.Add(new RewriteTaskEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TargetUrl = "https://example.com/content-marketing",
            PriorityScore = 70m,
            ReasonJson = "{}",
            Status = StatusValues.Active,
            AssigneeActor = "developer",
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.CannibalizationCandidates.Add(new CannibalizationCandidateEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            PrimaryUrl = "https://example.com/content-marketing",
            CompetingUrlsJson = """[{ "url": "https://example.com/content" }]""",
            SeverityScore = 55m,
            EvidenceJson = "{}",
            RecommendationJson = "{}",
            Status = StatusValues.Active,
            DetectedAt = now
        });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static async Task SeedReportNotificationChannelAsync(ReportApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        dbContext.NotificationChannels.Add(new NotificationChannelEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelType = "discord",
            Name = "Report completion",
            WebhookSecretRef = "discord-webhook-dev",
            EventTypesJson = JsonSerializer.Serialize(new[] { "report_completed" }),
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static StorageObjectKey ResolveStorageObjectKey(string fileUri)
    {
        var uri = new Uri(fileUri, UriKind.Absolute);
        return new StorageObjectKey(uri.AbsolutePath.Trim('/'));
    }

    private static HttpClient CreateClient(ReportApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>
    /// Stands in for the recipient of a share link, who has no service key. Without dropping the
    /// header the test would pass on the key alone and prove nothing about anonymous access.
    /// </summary>
    private static HttpClient CreateAnonymousClient(ReportApiFactory factory)
    {
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Remove(ServiceAuthenticationOptions.HeaderName);
        return client;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-report-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>
    /// Captures everything the host logs, so a test can assert on what did NOT reach the log.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> messages = [];
        private readonly Lock gate = new();

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (gate)
                {
                    return [.. messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private void Add(string message)
        {
            lock (gate)
            {
                messages.Add(message);
            }
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                // The formatted message plus the raw state: a token could reach either, because
                // structured logging keeps the argument values separately from the message.
                provider.Add($"{formatter(state, exception)} {state} {exception}");
            }
        }
    }

    /// <summary>
    /// Holds each read until the test releases it, so a fixed number of requests can be kept in
    /// flight deterministically. Without this the concurrency limit could only be probed by racing
    /// requests and hoping enough overlapped.
    /// </summary>
    private sealed class GatedObjectStorage(
        IObjectStorage inner,
        Func<(SemaphoreSlim Arrived, TaskCompletionSource Release)?> gate) : IObjectStorage
    {
        public Task<StoredObjectReference> PutAsync(StoragePutRequest request, CancellationToken cancellationToken = default)
            => inner.PutAsync(request, cancellationToken);

        public Task<bool> ExistsAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
            => inner.ExistsAsync(key, cancellationToken);

        public async Task<Stream> OpenReadAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
        {
            if (gate() is { } active)
            {
                active.Arrived.Release();
                await active.Release.Task.WaitAsync(cancellationToken);
            }

            return await inner.OpenReadAsync(key, cancellationToken);
        }

        public Task DeleteAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(key, cancellationToken);

        public Task<StorageConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
            => inner.CheckConnectivityAsync(cancellationToken);
    }

    private sealed class ReportApiFactory : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");
        private readonly IReadOnlyDictionary<string, string?> additionalConfiguration;

        public CapturingLoggerProvider CapturedLogs { get; } = new();

        /// <summary>
        /// When set, every storage read signals the semaphore and then waits for the task, so a
        /// test can hold a known number of requests in flight without sleeping.
        /// </summary>
        public (SemaphoreSlim Arrived, TaskCompletionSource Release)? BlockStorageReads { get; set; }

        public string StoragePath { get; } = CreateTempStoragePath();

        public ReportApiFactory(IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
        {
            this.additionalConfiguration = additionalConfiguration ?? new Dictionary<string, string?>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
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
                };

                foreach (var pair in additionalConfiguration)
                {
                    settings[pair.Key] = pair.Value;
                }

                configuration.AddInMemoryCollection(settings);
            });

            builder.ConfigureLogging(logging => logging.AddProvider(CapturedLogs));

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                var storageDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IObjectStorage));
                services.Remove(storageDescriptor);
                services.Add(ServiceDescriptor.Describe(
                    typeof(IObjectStorage),
                    serviceProvider =>
                    {
                        var inner = (IObjectStorage)(storageDescriptor.ImplementationFactory?.Invoke(serviceProvider)
                            ?? ActivatorUtilities.CreateInstance(serviceProvider, storageDescriptor.ImplementationType!));
                        return new GatedObjectStorage(inner, () => BlockStorageReads);
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

    private sealed class FakeDiscordWebhookServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Queue<HttpStatusCode> responses;
        private readonly Task acceptLoop;

        private FakeDiscordWebhookServer(TcpListener listener, Queue<HttpStatusCode> responses)
        {
            this.listener = listener;
            this.responses = responses;
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            Url = new Uri($"http://127.0.0.1:{endpoint.Port}/discord");
            acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public Uri Url { get; }

        public List<string> RequestBodies { get; } = [];

        public static FakeDiscordWebhookServer Start(params HttpStatusCode[] responses)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new FakeDiscordWebhookServer(
                listener,
                new Queue<HttpStatusCode>(responses.Length == 0 ? [HttpStatusCode.NoContent] : responses));
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();

            try
            {
                await acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            cancellation.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                await HandleClientAsync(client, cancellation.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            await using var stream = client.GetStream();
            var buffer = new byte[8192];
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var bodyStart = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (bodyStart >= 0)
            {
                RequestBodies.Add(request[(bodyStart + 4)..]);
            }

            var status = responses.Count > 0 ? responses.Dequeue() : HttpStatusCode.NoContent;
            var reason = status == HttpStatusCode.NoContent ? "No Content" : "Error";
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {reason}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
            await stream.WriteAsync(response, cancellationToken);
        }
    }
}
