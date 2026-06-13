using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class AdministrationAuditRecorder(IAuditLogWriter auditLogWriter)
{
    public void AddApiCredentialAudit(
        ProjectExecutionContext context,
        string action,
        ApiCredentialEntity credential,
        object? before)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.ApiCredential,
                credential.Id.ToString("D"),
                new
                {
                    before,
                    after = ToApiCredentialAuditSnapshot(credential)
                }));

    public static object ToApiCredentialAuditSnapshot(ApiCredentialEntity entity)
        => new
        {
            provider = entity.Provider,
            keyRef = entity.KeyRef,
            status = entity.Status,
            disabledAt = entity.DisabledAt
        };
}
