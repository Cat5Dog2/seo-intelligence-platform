using System.Text.Json;
using SeoIntelligence.Application.Common;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Application.Jobs;

public interface IJobService
{
    Task<Result<JobDetails>> RegisterAsync(
        ProjectExecutionContext context,
        JobRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<JobDetails>>> SearchAsync(
        ProjectExecutionContext context,
        JobSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<JobDetails>> GetAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<Result<JobDetails>> CancelAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<Result<JobDetails>> RetryAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<Result<IJobExecutionLease>> TryStartAsync(
        ProjectExecutionContext context,
        Guid jobId,
        JobExecutionStartRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobDetails>> RecordFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
        JobFailure failure,
        CancellationToken cancellationToken = default);

    Task<Result<JobDetails>> CompleteAsync(
        ProjectExecutionContext context,
        Guid jobId,
        JobCompletion completion,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> CanIngestExternalResultAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default);
}

public interface IJobExecutionLease : IAsyncDisposable
{
    Guid JobId { get; }

    string? LockKey { get; }

    string? LockOwner { get; }
}

public sealed record JobRegistrationRequest(
    string? JobType,
    JsonElement? RequestPayload = null,
    string? RequestHash = null,
    string? IdempotencyKey = null,
    string? TargetKey = null,
    string? Queue = null);

public sealed record JobSearchQuery(
    SearchQuery Search,
    string? JobType = null,
    Guid? ProjectId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record JobExecutionStartRequest(
    string? TargetKey = null,
    TimeSpan? LockTtl = null);

public sealed record JobFailure(
    JobFailureKind Kind,
    int? HttpStatusCode,
    string? ErrorCode,
    string Message)
{
    public static JobFailure FromHttpStatusCode(int statusCode, string? errorCode = null, string? message = null)
        => new(JobFailureKind.HttpStatusCode, statusCode, errorCode, message ?? $"External API returned HTTP {statusCode}.");

    public static JobFailure Timeout(string? message = null)
        => new(JobFailureKind.Timeout, null, "timeout", message ?? "The job timed out while waiting for an external dependency.");

    public static JobFailure DatabaseTransient(string? message = null)
        => new(JobFailureKind.DatabaseTransient, null, "database_transient", message ?? "A transient database failure occurred.");
}

public sealed record JobCompletion(
    int Progress = 100,
    JobResultResource? ResultResource = null);

public enum JobFailureKind
{
    HttpStatusCode,
    Timeout,
    DatabaseTransient,
    Unexpected
}

public sealed record JobResultResource(
    string ResourceType,
    Guid ResourceId);

public sealed record JobDetails(
    Guid JobId,
    Guid WorkspaceId,
    Guid? ProjectId,
    string JobType,
    string Status,
    int Progress,
    string StatusUrl,
    string? ExternalRequestId,
    JobResultResource? ResultResource,
    int RetryCount,
    DateTime? NextRunAt,
    JsonElement? Error,
    string RequestedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt);
