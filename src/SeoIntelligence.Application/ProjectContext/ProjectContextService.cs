using SeoIntelligence.Domain.Common;

namespace SeoIntelligence.Application.ProjectContext;

public sealed class ProjectContextService : IProjectContextService
{
    private readonly TimeProvider _timeProvider;

    public ProjectContextService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ProjectContext Create(Guid workspaceId, Guid? projectId = null, string? correlationId = null)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("workspaceId is required.", nameof(workspaceId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("projectId must not be empty when provided.", nameof(projectId));
        }

        return new ProjectContext(
            workspaceId,
            projectId,
            SystemActor.Developer,
            UtcDateTime.EnsureUtc(_timeProvider.GetUtcNow()),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim());
    }

    public bool IsInProjectScope(ProjectContext context, Guid resourceProjectId)
        => context.ProjectId.HasValue
            && context.ProjectId.Value == resourceProjectId
            && resourceProjectId != Guid.Empty;
}
