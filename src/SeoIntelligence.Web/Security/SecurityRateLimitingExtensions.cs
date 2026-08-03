using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SeoIntelligence.Application.Security;

namespace SeoIntelligence.Web.Security;

public static class SecurityRateLimitingExtensions
{
    /// <summary>
    /// Limits credential-guessing attempts. Identity's own lockout protects a known account;
    /// these limits also protect against someone spraying many candidate user names.
    /// </summary>
    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddFixedWindowPolicy(
                options,
                SecurityRateLimitPolicyNames.Login,
                permitLimit: 10,
                window: TimeSpan.FromMinutes(1),
                useAuthenticatedUser: false);

            AddFixedWindowPolicy(
                options,
                SecurityRateLimitPolicyNames.PasswordChange,
                permitLimit: 5,
                window: TimeSpan.FromMinutes(10));
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window,
        bool useAuthenticatedUser = true)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(httpContext, policyName, useAuthenticatedUser),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = 0,
                    Window = window
                }));
    }

    private static string GetPartitionKey(
        HttpContext httpContext,
        string policyName,
        bool useAuthenticatedUser)
    {
        var userId = useAuthenticatedUser
            ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var client = string.IsNullOrWhiteSpace(userId)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : userId;

        return $"{policyName}:{client}";
    }
}
