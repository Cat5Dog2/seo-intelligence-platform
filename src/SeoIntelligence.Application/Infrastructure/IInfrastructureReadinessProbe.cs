namespace SeoIntelligence.Application.Infrastructure;

public interface IInfrastructureReadinessProbe
{
    Task<IReadOnlyList<InfrastructureReadinessCheck>> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record InfrastructureReadinessCheck(
    string Name,
    bool IsHealthy,
    string Message);
