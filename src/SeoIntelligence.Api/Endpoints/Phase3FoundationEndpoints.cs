using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class Phase3FoundationEndpoints
{
    private static readonly string[] RewriteTaskSortFields =
    [
        "targetUrl",
        "priorityScore",
        "status",
        "assigneeActor",
        "createdAt",
        "updatedAt"
    ];

    private static readonly string[] CannibalizationCandidateSortFields =
    [
        "keyword",
        "primaryUrl",
        "severityScore",
        "status",
        "detectedAt"
    ];

    private static readonly string[] ContentStatuses =
    [
        "draft",
        "active",
        "archived",
        "completed",
        "all"
    ];

    public static IEndpointRouteBuilder MapPhase3FoundationEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup(Phase3EndpointRoutes.ProjectBase);

        var rewrite = project.MapGroup(Phase3EndpointRoutes.Rewrite);
        rewrite.MapGet("/tasks", GetRewriteTasksAsync);
        rewrite.MapGet("/tasks/{taskId:guid}", GetRewriteTaskAsync);
        rewrite.MapPut("/tasks/{taskId:guid}", UpdateRewriteTaskAsync);

        var cannibalization = project.MapGroup(Phase3EndpointRoutes.Cannibalization);
        cannibalization.MapGet("/candidates", GetCannibalizationCandidatesAsync);
        cannibalization.MapPost("/refresh", RefreshCannibalizationAsync);

        _ = project.MapGroup(Phase3EndpointRoutes.Reports);
        _ = project.MapGroup(Phase3EndpointRoutes.Exports);
        _ = project.MapGroup(Phase3EndpointRoutes.Imports);
        _ = project.MapGroup(Phase3EndpointRoutes.Connectors);
        _ = project.MapGroup(Phase3EndpointRoutes.Ai);
        _ = app.MapGroup(Phase3EndpointRoutes.ReportShares);

        return app;
    }

    private static async Task<IResult> GetRewriteTasksAsync(
        Guid projectId,
        [FromServices] IRewriteManagementService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "priorityScore",
        string orderBy = "desc",
        string? q = null)
    {
        var validationErrors = ValidateListQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            ContentStatuses,
            RewriteTaskSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchRewriteTasksAsync(
                CreateContext(contextService, httpContext, projectId),
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> GetRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        [FromServices] IRewriteManagementService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetRewriteTaskAsync(
                CreateContext(contextService, httpContext, projectId),
                taskId,
                cancellationToken));

    private static async Task<IResult> UpdateRewriteTaskAsync(
        Guid projectId,
        Guid taskId,
        [FromBody] RewriteTaskUpdateRequest request,
        [FromServices] IRewriteManagementService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateRewriteTaskAsync(
                CreateContext(contextService, httpContext, projectId),
                taskId,
                request,
                cancellationToken));

    private static async Task<IResult> GetCannibalizationCandidatesAsync(
        Guid projectId,
        [FromServices] IRewriteManagementService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "severityScore",
        string orderBy = "desc",
        string? q = null)
    {
        var validationErrors = ValidateListQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            ContentStatuses,
            CannibalizationCandidateSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchCannibalizationCandidatesAsync(
                CreateContext(contextService, httpContext, projectId),
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> RefreshCannibalizationAsync(
        Guid projectId,
        [FromServices] IRewriteManagementService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.RefreshCannibalizationAsync(
            CreateContext(contextService, httpContext, projectId),
            cancellationToken);
        return result.IsSuccess
            ? ApiResponseResults.Accepted(
                httpContext,
                result.Value!,
                new ApiResponseMeta(JobId: result.Value!.JobId))
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static IReadOnlyDictionary<string, string[]> ValidateListQuery(
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        string[] allowedStatuses,
        string[] allowedSortFields)
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
        return parameters.Validate(allowedStatuses, allowedSortFields);
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

    private static ProjectContext CreateContext(
        IProjectContextService contextService,
        HttpContext httpContext,
        Guid projectId)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            projectId,
            httpContext.GetCorrelationId());
}

internal static class Phase3EndpointRoutes
{
    public const string ProjectBase = "/api/projects/{projectId:guid}";
    public const string Rewrite = "/rewrite";
    public const string Cannibalization = "/cannibalization";
    public const string Reports = "/reports";
    public const string Exports = "/exports";
    public const string Imports = "/imports";
    public const string Connectors = "/connectors";
    public const string Ai = "/ai";
    public const string ReportShares = "/api/report-shares";
}
