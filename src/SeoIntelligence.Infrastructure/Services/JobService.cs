using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

public interface IJobDispatcher
{
    Task DispatchAsync(Guid jobId);
}

internal sealed class JobService(
    SeoIntelligenceDbContext dbContext,
    TimeProvider timeProvider,
    IAuditLogWriter auditLogWriter,
    IOptions<HangfireOptions> hangfireOptions,
    IJobQueueClient jobQueueClient,
    IEnumerable<IRedisCoordinator> redisCoordinators)
    : IJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ActiveExternalRequestStatuses =
    [
        StatusValues.Queued,
        StatusValues.Running,
        StatusValues.WaitingExternal,
        "pending",
        "retrying"
    ];

    private readonly IRedisCoordinator? redisCoordinator = redisCoordinators.SingleOrDefault();

    public async Task<Result<JobDetails>> RegisterAsync(
        ProjectExecutionContext context,
        JobRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var jobType = RequireText(request.JobType, nameof(request.JobType), errors);
        var idempotencyKey = OptionalText(request.IdempotencyKey);
        var targetKey = OptionalText(request.TargetKey);
        var queue = NormalizeQueue(request.Queue, jobType, errors);
        var requestHash = OptionalText(request.RequestHash)
            ?? ComputeRequestHash(jobType, request.RequestPayload, targetKey);

        if (idempotencyKey is not null && requestHash is null)
        {
            errors.Add(nameof(request.RequestHash), "requestHash is required when Idempotency-Key is used.");
        }

        if (errors.HasErrors)
        {
            return ValidationFailure<JobDetails>(errors);
        }

        if (context.ProjectId.HasValue)
        {
            var projectExists = await dbContext.Projects
                .AsNoTracking()
                .AnyAsync(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == context.ProjectId.Value, cancellationToken);
            if (!projectExists)
            {
                return Failure<JobDetails>(ErrorCode.NotFound, "Project was not found.");
            }
        }

        if (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(
                context.WorkspaceId,
                context.ProjectId,
                jobType!,
                idempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return Failure<JobDetails>(
                        ErrorCode.Conflict,
                        "Idempotency-Key was already used for a different request hash.");
                }

                return Result<JobDetails>.Success(MapJob(existing));
            }
        }

        var now = NowUtc();
        var job = new JobEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            JobType = jobType!,
            Status = StatusValues.Queued,
            Progress = 0,
            RetryCount = 0,
            NextRunAt = now,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Jobs.Add(job);
        AddJobAudit(context, AuditLogActionNames.JobQueued, job, before: null);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(
                context.WorkspaceId,
                context.ProjectId,
                jobType!,
                idempotencyKey,
                cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
                ? Result<JobDetails>.Success(MapJob(existing))
                : Failure<JobDetails>(
                    ErrorCode.Conflict,
                    "Idempotency-Key was already used for a different request hash.");
        }

        await jobQueueClient.EnqueueAsync(job.Id, queue!, cancellationToken);
        return Result<JobDetails>.Success(MapJob(job));
    }

    public async Task<Result<PagedResult<JobDetails>>> SearchAsync(
        ProjectExecutionContext context,
        JobSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.Jobs
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId);

        if (context.ProjectId.HasValue)
        {
            source = source.Where(entity => entity.ProjectId == context.ProjectId.Value);
        }

        if (query.ProjectId.HasValue)
        {
            source = source.Where(entity => entity.ProjectId == query.ProjectId.Value);
        }

        var status = OptionalText(query.Search.Status);
        if (status is not null && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            source = source.Where(entity => entity.Status == status.ToLowerInvariant());
        }

        var jobType = OptionalText(query.JobType);
        if (jobType is not null)
        {
            source = source.Where(entity => entity.JobType == jobType);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value.UtcDateTime;
            source = source.Where(entity => entity.CreatedAt >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value.UtcDateTime;
            source = source.Where(entity => entity.CreatedAt <= to);
        }

        var q = NormalizeSearchText(query.Search.Q);
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.JobType.ToLower().Contains(q) ||
                (entity.IdempotencyKey != null && entity.IdempotencyKey.ToLower().Contains(q)) ||
                (entity.RequestHash != null && entity.RequestHash.ToLower().Contains(q)));
        }

        source = SortJobs(source, query.Search.Sort);
        return Result<PagedResult<JobDetails>>.Success(
            await ToPagedResultAsync(source, query.Search, MapJob, cancellationToken));
    }

    public async Task<Result<JobDetails>> GetAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: false, cancellationToken);
        return job is null
            ? Failure<JobDetails>(ErrorCode.NotFound, "Job was not found.")
            : Result<JobDetails>.Success(await MapJobWithExternalRequestAsync(job, cancellationToken));
    }

    public async Task<Result<JobDetails>> CancelAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobDetails>(ErrorCode.NotFound, "Job was not found.");
        }

        var currentStatus = StatusExtensions.ToJobStatus(job.Status);
        if (!JobStatusTransitions.CanCancel(currentStatus))
        {
            return Failure<JobDetails>(ErrorCode.Conflict, "Only queued or waiting_external jobs can be canceled.");
        }

        var before = ToJobAuditSnapshot(job);
        var now = NowUtc();
        job.Status = StatusValues.Canceled;
        job.UpdatedAt = now;
        job.CompletedAt = now;

        if (currentStatus == JobStatus.WaitingExternal)
        {
            var externalRequests = await dbContext.JobExternalRequests
                .Where(entity => entity.JobId == job.Id && ActiveExternalRequestStatuses.Contains(entity.Status))
                .ToListAsync(cancellationToken);

            foreach (var externalRequest in externalRequests)
            {
                externalRequest.Status = StatusValues.Canceled;
                externalRequest.UpdatedAt = now;
                externalRequest.CompletedAt = now;
            }
        }

        AddJobAudit(context, AuditLogActionNames.JobCanceled, job, before);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<JobDetails>.Success(await MapJobWithExternalRequestAsync(job, cancellationToken));
    }

    public async Task<Result<JobDetails>> RetryAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobDetails>(ErrorCode.NotFound, "Job was not found.");
        }

        if (!string.Equals(job.Status, StatusValues.FailedRetryable, StringComparison.Ordinal))
        {
            return Failure<JobDetails>(ErrorCode.Conflict, "Only failed_retryable jobs can be retried.");
        }

        var before = ToJobAuditSnapshot(job);
        var now = NowUtc();
        job.Status = StatusValues.Queued;
        job.RetryCount += 1;
        job.NextRunAt = now;
        job.ErrorJson = null;
        job.UpdatedAt = now;
        job.CompletedAt = null;

        AddJobAudit(context, AuditLogActionNames.JobRetried, job, before);
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobQueueClient.EnqueueAsync(job.Id, ResolveQueue(job.JobType), cancellationToken);
        return Result<JobDetails>.Success(MapJob(job));
    }

    public async Task<Result<IJobExecutionLease>> TryStartAsync(
        ProjectExecutionContext context,
        Guid jobId,
        JobExecutionStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<IJobExecutionLease>(ErrorCode.NotFound, "Job was not found.");
        }

        if (!string.Equals(job.Status, StatusValues.Queued, StringComparison.Ordinal))
        {
            return Failure<IJobExecutionLease>(ErrorCode.Conflict, "Only queued jobs can start execution.");
        }

        IRedisLease? redisLease = null;
        var lockKey = BuildExecutionLockKey(job, request.TargetKey);
        var lockOwner = $"job:{job.Id:N}";

        if (redisCoordinator is not null)
        {
            redisLease = await redisCoordinator.TryAcquireLockAsync(
                new RedisKey(lockKey),
                lockOwner,
                request.LockTtl ?? TimeSpan.FromMinutes(30),
                cancellationToken);

            if (redisLease is null)
            {
                return Failure<IJobExecutionLease>(
                    ErrorCode.Conflict,
                    "Another job with the same project, job type, and target is already running.");
            }
        }

        try
        {
            var before = ToJobAuditSnapshot(job);
            var now = NowUtc();
            job.Status = StatusValues.Running;
            job.NextRunAt = null;
            job.UpdatedAt = now;
            AddJobAudit(context, AuditLogActionNames.JobStarted, job, before);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<IJobExecutionLease>.Success(new JobExecutionLease(job.Id, lockKey, lockOwner, redisLease));
        }
        catch
        {
            if (redisLease is not null)
            {
                await redisLease.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<Result<JobDetails>> RecordFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
        JobFailure failure,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobDetails>(ErrorCode.NotFound, "Job was not found.");
        }

        var currentStatus = StatusExtensions.ToJobStatus(job.Status);
        var nextStatus = ClassifyFailure(failure);
        if (!JobStatusTransitions.CanTransition(currentStatus, nextStatus))
        {
            return Failure<JobDetails>(ErrorCode.Conflict, "Job status does not allow recording this failure.");
        }

        var before = ToJobAuditSnapshot(job);
        var now = NowUtc();
        job.Status = nextStatus.ToStorageValue();
        job.ErrorJson = SerializeFailure(failure, nextStatus);
        job.UpdatedAt = now;

        if (nextStatus == JobStatus.FailedRetryable)
        {
            job.RetryCount += 1;
            job.NextRunAt = now + CalculateBackoff(job.RetryCount, failure);
            job.CompletedAt = null;
        }
        else
        {
            job.NextRunAt = null;
            job.CompletedAt = now;
        }

        AddJobAudit(context, AuditLogActionNames.JobFailed, job, before);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<JobDetails>.Success(MapJob(job));
    }

    public async Task<Result<bool>> CanIngestExternalResultAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindJobAsync(context, jobId, asTracking: false, cancellationToken);
        if (job is null)
        {
            return Failure<bool>(ErrorCode.NotFound, "Job was not found.");
        }

        return Result<bool>.Success(!string.Equals(job.Status, StatusValues.Canceled, StringComparison.Ordinal));
    }

    private async Task<JobEntity?> FindByIdempotencyKeyAsync(
        Guid workspaceId,
        Guid? projectId,
        string jobType,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == workspaceId &&
                    entity.ProjectId == projectId &&
                    entity.JobType == jobType &&
                    entity.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private async Task<JobEntity?> FindJobAsync(
        ProjectExecutionContext context,
        Guid jobId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var source = asTracking
            ? dbContext.Jobs
            : dbContext.Jobs.AsNoTracking();

        source = source.Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == jobId);
        if (context.ProjectId.HasValue)
        {
            source = source.Where(entity => entity.ProjectId == context.ProjectId.Value);
        }

        return await source.FirstOrDefaultAsync(cancellationToken);
    }

    private string? NormalizeQueue(string? requestedQueue, string? jobType, ValidationErrors errors)
    {
        var queue = OptionalText(requestedQueue) ?? (jobType is null ? null : ResolveQueue(jobType));
        if (queue is null)
        {
            return null;
        }

        if (!hangfireOptions.Value.Queues.Contains(queue, StringComparer.Ordinal))
        {
            errors.Add(nameof(requestedQueue), "queue is not configured for Hangfire.");
        }

        return queue;
    }

    private static JobStatus ClassifyFailure(JobFailure failure)
        => failure.Kind switch
        {
            JobFailureKind.HttpStatusCode when failure.HttpStatusCode.HasValue
                => JobFailureClassifier.FromHttpStatusCode(failure.HttpStatusCode.Value),
            JobFailureKind.Timeout or JobFailureKind.DatabaseTransient
                => JobStatus.FailedRetryable,
            _ => JobStatus.FailedFatal
        };

    private static TimeSpan CalculateBackoff(int retryCount, JobFailure failure)
    {
        var cappedRetry = Math.Clamp(retryCount, 1, 6);
        var baseDelaySeconds = failure.Kind == JobFailureKind.HttpStatusCode && failure.HttpStatusCode == 503 ? 60 : 30;
        var delaySeconds = baseDelaySeconds * Math.Pow(2, cappedRetry - 1);
        var jitterSeconds = Math.Abs(HashCode.Combine(failure.ErrorCode, failure.HttpStatusCode, retryCount)) % 10;
        return TimeSpan.FromSeconds(delaySeconds + jitterSeconds);
    }

    private static string SerializeFailure(JobFailure failure, JobStatus status)
        => JsonSerializer.Serialize(new
        {
            kind = failure.Kind.ToString(),
            httpStatusCode = failure.HttpStatusCode,
            errorCode = failure.ErrorCode,
            message = failure.Message,
            status = status.ToStorageValue(),
            retryable = status == JobStatus.FailedRetryable
        }, JsonOptions);

    private static string ResolveQueue(string jobType)
    {
        var normalized = jobType.Trim().ToLowerInvariant();
        if (normalized.Contains("notification", StringComparison.Ordinal))
        {
            return "notifications";
        }

        if (normalized.Contains("export", StringComparison.Ordinal) || normalized.Contains("report", StringComparison.Ordinal))
        {
            return "exports";
        }

        if (normalized.Contains("poll", StringComparison.Ordinal))
        {
            return "polling";
        }

        if (normalized.Contains("analysis", StringComparison.Ordinal) ||
            normalized.Contains("score", StringComparison.Ordinal) ||
            normalized.Contains("cluster", StringComparison.Ordinal))
        {
            return "analysis";
        }

        if (normalized.Contains("search", StringComparison.Ordinal) ||
            normalized.Contains("rank", StringComparison.Ordinal) ||
            normalized.Contains("external", StringComparison.Ordinal))
        {
            return "external-api";
        }

        return "default";
    }

    private static string BuildExecutionLockKey(JobEntity job, string? targetKey)
    {
        var projectSegment = job.ProjectId?.ToString("N") ?? "global";
        var target = OptionalText(targetKey) ?? job.RequestHash ?? job.Id.ToString("N");
        var targetHash = HashText(target);
        return $"seo-intelligence:job-lock:{job.WorkspaceId:N}:{projectSegment}:{job.JobType}:{targetHash}";
    }

    private static string? ComputeRequestHash(string? jobType, JsonElement? payload, string? targetKey)
    {
        if (jobType is null)
        {
            return null;
        }

        var rawPayload = payload.HasValue && payload.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? payload.Value.GetRawText()
            : "{}";
        return HashText($"{jobType.Trim()}\n{OptionalText(targetKey) ?? string.Empty}\n{rawPayload}");
    }

    private async Task<JobDetails> MapJobWithExternalRequestAsync(JobEntity entity, CancellationToken cancellationToken)
    {
        var externalRequestId = await dbContext.JobExternalRequests
            .AsNoTracking()
            .Where(request => request.JobId == entity.Id)
            .OrderBy(request => request.SequenceNo)
            .Select(request => request.ExternalRequestId)
            .FirstOrDefaultAsync(cancellationToken);

        return MapJob(entity, externalRequestId);
    }

    private static JobDetails MapJob(JobEntity entity)
        => MapJob(entity, externalRequestId: null);

    private static JobDetails MapJob(JobEntity entity, string? externalRequestId)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.JobType,
            entity.Status,
            entity.Progress,
            $"/api/jobs/{entity.Id:D}",
            externalRequestId,
            entity.ResultResourceType is null || !entity.ResultResourceId.HasValue
                ? null
                : new JobResultResource(entity.ResultResourceType, entity.ResultResourceId.Value),
            entity.RetryCount,
            entity.NextRunAt,
            ParseJsonOrNull(entity.ErrorJson),
            entity.RequestedBy,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CompletedAt);

    private static JsonElement? ParseJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IQueryable<JobEntity> SortJobs(IQueryable<JobEntity> source, SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "createdAt";
        var direction = sort?.Direction ?? SortDirection.Desc;

        return sortBy switch
        {
            "jobType" => Order(source, direction, entity => entity.JobType),
            "status" => Order(source, direction, entity => entity.Status),
            "progress" => Order(source, direction, entity => entity.Progress),
            "retryCount" => Order(source, direction, entity => entity.RetryCount),
            "nextRunAt" => Order(source, direction, entity => entity.NextRunAt),
            "updatedAt" => Order(source, direction, entity => entity.UpdatedAt),
            "completedAt" => Order(source, direction, entity => entity.CompletedAt),
            _ => Order(source, direction, entity => entity.CreatedAt)
        };
    }

    private static IQueryable<T> Order<T, TKey>(
        IQueryable<T> source,
        SortDirection direction,
        System.Linq.Expressions.Expression<Func<T, TKey>> keySelector)
        => direction == SortDirection.Asc
            ? source.OrderBy(keySelector)
            : source.OrderByDescending(keySelector);

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        IQueryable<TEntity> source,
        SearchQuery query,
        Func<TEntity, TResponse> map,
        CancellationToken cancellationToken)
    {
        var page = query.EffectivePage;
        var totalCount = await source.LongCountAsync(cancellationToken);
        var entities = await source
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResponse>(
            entities.Select(map).ToArray(),
            page.Page,
            page.PageSize,
            totalCount);
    }

    private void AddJobAudit(
        ProjectExecutionContext context,
        string action,
        JobEntity job,
        object? before)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.Job,
                job.Id.ToString("D"),
                new
                {
                    before,
                    after = ToJobAuditSnapshot(job)
                }));

    private static object ToJobAuditSnapshot(JobEntity entity)
        => new
        {
            jobType = entity.JobType,
            status = entity.Status,
            progress = entity.Progress,
            retryCount = entity.RetryCount,
            nextRunAt = entity.NextRunAt,
            projectId = entity.ProjectId,
            requestHash = entity.RequestHash,
            idempotencyKey = entity.IdempotencyKey,
            completedAt = entity.CompletedAt
        };

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string? RequireText(string? value, string target, ValidationErrors errors, int maxLength = 500)
    {
        var trimmed = OptionalText(value);
        if (trimmed is null)
        {
            errors.Add(target, $"{ToCamelCase(target)} is required.");
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors.Add(target, $"{ToCamelCase(target)} must be {maxLength} characters or fewer.");
            return null;
        }

        return trimmed;
    }

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSearchText(string? value)
        => OptionalText(value)?.ToLowerInvariant();

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ToCamelCase(string value)
    {
        var sanitized = value.Trim();
        return string.IsNullOrEmpty(sanitized)
            ? sanitized
            : char.ToLowerInvariant(sanitized[0]) + sanitized[1..];
    }

    private sealed class JobExecutionLease(
        Guid jobId,
        string? lockKey,
        string? lockOwner,
        IRedisLease? redisLease)
        : IJobExecutionLease
    {
        public Guid JobId { get; } = jobId;

        public string? LockKey { get; } = lockKey;

        public string? LockOwner { get; } = lockOwner;

        public async ValueTask DisposeAsync()
        {
            if (redisLease is not null)
            {
                await redisLease.DisposeAsync();
            }
        }
    }

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => errors.Count > 0;

        public void Add(string target, string message)
        {
            var camelTarget = ToCamelCase(target);
            if (!errors.TryGetValue(camelTarget, out var messages))
            {
                messages = [];
                errors[camelTarget] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }
}

internal interface IJobQueueClient
{
    Task EnqueueAsync(Guid jobId, string queue, CancellationToken cancellationToken = default);
}

internal sealed class HangfireJobQueueClient(
    IServiceProvider serviceProvider,
    ILogger<HangfireJobQueueClient> logger)
    : IJobQueueClient
{
    public Task EnqueueAsync(Guid jobId, string queue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backgroundJobClient = serviceProvider.GetService<IBackgroundJobClient>();
        if (backgroundJobClient is null)
        {
            logger.LogDebug("Hangfire is not configured; job {job_id} remains queued in the jobs table.", jobId);
            return Task.CompletedTask;
        }

        backgroundJobClient.Create(
            Job.FromExpression<IJobDispatcher>(dispatcher => dispatcher.DispatchAsync(jobId)),
            new EnqueuedState(queue));
        return Task.CompletedTask;
    }
}

internal sealed class JobDispatcher(
    ILogger<JobDispatcher> logger)
    : IJobDispatcher
{
    public Task DispatchAsync(Guid jobId)
    {
        logger.LogInformation("Job {job_id} was dequeued. Concrete job handlers are registered by feature-specific issues.", jobId);
        return Task.CompletedTask;
    }
}
