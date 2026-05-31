using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Api.Health;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSeoIntelligenceInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("API host is running."),
        tags: ["live"])
    .AddCheck<InfrastructureReadinessHealthCheck>(
        "infrastructure",
        tags: ["ready"]);

var app = builder.Build();

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var correlationId = GetCorrelationId(context);
    using var scope = app.Logger.BeginScope(new SeoIntelligenceLogContext(CorrelationId: correlationId).ToScopeDictionary());
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next(context);
    }
    finally
    {
        stopwatch.Stop();
        app.Logger.LogInformation(
            "HTTP request completed for {endpoint} with {status_code} in {elapsed_ms} ms.",
            context.Request.Path.Value,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
});

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

static string GetCorrelationId(HttpContext context)
{
    if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var values)
        && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
    {
        return values.First()!;
    }

    return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
