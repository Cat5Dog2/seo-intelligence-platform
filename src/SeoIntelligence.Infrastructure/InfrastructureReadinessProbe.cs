using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Infrastructure;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Infrastructure;

internal sealed class InfrastructureReadinessProbe(
    IServiceProvider serviceProvider,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<RedisOptions> redisOptions,
    IObjectStorage storage,
    ISecretStore secretStore)
    : IInfrastructureReadinessProbe
{
    public async Task<IReadOnlyList<InfrastructureReadinessCheck>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<InfrastructureReadinessCheck>
        {
            await CheckDatabaseAsync(cancellationToken),
            await CheckRedisAsync(cancellationToken),
            await CheckStorageAsync(cancellationToken),
            await CheckSecretStoreAsync(cancellationToken)
        };

        return checks;
    }

    private async Task<InfrastructureReadinessCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(databaseOptions.Value.ConnectionString))
        {
            return Healthy("db", "Database is not configured.");
        }

        try
        {
            var factory = serviceProvider.GetRequiredService<IDbContextFactory<SeoIntelligenceDbContext>>();
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return Unhealthy("db", "Database connection failed.");
            }

            // Migrations are applied by the one-shot migrate step, not at startup;
            // readiness must fail when the schema lags the deployed application.
            if (context.Database.IsRelational())
            {
                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count > 0)
                {
                    return Unhealthy(
                        "db",
                        $"Database has {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}. Apply migrations before serving traffic.");
                }
            }

            return Healthy("db", "Database connection succeeded and schema is up to date.");
        }
        catch (Exception exception)
        {
            return Unhealthy("db", $"Database connection failed: {exception.GetType().Name}.");
        }
    }

    private async Task<InfrastructureReadinessCheck> CheckRedisAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(redisOptions.Value.ConnectionString))
        {
            return Healthy("redis", "Redis is not configured.");
        }

        try
        {
            var redis = serviceProvider.GetRequiredService<IRedisCoordinator>();
            await redis.PingAsync(cancellationToken);
            return Healthy("redis", "Redis ping succeeded.");
        }
        catch (Exception exception)
        {
            return Unhealthy("redis", $"Redis ping failed: {exception.GetType().Name}.");
        }
    }

    private async Task<InfrastructureReadinessCheck> CheckStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await storage.CheckConnectivityAsync(cancellationToken);
            return result.IsHealthy
                ? Healthy("storage", result.Message)
                : Unhealthy("storage", result.Message);
        }
        catch (Exception exception)
        {
            return Unhealthy("storage", $"Storage check failed: {exception.GetType().Name}.");
        }
    }

    private async Task<InfrastructureReadinessCheck> CheckSecretStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await secretStore.CheckConnectivityAsync(cancellationToken);
            return result.IsHealthy
                ? Healthy("secret_store", result.Message)
                : Unhealthy("secret_store", result.Message);
        }
        catch (Exception exception)
        {
            return Unhealthy("secret_store", $"Secret Store check failed: {exception.GetType().Name}.");
        }
    }

    private static InfrastructureReadinessCheck Healthy(string name, string message)
        => new(name, true, message);

    private static InfrastructureReadinessCheck Unhealthy(string name, string message)
        => new(name, false, message);
}
