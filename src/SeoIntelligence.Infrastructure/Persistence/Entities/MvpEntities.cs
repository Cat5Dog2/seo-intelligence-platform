using System.Net;

namespace SeoIntelligence.Infrastructure.Persistence.Entities;

public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultLocation { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public string RetentionSettingsJson { get; set; } = "{}";
    public string NotificationDefaultsJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultLocation { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public string KpiJson { get; set; } = "{}";
    public string? Memo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}

public sealed class SiteEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}

public sealed class ApiCredentialEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string KeyRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

public sealed class ApiContractScopeEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int ApiKeyLimit { get; set; }
    public string DataUsageScope { get; set; } = string.Empty;
    public DateTime ConfirmedAt { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class NotificationChannelEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ChannelType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WebhookSecretRef { get; set; } = string.Empty;
    public string EventTypesJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

public sealed class NotificationDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid? JobId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string BeforeAfterJson { get; set; } = "{}";
    public string? CorrelationId { get; set; }
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class LocationEntity
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}

public sealed class LanguageEntity
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}

public sealed class ExternalApiCallEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? ApiCredentialId { get; set; }
    public Guid? ApiContractScopeId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string RequestUri { get; set; } = string.Empty;
    public string? ResponseHash { get; set; }
    public string? ResponseUri { get; set; }
    public string ContractScopeKey { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public int StatusCode { get; set; }
    public decimal ConsumedCredit { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorCode { get; set; }
    public string? CorrelationId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTime RetainedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class JobEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? ResultResourceType { get; set; }
    public Guid? ResultResourceId { get; set; }
    public string? ErrorJson { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class JobExternalRequestEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ExternalRequestId { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public Guid? SourceCallId { get; set; }
    public decimal ConsumedCredit { get; set; }
    public string? ErrorJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class KeywordSeedEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Seed { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class KeywordEntity
{
    public Guid Id { get; set; }
    public string NormalizedText { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TextHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class KeywordSuggestionEntity
{
    public Guid Id { get; set; }
    public Guid SeedId { get; set; }
    public Guid KeywordId { get; set; }
    public string Engine { get; set; } = string.Empty;
    public string SuggestClass { get; set; } = string.Empty;
    public int EngineCount { get; set; }
    public string? FirstSeenRange { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RelatedKeywordEntity
{
    public Guid Id { get; set; }
    public Guid SeedId { get; set; }
    public Guid KeywordId { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public string? MetricsSnapshotJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class QuestionEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SeedKeywordId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Importance { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class LsiPaaItemEntity
{
    public Guid Id { get; set; }
    public Guid SeedKeywordId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? KeywordId { get; set; }
    public string? QuestionText { get; set; }
    public decimal Importance { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RankingKeywordEntity
{
    public Guid Id { get; set; }
    public Guid SeedKeywordId { get; set; }
    public Guid KeywordId { get; set; }
    public int WordCount { get; set; }
    public decimal Relevance { get; set; }
    public string? MetricsSnapshotJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SearchVolumeJobEntity
{
    public Guid JobId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int AggregationMonths { get; set; }
    public string RequestOptionsJson { get; set; } = "{}";
    public string StatusJson { get; set; } = "{}";
}

public sealed class SearchVolumeResultEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid KeywordId { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public Guid? SourceCallId { get; set; }
    public bool CacheHit { get; set; }
    public string MetricsSnapshotJson { get; set; } = "{}";
    public string TrendsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class KeywordMetricEntity
{
    public Guid Id { get; set; }
    public Guid KeywordId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ContractScopeKey { get; set; } = string.Empty;
    public Guid? SourceCallId { get; set; }
    public int SearchVolume { get; set; }
    public decimal SeoDifficulty { get; set; }
    public decimal Cpc { get; set; }
    public decimal Competition { get; set; }
    public string? FirstSeenRange { get; set; }
    public DateTime FetchedAt { get; set; }
}

public sealed class KeywordMonthlyVolumeEntity
{
    public Guid Id { get; set; }
    public Guid KeywordId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ContractScopeKey { get; set; } = string.Empty;
    public Guid? SourceCallId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int SearchVolume { get; set; }
    public DateTime FetchedAt { get; set; }
}

public sealed class ProjectKeywordScoreEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public Guid? SourceCallId { get; set; }
    public decimal OpportunityScore { get; set; }
    public string ScoreComponentsJson { get; set; } = "{}";
    public DateTime ScoredAt { get; set; }
}

public sealed class DataExportEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ExportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public string? FileUri { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
