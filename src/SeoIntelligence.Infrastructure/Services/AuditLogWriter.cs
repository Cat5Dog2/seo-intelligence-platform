using System.Text.Json;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class AuditLogWriter(
    SeoIntelligenceDbContext dbContext,
    TimeProvider timeProvider)
    : IAuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(ProjectContext context, AuditLogWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        dbContext.AuditLogs.Add(new AuditLogEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            Actor = RequireAuditText(context.Actor, nameof(context.Actor)),
            Action = RequireAuditText(request.Action, nameof(request.Action)),
            ResourceType = RequireAuditText(request.ResourceType, nameof(request.ResourceType)),
            ResourceId = RequireAuditText(request.ResourceId, nameof(request.ResourceId)),
            BeforeAfterJson = request.BeforeAfter is null ? "{}" : JsonSerializer.Serialize(request.BeforeAfter, JsonOptions),
            CorrelationId = context.CorrelationId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    public async Task RecordAsync(
        ProjectContext context,
        AuditLogWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        Add(context, request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string RequireAuditText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}
