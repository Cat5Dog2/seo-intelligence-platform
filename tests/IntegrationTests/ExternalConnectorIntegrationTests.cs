using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace IntegrationTests;

public sealed class ExternalConnectorIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task ConnectorLifecyclePersistsSecretReferencesAndStubTestRunsWithoutExternalFetch()
    {
        await using var factory = new ExternalConnectorApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Connector lifecycle project");

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/connectors",
            new
            {
                connectorType = "gsc",
                name = "GSC main",
                authRef = "secret-ref/gsc-main",
                settings = new
                {
                    siteUrl = "https://example.com",
                    dimensions = new[] { "query", "page" }
                }
            });
        using var createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = createDocument.RootElement.GetProperty("data");
        var connectorId = created.GetProperty("connectorId").GetGuid();
        Assert.Equal("gsc", created.GetProperty("connectorType").GetString());
        Assert.Equal("GSC main", created.GetProperty("name").GetString());
        Assert.Equal("secret-ref/gsc-main", created.GetProperty("authRef").GetString());
        Assert.Equal("https://example.com", created.GetProperty("settings").GetProperty("siteUrl").GetString());
        Assert.False(created.TryGetProperty("secretValue", out _));
        Assert.False(created.GetProperty("settings").TryGetProperty("secretValue", out _));

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/connectors/{connectorId}",
            new
            {
                connectorType = "ga4",
                name = "GA4 main",
                authRef = "secret-ref/ga4-main",
                settings = new
                {
                    propertyId = "properties/1234",
                    landingPageDimension = "pagePath"
                }
            });
        using var updateDocument = await ReadJsonAsync(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = updateDocument.RootElement.GetProperty("data");
        Assert.Equal("ga4", updated.GetProperty("connectorType").GetString());
        Assert.Equal("GA4 main", updated.GetProperty("name").GetString());
        Assert.Equal("secret-ref/ga4-main", updated.GetProperty("authRef").GetString());
        Assert.Equal("properties/1234", updated.GetProperty("settings").GetProperty("propertyId").GetString());

        using var testResponse = await client.PostAsync($"/api/projects/{projectId}/connectors/{connectorId}/test", content: null);
        using var testDocument = await ReadJsonAsync(testResponse);

        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        var run = testDocument.RootElement.GetProperty("data");
        var runId = run.GetProperty("runId").GetGuid();
        Assert.Equal(connectorId, run.GetProperty("connectorId").GetGuid());
        Assert.Equal("connection_test", run.GetProperty("runType").GetString());
        Assert.Equal(StatusValues.Succeeded, run.GetProperty("status").GetString());
        Assert.False(run.GetProperty("resultSummary").GetProperty("dataFetched").GetBoolean());
        Assert.Equal("stub", run.GetProperty("resultSummary").GetProperty("mode").GetString());

        using var runsResponse = await client.GetAsync($"/api/projects/{projectId}/connectors/{connectorId}/runs?status=all");
        using var runsDocument = await ReadJsonAsync(runsResponse);

        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runItems = runsDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
        Assert.Single(runItems);
        Assert.Equal(runId, runItems[0].GetProperty("runId").GetGuid());

        using var deleteResponse = await client.DeleteAsync($"/api/projects/{projectId}/connectors/{connectorId}");
        using var deleteDocument = await ReadJsonAsync(deleteResponse);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var disabled = deleteDocument.RootElement.GetProperty("data");
        Assert.Equal(StatusValues.Disabled, disabled.GetProperty("status").GetString());
        Assert.NotEqual(default, disabled.GetProperty("disabledAt").GetDateTime());

        using var disabledTestResponse = await client.PostAsync($"/api/projects/{projectId}/connectors/{connectorId}/test", content: null);
        using var disabledTestDocument = await ReadJsonAsync(disabledTestResponse);

        Assert.Equal(HttpStatusCode.Conflict, disabledTestResponse.StatusCode);
        Assert.Equal("Resource.Conflict", disabledTestDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var setting = await dbContext.ExternalConnectorSettings.AsNoTracking().SingleAsync(entity => entity.Id == connectorId);
        var persistedRun = await dbContext.ExternalConnectorRuns.AsNoTracking().SingleAsync(entity => entity.Id == runId);
        var auditActions = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entity => entity.ResourceId == connectorId.ToString("D") || entity.ResourceId == runId.ToString("D"))
            .Select(entity => entity.Action)
            .ToArrayAsync();

        Assert.Equal(StatusValues.Disabled, setting.Status);
        Assert.Equal("secret-ref/ga4-main", setting.AuthRef);
        Assert.DoesNotContain("secretValue", setting.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatusValues.Succeeded, persistedRun.Status);
        Assert.Contains("\"dataFetched\":false", persistedRun.ResultSummaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AuditLogActionNames.ExternalConnectorCreated, auditActions);
        Assert.Contains(AuditLogActionNames.ExternalConnectorUpdated, auditActions);
        Assert.Contains(AuditLogActionNames.ExternalConnectorTested, auditActions);
        Assert.Contains(AuditLogActionNames.ExternalConnectorDisabled, auditActions);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task ConnectorSettingsRejectSecretValuesInSettingsAndCrossProjectTestAccess()
    {
        await using var factory = new ExternalConnectorApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory, "Connector security project");
        var otherProjectId = await SeedProjectAsync(factory, "Other connector project");

        using var invalidResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/connectors",
            new
            {
                connectorType = "cms",
                name = "CMS with leaked secret",
                authRef = "secret-ref/cms",
                settings = new
                {
                    baseUrl = "https://cms.example.com",
                    secretValue = "cms-secret-value"
                }
            });
        using var invalidDocument = await ReadJsonAsync(invalidResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Contains(
            invalidDocument.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("target").GetString() == "settings");

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/connectors",
            new
            {
                connectorType = "bi",
                name = "BI export stub",
                authRef = "secret-ref/bi",
                settings = new
                {
                    dataset = "seo",
                    table = "monthly_report"
                }
            });
        using var createDocument = await ReadJsonAsync(createResponse);
        var connectorId = createDocument.RootElement.GetProperty("data").GetProperty("connectorId").GetGuid();

        using var wrongProjectResponse = await client.PostAsync($"/api/projects/{otherProjectId}/connectors/{connectorId}/test", content: null);
        using var wrongProjectDocument = await ReadJsonAsync(wrongProjectResponse);

        Assert.Equal(HttpStatusCode.NotFound, wrongProjectResponse.StatusCode);
        Assert.Equal("Resource.NotFound", wrongProjectDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        Assert.False(await dbContext.ExternalConnectorSettings.AnyAsync(entity => entity.Name == "CMS with leaked secret"));
        Assert.False(await dbContext.ExternalConnectorRuns.AnyAsync(entity => entity.ConnectorSettingId == connectorId));
    }

    private static async Task<Guid> SeedProjectAsync(ExternalConnectorApiFactory factory, string name)
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

    private static HttpClient CreateClient(ExternalConnectorApiFactory factory)
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

    private sealed class ExternalConnectorApiFactory : WebApplicationFactory<Program>
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
                    ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "seo-intelligence-connector-tests", databaseName),
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
