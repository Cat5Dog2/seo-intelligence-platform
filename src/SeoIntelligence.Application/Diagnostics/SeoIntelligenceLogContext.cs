namespace SeoIntelligence.Application.Diagnostics;

public sealed record SeoIntelligenceLogContext(
    Guid? WorkspaceId = null,
    Guid? ProjectId = null,
    Guid? JobId = null,
    string? ExternalRequestId = null,
    string? CorrelationId = null)
{
    public IReadOnlyDictionary<string, object?> ToScopeDictionary()
        => new Dictionary<string, object?>
        {
            ["workspace_id"] = WorkspaceId,
            ["project_id"] = ProjectId,
            ["job_id"] = JobId,
            ["external_request_id"] = ExternalRequestId,
            ["correlation_id"] = CorrelationId
        };
}
