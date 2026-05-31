using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Infrastructure;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure;

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
