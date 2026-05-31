using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Worker;

var builder = Host.CreateApplicationBuilder(args);

RegisterSeoIntelligenceOptions(builder.Services, builder.Configuration);

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId |
        ActivityTrackingOptions.Baggage;
});

builder.Services.AddSingleton(SeoIntelligenceDiagnostics.ActivitySource);
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.Meter);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static void RegisterSeoIntelligenceOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<DatabaseOptions>()
        .Configure(options => options.ConnectionString =
            configuration.GetConnectionString(DatabaseOptions.DefaultConnectionName));

    services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
    services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));
    services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
    services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
}
