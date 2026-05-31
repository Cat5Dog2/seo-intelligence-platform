using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Infrastructure;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Redis;
using SeoIntelligence.Infrastructure.Secrets;
using SeoIntelligence.Infrastructure.Services;
using SeoIntelligence.Infrastructure.Storage;

namespace SeoIntelligence.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSeoIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addHangfireServer = false)
    {
        var databaseOptions = BindDatabaseOptions(configuration);
        var redisOptions = BindOptions<RedisOptions>(configuration, RedisOptions.SectionName);
        var hangfireOptions = BindOptions<HangfireOptions>(configuration, HangfireOptions.SectionName);
        var storageOptions = BindOptions<StorageOptions>(configuration, StorageOptions.SectionName);
        var secretStoreOptions = BindOptions<SecretStoreOptions>(configuration, SecretStoreOptions.SectionName);
        var openTelemetryOptions = BindOptions<OpenTelemetryOptions>(configuration, OpenTelemetryOptions.SectionName);

        services.TryAddSingleton(configuration);
        ConfigureOptions(services, databaseOptions, redisOptions, hangfireOptions, storageOptions, secretStoreOptions, openTelemetryOptions);
        ValidateConfiguredOptions(storageOptions, secretStoreOptions, hangfireOptions, openTelemetryOptions);

        if (!string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
        {
            services.AddSeoIntelligencePersistence(databaseOptions);
            AddSeoIntelligenceHangfire(services, databaseOptions, hangfireOptions, addHangfireServer);
        }

        AddStorage(services, storageOptions);
        services.AddSeoIntelligenceAdministration();
        services.AddSingleton<ISecretStore, ConfigurationSecretStore>();

        if (!string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            ValidateOptionErrors(redisOptions.Validate());
            services.AddSingleton<IRedisCoordinator, StackExchangeRedisCoordinator>();
        }

        services.AddSingleton<IInfrastructureReadinessProbe, InfrastructureReadinessProbe>();

        return services;
    }

    private static DatabaseOptions BindDatabaseOptions(IConfiguration configuration)
        => new()
        {
            ConnectionString = configuration.GetConnectionString(DatabaseOptions.DefaultConnectionName)
        };

    private static TOptions BindOptions<TOptions>(IConfiguration configuration, string sectionName)
        where TOptions : new()
    {
        var options = new TOptions();
        configuration.GetSection(sectionName).Bind(options);
        return options;
    }

    private static void ConfigureOptions(
        IServiceCollection services,
        DatabaseOptions databaseOptions,
        RedisOptions redisOptions,
        HangfireOptions hangfireOptions,
        StorageOptions storageOptions,
        SecretStoreOptions secretStoreOptions,
        OpenTelemetryOptions openTelemetryOptions)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = databaseOptions.ConnectionString);
        services.AddOptions<RedisOptions>()
            .Configure(options => options.ConnectionString = redisOptions.ConnectionString);
        services.AddOptions<HangfireOptions>()
            .Configure(options =>
            {
                options.Storage = hangfireOptions.Storage;
                options.Queues = hangfireOptions.Queues;
                options.WorkerCount = hangfireOptions.WorkerCount;
            });
        services.AddOptions<StorageOptions>()
            .Configure(options =>
            {
                options.Provider = storageOptions.Provider;
                options.BasePath = storageOptions.BasePath;
                options.Endpoint = storageOptions.Endpoint;
                options.BucketName = storageOptions.BucketName;
            });
        services.AddOptions<SecretStoreOptions>()
            .Configure(options =>
            {
                options.Provider = secretStoreOptions.Provider;
                options.ConfigurationPrefix = secretStoreOptions.ConfigurationPrefix;
            });
        services.AddOptions<OpenTelemetryOptions>()
            .Configure(options =>
            {
                options.Enabled = openTelemetryOptions.Enabled;
                options.ServiceName = openTelemetryOptions.ServiceName;
                options.OtlpEndpoint = openTelemetryOptions.OtlpEndpoint;
            });
    }

    private static void ValidateConfiguredOptions(
        StorageOptions storageOptions,
        SecretStoreOptions secretStoreOptions,
        HangfireOptions hangfireOptions,
        OpenTelemetryOptions openTelemetryOptions)
    {
        ValidateOptionErrors(storageOptions.Validate());
        ValidateOptionErrors(secretStoreOptions.Validate());
        ValidateOptionErrors(hangfireOptions.Validate());
        ValidateOptionErrors(openTelemetryOptions.Validate());
    }

    private static void ValidateOptionErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void AddStorage(IServiceCollection services, StorageOptions storageOptions)
    {
        if (string.Equals(storageOptions.Provider, StorageOptions.MinioProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, MinioEndpointObjectStorage>();
            return;
        }

        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
    }

    private static void AddSeoIntelligenceHangfire(
        IServiceCollection services,
        DatabaseOptions databaseOptions,
        HangfireOptions hangfireOptions,
        bool addHangfireServer)
    {
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(databaseOptions.ConnectionString!)));

        if (!addHangfireServer)
        {
            return;
        }

        services.AddHangfireServer(options =>
        {
            options.Queues = hangfireOptions.Queues;

            if (hangfireOptions.WorkerCount.HasValue)
            {
                options.WorkerCount = hangfireOptions.WorkerCount.Value;
            }
        });
    }
}
