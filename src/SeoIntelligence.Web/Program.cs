using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Web.Components;

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

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.ActivitySource);
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.Meter);
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Web host is running."),
        tags: ["live", "ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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
