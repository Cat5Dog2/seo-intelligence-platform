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

internal sealed class SearchVolumeService(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    IAuditLogWriter auditLogWriter,
    IJobQueueClient jobQueueClient,
    ISearchVolumeJobScheduler jobScheduler,
    TimeProvider timeProvider)
    : ISearchVolumeService
{
    public const string RegisterJobType = "RegisterSearchVolumeJob";
    public const string ResultResourceType = "search_volume_job";
    public const int MaxKeywordCount = 50_000;
    public const int ExternalRequestKeywordLimit = 50_000;

    // ラッコキーワードAPI v1.12.0 の POST /v1/search-volume 消費クレジット:
    // 0.03/キーワード、seoDifficulty有効時は追加で0.75/キーワード、1リクエスト最低15クレジット。
    public const decimal CreditPerKeyword = 0.03m;
    public const decimal SeoDifficultyCreditPerKeyword = 0.75m;
    public const decimal MinimumCreditPerExternalRequest = 15m;

    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<int> AllowedAggregationMonths = [12, 24, 36, 48];
    private static readonly HashSet<string> CompletedExternalRequestStatuses =
    [
        StatusValues.Succeeded,
        StatusValues.Canceled
    ];

    public async Task<Result<JobReference>> RegisterAsync(
        ProjectExecutionContext context,
        SearchVolumeJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeRequestAsync(context, request, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<JobReference>.Failure(normalized.Error);
        }

        var searchVolumeRequest = normalized.Request!;
        var existing = await dbContext.Jobs
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId &&
                entity.JobType == RegisterJobType &&
                entity.IdempotencyKey == searchVolumeRequest.IdempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, searchVolumeRequest.RequestHash, StringComparison.Ordinal))
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
            IdempotencyKey = searchVolumeRequest.IdempotencyKey,
            RequestHash = searchVolumeRequest.RequestHash,
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };
        job.ResultResourceId = job.Id;

        var searchVolumeJob = new SearchVolumeJobEntity
        {
            JobId = job.Id,
            Location = searchVolumeRequest.Location,
            Language = searchVolumeRequest.Language,
            AggregationMonths = searchVolumeRequest.AggregationPeriodMonths,
            RequestOptionsJson = JsonSerializer.Serialize(searchVolumeRequest.ToOptions(), JsonOptions),
            StatusJson = JsonSerializer.Serialize(
                new SearchVolumeStatusSnapshot(
                    StatusValues.Queued,
                    ExternalRequestCount: 0,
                    CompletedExternalRequestCount: 0,
                    searchVolumeRequest.EstimatedCredit,
                    Message: null),
                JsonOptions)
        };

        dbContext.Jobs.Add(job);
        dbContext.SearchVolumeJobs.Add(searchVolumeJob);
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
                        entity.IdempotencyKey == searchVolumeRequest.IdempotencyKey,
                    cancellationToken);

            if (duplicate is null)
            {
                throw;
            }

            return string.Equals(duplicate.RequestHash, searchVolumeRequest.RequestHash, StringComparison.Ordinal)
                ? Result<JobReference>.Success(new JobReference(duplicate.Id, duplicate.Status))
                : Failure<JobReference>(
                    ErrorCode.Conflict,
                    "Idempotency-Key was already used for a different request hash.");
        }

        await jobQueueClient.EnqueueAsync(job.Id, "external-api", cancellationToken);
        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<JobReference>> GetJobAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindSearchVolumeJobAsync(context, jobId, asTracking: false, cancellationToken);
        return job is null
            ? Failure<JobReference>(ErrorCode.NotFound, "Search volume job was not found.")
            : Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<PagedResult<SearchVolumeResultRow>>> GetResultsAsync(
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
            return Failure<PagedResult<SearchVolumeResultRow>>(ErrorCode.NotFound, "Search volume job was not found.");
        }

        var entities = await dbContext.SearchVolumeResults
            .AsNoTracking()
            .Where(entity => entity.JobId == jobId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => new SearchVolumeResultProjection(result, keyword))
            .ToArrayAsync(cancellationToken);

        var rows = entities.Select(MapResultRow);
        var q = OptionalText(query.Q);
        if (q is not null)
        {
            rows = rows.Where(row => row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortResults(rows, query.Sort).ToArray();
        var page = query.EffectivePage;
        var totalCount = rows.LongCount();
        var pagedRows = rows
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToArray();

        return Result<PagedResult<SearchVolumeResultRow>>.Success(
            new PagedResult<SearchVolumeResultRow>(
                pagedRows,
                page.Page,
                page.PageSize,
                totalCount));
    }

    public async Task<Result<JobReference>> RegisterExternalRequestsAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindSearchVolumeJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Search volume job was not found.");
        }

        var request = await ReadRequestOptionsAsync(jobId, cancellationToken);
        if (request is null)
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Search volume job payload was missing.");
        }

        var existingSequences = await dbContext.JobExternalRequests
            .AsNoTracking()
            .Where(entity => entity.JobId == jobId)
            .Select(entity => entity.SequenceNo)
            .ToHashSetAsync(cancellationToken);

        var chunks = request.Keywords
            .Chunk(request.ExternalRequestKeywordLimit)
            .Select((keywords, index) => new SearchVolumeKeywordChunk(index + 1, keywords))
            .ToArray();

        var consumedCredit = await dbContext.JobExternalRequests
            .Where(entity => entity.JobId == jobId)
            .SumAsync(entity => entity.ConsumedCredit, cancellationToken);
        foreach (var chunk in chunks)
        {
            if (existingSequences.Contains(chunk.SequenceNo))
            {
                continue;
            }

            var result = await rakkoKeywordClient.RegisterSearchVolumeAsync(
                CreateClientContext(context, jobId),
                new RakkoSearchVolumeRegistrationRequest(
                    chunk.Keywords,
                    SeoDifficulty: request.SeoDifficulty,
                    DataCompletion: true,
                    request.Location,
                    request.Language,
                    Deduplicate: true,
                    request.AggregationPeriodMonths),
                cancellationToken);

            if (!result.IsSuccess || result.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(result));
            }

            consumedCredit += result.ConsumedCredit;
            var now = NowUtc();
            dbContext.JobExternalRequests.Add(new JobExternalRequestEntity
            {
                Id = UuidV7.New(),
                JobId = jobId,
                Endpoint = "/v1/search-volume",
                ExternalRequestId = result.Data.RequestId.ToString(CultureInfo.InvariantCulture),
                SequenceNo = chunk.SequenceNo,
                Status = StatusValues.WaitingExternal,
                RetryCount = 0,
                SourceCallId = result.ExternalCall.CallId,
                ConsumedCredit = result.ConsumedCredit,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var completedCount = await dbContext.JobExternalRequests
            .Where(entity => entity.JobId == jobId && CompletedExternalRequestStatuses.Contains(entity.Status))
            .CountAsync(cancellationToken);
        var registeredCount = Math.Max(existingSequences.Count, chunks.Length);
        var waitingAt = NowUtc();
        job.Status = StatusValues.WaitingExternal;
        job.Progress = 25;
        job.NextRunAt = waitingAt + PollInterval;
        job.UpdatedAt = waitingAt;

        await UpdateSearchVolumeStatusAsync(
            jobId,
            new SearchVolumeStatusSnapshot(
                StatusValues.WaitingExternal,
                registeredCount,
                completedCount,
                request.EstimatedCredit,
                $"Registered {registeredCount.ToString(CultureInfo.InvariantCulture)} external search volume request(s)."),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await jobScheduler.SchedulePollAsync(jobId, PollInterval, cancellationToken);
        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<SearchVolumePollOutcome>> PollStatusAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await FindSearchVolumeJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<SearchVolumePollOutcome>(ErrorCode.NotFound, "Search volume job was not found.");
        }

        if (string.Equals(job.Status, StatusValues.Canceled, StringComparison.Ordinal))
        {
            return Result<SearchVolumePollOutcome>.Success(new SearchVolumePollOutcome(IsCompleted: false, IsCanceled: true));
        }

        if (!string.Equals(job.Status, StatusValues.WaitingExternal, StringComparison.Ordinal) &&
            !string.Equals(job.Status, StatusValues.Running, StringComparison.Ordinal))
        {
            return Result<SearchVolumePollOutcome>.Success(new SearchVolumePollOutcome(IsCompleted: false, IsCanceled: false));
        }

        var request = await ReadRequestOptionsAsync(jobId, cancellationToken);
        if (request is null)
        {
            return Failure<SearchVolumePollOutcome>(ErrorCode.Conflict, "Search volume job payload was missing.");
        }

        var allExternalRequests = await dbContext.JobExternalRequests
            .Where(entity => entity.JobId == jobId)
            .OrderBy(entity => entity.SequenceNo)
            .ToArrayAsync(cancellationToken);
        var externalRequests = allExternalRequests
            .Where(entity => !CompletedExternalRequestStatuses.Contains(entity.Status))
            .ToArray();

        foreach (var externalRequest in externalRequests)
        {
            if (!long.TryParse(externalRequest.ExternalRequestId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestId))
            {
                return Failure<SearchVolumePollOutcome>(ErrorCode.Conflict, "External request id was invalid.");
            }

            var statusResult = await rakkoKeywordClient.GetSearchVolumeStatusAsync(
                CreateClientContext(context, jobId),
                requestId,
                cancellationToken);

            if (!statusResult.IsSuccess || statusResult.Data is null)
            {
                return Result<SearchVolumePollOutcome>.Failure(ToExternalError(statusResult));
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
        }

        var totalCount = allExternalRequests.Length;
        var completedCount = allExternalRequests.Count(entity => entity.Status == StatusValues.Succeeded);

        var allCompleted = totalCount > 0 && completedCount == totalCount;
        var progress = allCompleted ? 75 : 50;
        var updatedAt = NowUtc();
        job.Status = StatusValues.WaitingExternal;
        job.Progress = progress;
        job.NextRunAt = allCompleted ? null : updatedAt + PollInterval;
        job.UpdatedAt = updatedAt;

        await UpdateSearchVolumeStatusAsync(
            jobId,
            new SearchVolumeStatusSnapshot(
                StatusValues.WaitingExternal,
                totalCount,
                completedCount,
                request.EstimatedCredit,
                allCompleted ? "All external search volume requests completed." : "External search volume requests are still running."),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        if (!allCompleted)
        {
            await jobScheduler.SchedulePollAsync(jobId, PollInterval, cancellationToken);
        }

        return Result<SearchVolumePollOutcome>.Success(new SearchVolumePollOutcome(allCompleted, IsCanceled: false));
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

        var job = await FindSearchVolumeJobAsync(context, jobId, asTracking: true, cancellationToken);
        if (job is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Search volume job was not found.");
        }

        var request = await ReadRequestOptionsAsync(jobId, cancellationToken);
        if (request is null)
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Search volume job payload was missing.");
        }

        if (await dbContext.SearchVolumeResults.AnyAsync(entity => entity.JobId == jobId, cancellationToken))
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

        var externalRequests = await dbContext.JobExternalRequests
            .Where(entity => entity.JobId == jobId && entity.Status == StatusValues.Succeeded)
            .OrderBy(entity => entity.SequenceNo)
            .ToArrayAsync(cancellationToken);

        foreach (var externalRequest in externalRequests)
        {
            if (!long.TryParse(externalRequest.ExternalRequestId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestId))
            {
                return Failure<JobReference>(ErrorCode.Conflict, "External request id was invalid.");
            }

            var chunkSize = request.ChunkSize(externalRequest.SequenceNo);
            var result = await rakkoKeywordClient.GetSearchVolumeResultsAsync(
                CreateClientContext(context, jobId),
                requestId,
                new RakkoSearchVolumeResultsRequest(Limit: chunkSize),
                cancellationToken);

            if (!result.IsSuccess || result.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(result));
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

            await SaveResultsAsync(context, jobId, request, result, cancellationToken);
            externalRequest.ConsumedCredit += result.ConsumedCredit;
            externalRequest.UpdatedAt = NowUtc();
            externalRequest.CompletedAt ??= externalRequest.UpdatedAt;
        }

        await UpdateSearchVolumeStatusAsync(
            jobId,
            new SearchVolumeStatusSnapshot(
                StatusValues.Succeeded,
                externalRequests.Length,
                externalRequests.Length,
                request.EstimatedCredit,
                "Search volume results were fetched and saved."),
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

    private async Task SaveResultsAsync(
        ProjectExecutionContext context,
        Guid jobId,
        NormalizedSearchVolumeJobRequest request,
        RakkoKeywordCallResult<RakkoSearchVolumeResults> result,
        CancellationToken cancellationToken)
    {
        var fetchedAt = NowUtc();
        foreach (var item in result.Data!.Items)
        {
            var keywordText = OptionalText(item.Keyword);
            if (keywordText is null)
            {
                continue;
            }

            var keyword = await EnsureKeywordAsync(keywordText, request.Language, cancellationToken);
            var metrics = new SearchVolumeMetricsSnapshot(
                ToNullableInt(item.Metrics.SearchVolume),
                item.Metrics.SeoDifficulty,
                item.Metrics.Cpc,
                item.Metrics.Competition,
                item.Metrics.FirstSeenRange);
            var trends = new SearchVolumeTrendsSnapshot(item.MonthlySearchVolume);

            dbContext.KeywordMetrics.Add(new KeywordMetricEntity
            {
                Id = UuidV7.New(),
                KeywordId = keyword.Id,
                Location = request.Location,
                Language = request.Language,
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                SourceCallId = result.ExternalCall.CallId,
                SearchVolume = metrics.SearchVolume ?? 0,
                SeoDifficulty = metrics.SeoDifficulty ?? 0,
                Cpc = metrics.Cpc ?? 0,
                Competition = metrics.Competition ?? 0,
                FirstSeenRange = metrics.FirstSeenRange,
                FetchedAt = fetchedAt
            });

            foreach (var (yearMonth, searchVolume) in item.MonthlySearchVolume)
            {
                if (!IsYearMonth(yearMonth))
                {
                    continue;
                }

                dbContext.KeywordMonthlyVolumes.Add(new KeywordMonthlyVolumeEntity
                {
                    Id = UuidV7.New(),
                    KeywordId = keyword.Id,
                    Location = request.Location,
                    Language = request.Language,
                    ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                    SourceCallId = result.ExternalCall.CallId,
                    YearMonth = yearMonth,
                    SearchVolume = searchVolume,
                    FetchedAt = fetchedAt
                });
            }

            dbContext.SearchVolumeResults.Add(new SearchVolumeResultEntity
            {
                Id = UuidV7.New(),
                JobId = jobId,
                KeywordId = keyword.Id,
                DataSource = OptionalText(item.DataSource) ?? "rakko_keyword",
                SourceCallId = result.ExternalCall.CallId,
                CacheHit = result.ExternalCall.CacheHit,
                MetricsSnapshotJson = JsonSerializer.Serialize(metrics, JsonOptions),
                TrendsJson = JsonSerializer.Serialize(trends, JsonOptions),
                CreatedAt = fetchedAt
            });
        }
    }

    private async Task<NormalizedSearchVolumeJobRequest?> ReadRequestOptionsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var json = await dbContext.SearchVolumeJobs
            .AsNoTracking()
            .Where(entity => entity.JobId == jobId)
            .Select(entity => entity.RequestOptionsJson)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<NormalizedSearchVolumeJobRequest>(json, JsonOptions);
    }

    private async Task UpdateSearchVolumeStatusAsync(
        Guid jobId,
        SearchVolumeStatusSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var searchVolumeJob = await dbContext.SearchVolumeJobs
            .SingleAsync(entity => entity.JobId == jobId, cancellationToken);
        searchVolumeJob.StatusJson = JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private async Task<NormalizeResult> NormalizeRequestAsync(
        ProjectExecutionContext context,
        SearchVolumeJobRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
            return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
        if (project is null)
        {
            return new NormalizeResult(null, new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var keywords = KeywordNormalizer.NormalizeMany(request.Keywords ?? []);
        if (keywords.Count == 0)
        {
            errors.Add("keywords", "keywords must contain at least one keyword.");
        }
        else if (keywords.Count > MaxKeywordCount)
        {
            errors.Add("keywords", $"keywords must contain {MaxKeywordCount.ToString(CultureInfo.InvariantCulture)} items or fewer after normalization.");
        }

        var location = OptionalText(request.Location) ?? project.DefaultLocation;
        var language = OptionalText(request.Language) ?? project.DefaultLanguage;
        if (string.IsNullOrWhiteSpace(location))
        {
            errors.Add("location", "location is required.");
        }
        else
        {
            location = await ResolveCanonicalLocationAsync(location, errors, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            errors.Add("language", "language is required.");
        }
        else
        {
            language = await ResolveCanonicalLanguageAsync(language, errors, cancellationToken);
        }

        if (!AllowedAggregationMonths.Contains(request.AggregationPeriodMonths))
        {
            errors.Add("aggregationPeriodMonths", "aggregationPeriodMonths must be 12, 24, 36, or 48.");
        }

        if (errors.HasErrors)
        {
            return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var normalized = new NormalizedSearchVolumeJobRequest(
            Version: 1,
            keywords,
            location,
            language,
            request.AggregationPeriodMonths,
            request.SeoDifficulty,
            ExternalRequestKeywordLimit,
            NormalizedKeywordCount: keywords.Count,
            EstimatedCredit: EstimateCredit(keywords.Count, request.SeoDifficulty),
            IdempotencyKey: string.Empty,
            RequestHash: string.Empty);
        var requestHash = HashText(JsonSerializer.Serialize(normalized with { IdempotencyKey = string.Empty, RequestHash = string.Empty }, JsonOptions));
        normalized = normalized with
        {
            IdempotencyKey = BuildIdempotencyKey(context.ProjectId.Value, requestHash),
            RequestHash = requestHash
        };

        return new NormalizeResult(normalized, null);
    }

    private static decimal EstimateCredit(int keywordCount, bool seoDifficulty)
    {
        var creditPerKeyword = CreditPerKeyword + (seoDifficulty ? SeoDifficultyCreditPerKeyword : 0m);
        var estimated = 0m;
        for (var remaining = keywordCount; remaining > 0; remaining -= ExternalRequestKeywordLimit)
        {
            var batchSize = Math.Min(remaining, ExternalRequestKeywordLimit);
            estimated += Math.Max(MinimumCreditPerExternalRequest, batchSize * creditPerKeyword);
        }

        return estimated;
    }

    // ラッコキーワードAPI v1.12.0のPOST /v1/search-volumeは、location/languageに
    // metadata一覧(同期済みマスタ)の名前を要求する。provider行が1件もない未同期時だけ検証を省略し、
    // 旧コード値は変換先の名前がactiveな場合に限って自動正規化する。
    private async Task<string> ResolveCanonicalLocationAsync(
        string location,
        ValidationErrors errors,
        CancellationToken cancellationToken)
    {
        return await ResolveCanonicalNameAsync(
            MasterNameQuery.ForLocations(dbContext),
            location,
            "location",
            compatibilityAlias: "JP",
            compatibilityName: "Japan",
            errors,
            cancellationToken);
    }

    private async Task<string> ResolveCanonicalLanguageAsync(
        string language,
        ValidationErrors errors,
        CancellationToken cancellationToken)
    {
        return await ResolveCanonicalNameAsync(
            MasterNameQuery.ForLanguages(dbContext),
            language,
            "language",
            compatibilityAlias: "ja",
            compatibilityName: "Japanese",
            errors,
            cancellationToken);
    }

    private static async Task<string> ResolveCanonicalNameAsync(
        IQueryable<MasterNameEntry> entries,
        string value,
        string field,
        string compatibilityAlias,
        string compatibilityName,
        ValidationErrors errors,
        CancellationToken cancellationToken)
    {
        var normalizedValue = value.ToUpperInvariant();
        var canonical = await MasterNameQuery
            .ActiveNamesMatching(entries, normalizedValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (canonical is not null)
        {
            return canonical;
        }

        if (string.Equals(value, compatibilityAlias, StringComparison.OrdinalIgnoreCase))
        {
            var aliasTarget = compatibilityName.ToUpperInvariant();
            var canonicalAlias = await MasterNameQuery
                .ActiveNamesMatching(entries, aliasTarget)
                .FirstOrDefaultAsync(cancellationToken);
            if (canonicalAlias is not null)
            {
                return canonicalAlias;
            }
        }

        var legacyName = await MasterNameQuery
            .NamesForLegacyCode(entries, normalizedValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (legacyName is not null)
        {
            var normalizedLegacyName = legacyName.ToUpperInvariant();
            var activeLegacyTarget = await MasterNameQuery
                .ActiveNamesMatching(entries, normalizedLegacyName)
                .FirstOrDefaultAsync(cancellationToken);
            if (activeLegacyTarget is not null)
            {
                return activeLegacyTarget;
            }

            errors.Add(field, $"{field} legacy code does not map to an active synchronized master entry.");
            return value;
        }

        if (!await entries.AnyAsync(cancellationToken))
        {
            return value;
        }

        errors.Add(field, $"{field} must be a name from the synchronized {field} master data.");
        return value;
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

    private async Task<JobEntity?> FindSearchVolumeJobAsync(
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

    private static Error ToExternalError<T>(RakkoKeywordCallResult<T> result)
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
            result.Errors.FirstOrDefault() ?? "Search volume external API call failed.",
            new Dictionary<string, string[]>
            {
                ["statusCode"] = [result.StatusCode.ToString(CultureInfo.InvariantCulture)],
                ["errorCode"] = [result.ExternalCall.ErrorCode ?? string.Empty]
            });
    }

    private static SearchVolumeResultRow MapResultRow(SearchVolumeResultProjection projection)
    {
        var metrics = DeserializeOrDefault<SearchVolumeMetricsSnapshot>(projection.Result.MetricsSnapshotJson)
            ?? new SearchVolumeMetricsSnapshot(null, null, null, null, null);
        var trends = DeserializeOrDefault<SearchVolumeTrendsSnapshot>(projection.Result.TrendsJson)
            ?? new SearchVolumeTrendsSnapshot(new Dictionary<string, int>(StringComparer.Ordinal));

        return new SearchVolumeResultRow(
            projection.Keyword.NormalizedText,
            metrics.SearchVolume,
            metrics.SeoDifficulty,
            metrics.Cpc,
            metrics.Competition,
            trends.MonthlySearchVolume,
            projection.Result.DataSource,
            projection.Result.CacheHit,
            projection.Keyword.Id);
    }

    private static IEnumerable<SearchVolumeResultRow> SortResults(
        IEnumerable<SearchVolumeResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "keyword";
        var ascending = sort?.Direction == SortDirection.Asc;

        return sortBy switch
        {
            "searchVolume" => SortNullableInt(rows, ascending, row => row.SearchVolume),
            "seoDifficulty" => SortNullableDecimal(rows, ascending, row => row.SeoDifficulty),
            "cpc" => SortNullableDecimal(rows, ascending, row => row.Cpc),
            "competition" => SortNullableDecimal(rows, ascending, row => row.Competition),
            _ => ascending
                ? rows.OrderBy(row => row.Keyword, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(row => row.Keyword, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<SearchVolumeResultRow> SortNullableInt(
        IEnumerable<SearchVolumeResultRow> rows,
        bool ascending,
        Func<SearchVolumeResultRow, int?> selector)
        => ascending
            ? rows.OrderBy(row => selector(row) ?? int.MaxValue)
            : rows.OrderByDescending(row => selector(row) ?? int.MinValue);

    private static IOrderedEnumerable<SearchVolumeResultRow> SortNullableDecimal(
        IEnumerable<SearchVolumeResultRow> rows,
        bool ascending,
        Func<SearchVolumeResultRow, decimal?> selector)
        => ascending
            ? rows.OrderBy(row => selector(row) ?? decimal.MaxValue)
            : rows.OrderByDescending(row => selector(row) ?? decimal.MinValue);

    private static T? DeserializeOrDefault<T>(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static int? ToNullableInt(decimal? value)
        => value.HasValue
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : null;

    private static bool IsYearMonth(string value)
        => value.Length == 7 &&
            value[4] == '-' &&
            int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out _) &&
            int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
            month is >= 1 and <= 12;

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string BuildIdempotencyKey(Guid projectId, string requestHash)
        => $"search-volume:{projectId:N}:{requestHash}";

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

    private sealed record NormalizeResult(NormalizedSearchVolumeJobRequest? Request, Error? Error);

    private sealed record SearchVolumeKeywordChunk(int SequenceNo, IReadOnlyList<string> Keywords);

    private sealed record SearchVolumeResultProjection(SearchVolumeResultEntity Result, KeywordEntity Keyword);
}

internal sealed record SearchVolumePollOutcome(bool IsCompleted, bool IsCanceled);

internal sealed record NormalizedSearchVolumeJobRequest(
    int Version,
    IReadOnlyList<string> Keywords,
    string Location,
    string Language,
    int AggregationPeriodMonths,
    bool SeoDifficulty,
    int ExternalRequestKeywordLimit,
    int NormalizedKeywordCount,
    decimal EstimatedCredit,
    string IdempotencyKey,
    string RequestHash)
{
    public NormalizedSearchVolumeJobRequest ToOptions()
        => this;

    public int ChunkSize(int sequenceNo)
    {
        var remaining = NormalizedKeywordCount - ((sequenceNo - 1) * ExternalRequestKeywordLimit);
        return Math.Clamp(remaining, 1, ExternalRequestKeywordLimit);
    }
}

internal sealed record SearchVolumeStatusSnapshot(
    string Status,
    int ExternalRequestCount,
    int CompletedExternalRequestCount,
    decimal EstimatedCredit,
    string? Message);

/// <summary>
/// Projection over the location/language master tables. The members are assigned through an object
/// initializer rather than a positional constructor: EF Core can see through a member-init
/// projection when a later Where filters on it, but not through a constructor call, which made the
/// canonical-name lookups fail to translate.
/// </summary>
internal sealed record MasterNameEntry
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Builds the master-name queries used to canonicalize a requested location or language. They live
/// here so the composed query shapes stay translatable and can be pinned by a contract test.
/// </summary>
internal static class MasterNameQuery
{
    public static IQueryable<MasterNameEntry> ForLocations(SeoIntelligenceDbContext dbContext)
        => dbContext.Locations
            .AsNoTracking()
            .Where(entity => entity.Provider == SeoIntelligenceSeedData.RakkoKeywordProvider)
            .Select(entity => new MasterNameEntry
            {
                Code = entity.LocationCode,
                Name = entity.LocationName,
                Status = entity.Status
            });

    public static IQueryable<MasterNameEntry> ForLanguages(SeoIntelligenceDbContext dbContext)
        => dbContext.Languages
            .AsNoTracking()
            .Where(entity => entity.Provider == SeoIntelligenceSeedData.RakkoKeywordProvider)
            .Select(entity => new MasterNameEntry
            {
                Code = entity.LanguageCode,
                Name = entity.LanguageName,
                Status = entity.Status
            });

    public static IQueryable<string> ActiveNamesMatching(
        IQueryable<MasterNameEntry> entries,
        string normalizedName)
        => entries
            .Where(entry =>
                entry.Status == StatusValues.Active &&
                entry.Name.ToUpper() == normalizedName)
            .Select(entry => entry.Name);

    public static IQueryable<string> NamesForLegacyCode(
        IQueryable<MasterNameEntry> entries,
        string normalizedCode)
        => entries
            .Where(entry =>
                entry.Code.ToUpper() == normalizedCode &&
                entry.Code.ToUpper() != entry.Name.ToUpper())
            .Select(entry => entry.Name);
}

internal sealed record SearchVolumeMetricsSnapshot(
    int? SearchVolume,
    decimal? SeoDifficulty,
    decimal? Cpc,
    decimal? Competition,
    string? FirstSeenRange);

internal sealed record SearchVolumeTrendsSnapshot(IReadOnlyDictionary<string, int> MonthlySearchVolume);

internal interface ISearchVolumeJobScheduler
{
    Task SchedulePollAsync(Guid jobId, TimeSpan delay, CancellationToken cancellationToken = default);

    Task EnqueueFetchAsync(Guid jobId, CancellationToken cancellationToken = default);
}

internal sealed class SearchVolumeHangfireJobScheduler(
    IServiceProvider serviceProvider,
    ILogger<SearchVolumeHangfireJobScheduler> logger)
    : ISearchVolumeJobScheduler
{
    public Task SchedulePollAsync(Guid jobId, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = serviceProvider.GetService<IBackgroundJobClient>();
        if (client is null)
        {
            logger.LogDebug("Hangfire is not configured; search volume poll for job {job_id} was not scheduled.", jobId);
            return Task.CompletedTask;
        }

        client.Schedule<PollSearchVolumeStatusJob>(job => job.ExecuteAsync(jobId), delay);
        return Task.CompletedTask;
    }

    public Task EnqueueFetchAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = serviceProvider.GetService<IBackgroundJobClient>();
        if (client is null)
        {
            logger.LogDebug("Hangfire is not configured; search volume fetch for job {job_id} was not enqueued.", jobId);
            return Task.CompletedTask;
        }

        client.Enqueue<FetchSearchVolumeResultsJob>(job => job.ExecuteAsync(jobId));
        return Task.CompletedTask;
    }
}

internal sealed class RegisterSearchVolumeJob(
    SeoIntelligenceDbContext dbContext,
    SearchVolumeService searchVolumeService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<RegisterSearchVolumeJob> logger)
{
    public const string JobType = SearchVolumeService.RegisterJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Search volume registration job {job_id} was not found.", jobId);
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
                "Search volume registration job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await searchVolumeService.RegisterExternalRequestsAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Search volume registration job {job_id} could not persist state.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Search volume registration could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Search volume registration job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Search volume registration failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        var errorCode = TryReadDetail(error, "errorCode");

        // 契約違反レスポンス(invalid_response等)はHTTPステータスが5xxでも再試行しない。
        // 再試行しても解消せず、登録経路では課金される呼び出しを繰り返すだけになる。
        if (error.Code is ErrorCode.ExternalFatalFailure)
        {
            return JobFailure.ExternalFatal(statusCode, errorCode, error.Message);
        }

        return JobFailure.FromHttpStatusCode(statusCode, errorCode, error.Message);
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

internal sealed class PollSearchVolumeStatusJob(
    SeoIntelligenceDbContext dbContext,
    SearchVolumeService searchVolumeService,
    FetchSearchVolumeResultsJob fetchSearchVolumeResultsJob,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<PollSearchVolumeStatusJob> logger)
{
    public const string JobType = "PollSearchVolumeStatusJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Search volume polling job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        try
        {
            var poll = await searchVolumeService.PollStatusAsync(context, jobId);
            if (!poll.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(poll.Error!));
                return;
            }

            if (poll.Value!.IsCompleted)
            {
                await fetchSearchVolumeResultsJob.ExecuteAsync(jobId);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Search volume polling job {job_id} could not persist state.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Search volume polling could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Search volume polling job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Search volume polling failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        var errorCode = TryReadDetail(error, "errorCode");

        // 契約違反レスポンス(invalid_response等)はHTTPステータスが5xxでも再試行しない。
        // 再試行しても解消せず、登録経路では課金される呼び出しを繰り返すだけになる。
        if (error.Code is ErrorCode.ExternalFatalFailure)
        {
            return JobFailure.ExternalFatal(statusCode, errorCode, error.Message);
        }

        return JobFailure.FromHttpStatusCode(statusCode, errorCode, error.Message);
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

internal sealed class FetchSearchVolumeResultsJob(
    SeoIntelligenceDbContext dbContext,
    SearchVolumeService searchVolumeService,
    OpportunityScoringJob opportunityScoringJob,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<FetchSearchVolumeResultsJob> logger)
{
    public const string JobType = "FetchSearchVolumeResultsJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Search volume result fetch job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        try
        {
            var result = await searchVolumeService.FetchResultsAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
                return;
            }

            if (string.Equals(result.Value!.Status, StatusValues.Succeeded, StringComparison.Ordinal))
            {
                await RegisterAndRunOpportunityScoringAsync(context, jobId);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Search volume result fetch job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Search volume results could not be persisted."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Search volume result fetch job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Search volume result fetch failed unexpectedly."));
        }
    }

    private async Task RegisterAndRunOpportunityScoringAsync(ProjectExecutionContext context, Guid searchVolumeJobId)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            version = 1,
            sourceSearchVolumeJobId = searchVolumeJobId
        });
        var scoringJob = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                OpportunityScoringJob.JobType,
                payload,
                IdempotencyKey: $"opportunity-scoring:{searchVolumeJobId:N}",
                TargetKey: searchVolumeJobId.ToString("N"),
                Queue: "analysis",
                InitialResource: new JobResultResource(SearchVolumeService.ResultResourceType, searchVolumeJobId)));
        if (!scoringJob.IsSuccess)
        {
            logger.LogWarning(
                "Opportunity scoring job for search volume job {job_id} could not be registered: {message}",
                searchVolumeJobId,
                scoringJob.Error?.Message);
            return;
        }

        if (string.Equals(scoringJob.Value!.Status, StatusValues.Queued, StringComparison.Ordinal))
        {
            await opportunityScoringJob.ExecuteAsync(scoringJob.Value.JobId);
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        var errorCode = TryReadDetail(error, "errorCode");

        // 契約違反レスポンス(invalid_response等)はHTTPステータスが5xxでも再試行しない。
        // 再試行しても解消せず、登録経路では課金される呼び出しを繰り返すだけになる。
        if (error.Code is ErrorCode.ExternalFatalFailure)
        {
            return JobFailure.ExternalFatal(statusCode, errorCode, error.Message);
        }

        return JobFailure.FromHttpStatusCode(statusCode, errorCode, error.Message);
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
