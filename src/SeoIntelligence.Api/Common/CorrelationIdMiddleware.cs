using System.Diagnostics;
using SeoIntelligence.Application.Diagnostics;

namespace SeoIntelligence.Api.Common;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.SetCorrelationId(correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHttpContextExtensions.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new SeoIntelligenceLogContext(CorrelationId: correlationId).ToScopeDictionary());
        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHttpContextExtensions.HeaderName, out var values))
        {
            var candidate = values.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxCorrelationIdLength)
            {
                return candidate;
            }
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
