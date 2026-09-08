using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Infrastructure;

namespace IntegrationTests;

public sealed class RedisCoordinatorIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "REDIS_INTEGRATION_CONNECTION_STRING";

    [RedisIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task RealRedisSupportsStringLifecycleAndExclusiveLease()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var storagePath = Path.Combine(Path.GetTempPath(), "seo-intelligence-tests", Guid.NewGuid().ToString("N"));
        var keyPrefix = $"seo-intelligence-tests:{Guid.NewGuid():N}";
        var valueKey = new RedisKey($"{keyPrefix}:value");
        var lockKey = new RedisKey($"{keyPrefix}:lock");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var provider = BuildProvider(storagePath, connectionString!);

        try
        {
            var coordinator = provider.GetRequiredService<IRedisCoordinator>();

            await coordinator.SetStringAsync(valueKey, "redis-3-smoke", TimeSpan.FromMinutes(1), timeout.Token);
            Assert.Equal("redis-3-smoke", await coordinator.GetStringAsync(valueKey, timeout.Token));
            Assert.True(await coordinator.RemoveAsync(valueKey, timeout.Token));
            Assert.Null(await coordinator.GetStringAsync(valueKey, timeout.Token));

            var firstLease = await coordinator.TryAcquireLockAsync(
                lockKey,
                "owner-a",
                TimeSpan.FromSeconds(30),
                timeout.Token);
            Assert.NotNull(firstLease);

            try
            {
                var competingLease = await coordinator.TryAcquireLockAsync(
                    lockKey,
                    "owner-b",
                    TimeSpan.FromSeconds(30),
                    timeout.Token);
                Assert.Null(competingLease);
            }
            finally
            {
                await firstLease.DisposeAsync();
            }

            var successorLease = await coordinator.TryAcquireLockAsync(
                lockKey,
                "owner-b",
                TimeSpan.FromSeconds(30),
                timeout.Token);
            Assert.NotNull(successorLease);
            await successorLease.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildProvider(string storagePath, string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = connectionString,
                ["Storage:Provider"] = "Local",
                ["Storage:BasePath"] = storagePath,
                ["Storage:BucketName"] = "seo-intelligence",
                ["SecretStore:Provider"] = "Configuration",
                ["SecretStore:ConfigurationPrefix"] = "Secrets",
                ["Hangfire:Storage"] = "PostgreSQL",
                ["OpenTelemetry:ServiceName"] = "RedisCoordinatorIntegrationTests"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSeoIntelligenceInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class RedisIntegrationFactAttribute : FactAttribute
    {
        public RedisIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
            {
                Skip = $"Set {ConnectionStringEnvironmentVariable} to run the real Redis integration test.";
            }
        }
    }
}
