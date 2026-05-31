namespace SeoIntelligence.Application.ProjectContext;

public interface IProjectContextService
{
    ProjectContext Create(Guid workspaceId, Guid? projectId = null, string? correlationId = null);

    bool IsInProjectScope(ProjectContext context, Guid resourceProjectId);

    ProjectScopeDecision ValidateScope(
        ProjectContext context,
        Guid resourceProjectId,
        ProjectScopeMismatchBehavior mismatchBehavior = ProjectScopeMismatchBehavior.NotFound);
}
