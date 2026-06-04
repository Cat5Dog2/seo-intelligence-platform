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
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class TopicClusterService(
    SeoIntelligenceDbContext dbContext,
    IJobService jobService,
    TimeProvider timeProvider)
    : ITopicClusterService
{
    public const string TopicClusterGenerateJobType = "TopicClusterGenerateJob";
    public const string TopicClusterGenerationResourceType = "topic_cluster_generation";

    private const decimal MinimumTopicSimilarity = 0.15m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<JobReference>> GenerateAsync(
        ProjectExecutionContext context,
        TopicClusterGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue)
        {
            return Result<JobReference>.Failure(Error.Validation(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    ["projectId"] = ["projectId is required."]
                }));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        var normalized = new NormalizedTopicClusterGenerateRequest(Version: 1, request.Regenerate);
        var payload = JsonSerializer.SerializeToElement(normalized, JsonOptions);
        var evidenceSignature = await BuildEvidenceSignatureAsync(project.Id, cancellationToken);
        var requestHash = HashText($"{project.Id:N}\n{payload.GetRawText()}\n{evidenceSignature}");

        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                TopicClusterGenerateJobType,
                payload,
                RequestHash: requestHash,
                IdempotencyKey: $"topic-cluster-generate:{project.Id:N}:regenerate={(normalized.Regenerate ? 1 : 0).ToString(CultureInfo.InvariantCulture)}:{requestHash}",
                TargetKey: project.Id.ToString("N"),
                Queue: "analysis",
                InitialResource: new JobResultResource(TopicClusterGenerationResourceType, project.Id)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<PagedResult<TopicClusterSummary>>> GetClustersAsync(
        ProjectExecutionContext context,
        TopicClusterSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<TopicClusterSummary>>(ErrorCode.NotFound, "Project was not found.");
        }

        var views = await LoadClusterViewsAsync(project.Id, cancellationToken);
        IEnumerable<TopicClusterView> rows = views;

        if (query.ParentId.HasValue)
        {
            rows = rows.Where(row => row.Cluster.ParentId == query.ParentId.Value);
        }

        var intent = OptionalText(query.IntentLabel);
        if (intent is not null)
        {
            rows = rows.Where(row => string.Equals(row.IntentLabel, intent, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.Cluster.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (row.RepresentativeKeyword?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                row.Keywords.Any(keyword => keyword.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        rows = SortClusters(rows, query.Search.Sort).ToArray();
        var summaries = rows.Select(row => MapSummary(row, views)).ToArray();
        return Result<PagedResult<TopicClusterSummary>>.Success(ToPagedResult(summaries, query.Search));
    }

    public async Task<Result<TopicClusterDetails>> GetClusterAsync(
        ProjectExecutionContext context,
        Guid clusterId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<TopicClusterDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var views = await LoadClusterViewsAsync(project.Id, cancellationToken);
        var view = views.FirstOrDefault(row => row.Cluster.Id == clusterId);
        if (view is null)
        {
            return Failure<TopicClusterDetails>(ErrorCode.NotFound, "Topic cluster was not found.");
        }

        return Result<TopicClusterDetails>.Success(MapDetails(view, views));
    }

    public async Task<Result> ExecuteGenerateAsync(
        ProjectExecutionContext context,
        Guid projectId,
        bool regenerate,
        CancellationToken cancellationToken = default)
    {
        if (!context.ProjectId.HasValue || context.ProjectId.Value != projectId)
        {
            return Result.Failure(new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Result.Failure(new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var clusterPlan = await BuildClusterPlanAsync(project, cancellationToken);
        await SaveClusterPlanAsync(project.Id, clusterPlan, regenerate, cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyList<TopicClusterDraft>> BuildClusterPlanAsync(
        ProjectEntity project,
        CancellationToken cancellationToken)
    {
        var seedTopics = await LoadProjectSeedTopicsAsync(project, cancellationToken);
        var members = new Dictionary<MemberKey, TopicMemberDraft>();

        foreach (var seed in seedTopics)
        {
            AddMember(members, seed.TopicKey, seed.TopicName, seed.Keyword, "seed", 1m, null);
        }

        if (seedTopics.Count > 0)
        {
            await AddSuggestionMembersAsync(members, seedTopics, cancellationToken);
            await AddRelatedMembersAsync(members, seedTopics, cancellationToken);
            await AddRankingMembersAsync(members, seedTopics, cancellationToken);
            await AddLsiMembersAsync(members, seedTopics, cancellationToken);
        }

        await AddProjectEvidenceMembersAsync(project.Id, members, seedTopics, cancellationToken);
        await ApplyProjectScoresAsync(project.Id, members, cancellationToken);
        await ApplyTopicFaqCountsAsync(project.Id, members, seedTopics, cancellationToken);
        ApplyComputedScores(members.Values);

        return BuildClusterDrafts(project.Id, members.Values);
    }

    private async Task<IReadOnlyList<ProjectSeedTopic>> LoadProjectSeedTopicsAsync(
        ProjectEntity project,
        CancellationToken cancellationToken)
    {
        var seeds = await dbContext.KeywordSeeds
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .OrderBy(entity => entity.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var topics = new List<ProjectSeedTopic>();

        foreach (var seed in seeds)
        {
            var language = ReadSeedLanguage(seed.Memo) ?? project.DefaultLanguage;
            var normalizedSeed = KeywordNormalizer.Normalize(seed.Seed);
            var textHash = HashText(normalizedSeed);
            var seedKeyword = await dbContext.Keywords
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity => entity.Language == language && entity.TextHash == textHash,
                    cancellationToken);
            if (seedKeyword is null)
            {
                continue;
            }

            topics.Add(new ProjectSeedTopic(seed.Id, seedKeyword.Id, seedKeyword.NormalizedText, seedKeyword));
        }

        return topics
            .GroupBy(topic => topic.TopicKey)
            .Select(group => group.First())
            .ToArray();
    }

    private async Task AddSuggestionMembersAsync(
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        var bySeedId = seedTopics.ToDictionary(topic => topic.SeedId);
        var seedIds = bySeedId.Keys.ToArray();
        var rows = await dbContext.KeywordSuggestions
            .AsNoTracking()
            .Where(entity => seedIds.Contains(entity.SeedId))
            .Join(
                dbContext.Keywords.AsNoTracking(),
                suggestion => suggestion.KeywordId,
                keyword => keyword.Id,
                (suggestion, keyword) => new { suggestion, keyword })
            .ToArrayAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (bySeedId.TryGetValue(row.suggestion.SeedId, out var topic))
            {
                AddMember(members, topic.TopicKey, topic.TopicName, row.keyword, "suggest", 0.65m, null);
            }
        }
    }

    private async Task AddRelatedMembersAsync(
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        var bySeedId = seedTopics.ToDictionary(topic => topic.SeedId);
        var seedIds = bySeedId.Keys.ToArray();
        var rows = await dbContext.RelatedKeywords
            .AsNoTracking()
            .Where(entity => seedIds.Contains(entity.SeedId))
            .Join(
                dbContext.Keywords.AsNoTracking(),
                related => related.KeywordId,
                keyword => keyword.Id,
                (related, keyword) => new { related, keyword })
            .ToArrayAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (bySeedId.TryGetValue(row.related.SeedId, out var topic))
            {
                AddMember(members, topic.TopicKey, topic.TopicName, row.keyword, "related", 0.6m, null);
            }
        }
    }

    private async Task AddRankingMembersAsync(
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        var byTopicKey = seedTopics.ToDictionary(topic => topic.TopicKey);
        var seedKeywordIds = byTopicKey.Keys.ToArray();
        var rows = await dbContext.RankingKeywords
            .AsNoTracking()
            .Where(entity => seedKeywordIds.Contains(entity.SeedKeywordId))
            .Join(
                dbContext.Keywords.AsNoTracking(),
                ranking => ranking.KeywordId,
                keyword => keyword.Id,
                (ranking, keyword) => new { ranking, keyword })
            .ToArrayAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (byTopicKey.TryGetValue(row.ranking.SeedKeywordId, out var topic))
            {
                AddMember(members, topic.TopicKey, topic.TopicName, row.keyword, "ranking", 0.8m, row.ranking.Relevance);
            }
        }
    }

    private async Task AddLsiMembersAsync(
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        var byTopicKey = seedTopics.ToDictionary(topic => topic.TopicKey);
        var seedKeywordIds = byTopicKey.Keys.ToArray();
        var rows = await dbContext.LsiPaaItems
            .AsNoTracking()
            .Where(entity => entity.KeywordId.HasValue && seedKeywordIds.Contains(entity.SeedKeywordId))
            .Join(
                dbContext.Keywords.AsNoTracking(),
                item => item.KeywordId!.Value,
                keyword => keyword.Id,
                (item, keyword) => new { item, keyword })
            .ToArrayAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!byTopicKey.TryGetValue(row.item.SeedKeywordId, out var topic))
            {
                continue;
            }

            var member = AddMember(
                members,
                topic.TopicKey,
                topic.TopicName,
                row.keyword,
                string.IsNullOrWhiteSpace(row.item.QuestionText) ? "lsi" : "faq",
                Math.Max(0.45m, row.item.Importance),
                null);
            if (!string.IsNullOrWhiteSpace(row.item.QuestionText))
            {
                member.FaqCount++;
            }
        }
    }

    private async Task AddProjectEvidenceMembersAsync(
        Guid projectId,
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        var influxRows = await dbContext.InfluxKeywordResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => new { result, keyword })
            .ToArrayAsync(cancellationToken);

        foreach (var row in influxRows)
        {
            var member = AddProjectEvidenceMember(members, seedTopics, row.keyword, "co_ranking");
            member.RankedUrls.Add(row.result.RankedUrl);
        }

        var rankedUrlGroups = influxRows
            .Where(row => !string.IsNullOrWhiteSpace(row.result.RankedUrl))
            .GroupBy(row => row.result.RankedUrl, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(row => row.keyword.Id).Distinct().Count() > 1)
            .ToArray();
        foreach (var group in rankedUrlGroups)
        {
            var keywordIds = group.Select(row => row.keyword.Id).Distinct().ToArray();
            foreach (var member in members.Values.Where(member => keywordIds.Contains(member.Keyword.Id)))
            {
                member.CoRankingScore += keywordIds.Length - 1;
            }
        }

        var contentRows = await dbContext.ContentSearchResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => keyword)
            .ToArrayAsync(cancellationToken);
        foreach (var keyword in contentRows)
        {
            AddProjectEvidenceMember(members, seedTopics, keyword, "content");
        }

        var headlineRows = await dbContext.SerpHeadlinePages
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => keyword)
            .ToArrayAsync(cancellationToken);
        foreach (var keyword in headlineRows)
        {
            AddProjectEvidenceMember(members, seedTopics, keyword, "headline");
        }

        var coOccurrenceRows = await dbContext.CoOccurrenceWords
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => new { result, keyword })
            .ToArrayAsync(cancellationToken);
        foreach (var row in coOccurrenceRows)
        {
            var member = AddProjectEvidenceMember(members, seedTopics, row.keyword, "co_occurrence");
            member.CoOccurrenceCount++;
        }
    }

    private TopicMemberDraft AddProjectEvidenceMember(
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        KeywordEntity keyword,
        string source)
    {
        var existingTopics = members.Values
            .Where(member => member.Keyword.Id == keyword.Id)
            .Select(member => new TopicChoice(member.TopicKey, member.TopicName, Similarity: 1m))
            .DistinctBy(choice => choice.TopicKey)
            .ToArray();
        if (existingTopics.Length > 0)
        {
            TopicMemberDraft? last = null;
            foreach (var topic in existingTopics)
            {
                last = AddMember(members, topic.TopicKey, topic.TopicName, keyword, source, topic.Similarity, null);
            }

            return last!;
        }

        var selected = SelectTopic(keyword, seedTopics);
        return AddMember(members, selected.TopicKey, selected.TopicName, keyword, source, selected.Similarity, null);
    }

    private async Task ApplyProjectScoresAsync(
        Guid projectId,
        IDictionary<MemberKey, TopicMemberDraft> members,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return;
        }

        var keywordIds = members.Values.Select(member => member.Keyword.Id).Distinct().ToArray();
        var scores = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);
        var latestScores = scores
            .GroupBy(entity => entity.KeywordId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entity => entity.ScoredAt).First().OpportunityScore);

        foreach (var member in members.Values)
        {
            if (latestScores.TryGetValue(member.Keyword.Id, out var score))
            {
                member.OpportunityScore = score;
                member.Sources.Add("opportunity_score");
            }
        }
    }

    private async Task ApplyTopicFaqCountsAsync(
        Guid projectId,
        IDictionary<MemberKey, TopicMemberDraft> members,
        IReadOnlyList<ProjectSeedTopic> seedTopics,
        CancellationToken cancellationToken)
    {
        if (seedTopics.Count == 0)
        {
            return;
        }

        var seedKeywordIds = seedTopics.Select(topic => topic.TopicKey).ToArray();
        var questionCounts = await dbContext.Questions
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.SeedKeywordId.HasValue && seedKeywordIds.Contains(entity.SeedKeywordId.Value))
            .GroupBy(entity => entity.SeedKeywordId!.Value)
            .Select(group => new { TopicKey = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entity => entity.TopicKey, entity => entity.Count, cancellationToken);

        foreach (var member in members.Values)
        {
            if (questionCounts.TryGetValue(member.TopicKey, out var count))
            {
                member.FaqCount += count;
                member.Sources.Add("faq");
            }
        }
    }

    private static void ApplyComputedScores(IEnumerable<TopicMemberDraft> members)
    {
        foreach (var member in members)
        {
            member.IntentLabel = ClassifyIntent(member.Keyword.NormalizedText, member.FaqCount);
            member.LexicalSimilarity = CalculateLexicalSimilarity(member.TopicName, member.Keyword.NormalizedText);

            var opportunity = member.OpportunityScore > 0m ? member.OpportunityScore * 0.45m : 8m;
            var ranking = member.RankingRelevance > 0m ? member.RankingRelevance * 0.2m : 0m;
            var lexical = member.LexicalSimilarity * 15m;
            var coRanking = Math.Min(10m, member.CoRankingScore * 5m);
            var faq = Math.Min(5m, member.FaqCount * 2.5m);
            var source = Math.Min(5m, member.Sources.Count);
            var score = opportunity + ranking + lexical + coRanking + faq + source;

            member.ClusterScore = Math.Round(Math.Min(100m, score), 4);
        }
    }

    private static IReadOnlyList<TopicClusterDraft> BuildClusterDrafts(
        Guid projectId,
        IEnumerable<TopicMemberDraft> members)
    {
        var drafts = new List<TopicClusterDraft>();
        var memberGroups = members
            .GroupBy(member => new { member.TopicKey, member.TopicName })
            .OrderBy(group => group.Key.TopicName, StringComparer.OrdinalIgnoreCase);

        foreach (var topicGroup in memberGroups)
        {
            var topicMembers = topicGroup
                .GroupBy(member => member.Keyword.Id)
                .Select(group => group.OrderByDescending(member => member.ClusterScore).First())
                .OrderByDescending(member => member.ClusterScore)
                .ThenBy(member => member.Keyword.NormalizedText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (topicMembers.Length == 0)
            {
                continue;
            }

            var rootId = StableGuid(projectId, "topic-root", topicGroup.Key.TopicKey.ToString("N"));
            var rootRepresentative = topicMembers.First().Keyword.Id;
            drafts.Add(new TopicClusterDraft(
                rootId,
                projectId,
                topicGroup.Key.TopicName,
                ParentId: null,
                rootRepresentative,
                CalculateClusterScore(topicMembers),
                topicMembers));

            foreach (var intentGroup in topicMembers.GroupBy(member => member.IntentLabel).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var intentMembers = intentGroup
                    .OrderByDescending(member => member.ClusterScore)
                    .ThenBy(member => member.Keyword.NormalizedText, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var childId = StableGuid(projectId, "topic-child", topicGroup.Key.TopicKey.ToString("N"), intentGroup.Key);
                drafts.Add(new TopicClusterDraft(
                    childId,
                    projectId,
                    $"{topicGroup.Key.TopicName} - {intentGroup.Key}",
                    rootId,
                    intentMembers.First().Keyword.Id,
                    CalculateClusterScore(intentMembers),
                    intentMembers));
            }
        }

        return drafts;
    }

    private async Task SaveClusterPlanAsync(
        Guid projectId,
        IReadOnlyList<TopicClusterDraft> clusterPlan,
        bool regenerate,
        CancellationToken cancellationToken)
    {
        if (clusterPlan.Count == 0)
        {
            if (regenerate)
            {
                await PruneStaleClustersAsync(projectId, [], cancellationToken);
            }

            return;
        }

        var now = NowUtc();
        var clusterIds = clusterPlan.Select(cluster => cluster.ClusterId).ToArray();
        var existingClusters = await dbContext.TopicClusters
            .Where(entity => entity.ProjectId == projectId && clusterIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        foreach (var draft in clusterPlan)
        {
            if (!existingClusters.TryGetValue(draft.ClusterId, out var entity))
            {
                entity = new TopicClusterEntity
                {
                    Id = draft.ClusterId,
                    ProjectId = draft.ProjectId,
                    CreatedAt = now
                };
                dbContext.TopicClusters.Add(entity);
            }

            entity.Name = draft.Name;
            entity.ParentId = draft.ParentId;
            entity.RepresentativeKeywordId = draft.RepresentativeKeywordId;
            entity.Score = draft.Score;
            entity.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var existingKeywords = await dbContext.ClusterKeywords
            .Where(entity => clusterIds.Contains(entity.ClusterId))
            .ToListAsync(cancellationToken);
        var existingByKey = existingKeywords.ToDictionary(entity => new MemberKey(entity.ClusterId, entity.KeywordId));
        var desiredKeys = new HashSet<MemberKey>();

        foreach (var draft in clusterPlan)
        {
            var orderedMembers = draft.Members
                .OrderByDescending(member => member.ClusterScore)
                .ThenBy(member => member.Keyword.NormalizedText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            for (var index = 0; index < orderedMembers.Length; index++)
            {
                var member = orderedMembers[index];
                var key = new MemberKey(draft.ClusterId, member.Keyword.Id);
                desiredKeys.Add(key);

                if (!existingByKey.TryGetValue(key, out var keywordEntity))
                {
                    keywordEntity = new ClusterKeywordEntity
                    {
                        ClusterId = draft.ClusterId,
                        KeywordId = member.Keyword.Id
                    };
                    dbContext.ClusterKeywords.Add(keywordEntity);
                }

                keywordEntity.Role = index == 0 ? "representative" : "supporting";
                keywordEntity.OpportunityScore = member.ClusterScore;
                keywordEntity.IntentLabel = member.IntentLabel;
            }
        }

        dbContext.ClusterKeywords.RemoveRange(existingKeywords.Where(entity => !desiredKeys.Contains(new MemberKey(entity.ClusterId, entity.KeywordId))));
        await dbContext.SaveChangesAsync(cancellationToken);

        if (regenerate)
        {
            await PruneStaleClustersAsync(projectId, clusterIds, cancellationToken);
        }
    }

    private async Task PruneStaleClustersAsync(
        Guid projectId,
        IReadOnlyList<Guid> desiredClusterIds,
        CancellationToken cancellationToken)
    {
        var staleClusters = await dbContext.TopicClusters
            .Where(entity => entity.ProjectId == projectId && !desiredClusterIds.Contains(entity.Id))
            .ToArrayAsync(cancellationToken);
        if (staleClusters.Length == 0)
        {
            return;
        }

        var staleClusterIds = staleClusters.Select(entity => entity.Id).ToArray();
        var referencedClusterIds = await dbContext.ArticleBriefs
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.ClusterId.HasValue && staleClusterIds.Contains(entity.ClusterId.Value))
            .Select(entity => entity.ClusterId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var referenced = referencedClusterIds.ToHashSet();
        var removableIds = staleClusterIds.Where(id => !referenced.Contains(id)).ToHashSet();
        if (removableIds.Count == 0)
        {
            return;
        }

        var memberships = await dbContext.ClusterKeywords
            .Where(entity => removableIds.Contains(entity.ClusterId))
            .ToArrayAsync(cancellationToken);
        dbContext.ClusterKeywords.RemoveRange(memberships);
        await dbContext.SaveChangesAsync(cancellationToken);

        var childClusters = staleClusters
            .Where(entity => removableIds.Contains(entity.Id) && entity.ParentId.HasValue)
            .ToArray();
        dbContext.TopicClusters.RemoveRange(childClusters);
        await dbContext.SaveChangesAsync(cancellationToken);

        var remainingChildParentIds = await dbContext.TopicClusters
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.ParentId.HasValue)
            .Select(entity => entity.ParentId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var blockedParents = remainingChildParentIds.ToHashSet();
        var rootClusters = staleClusters
            .Where(entity =>
                removableIds.Contains(entity.Id) &&
                !entity.ParentId.HasValue &&
                !blockedParents.Contains(entity.Id))
            .ToArray();
        dbContext.TopicClusters.RemoveRange(rootClusters);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TopicClusterView>> LoadClusterViewsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var clusters = await dbContext.TopicClusters
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .ToArrayAsync(cancellationToken);
        if (clusters.Length == 0)
        {
            return [];
        }

        var clusterIds = clusters.Select(entity => entity.Id).ToArray();
        var clusterKeywords = await dbContext.ClusterKeywords
            .AsNoTracking()
            .Where(entity => clusterIds.Contains(entity.ClusterId))
            .ToArrayAsync(cancellationToken);
        var keywordIds = clusterKeywords
            .Select(entity => entity.KeywordId)
            .Concat(clusters.Where(entity => entity.RepresentativeKeywordId.HasValue).Select(entity => entity.RepresentativeKeywordId!.Value))
            .Distinct()
            .ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);
        var evidence = await LoadEvidenceLookupAsync(projectId, keywordIds, cancellationToken);
        var briefs = await dbContext.ArticleBriefs
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.ClusterId.HasValue && clusterIds.Contains(entity.ClusterId.Value))
            .ToArrayAsync(cancellationToken);
        var briefsByCluster = briefs
            .GroupBy(entity => entity.ClusterId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ArticleBriefEntity>)group.OrderByDescending(entity => entity.UpdatedAt).ToArray());
        var byId = clusters.ToDictionary(entity => entity.Id);
        var childCounts = clusters
            .Where(entity => entity.ParentId.HasValue)
            .GroupBy(entity => entity.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return clusters
            .Select(cluster =>
            {
                var rows = clusterKeywords
                    .Where(row => row.ClusterId == cluster.Id)
                    .Select(row =>
                    {
                        keywords.TryGetValue(row.KeywordId, out var keyword);
                        evidence.TryGetValue(row.KeywordId, out var keywordEvidence);
                        return new TopicClusterKeywordProjection(
                            row.KeywordId,
                            keyword?.NormalizedText ?? string.Empty,
                            row.Role,
                            row.OpportunityScore,
                            row.IntentLabel,
                            keywordEvidence ?? TopicKeywordEvidence.Empty);
                    })
                    .Where(row => !string.IsNullOrWhiteSpace(row.Keyword))
                    .OrderByDescending(row => row.OpportunityScore)
                    .ThenBy(row => row.Keyword, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var representativeKeyword = cluster.RepresentativeKeywordId.HasValue &&
                    keywords.TryGetValue(cluster.RepresentativeKeywordId.Value, out var representative)
                    ? representative.NormalizedText
                    : null;
                var parentName = cluster.ParentId.HasValue && byId.TryGetValue(cluster.ParentId.Value, out var parent)
                    ? parent.Name
                    : null;
                briefsByCluster.TryGetValue(cluster.Id, out var clusterBriefs);
                return new TopicClusterView(
                    cluster,
                    parentName,
                    representativeKeyword,
                    rows,
                    MajorityIntent(rows),
                    childCounts.GetValueOrDefault(cluster.Id),
                    clusterBriefs ?? []);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, TopicKeywordEvidence>> LoadEvidenceLookupAsync(
        Guid projectId,
        IReadOnlyList<Guid> keywordIds,
        CancellationToken cancellationToken)
    {
        var lookup = keywordIds.Distinct().ToDictionary(id => id, _ => new MutableTopicKeywordEvidence());
        if (lookup.Count == 0)
        {
            return new Dictionary<Guid, TopicKeywordEvidence>();
        }

        var ids = lookup.Keys.ToArray();
        var seedTopics = await LoadProjectSeedTopicsForProjectIdAsync(projectId, cancellationToken);
        var seedIds = seedTopics.Select(topic => topic.SeedId).ToArray();
        var seedKeywordIds = seedTopics.Select(topic => topic.TopicKey).ToArray();

        var suggestions = await dbContext.KeywordSuggestions
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.KeywordId) && seedIds.Contains(entity.SeedId))
            .ToArrayAsync(cancellationToken);
        foreach (var suggestion in suggestions)
        {
            lookup[suggestion.KeywordId].Sources.Add("suggest");
        }

        var related = await dbContext.RelatedKeywords
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.KeywordId) && seedIds.Contains(entity.SeedId))
            .ToArrayAsync(cancellationToken);
        foreach (var row in related)
        {
            lookup[row.KeywordId].Sources.Add("related");
        }

        var rankings = await dbContext.RankingKeywords
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.KeywordId) && seedKeywordIds.Contains(entity.SeedKeywordId))
            .ToArrayAsync(cancellationToken);
        foreach (var ranking in rankings)
        {
            lookup[ranking.KeywordId].Sources.Add("ranking");
        }

        var lsiItems = await dbContext.LsiPaaItems
            .AsNoTracking()
            .Where(entity => entity.KeywordId.HasValue && ids.Contains(entity.KeywordId.Value) && seedKeywordIds.Contains(entity.SeedKeywordId))
            .ToArrayAsync(cancellationToken);
        foreach (var item in lsiItems)
        {
            var mutable = lookup[item.KeywordId!.Value];
            mutable.Sources.Add(string.IsNullOrWhiteSpace(item.QuestionText) ? "lsi" : "faq");
            if (!string.IsNullOrWhiteSpace(item.QuestionText))
            {
                mutable.FaqCount++;
            }
        }

        var topicQuestionCount = await dbContext.Questions
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        if (topicQuestionCount > 0)
        {
            foreach (var mutable in lookup.Values)
            {
                mutable.Sources.Add("faq");
                mutable.FaqCount += topicQuestionCount;
            }
        }

        var influxRows = await dbContext.InfluxKeywordResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && ids.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);
        foreach (var row in influxRows)
        {
            lookup[row.KeywordId].Sources.Add("co_ranking");
        }

        foreach (var group in influxRows.GroupBy(row => row.RankedUrl, StringComparer.OrdinalIgnoreCase))
        {
            var groupedIds = group.Select(row => row.KeywordId).Distinct().ToArray();
            if (groupedIds.Length <= 1)
            {
                continue;
            }

            foreach (var keywordId in groupedIds)
            {
                lookup[keywordId].CoRankingScore += groupedIds.Length - 1;
            }
        }

        var contentKeywordIds = await dbContext.ContentSearchResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && ids.Contains(entity.KeywordId))
            .Select(entity => entity.KeywordId)
            .ToArrayAsync(cancellationToken);
        foreach (var keywordId in contentKeywordIds)
        {
            lookup[keywordId].Sources.Add("content");
        }

        var headlineKeywordIds = await dbContext.SerpHeadlinePages
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && ids.Contains(entity.KeywordId))
            .Select(entity => entity.KeywordId)
            .ToArrayAsync(cancellationToken);
        foreach (var keywordId in headlineKeywordIds)
        {
            lookup[keywordId].Sources.Add("headline");
        }

        var coOccurrenceKeywordIds = await dbContext.CoOccurrenceWords
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && ids.Contains(entity.KeywordId))
            .Select(entity => entity.KeywordId)
            .ToArrayAsync(cancellationToken);
        foreach (var keywordId in coOccurrenceKeywordIds)
        {
            lookup[keywordId].Sources.Add("co_occurrence");
        }

        var projectScoreIds = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && ids.Contains(entity.KeywordId))
            .Select(entity => entity.KeywordId)
            .ToArrayAsync(cancellationToken);
        foreach (var keywordId in projectScoreIds)
        {
            lookup[keywordId].Sources.Add("opportunity_score");
        }

        return lookup.ToDictionary(
            pair => pair.Key,
            pair => new TopicKeywordEvidence(
                CoRankingScore: Math.Round(pair.Value.CoRankingScore, 4),
                FaqCount: pair.Value.FaqCount,
                Sources: pair.Value.Sources.OrderBy(source => source, StringComparer.Ordinal).ToArray()));
    }

    private async Task<IReadOnlyList<ProjectSeedTopic>> LoadProjectSeedTopicsForProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.AsNoTracking().FirstAsync(entity => entity.Id == projectId, cancellationToken);
        return await LoadProjectSeedTopicsAsync(project, cancellationToken);
    }

    private static TopicClusterSummary MapSummary(
        TopicClusterView view,
        IReadOnlyList<TopicClusterView> allClusters)
        => new(
            view.Cluster.Id,
            view.Cluster.ProjectId,
            view.Cluster.Name,
            view.Cluster.ParentId,
            view.ParentName,
            view.Cluster.RepresentativeKeywordId,
            view.RepresentativeKeyword,
            view.Cluster.Score,
            view.Keywords.Count,
            view.IntentLabel,
            view.ChildCount,
            BuildArticleCandidates(view),
            BuildInternalLinks(view, allClusters),
            view.Cluster.CreatedAt,
            view.Cluster.UpdatedAt);

    private static TopicClusterDetails MapDetails(
        TopicClusterView view,
        IReadOnlyList<TopicClusterView> allClusters)
        => new(
            view.Cluster.Id,
            view.Cluster.ProjectId,
            view.Cluster.Name,
            view.Cluster.ParentId,
            view.ParentName,
            view.Cluster.RepresentativeKeywordId,
            view.RepresentativeKeyword,
            view.Cluster.Score,
            view.Keywords.Count,
            view.IntentLabel,
            view.Keywords.Select(keyword => MapKeywordRow(view, keyword)).ToArray(),
            allClusters
                .Where(cluster => cluster.Cluster.ParentId == view.Cluster.Id)
                .OrderByDescending(cluster => cluster.Cluster.Score)
                .Select(cluster => MapSummary(cluster, allClusters))
                .ToArray(),
            BuildArticleCandidates(view),
            BuildInternalLinks(view, allClusters),
            view.Cluster.CreatedAt,
            view.Cluster.UpdatedAt);

    private static TopicClusterKeywordRow MapKeywordRow(
        TopicClusterView cluster,
        TopicClusterKeywordProjection keyword)
        => new(
            keyword.KeywordId,
            keyword.Keyword,
            keyword.Role,
            keyword.OpportunityScore,
            keyword.IntentLabel,
            new TopicClusterKeywordEvidence(
                CalculateLexicalSimilarity(RootTopicName(cluster), keyword.Keyword),
                keyword.Evidence.CoRankingScore,
                keyword.Evidence.FaqCount,
                keyword.Evidence.Sources));

    private static IReadOnlyList<ArticleCandidateSummary> BuildArticleCandidates(TopicClusterView view)
    {
        if (view.Briefs.Count > 0)
        {
            return view.Briefs
                .Select(brief =>
                {
                    var target = view.Keywords.FirstOrDefault(keyword => keyword.KeywordId == brief.TargetKeywordId);
                    return new ArticleCandidateSummary(
                        brief.Id,
                        brief.Title,
                        brief.TargetKeywordId,
                        target?.Keyword,
                        target?.IntentLabel ?? view.IntentLabel,
                        target?.OpportunityScore ?? view.Cluster.Score,
                        brief.Status);
                })
                .ToArray();
        }

        var representative = view.Keywords
            .OrderByDescending(keyword => keyword.OpportunityScore)
            .FirstOrDefault();
        if (representative is null)
        {
            return [];
        }

        return
        [
            new ArticleCandidateSummary(
                BriefId: null,
                Title: $"Article brief: {representative.Keyword}",
                representative.KeywordId,
                representative.Keyword,
                representative.IntentLabel ?? view.IntentLabel,
                representative.OpportunityScore,
                Status: "candidate")
        ];
    }

    private static IReadOnlyList<InternalLinkCandidate> BuildInternalLinks(
        TopicClusterView view,
        IReadOnlyList<TopicClusterView> allClusters)
    {
        var links = new List<InternalLinkCandidate>();
        if (view.Cluster.ParentId.HasValue)
        {
            var parent = allClusters.FirstOrDefault(cluster => cluster.Cluster.Id == view.Cluster.ParentId.Value);
            if (parent is not null)
            {
                links.Add(new InternalLinkCandidate(
                    view.Cluster.Id,
                    view.Cluster.Name,
                    parent.Cluster.Id,
                    parent.Cluster.Name,
                    "child_to_parent"));
            }
        }

        links.AddRange(allClusters
            .Where(cluster => cluster.Cluster.ParentId == view.Cluster.Id)
            .OrderByDescending(cluster => cluster.Cluster.Score)
            .Take(3)
            .Select(child => new InternalLinkCandidate(
                view.Cluster.Id,
                view.Cluster.Name,
                child.Cluster.Id,
                child.Cluster.Name,
                "parent_to_child")));

        if (links.Count == 0 && view.Cluster.ParentId.HasValue)
        {
            links.AddRange(allClusters
                .Where(cluster =>
                    cluster.Cluster.ParentId == view.Cluster.ParentId &&
                    cluster.Cluster.Id != view.Cluster.Id)
                .OrderByDescending(cluster => cluster.Cluster.Score)
                .Take(3)
                .Select(sibling => new InternalLinkCandidate(
                    view.Cluster.Id,
                    view.Cluster.Name,
                    sibling.Cluster.Id,
                    sibling.Cluster.Name,
                    "sibling_intent")));
        }

        return links;
    }

    private static TopicMemberDraft AddMember(
        IDictionary<MemberKey, TopicMemberDraft> members,
        Guid topicKey,
        string topicName,
        KeywordEntity keyword,
        string source,
        decimal affinity,
        decimal? rankingRelevance)
    {
        var key = new MemberKey(topicKey, keyword.Id);
        if (!members.TryGetValue(key, out var member))
        {
            member = new TopicMemberDraft(topicKey, topicName, keyword);
            members[key] = member;
        }

        member.Affinity = Math.Max(member.Affinity, affinity);
        member.Sources.Add(source);
        if (rankingRelevance.HasValue)
        {
            member.RankingRelevance = Math.Max(member.RankingRelevance, rankingRelevance.Value);
        }

        return member;
    }

    private static TopicChoice SelectTopic(KeywordEntity keyword, IReadOnlyList<ProjectSeedTopic> seedTopics)
    {
        var best = seedTopics
            .Select(topic => new TopicChoice(
                topic.TopicKey,
                topic.TopicName,
                CalculateLexicalSimilarity(topic.TopicName, keyword.NormalizedText)))
            .OrderByDescending(choice => choice.Similarity)
            .FirstOrDefault();

        return best is not null && best.Similarity >= MinimumTopicSimilarity
            ? best
            : new TopicChoice(keyword.Id, keyword.NormalizedText, 1m);
    }

    private async Task<string> BuildEvidenceSignatureAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var seedCount = await dbContext.KeywordSeeds.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var seedTopics = await LoadProjectSeedTopicsForProjectIdAsync(projectId, cancellationToken);
        var seedKeywordIds = seedTopics.Select(topic => topic.TopicKey).ToArray();
        var suggestionCount = await dbContext.KeywordSuggestions
            .Join(
                dbContext.KeywordSeeds.Where(seed => seed.ProjectId == projectId),
                suggestion => suggestion.SeedId,
                seed => seed.Id,
                (suggestion, seed) => suggestion.Id)
            .CountAsync(cancellationToken);
        var relatedCount = await dbContext.RelatedKeywords
            .Join(
                dbContext.KeywordSeeds.Where(seed => seed.ProjectId == projectId),
                related => related.SeedId,
                seed => seed.Id,
                (related, seed) => related.Id)
            .CountAsync(cancellationToken);
        var rankingCount = await dbContext.RankingKeywords
            .CountAsync(entity => seedKeywordIds.Contains(entity.SeedKeywordId), cancellationToken);
        var lsiCount = await dbContext.LsiPaaItems
            .CountAsync(entity => seedKeywordIds.Contains(entity.SeedKeywordId), cancellationToken);
        var questionCount = await dbContext.Questions.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var influxCount = await dbContext.InfluxKeywordResults.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var contentCount = await dbContext.ContentSearchResults.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var headlineCount = await dbContext.SerpHeadlinePages.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var coOccurrenceCount = await dbContext.CoOccurrenceWords.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);
        var scoreCount = await dbContext.ProjectKeywordScores.CountAsync(entity => entity.ProjectId == projectId, cancellationToken);

        return string.Join(
            ":",
            seedCount,
            suggestionCount,
            relatedCount,
            rankingCount,
            lsiCount,
            questionCount,
            influxCount,
            contentCount,
            headlineCount,
            coOccurrenceCount,
            scoreCount);
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

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static PagedResult<T> ToPagedResult<T>(IReadOnlyList<T> rows, SearchQuery query)
    {
        var page = query.EffectivePage;
        return new PagedResult<T>(
            rows.Skip(page.Offset).Take(page.PageSize).ToArray(),
            page.Page,
            page.PageSize,
            rows.LongCount());
    }

    private static IEnumerable<TopicClusterView> SortClusters(
        IEnumerable<TopicClusterView> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "score";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "name" => SortString(rows, ascending, row => row.Cluster.Name),
            "representativeKeyword" => SortString(rows, ascending, row => row.RepresentativeKeyword),
            "keywordCount" => SortInt(rows, ascending, row => row.Keywords.Count),
            "childCount" => SortInt(rows, ascending, row => row.ChildCount),
            "updatedAt" => SortDateTime(rows, ascending, row => row.Cluster.UpdatedAt),
            "createdAt" => SortDateTime(rows, ascending, row => row.Cluster.CreatedAt),
            _ => SortDecimal(rows, ascending, row => row.Cluster.Score)
        };
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

    private static decimal CalculateClusterScore(IReadOnlyList<TopicMemberDraft> members)
        => Math.Round(members.Take(5).Average(member => member.ClusterScore), 4);

    private static decimal CalculateLexicalSimilarity(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0m;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var jaccard = union == 0 ? 0m : (decimal)intersection / union;

        var normalizedLeft = left.Trim();
        var normalizedRight = right.Trim();
        if (normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase) ||
            normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            jaccard = Math.Max(jaccard, 0.5m);
        }

        return Math.Round(Math.Min(1m, jaccard), 4);
    }

    private static IReadOnlySet<string> Tokenize(string value)
    {
        var tokens = value
            .Split([' ', '\t', '\r', '\n', '-', '_', '/', '\\', '|', ',', '.', ':', ';', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Where(token => token.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tokens.Count == 0 && !string.IsNullOrWhiteSpace(value))
        {
            tokens.Add(value.Trim().ToLowerInvariant());
        }

        return tokens;
    }

    private static string ClassifyIntent(string keyword, int faqCount)
    {
        var value = keyword.ToLowerInvariant();
        if (faqCount > 0 ||
            value.Contains("what", StringComparison.Ordinal) ||
            value.Contains("how", StringComparison.Ordinal) ||
            value.Contains("why", StringComparison.Ordinal) ||
            value.Contains("guide", StringComparison.Ordinal) ||
            value.Contains("tutorial", StringComparison.Ordinal) ||
            value.Contains("checklist", StringComparison.Ordinal) ||
            value.Contains("とは", StringComparison.Ordinal) ||
            value.Contains("方法", StringComparison.Ordinal) ||
            value.Contains("やり方", StringComparison.Ordinal))
        {
            return "informational";
        }

        if (value.Contains("price", StringComparison.Ordinal) ||
            value.Contains("cost", StringComparison.Ordinal) ||
            value.Contains("pricing", StringComparison.Ordinal) ||
            value.Contains("compare", StringComparison.Ordinal) ||
            value.Contains("best", StringComparison.Ordinal) ||
            value.Contains("料金", StringComparison.Ordinal) ||
            value.Contains("費用", StringComparison.Ordinal) ||
            value.Contains("比較", StringComparison.Ordinal) ||
            value.Contains("おすすめ", StringComparison.Ordinal))
        {
            return "commercial";
        }

        if (value.Contains("buy", StringComparison.Ordinal) ||
            value.Contains("order", StringComparison.Ordinal) ||
            value.Contains("purchase", StringComparison.Ordinal) ||
            value.Contains("購入", StringComparison.Ordinal) ||
            value.Contains("申し込み", StringComparison.Ordinal))
        {
            return "transactional";
        }

        if (value.Contains("login", StringComparison.Ordinal) ||
            value.Contains("ログイン", StringComparison.Ordinal))
        {
            return "navigational";
        }

        return "informational";
    }

    private static string? MajorityIntent(IReadOnlyList<TopicClusterKeywordProjection> keywords)
        => keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword.IntentLabel))
            .GroupBy(keyword => keyword.IntentLabel!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

    private static string RootTopicName(TopicClusterView cluster)
    {
        var name = cluster.ParentName ?? cluster.Cluster.Name;
        var separatorIndex = name.IndexOf(" - ", StringComparison.Ordinal);
        return separatorIndex > 0 ? name[..separatorIndex] : name;
    }

    private static Guid StableGuid(Guid projectId, params string[] parts)
    {
        var input = $"{projectId:N}:{string.Join(":", parts)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string? ReadSeedLanguage(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(memo);
            if (document.RootElement.TryGetProperty("request", out var request) &&
                request.ValueKind == JsonValueKind.Object &&
                request.TryGetProperty("language", out var language) &&
                language.ValueKind == JsonValueKind.String)
            {
                return OptionalText(language.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private readonly record struct MemberKey(Guid TopicKey, Guid KeywordId);

    private sealed record ProjectSeedTopic(Guid SeedId, Guid TopicKey, string TopicName, KeywordEntity Keyword);

    private sealed record TopicChoice(Guid TopicKey, string TopicName, decimal Similarity);

    private sealed record TopicClusterDraft(
        Guid ClusterId,
        Guid ProjectId,
        string Name,
        Guid? ParentId,
        Guid RepresentativeKeywordId,
        decimal Score,
        IReadOnlyList<TopicMemberDraft> Members);

    private sealed class TopicMemberDraft(Guid topicKey, string topicName, KeywordEntity keyword)
    {
        public Guid TopicKey { get; } = topicKey;

        public string TopicName { get; } = topicName;

        public KeywordEntity Keyword { get; } = keyword;

        public HashSet<string> Sources { get; } = new(StringComparer.Ordinal);

        public HashSet<string> RankedUrls { get; } = new(StringComparer.OrdinalIgnoreCase);

        public decimal Affinity { get; set; }

        public decimal RankingRelevance { get; set; }

        public decimal OpportunityScore { get; set; }

        public decimal CoRankingScore { get; set; }

        public int FaqCount { get; set; }

        public int CoOccurrenceCount { get; set; }

        public decimal LexicalSimilarity { get; set; }

        public string IntentLabel { get; set; } = "informational";

        public decimal ClusterScore { get; set; }
    }

    private sealed record TopicClusterView(
        TopicClusterEntity Cluster,
        string? ParentName,
        string? RepresentativeKeyword,
        IReadOnlyList<TopicClusterKeywordProjection> Keywords,
        string? IntentLabel,
        int ChildCount,
        IReadOnlyList<ArticleBriefEntity> Briefs);

    private sealed record TopicClusterKeywordProjection(
        Guid KeywordId,
        string Keyword,
        string Role,
        decimal OpportunityScore,
        string? IntentLabel,
        TopicKeywordEvidence Evidence);

    private sealed record TopicKeywordEvidence(decimal CoRankingScore, int FaqCount, IReadOnlyList<string> Sources)
    {
        public static TopicKeywordEvidence Empty { get; } = new(0m, 0, []);
    }

    private sealed class MutableTopicKeywordEvidence
    {
        public decimal CoRankingScore { get; set; }

        public int FaqCount { get; set; }

        public HashSet<string> Sources { get; } = new(StringComparer.Ordinal);
    }
}

internal sealed record NormalizedTopicClusterGenerateRequest(int Version, bool Regenerate);

internal sealed class TopicClusterGenerateJob(
    SeoIntelligenceDbContext dbContext,
    TopicClusterService topicClusterService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<TopicClusterGenerateJob> logger)
{
    public const string JobType = TopicClusterService.TopicClusterGenerateJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Topic cluster generate job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, TopicClusterService.TopicClusterGenerationResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Topic cluster generate job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Topic cluster generate job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await topicClusterService.ExecuteGenerateAsync(
                context,
                job.ResultResourceId.Value,
                ReadRegenerate(job));
            if (result.IsSuccess)
            {
                await jobService.CompleteAsync(
                    context,
                    jobId,
                    new JobCompletion(
                        100,
                        new JobResultResource(TopicClusterService.TopicClusterGenerationResourceType, job.ResultResourceId.Value)));
                return;
            }

            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, result.Error!.Code.ToString(), result.Error.Message));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Topic cluster generate job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Topic cluster generation could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Topic cluster generate job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Topic cluster generation failed unexpectedly."));
        }
    }

    private static bool ReadRegenerate(JobEntity job)
        => (job.IdempotencyKey ?? string.Empty)
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => string.Equals(segment, "regenerate=1", StringComparison.OrdinalIgnoreCase));
}
