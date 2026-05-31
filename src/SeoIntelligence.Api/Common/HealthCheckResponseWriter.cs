using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Api.Common;

internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var data = new HealthCheckResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(
                pair => pair.Key,
                pair => new HealthCheckEntryResponse(
                    pair.Value.Status.ToString(),
                    pair.Value.Description,
                    pair.Value.Data.ToDictionary(data => data.Key, data => data.Value)),
                StringComparer.Ordinal));

        if (report.Status == HealthStatus.Healthy)
        {
            return JsonSerializer.SerializeAsync(
                context.Response.Body,
                ApiResponseEnvelope<HealthCheckResponse>.Success(context.GetCorrelationId(), data),
                JsonOptions);
        }

        var errors = report.Entries
            .Where(pair => pair.Value.Status != HealthStatus.Healthy)
            .Select(pair => new ApiError(
                "Health.Unhealthy",
                pair.Value.Description ?? $"{pair.Key} is {pair.Value.Status}.",
                pair.Key))
            .ToArray();

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            ApiResponseEnvelope<HealthCheckResponse>.Failure(context.GetCorrelationId(), errors),
            JsonOptions);
    }

    private sealed record HealthCheckResponse(
        string Status,
        IReadOnlyDictionary<string, HealthCheckEntryResponse> Checks);

    private sealed record HealthCheckEntryResponse(
        string Status,
        string? Description,
        IReadOnlyDictionary<string, object> Data);
}
