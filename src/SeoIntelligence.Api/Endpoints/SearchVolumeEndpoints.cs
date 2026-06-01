using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class SearchVolumeEndpoints
{
    private static readonly string[] ResultSortFields =
    [
        "keyword",
        "searchVolume",
        "seoDifficulty",
        "cpc",
        "competition"
    ];

    public static IEndpointRouteBuilder MapSearchVolumeEndpoints(this IEndpointRouteBuilder app)
    {
        var searchVolume = app.MapGroup("/api/projects/{projectId:guid}/search-volume");

        searchVolume.MapPost("/jobs", RegisterJobAsync);
        searchVolume.MapGet("/jobs/{jobId:guid}", GetJobAsync);
        searchVolume.MapGet("/jobs/{jobId:guid}/results", GetResultsAsync);

        return app;
    }

    private static async Task<IResult> RegisterJobAsync(
        Guid projectId,
        [FromBody] SearchVolumeJobRequest request,
        [FromServices] ISearchVolumeService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(
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

    private static async Task<IResult> GetJobAsync(
        Guid projectId,
        Guid jobId,
        [FromServices] ISearchVolumeService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GetJobAsync(
            CreateContext(contextService, httpContext, projectId),
            jobId,
            cancellationToken);
        return result.IsSuccess
            ? ApiResponseResults.Ok(
                httpContext,
                result.Value!,
                new ApiResponseMeta(JobId: result.Value!.JobId))
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static async Task<IResult> GetResultsAsync(
        Guid projectId,
        Guid jobId,
        [FromServices] ISearchVolumeService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "keyword",
        string orderBy = "asc",
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
        var validationErrors = parameters.Validate(["all"], ResultSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        var query = new SearchQuery(
            q,
            status,
            new SortRequest(
                sortBy.Trim(),
                string.Equals(orderBy, "asc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Asc
                    : SortDirection.Desc),
            new PageRequest(page, pageSize));

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetResultsAsync(
                CreateContext(contextService, httpContext, projectId),
                jobId,
                query,
                cancellationToken));
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
