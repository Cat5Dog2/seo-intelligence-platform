using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Api.Endpoints;
using SeoIntelligence.Api.Health;
using SeoIntelligence.Api.Security;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();
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
builder.Services.AddTrustedProxyForwardedHeaders(builder.Configuration, builder.Environment);
builder.Services.AddApiServiceKeyAuthentication(builder.Configuration);
builder.Services.AddApiRateLimiting();
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

// First: everything after this reads RemoteIpAddress and Scheme. UseHttpsRedirection would
// otherwise see http behind a TLS-terminating proxy and redirect in a loop, and the rate
// limiter would partition every caller under Caddy's address.
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next(context);
    }
    finally
    {
        stopwatch.Stop();

        // The route pattern, not the resolved path. The report share endpoints carry the share
        // token in the path, and logging the raw value would write a live credential into every
        // log sink - the same secret the database deliberately only keeps a hash of. Requests that
        // matched no endpoint have no pattern, and their path is not logged either: an unmatched
        // path is attacker-controlled and is exactly where a leaked token would show up.
        var endpoint = context.GetEndpoint() as RouteEndpoint;
        app.Logger.LogInformation(
            "HTTP request completed for {endpoint} with {status_code} in {elapsed_ms} ms.",
            endpoint?.RoutePattern.RawText ?? "(unmatched)",
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet(
    "/api",
    (
        HttpContext context,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null) =>
    {
        var query = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            SortBy = sortBy,
            OrderBy = orderBy,
            Q = q
        };
        var validationErrors = query.Validate();
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(context, validationErrors);
        }

        return ApiResponseResults.Ok(
            context,
            new ApiStatusResponse(
                "SeoIntelligence.Api",
                SeoIntelligenceDiagnostics.ServiceName,
                "running"));
    });

app.MapAdministrationEndpoints();
app.MapJobEndpoints();
app.MapDashboardEndpoints();
app.MapKeywordDiscoveryEndpoints();
app.MapSearchVolumeEndpoints();
app.MapCompetitiveAnalysisEndpoints();
app.MapTopicClusterEndpoints();
app.MapContentAnalysisEndpoints();
app.MapRankMonitoringEndpoints();
app.MapDataExportEndpoints();
app.MapPhase3FoundationEndpoints();

app.MapGet("/openapi/v1.json", OpenApiDocumentEndpoint.GetV1);

// Container health probes and the shared Caddy gate reach these before any service key is
// available, so they stay anonymous. MapHealthChecks accepts any HTTP method by default;
// constraining them to GET keeps the anonymous surface to what api_design.md documents.
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).AllowAnonymous().WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).AllowAnonymous().WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));

app.Run();

public partial class Program;
