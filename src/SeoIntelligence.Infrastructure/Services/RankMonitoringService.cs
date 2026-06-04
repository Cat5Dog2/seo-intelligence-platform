using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class RankMonitoringService(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    INotificationService notificationService,
    IAuditLogWriter auditLogWriter,
    IJobQueueClient jobQueueClient,
    IRankCheckJobScheduler jobScheduler,
    TimeProvider timeProvider)
    : IRankMonitoringService
{
    public const string RegisterJobType = "RegisterRankCheckJob";
    public const string ResultResourceType = "rank_check_job";
    public const string AlertEventResourceType = "alert_event";
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private const int MaxTargetCount = 50;
    private const int ExternalResultLimit = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<int> AllowedDepths = [30, 40, 50, 60, 70, 80, 90, 100];
    private static readonly HashSet<string> AllowedMatchTypes = ["domain", "url", "exact", "prefix"];
    private static readonly HashSet<string> AllowedTargetTypes = ["domain", "url"];
    private static readonly HashSet<string> AllowedAlertTypes =
    [
        "rank_drop",
        "out_of_range",
        "competitor_passed",
        "top_entry",
        "four_to_ten"
    ];

    public async Task<Result<JobReference>> RegisterRankCheckAsync(
        ProjectExecutionContext context,
        RankCheckJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeRequestAsync(context, request, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<JobReference>.Failure(normalized.Error);
        }

        var rankRequest = normalized.Request!;
        var existing = await dbContext.Jobs
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId &&
                entity.JobType == RegisterJobType &&
                entity.IdempotencyKey == rankRequest.IdempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, rankRequest.RequestHash, StringComparison.Ordinal))
            {
                return Failure<JobReference>(
                    ErrorCode.Conflict,
                    "Idempotency-Key was already used for a different request hash.");
            }

            return Result<JobReference>.Success(new JobReference(existing.Id, existing.Status));
        }

        var now = NowUtc();
        var job = new JobEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            JobType = RegisterJobType,
            Status = StatusValues.Queued,
            Progress = 0,
            RetryCount = 0,
            NextRunAt = now,
            ResultResourceType = ResultResourceType,
            IdempotencyKey = rankRequest.IdempotencyKey,
            RequestHash = rankRequest.RequestHash,
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };
        job.ResultResourceId = job.Id;

        dbContext.Jobs.Add(job);
        dbContext.RankCheckJobs.Add(new RankCheckJobEntity
        {
            JobId = job.Id,
            Depth = rankRequest.Depth,
            MatchType = rankRequest.MatchType,
            WithMetrics = rankRequest.WithMetrics,
            RequestOptionsJson = JsonSerializer.Serialize(rankRequest.ToOptions(), JsonOptions),
            StatusJson = JsonSerializer.Serialize(
                new RankCheckStatusSnapshot(
                    StatusValues.Queued,
                    ExternalRequestCount: 0,
                    CompletedExternalRequestCount: 0,
                    ExternalStatuses: new Dictionary<string, string>(StringComparer.Ordinal),
                    Message: null),
                JsonOptions)
        });
        dbContext.RankCheckTargets.AddRange(rankRequest.Targets.Select(target => new RankCheckTargetEntity
        {
            Id = UuidV7.New(),
            JobId = job.Id,
            Target = target.Target,
            TargetType = target.TargetType
        }));
        AddJobQueuedAudit(context, job);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var duplicate = await dbContext.Jobs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.WorkspaceId == context.WorkspaceId &&
                        entity.ProjectId == context.ProjectId &&
                        entity.JobType == RegisterJobType &&
                        entity.IdempotencyKey == rankRequest.IdempotencyKey,
                    cancellationToken);

            if (duplicate is null)
            {
                throw;
            }

            return string.Equals(duplicate.RequestHash, rankRequest.RequestHash, StringComparison.Ordinal)
                ? Result<JobReference>.Success(new JobReference(duplicate.Id, duplicate.Status))
                : Failure<JobReference>(
                    ErrorCode.Conflict,
                    "Idempotency-Key was already used for a different request hash.");
        }

        await jobQueueClient.EnqueueAsync(job.Id, "external-api", cancellationToken);
        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<PagedResult<RankResultRow>>> GetJobResultsAsync(
        ProjectExecutionContext context,
        Guid jobId,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var jobExists = await dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == RegisterJobType,
                cancellationToken);
        if (!jobExists)
        {
            return Failure<PagedResult<RankResultRow>>(ErrorCode.NotFound, "Rank check job was not found.");
        }

        var list = await SearchRankResultsAsync(
            context,
            new RankResultSearchQuery(query, JobId: jobId),
            cancellationToken);
        return list.IsSuccess
            ? Result<PagedResult<RankResultRow>>.Success(new PagedResult<RankResultRow>(
                list.Value!.Items,
                list.Value.Page,
                list.Value.PageSize,
                list.Value.TotalCount))
            : Result<PagedResult<RankResultRow>>.Failure(list.Error!);
    }

    public async Task<Result<RankResultList>> SearchRankResultsAsync(
        ProjectExecutionContext context,
        RankResultSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<RankResultList>(ErrorCode.NotFound, "Project was not found.");
        }

        var rankResults = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var keywordIds = rankResults.Select(entity => entity.KeywordId).Distinct().ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);

        var rows = rankResults
            .Select(entity => MapRankResultRow(entity, keywords, rankResults))
            .Where(row => !string.IsNullOrWhiteSpace(row.Keyword));
        rows = FilterRankResults(rows, query);
        rows = SortRankResults(rows, query.Search.Sort).ToArray();

        var distribution = BuildDistribution(rows);
        var page = query.Search.EffectivePage;
        var materialized = rows.ToArray();
        var pagedRows = materialized
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToArray();
        var totalCount = materialized.LongLength;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / page.PageSize);

        return Result<RankResultList>.Success(new RankResultList(
            pagedRows,
            distribution,
            page.Page,
            page.PageSize,
            totalCount,
            totalPages));
    }

    public async Task<Result<PagedResult<RankAlertDetails>>> SearchAlertsAsync(
        ProjectExecutionContext context,
        RankAlertSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<RankAlertDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var entities = await dbContext.Alerts
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var rows = entities.Select(MapAlertDetails);

        var status = OptionalText(query.Search.Status) ?? StatusValues.Active;
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(row => string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var alertType = OptionalText(query.AlertType);
        if (alertType is not null)
        {
            rows = rows.Where(row => string.Equals(row.AlertType, alertType, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row => row.AlertType.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortAlerts(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<RankAlertDetails>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<RankAlertDetails>> CreateAlertAsync(
        ProjectExecutionContext context,
        RankAlertCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<RankAlertDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var normalized = await NormalizeAlertRequestAsync(context, request.AlertType, request.Condition, request.NotificationChannelId, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<RankAlertDetails>.Failure(normalized.Error);
        }

        var now = NowUtc();
        var alert = new AlertEntity
        {
            Id = UuidV7.New(),
            ProjectId = project.Id,
            AlertType = normalized.AlertType!,
            ConditionJson = normalized.ConditionJson!,
            NotificationChannelId = request.NotificationChannelId,
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Alerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RankAlertDetails>.Success(MapAlertDetails(alert));
    }

    public async Task<Result<RankAlertDetails>> UpdateAlertAsync(
        ProjectExecutionContext context,
        Guid alertId,
        RankAlertUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var alert = await FindAlertAsync(context, alertId, asTracking: true, cancellationToken);
        if (alert is null)
        {
            return Failure<RankAlertDetails>(ErrorCode.NotFound, "Rank alert was not found.");
        }

        var alertType = OptionalText(request.AlertType) ?? alert.AlertType;
        var condition = request.Condition.HasValue &&
            request.Condition.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? request.Condition
                : ParseJsonElement(alert.ConditionJson);
        var channelId = request.NotificationChannelId ?? alert.NotificationChannelId;
        var normalized = await NormalizeAlertRequestAsync(context, alertType, condition, channelId, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<RankAlertDetails>.Failure(normalized.Error);
        }

        alert.AlertType = normalized.AlertType!;
        alert.ConditionJson = normalized.ConditionJson!;
        alert.NotificationChannelId = channelId;
        alert.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RankAlertDetails>.Success(MapAlertDetails(alert));
    }

    public async Task<Result<RankAlertDetails>> DisableAlertAsync(
        ProjectExecutionContext context,
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await FindAlertAsync(context, alertId, asTracking: true, cancellationToken);
        if (alert is null)
        {
            return Failure<RankAlertDetails>(ErrorCode.NotFound, "Rank alert was not found.");
        }

        alert.Status = StatusValues.Disabled;
        alert.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RankAlertDetails>.Success(MapAlertDetails(alert));
    }

    public async Task<Result<RankAlertDetails>> EnableAlertAsync(
        ProjectExecutionContext context,
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await FindAlertAsync(context, alertId, asTracking: true, cancellationToken);
        if (alert is null)
        {
            return Failure<RankAlertDetails>(ErrorCode.NotFound, "Rank alert was not found.");
        }

        alert.Status = StatusValues.Active;
        alert.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RankAlertDetails>.Success(MapAlertDetails(alert));
    }

    public async Task<Result<PagedResult<RankAlertEventDetails>>> SearchAlertEventsAsync(
        ProjectExecutionContext context,
        RankAlertEventSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<RankAlertEventDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var source = dbContext.AlertEvents
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id);

        if (query.AlertId.HasValue)
        {
            source = source.Where(entity => entity.AlertId == query.AlertId.Value);
        }

        if (OptionalText(query.EventType) is { } eventType)
        {
            source = source.Where(entity => entity.EventType == eventType);
        }

        if (query.From.HasValue)
        {
            source = source.Where(entity => entity.TriggeredAt >= query.From.Value.UtcDateTime);
        }

        if (query.To.HasValue)
        {
            source = source.Where(entity => entity.TriggeredAt <= query.To.Value.UtcDateTime);
        }

        var entities = await source.ToArrayAsync(cancellationToken);
        var keywordIds = entities
            .Where(entity => entity.KeywordId.HasValue)
            .Select(entity => entity.KeywordId!.Value)
            .Distinct()
            .ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);

        var rows = entities.Select(entity => MapAlertEventDetails(entity, keywords));
        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.EventType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (row.Keyword is not null && row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        rows = SortAlertEvents(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<RankAlertEventDetails>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<JobReference>> RegisterExternalRequestAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindRankCheckJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Rank check job was not found.");
        }

        var request = await ReadRequestOptionsAsync(jobId, cancellationToken);
        if (request is null)
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Rank check job payload was missing.");
        }

        var externalRequestExists = await dbContext.JobExternalRequests
            .AsNoTracking()
            .AnyAsync(entity => entity.JobId == jobId, cancellationToken);
        if (!externalRequestExists)
        {
            var result = await rakkoKeywordClient.RegisterSearchRankAsync(
                CreateClientContext(context, jobId),
                new RakkoSearchRankRegistrationRequest(
                    request.Keywords,
                    request.Targets.Select(target => target.Target).ToArray(),
                    request.MatchType,
                    request.Depth,
                    request.WithMetrics,
                    request.Deduplicate),
                cancellationToken);

            if (!result.IsSuccess || result.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(result, "Rank check external registration failed."));
            }

            var now = NowUtc();
            dbContext.JobExternalRequests.Add(new JobExternalRequestEntity
            {
                Id = UuidV7.New(),
                JobId = jobId,
                Endpoint = "/v1/search-rank",
                ExternalRequestId = result.Data.RequestId,
                SequenceNo = 1,
                Status = StatusValues.WaitingExternal,
                RetryCount = 0,
                SourceCallId = result.ExternalCall.CallId,
                ConsumedCredit = result.ConsumedCredit,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var waitingAt = NowUtc();
        job.Status = StatusValues.WaitingExternal;
        job.Progress = 25;
        job.NextRunAt = waitingAt + PollInterval;
        job.UpdatedAt = waitingAt;

        await UpdateRankStatusAsync(
            jobId,
            new RankCheckStatusSnapshot(
                StatusValues.WaitingExternal,
                ExternalRequestCount: 1,
                CompletedExternalRequestCount: 0,
                ExternalStatuses: new Dictionary<string, string>(StringComparer.Ordinal),
                "Registered external rank check request."),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await jobScheduler.SchedulePollAsync(jobId, PollInterval, cancellationToken);
        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<RankCheckPollOutcome>> PollStatusAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindRankCheckJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<RankCheckPollOutcome>(ErrorCode.NotFound, "Rank check job was not found.");
        }

        if (string.Equals(job.Status, StatusValues.Canceled, StringComparison.Ordinal))
        {
            return Result<RankCheckPollOutcome>.Success(new RankCheckPollOutcome(IsCompleted: false, IsCanceled: true));
        }

        if (!string.Equals(job.Status, StatusValues.WaitingExternal, StringComparison.Ordinal) &&
            !string.Equals(job.Status, StatusValues.Running, StringComparison.Ordinal))
        {
            return Result<RankCheckPollOutcome>.Success(new RankCheckPollOutcome(IsCompleted: false, IsCanceled: false));
        }

        var externalRequest = await dbContext.JobExternalRequests
            .FirstOrDefaultAsync(entity => entity.JobId == jobId && entity.SequenceNo == 1, cancellationToken);
        if (externalRequest is null)
        {
            return Failure<RankCheckPollOutcome>(ErrorCode.Conflict, "Rank check external request was not found.");
        }

        if (!string.Equals(externalRequest.Status, StatusValues.Succeeded, StringComparison.Ordinal))
        {
            var statusResult = await rakkoKeywordClient.GetSearchRankStatusAsync(
                CreateClientContext(context, jobId),
                externalRequest.ExternalRequestId,
                cancellationToken);
            if (!statusResult.IsSuccess || statusResult.Data is null)
            {
                return Result<RankCheckPollOutcome>.Failure(ToExternalError(statusResult, "Rank check status polling failed."));
            }

            var now = NowUtc();
            externalRequest.ConsumedCredit += statusResult.ConsumedCredit;
            externalRequest.UpdatedAt = now;
            if (statusResult.Data.IsCompleted)
            {
                externalRequest.Status = StatusValues.Succeeded;
                externalRequest.CompletedAt = now;
            }
            else
            {
                externalRequest.Status = StatusValues.WaitingExternal;
            }

            await UpdateRankStatusAsync(
                jobId,
                new RankCheckStatusSnapshot(
                    StatusValues.WaitingExternal,
                    ExternalRequestCount: 1,
                    CompletedExternalRequestCount: statusResult.Data.IsCompleted ? 1 : 0,
                    ExternalStatuses: statusResult.Data.Statuses,
                    statusResult.Data.IsCompleted ? "External rank check request completed." : "External rank check request is still running."),
                cancellationToken);
        }

        var allCompleted = string.Equals(externalRequest.Status, StatusValues.Succeeded, StringComparison.Ordinal);
        var updatedAt = NowUtc();
        job.Status = StatusValues.WaitingExternal;
        job.Progress = allCompleted ? 75 : 50;
        job.NextRunAt = allCompleted ? null : updatedAt + PollInterval;
        job.UpdatedAt = updatedAt;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (!allCompleted)
        {
            await jobScheduler.SchedulePollAsync(jobId, PollInterval, cancellationToken);
        }

        return Result<RankCheckPollOutcome>.Success(new RankCheckPollOutcome(allCompleted, IsCanceled: false));
    }

    public async Task<Result<JobReference>> FetchResultsAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var canIngest = await jobService.CanIngestExternalResultAsync(context, jobId, cancellationToken);
        if (!canIngest.IsSuccess)
        {
            return Result<JobReference>.Failure(canIngest.Error!);
        }

        if (canIngest.Value != true)
        {
            return Result<JobReference>.Success(new JobReference(jobId, StatusValues.Canceled));
        }

        var job = await FindRankCheckJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Rank check job was not found.");
        }

        var request = await ReadRequestOptionsAsync(jobId, cancellationToken);
        if (request is null)
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Rank check job payload was missing.");
        }

        if (await dbContext.RankResults.AnyAsync(entity => entity.JobId == jobId, cancellationToken))
        {
            var completion = await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(100, new JobResultResource(ResultResourceType, jobId)),
                cancellationToken);
            return completion.IsSuccess
                ? Result<JobReference>.Success(new JobReference(completion.Value!.JobId, completion.Value.Status))
                : Result<JobReference>.Failure(completion.Error!);
        }

        var externalRequest = await dbContext.JobExternalRequests
            .FirstOrDefaultAsync(
                entity => entity.JobId == jobId && entity.Status == StatusValues.Succeeded,
                cancellationToken);
        if (externalRequest is null)
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Completed rank check external request was not found.");
        }

        var result = await rakkoKeywordClient.GetSearchRankResultsAsync(
            CreateClientContext(context, jobId),
            externalRequest.ExternalRequestId,
            new RakkoSearchRankResultsRequest(Limit: ExternalResultLimit, WithAggregation: true),
            cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            return Result<JobReference>.Failure(ToExternalError(result, "Rank check results fetch failed."));
        }

        canIngest = await jobService.CanIngestExternalResultAsync(context, jobId, cancellationToken);
        if (!canIngest.IsSuccess)
        {
            return Result<JobReference>.Failure(canIngest.Error!);
        }

        if (canIngest.Value != true)
        {
            return Result<JobReference>.Success(new JobReference(jobId, StatusValues.Canceled));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        await SaveRankResultsAsync(project, jobId, request, result, cancellationToken);
        externalRequest.ConsumedCredit += result.ConsumedCredit;
        externalRequest.UpdatedAt = NowUtc();
        externalRequest.CompletedAt ??= externalRequest.UpdatedAt;

        await UpdateRankStatusAsync(
            jobId,
            new RankCheckStatusSnapshot(
                StatusValues.Succeeded,
                ExternalRequestCount: 1,
                CompletedExternalRequestCount: 1,
                ExternalStatuses: new Dictionary<string, string>(StringComparer.Ordinal) { ["overall"] = StatusValues.Succeeded },
                "Rank check results were fetched and saved."),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var completed = await jobService.CompleteAsync(
            context,
            jobId,
            new JobCompletion(100, new JobResultResource(ResultResourceType, jobId)),
            cancellationToken);
        return completed.IsSuccess
            ? Result<JobReference>.Success(new JobReference(completed.Value!.JobId, completed.Value.Status))
            : Result<JobReference>.Failure(completed.Error!);
    }

    public async Task<Result<int>> EvaluateAlertsAsync(
        ProjectExecutionContext context,
        Guid sourceRankCheckJobId,
        CancellationToken cancellationToken = default)
        => await EvaluateAlertsAsync(context, sourceRankCheckJobId, evaluationJobId: null, cancellationToken);

    public async Task<Result<int>> EvaluateAlertsAsync(
        ProjectExecutionContext context,
        Guid sourceRankCheckJobId,
        Guid? evaluationJobId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<int>(ErrorCode.NotFound, "Project was not found.");
        }

        var currentResults = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && entity.JobId == sourceRankCheckJobId)
            .OrderBy(entity => entity.KeywordId)
            .ThenBy(entity => entity.Target)
            .ToArrayAsync(cancellationToken);
        if (currentResults.Length == 0)
        {
            return Result<int>.Success(0);
        }

        var activeAlerts = await dbContext.Alerts
            .Where(entity => entity.ProjectId == project.Id && entity.Status == StatusValues.Active)
            .ToArrayAsync(cancellationToken);
        if (activeAlerts.Length == 0)
        {
            return Result<int>.Success(0);
        }

        var keywordIds = currentResults.Select(entity => entity.KeywordId).Distinct().ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        var allRankResults = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);

        var createdCount = 0;
        foreach (var alert in activeAlerts)
        {
            var condition = ParseJsonElement(alert.ConditionJson);
            foreach (var current in currentResults)
            {
                var previous = FindPreviousResult(allRankResults, current);
                if (!ShouldTrigger(alert.AlertType, condition, current, previous, currentResults))
                {
                    continue;
                }

                var eventType = NormalizeAlertType(alert.AlertType)!;
                var duplicateExists = await dbContext.AlertEvents.AnyAsync(
                    entity =>
                        entity.AlertId == alert.Id &&
                        entity.JobId == sourceRankCheckJobId &&
                        entity.KeywordId == current.KeywordId &&
                        entity.EventType == eventType,
                    cancellationToken);
                if (duplicateExists)
                {
                    continue;
                }

                var now = NowUtc();
                var alertEvent = new AlertEventEntity
                {
                    Id = UuidV7.New(),
                    AlertId = alert.Id,
                    ProjectId = project.Id,
                    JobId = sourceRankCheckJobId,
                    KeywordId = current.KeywordId,
                    EventType = eventType,
                    PreviousValueJson = SerializeRankValue(previous),
                    CurrentValueJson = SerializeRankValue(current),
                    EvidenceJson = SerializeAlertEvidence(alert.AlertType, condition, current, previous, keywords),
                    TriggeredAt = now
                };
                dbContext.AlertEvents.Add(alertEvent);
                alert.LastTriggeredAt = now;
                alert.UpdatedAt = now;
                await dbContext.SaveChangesAsync(cancellationToken);

                var delivery = await notificationService.EnqueueAsync(
                    context,
                    new NotificationRequest(
                        NotificationService.RankAlertEventType,
                        AlertEventResourceType,
                        alertEvent.Id,
                        BuildRankAlertMessage(alert.AlertType, current, previous, keywords),
                        JobId: sourceRankCheckJobId,
                        ChannelId: alert.NotificationChannelId),
                    cancellationToken);
                if (delivery.IsSuccess && delivery.Value?.DeliveryId.HasValue == true)
                {
                    alertEvent.NotificationDeliveryId = delivery.Value.DeliveryId.Value;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                createdCount++;
            }
        }

        return Result<int>.Success(createdCount);
    }

    private async Task SaveRankResultsAsync(
        ProjectEntity project,
        Guid jobId,
        NormalizedRankCheckJobRequest request,
        RakkoKeywordCallResult<RakkoExternalSearchResults> result,
        CancellationToken cancellationToken)
    {
        var checkedAt = NowUtc();
        foreach (var item in result.Data!.Items)
        {
            var keywordText = OptionalText(item.Keyword);
            if (keywordText is null)
            {
                continue;
            }

            var keyword = await EnsureKeywordAsync(keywordText, project.DefaultLanguage, cancellationToken);
            var parsedRankings = ParseRankings(item);
            foreach (var ranking in parsedRankings)
            {
                var target = NormalizeTargetOrFallback(ranking.Target ?? item.Target, request.Targets);
                var rankedUrl = NormalizeUrlOrFallback(ranking.RankedUrl ?? item.Url);
                var position = ToInt(ranking.Position ?? item.Position);
                if (target is null || rankedUrl is null || position is null)
                {
                    continue;
                }

                dbContext.RankResults.Add(new RankResultEntity
                {
                    Id = UuidV7.New(),
                    JobId = jobId,
                    ProjectId = project.Id,
                    KeywordId = keyword.Id,
                    Target = target,
                    Position = position.Value,
                    RankedUrl = rankedUrl,
                    EstimatedTraffic = ranking.EstimatedTraffic ?? item.EstimatedTraffic ?? 0m,
                    MetricsSnapshotJson = string.IsNullOrWhiteSpace(item.RawJson) ? "{}" : item.RawJson,
                    SourceCallId = result.ExternalCall.CallId,
                    ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                    CheckedAt = checkedAt
                });
            }
        }
    }

    private async Task<NormalizedRankCheckJobRequest?> ReadRequestOptionsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var json = await dbContext.RankCheckJobs
            .AsNoTracking()
            .Where(entity => entity.JobId == jobId)
            .Select(entity => entity.RequestOptionsJson)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<NormalizedRankCheckJobRequest>(json, JsonOptions);
    }

    private async Task UpdateRankStatusAsync(
        Guid jobId,
        RankCheckStatusSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var rankJob = await dbContext.RankCheckJobs
            .SingleAsync(entity => entity.JobId == jobId, cancellationToken);
        rankJob.StatusJson = JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private async Task<NormalizeResult> NormalizeRequestAsync(
        ProjectExecutionContext context,
        RankCheckJobRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
            return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return new NormalizeResult(null, new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var keywords = KeywordNormalizer.NormalizeMany(request.Keywords ?? []);
        if (keywords.Count == 0)
        {
            errors.Add("keywords", "keywords must contain at least one keyword.");
        }

        var matchType = NormalizeMatchType(request.MatchType);
        if (matchType is null)
        {
            errors.Add("matchType", "matchType must be domain, url, exact, or prefix.");
        }

        if (!AllowedDepths.Contains(request.Depth))
        {
            errors.Add("depth", "depth must be one of 30, 40, 50, 60, 70, 80, 90, or 100.");
        }

        var targets = NormalizeTargets(request.Targets ?? [], request.Deduplicate, errors);
        if (targets.Count == 0)
        {
            errors.Add("targets", "targets must contain at least one target.");
        }
        else if (targets.Count > MaxTargetCount)
        {
            errors.Add("targets", $"targets must contain {MaxTargetCount.ToString(CultureInfo.InvariantCulture)} items or fewer.");
        }

        if (errors.HasErrors)
        {
            return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var normalized = new NormalizedRankCheckJobRequest(
            Version: 1,
            keywords,
            targets,
            matchType!,
            request.Depth,
            request.WithMetrics,
            request.Deduplicate,
            IdempotencyKey: string.Empty,
            RequestHash: string.Empty);
        var requestHash = HashText(JsonSerializer.Serialize(
            normalized with { IdempotencyKey = string.Empty, RequestHash = string.Empty },
            JsonOptions));
        normalized = normalized with
        {
            IdempotencyKey = BuildIdempotencyKey(context.ProjectId.Value, requestHash),
            RequestHash = requestHash
        };

        return new NormalizeResult(normalized, null);
    }

    private async Task<NormalizeAlertResult> NormalizeAlertRequestAsync(
        ProjectExecutionContext context,
        string? alertTypeValue,
        JsonElement? condition,
        Guid? notificationChannelId,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var alertType = NormalizeAlertType(alertTypeValue);
        if (alertType is null)
        {
            errors.Add("alertType", "alertType must be rank_drop, out_of_range, competitor_passed, top_entry, or four_to_ten.");
        }

        if (notificationChannelId.HasValue)
        {
            var exists = await dbContext.NotificationChannels
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.WorkspaceId == context.WorkspaceId &&
                        entity.Id == notificationChannelId.Value &&
                        entity.Status == StatusValues.Active &&
                        (entity.ProjectId == null || entity.ProjectId == context.ProjectId),
                    cancellationToken);
            if (!exists)
            {
                errors.Add("notificationChannelId", "notificationChannelId must refer to an active notification channel in scope.");
            }
        }

        if (errors.HasErrors)
        {
            return new NormalizeAlertResult(null, null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        return new NormalizeAlertResult(alertType, SerializeCondition(condition), null);
    }

    private async Task<KeywordEntity> EnsureKeywordAsync(
        string keyword,
        string language,
        CancellationToken cancellationToken)
    {
        var normalized = KeywordNormalizer.Normalize(keyword);
        var hash = HashText(normalized);
        var existing = await dbContext.Keywords
            .FirstOrDefaultAsync(entity => entity.Language == language && entity.TextHash == hash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var keywordEntity = new KeywordEntity
        {
            Id = UuidV7.New(),
            NormalizedText = normalized,
            Language = language,
            TextHash = hash,
            CreatedAt = NowUtc()
        };
        dbContext.Keywords.Add(keywordEntity);
        return keywordEntity;
    }

    private async Task<ProjectEntity?> FindActiveProjectAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        return await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
    }

    private async Task<JobEntity?> FindRankCheckJobAsync(
        ProjectExecutionContext context,
        Guid jobId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var source = asTracking ? dbContext.Jobs : dbContext.Jobs.AsNoTracking();
        return await source.FirstOrDefaultAsync(
            entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId &&
                entity.Id == jobId &&
                entity.JobType == RegisterJobType,
            cancellationToken);
    }

    private async Task<AlertEntity?> FindAlertAsync(
        ProjectExecutionContext context,
        Guid alertId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        var source = asTracking ? dbContext.Alerts : dbContext.Alerts.AsNoTracking();
        return await source.FirstOrDefaultAsync(
            entity =>
                entity.ProjectId == context.ProjectId.Value &&
                entity.Id == alertId,
            cancellationToken);
    }

    private void AddJobQueuedAudit(ProjectExecutionContext context, JobEntity job)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                AuditLogActionNames.JobQueued,
                AuditLogResourceTypes.Job,
                job.Id.ToString("D"),
                new
                {
                    before = (object?)null,
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

    private static RakkoKeywordClientContext CreateClientContext(ProjectExecutionContext context, Guid jobId)
        => new(
            context.WorkspaceId,
            context.ProjectId,
            jobId,
            ApiContractScopeId: SeoIntelligenceSeedData.DefaultRakkoContractScopeId,
            ContractScopeKey: SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CorrelationId: context.CorrelationId,
            Actor: context.Actor);

    private static Error ToExternalError<T>(RakkoKeywordCallResult<T> result, string fallbackMessage)
    {
        var code = result.IsRetryable
            ? ErrorCode.ExternalTemporaryFailure
            : result.StatusCode switch
            {
                402 => ErrorCode.CreditInsufficient,
                403 => ErrorCode.Forbidden,
                429 => ErrorCode.RateLimited,
                _ => ErrorCode.ExternalFatalFailure
            };

        return new Error(
            code,
            result.Errors.FirstOrDefault() ?? fallbackMessage,
            new Dictionary<string, string[]>
            {
                ["statusCode"] = [result.StatusCode.ToString(CultureInfo.InvariantCulture)],
                ["errorCode"] = [result.ExternalCall.ErrorCode ?? string.Empty]
            });
    }

    private static IReadOnlyList<NormalizedRankCheckTarget> NormalizeTargets(
        IReadOnlyList<RankCheckTargetRequest> targets,
        bool deduplicate,
        ValidationErrors errors)
    {
        var normalized = new List<NormalizedRankCheckTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            var targetType = NormalizeTargetType(target.TargetType);
            if (targetType is null)
            {
                errors.Add("targets", "targetType must be domain or url.");
                continue;
            }

            var targetText = OptionalText(target.Target);
            if (targetText is null)
            {
                errors.Add("targets", "target is required.");
                continue;
            }

            string normalizedTarget;
            try
            {
                normalizedTarget = string.Equals(targetType, "url", StringComparison.Ordinal)
                    ? UrlNormalizer.NormalizeUrl(targetText)
                    : UrlNormalizer.NormalizeDomain(targetText);
            }
            catch (ArgumentException)
            {
                errors.Add("targets", "target must be a valid http(s) URL or domain.");
                continue;
            }
            catch (UriFormatException)
            {
                errors.Add("targets", "target must be a valid http(s) URL or domain.");
                continue;
            }

            var dedupeKey = $"{targetType}:{normalizedTarget}";
            if (deduplicate && !seen.Add(dedupeKey))
            {
                continue;
            }

            normalized.Add(new NormalizedRankCheckTarget(normalizedTarget, targetType));
        }

        return normalized;
    }

    private static IReadOnlyList<ParsedRanking> ParseRankings(RakkoExternalSearchResultItem item)
    {
        if (string.IsNullOrWhiteSpace(item.RawJson))
        {
            return [new ParsedRanking(item.Target, item.Url, item.Position, item.EstimatedTraffic)];
        }

        try
        {
            using var document = JsonDocument.Parse(item.RawJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("rankings", out var rankings) &&
                rankings.ValueKind == JsonValueKind.Array)
            {
                return rankings
                    .EnumerateArray()
                    .Select(ranking => new ParsedRanking(
                        GetString(ranking, "target"),
                        GetString(ranking, "rankedUrl") ?? GetString(ranking, "url"),
                        GetDecimal(ranking, "position"),
                        GetDecimal(ranking, "estimatedTraffic")))
                    .ToArray();
            }
        }
        catch (JsonException)
        {
        }

        return [new ParsedRanking(item.Target, item.Url, item.Position, item.EstimatedTraffic)];
    }

    private static string? NormalizeTargetOrFallback(string? target, IReadOnlyList<NormalizedRankCheckTarget> configuredTargets)
    {
        var value = OptionalText(target);
        if (value is not null)
        {
            foreach (var configured in configuredTargets)
            {
                if (string.Equals(configured.Target, value, StringComparison.OrdinalIgnoreCase))
                {
                    return configured.Target;
                }
            }

            try
            {
                var normalizedUrl = UrlNormalizer.NormalizeUrl(value);
                var matchingUrl = configuredTargets.FirstOrDefault(configured =>
                    string.Equals(configured.TargetType, "url", StringComparison.Ordinal) &&
                    string.Equals(configured.Target, normalizedUrl, StringComparison.OrdinalIgnoreCase));
                if (matchingUrl is not null)
                {
                    return matchingUrl.Target;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (UriFormatException)
            {
            }

            try
            {
                var normalizedDomain = UrlNormalizer.NormalizeDomain(value);
                var matchingDomain = configuredTargets.FirstOrDefault(configured =>
                    string.Equals(configured.TargetType, "domain", StringComparison.Ordinal) &&
                    string.Equals(configured.Target, normalizedDomain, StringComparison.OrdinalIgnoreCase));
                if (matchingDomain is not null)
                {
                    return matchingDomain.Target;
                }

                return normalizedDomain;
            }
            catch (ArgumentException)
            {
                return value;
            }
            catch (UriFormatException)
            {
                return value;
            }
        }

        return configuredTargets.Count == 1 ? configuredTargets[0].Target : null;
    }

    private static string? NormalizeUrlOrFallback(string? value)
    {
        var text = OptionalText(value);
        if (text is null)
        {
            return null;
        }

        try
        {
            return UrlNormalizer.NormalizeUrl(text);
        }
        catch (ArgumentException)
        {
            return text;
        }
        catch (UriFormatException)
        {
            return text;
        }
    }

    private static RankResultRow MapRankResultRow(
        RankResultEntity entity,
        IReadOnlyDictionary<Guid, string> keywords,
        IReadOnlyList<RankResultEntity> allRankResults)
    {
        var previous = FindPreviousResult(allRankResults, entity);
        return new RankResultRow(
            entity.Id,
            entity.JobId,
            entity.KeywordId,
            keywords.GetValueOrDefault(entity.KeywordId) ?? string.Empty,
            entity.Target,
            entity.Position,
            previous?.Position,
            previous is null ? null : entity.Position - previous.Position,
            entity.RankedUrl,
            entity.EstimatedTraffic,
            ParseJsonElementOrEmpty(entity.MetricsSnapshotJson),
            entity.ContractScopeKey,
            entity.CheckedAt);
    }

    private static RankResultEntity? FindPreviousResult(
        IReadOnlyList<RankResultEntity> allRankResults,
        RankResultEntity current)
        => allRankResults
            .Where(entity =>
                entity.Id != current.Id &&
                entity.ProjectId == current.ProjectId &&
                entity.KeywordId == current.KeywordId &&
                string.Equals(entity.Target, current.Target, StringComparison.OrdinalIgnoreCase) &&
                entity.CheckedAt < current.CheckedAt)
            .OrderByDescending(entity => entity.CheckedAt)
            .FirstOrDefault();

    private static IEnumerable<RankResultRow> FilterRankResults(
        IEnumerable<RankResultRow> rows,
        RankResultSearchQuery query)
    {
        if (query.JobId.HasValue)
        {
            rows = rows.Where(row => row.JobId == query.JobId.Value);
        }

        if (query.KeywordId.HasValue)
        {
            rows = rows.Where(row => row.KeywordId == query.KeywordId.Value);
        }

        var target = OptionalText(query.Target);
        if (target is not null)
        {
            rows = rows.Where(row => row.Target.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MinPosition.HasValue)
        {
            rows = rows.Where(row => row.Position >= query.MinPosition.Value);
        }

        if (query.MaxPosition.HasValue)
        {
            rows = rows.Where(row => row.Position <= query.MaxPosition.Value);
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.Target.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.RankedUrl.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private static IEnumerable<RankResultRow> SortRankResults(
        IEnumerable<RankResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "checkedAt";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "keyword" => SortString(rows, ascending, row => row.Keyword),
            "target" => SortString(rows, ascending, row => row.Target),
            "position" => SortInt(rows, ascending, row => row.Position),
            "previousPosition" => SortNullableInt(rows, ascending, row => row.PreviousPosition),
            "positionDelta" => SortNullableInt(rows, ascending, row => row.PositionDelta),
            "estimatedTraffic" => SortDecimal(rows, ascending, row => row.EstimatedTraffic),
            _ => SortDateTime(rows, ascending, row => row.CheckedAt)
        };
    }

    private static RankDistribution BuildDistribution(IEnumerable<RankResultRow> rows)
    {
        var materialized = rows.ToArray();
        return new RankDistribution(
            materialized.Count(row => row.Position is >= 1 and <= 3),
            materialized.Count(row => row.Position is >= 4 and <= 10),
            materialized.Count(row => row.Position is >= 11 and <= 20),
            materialized.Count(row => row.Position is >= 21 and <= 50),
            materialized.Count(row => row.Position is >= 51 and <= 100),
            materialized.Count(row => row.Position <= 0 || row.Position > 100));
    }

    private static bool ShouldTrigger(
        string alertType,
        JsonElement? condition,
        RankResultEntity current,
        RankResultEntity? previous,
        IReadOnlyList<RankResultEntity> currentResults)
    {
        var normalized = NormalizeAlertType(alertType);
        return normalized switch
        {
            "rank_drop" => previous is not null &&
                current.Position - previous.Position >= GetConditionInt(condition, "minDrop", 5),
            "out_of_range" => current.Position <= 0 ||
                current.Position > GetConditionInt(condition, "maxPosition", 100),
            "top_entry" => current.Position <= GetConditionInt(condition, "maxPosition", 10) &&
                (previous is null || previous.Position > GetConditionInt(condition, "maxPosition", 10)),
            "four_to_ten" => current.Position is >= 4 and <= 10 &&
                (previous is null || previous.Position is < 4 or > 10),
            "competitor_passed" => IsCompetitorPassed(condition, current, currentResults),
            _ => false
        };
    }

    private static bool IsCompetitorPassed(
        JsonElement? condition,
        RankResultEntity current,
        IReadOnlyList<RankResultEntity> currentResults)
    {
        var competitorTarget = OptionalText(GetConditionString(condition, "competitorTarget"));
        var ownTarget = OptionalText(GetConditionString(condition, "ownTarget"));
        if (competitorTarget is null || ownTarget is null)
        {
            return false;
        }

        if (!string.Equals(current.Target, competitorTarget, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var own = currentResults.FirstOrDefault(entity =>
            entity.KeywordId == current.KeywordId &&
            string.Equals(entity.Target, ownTarget, StringComparison.OrdinalIgnoreCase));
        return own is not null &&
            current.Position > 0 &&
            own.Position > 0 &&
            current.Position < own.Position;
    }

    private static string SerializeRankValue(RankResultEntity? result)
        => result is null
            ? "{}"
            : JsonSerializer.Serialize(new
            {
                result.Position,
                result.RankedUrl,
                result.Target,
                result.EstimatedTraffic,
                result.CheckedAt
            }, JsonOptions);

    private static string SerializeAlertEvidence(
        string alertType,
        JsonElement? condition,
        RankResultEntity current,
        RankResultEntity? previous,
        IReadOnlyDictionary<Guid, string> keywords)
        => JsonSerializer.Serialize(new
        {
            alertType,
            keyword = keywords.GetValueOrDefault(current.KeywordId),
            current.Target,
            current.Position,
            previousPosition = previous?.Position,
            delta = previous is null ? (int?)null : current.Position - previous.Position,
            condition = condition.HasValue ? condition.Value : ParseJsonElement("{}")
        }, JsonOptions);

    private static string BuildRankAlertMessage(
        string alertType,
        RankResultEntity current,
        RankResultEntity? previous,
        IReadOnlyDictionary<Guid, string> keywords)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                $"[rank_alert] {alertType}",
                $"Keyword: {keywords.GetValueOrDefault(current.KeywordId) ?? current.KeywordId.ToString("D")}",
                $"Target: {current.Target}",
                $"Position: {current.Position.ToString(CultureInfo.InvariantCulture)}",
                previous is null ? null : $"Previous: {previous.Position.ToString(CultureInfo.InvariantCulture)}",
                $"Ranked URL: {current.RankedUrl}"
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static RankAlertDetails MapAlertDetails(AlertEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.AlertType,
            ParseJsonElement(entity.ConditionJson),
            entity.NotificationChannelId,
            entity.Status,
            entity.LastTriggeredAt,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static RankAlertEventDetails MapAlertEventDetails(
        AlertEventEntity entity,
        IReadOnlyDictionary<Guid, string> keywords)
        => new(
            entity.Id,
            entity.AlertId,
            entity.ProjectId,
            entity.JobId,
            entity.KeywordId,
            entity.KeywordId.HasValue && keywords.TryGetValue(entity.KeywordId.Value, out var keyword) ? keyword : null,
            entity.EventType,
            ParseJsonElement(entity.PreviousValueJson),
            ParseJsonElement(entity.CurrentValueJson),
            ParseJsonElement(entity.EvidenceJson),
            entity.NotificationDeliveryId,
            entity.TriggeredAt,
            entity.ResolvedAt);

    private static IEnumerable<RankAlertDetails> SortAlerts(
        IEnumerable<RankAlertDetails> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "createdAt";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "alertType" => SortString(rows, ascending, row => row.AlertType),
            "status" => SortString(rows, ascending, row => row.Status),
            "lastTriggeredAt" => SortNullableDateTime(rows, ascending, row => row.LastTriggeredAt),
            "updatedAt" => SortDateTime(rows, ascending, row => row.UpdatedAt),
            _ => SortDateTime(rows, ascending, row => row.CreatedAt)
        };
    }

    private static IEnumerable<RankAlertEventDetails> SortAlertEvents(
        IEnumerable<RankAlertEventDetails> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "triggeredAt";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "eventType" => SortString(rows, ascending, row => row.EventType),
            "keyword" => SortString(rows, ascending, row => row.Keyword),
            _ => SortDateTime(rows, ascending, row => row.TriggeredAt)
        };
    }

    private static PagedResult<T> ToPagedResult<T>(IEnumerable<T> rows, SearchQuery query)
    {
        var page = query.EffectivePage;
        var materialized = rows.ToArray();
        var pagedRows = materialized
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToArray();
        return new PagedResult<T>(pagedRows, page.Page, page.PageSize, materialized.LongLength);
    }

    private static IOrderedEnumerable<T> SortString<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, string?> selector)
        => ascending
            ? rows.OrderBy(row => selector(row) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            : rows.OrderByDescending(row => selector(row) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static IOrderedEnumerable<T> SortInt<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, int> selector)
        => ascending ? rows.OrderBy(selector) : rows.OrderByDescending(selector);

    private static IOrderedEnumerable<T> SortNullableInt<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, int?> selector)
        => ascending
            ? rows.OrderBy(row => selector(row) ?? int.MaxValue)
            : rows.OrderByDescending(row => selector(row) ?? int.MinValue);

    private static IOrderedEnumerable<T> SortDecimal<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, decimal> selector)
        => ascending ? rows.OrderBy(selector) : rows.OrderByDescending(selector);

    private static IOrderedEnumerable<T> SortDateTime<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, DateTime> selector)
        => ascending ? rows.OrderBy(selector) : rows.OrderByDescending(selector);

    private static IOrderedEnumerable<T> SortNullableDateTime<T>(
        IEnumerable<T> rows,
        bool ascending,
        Func<T, DateTime?> selector)
        => ascending
            ? rows.OrderBy(row => selector(row) ?? DateTime.MaxValue)
            : rows.OrderByDescending(row => selector(row) ?? DateTime.MinValue);

    private static string? NormalizeMatchType(string? value)
    {
        var normalized = OptionalText(value)?.ToLowerInvariant() ?? "domain";
        return AllowedMatchTypes.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeTargetType(string? value)
    {
        var normalized = OptionalText(value)?.ToLowerInvariant() ?? "domain";
        return AllowedTargetTypes.Contains(normalized) ? normalized : null;
    }

    private static string? NormalizeAlertType(string? value)
    {
        var normalized = OptionalText(value)?.ToLowerInvariant();
        return normalized is not null && AllowedAlertTypes.Contains(normalized) ? normalized : null;
    }

    private static string SerializeCondition(JsonElement? condition)
        => condition.HasValue &&
            condition.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? condition.Value.GetRawText()
                : "{}";

    private static JsonElement ParseJsonElement(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }
    }

    private static JsonElement ParseJsonElementOrEmpty(string json)
        => ParseJsonElement(json);

    private static string? GetString(JsonElement? root, string propertyName)
        => root.HasValue ? GetString(root.Value, propertyName) : null;

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static int GetConditionInt(JsonElement? condition, string propertyName, int defaultValue)
    {
        if (!condition.HasValue ||
            condition.Value.ValueKind != JsonValueKind.Object ||
            !condition.Value.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
    }

    private static string? GetConditionString(JsonElement? condition, string propertyName)
        => !condition.HasValue ? null : GetString(condition.Value, propertyName);

    private static int? ToInt(decimal? value)
        => value.HasValue
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : null;

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string BuildIdempotencyKey(Guid projectId, string requestHash)
        => $"rank-check:{projectId:N}:{requestHash}";

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => errors.Count > 0;

        public void Add(string target, string message)
        {
            if (!errors.TryGetValue(target, out var messages))
            {
                messages = [];
                errors[target] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record NormalizeResult(NormalizedRankCheckJobRequest? Request, Error? Error);

    private sealed record NormalizeAlertResult(string? AlertType, string? ConditionJson, Error? Error);

    private sealed record ParsedRanking(string? Target, string? RankedUrl, decimal? Position, decimal? EstimatedTraffic);
}

internal sealed record RankCheckPollOutcome(bool IsCompleted, bool IsCanceled);

internal sealed record NormalizedRankCheckTarget(string Target, string TargetType);

internal sealed record NormalizedRankCheckJobRequest(
    int Version,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<NormalizedRankCheckTarget> Targets,
    string MatchType,
    int Depth,
    bool WithMetrics,
    bool Deduplicate,
    string IdempotencyKey,
    string RequestHash)
{
    public NormalizedRankCheckJobRequest ToOptions()
        => this;
}

internal sealed record RankCheckStatusSnapshot(
    string Status,
    int ExternalRequestCount,
    int CompletedExternalRequestCount,
    IReadOnlyDictionary<string, string> ExternalStatuses,
    string? Message);

internal interface IRankCheckJobScheduler
{
    Task SchedulePollAsync(Guid jobId, TimeSpan delay, CancellationToken cancellationToken = default);
}

internal sealed class RankCheckHangfireJobScheduler(
    IServiceProvider serviceProvider,
    ILogger<RankCheckHangfireJobScheduler> logger)
    : IRankCheckJobScheduler
{
    public Task SchedulePollAsync(Guid jobId, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = serviceProvider.GetService<IBackgroundJobClient>();
        if (client is null)
        {
            logger.LogDebug("Hangfire is not configured; rank check poll for job {job_id} was not scheduled.", jobId);
            return Task.CompletedTask;
        }

        client.Schedule<PollRankStatusJob>(job => job.ExecuteAsync(jobId), delay);
        return Task.CompletedTask;
    }
}

internal sealed class RegisterRankCheckJob(
    SeoIntelligenceDbContext dbContext,
    RankMonitoringService rankMonitoringService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<RegisterRankCheckJob> logger)
{
    public const string JobType = RankMonitoringService.RegisterJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Rank check registration job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.IdempotencyKey, TimeSpan.FromMinutes(30)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Rank check registration job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await rankMonitoringService.RegisterExternalRequestAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Rank check registration job {job_id} could not persist state.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Rank check registration could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Rank check registration job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Rank check registration failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        return JobFailure.FromHttpStatusCode(statusCode, TryReadDetail(error, "errorCode"), error.Message);
    }

    private static int? TryReadStatusCode(Error error)
        => int.TryParse(TryReadDetail(error, "statusCode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode)
            ? statusCode
            : null;

    private static string? TryReadDetail(Error error, string key)
        => error.Details is not null &&
            error.Details.TryGetValue(key, out var values) &&
            values.Length > 0
            ? values[0]
            : null;
}

internal sealed class PollRankStatusJob(
    SeoIntelligenceDbContext dbContext,
    RankMonitoringService rankMonitoringService,
    FetchRankResultsJob fetchRankResultsJob,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<PollRankStatusJob> logger)
{
    public const string JobType = "PollRankStatusJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Rank check polling job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        try
        {
            var poll = await rankMonitoringService.PollStatusAsync(context, jobId);
            if (!poll.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(poll.Error!));
                return;
            }

            if (poll.Value!.IsCompleted)
            {
                await fetchRankResultsJob.ExecuteAsync(jobId);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Rank check polling job {job_id} could not persist state.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Rank check polling could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Rank check polling job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Rank check polling failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        return JobFailure.FromHttpStatusCode(statusCode, TryReadDetail(error, "errorCode"), error.Message);
    }

    private static int? TryReadStatusCode(Error error)
        => int.TryParse(TryReadDetail(error, "statusCode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode)
            ? statusCode
            : null;

    private static string? TryReadDetail(Error error, string key)
        => error.Details is not null &&
            error.Details.TryGetValue(key, out var values) &&
            values.Length > 0
            ? values[0]
            : null;
}

internal sealed class FetchRankResultsJob(
    SeoIntelligenceDbContext dbContext,
    RankMonitoringService rankMonitoringService,
    RankAlertEvaluateJob rankAlertEvaluateJob,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<FetchRankResultsJob> logger)
{
    public const string JobType = "FetchRankResultsJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Rank check result fetch job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        try
        {
            var result = await rankMonitoringService.FetchResultsAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
                return;
            }

            if (string.Equals(result.Value!.Status, StatusValues.Succeeded, StringComparison.Ordinal))
            {
                await RegisterAndRunAlertEvaluationAsync(context, jobId);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Rank check result fetch job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Rank check results could not be persisted."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Rank check result fetch job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Rank check result fetch failed unexpectedly."));
        }
    }

    private async Task RegisterAndRunAlertEvaluationAsync(ProjectExecutionContext context, Guid rankCheckJobId)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            version = 1,
            sourceRankCheckJobId = rankCheckJobId
        });
        var evaluationJob = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                RankAlertEvaluateJob.JobType,
                payload,
                IdempotencyKey: $"rank-alert-evaluate:{rankCheckJobId:N}",
                TargetKey: rankCheckJobId.ToString("N"),
                Queue: "analysis",
                InitialResource: new JobResultResource(RankMonitoringService.ResultResourceType, rankCheckJobId)));
        if (!evaluationJob.IsSuccess)
        {
            logger.LogWarning(
                "Rank alert evaluation job for rank check job {job_id} could not be registered: {message}",
                rankCheckJobId,
                evaluationJob.Error?.Message);
            return;
        }

        if (string.Equals(evaluationJob.Value!.Status, StatusValues.Queued, StringComparison.Ordinal))
        {
            await rankAlertEvaluateJob.ExecuteAsync(evaluationJob.Value.JobId);
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        return JobFailure.FromHttpStatusCode(statusCode, TryReadDetail(error, "errorCode"), error.Message);
    }

    private static int? TryReadStatusCode(Error error)
        => int.TryParse(TryReadDetail(error, "statusCode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode)
            ? statusCode
            : null;

    private static string? TryReadDetail(Error error, string key)
        => error.Details is not null &&
            error.Details.TryGetValue(key, out var values) &&
            values.Length > 0
            ? values[0]
            : null;
}

internal sealed class RankAlertEvaluateJob(
    SeoIntelligenceDbContext dbContext,
    RankMonitoringService rankMonitoringService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<RankAlertEvaluateJob> logger)
{
    public const string JobType = "RankAlertEvaluateJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Rank alert evaluation job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (!string.Equals(job.ResultResourceType, RankMonitoringService.ResultResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Rank alert evaluation job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Rank alert evaluation job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await rankMonitoringService.EvaluateAlertsAsync(context, job.ResultResourceId.Value, jobId);
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(
                    context,
                    jobId,
                    new JobFailure(JobFailureKind.Unexpected, null, result.Error!.Code.ToString(), result.Error.Message));
                return;
            }

            await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(100, new JobResultResource(RankMonitoringService.ResultResourceType, job.ResultResourceId.Value)));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Rank alert evaluation job {job_id} could not persist events.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Rank alert evaluation could not persist events."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Rank alert evaluation job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Rank alert evaluation failed unexpectedly."));
        }
    }
}
