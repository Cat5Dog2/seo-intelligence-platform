namespace SeoIntelligence.Application.ProjectContext;

public sealed record ProjectContext(
    Guid WorkspaceId,
    Guid? ProjectId,
    string Actor,
    DateTimeOffset RequestedAtUtc,
    string? CorrelationId)
{
    public bool HasProject => ProjectId.HasValue;
}
