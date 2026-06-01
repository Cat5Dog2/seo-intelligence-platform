using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Api.Endpoints;
using SeoIntelligence.Api.Health;
using SeoIntelligence.Contracts.Api;
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
        app.Logger.LogInformation(
            "HTTP request completed for {endpoint} with {status_code} in {elapsed_ms} ms.",
            context.Request.Path.Value,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
});

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
app.MapKeywordDiscoveryEndpoints();

app.MapGet("/openapi/v1.json", OpenApiDocumentEndpoint.GetV1);

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.Run();

public partial class Program;
