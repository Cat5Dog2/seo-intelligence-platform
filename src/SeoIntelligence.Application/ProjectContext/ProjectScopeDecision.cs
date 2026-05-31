namespace SeoIntelligence.Application.ProjectContext;

public enum ProjectScopeMismatchBehavior
{
    NotFound,
    Forbidden
}

public enum ProjectScopeDecisionKind
{
    Allowed,
    NotFound,
    Forbidden
}

public sealed record ProjectScopeDecision(ProjectScopeDecisionKind Kind)
{
    public bool IsAllowed => Kind == ProjectScopeDecisionKind.Allowed;

    public static ProjectScopeDecision Allowed { get; } = new(ProjectScopeDecisionKind.Allowed);

    public static ProjectScopeDecision Rejected(ProjectScopeMismatchBehavior behavior)
        => behavior == ProjectScopeMismatchBehavior.Forbidden
            ? new ProjectScopeDecision(ProjectScopeDecisionKind.Forbidden)
            : new ProjectScopeDecision(ProjectScopeDecisionKind.NotFound);
}
