using SeoIntelligence.Application.Common;

namespace SeoIntelligence.Application.Auditing;

public sealed record AuditLogSearchQuery(
    SearchQuery Query,
    string? Actor = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? CorrelationId = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null);
