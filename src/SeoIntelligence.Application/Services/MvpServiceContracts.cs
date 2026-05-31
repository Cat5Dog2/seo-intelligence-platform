using SeoIntelligence.Application.Common;
using SeoIntelligence.Domain.Common;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Application.Services;

public interface IAdministrationService
{
    Task<Result<WorkspaceSummary>> GetWorkspaceAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ProjectSummary>>> SearchProjectsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SiteSummary>>> SearchSitesAsync(ProjectExecutionContext context, Guid projectId, SearchQuery query, CancellationToken cancellationToken = default);
}

public interface IMasterDataService
{
    Task<Result<JobReference>> SyncAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LocationSummary>>> ListLocationsAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(CancellationToken cancellationToken = default);
}

public interface IKeywordDiscoveryService
{
    Task<Result<KeywordDiscoveryResult>> DiscoverAsync(ProjectExecutionContext context, KeywordDiscoveryRequest request, CancellationToken cancellationToken = default);
}

public interface ISearchVolumeService
{
    Task<Result<JobReference>> RegisterAsync(ProjectExecutionContext context, SearchVolumeJobRequest request, CancellationToken cancellationToken = default);

    Task<Result<JobReference>> GetJobAsync(ProjectExecutionContext context, Guid jobId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SearchVolumeResultRow>>> GetResultsAsync(ProjectExecutionContext context, Guid jobId, SearchQuery query, CancellationToken cancellationToken = default);
}

public interface IScoringService
{
    Task<Result<OpportunityScoreResult>> CalculateOpportunityScoresAsync(ProjectExecutionContext context, OpportunityScoreRequest request, CancellationToken cancellationToken = default);
}

public interface IDataTransferService
{
    Task<Result<DataExportReference>> CreateCsvExportAsync(ProjectExecutionContext context, DataExportRequest request, CancellationToken cancellationToken = default);

    Task<Result<DataExportReference>> GetExportAsync(ProjectExecutionContext context, Guid exportId, CancellationToken cancellationToken = default);
}

public interface IExternalApiUsageService
{
    Task<Result<ExternalApiUsageSummary>> GetUsageSummaryAsync(ProjectExecutionContext context, ExternalApiUsageQuery query, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<Result<NotificationResult>> SendTestAsync(ProjectExecutionContext context, Guid channelId, CancellationToken cancellationToken = default);

    Task<Result<NotificationResult>> EnqueueAsync(ProjectExecutionContext context, NotificationRequest request, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<Result<DashboardSnapshot>> GetDashboardAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);
}

public sealed record WorkspaceSummary(Guid WorkspaceId, string Name, LifecycleStatus Status);

public sealed record ProjectSummary(Guid ProjectId, string Name, string DefaultLocation, string DefaultLanguage, LifecycleStatus Status);

public sealed record SiteSummary(Guid SiteId, string Domain, string? CanonicalUrl, string Type, LifecycleStatus Status);

public sealed record LocationSummary(string Provider, string Code, string Name, string? CountryCode, LifecycleStatus Status);

public sealed record LanguageSummary(string Provider, string Code, string Name, LifecycleStatus Status);

public sealed record KeywordDiscoveryRequest(
    IReadOnlyList<string> Seeds,
    IReadOnlyList<string> Engines,
    string Location,
    string Language);

public sealed record KeywordDiscoveryResult(IReadOnlyList<KeywordCandidate> Candidates);

public sealed record KeywordCandidate(string Keyword, string Source, string? SuggestClass, decimal? OpportunityScore);

public sealed record SearchVolumeJobRequest(
    IReadOnlyList<string> Keywords,
    string Location,
    string Language,
    int AggregationPeriodMonths = 12);

public sealed record SearchVolumeResultRow(
    string Keyword,
    int? SearchVolume,
    decimal? SeoDifficulty,
    decimal? Cpc,
    decimal? Competition);

public sealed record OpportunityScoreRequest(IReadOnlyList<Guid> KeywordIds, string Location, string Language);

public sealed record OpportunityScoreResult(IReadOnlyList<OpportunityScoreRow> Scores);

public sealed record OpportunityScoreRow(Guid KeywordId, decimal Score, IReadOnlyDictionary<string, decimal> Components);

public sealed record DataExportRequest(string ExportType, SearchQuery Query);

public sealed record DataExportReference(Guid ExportId, JobStatus Status, string? FileUri);

public sealed record ExternalApiUsageQuery(DateOnly? From, DateOnly? To, string? Provider);

public sealed record ExternalApiUsageSummary(int CallCount, int ConsumedCredit, int RetryableFailureCount, int FatalFailureCount);

public sealed record NotificationRequest(string EventType, string ResourceType, Guid? ResourceId, string Message);

public sealed record NotificationResult(Guid? DeliveryId, NotificationDeliveryStatus Status);

public sealed record DashboardSnapshot(
    int KeywordCandidateCount,
    int RunningJobCount,
    int FailedJobCount,
    int ConsumedCredit);

public sealed record JobReference(Guid JobId, JobStatus Status);
