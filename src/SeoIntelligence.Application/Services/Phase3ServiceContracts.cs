using System.Text.Json;
using SeoIntelligence.Application.Common;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Application.Services;

public interface IRewriteManagementService
{
    Task<Result<PagedResult<RewriteTaskDetails>>> SearchRewriteTasksAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<RewriteTaskDetails>> GetRewriteTaskAsync(ProjectExecutionContext context, Guid taskId, CancellationToken cancellationToken = default);

    Task<Result<RewriteTaskDetails>> UpdateRewriteTaskAsync(ProjectExecutionContext context, Guid taskId, RewriteTaskUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<CannibalizationCandidateDetails>>> SearchCannibalizationCandidatesAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<JobReference>> RefreshCannibalizationAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<Result<JobReference>> CreateReportAsync(ProjectExecutionContext context, ReportRequest request, CancellationToken cancellationToken = default);

    Task<Result<ReportDetails>> GetReportAsync(ProjectExecutionContext context, Guid reportId, CancellationToken cancellationToken = default);

    Task<Result<ReportDownload>> CreateDownloadUrlAsync(ProjectExecutionContext context, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored report file for streaming to the caller. The caller owns the returned
    /// stream and must dispose it.
    /// </summary>
    Task<Result<ArtifactContent>> OpenReportContentAsync(ProjectExecutionContext context, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored report file for a share-link recipient. The token is revalidated here, so
    /// a revoked or expired share cannot still hand out the file. The caller owns the stream.
    /// </summary>
    Task<Result<ArtifactContent>> OpenSharedReportContentAsync(ProjectExecutionContext context, string token, CancellationToken cancellationToken = default);

    Task<Result<ReportShareDetails>> ShareReportAsync(ProjectExecutionContext context, Guid reportId, ReportShareRequest request, CancellationToken cancellationToken = default);

    Task<Result<ReportShareDetails>> RevokeShareAsync(ProjectExecutionContext context, Guid reportId, CancellationToken cancellationToken = default);

    Task<Result<ReportShareAccessDetails>> GetSharedReportAsync(ProjectExecutionContext context, string token, CancellationToken cancellationToken = default);
}

public interface IDataImportService
{
    Task<Result<ImportUploadUrlDetails>> CreateUploadUrlAsync(ProjectExecutionContext context, ImportUploadUrlRequest request, CancellationToken cancellationToken = default);

    Task<Result<JobReference>> RegisterImportAsync(ProjectExecutionContext context, ImportRequest request, CancellationToken cancellationToken = default);

    Task<Result<DataImportDetails>> GetImportAsync(ProjectExecutionContext context, Guid importId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DataImportErrorDetails>>> GetImportErrorsAsync(ProjectExecutionContext context, Guid importId, SearchQuery query, CancellationToken cancellationToken = default);
}

public interface IExternalConnectorService
{
    Task<Result<PagedResult<ConnectorSettingsDetails>>> SearchConnectorsAsync(ProjectExecutionContext context, SearchQuery query, CancellationToken cancellationToken = default);

    Task<Result<ConnectorSettingsDetails>> CreateConnectorAsync(ProjectExecutionContext context, ConnectorSettingsRequest request, CancellationToken cancellationToken = default);

    Task<Result<ConnectorSettingsDetails>> UpdateConnectorAsync(ProjectExecutionContext context, Guid connectorId, ConnectorSettingsRequest request, CancellationToken cancellationToken = default);

    Task<Result<ConnectorSettingsDetails>> DisableConnectorAsync(ProjectExecutionContext context, Guid connectorId, CancellationToken cancellationToken = default);

    Task<Result<ConnectorRunDetails>> TestConnectorAsync(ProjectExecutionContext context, Guid connectorId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ConnectorRunDetails>>> GetConnectorRunsAsync(ProjectExecutionContext context, Guid connectorId, SearchQuery query, CancellationToken cancellationToken = default);
}

public interface IAiAssistantService
{
    Task<Result<AiChatResponse>> ChatAsync(ProjectExecutionContext context, AiChatRequest request, CancellationToken cancellationToken = default);
}

public sealed record RewriteTaskUpdateRequest(
    string? Status = null,
    decimal? PriorityScore = null,
    string? AssigneeActor = null,
    string? Memo = null);

public sealed record RewriteTaskDetails(
    Guid TaskId,
    Guid ProjectId,
    string TargetUrl,
    decimal PriorityScore,
    JsonElement Reason,
    string Status,
    string AssigneeActor,
    string? Memo,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CannibalizationCandidateDetails(
    Guid CandidateId,
    Guid ProjectId,
    Guid KeywordId,
    string Keyword,
    string PrimaryUrl,
    JsonElement CompetingUrls,
    decimal SeverityScore,
    JsonElement Evidence,
    JsonElement Recommendation,
    string Status,
    DateTime DetectedAt);

public sealed record ReportRequest(
    string? ReportType,
    string? Period,
    string? Format,
    IReadOnlyList<string>? Sections = null,
    DateTimeOffset? ShareExpiresAt = null);

public sealed record ReportDetails(
    Guid ReportId,
    Guid ProjectId,
    string ReportType,
    string Period,
    string Format,
    int CurrentVersion,
    string? FileUri,
    DateTimeOffset? ShareExpiresAt,
    DateTimeOffset? ShareRevokedAt,
    string Status,
    string GeneratedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Where a finished report can be fetched from. No expiry, for the reason given on
/// <see cref="DataExportDownload"/>: the URL is an authenticated API path, not a pre-signed one.
/// </summary>
public sealed record ReportDownload(
    Guid ReportId,
    string DownloadUrl);

public sealed record ReportShareRequest(DateTimeOffset ShareExpiresAt);

public sealed record ReportShareDetails(
    Guid ReportId,
    string? ShareUrl,
    DateTimeOffset? ShareExpiresAt,
    DateTimeOffset? ShareRevokedAt,
    string Status);

/// <summary>
/// What a share-link recipient is told about the report.
/// <para>
/// <see cref="DownloadExpiresAt"/> is the share's own expiry, which is enforced: every access
/// revalidates the token against it and against the revocation time. It used to be an unrelated
/// 15-minute value that nothing checked, so a recipient was shown a deadline that did not apply.
/// </para>
/// </summary>
public sealed record ReportShareAccessDetails(
    Guid ReportId,
    string ReportType,
    string Period,
    string Format,
    string DownloadUrl,
    DateTimeOffset? DownloadExpiresAt);

public sealed record ImportUploadUrlRequest(
    string? ImportType,
    string? Format,
    string? FileName);

public sealed record ImportUploadUrlDetails(
    string UploadUrl,
    string SourceFileUri,
    DateTimeOffset ExpiresAt);

public sealed record ImportRequest(
    string? ImportType,
    string? Format,
    string? SourceFileUri,
    string? ValidationMode = null);

public sealed record DataImportDetails(
    Guid ImportId,
    Guid WorkspaceId,
    Guid? ProjectId,
    string ImportType,
    string Format,
    string SourceFileUri,
    string Status,
    JsonElement ValidationErrors,
    string RequestedBy,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record DataImportErrorDetails(
    string Target,
    string Message,
    JsonElement? Evidence = null);

public sealed record ConnectorSettingsRequest(
    string? ConnectorType,
    string? Name,
    string? AuthRef,
    JsonElement? Settings = null,
    string? Status = null);

public sealed record ConnectorSettingsDetails(
    Guid ConnectorId,
    Guid WorkspaceId,
    Guid? ProjectId,
    string ConnectorType,
    string Name,
    string? AuthRef,
    JsonElement Settings,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DisabledAt);

public sealed record ConnectorRunDetails(
    Guid RunId,
    Guid ConnectorId,
    Guid WorkspaceId,
    Guid? ProjectId,
    string RunType,
    string Status,
    JsonElement Request,
    JsonElement ResultSummary,
    JsonElement? Error,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt);

public sealed record AiChatRequest(
    string? Message,
    Guid? ConversationId = null,
    IReadOnlyList<string>? AllowedTools = null,
    JsonElement? ReferenceScope = null);

public sealed record AiChatResponse(
    Guid SessionId,
    Guid MessageId,
    Guid JobId,
    string Response,
    IReadOnlyList<JsonElement> ToolCalls,
    JsonElement ReferenceData,
    JsonElement TokenUsage,
    string RedactionStatus,
    string ReviewStatus);

public sealed record DashboardRewriteSummary(
    int TaskCount,
    int ActiveTaskCount,
    decimal MaxPriorityScore);

public sealed record DashboardCannibalizationSummary(
    int CandidateCount,
    int ActiveCandidateCount,
    decimal MaxSeverityScore);

public sealed record DashboardReportSummary(
    int ReportCount,
    int SharedReportCount,
    int ExpiredShareCount);

public sealed record DashboardAiSummary(
    int SessionCount,
    int MessageCount,
    int PendingReviewCount);
