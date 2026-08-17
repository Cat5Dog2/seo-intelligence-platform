using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

internal sealed class KeywordDiscoveryService(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    TimeProvider timeProvider)
    : IKeywordDiscoveryService
{
    public const string SeedSource = "keyword_discovery";
    public const string ResultResourceType = "keyword_seed";

    // よくある質問検索が相対需要を返さない場合の既定重要度。
    private const decimal DefaultQuestionImportance = 0.5m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DefaultSources = ["suggest", "related", "other", "question", "ranking"];
    private static readonly IReadOnlyList<string> DefaultEngines = ["google"];
    private static readonly HashSet<string> AllowedSources = DefaultSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "keyword",
        "source",
        "suggestClass",
        "searchVolume",
        "seoDifficulty",
        "cpc",
        "competition",
        "firstSeenRange",
        "opportunityScore",
        "importance",
        "relevance"
    };

    public async Task<Result<KeywordDiscoveryResult>> DiscoverAsync(
        ProjectExecutionContext context,
        KeywordDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeRequestAsync(context, request, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<KeywordDiscoveryResult>.Failure(normalized.Error);
        }

        var discoveryRequest = normalized.Request!;
        if (ShouldRunAsync(discoveryRequest))
        {
            return await QueueAsync(context, discoveryRequest, cancellationToken);
        }

        return await ExecuteAndSaveAsync(
            context,
            discoveryRequest,
            jobId: null,
            seedId: null,
            skipFetchedSources: false,
            cancellationToken);
    }

    public async Task<Result<KeywordDiscoveryResult>> ExecuteQueuedAsync(
        ProjectExecutionContext context,
        Guid jobId,
        Guid seedId,
        NormalizedKeywordDiscoveryRequest request,
        CancellationToken cancellationToken = default)
        => await ExecuteAndSaveAsync(
            context,
            request,
            jobId,
            seedId,
            skipFetchedSources: true,
            cancellationToken);

    public async Task<KeywordDiscoverySeedMemo?> ReadSeedMemoAsync(
        Guid seedId,
        CancellationToken cancellationToken = default)
    {
        var memo = await dbContext.KeywordSeeds
            .AsNoTracking()
            .Where(entity => entity.Id == seedId)
            .Select(entity => entity.Memo)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(memo)
            ? null
            : JsonSerializer.Deserialize<KeywordDiscoverySeedMemo>(memo, JsonOptions);
    }

    private async Task<Result<KeywordDiscoveryResult>> QueueAsync(
        ProjectExecutionContext context,
        NormalizedKeywordDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Jobs
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId &&
                entity.JobType == KeywordDiscoveryJob.JobType &&
                entity.IdempotencyKey == request.IdempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, request.RequestHash, StringComparison.Ordinal))
            {
                return Failure<KeywordDiscoveryResult>(
                    ErrorCode.Conflict,
                    "Idempotency-Key was already used for a different request hash.");
            }

            return Result<KeywordDiscoveryResult>.Success(AcceptedResult(request, existing.Id, existing.Status));
        }

        var now = NowUtc();
        var seed = new KeywordSeedEntity
        {
            Id = UuidV7.New(),
            ProjectId = context.ProjectId!.Value,
            Seed = request.SeedKeyword,
            Source = SeedSource,
            Memo = JsonSerializer.Serialize(
                new KeywordDiscoverySeedMemo(1, request.IdempotencyKey, request.RequestHash, request),
                JsonOptions),
            CreatedAt = now
        };
        dbContext.KeywordSeeds.Add(seed);
        await dbContext.SaveChangesAsync(cancellationToken);

        using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                KeywordDiscoveryJob.JobType,
                payloadDocument.RootElement.Clone(),
                RequestHash: request.RequestHash,
                IdempotencyKey: request.IdempotencyKey,
                TargetKey: seed.Id.ToString("N"),
                Queue: "external-api",
                InitialResource: new JobResultResource(ResultResourceType, seed.Id)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<KeywordDiscoveryResult>.Success(AcceptedResult(request, registration.Value!.JobId, registration.Value.Status))
            : Result<KeywordDiscoveryResult>.Failure(registration.Error!);
    }

    private async Task<Result<KeywordDiscoveryResult>> ExecuteAndSaveAsync(
        ProjectExecutionContext context,
        NormalizedKeywordDiscoveryRequest request,
        Guid? jobId,
        Guid? seedId,
        bool skipFetchedSources,
        CancellationToken cancellationToken)
    {
        var seed = seedId.HasValue
            ? await dbContext.KeywordSeeds.SingleOrDefaultAsync(entity => entity.Id == seedId.Value, cancellationToken)
            : await CreateSeedAsync(context, request, cancellationToken);
        if (seed is null || seed.ProjectId != context.ProjectId)
        {
            return Failure<KeywordDiscoveryResult>(ErrorCode.NotFound, "Keyword discovery seed was not found.");
        }

        var seedKeyword = await EnsureKeywordAsync(request.SeedKeyword, request.Language, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var collected = new List<KeywordCandidate>();
        var statuses = new List<KeywordDiscoverySourceStatus>();
        var consumedCredit = 0m;
        SourceFailure? firstFailure = null;

        foreach (var source in request.Sources)
        {
            if (skipFetchedSources && await SourceAlreadyFetchedAsync(seed, seedKeyword.Id, source, cancellationToken))
            {
                statuses.Add(new KeywordDiscoverySourceStatus(source, StatusValues.Succeeded, CandidateCount: 0));
                continue;
            }

            var outcome = await FetchSourceAsync(context, jobId, request, source, cancellationToken);
            consumedCredit += outcome.ConsumedCredit;

            if (outcome.Failure is not null)
            {
                firstFailure = outcome.Failure;
                statuses.Add(new KeywordDiscoverySourceStatus(
                    source,
                    outcome.Failure.Retryable ? StatusValues.FailedRetryable : StatusValues.FailedFatal,
                    CandidateCount: 0,
                    outcome.ConsumedCredit,
                    outcome.Failure.StatusCode,
                    outcome.Failure.ErrorCode,
                    outcome.Failure.Message));
                break;
            }

            var candidates = await SaveSourceResultAsync(
                context,
                request,
                seed,
                seedKeyword,
                source,
                outcome,
                cancellationToken);
            collected.AddRange(candidates);
            statuses.Add(new KeywordDiscoverySourceStatus(source, StatusValues.Succeeded, candidates.Count, outcome.ConsumedCredit, outcome.StatusCode));
        }

        var filteredCandidates = ApplyFilterAndSort(collected, request).ToArray();
        var result = new KeywordDiscoveryResult(
            filteredCandidates,
            seed.Id,
            seedKeyword.Id,
            request.SeedKeyword,
            request.Location,
            request.Language,
            request.Sources,
            IsAccepted: false,
            JobId: jobId,
            StatusUrl: jobId.HasValue ? $"/api/jobs/{jobId.Value:D}" : null,
            SourceStatuses: statuses,
            consumedCredit);

        return firstFailure is null
            ? Result<KeywordDiscoveryResult>.Success(result)
            : Result<KeywordDiscoveryResult>.Failure(ToError(firstFailure));
    }

    private async Task<KeywordSeedEntity> CreateSeedAsync(
        ProjectExecutionContext context,
        NormalizedKeywordDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var seed = new KeywordSeedEntity
        {
            Id = UuidV7.New(),
            ProjectId = context.ProjectId!.Value,
            Seed = request.SeedKeyword,
            Source = SeedSource,
            Memo = JsonSerializer.Serialize(
                new KeywordDiscoverySeedMemo(1, request.IdempotencyKey, request.RequestHash, request),
                JsonOptions),
            CreatedAt = NowUtc()
        };
        dbContext.KeywordSeeds.Add(seed);
        await dbContext.SaveChangesAsync(cancellationToken);
        return seed;
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

    private async Task<SourceOutcome> FetchSourceAsync(
        ProjectExecutionContext context,
        Guid? jobId,
        NormalizedKeywordDiscoveryRequest request,
        string source,
        CancellationToken cancellationToken)
    {
        var clientContext = CreateClientContext(context, jobId);

        return source switch
        {
            "suggest" => ToOutcome(await rakkoKeywordClient.GetSuggestKeywordsAsync(
                clientContext,
                new RakkoSuggestKeywordsRequest(
                    request.SeedKeyword,
                    request.Engines,
                    IncreaseKeyword: false,
                    request.Limit,
                    ExternalSortBy("suggest", request.SortBy),
                    request.OrderBy),
                cancellationToken)),
            "related" => ToOutcome(await rakkoKeywordClient.GetRelatedKeywordsAsync(
                clientContext,
                new RakkoRelatedKeywordsRequest(
                    request.SeedKeyword,
                    Limit: request.Limit,
                    SortBy: ExternalSortBy("related", request.SortBy),
                    OrderBy: request.OrderBy),
                cancellationToken)),
            "other" => ToOutcome(await rakkoKeywordClient.GetOtherKeywordsAsync(
                clientContext,
                new RakkoOtherKeywordsRequest(request.SeedKeyword, ExternalSortBy("other", request.SortBy), request.OrderBy),
                cancellationToken)),
            "question" => ToOutcome(await rakkoKeywordClient.GetQuestionsAsync(
                clientContext,
                new RakkoQuestionSearchRequest(request.SeedKeyword, request.Limit),
                cancellationToken)),
            "ranking" => ToOutcome(await rakkoKeywordClient.GetRankingKeywordsAsync(
                clientContext,
                new RakkoRankingKeywordsRequest(
                    request.SeedKeyword,
                    Limit: request.Limit,
                    SortBy: ExternalSortBy("ranking", request.SortBy),
                    OrderBy: request.OrderBy),
                cancellationToken)),
            _ => new SourceOutcome(
                Candidates: null,
                Questions: null,
                ConsumedCredit: 0,
                StatusCode: 400,
                Failure: new SourceFailure(400, "invalid_source", $"Unsupported source '{source}'.", Retryable: false))
        };
    }

    private async Task<IReadOnlyList<KeywordCandidate>> SaveSourceResultAsync(
        ProjectExecutionContext context,
        NormalizedKeywordDiscoveryRequest request,
        KeywordSeedEntity seed,
        KeywordEntity seedKeyword,
        string source,
        SourceOutcome outcome,
        CancellationToken cancellationToken)
    {
        var candidates = new List<KeywordCandidate>();
        if (source == "question")
        {
            foreach (var question in outcome.Questions?.Items ?? [])
            {
                var questionText = OptionalText(question.Question);
                if (questionText is null)
                {
                    continue;
                }

                var importance = ToQuestionImportance(question.RelativeDemand);
                dbContext.Questions.Add(new QuestionEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = context.ProjectId!.Value,
                    SeedKeywordId = seedKeyword.Id,
                    QuestionText = questionText,
                    Source = "question",
                    Importance = importance,
                    FirstSeenRange = OptionalText(question.FirstSeenRange),
                    CreatedAt = NowUtc()
                });
                candidates.Add(new KeywordCandidate(
                    questionText,
                    "question",
                    SuggestClass: null,
                    OpportunityScore: 5m,
                    KeywordId: null,
                    Type: "faq",
                    Question: questionText,
                    Importance: importance));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return candidates;
        }

        foreach (var item in outcome.Candidates?.Items ?? [])
        {
            switch (source)
            {
                case "suggest":
                    await SaveSuggestionAsync(request, seed, item, candidates, cancellationToken);
                    break;
                case "related":
                    await SaveRelatedAsync(request, seed, item, candidates, cancellationToken);
                    break;
                case "other":
                    await SaveOtherAsync(request, seedKeyword, item, candidates, cancellationToken);
                    break;
                case "ranking":
                    await SaveRankingAsync(request, seedKeyword, item, candidates, cancellationToken);
                    break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return candidates;
    }

    private async Task SaveSuggestionAsync(
        NormalizedKeywordDiscoveryRequest request,
        KeywordSeedEntity seed,
        RakkoKeywordCandidate item,
        ICollection<KeywordCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var keywordText = OptionalText(item.Keyword);
        if (keywordText is null)
        {
            return;
        }

        var keyword = await EnsureKeywordAsync(keywordText, request.Language, cancellationToken);
        var engines = item.Engines.Count > 0 ? item.Engines : request.Engines;
        foreach (var engine in engines)
        {
            dbContext.KeywordSuggestions.Add(new KeywordSuggestionEntity
            {
                Id = UuidV7.New(),
                SeedId = seed.Id,
                KeywordId = keyword.Id,
                Engine = engine,
                SuggestClass = item.SuggestClass ?? string.Empty,
                EngineCount = engines.Count,
                FirstSeenRange = item.Metrics?.FirstSeenRange,
                CreatedAt = NowUtc()
            });
        }

        candidates.Add(ToCandidate(keyword, item, "suggest", item.SuggestClass, engine: string.Join(",", engines)));
    }

    private async Task SaveRelatedAsync(
        NormalizedKeywordDiscoveryRequest request,
        KeywordSeedEntity seed,
        RakkoKeywordCandidate item,
        ICollection<KeywordCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var keywordText = OptionalText(item.Keyword);
        if (keywordText is null)
        {
            return;
        }

        var keyword = await EnsureKeywordAsync(keywordText, request.Language, cancellationToken);
        dbContext.RelatedKeywords.Add(new RelatedKeywordEntity
        {
            Id = UuidV7.New(),
            SeedId = seed.Id,
            KeywordId = keyword.Id,
            MatchType = "partialMatch",
            MetricsSnapshotJson = SerializeMetrics(item.Metrics),
            CreatedAt = NowUtc()
        });
        candidates.Add(ToCandidate(keyword, item, "related", SuggestClass: null));
    }

    private async Task SaveOtherAsync(
        NormalizedKeywordDiscoveryRequest request,
        KeywordEntity seedKeyword,
        RakkoKeywordCandidate item,
        ICollection<KeywordCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var keywordText = OptionalText(item.Keyword);
        KeywordEntity? keyword = null;
        if (keywordText is not null)
        {
            keyword = await EnsureKeywordAsync(keywordText, request.Language, cancellationToken);
        }

        var questionText = OptionalText(item.Question);
        var importance = ParseImportance(item.Importance);
        dbContext.LsiPaaItems.Add(new LsiPaaItemEntity
        {
            Id = UuidV7.New(),
            SeedKeywordId = seedKeyword.Id,
            Type = OptionalText(item.Type) ?? (questionText is null ? "lsi" : "paa"),
            KeywordId = keyword?.Id,
            QuestionText = questionText,
            Importance = importance,
            CreatedAt = NowUtc()
        });

        if (keyword is not null)
        {
            candidates.Add(ToCandidate(keyword, item, "other", SuggestClass: null, importance: importance));
        }
        else if (questionText is not null)
        {
            candidates.Add(new KeywordCandidate(
                questionText,
                "other",
                SuggestClass: null,
                OpportunityScore: CalculateOpportunityScore(item.Metrics, importance, item.Relevance),
                KeywordId: null,
                Type: item.Type,
                Question: questionText,
                SearchVolume: item.Metrics?.SearchVolume,
                SeoDifficulty: item.Metrics?.SeoDifficulty,
                Cpc: item.Metrics?.Cpc,
                Competition: item.Metrics?.Competition,
                FirstSeenRange: item.Metrics?.FirstSeenRange,
                Importance: importance,
                Relevance: item.Relevance));
        }
    }

    private async Task SaveRankingAsync(
        NormalizedKeywordDiscoveryRequest request,
        KeywordEntity seedKeyword,
        RakkoKeywordCandidate item,
        ICollection<KeywordCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var keywordText = OptionalText(item.Keyword);
        if (keywordText is null)
        {
            return;
        }

        var keyword = await EnsureKeywordAsync(keywordText, request.Language, cancellationToken);
        dbContext.RankingKeywords.Add(new RankingKeywordEntity
        {
            Id = UuidV7.New(),
            SeedKeywordId = seedKeyword.Id,
            KeywordId = keyword.Id,
            WordCount = Convert.ToInt32(item.WordCount ?? 0, CultureInfo.InvariantCulture),
            Relevance = item.Relevance ?? 0,
            MetricsSnapshotJson = SerializeMetrics(item.Metrics),
            CreatedAt = NowUtc()
        });
        candidates.Add(ToCandidate(keyword, item, "ranking", SuggestClass: null));
    }

    private async Task<bool> SourceAlreadyFetchedAsync(
        KeywordSeedEntity seed,
        Guid seedKeywordId,
        string source,
        CancellationToken cancellationToken)
    {
        var createdAt = seed.CreatedAt.AddSeconds(-1);
        return source switch
        {
            "suggest" => await dbContext.KeywordSuggestions.AnyAsync(entity => entity.SeedId == seed.Id, cancellationToken),
            "related" => await dbContext.RelatedKeywords.AnyAsync(entity => entity.SeedId == seed.Id, cancellationToken),
            "other" => await dbContext.LsiPaaItems.AnyAsync(entity => entity.SeedKeywordId == seedKeywordId && entity.CreatedAt >= createdAt, cancellationToken),
            "question" => await dbContext.Questions.AnyAsync(entity => entity.SeedKeywordId == seedKeywordId && entity.CreatedAt >= createdAt, cancellationToken),
            "ranking" => await dbContext.RankingKeywords.AnyAsync(entity => entity.SeedKeywordId == seedKeywordId && entity.CreatedAt >= createdAt, cancellationToken),
            _ => false
        };
    }

    private async Task<NormalizeResult> NormalizeRequestAsync(
        ProjectExecutionContext context,
        KeywordDiscoveryRequest request,
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
            .FirstOrDefaultAsync(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.Id == context.ProjectId.Value &&
                entity.Status == StatusValues.Active,
                cancellationToken);
        if (project is null)
        {
            return new NormalizeResult(null, new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var seedCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.SeedKeyword))
        {
            seedCandidates.Add(request.SeedKeyword);
        }

        if (request.Seeds is not null)
        {
            seedCandidates.AddRange(request.Seeds.Where(seed => !string.IsNullOrWhiteSpace(seed)));
        }

        var seeds = KeywordNormalizer.NormalizeMany(seedCandidates);
        if (seeds.Count == 0)
        {
            errors.Add("seedKeyword", "seedKeyword is required.");
        }
        else if (seeds.Count > 1)
        {
            errors.Add("seedKeyword", "Keyword discovery accepts exactly one seed keyword.");
        }

        var sources = NormalizeList(request.Sources, DefaultSources, value => value.ToLowerInvariant());
        foreach (var source in sources)
        {
            if (!AllowedSources.Contains(source))
            {
                errors.Add("sources", "sources must contain suggest, related, other, question, or ranking.");
            }
        }

        var engines = NormalizeList(request.Engines, DefaultEngines, value => value.ToLowerInvariant());
        var location = OptionalText(request.Location) ?? project.DefaultLocation;
        var language = OptionalText(request.Language) ?? project.DefaultLanguage;
        var limit = request.Limit ?? 20;
        if (limit is < 1 or > 100)
        {
            errors.Add("limit", "limit must be between 1 and 100.");
        }

        var sortBy = CanonicalSortBy(OptionalText(request.SortBy) ?? "opportunityScore");
        if (sortBy is null)
        {
            errors.Add("sortBy", "sortBy is not supported for keyword discovery.");
        }

        var orderBy = OptionalText(request.OrderBy)?.ToLowerInvariant() ?? "desc";
        if (orderBy is not "asc" and not "desc")
        {
            errors.Add("orderBy", "orderBy must be asc or desc.");
        }

        if (errors.HasErrors)
        {
            return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var filterHash = HashText(JsonSerializer.Serialize(request.Filter, JsonOptions));
        var payload = new NormalizedKeywordDiscoveryRequest(
            seeds[0],
            sources,
            engines,
            location,
            language,
            limit,
            request.Filter,
            sortBy!,
            orderBy,
            request.SyncPreferred ?? true,
            IdempotencyKey: BuildIdempotencyKey(context.ProjectId.Value, seeds[0], sources, filterHash),
            RequestHash: string.Empty);
        payload = payload with { RequestHash = HashText(JsonSerializer.Serialize(payload, JsonOptions)) };
        return new NormalizeResult(payload, null);
    }

    private static bool ShouldRunAsync(NormalizedKeywordDiscoveryRequest request)
        => !request.SyncPreferred || request.Limit > 50;

    private static KeywordDiscoveryResult AcceptedResult(
        NormalizedKeywordDiscoveryRequest request,
        Guid jobId,
        string status)
        => new(
            Candidates: [],
            SeedKeyword: request.SeedKeyword,
            Location: request.Location,
            Language: request.Language,
            Sources: request.Sources,
            IsAccepted: true,
            JobId: jobId,
            StatusUrl: $"/api/jobs/{jobId:D}",
            SourceStatuses:
            [
                new KeywordDiscoverySourceStatus("job", status, CandidateCount: 0)
            ]);

    private static SourceOutcome ToOutcome(RakkoKeywordCallResult<RakkoKeywordCandidates> result)
        => result.IsSuccess
            ? new SourceOutcome(result.Data, null, result.ConsumedCredit, result.StatusCode, Failure: null)
            : new SourceOutcome(
                Candidates: null,
                Questions: null,
                result.ConsumedCredit,
                result.StatusCode,
                new SourceFailure(
                    result.StatusCode,
                    result.ExternalCall.ErrorCode,
                    result.Errors.FirstOrDefault() ?? "Keyword discovery source failed.",
                    result.IsRetryable));

    private static SourceOutcome ToOutcome(RakkoKeywordCallResult<RakkoQuestions> result)
        => result.IsSuccess
            ? new SourceOutcome(null, result.Data, result.ConsumedCredit, result.StatusCode, Failure: null)
            : new SourceOutcome(
                Candidates: null,
                Questions: null,
                result.ConsumedCredit,
                result.StatusCode,
                new SourceFailure(
                    result.StatusCode,
                    result.ExternalCall.ErrorCode,
                    result.Errors.FirstOrDefault() ?? "Keyword discovery source failed.",
                    result.IsRetryable));

    private static Error ToError(SourceFailure failure)
        => new(
            failure.Retryable ? ErrorCode.ExternalTemporaryFailure : ErrorCode.ExternalFatalFailure,
            failure.Message,
            new Dictionary<string, string[]>
            {
                ["statusCode"] = [failure.StatusCode.ToString(CultureInfo.InvariantCulture)],
                ["errorCode"] = [failure.ErrorCode ?? string.Empty]
            });

    private static IEnumerable<KeywordCandidate> ApplyFilterAndSort(
        IEnumerable<KeywordCandidate> candidates,
        NormalizedKeywordDiscoveryRequest request)
    {
        var filtered = candidates.Where(candidate => MatchesFilter(candidate, request.Filter));
        var ascending = string.Equals(request.OrderBy, "asc", StringComparison.OrdinalIgnoreCase);

        return request.SortBy switch
        {
            "keyword" => ascending
                ? filtered.OrderBy(candidate => candidate.Keyword, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(candidate => candidate.Keyword, StringComparer.OrdinalIgnoreCase),
            "source" => ascending
                ? filtered.OrderBy(candidate => candidate.Source, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(candidate => candidate.Source, StringComparer.OrdinalIgnoreCase),
            "suggestClass" => SortString(filtered, ascending, candidate => candidate.SuggestClass),
            "searchVolume" => SortDecimal(filtered, ascending, candidate => candidate.SearchVolume),
            "seoDifficulty" => SortDecimal(filtered, ascending, candidate => candidate.SeoDifficulty),
            "cpc" => SortDecimal(filtered, ascending, candidate => candidate.Cpc),
            "competition" => SortDecimal(filtered, ascending, candidate => candidate.Competition),
            "firstSeenRange" => SortString(filtered, ascending, candidate => candidate.FirstSeenRange),
            "importance" => SortDecimal(filtered, ascending, candidate => candidate.Importance),
            "relevance" => SortDecimal(filtered, ascending, candidate => candidate.Relevance),
            _ => SortDecimal(filtered, ascending, candidate => candidate.OpportunityScore)
        };
    }

    private static bool MatchesFilter(KeywordCandidate candidate, KeywordDiscoveryFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }

        if (!MatchesRange(candidate.SearchVolume, filter.MinSearchVolume, filter.MaxSearchVolume) ||
            !MatchesRange(candidate.SeoDifficulty, filter.MinSeoDifficulty, filter.MaxSeoDifficulty) ||
            !MatchesRange(candidate.Cpc, filter.MinCpc, filter.MaxCpc) ||
            !MatchesRange(candidate.Competition, filter.MinCompetition, filter.MaxCompetition))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.FirstSeenRange) &&
            !string.Equals(candidate.FirstSeenRange, filter.FirstSeenRange.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.SuggestClass) &&
            !string.Equals(candidate.SuggestClass, filter.SuggestClass.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var searchableText = $"{candidate.Keyword}\n{candidate.Question}";
        if (filter.Include is { Count: > 0 } &&
            !filter.Include
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => searchableText.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (filter.Exclude is { Count: > 0 } &&
            filter.Exclude
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => searchableText.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRange(decimal? value, decimal? min, decimal? max)
    {
        if (!min.HasValue && !max.HasValue)
        {
            return true;
        }

        if (!value.HasValue)
        {
            return false;
        }

        return (!min.HasValue || value.Value >= min.Value) &&
            (!max.HasValue || value.Value <= max.Value);
    }

    private static IOrderedEnumerable<KeywordCandidate> SortDecimal(
        IEnumerable<KeywordCandidate> candidates,
        bool ascending,
        Func<KeywordCandidate, decimal?> selector)
        => ascending
            ? candidates.OrderBy(candidate => selector(candidate) ?? decimal.MaxValue)
            : candidates.OrderByDescending(candidate => selector(candidate) ?? decimal.MinValue);

    private static IOrderedEnumerable<KeywordCandidate> SortString(
        IEnumerable<KeywordCandidate> candidates,
        bool ascending,
        Func<KeywordCandidate, string?> selector)
        => ascending
            ? candidates.OrderBy(candidate => selector(candidate) ?? "\uFFFF", StringComparer.OrdinalIgnoreCase)
            : candidates.OrderByDescending(candidate => selector(candidate) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static KeywordCandidate ToCandidate(
        KeywordEntity keyword,
        RakkoKeywordCandidate item,
        string source,
        string? SuggestClass,
        string? engine = null,
        decimal? importance = null)
        => new(
            keyword.NormalizedText,
            source,
            SuggestClass,
            CalculateOpportunityScore(item.Metrics, importance, item.Relevance),
            keyword.Id,
            item.Type,
            item.Question,
            engine,
            item.Engines.Count > 0 ? item.Engines.Count : null,
            item.Metrics?.SearchVolume,
            item.Metrics?.SeoDifficulty,
            item.Metrics?.Cpc,
            item.Metrics?.Competition,
            item.Metrics?.FirstSeenRange,
            importance,
            item.WordCount.HasValue ? Convert.ToInt32(item.WordCount.Value, CultureInfo.InvariantCulture) : null,
            item.Relevance);

    private static decimal? CalculateOpportunityScore(
        RakkoKeywordMetrics? metrics,
        decimal? importance = null,
        decimal? relevance = null)
    {
        if (metrics is null && !importance.HasValue && !relevance.HasValue)
        {
            return null;
        }

        var volumeScore = Math.Min((metrics?.SearchVolume ?? 0) / 100m, 40m);
        var difficultyScore = metrics?.SeoDifficulty is null ? 0m : Math.Max(0m, 100m - metrics.SeoDifficulty.Value) * 0.35m;
        var competitionScore = metrics?.Competition is null ? 0m : Math.Max(0m, 100m - metrics.Competition.Value) * 0.10m;
        var importanceScore = (importance ?? 0m) * 10m;
        var relevanceScore = (relevance ?? 0m) * 0.15m;
        return Math.Round(Math.Min(100m, volumeScore + difficultyScore + competitionScore + importanceScore + relevanceScore), 4);
    }

    private static decimal ParseImportance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "high" => 0.8m,
            "medium" => 0.5m,
            "low" => 0.2m,
            _ => 0m
        };
    }

    private static string SerializeMetrics(RakkoKeywordMetrics? metrics)
        => JsonSerializer.Serialize(
            (object)(metrics is null
                ? new { }
                : new
                {
                    metrics.SearchVolume,
                    metrics.SeoDifficulty,
                    metrics.Cpc,
                    metrics.Competition,
                    metrics.FirstSeenRange
                }),
            JsonOptions);

    private static IReadOnlyList<string> NormalizeList(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> defaults,
        Func<string, string> normalize)
    {
        var normalized = values?
            .Select(OptionalText)
            .Where(value => value is not null)
            .Select(value => normalize(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized is { Length: > 0 } ? normalized : defaults;
    }

    private static string? CanonicalSortBy(string value)
        => AllowedSortFields.FirstOrDefault(field => string.Equals(field, value, StringComparison.OrdinalIgnoreCase));

    private static string BuildIdempotencyKey(
        Guid projectId,
        string seedKeyword,
        IReadOnlyList<string> sources,
        string filterHash)
        => $"keyword-discovery:{projectId:N}:{HashText($"{projectId:N}\n{seedKeyword}\n{string.Join(",", sources)}\n{filterHash}")}";

    private static string ExternalSortBy(string source, string requestedSortBy)
        => source switch
        {
            "other" => "importance",
            "ranking" => "relevance",
            _ when requestedSortBy is "searchVolume" or "seoDifficulty" or "cpc" or "competition" or "firstSeenRange" => requestedSortBy,
            _ => "searchVolume"
        };

    private static RakkoKeywordClientContext CreateClientContext(
        ProjectExecutionContext context,
        Guid? jobId)
        => new(
            context.WorkspaceId,
            context.ProjectId,
            jobId,
            ApiContractScopeId: SeoIntelligenceSeedData.DefaultRakkoContractScopeId,
            ContractScopeKey: SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CorrelationId: context.CorrelationId,
            Actor: context.Actor);

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ラッコキーワードAPI v1.14.0のrelativeDemandは検索結果内での1〜100の相対値。
    // importanceは0〜1で保持するため100で割る。相対需要が無い場合は既定値を使う。
    private static decimal ToQuestionImportance(decimal? relativeDemand)
        => relativeDemand is null
            ? DefaultQuestionImportance
            : Math.Clamp(relativeDemand.Value / 100m, 0m, 1m);

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record NormalizeResult(NormalizedKeywordDiscoveryRequest? Request, Error? Error);

    private sealed record SourceOutcome(
        RakkoKeywordCandidates? Candidates,
        RakkoQuestions? Questions,
        decimal ConsumedCredit,
        int StatusCode,
        SourceFailure? Failure);

    private sealed record SourceFailure(
        int StatusCode,
        string? ErrorCode,
        string Message,
        bool Retryable);

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
}

internal sealed record KeywordDiscoverySeedMemo(
    int Version,
    string IdempotencyKey,
    string RequestHash,
    NormalizedKeywordDiscoveryRequest Request);

internal sealed record NormalizedKeywordDiscoveryRequest(
    string SeedKeyword,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Engines,
    string Location,
    string Language,
    int Limit,
    KeywordDiscoveryFilter? Filter,
    string SortBy,
    string OrderBy,
    bool SyncPreferred,
    string IdempotencyKey,
    string RequestHash);

internal sealed class KeywordDiscoveryJob(
    SeoIntelligenceDbContext dbContext,
    KeywordDiscoveryService keywordDiscoveryService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<KeywordDiscoveryJob> logger)
{
    public const string JobType = "KeywordDiscoveryJob";

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Keyword discovery job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, KeywordDiscoveryService.ResultResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Keyword discovery job payload was missing."));
            return;
        }

        var memo = await keywordDiscoveryService.ReadSeedMemoAsync(job.ResultResourceId.Value);
        if (memo is null)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_seed_payload", "Keyword discovery seed payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Keyword discovery job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await keywordDiscoveryService.ExecuteQueuedAsync(
                context,
                jobId,
                job.ResultResourceId.Value,
                memo.Request);

            if (result.IsSuccess)
            {
                await jobService.CompleteAsync(
                    context,
                    jobId,
                    new JobCompletion(
                        Progress: 100,
                        new JobResultResource(KeywordDiscoveryService.ResultResourceType, job.ResultResourceId.Value)));
                return;
            }

            await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Keyword discovery job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Keyword discovery could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Keyword discovery job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Keyword discovery failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
    {
        var statusCode = TryReadStatusCode(error) ??
            (error.Code is ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited ? 429 : 400);
        var errorCode = TryReadDetail(error, "errorCode");
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
