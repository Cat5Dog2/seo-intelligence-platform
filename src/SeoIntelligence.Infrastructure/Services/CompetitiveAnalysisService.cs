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

internal sealed class CompetitiveAnalysisService(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    TimeProvider timeProvider)
    : ICompetitiveAnalysisService
{
    public const string JobType = "CompetitorRefreshJob";
    public const string TargetSiteResourceType = "site";
    private const int ExternalResultLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<JobReference>> AnalyzeAsync(
        ProjectExecutionContext context,
        CompetitorAnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeAnalyzeRequestAsync(context, request, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<JobReference>.Failure(normalized.Error);
        }

        var analyzeRequest = normalized.Request!;
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(analyzeRequest, JsonOptions));
        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                JobType,
                payload.RootElement.Clone(),
                RequestHash: analyzeRequest.RequestHash,
                IdempotencyKey: analyzeRequest.IdempotencyKey,
                TargetKey: analyzeRequest.SiteId.ToString("N"),
                Queue: "external-api",
                InitialResource: new JobResultResource(TargetSiteResourceType, analyzeRequest.SiteId)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<PagedResult<CompetitorResultRow>>> GetCompetitorsAsync(
        ProjectExecutionContext context,
        CompetitorSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<CompetitorResultRow>>(ErrorCode.NotFound, "Project was not found.");
        }

        var savedDomains = await dbContext.CompetitorSites
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .Select(entity => entity.Domain)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var entities = await dbContext.CompetitiveResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);

        var rows = entities.Select(entity => MapCompetitorRow(entity, savedDomains.Contains(entity.SiteDomain)));
        var domain = OptionalText(query.Domain);
        if (domain is not null)
        {
            rows = rows.Where(row => row.Domain.Contains(domain, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row => row.Domain.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortCompetitors(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<CompetitorResultRow>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<PagedResult<InfluxKeywordResultRow>>> GetInfluxKeywordsAsync(
        ProjectExecutionContext context,
        InfluxKeywordSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<InfluxKeywordResultRow>>(ErrorCode.NotFound, "Project was not found.");
        }

        var ownDomains = await dbContext.Sites
            .AsNoTracking()
            .Where(entity =>
                entity.ProjectId == project.Id &&
                entity.Type == "own" &&
                entity.Status == StatusValues.Active)
            .Select(entity => entity.Domain)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var projections = await dbContext.InfluxKeywordResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                result => result.KeywordId,
                keyword => keyword.Id,
                (result, keyword) => new InfluxKeywordProjection(result, keyword))
            .ToArrayAsync(cancellationToken);

        var rows = projections.Select(projection => MapInfluxKeywordRow(projection, ownDomains));
        rows = FilterInfluxKeywords(rows, query);
        rows = SortInfluxKeywords(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<InfluxKeywordResultRow>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<PagedResult<InfluxPageResultRow>>> GetInfluxPagesAsync(
        ProjectExecutionContext context,
        InfluxPageSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<InfluxPageResultRow>>(ErrorCode.NotFound, "Project was not found.");
        }

        var topKeywordIds = await dbContext.InfluxPageResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id && entity.TopKeywordId.HasValue)
            .Select(entity => entity.TopKeywordId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var topKeywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => topKeywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);

        var entities = await dbContext.InfluxPageResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);

        var rows = entities.Select(entity => MapInfluxPageRow(entity, topKeywords));
        rows = FilterInfluxPages(rows, query);
        rows = SortInfluxPages(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<InfluxPageResultRow>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<JobReference>> ExecuteQueuedAsync(
        ProjectExecutionContext context,
        Guid jobId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        var site = await dbContext.Sites
            .SingleOrDefaultAsync(
                entity =>
                    entity.ProjectId == project.Id &&
                    entity.Id == siteId &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
        if (site is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Target site was not found.");
        }

        var clientContext = CreateClientContext(context, jobId);
        var competitive = await rakkoKeywordClient.GetCompetitiveSitesAsync(
            clientContext,
            new RakkoCompetitiveRequest(site.Domain),
            cancellationToken);
        if (!competitive.IsSuccess || competitive.Data is null)
        {
            return Result<JobReference>.Failure(ToExternalError(competitive, "Competitive analysis external API call failed."));
        }

        var competitorDomains = await SaveCompetitiveResultsAsync(project.Id, competitive.Data, cancellationToken);
        var targets = new[] { site.Domain }
            .Concat(competitorDomains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(target => new RakkoApiTargetRequest(target))
            .ToArray();

        var influxKeywords = await rakkoKeywordClient.GetInfluxKeywordsAsync(
            clientContext,
            new RakkoInfluxKeywordsRequest(targets, Limit: ExternalResultLimit),
            cancellationToken);
        if (!influxKeywords.IsSuccess || influxKeywords.Data is null)
        {
            return Result<JobReference>.Failure(ToExternalError(influxKeywords, "Influx keyword external API call failed."));
        }

        await SaveInfluxKeywordsAsync(project.Id, project.DefaultLanguage, influxKeywords.Data, site.Domain, cancellationToken);

        var influxPages = await rakkoKeywordClient.GetInfluxPagesAsync(
            clientContext,
            new RakkoInfluxPagesRequest(targets, Limit: ExternalResultLimit),
            cancellationToken);
        if (!influxPages.IsSuccess || influxPages.Data is null)
        {
            return Result<JobReference>.Failure(ToExternalError(influxPages, "Influx page external API call failed."));
        }

        await SaveInfluxPagesAsync(project.Id, project.DefaultLanguage, influxPages.Data, site.Domain, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<JobReference>.Success(new JobReference(jobId, StatusValues.Running));
    }

    private async Task<NormalizeResult> NormalizeAnalyzeRequestAsync(
        ProjectExecutionContext context,
        CompetitorAnalyzeRequest request,
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

        SiteEntity? site = null;
        if (request.SiteId.HasValue)
        {
            site = await dbContext.Sites
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == project.Id &&
                        entity.Id == request.SiteId.Value &&
                        entity.Status == StatusValues.Active,
                    cancellationToken);
            if (site is null)
            {
                return new NormalizeResult(null, new Error(ErrorCode.NotFound, "Target site was not found."));
            }
        }
        else
        {
            var target = OptionalText(request.Target);
            if (target is null)
            {
                errors.Add("target", "target is required.");
            }

            if (errors.HasErrors)
            {
                return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
            }

            if (!TryNormalizeHttpUrl(target!, out var canonicalUrl, out var domain))
            {
                errors.Add("target", "target must be a valid http(s) URL or domain.");
                return new NormalizeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
            }

            site = await dbContext.Sites
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == project.Id &&
                        entity.Domain == domain &&
                        entity.Status == StatusValues.Active,
                    cancellationToken);
            if (site is null)
            {
                var now = NowUtc();
                site = new SiteEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = project.Id,
                    Domain = domain,
                    CanonicalUrl = canonicalUrl,
                    Type = "own",
                    Status = StatusValues.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.Sites.Add(site);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var normalized = new NormalizedCompetitorAnalyzeRequest(
            Version: 1,
            site!.Id,
            site.Domain,
            site.CanonicalUrl,
            IdempotencyKey: string.Empty,
            RequestHash: string.Empty);
        var requestHash = HashText(JsonSerializer.Serialize(
            normalized with { IdempotencyKey = string.Empty, RequestHash = string.Empty },
            JsonOptions));
        normalized = normalized with
        {
            IdempotencyKey = BuildIdempotencyKey(project.Id, site.Id, requestHash),
            RequestHash = requestHash
        };

        return new NormalizeResult(normalized, null);
    }

    private async Task<IReadOnlyList<string>> SaveCompetitiveResultsAsync(
        Guid projectId,
        RakkoExternalSearchResults results,
        CancellationToken cancellationToken)
    {
        var domains = new List<string>();
        foreach (var item in results.Items)
        {
            var domain = NormalizeDomainOrNull(item.Domain ?? item.Url);
            if (domain is null)
            {
                continue;
            }

            domains.Add(domain);
            var raw = ParseRaw(item.RawJson);
            var duplicateRate = GetDecimal(raw, ["metrics", "duplicateRate"], ["duplicateRate"]) ?? 0m;
            var estimatedTraffic = item.EstimatedTraffic ?? 0m;
            var trafficValue = item.TrafficValue ?? 0m;
            var keywordCount = GetInt(raw, ["metrics", "keywordCount"], ["keywordCount"]) ?? 0;
            var uniqueCounts = new CompetitorUniqueCounts(
                GetInt(raw, ["metrics", "duplicateKeywordCount"], ["duplicateKeywordCount"]) ?? 0,
                GetInt(raw, ["metrics", "competitorUniqueKeywordCount"], ["competitorUniqueKeywordCount"]) ?? 0,
                GetInt(raw, ["metrics", "targetUniqueKeywordCount"], ["targetUniqueKeywordCount"]) ?? 0,
                GetInt(raw, ["metrics", "pageCount"], ["pageCount"]));
            var now = NowUtc();

            var existing = await dbContext.CompetitiveResults
                .FirstOrDefaultAsync(
                    entity => entity.ProjectId == projectId && entity.SiteDomain == domain,
                    cancellationToken);
            if (existing is null)
            {
                dbContext.CompetitiveResults.Add(new CompetitiveResultEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    SiteDomain = domain,
                    EstimatedTraffic = estimatedTraffic,
                    TrafficValue = trafficValue,
                    KeywordCount = keywordCount,
                    DuplicateRate = duplicateRate,
                    UniqueCountsJson = JsonSerializer.Serialize(uniqueCounts, JsonOptions),
                    CreatedAt = now
                });
            }
            else
            {
                existing.EstimatedTraffic = estimatedTraffic;
                existing.TrafficValue = trafficValue;
                existing.KeywordCount = keywordCount;
                existing.DuplicateRate = duplicateRate;
                existing.UniqueCountsJson = JsonSerializer.Serialize(uniqueCounts, JsonOptions);
                existing.CreatedAt = now;
            }

            var saved = await dbContext.CompetitorSites
                .FirstOrDefaultAsync(
                    entity => entity.ProjectId == projectId && entity.Domain == domain,
                    cancellationToken);
            if (saved is null)
            {
                dbContext.CompetitorSites.Add(new CompetitorSiteEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    Domain = domain,
                    Source = "competitive",
                    DuplicateRate = duplicateRate,
                    EstimatedTraffic = estimatedTraffic,
                    CreatedAt = now
                });
            }
            else
            {
                saved.Source = "competitive";
                saved.DuplicateRate = duplicateRate;
                saved.EstimatedTraffic = estimatedTraffic;
                saved.CreatedAt = now;
            }
        }

        return domains;
    }

    private async Task SaveInfluxKeywordsAsync(
        Guid projectId,
        string language,
        RakkoExternalSearchResults results,
        string fallbackTarget,
        CancellationToken cancellationToken)
    {
        foreach (var item in results.Items)
        {
            var keywordText = OptionalText(item.Keyword);
            var rankedUrl = NormalizeUrlOrFallback(item.Url);
            if (keywordText is null || rankedUrl is null)
            {
                continue;
            }

            var keyword = await EnsureKeywordAsync(keywordText, language, cancellationToken);
            var target = NormalizeTargetOrFallback(item.Target, fallbackTarget);
            var rank = ToInt(item.Position) ?? 0;
            var estimatedTraffic = item.EstimatedTraffic ?? 0m;
            var now = NowUtc();

            var existing = await dbContext.InfluxKeywordResults
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == projectId &&
                        entity.Target == target &&
                        entity.KeywordId == keyword.Id &&
                        entity.RankedUrl == rankedUrl,
                    cancellationToken);
            if (existing is null)
            {
                dbContext.InfluxKeywordResults.Add(new InfluxKeywordResultEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    Target = target,
                    KeywordId = keyword.Id,
                    Rank = rank,
                    RankedUrl = rankedUrl,
                    EstimatedTraffic = estimatedTraffic,
                    MetricsSnapshotJson = string.IsNullOrWhiteSpace(item.RawJson) ? "{}" : item.RawJson,
                    CreatedAt = now
                });
            }
            else
            {
                existing.Rank = rank;
                existing.EstimatedTraffic = estimatedTraffic;
                existing.MetricsSnapshotJson = string.IsNullOrWhiteSpace(item.RawJson) ? "{}" : item.RawJson;
                existing.CreatedAt = now;
            }
        }
    }

    private async Task SaveInfluxPagesAsync(
        Guid projectId,
        string language,
        RakkoExternalSearchResults results,
        string fallbackTarget,
        CancellationToken cancellationToken)
    {
        foreach (var item in results.Items)
        {
            var pageUrl = NormalizeUrlOrFallback(item.Url);
            if (pageUrl is null)
            {
                continue;
            }

            var raw = ParseRaw(item.RawJson);
            var target = NormalizeTargetOrFallback(item.Target, fallbackTarget);
            var topKeyword = OptionalText(item.Keyword);
            var topKeywordEntity = topKeyword is null
                ? null
                : await EnsureKeywordAsync(topKeyword, language, cancellationToken);
            var keywordCount = GetInt(raw, ["performance", "rankingKeywordCount"], ["metrics", "rankingKeywordCount"], ["keywordCount"]) ?? 0;
            var now = NowUtc();

            var existing = await dbContext.InfluxPageResults
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == projectId &&
                        entity.Target == target &&
                        entity.PageUrl == pageUrl,
                    cancellationToken);
            if (existing is null)
            {
                dbContext.InfluxPageResults.Add(new InfluxPageResultEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    Target = target,
                    PageUrl = pageUrl,
                    Title = OptionalText(item.Title) ?? pageUrl,
                    KeywordCount = keywordCount,
                    EstimatedTraffic = item.EstimatedTraffic ?? 0m,
                    TrafficValue = item.TrafficValue ?? 0m,
                    TopKeywordId = topKeywordEntity?.Id,
                    CreatedAt = now
                });
            }
            else
            {
                existing.Title = OptionalText(item.Title) ?? pageUrl;
                existing.KeywordCount = keywordCount;
                existing.EstimatedTraffic = item.EstimatedTraffic ?? 0m;
                existing.TrafficValue = item.TrafficValue ?? 0m;
                existing.TopKeywordId = topKeywordEntity?.Id;
                existing.CreatedAt = now;
            }
        }
    }

    private async Task<KeywordEntity> EnsureKeywordAsync(
        string keyword,
        string language,
        CancellationToken cancellationToken)
    {
        var normalized = KeywordNormalizer.Normalize(keyword);
        var hash = HashText(normalized);
        var local = dbContext.Keywords.Local
            .FirstOrDefault(entity => entity.Language == language && entity.TextHash == hash);
        if (local is not null)
        {
            return local;
        }

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
        => context.ProjectId.HasValue
            ? await dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.WorkspaceId == context.WorkspaceId &&
                        entity.Id == context.ProjectId.Value &&
                        entity.Status == StatusValues.Active,
                    cancellationToken)
            : null;

    private static CompetitorResultRow MapCompetitorRow(CompetitiveResultEntity entity, bool saved)
    {
        var uniqueCounts = DeserializeOrDefault<CompetitorUniqueCounts>(entity.UniqueCountsJson)
            ?? new CompetitorUniqueCounts(0, 0, 0, null);
        return new CompetitorResultRow(
            entity.Id,
            entity.SiteDomain,
            entity.DuplicateRate,
            entity.EstimatedTraffic,
            entity.TrafficValue,
            entity.KeywordCount,
            uniqueCounts.DuplicateKeywordCount,
            uniqueCounts.CompetitorUniqueKeywordCount,
            uniqueCounts.TargetUniqueKeywordCount,
            saved,
            entity.CreatedAt);
    }

    private static InfluxKeywordResultRow MapInfluxKeywordRow(
        InfluxKeywordProjection projection,
        IReadOnlySet<string> ownDomains)
    {
        var targetDomain = NormalizeDomainOrNull(projection.Result.Target) ?? projection.Result.Target;
        var isGap = !ownDomains.Contains(targetDomain);
        return new InfluxKeywordResultRow(
            projection.Result.Id,
            projection.Keyword.Id,
            projection.Result.Target,
            projection.Keyword.NormalizedText,
            projection.Result.Rank,
            projection.Result.RankedUrl,
            projection.Result.EstimatedTraffic,
            ParseJsonElementOrEmpty(projection.Result.MetricsSnapshotJson),
            isGap,
            isGap ? "competitor_unique" : "owned",
            projection.Result.CreatedAt);
    }

    private static InfluxPageResultRow MapInfluxPageRow(
        InfluxPageResultEntity entity,
        IReadOnlyDictionary<Guid, string> topKeywords)
        => new(
            entity.Id,
            entity.Target,
            entity.PageUrl,
            entity.Title,
            entity.KeywordCount,
            entity.EstimatedTraffic,
            entity.TrafficValue,
            entity.TopKeywordId,
            entity.TopKeywordId.HasValue && topKeywords.TryGetValue(entity.TopKeywordId.Value, out var keyword)
                ? keyword
                : null,
            entity.CreatedAt);

    private static IEnumerable<InfluxKeywordResultRow> FilterInfluxKeywords(
        IEnumerable<InfluxKeywordResultRow> rows,
        InfluxKeywordSearchQuery query)
    {
        var target = OptionalText(query.Target);
        if (target is not null)
        {
            rows = rows.Where(row => row.Target.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MinRank.HasValue)
        {
            rows = rows.Where(row => row.Rank >= query.MinRank.Value);
        }

        if (query.MaxRank.HasValue)
        {
            rows = rows.Where(row => row.Rank <= query.MaxRank.Value);
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.Target.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.RankedUrl.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private static IEnumerable<InfluxPageResultRow> FilterInfluxPages(
        IEnumerable<InfluxPageResultRow> rows,
        InfluxPageSearchQuery query)
    {
        var target = OptionalText(query.Target);
        if (target is not null)
        {
            rows = rows.Where(row => row.Target.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        var q = OptionalText(query.Search.Q);
        if (q is not null)
        {
            rows = rows.Where(row =>
                row.Target.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.PageUrl.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                row.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (row.TopKeyword is not null && row.TopKeyword.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return rows;
    }

    private static IEnumerable<CompetitorResultRow> SortCompetitors(
        IEnumerable<CompetitorResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "duplicateRate";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "domain" => SortString(rows, ascending, row => row.Domain),
            "estimatedTraffic" => SortDecimal(rows, ascending, row => row.EstimatedTraffic),
            "trafficValue" => SortDecimal(rows, ascending, row => row.TrafficValue),
            "keywordCount" => SortInt(rows, ascending, row => row.KeywordCount),
            "gapKeywordCount" => SortInt(rows, ascending, row => row.CompetitorUniqueKeywordCount),
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            _ => SortDecimal(rows, ascending, row => row.DuplicateRate)
        };
    }

    private static IEnumerable<InfluxKeywordResultRow> SortInfluxKeywords(
        IEnumerable<InfluxKeywordResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "rank";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "target" => SortString(rows, ascending, row => row.Target),
            "keyword" => SortString(rows, ascending, row => row.Keyword),
            "estimatedTraffic" => SortDecimal(rows, ascending, row => row.EstimatedTraffic),
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            _ => SortInt(rows, ascending, row => row.Rank)
        };
    }

    private static IEnumerable<InfluxPageResultRow> SortInfluxPages(
        IEnumerable<InfluxPageResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "estimatedTraffic";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "target" => SortString(rows, ascending, row => row.Target),
            "pageUrl" => SortString(rows, ascending, row => row.PageUrl),
            "keywordCount" => SortInt(rows, ascending, row => row.KeywordCount),
            "trafficValue" => SortDecimal(rows, ascending, row => row.TrafficValue),
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            _ => SortDecimal(rows, ascending, row => row.EstimatedTraffic)
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

    private static bool TryNormalizeHttpUrl(string value, out string canonicalUrl, out string domain)
    {
        canonicalUrl = string.Empty;
        domain = string.Empty;
        try
        {
            canonicalUrl = UrlNormalizer.NormalizeUrl(value);
            var uri = new Uri(canonicalUrl, UriKind.Absolute);
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            domain = UrlNormalizer.NormalizeDomain(canonicalUrl);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string? NormalizeDomainOrNull(string? value)
    {
        var text = OptionalText(value);
        if (text is null)
        {
            return null;
        }

        try
        {
            return UrlNormalizer.NormalizeDomain(text);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NormalizeTargetOrFallback(string? target, string fallback)
        => NormalizeDomainOrNull(target) ?? fallback;

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
    }

    private static JsonElement? ParseRaw(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement ParseJsonElementOrEmpty(string json)
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

    private static decimal? GetDecimal(JsonElement? root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (TryGetProperty(root, path, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement? root, params string[][] paths)
    {
        var number = GetDecimal(root, paths);
        return number.HasValue
            ? Convert.ToInt32(number.Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static bool TryGetProperty(JsonElement? root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = default;
        if (root is null || root.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = root.Value;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

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

    private static string BuildIdempotencyKey(Guid projectId, Guid siteId, string requestHash)
        => $"competitor-refresh:{projectId:N}:{siteId:N}:{requestHash}";

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

    private sealed record NormalizeResult(NormalizedCompetitorAnalyzeRequest? Request, Error? Error);

    private sealed record InfluxKeywordProjection(InfluxKeywordResultEntity Result, KeywordEntity Keyword);
}

internal sealed record NormalizedCompetitorAnalyzeRequest(
    int Version,
    Guid SiteId,
    string TargetDomain,
    string TargetUrl,
    string IdempotencyKey,
    string RequestHash);

internal sealed record CompetitorUniqueCounts(
    int DuplicateKeywordCount,
    int CompetitorUniqueKeywordCount,
    int TargetUniqueKeywordCount,
    int? PageCount);

internal sealed class CompetitorRefreshJob(
    SeoIntelligenceDbContext dbContext,
    CompetitiveAnalysisService competitiveAnalysisService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<CompetitorRefreshJob> logger)
{
    public const string JobType = CompetitiveAnalysisService.JobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Competitor refresh job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, CompetitiveAnalysisService.TargetSiteResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Competitor refresh job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(20)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Competitor refresh job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await competitiveAnalysisService.ExecuteQueuedAsync(
                context,
                jobId,
                job.ResultResourceId.Value);

            if (result.IsSuccess)
            {
                await jobService.CompleteAsync(
                    context,
                    jobId,
                    new JobCompletion(
                        Progress: 100,
                        new JobResultResource(CompetitiveAnalysisService.TargetSiteResourceType, job.ResultResourceId.Value)));
                return;
            }

            await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Competitor refresh job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Competitor refresh could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Competitor refresh job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Competitor refresh failed unexpectedly."));
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
