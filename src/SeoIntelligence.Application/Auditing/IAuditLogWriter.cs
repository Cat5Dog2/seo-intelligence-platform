using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Application.Auditing;

public interface IAuditLogWriter
{
    void Add(ProjectExecutionContext context, AuditLogWriteRequest request);

    Task RecordAsync(ProjectExecutionContext context, AuditLogWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record AuditLogWriteRequest(
    string Action,
    string ResourceType,
    string ResourceId,
    object? BeforeAfter = null);
