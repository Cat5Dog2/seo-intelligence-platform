using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class TopicClusterEndpoints
{
    private static readonly string[] ClusterSortFields =
    [
        "name",
        "representativeKeyword",
        "score",
        "keywordCount",
        "childCount",
        "createdAt",
        "updatedAt"
    ];

    private static readonly string[] IntentLabels =
    [
        "informational",
        "commercial",
        "transactional",
        "navigational"
    ];

    public static IEndpointRouteBuilder MapTopicClusterEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup("/api/projects/{projectId:guid}");

        project.MapGet("/clusters", GetClustersAsync);
        project.MapGet("/clusters/{clusterId:guid}", GetClusterAsync);
        project.MapPost("/clusters/generate", GenerateAsync);

        return app;
    }

    private static async Task<IResult> GenerateAsync(
        Guid projectId,
        [FromBody] TopicClusterGenerateRequest request,
        [FromServices] ITopicClusterService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GenerateAsync(
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

    private static async Task<IResult> GetClustersAsync(
        Guid projectId,
        [FromServices] ITopicClusterService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "score",
        string orderBy = "desc",
        string? q = null,
        Guid? parentId = null,
        string? intentLabel = null)
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
        var validationErrors = parameters.Validate(["all"], ClusterSortFields);
        if (parentId == Guid.Empty)
        {
            validationErrors = AddValidationError(validationErrors, "parentId", "parentId must not be empty when provided.");
        }

        if (intentLabel is not null &&
            !IntentLabels.Contains(intentLabel, StringComparer.OrdinalIgnoreCase))
        {
            validationErrors = AddValidationError(
                validationErrors,
                "intentLabel",
                "intentLabel must be informational, commercial, transactional, or navigational.");
        }

        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetClustersAsync(
                CreateContext(contextService, httpContext, projectId),
                new TopicClusterSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    parentId,
                    intentLabel),
                cancellationToken));
    }

    private static async Task<IResult> GetClusterAsync(
        Guid projectId,
        Guid clusterId,
        [FromServices] ITopicClusterService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetClusterAsync(
                CreateContext(contextService, httpContext, projectId),
                clusterId,
                cancellationToken));

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
