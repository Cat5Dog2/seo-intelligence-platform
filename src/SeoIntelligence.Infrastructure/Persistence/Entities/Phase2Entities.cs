namespace SeoIntelligence.Infrastructure.Persistence.Entities;

public sealed class CompetitorSiteEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal DuplicateRate { get; set; }
    public decimal EstimatedTraffic { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class InfluxKeywordResultEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Target { get; set; } = string.Empty;
    public Guid KeywordId { get; set; }
    public int Rank { get; set; }
    public string RankedUrl { get; set; } = string.Empty;
    public decimal EstimatedTraffic { get; set; }
    public string MetricsSnapshotJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class InfluxPageResultEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Target { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int KeywordCount { get; set; }
    public decimal EstimatedTraffic { get; set; }
    public decimal TrafficValue { get; set; }
    public Guid? TopKeywordId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CompetitiveResultEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string SiteDomain { get; set; } = string.Empty;
    public decimal EstimatedTraffic { get; set; }
    public decimal TrafficValue { get; set; }
    public int KeywordCount { get; set; }
    public decimal DuplicateRate { get; set; }
    public string UniqueCountsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class ContentSearchResultEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal EstimatedTraffic { get; set; }
    public decimal TrafficValue { get; set; }
    public Guid? TopKeywordId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SerpHeadlinePageEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public int Rank { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int HeadlineCount { get; set; }
    public int WordCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SerpHeadlineEntity
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public short Level { get; set; }
    public string Text { get; set; } = string.Empty;
    public int OrderNo { get; set; }
}

public sealed class CoOccurrenceWordEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string OccurrenceCountsJson { get; set; } = "{}";
    public string SiteCountsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class CoOccurrencePageDetailEntity
{
    public Guid Id { get; set; }
    public Guid CoWordId { get; set; }
    public int Rank { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
    public int CountInHeadline { get; set; }
    public int CountInTitle { get; set; }
}

public sealed class TopicClusterEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? RepresentativeKeywordId { get; set; }
    public decimal Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ClusterKeywordEntity
{
    public Guid ClusterId { get; set; }
    public Guid KeywordId { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal OpportunityScore { get; set; }
    public string? IntentLabel { get; set; }
}

public sealed class ArticleBriefEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ClusterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? TargetKeywordId { get; set; }
    public int CurrentVersion { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string ReviewStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ArtifactVersionEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public Guid ArtifactId { get; set; }
    public int VersionNo { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? ContentUri { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string CreatedBy { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RankCheckJobEntity
{
    public Guid JobId { get; set; }
    public int Depth { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public bool WithMetrics { get; set; }
    public string RequestOptionsJson { get; set; } = "{}";
    public string StatusJson { get; set; } = "{}";
}

public sealed class RankCheckTargetEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Target { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
}

public sealed class RankResultEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid KeywordId { get; set; }
    public string Target { get; set; } = string.Empty;
    public int Position { get; set; }
    public string RankedUrl { get; set; } = string.Empty;
    public decimal EstimatedTraffic { get; set; }
    public string MetricsSnapshotJson { get; set; } = "{}";
    public Guid? SourceCallId { get; set; }
    public string ContractScopeKey { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}

public sealed class AlertEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string ConditionJson { get; set; } = "{}";
    public Guid? NotificationChannelId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AlertEventEntity
{
    public Guid Id { get; set; }
    public Guid AlertId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? KeywordId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PreviousValueJson { get; set; } = "{}";
    public string CurrentValueJson { get; set; } = "{}";
    public string EvidenceJson { get; set; } = "{}";
    public Guid? NotificationDeliveryId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
