using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Infrastructure.Persistence;

namespace IntegrationTests;

public sealed class ManagementApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProjectAndSiteEndpointsArchiveRestoreAndRejectCrossProjectSiteAccess()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            var projectAId = await CreateProjectAsync(client, "Project A");
            var projectBId = await CreateProjectAsync(client, "Project B");

            using (var archiveResponse = await client.DeleteAsync($"/api/projects/{projectAId}"))
            using (var archiveDocument = await ReadJsonAsync(archiveResponse))
            {
                Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
                Assert.Equal("archived", archiveDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
                Assert.NotEqual(JsonValueKind.Null, archiveDocument.RootElement.GetProperty("data").GetProperty("archivedAt").ValueKind);
            }

            using (var activeProjectsResponse = await client.GetAsync("/api/projects"))
            using (var activeProjectsDocument = await ReadJsonAsync(activeProjectsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, activeProjectsResponse.StatusCode);
                var activeProjectIds = activeProjectsDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("projectId").GetGuid())
                    .ToArray();

                Assert.DoesNotContain(projectAId, activeProjectIds);
                Assert.Contains(projectBId, activeProjectIds);
            }

            using (var restoreResponse = await client.PostAsync($"/api/projects/{projectAId}/restore", content: null))
            using (var restoreDocument = await ReadJsonAsync(restoreResponse))
            {
                Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
                Assert.Equal("active", restoreDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, restoreDocument.RootElement.GetProperty("data").GetProperty("archivedAt").ValueKind);
            }

            var siteId = await CreateSiteAsync(client, projectBId);

            using (var crossProjectResponse = await client.GetAsync($"/api/projects/{projectAId}/sites/{siteId}"))
            using (var crossProjectDocument = await ReadJsonAsync(crossProjectResponse))
            {
                Assert.Equal(HttpStatusCode.NotFound, crossProjectResponse.StatusCode);
                Assert.False(crossProjectDocument.RootElement.GetProperty("result").GetBoolean());
                Assert.Equal("Resource.NotFound", crossProjectDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            using (var archiveSiteResponse = await client.DeleteAsync($"/api/projects/{projectBId}/sites/{siteId}"))
            using (var archiveSiteDocument = await ReadJsonAsync(archiveSiteResponse))
            {
                Assert.Equal(HttpStatusCode.OK, archiveSiteResponse.StatusCode);
                Assert.Equal("archived", archiveSiteDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var activeSitesResponse = await client.GetAsync($"/api/projects/{projectBId}/sites"))
            using (var activeSitesDocument = await ReadJsonAsync(activeSitesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, activeSitesResponse.StatusCode);
                Assert.Empty(activeSitesDocument.RootElement.GetProperty("data").EnumerateArray());
            }

            using (var restoreSiteResponse = await client.PostAsync($"/api/projects/{projectBId}/sites/{siteId}/restore", content: null))
            using (var restoreSiteDocument = await ReadJsonAsync(restoreSiteResponse))
            {
                Assert.Equal(HttpStatusCode.OK, restoreSiteResponse.StatusCode);
                Assert.Equal("active", restoreSiteDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AdminEndpointsManageWorkspaceCredentialsNotificationsAndAuditLists()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            using (var workspaceResponse = await client.PutAsJsonAsync("/api/admin/workspace", new
            {
                name = "MVP Workspace",
                defaultLocation = "JP",
                defaultLanguage = "ja",
                retentionSettings = new { externalApiRawDataMonths = 24 },
                notificationDefaults = new { discordEnabled = true }
            }))
            using (var workspaceDocument = await ReadJsonAsync(workspaceResponse))
            {
                Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
                Assert.Equal("MVP Workspace", workspaceDocument.RootElement.GetProperty("data").GetProperty("name").GetString());
                Assert.True(workspaceDocument.RootElement.GetProperty("data").GetProperty("notificationDefaults").GetProperty("discordEnabled").GetBoolean());
            }

            Guid credentialId;
            using (var credentialResponse = await client.PostAsJsonAsync("/api/admin/api-credentials", new
            {
                provider = "rakko_keyword",
                keyRef = "rakko-keyword-api-key-dev"
            }))
            using (var credentialDocument = await ReadJsonAsync(credentialResponse))
            {
                Assert.Equal(HttpStatusCode.Created, credentialResponse.StatusCode);
                var credentialData = credentialDocument.RootElement.GetProperty("data");
                credentialId = credentialData.GetProperty("credentialId").GetGuid();
                Assert.Equal("rakko-keyword-api-key-dev", credentialData.GetProperty("keyRef").GetString());
                Assert.False(credentialData.TryGetProperty("secretValue", out _));
            }

            using (var rotateResponse = await client.PostAsJsonAsync($"/api/admin/api-credentials/{credentialId}/rotate", new
            {
                newKeyRef = "rakko-keyword-api-key-rotated"
            }))
            using (var rotateDocument = await ReadJsonAsync(rotateResponse))
            {
                Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
                Assert.Equal("rakko-keyword-api-key-rotated", rotateDocument.RootElement.GetProperty("data").GetProperty("keyRef").GetString());
            }

            using (var disableResponse = await client.DeleteAsync($"/api/admin/api-credentials/{credentialId}"))
            using (var disableDocument = await ReadJsonAsync(disableResponse))
            {
                Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
                Assert.Equal("disabled", disableDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var enableResponse = await client.PostAsync($"/api/admin/api-credentials/{credentialId}/enable", content: null))
            using (var enableDocument = await ReadJsonAsync(enableResponse))
            {
                Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
                Assert.Equal("active", enableDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            Guid channelId;
            using (var channelResponse = await client.PostAsJsonAsync("/api/admin/notification-channels", new
            {
                projectId = (Guid?)null,
                channelType = "discord",
                name = "MVP Alerts",
                webhookSecretRef = "discord-webhook-dev",
                eventTypes = new[] { "job_failed", "credit_low" }
            }))
            using (var channelDocument = await ReadJsonAsync(channelResponse))
            {
                Assert.Equal(HttpStatusCode.Created, channelResponse.StatusCode);
                var channelData = channelDocument.RootElement.GetProperty("data");
                channelId = channelData.GetProperty("channelId").GetGuid();
                Assert.Equal("discord-webhook-dev", channelData.GetProperty("webhookSecretRef").GetString());
                Assert.False(channelData.TryGetProperty("webhookUrl", out _));
            }

            Guid deliveryId;
            using (var testResponse = await client.PostAsync($"/api/admin/notification-channels/{channelId}/test", content: null))
            using (var testDocument = await ReadJsonAsync(testResponse))
            {
                Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
                var deliveryData = testDocument.RootElement.GetProperty("data");
                deliveryId = deliveryData.GetProperty("deliveryId").GetGuid();
                Assert.Equal("succeeded", deliveryData.GetProperty("status").GetString());
            }

            using (var deliveryResponse = await client.GetAsync($"/api/admin/notification-deliveries/{deliveryId}"))
            using (var deliveryDocument = await ReadJsonAsync(deliveryResponse))
            {
                Assert.Equal(HttpStatusCode.OK, deliveryResponse.StatusCode);
                Assert.Equal("test", deliveryDocument.RootElement.GetProperty("data").GetProperty("eventType").GetString());
            }

            using (var externalCallsResponse = await client.GetAsync("/api/admin/external-api-calls"))
            using (var externalCallsDocument = await ReadJsonAsync(externalCallsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, externalCallsResponse.StatusCode);
                Assert.True(externalCallsDocument.RootElement.GetProperty("result").GetBoolean());
                Assert.Empty(externalCallsDocument.RootElement.GetProperty("data").EnumerateArray());
            }

            using (var auditLogsResponse = await client.GetAsync("/api/admin/audit-logs"))
            using (var auditLogsDocument = await ReadJsonAsync(auditLogsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, auditLogsResponse.StatusCode);
                Assert.True(auditLogsDocument.RootElement.GetProperty("result").GetBoolean());
                var actions = auditLogsDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("action").GetString())
                    .ToArray();

                Assert.Contains(AuditLogActionNames.ApiCredentialCreated, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialRotated, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialDisabled, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialEnabled, actions);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task ApiCredentialSecretValueIsStoredAsReferenceAndNeverReturnedOrAudited()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            const string inputSecret = "do-not-leak-api-key-value";
            const string correlationId = "corr-secret-create";

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/api-credentials")
            {
                Content = JsonContent.Create(new
                {
                    provider = "rakko_keyword",
                    secretValue = inputSecret
                })
            };
            createRequest.Headers.Add("X-Correlation-Id", correlationId);

            using var createResponse = await client.SendAsync(createRequest);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            using var createDocument = JsonDocument.Parse(createContent);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.True(!createContent.Contains(inputSecret, StringComparison.Ordinal), "Create response contained the input secret.");

            var credentialData = createDocument.RootElement.GetProperty("data");
            var credentialId = credentialData.GetProperty("credentialId").GetGuid();
            var keyRef = credentialData.GetProperty("keyRef").GetString();
            Assert.False(credentialData.TryGetProperty("secretValue", out _));
            Assert.False(string.IsNullOrWhiteSpace(keyRef));
            Assert.StartsWith("api-credential-rakko-keyword-", keyRef);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var credential = await dbContext.ApiCredentials
                    .AsNoTracking()
                    .SingleAsync(entity => entity.Id == credentialId);

                Assert.Equal(keyRef, credential.KeyRef);
                Assert.True(!string.Equals(inputSecret, credential.KeyRef, StringComparison.Ordinal), "DB key_ref contained the input secret.");

                var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
                var storedSecret = await secretStore.GetAsync(new SecretReference(keyRef!));
                Assert.NotNull(storedSecret);
                Assert.True(string.Equals(inputSecret, storedSecret!.Value, StringComparison.Ordinal), "Secret Store did not return the original input secret.");

                var auditPayloads = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(entity => entity.CorrelationId == correlationId)
                    .Select(entity => entity.BeforeAfterJson)
                    .ToArrayAsync();

                Assert.NotEmpty(auditPayloads);
                Assert.All(auditPayloads, payload =>
                    Assert.True(!payload.Contains(inputSecret, StringComparison.Ordinal), "Audit log contained the input secret."));
            }

            using (var auditResponse = await client.GetAsync($"/api/admin/audit-logs?resourceType=api_credential&resourceId={credentialId}&correlation_id={correlationId}&actor=developer&from=2000-01-01T00:00:00Z&to=2999-01-01T00:00:00Z"))
            {
                var auditContent = await auditResponse.Content.ReadAsStringAsync();
                using var auditDocument = JsonDocument.Parse(auditContent);

                Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
                Assert.True(!auditContent.Contains(inputSecret, StringComparison.Ordinal), "Audit response contained the input secret.");
                var auditItem = Assert.Single(auditDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal(AuditLogActionNames.ApiCredentialCreated, auditItem.GetProperty("action").GetString());
                Assert.Equal(correlationId, auditItem.GetProperty("correlationId").GetString());
            }

            const string rejectedSecret = "rejected-do-not-leak";
            using var rejectedResponse = await client.PostAsJsonAsync("/api/admin/api-credentials", new
            {
                provider = "rakko_keyword",
                keyRef = "existing-ref",
                secretValue = rejectedSecret
            });
            var rejectedContent = await rejectedResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, rejectedResponse.StatusCode);
            Assert.True(!rejectedContent.Contains(rejectedSecret, StringComparison.Ordinal), "Validation response contained the rejected secret.");
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AuditLogSearchFiltersActorResourceCorrelationAndPeriod()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            const string correlationId = "corr-audit-filter";
            var externalCallId = Guid.NewGuid().ToString("D");
            var csvExportId = Guid.NewGuid().ToString("D");
            var jobId = Guid.NewGuid().ToString("D");

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var contextService = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
                var context = contextService.Create(SeoIntelligenceSeedData.DefaultWorkspaceId, correlationId: correlationId);

                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.ExternalApiExecuted,
                        AuditLogResourceTypes.ExternalApiCall,
                        externalCallId,
                        new { after = new { provider = "rakko_keyword", endpoint = "/v1/suggest" } }));
                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.CsvExportCreated,
                        AuditLogResourceTypes.CsvExport,
                        csvExportId,
                        new { after = new { format = "csv", status = "queued" } }));
                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.JobCanceled,
                        AuditLogResourceTypes.Job,
                        jobId,
                        new { before = new { status = "waiting_external" }, after = new { status = "canceled" } }));
            }

            using (var correlationResponse = await client.GetAsync($"/api/admin/audit-logs?actor=developer&correlation_id={correlationId}&from=2000-01-01T00:00:00Z&to=2999-01-01T00:00:00Z&pageSize=10"))
            using (var correlationDocument = await ReadJsonAsync(correlationResponse))
            {
                Assert.Equal(HttpStatusCode.OK, correlationResponse.StatusCode);
                var actions = correlationDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("action").GetString())
                    .ToArray();

                Assert.Contains(AuditLogActionNames.ExternalApiExecuted, actions);
                Assert.Contains(AuditLogActionNames.CsvExportCreated, actions);
                Assert.Contains(AuditLogActionNames.JobCanceled, actions);
            }

            using (var csvResponse = await client.GetAsync($"/api/admin/audit-logs?resourceType=csv_export&resourceId={csvExportId}&correlationId={correlationId}"))
            using (var csvDocument = await ReadJsonAsync(csvResponse))
            {
                Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
                var item = Assert.Single(csvDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal(AuditLogActionNames.CsvExportCreated, item.GetProperty("action").GetString());
                Assert.Equal(AuditLogResourceTypes.CsvExport, item.GetProperty("resourceType").GetString());
                Assert.Equal(csvExportId, item.GetProperty("resourceId").GetString());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name,
            defaultLocation = "JP",
            defaultLanguage = "ja",
            kpi = new { organicSessions = 1000 },
            memo = "integration test"
        });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("active", document.RootElement.GetProperty("data").GetProperty("status").GetString());
        return document.RootElement.GetProperty("data").GetProperty("projectId").GetGuid();
    }

    private static async Task<Guid> CreateSiteAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/sites", new
        {
            domain = " HTTPS://WWW.Example.COM:443/topics?x=1 ",
            canonicalUrl = "WWW.Example.COM:443/topics?x=1#section",
            type = "own",
            memo = "owned site"
        });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("www.example.com", data.GetProperty("domain").GetString());
        Assert.Equal("https://www.example.com/topics?x=1", data.GetProperty("canonicalUrl").GetString());
        return data.GetProperty("siteId").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static HttpClient CreateClient(ManagementApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-management-api-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class ManagementApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

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
                    ["OpenTelemetry:ServiceName"] = "IntegrationTests"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }
}
