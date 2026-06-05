using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class RewriteManagementService(
    SeoIntelligenceDbContext dbContext,
    IJobService jobService,
    TimeProvider timeProvider)
    : IRewriteManagementService
{
    public const string RewriteScoringJobType = "RewriteScoringJob";
    public const string CannibalizationDetectionJobType = "CannibalizationDetectionJob";
    public const string RewriteScoringResourceType = "rewrite_scoring";
    public const string CannibalizationDetectionResourceType = "cannibalization_detection";

    private const int RewriteCandidatePositionLimit = 50;
    private const int ReasonKeywordLimit = 8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ContentStatuses =
    [
        "draft",
        StatusValues.Active,
        StatusValues.Archived,
        "completed"
    ];

    public async Task<Result<PagedResult<RewriteTaskDetails>>> SearchRewriteTasksAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<RewriteTaskDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var entities = await dbContext.RewriteTasks
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        IEnumerable<RewriteTaskDetails> rows = entities.Select(MapRewriteTask);

        var status = OptionalText(query.Status) ?? StatusValues.Active;
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(row => string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.TargetUrl.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.AssigneeActor.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (row.Memo?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                row.Reason.GetRawText().Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortRewriteTasks(rows, query.Sort).ToArray();
        return Result<PagedResult<RewriteTaskDetails>>.Success(ToPagedResult(rows, query));
    }

    public async Task<Result<RewriteTaskDetails>> GetRewriteTaskAsync(
        ProjectExecutionContext context,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindRewriteTaskAsync(context, taskId, asTracking: false, cancellationToken);
        return task is null
            ? Failure<RewriteTaskDetails>(ErrorCode.NotFound, "Rewrite task was not found.")
            : Result<RewriteTaskDetails>.Success(MapRewriteTask(task));
    }

    public async Task<Result<RewriteTaskDetails>> UpdateRewriteTaskAsync(
        ProjectExecutionContext context,
        Guid taskId,
        RewriteTaskUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await FindRewriteTaskAsync(context, taskId, asTracking: true, cancellationToken);
        if (task is null)
        {
            return Failure<RewriteTaskDetails>(ErrorCode.NotFound, "Rewrite task was not found.");
        }

        var errors = new ValidationErrors();
        if (request.Status is not null)
        {
            var status = OptionalText(request.Status)?.ToLowerInvariant();
            if (status is null || !ContentStatuses.Contains(status))
            {
                errors.Add("status", "status must be draft, active, archived, or completed.");
            }
            else
            {
                task.Status = status;
            }
        }

        if (request.PriorityScore.HasValue)
        {
            if (request.PriorityScore.Value is < 0m or > 100m)
            {
                errors.Add("priorityScore", "priorityScore must be between 0 and 100.");
            }
            else
            {
                task.PriorityScore = decimal.Round(request.PriorityScore.Value, 4);
            }
        }

        if (request.AssigneeActor is not null)
        {
            var assigneeActor = OptionalText(request.AssigneeActor);
            if (assigneeActor is null)
            {
                errors.Add("assigneeActor", "assigneeActor must not be empty when provided.");
            }
            else if (assigneeActor.Length > 100)
            {
                errors.Add("assigneeActor", "assigneeActor must be 100 characters or fewer.");
            }
            else
            {
                task.AssigneeActor = assigneeActor;
            }
        }

        if (request.Memo is not null)
        {
            if (request.Memo.Length > 2000)
            {
                errors.Add("memo", "memo must be 2000 characters or fewer.");
            }
            else
            {
                task.Memo = OptionalText(request.Memo);
            }
        }

        if (errors.HasErrors)
        {
            return Result<RewriteTaskDetails>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        task.UpdatedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RewriteTaskDetails>.Success(MapRewriteTask(task));
    }

    public async Task<Result<PagedResult<CannibalizationCandidateDetails>>> SearchCannibalizationCandidatesAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<CannibalizationCandidateDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var entities = await dbContext.CannibalizationCandidates
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var keywordIds = entities.Select(entity => entity.KeywordId).Distinct().ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        IEnumerable<CannibalizationCandidateDetails> rows = entities.Select(entity => MapCannibalizationCandidate(entity, keywords));

        var status = OptionalText(query.Status) ?? StatusValues.Active;
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(row => string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.PrimaryUrl.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.CompetingUrls.GetRawText().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.Evidence.GetRawText().Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortCannibalizationCandidates(rows, query.Sort).ToArray();
        return Result<PagedResult<CannibalizationCandidateDetails>>.Success(ToPagedResult(rows, query));
    }

    public async Task<Result<JobReference>> RefreshCannibalizationAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        var payload = JsonSerializer.SerializeToElement(
            new CannibalizationRefreshJobRequest(Version: 1, project.Id),
            JsonOptions);
        var evidenceSignature = await BuildRankEvidenceSignatureAsync(project.Id, cancellationToken);
        var requestHash = HashText($"{project.Id:N}\n{payload.GetRawText()}\n{evidenceSignature}");
        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                CannibalizationDetectionJobType,
                payload,
                RequestHash: requestHash,
                IdempotencyKey: $"cannibalization-refresh:{project.Id:N}:{requestHash}",
                TargetKey: project.Id.ToString("N"),
                Queue: "analysis",
                InitialResource: new JobResultResource(CannibalizationDetectionResourceType, project.Id)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<int>> ExecuteCannibalizationDetectionAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue || context.ProjectId.Value != projectId)
        {
            return Failure<int>(ErrorCode.NotFound, "Project was not found.");
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<int>(ErrorCode.NotFound, "Project was not found.");
        }

        var rankResults = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var latestRankings = rankResults
            .Where(entity => OptionalText(entity.RankedUrl) is not null && entity.Position > 0)
            .GroupBy(entity => new RankUrlKey(entity.KeywordId, NormalizeUrlKey(entity.RankedUrl)), RankUrlKeyComparer.Instance)
            .Select(group => group
                .OrderByDescending(entity => entity.CheckedAt)
                .ThenBy(entity => entity.Position)
                .First())
            .GroupBy(entity => entity.KeywordId)
            .Where(group => group.Select(entity => NormalizeUrlKey(entity.RankedUrl)).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2)
            .ToArray();

        var keywordIds = latestRankings.Select(group => group.Key).ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        var existingCandidates = await dbContext.CannibalizationCandidates
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var detectedKeywordIds = new HashSet<Guid>();
        var savedCount = 0;
        var now = NowUtc();

        foreach (var group in latestRankings)
        {
            var rankings = group
                .OrderBy(entity => entity.Position)
                .ThenByDescending(entity => entity.EstimatedTraffic)
                .ToArray();
            var primary = rankings[0];
            var competing = rankings.Skip(1).ToArray();
            var keyword = keywords.GetValueOrDefault(group.Key) ?? group.Key.ToString("D");
            var severity = CalculateCannibalizationSeverity(rankings);
            var evidenceJson = SerializeCannibalizationEvidence(keyword, rankings, rankResults);
            var recommendationJson = SerializeCannibalizationRecommendation(primary.RankedUrl, competing, severity);

            var candidate = existingCandidates
                .Where(entity => entity.KeywordId == group.Key)
                .OrderByDescending(entity => entity.DetectedAt)
                .FirstOrDefault();
            if (candidate is null)
            {
                candidate = new CannibalizationCandidateEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = project.Id,
                    KeywordId = group.Key
                };
                dbContext.CannibalizationCandidates.Add(candidate);
            }

            candidate.PrimaryUrl = primary.RankedUrl;
            candidate.CompetingUrlsJson = JsonSerializer.Serialize(
                competing.Select(MapCompetingUrlEvidence).ToArray(),
                JsonOptions);
            candidate.SeverityScore = severity;
            candidate.EvidenceJson = evidenceJson;
            candidate.RecommendationJson = recommendationJson;
            candidate.Status = StatusValues.Active;
            candidate.DetectedAt = now;
            detectedKeywordIds.Add(group.Key);
            savedCount++;
        }

        foreach (var stale in existingCandidates.Where(entity =>
            entity.Status == StatusValues.Active &&
            !detectedKeywordIds.Contains(entity.KeywordId)))
        {
            stale.Status = StatusValues.Archived;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(savedCount);
    }

    public async Task<Result<int>> ExecuteRewriteScoringAsync(
        ProjectExecutionContext context,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue || context.ProjectId.Value != projectId)
        {
            return Failure<int>(ErrorCode.NotFound, "Project was not found.");
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<int>(ErrorCode.NotFound, "Project was not found.");
        }

        var rankResults = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var latestRankings = rankResults
            .Where(entity =>
                entity.Position is >= 4 and <= RewriteCandidatePositionLimit &&
                OptionalText(entity.RankedUrl) is not null)
            .GroupBy(entity => new RankUrlKey(entity.KeywordId, NormalizeUrlKey(entity.RankedUrl)), RankUrlKeyComparer.Instance)
            .Select(group => group
                .OrderByDescending(entity => entity.CheckedAt)
                .ThenBy(entity => entity.Position)
                .First())
            .ToArray();
        if (latestRankings.Length == 0)
        {
            return Result<int>.Success(0);
        }

        var keywordIds = latestRankings.Select(entity => entity.KeywordId).Distinct().ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        var metrics = await LoadLatestMetricsAsync(keywordIds, project, cancellationToken);
        var projectScores = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);
        var maxOpportunityScore = projectScores.Length == 0 ? 0m : projectScores.Max(entity => entity.OpportunityScore);
        var coWords = await dbContext.CoOccurrenceWords
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);
        var coWordIds = coWords.Select(entity => entity.Id).ToArray();
        var coDetails = await dbContext.CoOccurrencePageDetails
            .AsNoTracking()
            .Where(entity => coWordIds.Contains(entity.CoWordId))
            .ToArrayAsync(cancellationToken);
        var headlinePages = await dbContext.SerpHeadlinePages
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);
        var activeCandidates = await dbContext.CannibalizationCandidates
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && entity.Status == StatusValues.Active)
            .ToArrayAsync(cancellationToken);
        var contentTrafficValues = await dbContext.ContentSearchResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);

        var maxVolume = metrics.Count == 0 ? 0 : metrics.Values.Max(entity => entity.SearchVolume);
        var maxTrafficValue = latestRankings
            .Select(entity => GetTrafficValue(entity, contentTrafficValues))
            .DefaultIfEmpty(0m)
            .Max();
        var drafts = latestRankings.Select(entity =>
            BuildRewriteCandidateDraft(
                entity,
                keywords,
                metrics,
                projectScores,
                maxOpportunityScore,
                coWords,
                coDetails,
                headlinePages,
                activeCandidates,
                contentTrafficValues,
                maxVolume,
                maxTrafficValue)).ToArray();

        var groupedDrafts = drafts
            .GroupBy(draft => NormalizeUrlKey(draft.TargetUrl), StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildRewriteTaskDraft(group.ToArray()))
            .ToArray();
        var existingTasks = await dbContext.RewriteTasks
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var now = NowUtc();

        foreach (var draft in groupedDrafts)
        {
            var task = existingTasks
                .FirstOrDefault(entity => string.Equals(NormalizeUrlKey(entity.TargetUrl), NormalizeUrlKey(draft.TargetUrl), StringComparison.OrdinalIgnoreCase));
            if (task is null)
            {
                task = new RewriteTaskEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = project.Id,
                    TargetUrl = draft.TargetUrl,
                    Status = StatusValues.Active,
                    AssigneeActor = SystemActor.Developer,
                    CreatedAt = now
                };
                dbContext.RewriteTasks.Add(task);
            }

            task.PriorityScore = draft.PriorityScore;
            task.ReasonJson = draft.ReasonJson;
            if (string.IsNullOrWhiteSpace(task.Status))
            {
                task.Status = StatusValues.Active;
            }

            if (string.IsNullOrWhiteSpace(task.AssigneeActor))
            {
                task.AssigneeActor = SystemActor.Developer;
            }

            task.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(groupedDrafts.Length);
    }

    private async Task<Dictionary<Guid, KeywordMetricEntity>> LoadLatestMetricsAsync(
        IReadOnlyList<Guid> keywordIds,
        ProjectEntity project,
        CancellationToken cancellationToken)
    {
        var metrics = await dbContext.KeywordMetrics
            .AsNoTracking()
            .Where(entity =>
                keywordIds.Contains(entity.KeywordId) &&
                entity.Location == project.DefaultLocation &&
                entity.Language == project.DefaultLanguage)
            .ToArrayAsync(cancellationToken);

        return metrics
            .GroupBy(entity => entity.KeywordId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entity => entity.FetchedAt).First());
    }

    private RewriteCandidateDraft BuildRewriteCandidateDraft(
        RankResultEntity ranking,
        IReadOnlyDictionary<Guid, string> keywords,
        IReadOnlyDictionary<Guid, KeywordMetricEntity> metrics,
        IReadOnlyList<ProjectKeywordScoreEntity> projectScores,
        decimal maxOpportunityScore,
        IReadOnlyList<CoOccurrenceWordEntity> coWords,
        IReadOnlyList<CoOccurrencePageDetailEntity> coDetails,
        IReadOnlyList<SerpHeadlinePageEntity> headlinePages,
        IReadOnlyList<CannibalizationCandidateEntity> activeCandidates,
        IReadOnlyList<ContentSearchResultEntity> contentTrafficValues,
        int maxVolume,
        decimal maxTrafficValue)
    {
        var metric = metrics.GetValueOrDefault(ranking.KeywordId);
        var trafficValue = GetTrafficValue(ranking, contentTrafficValues);
        var coOccurrenceGap = CalculateCoOccurrenceGap(ranking.KeywordId, ranking.RankedUrl, coWords, coDetails);
        var headingGap = CalculateHeadingGap(ranking.KeywordId, ranking.RankedUrl, headlinePages);
        var projectScore = projectScores
            .Where(entity => entity.KeywordId == ranking.KeywordId)
            .OrderByDescending(entity => entity.OpportunityScore)
            .FirstOrDefault();
        var hasCannibalization = activeCandidates.Any(candidate =>
            candidate.KeywordId == ranking.KeywordId &&
            CandidateContainsUrl(candidate, ranking.RankedUrl));

        var positionWeight = ranking.Position switch
        {
            >= 4 and <= 10 => 1.0m,
            >= 11 and <= 20 => 0.8m,
            >= 21 and <= 50 => 0.4m,
            _ => 0.1m
        };
        var volumeScore = metric is null ? 0m : NormalizeLog(metric.SearchVolume, maxVolume);
        var trafficScore = maxTrafficValue <= 0m ? 0.5m : Clamp(trafficValue / maxTrafficValue, 0m, 1m);
        var difficultyPenalty = metric is null ? 0m : Clamp(metric.SeoDifficulty / 100m, 0m, 1m);
        var opportunityBoost = projectScore is null || maxOpportunityScore <= 0m
            ? 0m
            : Clamp(projectScore.OpportunityScore / maxOpportunityScore, 0m, 1m) * 8m;
        var cannibalizationBoost = hasCannibalization ? 10m : 0m;
        var score = 70m * positionWeight * volumeScore * (0.4m + (0.6m * trafficScore)) * (1m - (difficultyPenalty * 0.6m))
            + headingGap.Score
            + coOccurrenceGap.Score
            + opportunityBoost
            + cannibalizationBoost;
        score = decimal.Round(Clamp(score, 0m, 100m), 4);

        return new RewriteCandidateDraft(
            ranking.RankedUrl,
            ranking.KeywordId,
            keywords.GetValueOrDefault(ranking.KeywordId) ?? ranking.KeywordId.ToString("D"),
            ranking.Position,
            ranking.EstimatedTraffic,
            trafficValue,
            metric?.SearchVolume,
            metric?.SeoDifficulty,
            projectScore?.OpportunityScore,
            headingGap.MissingHeadingCount,
            headingGap.Score,
            coOccurrenceGap.MissingWordCount,
            coOccurrenceGap.MissingWords,
            coOccurrenceGap.Score,
            hasCannibalization,
            score,
            ranking.CheckedAt);
    }

    private static RewriteTaskDraft BuildRewriteTaskDraft(IReadOnlyList<RewriteCandidateDraft> drafts)
    {
        var ordered = drafts
            .OrderByDescending(draft => draft.PriorityScore)
            .ThenBy(draft => draft.Position)
            .ToArray();
        var representative = ordered[0];
        var priorityScore = decimal.Round(ordered.Max(draft => draft.PriorityScore), 4);
        var reasonJson = JsonSerializer.Serialize(
            new
            {
                scoreVersion = 1,
                priorityScore,
                targetUrl = representative.TargetUrl,
                bestPosition = ordered.Min(draft => draft.Position),
                maxEstimatedTraffic = ordered.Max(draft => draft.EstimatedTraffic),
                maxTrafficValue = ordered.Max(draft => draft.TrafficValue),
                hasCannibalization = ordered.Any(draft => draft.HasCannibalization),
                keywords = ordered
                    .Take(ReasonKeywordLimit)
                    .Select(draft => new
                    {
                        keywordId = draft.KeywordId,
                        keyword = draft.Keyword,
                        position = draft.Position,
                        estimatedTraffic = draft.EstimatedTraffic,
                        trafficValue = draft.TrafficValue,
                        searchVolume = draft.SearchVolume,
                        seoDifficulty = draft.SeoDifficulty,
                        opportunityScore = draft.OpportunityScore,
                        missingHeadingCount = draft.MissingHeadingCount,
                        missingHeadingScore = draft.MissingHeadingScore,
                        missingCoOccurrenceWordCount = draft.MissingCoOccurrenceWordCount,
                        missingCoOccurrenceWords = draft.MissingCoOccurrenceWords,
                        missingCoOccurrenceScore = draft.MissingCoOccurrenceScore,
                        hasCannibalization = draft.HasCannibalization,
                        priorityScore = draft.PriorityScore,
                        checkedAt = draft.CheckedAt
                    })
                    .ToArray()
            },
            JsonOptions);
        return new RewriteTaskDraft(representative.TargetUrl, priorityScore, reasonJson);
    }

    private static decimal GetTrafficValue(
        RankResultEntity ranking,
        IReadOnlyList<ContentSearchResultEntity> contentTrafficValues)
        => contentTrafficValues
            .Where(entity =>
                entity.KeywordId == ranking.KeywordId &&
                string.Equals(NormalizeUrlKey(entity.Url), NormalizeUrlKey(ranking.RankedUrl), StringComparison.OrdinalIgnoreCase))
            .Select(entity => entity.TrafficValue)
            .DefaultIfEmpty(ranking.EstimatedTraffic)
            .Max();

    private static GapEvidence CalculateCoOccurrenceGap(
        Guid keywordId,
        string targetUrl,
        IReadOnlyList<CoOccurrenceWordEntity> coWords,
        IReadOnlyList<CoOccurrencePageDetailEntity> coDetails)
    {
        var words = coWords.Where(entity => entity.KeywordId == keywordId).ToArray();
        if (words.Length == 0)
        {
            return new GapEvidence(0, [], 0m);
        }

        var missingWords = new List<string>();
        foreach (var word in words)
        {
            var details = coDetails.Where(entity => entity.CoWordId == word.Id).ToArray();
            if (details.Length == 0)
            {
                continue;
            }

            var maxCount = details.Max(entity => entity.Count);
            var own = details.FirstOrDefault(entity =>
                string.Equals(NormalizeUrlKey(entity.Url), NormalizeUrlKey(targetUrl), StringComparison.OrdinalIgnoreCase));
            if (own is null || own.Count < Math.Max(1, maxCount / 2))
            {
                missingWords.Add(word.Word);
            }
        }

        var score = decimal.Round(Math.Min(12m, missingWords.Count * 2m), 4);
        return new GapEvidence(missingWords.Count, missingWords.Take(10).ToArray(), score);
    }

    private static HeadingGapEvidence CalculateHeadingGap(
        Guid keywordId,
        string targetUrl,
        IReadOnlyList<SerpHeadlinePageEntity> headlinePages)
    {
        var pages = headlinePages.Where(entity => entity.KeywordId == keywordId).ToArray();
        if (pages.Length == 0)
        {
            return new HeadingGapEvidence(0, 0m);
        }

        var own = pages.FirstOrDefault(entity =>
            string.Equals(NormalizeUrlKey(entity.Url), NormalizeUrlKey(targetUrl), StringComparison.OrdinalIgnoreCase));
        var averageCount = pages.Average(entity => entity.HeadlineCount);
        var missingCount = own is null
            ? Convert.ToInt32(Math.Round(averageCount, MidpointRounding.AwayFromZero))
            : Math.Max(0, Convert.ToInt32(Math.Round(averageCount - own.HeadlineCount, MidpointRounding.AwayFromZero)));
        var score = decimal.Round(Math.Min(12m, missingCount * 1.5m), 4);
        return new HeadingGapEvidence(missingCount, score);
    }

    private static decimal CalculateCannibalizationSeverity(IReadOnlyList<RankResultEntity> rankings)
    {
        var positions = rankings.Select(entity => entity.Position).Where(position => position > 0).ToArray();
        if (positions.Length < 2)
        {
            return 0m;
        }

        var spread = positions.Max() - positions.Min();
        var urlCountBoost = Math.Min(20m, (rankings.Count - 2) * 7.5m);
        var trafficBoost = rankings.Sum(entity => entity.EstimatedTraffic) > 0m ? 10m : 0m;
        var score = 100m - Math.Min(65m, spread * 5m) + urlCountBoost + trafficBoost;
        return decimal.Round(Clamp(score, 0m, 100m), 4);
    }

    private static string SerializeCannibalizationEvidence(
        string keyword,
        IReadOnlyList<RankResultEntity> latestRankings,
        IReadOnlyList<RankResultEntity> allRankings)
        => JsonSerializer.Serialize(
            new
            {
                scoreVersion = 1,
                keyword,
                detectedUrlCount = latestRankings.Count,
                rankSpread = latestRankings.Max(entity => entity.Position) - latestRankings.Min(entity => entity.Position),
                latestRankings = latestRankings.Select(MapRankEvidence).ToArray(),
                rankingHistory = latestRankings.Select(ranking => new
                {
                    url = ranking.RankedUrl,
                    positions = allRankings
                        .Where(entity =>
                            entity.KeywordId == ranking.KeywordId &&
                            string.Equals(NormalizeUrlKey(entity.RankedUrl), NormalizeUrlKey(ranking.RankedUrl), StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(entity => entity.CheckedAt)
                        .Take(5)
                        .Select(entity => new
                        {
                            position = entity.Position,
                            estimatedTraffic = entity.EstimatedTraffic,
                            checkedAt = entity.CheckedAt
                        })
                        .ToArray()
                }).ToArray()
            },
            JsonOptions);

    private static string SerializeCannibalizationRecommendation(
        string primaryUrl,
        IReadOnlyList<RankResultEntity> competing,
        decimal severity)
        => JsonSerializer.Serialize(
            new
            {
                action = severity >= 70m ? "consolidate_or_canonicalize" : "review_internal_links",
                primaryUrl,
                competingUrls = competing.Select(entity => entity.RankedUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                suggestedActions = new[]
                {
                    "Keep the strongest URL as the primary landing page.",
                    "Consolidate overlapping intent or add canonical/noindex where appropriate.",
                    "Update internal links and anchor text toward the primary URL."
                }
            },
            JsonOptions);

    private static object MapCompetingUrlEvidence(RankResultEntity entity)
        => new
        {
            url = entity.RankedUrl,
            target = entity.Target,
            position = entity.Position,
            estimatedTraffic = entity.EstimatedTraffic,
            checkedAt = entity.CheckedAt
        };

    private static object MapRankEvidence(RankResultEntity entity)
        => new
        {
            url = entity.RankedUrl,
            target = entity.Target,
            position = entity.Position,
            estimatedTraffic = entity.EstimatedTraffic,
            checkedAt = entity.CheckedAt
        };

    private static bool CandidateContainsUrl(CannibalizationCandidateEntity candidate, string url)
    {
        var normalizedUrl = NormalizeUrlKey(url);
        if (string.Equals(NormalizeUrlKey(candidate.PrimaryUrl), normalizedUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(candidate.CompetingUrlsJson) ? "[]" : candidate.CompetingUrlsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var itemUrl = item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString(),
                    JsonValueKind.Object when item.TryGetProperty("url", out var property) => property.GetString(),
                    _ => null
                };
                if (string.Equals(NormalizeUrlKey(itemUrl ?? string.Empty), normalizedUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
        }

        return false;
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

    private async Task<RewriteTaskEntity?> FindRewriteTaskAsync(
        ProjectExecutionContext context,
        Guid taskId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        var source = dbContext.RewriteTasks.Where(entity => entity.ProjectId == context.ProjectId.Value);
        if (!asTracking)
        {
            source = source.AsNoTracking();
        }

        return await source.FirstOrDefaultAsync(entity => entity.Id == taskId, cancellationToken);
    }

    private async Task<string> BuildRankEvidenceSignatureAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .OrderBy(entity => entity.KeywordId)
            .ThenBy(entity => entity.RankedUrl)
            .ThenByDescending(entity => entity.CheckedAt)
            .Select(entity => new
            {
                entity.KeywordId,
                entity.RankedUrl,
                entity.Position,
                entity.EstimatedTraffic,
                entity.CheckedAt
            })
            .ToArrayAsync(cancellationToken);
        return HashText(JsonSerializer.Serialize(rows, JsonOptions));
    }

    private static RewriteTaskDetails MapRewriteTask(RewriteTaskEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.TargetUrl,
            entity.PriorityScore,
            ParseJsonElement(entity.ReasonJson),
            entity.Status,
            entity.AssigneeActor,
            entity.Memo,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static CannibalizationCandidateDetails MapCannibalizationCandidate(
        CannibalizationCandidateEntity entity,
        IReadOnlyDictionary<Guid, string> keywords)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.KeywordId,
            keywords.GetValueOrDefault(entity.KeywordId) ?? entity.KeywordId.ToString("D"),
            entity.PrimaryUrl,
            ParseJsonElement(entity.CompetingUrlsJson, "[]"),
            entity.SeverityScore,
            ParseJsonElement(entity.EvidenceJson),
            ParseJsonElement(entity.RecommendationJson),
            entity.Status,
            entity.DetectedAt);

    private static IEnumerable<RewriteTaskDetails> SortRewriteTasks(
        IEnumerable<RewriteTaskDetails> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "priorityScore";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "targetUrl" => SortString(rows, ascending, row => row.TargetUrl),
            "status" => SortString(rows, ascending, row => row.Status),
            "assigneeActor" => SortString(rows, ascending, row => row.AssigneeActor),
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            "updatedAt" => SortDateTime(rows, ascending, row => row.UpdatedAt),
            _ => SortDecimal(rows, ascending, row => row.PriorityScore)
        };
    }

    private static IEnumerable<CannibalizationCandidateDetails> SortCannibalizationCandidates(
        IEnumerable<CannibalizationCandidateDetails> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "severityScore";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "keyword" => SortString(rows, ascending, row => row.Keyword),
            "primaryUrl" => SortString(rows, ascending, row => row.PrimaryUrl),
            "status" => SortString(rows, ascending, row => row.Status),
            "detectedAt" => SortDateTime(rows, ascending, row => row.DetectedAt),
            _ => SortDecimal(rows, ascending, row => row.SeverityScore)
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

    private static JsonElement ParseJsonElement(string json, string fallback = "{}")
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? fallback : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse(fallback);
            return document.RootElement.Clone();
        }
    }

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal NormalizeLog(int value, int maxValue)
    {
        if (value <= 0 || maxValue <= 0)
        {
            return 0m;
        }

        var normalized = Math.Log10(value + 1d) / Math.Log10(maxValue + 1d);
        return Convert.ToDecimal(normalized, CultureInfo.InvariantCulture);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => Math.Min(max, Math.Max(min, value));

    private static string NormalizeUrlKey(string value)
        => value.Trim().TrimEnd('/').ToLowerInvariant();

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

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

    private sealed class RankUrlKeyComparer : IEqualityComparer<RankUrlKey>
    {
        public static RankUrlKeyComparer Instance { get; } = new();

        public bool Equals(RankUrlKey? x, RankUrlKey? y)
            => x is not null &&
               y is not null &&
               x.KeywordId == y.KeywordId &&
               string.Equals(x.Url, y.Url, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(RankUrlKey obj)
            => HashCode.Combine(obj.KeywordId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Url));
    }

    private sealed record RankUrlKey(Guid KeywordId, string Url);

    private sealed record CannibalizationRefreshJobRequest(int Version, Guid ProjectId);

    private sealed record GapEvidence(int MissingWordCount, IReadOnlyList<string> MissingWords, decimal Score);

    private sealed record HeadingGapEvidence(int MissingHeadingCount, decimal Score);

    private sealed record RewriteCandidateDraft(
        string TargetUrl,
        Guid KeywordId,
        string Keyword,
        int Position,
        decimal EstimatedTraffic,
        decimal TrafficValue,
        int? SearchVolume,
        decimal? SeoDifficulty,
        decimal? OpportunityScore,
        int MissingHeadingCount,
        decimal MissingHeadingScore,
        int MissingCoOccurrenceWordCount,
        IReadOnlyList<string> MissingCoOccurrenceWords,
        decimal MissingCoOccurrenceScore,
        bool HasCannibalization,
        decimal PriorityScore,
        DateTime CheckedAt);

    private sealed record RewriteTaskDraft(string TargetUrl, decimal PriorityScore, string ReasonJson);
}

internal sealed class CannibalizationDetectionJob(
    SeoIntelligenceDbContext dbContext,
    RewriteManagementService rewriteManagementService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<CannibalizationDetectionJob> logger)
{
    public const string JobType = RewriteManagementService.CannibalizationDetectionJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Cannibalization detection job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, RewriteManagementService.CannibalizationDetectionResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Cannibalization detection job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Cannibalization detection job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var detection = await rewriteManagementService.ExecuteCannibalizationDetectionAsync(
                context,
                job.ResultResourceId.Value);
            if (!detection.IsSuccess)
            {
                await RecordFailureAsync(context, jobId, detection.Error!);
                return;
            }

            var scoring = await rewriteManagementService.ExecuteRewriteScoringAsync(
                context,
                job.ResultResourceId.Value);
            if (!scoring.IsSuccess)
            {
                await RecordFailureAsync(context, jobId, scoring.Error!);
                return;
            }

            await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(
                    100,
                    new JobResultResource(RewriteManagementService.CannibalizationDetectionResourceType, job.ResultResourceId.Value)));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Cannibalization detection job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Cannibalization detection could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Cannibalization detection job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Cannibalization detection failed unexpectedly."));
        }
    }

    private async Task RecordFailureAsync(ProjectExecutionContext context, Guid jobId, Error error)
        => await jobService.RecordFailureAsync(
            context,
            jobId,
            new JobFailure(JobFailureKind.Unexpected, null, error.Code.ToString(), error.Message));
}

internal sealed class RewriteScoringJob(
    SeoIntelligenceDbContext dbContext,
    RewriteManagementService rewriteManagementService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<RewriteScoringJob> logger)
{
    public const string JobType = RewriteManagementService.RewriteScoringJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Rewrite scoring job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, RewriteManagementService.RewriteScoringResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Rewrite scoring job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Rewrite scoring job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await rewriteManagementService.ExecuteRewriteScoringAsync(
                context,
                job.ResultResourceId.Value);
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
                new JobCompletion(
                    100,
                    new JobResultResource(RewriteManagementService.RewriteScoringResourceType, job.ResultResourceId.Value)));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Rewrite scoring job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Rewrite scoring could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Rewrite scoring job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Rewrite scoring failed unexpectedly."));
        }
    }
}
