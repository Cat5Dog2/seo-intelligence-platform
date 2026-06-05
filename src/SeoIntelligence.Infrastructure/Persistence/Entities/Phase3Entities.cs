namespace SeoIntelligence.Infrastructure.Persistence.Entities;

public sealed class RewriteTaskEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public decimal PriorityScore { get; set; }
    public string ReasonJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public string AssigneeActor { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CannibalizationCandidateEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public string PrimaryUrl { get; set; } = string.Empty;
    public string CompetingUrlsJson { get; set; } = "[]";
    public decimal SeverityScore { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public string RecommendationJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}

public sealed class ReportEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public string? FileUri { get; set; }
    public string? ShareTokenHash { get; set; }
    public DateTime? ShareExpiresAt { get; set; }
    public DateTime? ShareRevokedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class DataImportEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ImportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SourceFileUri { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ValidationErrorsJson { get; set; } = "[]";
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class ExternalConnectorSettingEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ConnectorType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AuthRef { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

public sealed class ExternalConnectorRunEntity
{
    public Guid Id { get; set; }
    public Guid ConnectorSettingId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string RunType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string ResultSummaryJson { get; set; } = "{}";
    public string? ErrorJson { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AiSessionEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiMessageEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string MessageRole { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string ToolCallsJson { get; set; } = "[]";
    public string ReferenceDataJson { get; set; } = "{}";
    public string RedactionStatus { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string TokenUsage { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
