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
    private static readonly string[] ConnectorStatuses =
    [
        "active",
        "disabled",
        "all"
    ];
    private static readonly string[] ConnectorSortFields =
    [
        "connectorType",
        "name",
        "status",
        "createdAt",
        "updatedAt"
    ];
    private static readonly string[] ConnectorRunStatuses =
    [
        "queued",
        "running",
        "waiting_external",
        "succeeded",
        "failed_retryable",
        "failed_fatal",
        "canceled",
        "all"
    ];
    private static readonly string[] ConnectorRunSortFields =
    [
        "runType",
        "status",
        "startedAt",
        "completedAt",
        "createdAt"
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

        var reports = project.MapGroup(Phase3EndpointRoutes.Reports);
        reports.MapPost("", CreateReportAsync);
        reports.MapGet("/{reportId:guid}", GetReportAsync);
        reports.MapGet("/{reportId:guid}/download", CreateReportDownloadUrlAsync);
        reports.MapPost("/{reportId:guid}/share", ShareReportAsync);
        reports.MapDelete("/{reportId:guid}/share", RevokeReportShareAsync);

        _ = project.MapGroup(Phase3EndpointRoutes.Exports);
        _ = project.MapGroup(Phase3EndpointRoutes.Imports);

        var connectors = project.MapGroup(Phase3EndpointRoutes.Connectors);
        connectors.MapGet("", GetConnectorsAsync);
        connectors.MapPost("", CreateConnectorAsync);
        connectors.MapPut("/{connectorId:guid}", UpdateConnectorAsync);
        connectors.MapDelete("/{connectorId:guid}", DisableConnectorAsync);
        connectors.MapPost("/{connectorId:guid}/test", TestConnectorAsync);
        connectors.MapGet("/{connectorId:guid}/runs", GetConnectorRunsAsync);

        var ai = project.MapGroup(Phase3EndpointRoutes.Ai);
        ai.MapPost("/chat", ChatWithAiAsync);
        var reportShares = app.MapGroup(Phase3EndpointRoutes.ReportShares);

        // Report share links are handed to people outside the application, so this one endpoint is
        // reachable without a service key; the share token itself is the access control. Applied to
        // the endpoint rather than the group so anything added here later stays authenticated.
        reportShares.MapGet("/{token}", GetSharedReportAsync).AllowAnonymous();

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

    private static async Task<IResult> CreateReportAsync(
        Guid projectId,
        [FromBody] ReportRequest request,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateReportAsync(
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

    private static async Task<IResult> GetReportAsync(
        Guid projectId,
        Guid reportId,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetReportAsync(
                CreateContext(contextService, httpContext, projectId),
                reportId,
                cancellationToken));

    private static async Task<IResult> CreateReportDownloadUrlAsync(
        Guid projectId,
        Guid reportId,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.CreateDownloadUrlAsync(
                CreateContext(contextService, httpContext, projectId),
                reportId,
                cancellationToken));

    private static async Task<IResult> ShareReportAsync(
        Guid projectId,
        Guid reportId,
        [FromBody] ReportShareRequest request,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.ShareReportAsync(
                CreateContext(contextService, httpContext, projectId),
                reportId,
                request,
                cancellationToken));

    private static async Task<IResult> RevokeReportShareAsync(
        Guid projectId,
        Guid reportId,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.RevokeShareAsync(
                CreateContext(contextService, httpContext, projectId),
                reportId,
                cancellationToken));

    private static async Task<IResult> GetSharedReportAsync(
        string token,
        [FromServices] IReportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetSharedReportAsync(
                CreateSharedContext(contextService, httpContext),
                token,
                cancellationToken));

    private static async Task<IResult> GetConnectorsAsync(
        Guid projectId,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "updatedAt",
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
            ConnectorStatuses,
            ConnectorSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchConnectorsAsync(
                CreateContext(contextService, httpContext, projectId),
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> CreateConnectorAsync(
        Guid projectId,
        [FromBody] ConnectorSettingsRequest request,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateConnectorAsync(
                CreateContext(contextService, httpContext, projectId),
                request,
                cancellationToken));

    private static async Task<IResult> UpdateConnectorAsync(
        Guid projectId,
        Guid connectorId,
        [FromBody] ConnectorSettingsRequest request,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateConnectorAsync(
                CreateContext(contextService, httpContext, projectId),
                connectorId,
                request,
                cancellationToken));

    private static async Task<IResult> DisableConnectorAsync(
        Guid projectId,
        Guid connectorId,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.DisableConnectorAsync(
                CreateContext(contextService, httpContext, projectId),
                connectorId,
                cancellationToken));

    private static async Task<IResult> TestConnectorAsync(
        Guid projectId,
        Guid connectorId,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.TestConnectorAsync(
                CreateContext(contextService, httpContext, projectId),
                connectorId,
                cancellationToken));

    private static async Task<IResult> GetConnectorRunsAsync(
        Guid projectId,
        Guid connectorId,
        [FromServices] IExternalConnectorService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "createdAt",
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
            ConnectorRunStatuses,
            ConnectorRunSortFields);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetConnectorRunsAsync(
                CreateContext(contextService, httpContext, projectId),
                connectorId,
                CreateSearchQuery(q, status, sortBy, orderBy, page, pageSize),
                cancellationToken));
    }

    private static async Task<IResult> ChatWithAiAsync(
        Guid projectId,
        [FromBody] AiChatRequest request,
        [FromServices] IAiAssistantService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ChatAsync(
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

    private static ProjectContext CreateSharedContext(
        IProjectContextService contextService,
        HttpContext httpContext)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            correlationId: httpContext.GetCorrelationId());
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
