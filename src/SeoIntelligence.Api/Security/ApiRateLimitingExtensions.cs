using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Api.Security;

public static class ApiRateLimitingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The routes the anonymous report share endpoints are mapped at.</summary>
    private static readonly string[] RateLimitedRoutes =
    [
        "/api/report-shares/{token}",
        "/api/report-shares/{token}/content"
    ];

    /// <summary>
    /// Limits the anonymous report share endpoints.
    /// <para>
    /// Every other API endpoint requires the service key, so an unauthenticated caller cannot reach
    /// the database through them at all. The share endpoints are reachable by anyone who knows the
    /// public path, and a request carrying an unknown token still runs a lookup and writes a
    /// rejection audit row - so spraying random tokens would otherwise be a cheap way to grow the
    /// audit table and load the database.
    /// </para>
    /// <para>
    /// Two limiters are chained: a per-address window that stops one client from spraying, and a
    /// concurrency cap across all callers so a distributed attempt cannot saturate the database
    /// connection pool either. They are applied through the global limiter rather than an endpoint
    /// policy because a named policy resolves to a single limiter, and every other endpoint is left
    /// explicitly unlimited.
    /// </para>
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // RejectionStatusCode alone sends an empty body. Every other API failure answers with
            // the common envelope, and the OpenAPI document says 429 does too, so it is written
            // here rather than left for the caller to guess at.
            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.ContentType = "application/json; charset=utf-8";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await JsonSerializer.SerializeAsync(
                    httpContext.Response.Body,
                    ApiResponseEnvelope<object>.Failure(
                        httpContext.GetCorrelationId(),
                        [new ApiError("RateLimit.Exceeded", "Too many requests. Retry later.")]),
                    JsonOptions,
                    cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    IsRateLimited(httpContext)
                        ? RateLimitPartition.GetFixedWindowLimiter(
                            ResolveClientPartitionKey(httpContext),
                            _ => new FixedWindowRateLimiterOptions
                            {
                                AutoReplenishment = true,
                                PermitLimit = 30,
                                QueueLimit = 0,
                                Window = TimeSpan.FromMinutes(1)
                            })
                        : RateLimitPartition.GetNoLimiter<string>("unlimited")),
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    IsRateLimited(httpContext)
                        ? RateLimitPartition.GetConcurrencyLimiter(
                            "report-shares",
                            _ => new ConcurrencyLimiterOptions
                            {
                                PermitLimit = 8,
                                QueueLimit = 0
                            })
                        : RateLimitPartition.GetNoLimiter<string>("unlimited")));
        });

        return services;
    }

    /// <summary>
    /// The key one caller's requests are counted under.
    /// <para>
    /// Behind Caddy the forwarded-headers middleware has already replaced
    /// <c>RemoteIpAddress</c> with the caller's address, so this partitions per client rather than
    /// per proxy. If that middleware were ever disabled, every request would share the proxy's
    /// address and one caller could exhaust the window for everyone - so the deployment note in
    /// docs/docker_deployment.md keeps <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> on.
    /// </para>
    /// <para>
    /// Requests with no remote address share a single partition. That is deliberate: an unknown
    /// caller must not get an unlimited one of its own.
    /// </para>
    /// </summary>
    public static string ResolveClientPartitionKey(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Matched on the route pattern rather than the request path: the path carries the share token,
    /// and a prefix comparison against it would put the token into the partition key.
    /// </summary>
    public static bool IsRateLimited(HttpContext httpContext)
        => httpContext.GetEndpoint() is RouteEndpoint endpoint
            && Array.Exists(
                RateLimitedRoutes,
                route => string.Equals(endpoint.RoutePattern.RawText, route, StringComparison.Ordinal));
}
