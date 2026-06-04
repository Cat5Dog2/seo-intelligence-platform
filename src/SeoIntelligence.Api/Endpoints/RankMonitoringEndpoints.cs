using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class RankMonitoringEndpoints
{
    private static readonly string[] RankResultSortFields =
    [
        "keyword",
        "target",
        "position",
        "previousPosition",
        "positionDelta",
        "estimatedTraffic",
        "checkedAt"
    ];

    private static readonly string[] AlertSortFields =
    [
        "alertType",
        "status",
        "lastTriggeredAt",
        "createdAt",
        "updatedAt"
    ];

    private static readonly string[] AlertEventSortFields =
    [
        "eventType",
        "keyword",
        "triggeredAt"
    ];

    public static IEndpointRouteBuilder MapRankMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup("/api/projects/{projectId:guid}");

        project.MapPost("/rank-check/jobs", RegisterRankCheckAsync);
        project.MapGet("/rank-check/jobs/{jobId:guid}/results", GetRankCheckJobResultsAsync);
        project.MapGet("/rank-results", GetRankResultsAsync);
        project.MapGet("/alerts", GetAlertsAsync);
        project.MapPost("/alerts", CreateAlertAsync);
        project.MapPut("/alerts/{alertId:guid}", UpdateAlertAsync);
        project.MapDelete("/alerts/{alertId:guid}", DisableAlertAsync);
        project.MapPost("/alerts/{alertId:guid}/enable", EnableAlertAsync);
        project.MapGet("/alert-events", GetAlertEventsAsync);

        return app;
    }

    private static async Task<IResult> RegisterRankCheckAsync(
        Guid projectId,
        [FromBody] RankCheckJobRequest request,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterRankCheckAsync(
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

    private static async Task<IResult> GetRankCheckJobResultsAsync(
        Guid projectId,
        Guid jobId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "checkedAt",
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
            ["all"],
            RankResultSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetJobResultsAsync(
                CreateContext(contextService, httpContext, projectId),
                jobId,
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> GetRankResultsAsync(
        Guid projectId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "checkedAt",
        string orderBy = "desc",
        string? q = null,
        Guid? jobId = null,
        Guid? keywordId = null,
        string? target = null,
        int? minPosition = null,
        int? maxPosition = null)
    {
        var validationErrors = ValidateListQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            ["all"],
            RankResultSortFields);
        if (minPosition is < 1)
        {
            validationErrors = AddValidationError(validationErrors, "minPosition", "minPosition must be greater than or equal to 1.");
        }

        if (maxPosition is < 1)
        {
            validationErrors = AddValidationError(validationErrors, "maxPosition", "maxPosition must be greater than or equal to 1.");
        }

        if (minPosition.HasValue && maxPosition.HasValue && minPosition.Value > maxPosition.Value)
        {
            validationErrors = AddValidationError(validationErrors, "maxPosition", "maxPosition must be greater than or equal to minPosition.");
        }

        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromResult(
            httpContext,
            await service.SearchRankResultsAsync(
                CreateContext(contextService, httpContext, projectId),
                new RankResultSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    jobId,
                    keywordId,
                    target,
                    minPosition,
                    maxPosition),
                cancellationToken));
    }

    private static async Task<IResult> GetAlertsAsync(
        Guid projectId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null,
        string? alertType = null)
    {
        var validationErrors = ValidateListQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            ["active", "disabled", "all"],
            AlertSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchAlertsAsync(
                CreateContext(contextService, httpContext, projectId),
                new RankAlertSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    alertType),
                cancellationToken));
    }

    private static async Task<IResult> CreateAlertAsync(
        Guid projectId,
        [FromBody] RankAlertCreateRequest request,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateAlertAsync(
                CreateContext(contextService, httpContext, projectId),
                request,
                cancellationToken));

    private static async Task<IResult> UpdateAlertAsync(
        Guid projectId,
        Guid alertId,
        [FromBody] RankAlertUpdateRequest request,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateAlertAsync(
                CreateContext(contextService, httpContext, projectId),
                alertId,
                request,
                cancellationToken));

    private static async Task<IResult> DisableAlertAsync(
        Guid projectId,
        Guid alertId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.DisableAlertAsync(
                CreateContext(contextService, httpContext, projectId),
                alertId,
                cancellationToken));

    private static async Task<IResult> EnableAlertAsync(
        Guid projectId,
        Guid alertId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.EnableAlertAsync(
                CreateContext(contextService, httpContext, projectId),
                alertId,
                cancellationToken));

    private static async Task<IResult> GetAlertEventsAsync(
        Guid projectId,
        [FromServices] IRankMonitoringService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "triggeredAt",
        string orderBy = "desc",
        string? q = null,
        Guid? alertId = null,
        string? eventType = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var validationErrors = ValidateListQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            ["all"],
            AlertEventSortFields);
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            validationErrors = AddValidationError(validationErrors, "to", "to must be greater than or equal to from.");
        }

        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchAlertEventsAsync(
                CreateContext(contextService, httpContext, projectId),
                new RankAlertEventSearchQuery(
                    CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                    alertId,
                    eventType,
                    from,
                    to),
                cancellationToken));
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
