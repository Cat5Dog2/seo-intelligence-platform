using System.Net;
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

public sealed class MasterDataIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MasterDataSyncEndpointRegistersJobAndDispatcherUpsertsCatalogs()
    {
        await using var factory = new MasterDataApiFactory();
        using var client = CreateClient(factory);

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                dbContext.Locations.Add(new LocationEntity
                {
                    Id = Guid.NewGuid(),
                    Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                    LocationCode = "9999",
                    LocationName = "Removed Location",
                    CountryCode = "ZZ",
                    Status = StatusValues.Active,
                    SyncedAt = DateTime.UtcNow.AddDays(-7)
                });
                dbContext.Languages.Add(new LanguageEntity
                {
                    Id = Guid.NewGuid(),
                    Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                    LanguageCode = "zz",
                    LanguageName = "Removed Language",
                    Status = StatusValues.Active,
                    SyncedAt = DateTime.UtcNow.AddDays(-7)
                });
                await dbContext.SaveChangesAsync();
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/master-data/sync");
            request.Headers.Add("X-Correlation-Id", "corr-master-sync");
            using var syncResponse = await client.SendAsync(request);
            using var syncDocument = await ReadJsonAsync(syncResponse);

            Assert.Equal(HttpStatusCode.Accepted, syncResponse.StatusCode);
            var jobId = syncDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, syncDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());
            Assert.Equal(StatusValues.Queued, syncDocument.RootElement.GetProperty("data").GetProperty("status").GetString());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var japan = await dbContext.Locations.AsNoTracking().SingleAsync(entity => entity.LocationCode == "Japan");
                var removedLocation = await dbContext.Locations.AsNoTracking().SingleAsync(entity => entity.LocationCode == "9999");
                var japanese = await dbContext.Languages.AsNoTracking().SingleAsync(entity => entity.LanguageCode == "Japanese");
                var removedLanguage = await dbContext.Languages.AsNoTracking().SingleAsync(entity => entity.LanguageCode == "zz");
                var externalCalls = await dbContext.ExternalApiCalls
                    .AsNoTracking()
                    .Where(entity => entity.JobId == jobId)
                    .OrderBy(entity => entity.Endpoint)
                    .ToArrayAsync();

                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.NotNull(job.CompletedAt);

                Assert.Equal(SeoIntelligenceSeedData.RakkoKeywordProvider, japan.Provider);
                Assert.Equal("Japan", japan.LocationName);
                Assert.Equal("JP", japan.CountryCode);
                Assert.Equal(StatusValues.Active, japan.Status);
                Assert.Equal(StatusValues.Archived, removedLocation.Status);

                Assert.Equal("Japanese", japanese.LanguageName);
                Assert.Equal(StatusValues.Active, japanese.Status);
                Assert.Equal(StatusValues.Archived, removedLanguage.Status);

                Assert.Equal(
                    ["/v1/metadata/languages", "/v1/metadata/locations"],
                    externalCalls.Select(entity => entity.Endpoint).ToArray());
                Assert.All(externalCalls, call =>
                {
                    Assert.Equal(200, call.StatusCode);
                    Assert.Equal(SeoIntelligenceSeedData.RakkoKeywordScopeKey, call.ContractScopeKey);
                });
            }

            using (var locationsResponse = await client.GetAsync("/api/master-data/locations"))
            using (var locationsDocument = await ReadJsonAsync(locationsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, locationsResponse.StatusCode);
                var location = Assert.Single(locationsDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("Japan", location.GetProperty("code").GetString());
                Assert.Equal(StatusValues.Active, location.GetProperty("status").GetString());
            }

            using (var languagesResponse = await client.GetAsync("/api/master-data/languages"))
            using (var languagesDocument = await ReadJsonAsync(languagesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, languagesResponse.StatusCode);
                var language = Assert.Single(languagesDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal("Japanese", language.GetProperty("code").GetString());
                Assert.Equal(StatusValues.Active, language.GetProperty("status").GetString());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static HttpClient CreateClient(MasterDataApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-master-data-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class MasterDataApiFactory : WebApplicationFactory<Program>
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
