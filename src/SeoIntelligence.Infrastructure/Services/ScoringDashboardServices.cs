using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class ScoringService(
    SeoIntelligenceDbContext dbContext,
    TimeProvider timeProvider)
    : IScoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<OpportunityScoreResult>> CalculateOpportunityScoresAsync(
        ProjectExecutionContext context,
        OpportunityScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue)
        {
            return Failure<OpportunityScoreResult>(ErrorCode.ValidationFailed, "projectId is required.");
        }

        var location = OptionalText(request.Location);
        var language = OptionalText(request.Language);
        if (location is null || language is null)
        {
            return Result<OpportunityScoreResult>.Failure(Error.Validation(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    ["location"] = location is null ? ["location is required."] : [],
                    ["language"] = language is null ? ["language is required."] : []
                }.Where(pair => pair.Value.Length > 0).ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        var projectId = context.ProjectId.Value;
        if (!await ProjectExistsAsync(context, projectId, cancellationToken))
        {
            return Failure<OpportunityScoreResult>(ErrorCode.NotFound, "Project was not found.");
        }

        var keywordIds = request.KeywordIds
            .Where(keywordId => keywordId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (keywordIds.Length == 0)
        {
            return Result<OpportunityScoreResult>.Success(new OpportunityScoreResult([]));
        }

        var metrics = await dbContext.KeywordMetrics
            .AsNoTracking()
            .Where(entity =>
                keywordIds.Contains(entity.KeywordId) &&
                entity.Location == location &&
                entity.Language == language)
            .ToArrayAsync(cancellationToken);
        var latestMetrics = metrics
            .GroupBy(entity => entity.KeywordId)
            .Select(group => group
                .OrderByDescending(entity => entity.FetchedAt)
                .ThenByDescending(entity => entity.Id)
                .First())
            .ToArray();
        if (latestMetrics.Length == 0)
        {
            return Result<OpportunityScoreResult>.Success(new OpportunityScoreResult([]));
        }

        var metricKeywordIds = latestMetrics.Select(entity => entity.KeywordId).ToArray();
        var monthlyVolumes = await dbContext.KeywordMonthlyVolumes
            .AsNoTracking()
            .Where(entity =>
                metricKeywordIds.Contains(entity.KeywordId) &&
                entity.Location == location &&
                entity.Language == language)
            .ToArrayAsync(cancellationToken);
        var monthlyByKeyword = monthlyVolumes
            .GroupBy(entity => entity.KeywordId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)group
                    .GroupBy(entity => entity.YearMonth)
                    .ToDictionary(
                        monthGroup => monthGroup.Key,
                        monthGroup => monthGroup
                            .OrderByDescending(entity => entity.FetchedAt)
                            .First()
                            .SearchVolume,
                        StringComparer.Ordinal));

        var relevanceByKeyword = await dbContext.RankingKeywords
            .AsNoTracking()
            .Where(entity => metricKeywordIds.Contains(entity.KeywordId))
            .GroupBy(entity => entity.KeywordId)
            .Select(group => new
            {
                KeywordId = group.Key,
                Relevance = group.Max(entity => entity.Relevance)
            })
            .ToDictionaryAsync(entity => entity.KeywordId, entity => (decimal?)entity.Relevance, cancellationToken);

        var inputs = latestMetrics
            .Select(metric => new OpportunityScoreCalculationInput(
                metric.KeywordId,
                metric.SearchVolume,
                metric.SeoDifficulty,
                metric.Cpc,
                metric.Competition,
                monthlyByKeyword.GetValueOrDefault(metric.KeywordId),
                relevanceByKeyword.GetValueOrDefault(metric.KeywordId),
                metric.SourceCallId,
                metric.Id))
            .ToArray();
        var calculated = OpportunityScoreCalculator.Calculate(inputs);

        var existingScores = await dbContext.ProjectKeywordScores
            .Where(entity =>
                entity.ProjectId == projectId &&
                metricKeywordIds.Contains(entity.KeywordId) &&
                entity.Location == location &&
                entity.Language == language)
            .ToDictionaryAsync(entity => entity.KeywordId, cancellationToken);
        var scoredAt = NowUtc();
        foreach (var score in calculated)
        {
            if (!existingScores.TryGetValue(score.KeywordId, out var entity))
            {
                entity = new ProjectKeywordScoreEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    KeywordId = score.KeywordId,
                    Location = location,
                    Language = language
                };
                dbContext.ProjectKeywordScores.Add(entity);
            }

            entity.SourceCallId = score.Components.SourceCallId;
            entity.OpportunityScore = score.OpportunityScore;
            entity.ScoreComponentsJson = JsonSerializer.Serialize(ToComponentDocument(score.Components), JsonOptions);
            entity.ScoredAt = scoredAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OpportunityScoreResult>.Success(new OpportunityScoreResult(
            calculated.Select(score => new OpportunityScoreRow(
                score.KeywordId,
                score.OpportunityScore,
                ToDecimalDictionary(score.Components))).ToArray()));
    }

    private async Task<bool> ProjectExistsAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken)
        => await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == projectId &&
                    entity.Status == StatusValues.Active,
                cancellationToken);

    private static IReadOnlyDictionary<string, decimal> ToDecimalDictionary(OpportunityScoreComponents components)
        => new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["volumeScore"] = components.VolumeScore,
            ["difficultyScore"] = components.DifficultyScore,
            ["trendScore"] = components.TrendScore,
            ["commercialScore"] = components.CommercialScore,
            ["relevanceScore"] = components.RelevanceScore,
            ["normalizedCpc"] = components.NormalizedCpc,
            ["normalizedCompetition"] = components.NormalizedCompetition,
            ["changeRate3m"] = components.ChangeRate3m ?? 0m,
            ["searchVolume"] = components.SearchVolume,
            ["maxVolume"] = components.MaxVolume,
            ["seoDifficulty"] = components.SeoDifficulty,
            ["cpc"] = components.Cpc ?? 0m,
            ["competition"] = components.Competition ?? 0m
        };

    private static object ToComponentDocument(OpportunityScoreComponents components)
        => new
        {
            components.VolumeScore,
            components.DifficultyScore,
            components.TrendScore,
            components.CommercialScore,
            components.RelevanceScore,
            components.NormalizedCpc,
            components.NormalizedCompetition,
            components.ChangeRate3m,
            components.SearchVolume,
            components.MaxVolume,
            components.SeoDifficulty,
            components.Cpc,
            components.Competition,
            components.SourceCallId,
            components.MetricId
        };

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));
}

internal sealed class DashboardService(SeoIntelligenceDbContext dbContext)
    : IDashboardService
{
    private static readonly string[] ActiveJobStatuses =
    [
        StatusValues.Queued,
        StatusValues.Running,
        StatusValues.WaitingExternal
    ];

    private static readonly string[] FailedJobStatuses =
    [
        StatusValues.FailedRetryable,
        StatusValues.FailedFatal
    ];

    private static readonly string[] FailedNotificationStatuses =
    [
        StatusValues.Failed,
        StatusValues.Retrying
    ];

    public async Task<Result<DashboardSnapshot>> GetDashboardAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue)
        {
            return Failure<DashboardSnapshot>(ErrorCode.ValidationFailed, "projectId is required.");
        }

        var projectId = context.ProjectId.Value;
        if (!await dbContext.Projects.AsNoTracking().AnyAsync(
            entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.Id == projectId &&
                entity.Status == StatusValues.Active,
            cancellationToken))
        {
            return Failure<DashboardSnapshot>(ErrorCode.NotFound, "Project was not found.");
        }

        var keywordDiscoveryCount = await dbContext.KeywordSeeds
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var keywordSuggestionCount = await dbContext.KeywordSuggestions
            .AsNoTracking()
            .Join(
                dbContext.KeywordSeeds.AsNoTracking().Where(seed => seed.ProjectId == projectId),
                suggestion => suggestion.SeedId,
                seed => seed.Id,
                (suggestion, _) => suggestion.Id)
            .CountAsync(cancellationToken);
        var relatedKeywordCount = await dbContext.RelatedKeywords
            .AsNoTracking()
            .Join(
                dbContext.KeywordSeeds.AsNoTracking().Where(seed => seed.ProjectId == projectId),
                related => related.SeedId,
                seed => seed.Id,
                (related, _) => related.Id)
            .CountAsync(cancellationToken);
        var questionCount = await dbContext.Questions
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var keywordCandidateCount = keywordSuggestionCount + relatedKeywordCount + questionCount;
        var searchVolumeJobCount = await dbContext.Jobs
            .AsNoTracking()
            .CountAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == projectId &&
                    entity.JobType == SearchVolumeService.RegisterJobType,
                cancellationToken);
        var searchVolumeResultCount = await dbContext.SearchVolumeResults
            .AsNoTracking()
            .Join(
                dbContext.Jobs.AsNoTracking().Where(job =>
                    job.WorkspaceId == context.WorkspaceId &&
                    job.ProjectId == projectId &&
                    job.JobType == SearchVolumeService.RegisterJobType),
                result => result.JobId,
                job => job.Id,
                (result, _) => result.Id)
            .CountAsync(cancellationToken);
        var runningJobCount = await dbContext.Jobs
            .AsNoTracking()
            .CountAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == projectId &&
                    ActiveJobStatuses.Contains(entity.Status),
                cancellationToken);
        var failedJobCount = await dbContext.Jobs
            .AsNoTracking()
            .CountAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == projectId &&
                    FailedJobStatuses.Contains(entity.Status),
                cancellationToken);
        var consumedCredit = await dbContext.ExternalApiCalls
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.ProjectId == projectId)
            .SumAsync(entity => entity.ConsumedCredit, cancellationToken);
        var notificationFailureCount = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .CountAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == projectId &&
                    FailedNotificationStatuses.Contains(entity.Status),
                cancellationToken);
        var opportunityScoreCount = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var topScoreQuery = dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.OpportunityScore)
            .ThenByDescending(entity => entity.ScoredAt)
            .Take(10);
        var topOpportunityScores = await topScoreQuery
            .Join(
                dbContext.Keywords.AsNoTracking(),
                score => score.KeywordId,
                keyword => keyword.Id,
                (score, keyword) => new DashboardOpportunityScoreRow(
                    score.KeywordId,
                    keyword.NormalizedText,
                    score.OpportunityScore,
                    score.Location,
                    score.Language,
                    score.ScoredAt))
            .OrderByDescending(row => row.OpportunityScore)
            .ThenByDescending(row => row.ScoredAt)
            .ToArrayAsync(cancellationToken);

        return Result<DashboardSnapshot>.Success(new DashboardSnapshot(
            keywordCandidateCount,
            runningJobCount,
            failedJobCount,
            Decimal.ToInt32(decimal.Truncate(consumedCredit)),
            keywordDiscoveryCount,
            searchVolumeJobCount,
            searchVolumeResultCount,
            opportunityScoreCount,
            topOpportunityScores,
            notificationFailureCount));
    }

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));
}

internal sealed class OpportunityScoringJob(
    SeoIntelligenceDbContext dbContext,
    IScoringService scoringService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<OpportunityScoringJob> logger)
{
    public const string JobType = "OpportunityScoringJob";
    public const string ResultResourceType = "project_keyword_scores";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Opportunity scoring job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, SearchVolumeService.ResultResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Opportunity scoring job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Opportunity scoring job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var searchVolumeJob = await dbContext.SearchVolumeJobs
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.JobId == job.ResultResourceId.Value);
            if (searchVolumeJob is null)
            {
                await jobService.RecordFailureAsync(
                    context,
                    jobId,
                    new JobFailure(JobFailureKind.Unexpected, null, "search_volume_job_not_found", "Source search volume job was not found."));
                return;
            }

            var keywordIds = await dbContext.SearchVolumeResults
                .AsNoTracking()
                .Where(entity => entity.JobId == job.ResultResourceId.Value)
                .Select(entity => entity.KeywordId)
                .Distinct()
                .ToArrayAsync();
            var result = await scoringService.CalculateOpportunityScoresAsync(
                context,
                new OpportunityScoreRequest(keywordIds, searchVolumeJob.Location, searchVolumeJob.Language));
            if (!result.IsSuccess)
            {
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
                return;
            }

            await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(
                    Progress: 100,
                    new JobResultResource(ResultResourceType, job.ResultResourceId.Value)));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Opportunity scoring job {job_id} could not persist scores.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Opportunity scoring could not persist scores."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Opportunity scoring job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Opportunity scoring failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = error.Code switch
        {
            ErrorCode.NotFound => 404,
            ErrorCode.Conflict => 409,
            ErrorCode.ValidationFailed => 400,
            _ => 500
        };
        return JobFailure.FromHttpStatusCode(statusCode, error.Code.ToString(), error.Message);
    }
}
