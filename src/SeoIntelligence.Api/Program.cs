using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("API host is running."),
        tags: ["live", "ready"]);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new
{
    service = "SeoIntelligence.Api",
    diagnostics = SeoIntelligenceDiagnostics.ServiceName,
    status = "running"
}));

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

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
