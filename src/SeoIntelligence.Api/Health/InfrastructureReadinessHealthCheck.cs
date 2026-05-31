using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Application.Infrastructure;

namespace SeoIntelligence.Api.Health;

internal sealed class InfrastructureReadinessHealthCheck(IInfrastructureReadinessProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var checks = await probe.CheckAsync(cancellationToken);
        var data = checks.ToDictionary(
            check => check.Name,
            check => (object)check.Message,
            StringComparer.Ordinal);

        if (checks.All(check => check.IsHealthy))
        {
            return HealthCheckResult.Healthy("Infrastructure readiness succeeded.", data);
        }

        return HealthCheckResult.Unhealthy("Infrastructure readiness failed.", data: data);
    }
}
