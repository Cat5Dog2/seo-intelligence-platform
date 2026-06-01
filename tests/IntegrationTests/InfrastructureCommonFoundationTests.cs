using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SeoIntelligence.Application.Infrastructure;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace IntegrationTests;

public sealed class InfrastructureCommonFoundationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LocalStorageWritesReadsAndDeletesObject()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            var storage = provider.GetRequiredService<IObjectStorage>();
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"));
            var key = new StorageObjectKey("raw/rakko/request-1.json");

            var reference = await storage.PutAsync(new StoragePutRequest(key, content, "application/json"));
            var existsAfterWrite = await storage.ExistsAsync(key);
            string readContent;
            await using (var readStream = await storage.OpenReadAsync(key))
            using (var reader = new StreamReader(readStream, Encoding.UTF8))
            {
                readContent = await reader.ReadToEndAsync();
            }

            await storage.DeleteAsync(key);

            Assert.Equal("storage://local/raw/rakko/request-1.json", reference.Uri);
            Assert.Equal("Local", reference.Provider);
            Assert.Equal("application/json", reference.ContentType);
            Assert.True(existsAfterWrite);
            Assert.Equal("{\"status\":\"ok\"}", readContent);
            Assert.False(await storage.ExistsAsync(key));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfigurationSecretStoreReturnsValueWithoutExposingItThroughToString()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(
            storagePath,
            new Dictionary<string, string?>
            {
                ["Secrets:rakko-keyword-api-key-dev"] = "actual-secret-value"
            });

        try
        {
            var secretStore = provider.GetRequiredService<ISecretStore>();
            var secret = await secretStore.GetAsync(new SecretReference("rakko-keyword-api-key-dev"));

            Assert.NotNull(secret);
            Assert.True(await secretStore.ExistsAsync(new SecretReference("rakko-keyword-api-key-dev")));
            Assert.Equal("actual-secret-value", secret!.Value);
            Assert.Equal("****", secret.ToString());
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadinessProbeChecksStorageSecretStoreAndSkipsUnconfiguredDbAndRedis()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProvider(storagePath);

        try
        {
            var probe = provider.GetRequiredService<IInfrastructureReadinessProbe>();
            var checks = await probe.CheckAsync();

            Assert.All(checks, check => Assert.True(check.IsHealthy, check.Message));
            Assert.Contains(checks, check => check.Name == "db" && check.Message == "Database is not configured.");
            Assert.Contains(checks, check => check.Name == "redis" && check.Message == "Redis is not configured.");
            Assert.Contains(checks, check => check.Name == "storage" && check.Message == "Local storage read/write succeeded.");
            Assert.Contains(checks, check => check.Name == "secret_store" && check.Message == "Configuration Secret Store is available.");
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RakkoKeywordMockClientStoresRawJsonAndExternalApiCall()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProviderWithInMemoryDb(storagePath);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var client = scope.ServiceProvider.GetRequiredService<IRakkoKeywordClient>();
            var result = await client.GetRelatedKeywordsAsync(
                CreateRakkoContext(),
                new RakkoRelatedKeywordsRequest("seo"));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ExternalCall.CallId);

            var call = await dbContext.ExternalApiCalls
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == result.ExternalCall.CallId);
            Assert.Equal(SeoIntelligenceSeedData.DefaultWorkspaceId, call.WorkspaceId);
            Assert.Equal("rakko_keyword", call.Provider);
            Assert.Equal("/v1/related-keywords", call.Endpoint);
            Assert.Equal(SeoIntelligenceSeedData.RakkoKeywordScopeKey, call.ContractScopeKey);
            Assert.Equal(200, call.StatusCode);
            Assert.Equal(1m, call.ConsumedCredit);
            Assert.False(call.CacheHit);
            Assert.Null(call.ErrorCode);
            Assert.Equal(result.ExternalCall.RequestHash, call.RequestHash);
            Assert.Equal(result.ExternalCall.ResponseHash, call.ResponseHash);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", call.RequestUri, StringComparison.Ordinal);
            Assert.StartsWith("storage://local/raw/rakko-keyword/", call.ResponseUri, StringComparison.Ordinal);

            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            Assert.True(await storage.ExistsAsync(ToStorageKey(call.RequestUri)));
            Assert.True(await storage.ExistsAsync(ToStorageKey(call.ResponseUri!)));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RakkoKeywordMetricCacheRequiresMatchingContractScopeKey()
    {
        var storagePath = CreateTempStoragePath();
        await using var provider = BuildProviderWithInMemoryDb(storagePath);

        try
        {
            var keywordId = Guid.Parse("018f3f12-0004-7000-8000-000000000001");
            var sourceCallId = Guid.Parse("018f3f12-0005-7000-8000-000000000001");

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.KeywordMetrics.Add(new KeywordMetricEntity
            {
                Id = Guid.NewGuid(),
                KeywordId = keywordId,
                Location = "Japan",
                Language = "Japanese",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                SourceCallId = sourceCallId,
                SearchVolume = 1000,
                SeoDifficulty = 30,
                Cpc = 0.5m,
                Competition = 12,
                FetchedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            var cache = scope.ServiceProvider.GetRequiredService<IRakkoKeywordMetricCache>();
            var matched = await cache.CanReuseAsync(new RakkoKeywordMetricCacheLookup(
                keywordId,
                "Japan",
                "Japanese",
                SeoIntelligenceSeedData.RakkoKeywordScopeKey));
            var mismatched = await cache.CanReuseAsync(new RakkoKeywordMetricCacheLookup(
                keywordId,
                "Japan",
                "Japanese",
                "rakko_keyword:other:scope"));

            Assert.True(matched.CanReuse);
            Assert.Equal("contract_scope_matched", matched.Reason);
            Assert.Equal(sourceCallId, matched.SourceCallId);
            Assert.False(mismatched.CanReuse);
            Assert.Equal("contract_scope_mismatch", mismatched.Reason);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    private static ServiceProvider BuildProvider(
        string storagePath,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "Local",
            ["Storage:BasePath"] = storagePath,
            ["Storage:BucketName"] = "seo-intelligence",
            ["SecretStore:Provider"] = "Configuration",
            ["SecretStore:ConfigurationPrefix"] = "Secrets",
            ["Hangfire:Storage"] = "PostgreSQL",
            ["OpenTelemetry:ServiceName"] = "IntegrationTests"
        };

        if (additionalConfiguration is not null)
        {
            foreach (var (key, value) in additionalConfiguration)
            {
                configurationValues[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSeoIntelligenceInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider BuildProviderWithInMemoryDb(string storagePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "",
                ["Redis:ConnectionString"] = "",
                ["Storage:Provider"] = "Local",
                ["Storage:BasePath"] = storagePath,
                ["Storage:BucketName"] = "seo-intelligence",
                ["SecretStore:Provider"] = "Configuration",
                ["SecretStore:ConfigurationPrefix"] = "Secrets",
                ["Hangfire:Storage"] = "PostgreSQL",
                ["OpenTelemetry:ServiceName"] = "IntegrationTests"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSeoIntelligenceInfrastructure(configuration);
        services.AddDbContext<SeoIntelligenceDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static RakkoKeywordClientContext CreateRakkoContext()
        => new(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            ApiContractScopeId: SeoIntelligenceSeedData.DefaultRakkoContractScopeId,
            ContractScopeKey: SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CorrelationId: "corr-rakko-integration");

    private static StorageObjectKey ToStorageKey(string uri)
    {
        const string prefix = "storage://local/";
        Assert.StartsWith(prefix, uri, StringComparison.Ordinal);
        return new StorageObjectKey(uri[prefix.Length..]);
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }
}
