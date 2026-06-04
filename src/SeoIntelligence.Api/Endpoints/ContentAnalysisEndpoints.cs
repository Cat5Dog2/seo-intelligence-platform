using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class ContentAnalysisEndpoints
{
    private static readonly string[] ContentAnalysisSortFields =
    [
        "keyword",
        "contentSearchCount",
        "headlinePageCount",
        "coOccurrenceWordCount",
        "lastAnalyzedAt"
    ];

    private static readonly string[] BriefSortFields =
    [
        "title",
        "targetKeyword",
        "currentVersion",
        "reviewStatus",
        "status",
        "createdAt",
        "updatedAt"
    ];

    private static readonly string[] VersionSortFields =
    [
        "versionNo",
        "createdAt"
    ];

    public static IEndpointRouteBuilder MapContentAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup("/api/projects/{projectId:guid}");

        project.MapGet("/content-analyses", GetContentAnalysesAsync);
        project.MapPost("/content/analyze", AnalyzeAsync);
        project.MapGet("/briefs", GetBriefsAsync);
        project.MapPost("/briefs/generate", GenerateBriefAsync);
        project.MapGet("/briefs/{briefId:guid}", GetBriefAsync);
        project.MapPut("/briefs/{briefId:guid}", UpdateBriefAsync);
        project.MapGet("/briefs/{briefId:guid}/versions", GetBriefVersionsAsync);
        project.MapPost("/briefs/{briefId:guid}/export", ExportBriefAsync);

        return app;
    }

    private static async Task<IResult> AnalyzeAsync(
        Guid projectId,
        [FromBody] ContentAnalyzeRequest request,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.AnalyzeAsync(
            CreateContext(contextService, httpContext, projectId),
            request,
            cancellationToken);
        return result.IsSuccess
            ? ApiResponseResults.Accepted(
                httpContext,
                result.Value!,
                new ApiResponseMeta(JobId: result.Value!.JobId))
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static async Task<IResult> GetContentAnalysesAsync(
        Guid projectId,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "lastAnalyzedAt",
        string orderBy = "desc",
        string? q = null,
        Guid? keywordId = null)
    {
        var parameters = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            SortBy = sortBy,
            OrderBy = orderBy,
            Q = q
        };
        var validationErrors = parameters.Validate(["all"], ContentAnalysisSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetContentAnalysesAsync(
                CreateContext(contextService, httpContext, projectId),
                new ContentAnalysisSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    keywordId),
                cancellationToken));
    }

    private static async Task<IResult> GetBriefsAsync(
        Guid projectId,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "updatedAt",
        string orderBy = "desc",
        string? q = null,
        Guid? targetKeywordId = null,
        Guid? clusterId = null,
        string? reviewStatus = null)
    {
        var parameters = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            SortBy = sortBy,
            OrderBy = orderBy,
            Q = q
        };
        var validationErrors = parameters.Validate(["all", "draft", "active", "archived"], BriefSortFields);
        if (reviewStatus is not null &&
            !new[] { "pending", "reviewed", "rejected" }.Contains(reviewStatus, StringComparer.OrdinalIgnoreCase))
        {
            validationErrors = AddValidationError(validationErrors, "reviewStatus", "reviewStatus must be pending, reviewed, or rejected.");
        }

        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetBriefsAsync(
                CreateContext(contextService, httpContext, projectId),
                new ArticleBriefSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    targetKeywordId,
                    clusterId,
                    reviewStatus),
                cancellationToken));
    }

    private static async Task<IResult> GenerateBriefAsync(
        Guid projectId,
        [FromBody] GenerateBriefRequest request,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GenerateBriefAsync(
            CreateContext(contextService, httpContext, projectId),
            request,
            cancellationToken);
        return result.IsSuccess
            ? ApiResponseResults.Accepted(
                httpContext,
                result.Value!,
                new ApiResponseMeta(JobId: result.Value!.JobId))
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static async Task<IResult> GetBriefAsync(
        Guid projectId,
        Guid briefId,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetBriefAsync(
                CreateContext(contextService, httpContext, projectId),
                briefId,
                cancellationToken));

    private static async Task<IResult> UpdateBriefAsync(
        Guid projectId,
        Guid briefId,
        [FromBody] ArticleBriefUpdateRequest request,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateBriefAsync(
                CreateContext(contextService, httpContext, projectId),
                briefId,
                request,
                cancellationToken));

    private static async Task<IResult> GetBriefVersionsAsync(
        Guid projectId,
        Guid briefId,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "versionNo",
        string orderBy = "desc",
        string? q = null)
    {
        var parameters = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            SortBy = sortBy,
            OrderBy = orderBy,
            Q = q
        };
        var validationErrors = parameters.Validate(["all"], VersionSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetBriefVersionsAsync(
                CreateContext(contextService, httpContext, projectId),
                briefId,
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> ExportBriefAsync(
        Guid projectId,
        Guid briefId,
        [FromBody] ArticleBriefExportRequest request,
        [FromServices] IContentAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ExportBriefAsync(
            CreateContext(contextService, httpContext, projectId),
            briefId,
            request,
            cancellationToken);
        return result.IsSuccess
            ? ApiResponseResults.Accepted(
                httpContext,
                result.Value!,
                new ApiResponseMeta(JobId: result.Value!.JobId))
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static SearchQuery CreateSearchQuery(
        string? q,
        string status,
        string sortBy,
        string orderBy,
        int page,
        int pageSize)
        => new(
            q,
            status,
            new SortRequest(
                sortBy.Trim(),
                string.Equals(orderBy, "asc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Asc
                    : SortDirection.Desc),
            new PageRequest(page, pageSize));

    private static IReadOnlyDictionary<string, string[]> AddValidationError(
        IReadOnlyDictionary<string, string[]> current,
        string target,
        string message)
    {
        var errors = current.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
        if (!errors.TryGetValue(target, out var messages))
        {
            messages = [];
            errors[target] = messages;
        }

        messages.Add(message);
        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static ProjectContext CreateContext(
        IProjectContextService contextService,
        HttpContext httpContext,
        Guid projectId)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            projectId,
            httpContext.GetCorrelationId());
}
