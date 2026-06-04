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
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class ContentAnalysisService(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    IObjectStorage objectStorage,
    TimeProvider timeProvider)
    : IContentAnalysisService
{
    public const string ContentAnalyzeJobType = "ContentAnalyzeJob";
    public const string GenerateBriefJobType = "GenerateBriefJob";
    public const string ArticleBriefExportJobType = "ArticleBriefExportJob";
    public const string ContentAnalysisResourceType = "content_analysis_keyword";
    public const string ArticleBriefResourceType = "article_brief";
    public const string ArticleBriefExportResourceType = "article_brief_export";
    public const string ArticleBriefArtifactType = "article_brief";
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;
    private const string MarkdownFormat = "markdown";
    private const string CsvFormat = "csv";
    private const string ArticleBriefExportType = "article_brief";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<JobReference>> AnalyzeAsync(
        ProjectExecutionContext context,
        ContentAnalyzeRequest request,
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
                ContentAnalyzeJobType,
                payload.RootElement.Clone(),
                RequestHash: analyzeRequest.RequestHash,
                IdempotencyKey: BuildAnalyzeIdempotencyKey(analyzeRequest),
                TargetKey: analyzeRequest.KeywordId.ToString("N"),
                Queue: "external-api",
                InitialResource: new JobResultResource(ContentAnalysisResourceType, analyzeRequest.KeywordId)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<PagedResult<ContentAnalysisResultRow>>> GetContentAnalysesAsync(
        ProjectExecutionContext context,
        ContentAnalysisSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<ContentAnalysisResultRow>>(ErrorCode.NotFound, "Project was not found.");
        }

        var contentResults = await dbContext.ContentSearchResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var headlinePages = await dbContext.SerpHeadlinePages
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var coWords = await dbContext.CoOccurrenceWords
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);

        var keywordIds = contentResults.Select(entity => entity.KeywordId)
            .Concat(headlinePages.Select(entity => entity.KeywordId))
            .Concat(coWords.Select(entity => entity.KeywordId))
            .Distinct()
            .ToArray();

        if (query.KeywordId.HasValue)
        {
            keywordIds = keywordIds.Where(id => id == query.KeywordId.Value).ToArray();
        }

        if (keywordIds.Length == 0)
        {
            return Result<PagedResult<ContentAnalysisResultRow>>.Success(ToPagedResult(Array.Empty<ContentAnalysisResultRow>(), query.Search));
        }

        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        var topKeywordIds = contentResults
            .Where(entity => entity.TopKeywordId.HasValue)
            .Select(entity => entity.TopKeywordId!.Value)
            .Distinct()
            .ToArray();
        var topKeywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => topKeywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);
        var pageIds = headlinePages.Select(entity => entity.Id).ToArray();
        var headlines = await dbContext.SerpHeadlines
            .AsNoTracking()
            .Where(entity => pageIds.Contains(entity.PageId))
            .OrderBy(entity => entity.OrderNo)
            .ToArrayAsync(cancellationToken);
        var coWordIds = coWords.Select(entity => entity.Id).ToArray();
        var coDetails = await dbContext.CoOccurrencePageDetails
            .AsNoTracking()
            .Where(entity => coWordIds.Contains(entity.CoWordId))
            .OrderBy(entity => entity.Rank)
            .ToArrayAsync(cancellationToken);

        var rows = keywordIds
            .Select(keywordId => MapContentAnalysisRow(
                keywordId,
                keywords.GetValueOrDefault(keywordId) ?? string.Empty,
                contentResults.Where(entity => entity.KeywordId == keywordId),
                headlinePages.Where(entity => entity.KeywordId == keywordId),
                headlines,
                coWords.Where(entity => entity.KeywordId == keywordId),
                coDetails,
                topKeywords))
            .Where(row => !string.IsNullOrWhiteSpace(row.Keyword));

        rows = FilterContentAnalyses(rows, query.Search.Q);
        rows = SortContentAnalyses(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<ContentAnalysisResultRow>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<JobReference>> GenerateBriefAsync(
        ProjectExecutionContext context,
        GenerateBriefRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeBriefRequestAsync(context, request, cancellationToken);
        if (normalized.Error is not null)
        {
            return Result<JobReference>.Failure(normalized.Error);
        }

        var briefRequest = normalized.Request!;
        var now = NowUtc();
        var content = JsonSerializer.SerializeToElement(new
        {
            targetKeyword = briefRequest.TargetKeyword,
            competitorUrls = briefRequest.CompetitorUrls,
            status = StatusValues.Queued
        }, JsonOptions);
        var brief = new ArticleBriefEntity
        {
            Id = UuidV7.New(),
            ProjectId = context.ProjectId!.Value,
            ClusterId = briefRequest.ClusterId,
            Title = briefRequest.Title ?? $"Article brief: {briefRequest.TargetKeyword}",
            TargetKeywordId = briefRequest.TargetKeywordId,
            CurrentVersion = 0,
            ContentJson = content.GetRawText(),
            ReviewStatus = StatusValues.Pending,
            Status = "draft",
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.ArticleBriefs.Add(brief);

        var jobPayload = JsonSerializer.SerializeToElement(briefRequest with { BriefId = brief.Id }, JsonOptions);
        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                GenerateBriefJobType,
                jobPayload,
                RequestHash: HashText(jobPayload.GetRawText()),
                TargetKey: brief.Id.ToString("N"),
                Queue: "analysis",
                InitialResource: new JobResultResource(ArticleBriefResourceType, brief.Id)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<PagedResult<ArticleBriefSummary>>> GetBriefsAsync(
        ProjectExecutionContext context,
        ArticleBriefSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<ArticleBriefSummary>>(ErrorCode.NotFound, "Project was not found.");
        }

        var briefs = await dbContext.ArticleBriefs
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var keywordIds = briefs
            .Where(entity => entity.TargetKeywordId.HasValue)
            .Select(entity => entity.TargetKeywordId!.Value)
            .Distinct()
            .ToArray();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.NormalizedText, cancellationToken);

        var rows = briefs.Select(entity => MapBriefSummary(entity, keywords));
        rows = FilterBriefs(rows, query);
        rows = SortBriefs(rows, query.Search.Sort).ToArray();
        return Result<PagedResult<ArticleBriefSummary>>.Success(ToPagedResult(rows, query.Search));
    }

    public async Task<Result<ArticleBriefDetails>> GetBriefAsync(
        ProjectExecutionContext context,
        Guid briefId,
        CancellationToken cancellationToken = default)
    {
        var brief = await FindBriefAsync(context, briefId, asTracking: false, cancellationToken);
        if (brief is null)
        {
            return Failure<ArticleBriefDetails>(ErrorCode.NotFound, "Article brief was not found.");
        }

        return Result<ArticleBriefDetails>.Success(await MapBriefDetailsAsync(brief, cancellationToken));
    }

    public async Task<Result<ArticleBriefDetails>> UpdateBriefAsync(
        ProjectExecutionContext context,
        Guid briefId,
        ArticleBriefUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateBriefUpdate(request);
        if (errors.HasErrors)
        {
            return ValidationFailure<ArticleBriefDetails>(errors);
        }

        var brief = await FindBriefAsync(context, briefId, asTracking: true, cancellationToken);
        if (brief is null)
        {
            return Failure<ArticleBriefDetails>(ErrorCode.NotFound, "Article brief was not found.");
        }

        var beforeContent = brief.ContentJson;
        var beforeTitle = brief.Title;
        var beforeReviewStatus = brief.ReviewStatus;

        if (OptionalText(request.Title) is { } title)
        {
            brief.Title = title;
        }

        if (request.Content.HasValue && request.Content.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            brief.ContentJson = request.Content.Value.GetRawText();
        }

        if (OptionalText(request.ReviewStatus) is { } reviewStatus)
        {
            brief.ReviewStatus = reviewStatus.ToLowerInvariant();
        }

        if (OptionalText(request.Status) is { } status)
        {
            brief.Status = status.ToLowerInvariant();
        }

        var shouldVersion =
            !string.Equals(beforeContent, brief.ContentJson, StringComparison.Ordinal) ||
            !string.Equals(beforeTitle, brief.Title, StringComparison.Ordinal) ||
            !string.Equals(beforeReviewStatus, brief.ReviewStatus, StringComparison.Ordinal);
        brief.UpdatedAt = NowUtc();
        if (shouldVersion)
        {
            brief.CurrentVersion += 1;
            AddArtifactVersion(
                context,
                brief,
                request.ChangeSummary ?? "Manual brief update.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ArticleBriefDetails>.Success(await MapBriefDetailsAsync(brief, cancellationToken));
    }

    public async Task<Result<PagedResult<ArticleBriefVersionDetails>>> GetBriefVersionsAsync(
        ProjectExecutionContext context,
        Guid briefId,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var brief = await FindBriefAsync(context, briefId, asTracking: false, cancellationToken);
        if (brief is null)
        {
            return Failure<PagedResult<ArticleBriefVersionDetails>>(ErrorCode.NotFound, "Article brief was not found.");
        }

        var source = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId &&
                entity.ArtifactType == ArticleBriefArtifactType &&
                entity.ArtifactId == briefId)
            .OrderByDescending(entity => entity.VersionNo)
            .ToArrayAsync(cancellationToken);

        var rows = SortVersions(source.Select(MapVersion), query.Sort).ToArray();
        return Result<PagedResult<ArticleBriefVersionDetails>>.Success(ToPagedResult(rows, query));
    }

    public async Task<Result<JobReference>> ExportBriefAsync(
        ProjectExecutionContext context,
        Guid briefId,
        ArticleBriefExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var format = NormalizeExportFormat(request.Format);
        if (format is null)
        {
            var errors = new ValidationErrors();
            errors.Add(nameof(ArticleBriefExportRequest.Format), "format must be markdown or csv.");
            return ValidationFailure<JobReference>(errors);
        }

        var brief = await FindBriefAsync(context, briefId, asTracking: false, cancellationToken);
        if (brief is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Article brief was not found.");
        }

        var payload = JsonSerializer.SerializeToElement(new ArticleBriefExportSnapshot(1, briefId, format), JsonOptions);
        var now = NowUtc();
        var export = new DataExportEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            ExportType = ArticleBriefExportType,
            Format = format,
            FilterJson = payload.GetRawText(),
            Status = StatusValues.Queued,
            RequestedBy = context.Actor,
            CreatedAt = now
        };
        dbContext.DataExports.Add(export);

        var registration = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                ArticleBriefExportJobType,
                payload,
                RequestHash: HashText(payload.GetRawText()),
                TargetKey: briefId.ToString("N"),
                Queue: "exports",
                InitialResource: new JobResultResource(ArticleBriefExportResourceType, export.Id)),
            cancellationToken);

        return registration.IsSuccess
            ? Result<JobReference>.Success(new JobReference(registration.Value!.JobId, registration.Value.Status))
            : Result<JobReference>.Failure(registration.Error!);
    }

    public async Task<Result<JobReference>> ExecuteContentAnalyzeAsync(
        ProjectExecutionContext context,
        Guid jobId,
        Guid keywordId,
        ContentAnalyzeJobOptions options,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        var keyword = await dbContext.Keywords
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == keywordId, cancellationToken);
        if (keyword is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Keyword was not found.");
        }

        var clientContext = CreateClientContext(context, jobId);
        if (options.IncludeContentSearch)
        {
            var content = await rakkoKeywordClient.GetContentSearchAsync(
                clientContext,
                new RakkoContentSearchRequest(keyword.NormalizedText, Limit: options.Limit),
                cancellationToken);
            if (!content.IsSuccess || content.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(content, "Content search external API call failed."));
            }

            await SaveContentSearchResultsAsync(project.Id, keywordId, keyword.Language, content.Data, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (options.IncludeHeadline)
        {
            var headlines = await rakkoKeywordClient.GetHeadlinesAsync(
                clientContext,
                new RakkoHeadlineRequest(keyword.NormalizedText, Limit: options.Limit),
                cancellationToken);
            if (!headlines.IsSuccess || headlines.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(headlines, "Headline external API call failed."));
            }

            await SaveHeadlineResultsAsync(project.Id, keywordId, headlines.Data, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (options.IncludeCoOccurrence)
        {
            var coOccurrences = await rakkoKeywordClient.GetCoOccurrencesAsync(
                clientContext,
                new RakkoCoOccurrenceRequest(keyword.NormalizedText, Limit: options.Limit),
                cancellationToken);
            if (!coOccurrences.IsSuccess || coOccurrences.Data is null)
            {
                return Result<JobReference>.Failure(ToExternalError(coOccurrences, "Co-occurrence external API call failed."));
            }

            await SaveCoOccurrenceResultsAsync(project.Id, keywordId, coOccurrences.Data, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result<JobReference>.Success(new JobReference(jobId, StatusValues.Running));
    }

    public async Task<Result<ArticleBriefDetails>> ExecuteGenerateBriefAsync(
        ProjectExecutionContext context,
        Guid briefId,
        CancellationToken cancellationToken = default)
    {
        var brief = await FindBriefAsync(context, briefId, asTracking: true, cancellationToken);
        if (brief is null)
        {
            return Failure<ArticleBriefDetails>(ErrorCode.NotFound, "Article brief was not found.");
        }

        if (!brief.TargetKeywordId.HasValue)
        {
            return Failure<ArticleBriefDetails>(ErrorCode.Conflict, "Article brief does not have a target keyword.");
        }

        var keyword = await dbContext.Keywords
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == brief.TargetKeywordId.Value, cancellationToken);
        if (keyword is null)
        {
            return Failure<ArticleBriefDetails>(ErrorCode.NotFound, "Target keyword was not found.");
        }

        var document = await BuildBriefDocumentAsync(context, brief, keyword, cancellationToken);
        var contentJson = JsonSerializer.Serialize(document, JsonOptions);
        brief.Title = document.Title;
        brief.ContentJson = contentJson;
        brief.CurrentVersion += 1;
        brief.ReviewStatus = StatusValues.Pending;
        brief.Status = "draft";
        brief.UpdatedAt = NowUtc();
        AddArtifactVersion(context, brief, "Generated from content analysis evidence.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ArticleBriefDetails>.Success(await MapBriefDetailsAsync(brief, cancellationToken));
    }

    public async Task<Result<DataExportDetails>> ExecuteBriefExportAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == ArticleBriefExportJobType,
                cancellationToken);
        if (job is null || !job.ResultResourceId.HasValue)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "Article brief export job was not found.");
        }

        var export = await dbContext.DataExports
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == job.ResultResourceId.Value,
                cancellationToken);
        if (export is null)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "Article brief export was not found.");
        }

        var snapshot = DeserializeOrDefault<ArticleBriefExportSnapshot>(export.FilterJson);
        if (snapshot is null)
        {
            return Failure<DataExportDetails>(ErrorCode.Conflict, "Article brief export request payload was invalid.");
        }

        var brief = await FindBriefAsync(context, snapshot.BriefId, asTracking: false, cancellationToken);
        if (brief is null)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "Article brief was not found.");
        }

        var contentBytes = snapshot.Format == CsvFormat
            ? BuildBriefCsv(brief)
            : BuildBriefMarkdown(brief);
        await using var content = new MemoryStream(contentBytes, writable: false);
        var stored = await objectStorage.PutAsync(
            new StoragePutRequest(
                new StorageObjectKey(BuildBriefExportObjectKey(context, export.Id, snapshot.Format)),
                content,
                snapshot.Format == CsvFormat ? "text/csv; charset=utf-8" : "text/markdown; charset=utf-8"),
            cancellationToken);

        export.FileUri = stored.Uri;
        export.Status = StatusValues.Succeeded;
        export.CompletedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DataExportDetails>.Success(MapExport(export));
    }

    public async Task RecordBriefExportFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == ArticleBriefExportJobType,
                cancellationToken);
        if (job?.ResultResourceId is null)
        {
            return;
        }

        var export = await dbContext.DataExports
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == job.ResultResourceId.Value,
                cancellationToken);
        if (export is null || string.Equals(export.Status, StatusValues.Succeeded, StringComparison.Ordinal))
        {
            return;
        }

        export.Status = status;
        export.CompletedAt = NowUtc();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<NormalizeAnalyzeResult> NormalizeAnalyzeRequestAsync(
        ProjectExecutionContext context,
        ContentAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        var keywordText = RequireText(request.Keyword, nameof(request.Keyword), errors, 200);
        var limit = request.Limit ?? DefaultLimit;
        if (limit < 1 || limit > MaxLimit)
        {
            errors.Add(nameof(request.Limit), $"limit must be between 1 and {MaxLimit}.");
        }

        if (!request.IncludeContentSearch && !request.IncludeHeadline && !request.IncludeCoOccurrence)
        {
            errors.Add("analysisTypes", "at least one analysis type must be enabled.");
        }

        if (errors.HasErrors)
        {
            return new NormalizeAnalyzeResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return new NormalizeAnalyzeResult(null, new Error(ErrorCode.NotFound, "Project was not found."));
        }

        var keyword = await EnsureKeywordAsync(keywordText!, project.DefaultLanguage, cancellationToken);
        var normalized = new NormalizedContentAnalyzeRequest(
            1,
            keyword.Id,
            keyword.NormalizedText,
            request.IncludeContentSearch,
            request.IncludeHeadline,
            request.IncludeCoOccurrence,
            limit,
            string.Empty);
        normalized = normalized with { RequestHash = HashText(JsonSerializer.Serialize(normalized, JsonOptions)) };
        return new NormalizeAnalyzeResult(normalized, null);
    }

    private async Task<NormalizeBriefResult> NormalizeBriefRequestAsync(
        ProjectExecutionContext context,
        GenerateBriefRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (request.TargetKeywordId.HasValue && !string.IsNullOrWhiteSpace(request.TargetKeyword))
        {
            errors.Add(nameof(request.TargetKeyword), "targetKeyword cannot be specified with targetKeywordId.");
        }

        if (!request.TargetKeywordId.HasValue && string.IsNullOrWhiteSpace(request.TargetKeyword))
        {
            errors.Add(nameof(request.TargetKeyword), "targetKeyword is required when targetKeywordId is omitted.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Trim().Length > 300)
        {
            errors.Add(nameof(request.Title), "title must be 300 characters or fewer.");
        }

        if (errors.HasErrors)
        {
            return new NormalizeBriefResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return new NormalizeBriefResult(null, new Error(ErrorCode.NotFound, "Project was not found."));
        }

        if (request.ClusterId.HasValue)
        {
            var clusterExists = await dbContext.TopicClusters
                .AsNoTracking()
                .AnyAsync(entity => entity.ProjectId == project.Id && entity.Id == request.ClusterId.Value, cancellationToken);
            if (!clusterExists)
            {
                return new NormalizeBriefResult(null, new Error(ErrorCode.NotFound, "Topic cluster was not found."));
            }
        }

        KeywordEntity? keyword;
        if (request.TargetKeywordId.HasValue)
        {
            keyword = await dbContext.Keywords
                .FirstOrDefaultAsync(entity => entity.Id == request.TargetKeywordId.Value, cancellationToken);
            if (keyword is null)
            {
                return new NormalizeBriefResult(null, new Error(ErrorCode.NotFound, "Target keyword was not found."));
            }
        }
        else
        {
            keyword = await EnsureKeywordAsync(request.TargetKeyword!, project.DefaultLanguage, cancellationToken);
        }

        var competitorUrls = NormalizeUrls(request.CompetitorUrls, errors);
        if (errors.HasErrors)
        {
            return new NormalizeBriefResult(null, Error.Validation("Validation failed.", errors.ToDictionary()));
        }

        return new NormalizeBriefResult(
            new NormalizedGenerateBriefRequest(
                1,
                BriefId: null,
                keyword!.Id,
                keyword.NormalizedText,
                request.ClusterId,
                OptionalText(request.Title),
                competitorUrls),
            null);
    }

    private static ValidationErrors ValidateBriefUpdate(ArticleBriefUpdateRequest request)
    {
        var errors = new ValidationErrors();
        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add(nameof(request.Title), "title cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Trim().Length > 300)
        {
            errors.Add(nameof(request.Title), "title must be 300 characters or fewer.");
        }

        if (request.Content.HasValue &&
            request.Content.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null and not JsonValueKind.Object)
        {
            errors.Add(nameof(request.Content), "content must be a JSON object.");
        }

        if (OptionalText(request.ReviewStatus) is { } reviewStatus &&
            !IsAllowed(reviewStatus, ["pending", "reviewed", "rejected"]))
        {
            errors.Add(nameof(request.ReviewStatus), "reviewStatus must be pending, reviewed, or rejected.");
        }

        if (OptionalText(request.Status) is { } status &&
            !IsAllowed(status, ["draft", StatusValues.Active, StatusValues.Archived]))
        {
            errors.Add(nameof(request.Status), "status must be draft, active, or archived.");
        }

        return errors;
    }

    private async Task SaveContentSearchResultsAsync(
        Guid projectId,
        Guid keywordId,
        string language,
        RakkoExternalSearchResults results,
        CancellationToken cancellationToken)
    {
        foreach (var item in results.Items)
        {
            var raw = ParseRaw(item.RawJson);
            var url = NormalizeUrlOrFallback(item.Url ?? GetString(raw, ["page", "url"]));
            if (url is null)
            {
                continue;
            }

            var domain = NormalizeDomainOrNull(item.Domain ?? url) ?? string.Empty;
            var topKeywordText = OptionalText(item.Keyword ?? GetString(raw, ["topKeyword", "keyword"]));
            var topKeyword = topKeywordText is null
                ? null
                : await EnsureKeywordAsync(topKeywordText, language, cancellationToken);
            var now = NowUtc();
            var existing = await dbContext.ContentSearchResults
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == projectId &&
                        entity.KeywordId == keywordId &&
                        entity.Url == url,
                    cancellationToken);

            if (existing is null)
            {
                dbContext.ContentSearchResults.Add(new ContentSearchResultEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = projectId,
                    KeywordId = keywordId,
                    Url = url,
                    Domain = domain,
                    Title = OptionalText(item.Title ?? GetString(raw, ["page", "title"])) ?? url,
                    Description = OptionalText(GetString(raw, ["page", "description"])) ?? string.Empty,
                    EstimatedTraffic = item.EstimatedTraffic ?? 0m,
                    TrafficValue = item.TrafficValue ?? 0m,
                    TopKeywordId = topKeyword?.Id,
                    CreatedAt = now
                });
            }
            else
            {
                existing.Domain = domain;
                existing.Title = OptionalText(item.Title ?? GetString(raw, ["page", "title"])) ?? url;
                existing.Description = OptionalText(GetString(raw, ["page", "description"])) ?? string.Empty;
                existing.EstimatedTraffic = item.EstimatedTraffic ?? 0m;
                existing.TrafficValue = item.TrafficValue ?? 0m;
                existing.TopKeywordId = topKeyword?.Id;
                existing.CreatedAt = now;
            }
        }
    }

    private async Task SaveHeadlineResultsAsync(
        Guid projectId,
        Guid keywordId,
        RakkoExternalSearchResults results,
        CancellationToken cancellationToken)
    {
        foreach (var item in results.Items)
        {
            var raw = ParseRaw(item.RawJson);
            var url = NormalizeUrlOrFallback(item.Url ?? GetString(raw, ["page", "url"]));
            if (url is null)
            {
                continue;
            }

            var now = NowUtc();
            var existing = await dbContext.SerpHeadlinePages
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == projectId &&
                        entity.KeywordId == keywordId &&
                        entity.Url == url,
                    cancellationToken);
            var page = existing ?? new SerpHeadlinePageEntity
            {
                Id = UuidV7.New(),
                ProjectId = projectId,
                KeywordId = keywordId,
                Url = url
            };

            page.Rank = ToInt(item.Position) ?? GetInt(raw, ["metrics", "position"]) ?? 0;
            page.Title = OptionalText(item.Title ?? GetString(raw, ["page", "title"])) ?? url;
            page.Description = OptionalText(GetString(raw, ["page", "description"])) ?? string.Empty;
            page.HeadlineCount = GetInt(raw, ["metrics", "headlineCount"]) ?? 0;
            page.WordCount = GetInt(raw, ["metrics", "wordCount"]) ?? 0;
            page.CreatedAt = now;

            if (existing is null)
            {
                dbContext.SerpHeadlinePages.Add(page);
            }
            else
            {
                var oldHeadlines = await dbContext.SerpHeadlines
                    .Where(entity => entity.PageId == existing.Id)
                    .ToArrayAsync(cancellationToken);
                dbContext.SerpHeadlines.RemoveRange(oldHeadlines);
            }

            var order = 1;
            foreach (var headline in EnumerateArray(raw, "headlines"))
            {
                var level = ParseHeadlineLevel(GetString(headline, "level")) ?? 2;
                var text = OptionalText(GetString(headline, "text"));
                if (text is null)
                {
                    continue;
                }

                dbContext.SerpHeadlines.Add(new SerpHeadlineEntity
                {
                    Id = UuidV7.New(),
                    PageId = page.Id,
                    Level = level,
                    Text = text,
                    OrderNo = order++
                });
            }

            if (page.HeadlineCount == 0)
            {
                page.HeadlineCount = order - 1;
            }
        }
    }

    private async Task SaveCoOccurrenceResultsAsync(
        Guid projectId,
        Guid keywordId,
        RakkoExternalSearchResults results,
        CancellationToken cancellationToken)
    {
        foreach (var item in results.Items)
        {
            var raw = ParseRaw(item.RawJson);
            var word = OptionalText(item.Keyword ?? GetString(raw, ["word"]));
            if (word is null)
            {
                continue;
            }

            var now = NowUtc();
            var existing = await dbContext.CoOccurrenceWords
                .FirstOrDefaultAsync(
                    entity =>
                        entity.ProjectId == projectId &&
                        entity.KeywordId == keywordId &&
                        entity.Word == word,
                    cancellationToken);
            var coWord = existing ?? new CoOccurrenceWordEntity
            {
                Id = UuidV7.New(),
                ProjectId = projectId,
                KeywordId = keywordId,
                Word = word
            };

            coWord.OccurrenceCountsJson = JsonSerializer.Serialize(new
            {
                occurrencePageCount = GetInt(raw, ["metrics", "occurrencePageCount"]) ?? 0,
                occurrenceTitleCount = GetInt(raw, ["metrics", "occurrenceTitleCount"]) ?? 0,
                occurrenceHeadingCount = GetInt(raw, ["metrics", "occurrenceHeadingCount"]) ?? 0
            }, JsonOptions);
            coWord.SiteCountsJson = JsonSerializer.Serialize(new
            {
                total = GetInt(raw, ["metrics", "siteCountTotal"]) ?? 0,
                heading = GetInt(raw, ["metrics", "siteCountHeading"]) ?? 0
            }, JsonOptions);
            coWord.CreatedAt = now;

            if (existing is null)
            {
                dbContext.CoOccurrenceWords.Add(coWord);
            }
            else
            {
                var oldDetails = await dbContext.CoOccurrencePageDetails
                    .Where(entity => entity.CoWordId == existing.Id)
                    .ToArrayAsync(cancellationToken);
                dbContext.CoOccurrencePageDetails.RemoveRange(oldDetails);
            }

            foreach (var detail in EnumerateArray(raw, "pageDetails"))
            {
                var url = NormalizeUrlOrFallback(GetString(detail, "url"));
                if (url is null)
                {
                    continue;
                }

                dbContext.CoOccurrencePageDetails.Add(new CoOccurrencePageDetailEntity
                {
                    Id = UuidV7.New(),
                    CoWordId = coWord.Id,
                    Rank = GetInt(detail, "rank") ?? 0,
                    Url = url,
                    Title = OptionalText(GetString(detail, "title")) ?? url,
                    Count = GetInt(detail, "count") ?? 0,
                    CountInHeadline = GetInt(detail, "countInHeadline") ?? 0,
                    CountInTitle = GetInt(detail, "countInTitle") ?? 0
                });
            }
        }
    }

    private async Task<BriefDocument> BuildBriefDocumentAsync(
        ProjectExecutionContext context,
        ArticleBriefEntity brief,
        KeywordEntity keyword,
        CancellationToken cancellationToken)
    {
        var contentResults = await dbContext.ContentSearchResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == brief.ProjectId && entity.KeywordId == keyword.Id)
            .OrderByDescending(entity => entity.EstimatedTraffic)
            .Take(10)
            .ToArrayAsync(cancellationToken);
        var headlinePages = await dbContext.SerpHeadlinePages
            .AsNoTracking()
            .Where(entity => entity.ProjectId == brief.ProjectId && entity.KeywordId == keyword.Id)
            .OrderBy(entity => entity.Rank)
            .Take(10)
            .ToArrayAsync(cancellationToken);
        var pageIds = headlinePages.Select(entity => entity.Id).ToArray();
        var headlines = await dbContext.SerpHeadlines
            .AsNoTracking()
            .Where(entity => pageIds.Contains(entity.PageId))
            .OrderBy(entity => entity.OrderNo)
            .ToArrayAsync(cancellationToken);
        var coWords = await dbContext.CoOccurrenceWords
            .AsNoTracking()
            .Where(entity => entity.ProjectId == brief.ProjectId && entity.KeywordId == keyword.Id)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var faq = await dbContext.Questions
            .AsNoTracking()
            .Where(entity => entity.ProjectId == brief.ProjectId && entity.SeedKeywordId == keyword.Id)
            .OrderBy(entity => entity.QuestionText)
            .Take(10)
            .Select(entity => entity.QuestionText)
            .ToArrayAsync(cancellationToken);
        var seedUrls = ReadQueuedCompetitorUrls(brief.ContentJson);
        var competitorUrls = seedUrls.Count > 0
            ? seedUrls
            : contentResults.Select(entity => entity.Url).Take(5).ToArray();
        var outline = BuildOutline(keyword.NormalizedText, headlinePages, headlines);
        var requiredTerms = coWords.Select(entity => entity.Word).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();

        return new BriefDocument(
            Title: string.IsNullOrWhiteSpace(brief.Title) || brief.CurrentVersion == 0
                ? $"{keyword.NormalizedText} content brief"
                : brief.Title,
            TargetKeyword: keyword.NormalizedText,
            SearchIntent: InferSearchIntent(keyword.NormalizedText, headlines),
            Outline: outline,
            RequiredTerms: requiredTerms,
            Faq: faq,
            CompetitorUrls: competitorUrls,
            Evidence: new BriefEvidence(
                ContentResults: contentResults.Select(entity => new BriefContentEvidence(entity.Url, entity.Title, entity.Description, entity.EstimatedTraffic, entity.TrafficValue)).ToArray(),
                HeadlinePages: headlinePages.Select(page => new BriefHeadlinePageEvidence(
                    page.Rank,
                    page.Url,
                    page.Title,
                    headlines
                        .Where(headline => headline.PageId == page.Id)
                        .Select(headline => new BriefHeadlineEvidence(headline.Level, headline.Text))
                        .ToArray())).ToArray(),
                CoOccurrenceWords: requiredTerms),
            GeneratedAt: NowUtc(),
            GeneratedBy: context.Actor);
    }

    private static IReadOnlyList<BriefOutlineSection> BuildOutline(
        string keyword,
        IReadOnlyList<SerpHeadlinePageEntity> pages,
        IReadOnlyList<SerpHeadlineEntity> headlines)
    {
        var sections = pages
            .SelectMany(page => headlines
                .Where(headline => headline.PageId == page.Id && headline.Level is 2 or 3)
                .Select(headline => new BriefOutlineSection(headline.Level, headline.Text)))
            .Where(section => !string.IsNullOrWhiteSpace(section.Heading))
            .DistinctBy(section => $"{section.Level}:{section.Heading}", StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        return sections.Length > 0
            ? sections
            : [
                new BriefOutlineSection(2, $"What is {keyword}?"),
                new BriefOutlineSection(2, "Key comparison points"),
                new BriefOutlineSection(2, "Implementation steps"),
                new BriefOutlineSection(2, "FAQ")
            ];
    }

    private static string InferSearchIntent(string keyword, IReadOnlyList<SerpHeadlineEntity> headlines)
    {
        var text = string.Join(" ", headlines.Select(entity => entity.Text));
        if (text.Contains("how", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("how", StringComparison.OrdinalIgnoreCase))
        {
            return "informational_how_to";
        }

        if (text.Contains("compare", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("best", StringComparison.OrdinalIgnoreCase))
        {
            return "commercial_comparison";
        }

        return "informational";
    }

    private void AddArtifactVersion(
        ProjectExecutionContext context,
        ArticleBriefEntity brief,
        string changeSummary)
    {
        var contentHash = HashText($"{brief.Title}\n{brief.ReviewStatus}\n{brief.ContentJson}");
        dbContext.ArtifactVersions.Add(new ArtifactVersionEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = brief.ProjectId,
            ArtifactType = ArticleBriefArtifactType,
            ArtifactId = brief.Id,
            VersionNo = brief.CurrentVersion,
            ContentHash = contentHash,
            ContentJson = brief.ContentJson,
            CreatedBy = context.Actor,
            ReviewStatus = brief.ReviewStatus,
            ChangeSummary = changeSummary,
            CreatedAt = NowUtc()
        });
    }

    private async Task<ArticleBriefEntity?> FindBriefAsync(
        ProjectExecutionContext context,
        Guid briefId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        var projectActive = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
        if (!projectActive)
        {
            return null;
        }

        var source = asTracking ? dbContext.ArticleBriefs : dbContext.ArticleBriefs.AsNoTracking();
        source = source.Where(entity => entity.Id == briefId && entity.ProjectId == context.ProjectId.Value);

        return await source.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ArticleBriefDetails> MapBriefDetailsAsync(
        ArticleBriefEntity entity,
        CancellationToken cancellationToken)
    {
        var keyword = entity.TargetKeywordId.HasValue
            ? await dbContext.Keywords
                .AsNoTracking()
                .Where(keywordEntity => keywordEntity.Id == entity.TargetKeywordId.Value)
                .Select(keywordEntity => keywordEntity.NormalizedText)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new ArticleBriefDetails(
            entity.Id,
            entity.ProjectId,
            entity.ClusterId,
            entity.Title,
            entity.TargetKeywordId,
            keyword,
            entity.CurrentVersion,
            ParseJsonElementOrEmpty(entity.ContentJson),
            entity.ReviewStatus,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static ArticleBriefSummary MapBriefSummary(
        ArticleBriefEntity entity,
        IReadOnlyDictionary<Guid, string> keywords)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.ClusterId,
            entity.Title,
            entity.TargetKeywordId,
            entity.TargetKeywordId.HasValue && keywords.TryGetValue(entity.TargetKeywordId.Value, out var keyword)
                ? keyword
                : null,
            entity.CurrentVersion,
            ParseJsonElementOrEmpty(entity.ContentJson),
            entity.ReviewStatus,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static ArticleBriefVersionDetails MapVersion(ArtifactVersionEntity entity)
        => new(
            entity.Id,
            entity.VersionNo,
            entity.ContentHash,
            entity.ContentUri,
            ParseJsonElementOrEmpty(entity.ContentJson),
            entity.CreatedBy,
            entity.ReviewStatus,
            entity.ChangeSummary,
            entity.CreatedAt);

    private static ContentAnalysisResultRow MapContentAnalysisRow(
        Guid keywordId,
        string keyword,
        IEnumerable<ContentSearchResultEntity> contentResults,
        IEnumerable<SerpHeadlinePageEntity> headlinePages,
        IReadOnlyList<SerpHeadlineEntity> headlines,
        IEnumerable<CoOccurrenceWordEntity> coWords,
        IReadOnlyList<CoOccurrencePageDetailEntity> coDetails,
        IReadOnlyDictionary<Guid, string> topKeywords)
    {
        var contentRows = contentResults
            .OrderByDescending(entity => entity.EstimatedTraffic)
            .Select(entity => new ContentSearchResultRow(
                entity.Id,
                entity.Url,
                entity.Domain,
                entity.Title,
                entity.Description,
                entity.EstimatedTraffic,
                entity.TrafficValue,
                entity.TopKeywordId,
                entity.TopKeywordId.HasValue && topKeywords.TryGetValue(entity.TopKeywordId.Value, out var topKeyword)
                    ? topKeyword
                    : null,
                entity.CreatedAt))
            .ToArray();
        var headlineRows = headlinePages
            .OrderBy(entity => entity.Rank)
            .Select(page => new SerpHeadlinePageResultRow(
                page.Id,
                page.Rank,
                page.Url,
                page.Title,
                page.Description,
                page.HeadlineCount,
                page.WordCount,
                headlines
                    .Where(headline => headline.PageId == page.Id)
                    .OrderBy(headline => headline.OrderNo)
                    .Select(headline => new SerpHeadlineResultRow(headline.Id, headline.Level, headline.Text, headline.OrderNo))
                    .ToArray(),
                page.CreatedAt))
            .ToArray();
        var coRows = coWords
            .OrderBy(entity => entity.Word)
            .Select(word => new CoOccurrenceWordResultRow(
                word.Id,
                word.Word,
                ParseJsonElementOrEmpty(word.OccurrenceCountsJson),
                ParseJsonElementOrEmpty(word.SiteCountsJson),
                coDetails
                    .Where(detail => detail.CoWordId == word.Id)
                    .OrderBy(detail => detail.Rank)
                    .Select(detail => new CoOccurrencePageDetailResultRow(
                        detail.Id,
                        detail.Rank,
                        detail.Url,
                        detail.Title,
                        detail.Count,
                        detail.CountInHeadline,
                        detail.CountInTitle))
                    .ToArray(),
                word.CreatedAt))
            .ToArray();
        var lastAnalyzedAt = contentRows.Select(row => row.CreatedAt)
            .Concat(headlineRows.Select(row => row.CreatedAt))
            .Concat(coRows.Select(row => row.CreatedAt))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        return new ContentAnalysisResultRow(keywordId, keyword, contentRows, headlineRows, coRows, lastAnalyzedAt);
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

    private static IEnumerable<ContentAnalysisResultRow> FilterContentAnalyses(
        IEnumerable<ContentAnalysisResultRow> rows,
        string? q)
    {
        var text = OptionalText(q);
        if (text is null)
        {
            return rows;
        }

        return rows.Where(row =>
            row.Keyword.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            row.ContentResults.Any(content =>
                content.Url.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                content.Title.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
            row.CoOccurrences.Any(word => word.Word.Contains(text, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<ArticleBriefSummary> FilterBriefs(
        IEnumerable<ArticleBriefSummary> rows,
        ArticleBriefSearchQuery query)
    {
        var status = OptionalText(query.Search.Status);
        if (status is not null && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(row => string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (query.TargetKeywordId.HasValue)
        {
            rows = rows.Where(row => row.TargetKeywordId == query.TargetKeywordId.Value);
        }

        if (query.ClusterId.HasValue)
        {
            rows = rows.Where(row => row.ClusterId == query.ClusterId.Value);
        }

        if (OptionalText(query.ReviewStatus) is { } reviewStatus)
        {
            rows = rows.Where(row => string.Equals(row.ReviewStatus, reviewStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (OptionalText(query.Search.Q) is { } q)
        {
            rows = rows.Where(row =>
                row.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (row.TargetKeyword is not null && row.TargetKeyword.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return rows;
    }

    private static IEnumerable<ContentAnalysisResultRow> SortContentAnalyses(
        IEnumerable<ContentAnalysisResultRow> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "lastAnalyzedAt";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "keyword" => SortString(rows, ascending, row => row.Keyword),
            "contentSearchCount" => SortInt(rows, ascending, row => row.ContentResults.Count),
            "headlinePageCount" => SortInt(rows, ascending, row => row.HeadlinePages.Count),
            "coOccurrenceWordCount" => SortInt(rows, ascending, row => row.CoOccurrences.Count),
            _ => SortDateTime(rows, ascending, row => row.LastAnalyzedAt)
        };
    }

    private static IEnumerable<ArticleBriefSummary> SortBriefs(
        IEnumerable<ArticleBriefSummary> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "updatedAt";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "title" => SortString(rows, ascending, row => row.Title),
            "targetKeyword" => SortString(rows, ascending, row => row.TargetKeyword),
            "currentVersion" => SortInt(rows, ascending, row => row.CurrentVersion),
            "reviewStatus" => SortString(rows, ascending, row => row.ReviewStatus),
            "status" => SortString(rows, ascending, row => row.Status),
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            _ => SortDateTime(rows, ascending, row => row.UpdatedAt)
        };
    }

    private static IEnumerable<ArticleBriefVersionDetails> SortVersions(
        IEnumerable<ArticleBriefVersionDetails> rows,
        SortRequest? sort)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? "versionNo";
        var ascending = sort?.Direction == SortDirection.Asc;
        return sortBy switch
        {
            "createdAt" => SortDateTime(rows, ascending, row => row.CreatedAt),
            _ => SortInt(rows, ascending, row => row.VersionNo)
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

    private static string BuildAnalyzeIdempotencyKey(NormalizedContentAnalyzeRequest request)
        => string.Join(
            ":",
            "content-analyze",
            request.KeywordId.ToString("N"),
            $"limit={request.Limit.ToString(CultureInfo.InvariantCulture)}",
            $"content={(request.IncludeContentSearch ? 1 : 0).ToString(CultureInfo.InvariantCulture)}",
            $"headline={(request.IncludeHeadline ? 1 : 0).ToString(CultureInfo.InvariantCulture)}",
            $"co={(request.IncludeCoOccurrence ? 1 : 0).ToString(CultureInfo.InvariantCulture)}",
            request.RequestHash);

    public static ContentAnalyzeJobOptions ReadAnalyzeOptions(JobEntity job)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in (job.IdempotencyKey ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                options[parts[0]] = parts[1];
            }
        }

        return new ContentAnalyzeJobOptions(
            IncludeContentSearch: ReadBool(options, "content", defaultValue: true),
            IncludeHeadline: ReadBool(options, "headline", defaultValue: true),
            IncludeCoOccurrence: ReadBool(options, "co", defaultValue: true),
            Limit: ReadInt(options, "limit", DefaultLimit));
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue)
        => values.TryGetValue(key, out var value)
            ? value == "1" || bool.TryParse(value, out var parsed) && parsed
            : defaultValue;

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int defaultValue)
        => values.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, MaxLimit)
            : defaultValue;

    private static byte[] BuildBriefMarkdown(ArticleBriefEntity brief)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(brief.Title);
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(brief.ContentJson);
        builder.AppendLine("```");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] BuildBriefCsv(ArticleBriefEntity brief)
    {
        var builder = new StringBuilder();
        builder.AppendLine("briefId,title,currentVersion,reviewStatus,status,contentJson");
        AppendCsvLine(builder, [
            brief.Id.ToString("D"),
            brief.Title,
            brief.CurrentVersion.ToString(CultureInfo.InvariantCulture),
            brief.ReviewStatus,
            brief.Status,
            brief.ContentJson
        ]);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendCsvLine(StringBuilder builder, IEnumerable<string?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(value));
            first = false;
        }

        builder.AppendLine();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains('"') ||
            value.Contains(',') ||
            value.Contains('\n') ||
            value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string BuildBriefExportObjectKey(ProjectExecutionContext context, Guid exportId, string format)
        => $"exports/{context.WorkspaceId:N}/{context.ProjectId!.Value:N}/briefs/{exportId:N}.{(format == CsvFormat ? "csv" : "md")}";

    private static IReadOnlyList<string> ReadQueuedCompetitorUrls(string contentJson)
    {
        try
        {
            using var document = JsonDocument.Parse(contentJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("competitorUrls", out var urls) ||
                urls.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return urls.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DataExportDetails MapExport(DataExportEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.ExportType,
            entity.Format,
            entity.Status,
            entity.FileUri,
            entity.CreatedAt,
            entity.CompletedAt);

    private static IReadOnlyList<string> NormalizeUrls(IReadOnlyList<string>? urls, ValidationErrors errors)
    {
        var normalized = new List<string>();
        foreach (var value in urls ?? [])
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            try
            {
                var url = UrlNormalizer.NormalizeUrl(value);
                if (!normalized.Contains(url, StringComparer.OrdinalIgnoreCase))
                {
                    normalized.Add(url);
                }
            }
            catch (ArgumentException)
            {
                errors.Add(nameof(GenerateBriefRequest.CompetitorUrls), "competitorUrls contains an invalid URL.");
            }
        }

        return normalized;
    }

    private static string? NormalizeExportFormat(string? format)
    {
        var normalized = OptionalText(format)?.ToLowerInvariant() ?? MarkdownFormat;
        return normalized is MarkdownFormat or CsvFormat ? normalized : null;
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

    private static string? RequireText(string? value, string target, ValidationErrors errors, int maxLength)
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

    private static string? GetString(JsonElement? root, IReadOnlyList<string> path)
        => TryGetProperty(root, path, out var value) ? GetString(value) : null;

    private static string? GetString(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property)
            ? GetString(property)
            : null;

    private static string? GetString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

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

    private static int? GetInt(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
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

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement? root, string propertyName)
    {
        if (root is null ||
            root.Value.ValueKind != JsonValueKind.Object ||
            !root.Value.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray().ToArray();
    }

    private static short? ParseHeadlineLevel(string? value)
    {
        var normalized = OptionalText(value)?.ToLowerInvariant();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length == 2 && normalized[0] == 'h' && short.TryParse(normalized[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefixed))
        {
            return (short)Math.Clamp((int)prefixed, 1, 6);
        }

        return short.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? (short)Math.Clamp((int)parsed, 1, 6)
            : null;
    }

    private static int? ToInt(decimal? value)
        => value.HasValue
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : null;

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static bool IsAllowed(string value, IEnumerable<string> allowed)
        => allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ToCamelCase(string value)
    {
        var sanitized = value.Trim();
        return string.IsNullOrEmpty(sanitized)
            ? sanitized
            : char.ToLowerInvariant(sanitized[0]) + sanitized[1..];
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

    private sealed record NormalizeAnalyzeResult(NormalizedContentAnalyzeRequest? Request, Error? Error);

    private sealed record NormalizeBriefResult(NormalizedGenerateBriefRequest? Request, Error? Error);
}

internal sealed record NormalizedContentAnalyzeRequest(
    int Version,
    Guid KeywordId,
    string Keyword,
    bool IncludeContentSearch,
    bool IncludeHeadline,
    bool IncludeCoOccurrence,
    int Limit,
    string RequestHash);

internal sealed record NormalizedGenerateBriefRequest(
    int Version,
    Guid? BriefId,
    Guid TargetKeywordId,
    string TargetKeyword,
    Guid? ClusterId,
    string? Title,
    IReadOnlyList<string> CompetitorUrls);

internal sealed record ContentAnalyzeJobOptions(
    bool IncludeContentSearch,
    bool IncludeHeadline,
    bool IncludeCoOccurrence,
    int Limit);

internal sealed record ArticleBriefExportSnapshot(
    int Version,
    Guid BriefId,
    string Format);

internal sealed record BriefDocument(
    string Title,
    string TargetKeyword,
    string SearchIntent,
    IReadOnlyList<BriefOutlineSection> Outline,
    IReadOnlyList<string> RequiredTerms,
    IReadOnlyList<string> Faq,
    IReadOnlyList<string> CompetitorUrls,
    BriefEvidence Evidence,
    DateTime GeneratedAt,
    string GeneratedBy);

internal sealed record BriefOutlineSection(short Level, string Heading);

internal sealed record BriefEvidence(
    IReadOnlyList<BriefContentEvidence> ContentResults,
    IReadOnlyList<BriefHeadlinePageEvidence> HeadlinePages,
    IReadOnlyList<string> CoOccurrenceWords);

internal sealed record BriefContentEvidence(
    string Url,
    string Title,
    string Description,
    decimal EstimatedTraffic,
    decimal TrafficValue);

internal sealed record BriefHeadlinePageEvidence(
    int Rank,
    string Url,
    string Title,
    IReadOnlyList<BriefHeadlineEvidence> Headlines);

internal sealed record BriefHeadlineEvidence(short Level, string Text);

internal sealed class ContentAnalyzeJob(
    SeoIntelligenceDbContext dbContext,
    ContentAnalysisService contentAnalysisService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<ContentAnalyzeJob> logger)
{
    public const string JobType = ContentAnalysisService.ContentAnalyzeJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Content analyze job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, ContentAnalysisService.ContentAnalysisResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Content analyze job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(20)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Content analyze job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var options = ContentAnalysisService.ReadAnalyzeOptions(job);
            var result = await contentAnalysisService.ExecuteContentAnalyzeAsync(
                context,
                jobId,
                job.ResultResourceId.Value,
                options);
            if (result.IsSuccess)
            {
                await jobService.CompleteAsync(
                    context,
                    jobId,
                    new JobCompletion(
                        100,
                        new JobResultResource(ContentAnalysisService.ContentAnalysisResourceType, job.ResultResourceId.Value)));
                return;
            }

            await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Content analyze job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Content analyze could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Content analyze job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Content analyze failed unexpectedly."));
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

internal sealed class GenerateBriefJob(
    SeoIntelligenceDbContext dbContext,
    ContentAnalysisService contentAnalysisService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<GenerateBriefJob> logger)
{
    public const string JobType = ContentAnalysisService.GenerateBriefJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Generate brief job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, ContentAnalysisService.ArticleBriefResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Generate brief job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Generate brief job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await contentAnalysisService.ExecuteGenerateBriefAsync(context, job.ResultResourceId.Value);
            if (result.IsSuccess)
            {
                await jobService.CompleteAsync(
                    context,
                    jobId,
                    new JobCompletion(
                        100,
                        new JobResultResource(ContentAnalysisService.ArticleBriefResourceType, job.ResultResourceId.Value)));
                return;
            }

            await jobService.RecordFailureAsync(context, jobId, new JobFailure(JobFailureKind.Unexpected, null, result.Error!.Code.ToString(), result.Error.Message));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Generate brief job {job_id} could not persist results.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Brief generation could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Generate brief job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Brief generation failed unexpectedly."));
        }
    }
}

internal sealed class ArticleBriefExportJob(
    SeoIntelligenceDbContext dbContext,
    ContentAnalysisService contentAnalysisService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<ArticleBriefExportJob> logger)
{
    public const string JobType = ContentAnalysisService.ArticleBriefExportJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Article brief export job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId?.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Article brief export job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await contentAnalysisService.ExecuteBriefExportAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await contentAnalysisService.RecordBriefExportFailureAsync(context, jobId, StatusValues.FailedFatal);
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
                    new JobResultResource(ContentAnalysisService.ArticleBriefExportResourceType, result.Value!.ExportId)));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Article brief export job {job_id} could not persist state.", jobId);
            await contentAnalysisService.RecordBriefExportFailureAsync(context, jobId, StatusValues.FailedRetryable);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Article brief export could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Article brief export job {job_id} failed unexpectedly.", jobId);
            await contentAnalysisService.RecordBriefExportFailureAsync(context, jobId, StatusValues.FailedFatal);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Article brief export failed unexpectedly."));
        }
    }
}
