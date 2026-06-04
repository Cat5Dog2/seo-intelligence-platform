using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class CompetitiveAnalysisEndpoints
{
    private static readonly string[] CompetitorSortFields =
    [
        "domain",
        "duplicateRate",
        "estimatedTraffic",
        "trafficValue",
        "keywordCount",
        "gapKeywordCount",
        "createdAt"
    ];

    private static readonly string[] InfluxKeywordSortFields =
    [
        "target",
        "keyword",
        "rank",
        "estimatedTraffic",
        "createdAt"
    ];

    private static readonly string[] InfluxPageSortFields =
    [
        "target",
        "pageUrl",
        "keywordCount",
        "estimatedTraffic",
        "trafficValue",
        "createdAt"
    ];

    public static IEndpointRouteBuilder MapCompetitiveAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup("/api/projects/{projectId:guid}");

        project.MapGet("/competitors", GetCompetitorsAsync);
        project.MapPost("/competitors/analyze", AnalyzeAsync);
        project.MapGet("/influx-keywords", GetInfluxKeywordsAsync);
        project.MapGet("/influx-pages", GetInfluxPagesAsync);

        return app;
    }

    private static async Task<IResult> AnalyzeAsync(
        Guid projectId,
        [FromBody] CompetitorAnalyzeRequest request,
        [FromServices] ICompetitiveAnalysisService service,
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

    private static async Task<IResult> GetCompetitorsAsync(
        Guid projectId,
        [FromServices] ICompetitiveAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "duplicateRate",
        string orderBy = "desc",
        string? q = null,
        string? domain = null)
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
        var validationErrors = parameters.Validate(["all"], CompetitorSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        var query = new CompetitorSearchQuery(
            CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
            domain);
        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetCompetitorsAsync(
                CreateContext(contextService, httpContext, projectId),
                query,
                cancellationToken));
    }

    private static async Task<IResult> GetInfluxKeywordsAsync(
        Guid projectId,
        [FromServices] ICompetitiveAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "rank",
        string orderBy = "asc",
        string? q = null,
        string? target = null,
        int? minRank = null,
        int? maxRank = null)
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
        var validationErrors = parameters.Validate(["all"], InfluxKeywordSortFields);
        if (minRank is < 1)
        {
            validationErrors = AddValidationError(validationErrors, "minRank", "minRank must be greater than or equal to 1.");
        }

        if (maxRank is < 1)
        {
            validationErrors = AddValidationError(validationErrors, "maxRank", "maxRank must be greater than or equal to 1.");
        }

        if (minRank.HasValue && maxRank.HasValue && minRank.Value > maxRank.Value)
        {
            validationErrors = AddValidationError(validationErrors, "maxRank", "maxRank must be greater than or equal to minRank.");
        }

        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        var query = new InfluxKeywordSearchQuery(
            CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
            target,
            minRank,
            maxRank);
        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetInfluxKeywordsAsync(
                CreateContext(contextService, httpContext, projectId),
                query,
                cancellationToken));
    }

    private static async Task<IResult> GetInfluxPagesAsync(
        Guid projectId,
        [FromServices] ICompetitiveAnalysisService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "estimatedTraffic",
        string orderBy = "desc",
        string? q = null,
        string? target = null)
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
        var validationErrors = parameters.Validate(["all"], InfluxPageSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        var query = new InfluxPageSearchQuery(
            CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
            target);
        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetInfluxPagesAsync(
                CreateContext(contextService, httpContext, projectId),
                query,
                cancellationToken));
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
