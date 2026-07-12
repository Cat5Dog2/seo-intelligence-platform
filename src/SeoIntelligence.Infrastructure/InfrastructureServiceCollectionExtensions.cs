using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.Ai;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Infrastructure;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Sharing;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.RakkoKeyword;
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
        var rakkoKeywordOptions = BindOptions<RakkoKeywordOptions>(configuration, RakkoKeywordOptions.SectionName);

        services.TryAddSingleton(configuration);
        ConfigureOptions(services, databaseOptions, redisOptions, hangfireOptions, storageOptions, secretStoreOptions, openTelemetryOptions, rakkoKeywordOptions);
        ValidateConfiguredOptions(storageOptions, secretStoreOptions, hangfireOptions, openTelemetryOptions, rakkoKeywordOptions);

        if (!string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
        {
            services.AddSeoIntelligencePersistence(databaseOptions);
            AddSeoIntelligenceHangfire(services, databaseOptions, hangfireOptions, addHangfireServer);
        }

        AddStorage(services, storageOptions);
        services.AddSeoIntelligenceAdministration();
        services.TryAddSingleton<IPromptRedactor, SensitivePromptRedactor>();
        services.TryAddSingleton<IShareTokenService, ShareTokenService>();
        services.AddSingleton<ISecretStore, ConfigurationSecretStore>();
        AddRakkoKeywordClient(services, rakkoKeywordOptions);

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
            ConnectionString = DatabaseConnectionStringResolver.Resolve(configuration)
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
        OpenTelemetryOptions openTelemetryOptions,
        RakkoKeywordOptions rakkoKeywordOptions)
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
        services.AddOptions<RakkoKeywordOptions>()
            .Configure(options =>
            {
                options.Mode = rakkoKeywordOptions.Mode;
                options.BaseUrl = rakkoKeywordOptions.BaseUrl;
                options.ApiKeySecretRef = rakkoKeywordOptions.ApiKeySecretRef;
                options.TimeoutSeconds = rakkoKeywordOptions.TimeoutSeconds;
                options.LongTimeoutSeconds = rakkoKeywordOptions.LongTimeoutSeconds;
                options.UserAgentProduct = rakkoKeywordOptions.UserAgentProduct;
                options.UserAgentVersion = rakkoKeywordOptions.UserAgentVersion;
                options.EnvironmentName = rakkoKeywordOptions.EnvironmentName;
                options.RawDataRetentionMonths = rakkoKeywordOptions.RawDataRetentionMonths;
                options.MockStatusCode = rakkoKeywordOptions.MockStatusCode;
            });
    }

    private static void ValidateConfiguredOptions(
        StorageOptions storageOptions,
        SecretStoreOptions secretStoreOptions,
        HangfireOptions hangfireOptions,
        OpenTelemetryOptions openTelemetryOptions,
        RakkoKeywordOptions rakkoKeywordOptions)
    {
        ValidateOptionErrors(storageOptions.Validate());
        ValidateOptionErrors(secretStoreOptions.Validate());
        ValidateOptionErrors(hangfireOptions.Validate());
        ValidateOptionErrors(openTelemetryOptions.Validate());
        ValidateOptionErrors(rakkoKeywordOptions.Validate());
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

    private static void AddRakkoKeywordClient(IServiceCollection services, RakkoKeywordOptions options)
    {
        services.AddScoped<IRakkoKeywordCallRecorder, RakkoKeywordCallRecorder>();
        services.AddScoped<IRakkoKeywordExternalApiCallStore, OptionalEfExternalApiCallStore>();
        services.AddScoped<IRakkoKeywordMetricCache, OptionalEfRakkoKeywordMetricCache>();

        if (string.Equals(options.Mode, RakkoKeywordOptions.RealMode, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IRakkoKeywordClient>(serviceProvider =>
            {
                var configuredOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RakkoKeywordOptions>>().Value;
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(configuredOptions.BaseUrl, UriKind.Absolute),
                    Timeout = Timeout.InfiniteTimeSpan
                };

                return new RakkoKeywordRealClient(
                    httpClient,
                    serviceProvider.GetRequiredService<ISecretStore>(),
                    serviceProvider.GetRequiredService<IRakkoKeywordCallRecorder>(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RakkoKeywordOptions>>(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RakkoKeywordRealClient>>());
            });
            return;
        }

        services.AddScoped<IRakkoKeywordClient, RakkoKeywordMockClient>();
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
