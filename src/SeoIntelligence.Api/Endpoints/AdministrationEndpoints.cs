using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class AdministrationEndpoints
{
    private static readonly string[] ProjectSiteStatuses = ["active", "archived", "all"];
    private static readonly string[] EnabledStatuses = ["active", "disabled", "all"];
    private static readonly string[] DeliveryStatuses = ["pending", "retrying", "succeeded", "failed", "all"];
    private static readonly string[] AuditStatuses = ["all"];

    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin");

        admin.MapGet("/workspace", GetWorkspaceAsync);
        admin.MapPut("/workspace", UpdateWorkspaceAsync);

        admin.MapGet("/api-credentials", SearchApiCredentialsAsync);
        admin.MapPost("/api-credentials", CreateApiCredentialAsync);
        admin.MapGet("/api-credentials/{credentialId:guid}", GetApiCredentialAsync);
        admin.MapPut("/api-credentials/{credentialId:guid}", UpdateApiCredentialAsync);
        admin.MapDelete("/api-credentials/{credentialId:guid}", DisableApiCredentialAsync);
        admin.MapPost("/api-credentials/{credentialId:guid}/enable", EnableApiCredentialAsync);
        admin.MapPost("/api-credentials/{credentialId:guid}/rotate", RotateApiCredentialAsync);

        admin.MapGet("/notification-channels", SearchNotificationChannelsAsync);
        admin.MapPost("/notification-channels", CreateNotificationChannelAsync);
        admin.MapGet("/notification-channels/{channelId:guid}", GetNotificationChannelAsync);
        admin.MapPut("/notification-channels/{channelId:guid}", UpdateNotificationChannelAsync);
        admin.MapDelete("/notification-channels/{channelId:guid}", DisableNotificationChannelAsync);
        admin.MapPost("/notification-channels/{channelId:guid}/enable", EnableNotificationChannelAsync);
        admin.MapPost("/notification-channels/{channelId:guid}/test", TestNotificationChannelAsync);

        admin.MapGet("/notification-deliveries", SearchNotificationDeliveriesAsync);
        admin.MapGet("/notification-deliveries/{deliveryId:guid}", GetNotificationDeliveryAsync);
        admin.MapPost("/notification-deliveries/{deliveryId:guid}/retry", RetryNotificationDeliveryAsync);

        admin.MapGet("/external-api-calls", SearchExternalApiCallsAsync);
        admin.MapGet("/audit-logs", SearchAuditLogsAsync);
        admin.MapGet("/audit-logs/{auditLogId:guid}", GetAuditLogAsync);

        var projects = app.MapGroup("/api/projects");
        projects.MapGet("", SearchProjectsAsync);
        projects.MapPost("", CreateProjectAsync);
        projects.MapGet("/{projectId:guid}", GetProjectAsync);
        projects.MapPut("/{projectId:guid}", UpdateProjectAsync);
        projects.MapDelete("/{projectId:guid}", ArchiveProjectAsync);
        projects.MapPost("/{projectId:guid}/restore", RestoreProjectAsync);

        projects.MapGet("/{projectId:guid}/sites", SearchSitesAsync);
        projects.MapPost("/{projectId:guid}/sites", CreateSiteAsync);
        projects.MapGet("/{projectId:guid}/sites/{siteId:guid}", GetSiteAsync);
        projects.MapPut("/{projectId:guid}/sites/{siteId:guid}", UpdateSiteAsync);
        projects.MapDelete("/{projectId:guid}/sites/{siteId:guid}", ArchiveSiteAsync);
        projects.MapPost("/{projectId:guid}/sites/{siteId:guid}/restore", RestoreSiteAsync);

        return app;
    }

    private static async Task<IResult> GetWorkspaceAsync(
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetWorkspaceAsync(CreateContext(contextService, httpContext), cancellationToken));

    private static async Task<IResult> UpdateWorkspaceAsync(
        [FromBody] WorkspaceUpdateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateWorkspaceAsync(CreateContext(contextService, httpContext), request, cancellationToken));

    private static async Task<IResult> SearchApiCredentialsAsync(
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null)
    {
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, EnabledStatuses, ["provider", "status", "createdAt", "updatedAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, EnabledStatuses, ["provider", "status", "createdAt", "updatedAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchApiCredentialsAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> CreateApiCredentialAsync(
        [FromBody] ApiCredentialCreateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateApiCredentialAsync(CreateContext(contextService, httpContext), request, cancellationToken));

    private static async Task<IResult> GetApiCredentialAsync(
        Guid credentialId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetApiCredentialAsync(CreateContext(contextService, httpContext), credentialId, cancellationToken));

    private static async Task<IResult> UpdateApiCredentialAsync(
        Guid credentialId,
        [FromBody] ApiCredentialUpdateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateApiCredentialAsync(CreateContext(contextService, httpContext), credentialId, request, cancellationToken));

    private static async Task<IResult> DisableApiCredentialAsync(
        Guid credentialId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.DisableApiCredentialAsync(CreateContext(contextService, httpContext), credentialId, cancellationToken));

    private static async Task<IResult> EnableApiCredentialAsync(
        Guid credentialId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.EnableApiCredentialAsync(CreateContext(contextService, httpContext), credentialId, cancellationToken));

    private static async Task<IResult> RotateApiCredentialAsync(
        Guid credentialId,
        [FromBody] ApiCredentialRotateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.RotateApiCredentialAsync(CreateContext(contextService, httpContext), credentialId, request, cancellationToken));

    private static async Task<IResult> SearchNotificationChannelsAsync(
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null)
    {
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, EnabledStatuses, ["name", "channelType", "status", "createdAt", "updatedAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, EnabledStatuses, ["name", "channelType", "status", "createdAt", "updatedAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchNotificationChannelsAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> CreateNotificationChannelAsync(
        [FromBody] NotificationChannelCreateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateNotificationChannelAsync(CreateContext(contextService, httpContext), request, cancellationToken));

    private static async Task<IResult> GetNotificationChannelAsync(
        Guid channelId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetNotificationChannelAsync(CreateContext(contextService, httpContext), channelId, cancellationToken));

    private static async Task<IResult> UpdateNotificationChannelAsync(
        Guid channelId,
        [FromBody] NotificationChannelUpdateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateNotificationChannelAsync(CreateContext(contextService, httpContext), channelId, request, cancellationToken));

    private static async Task<IResult> DisableNotificationChannelAsync(
        Guid channelId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.DisableNotificationChannelAsync(CreateContext(contextService, httpContext), channelId, cancellationToken));

    private static async Task<IResult> EnableNotificationChannelAsync(
        Guid channelId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.EnableNotificationChannelAsync(CreateContext(contextService, httpContext), channelId, cancellationToken));

    private static async Task<IResult> TestNotificationChannelAsync(
        Guid channelId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.SendNotificationChannelTestAsync(CreateContext(contextService, httpContext), channelId, cancellationToken));

    private static async Task<IResult> SearchNotificationDeliveriesAsync(
        [FromServices] IAdministrationService service,
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
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, DeliveryStatuses, ["eventType", "status", "retryCount", "createdAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, DeliveryStatuses, ["eventType", "status", "retryCount", "createdAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchNotificationDeliveriesAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> GetNotificationDeliveryAsync(
        Guid deliveryId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetNotificationDeliveryAsync(CreateContext(contextService, httpContext), deliveryId, cancellationToken));

    private static async Task<IResult> RetryNotificationDeliveryAsync(
        Guid deliveryId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.RetryNotificationDeliveryAsync(CreateContext(contextService, httpContext), deliveryId, cancellationToken));

    private static async Task<IResult> SearchExternalApiCallsAsync(
        [FromServices] IAdministrationService service,
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
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, AuditStatuses, ["provider", "endpoint", "statusCode", "consumedCredit", "createdAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, AuditStatuses, ["provider", "endpoint", "statusCode", "consumedCredit", "createdAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchExternalApiCallsAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> SearchAuditLogsAsync(
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null,
        string? actor = null,
        string? resourceType = null,
        string? resourceId = null,
        string? correlationId = null,
        [FromQuery(Name = "correlation_id")] string? correlationIdSnake = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var query = CreateAuditLogSearchQuery(page, pageSize, status, sortBy, orderBy, q, actor, resourceType, resourceId, correlationId ?? correlationIdSnake, from, to);
        return query is null
            ? InvalidAuditLogQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, actor, resourceType, resourceId, correlationId ?? correlationIdSnake, from, to)
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchAuditLogsAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> GetAuditLogAsync(
        Guid auditLogId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetAuditLogAsync(CreateContext(contextService, httpContext), auditLogId, cancellationToken));

    private static async Task<IResult> SearchProjectsAsync(
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null)
    {
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, ProjectSiteStatuses, ["name", "status", "createdAt", "updatedAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, ProjectSiteStatuses, ["name", "status", "createdAt", "updatedAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchProjectsAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> CreateProjectAsync(
        [FromBody] ProjectCreateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateProjectAsync(CreateContext(contextService, httpContext), request, cancellationToken));

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetProjectAsync(CreateContext(contextService, httpContext), projectId, cancellationToken));

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        [FromBody] ProjectUpdateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateProjectAsync(CreateContext(contextService, httpContext), projectId, request, cancellationToken));

    private static async Task<IResult> ArchiveProjectAsync(
        Guid projectId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.ArchiveProjectAsync(CreateContext(contextService, httpContext), projectId, cancellationToken));

    private static async Task<IResult> RestoreProjectAsync(
        Guid projectId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.RestoreProjectAsync(CreateContext(contextService, httpContext), projectId, cancellationToken));

    private static async Task<IResult> SearchSitesAsync(
        Guid projectId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "active",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null)
    {
        var query = CreateSearchQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, ProjectSiteStatuses, ["domain", "type", "status", "createdAt", "updatedAt"]);
        return query is null
            ? InvalidQuery(httpContext, page, pageSize, status, sortBy, orderBy, q, ProjectSiteStatuses, ["domain", "type", "status", "createdAt", "updatedAt"])
            : ApiResponseResults.FromPagedResult(
                httpContext,
                await service.SearchSitesAsync(CreateContext(contextService, httpContext, projectId), projectId, query, cancellationToken));
    }

    private static async Task<IResult> CreateSiteAsync(
        Guid projectId,
        [FromBody] SiteCreateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromCreatedResult(
            httpContext,
            await service.CreateSiteAsync(CreateContext(contextService, httpContext, projectId), projectId, request, cancellationToken));

    private static async Task<IResult> GetSiteAsync(
        Guid projectId,
        Guid siteId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetSiteAsync(CreateContext(contextService, httpContext, projectId), projectId, siteId, cancellationToken));

    private static async Task<IResult> UpdateSiteAsync(
        Guid projectId,
        Guid siteId,
        [FromBody] SiteUpdateRequest request,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.UpdateSiteAsync(CreateContext(contextService, httpContext, projectId), projectId, siteId, request, cancellationToken));

    private static async Task<IResult> ArchiveSiteAsync(
        Guid projectId,
        Guid siteId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.ArchiveSiteAsync(CreateContext(contextService, httpContext, projectId), projectId, siteId, cancellationToken));

    private static async Task<IResult> RestoreSiteAsync(
        Guid projectId,
        Guid siteId,
        [FromServices] IAdministrationService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.RestoreSiteAsync(CreateContext(contextService, httpContext, projectId), projectId, siteId, cancellationToken));

    private static ProjectContext CreateContext(
        IProjectContextService contextService,
        HttpContext httpContext,
        Guid? projectId = null)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            projectId,
            httpContext.GetCorrelationId());

    private static SearchQuery? CreateSearchQuery(
        HttpContext httpContext,
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        IReadOnlyList<string> allowedStatuses,
        IReadOnlyList<string> allowedSortBy)
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

        if (parameters.Validate(allowedStatuses, allowedSortBy).Count > 0)
        {
            return null;
        }

        return new SearchQuery(
            parameters.Q,
            parameters.Status.Trim(),
            new SortRequest(
                parameters.SortBy.Trim(),
                string.Equals(parameters.OrderBy, "asc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Asc
                    : SortDirection.Desc),
            new PageRequest(parameters.Page, parameters.PageSize));
    }

    private static AuditLogSearchQuery? CreateAuditLogSearchQuery(
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        string? actor,
        string? resourceType,
        string? resourceId,
        string? correlationId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (ValidateAuditLogQueryParameters(page, pageSize, status, sortBy, orderBy, q, actor, resourceType, resourceId, correlationId, from, to).Count > 0)
        {
            return null;
        }

        return new AuditLogSearchQuery(
            new SearchQuery(
                q,
                status.Trim(),
                new SortRequest(
                    sortBy.Trim(),
                    string.Equals(orderBy, "asc", StringComparison.OrdinalIgnoreCase)
                        ? SortDirection.Asc
                        : SortDirection.Desc),
                new PageRequest(page, pageSize)),
            actor,
            resourceType,
            resourceId,
            correlationId,
            from,
            to);
    }

    private static IReadOnlyDictionary<string, string[]> ValidateAuditLogQueryParameters(
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        string? actor,
        string? resourceType,
        string? resourceId,
        string? correlationId,
        DateTimeOffset? from,
        DateTimeOffset? to)
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

        var errors = parameters
            .Validate(AuditStatuses, ["actor", "action", "resourceType", "createdAt"])
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);

        ValidateOptionalFilterText(actor, nameof(actor), errors);
        ValidateOptionalFilterText(resourceType, nameof(resourceType), errors);
        ValidateOptionalFilterText(resourceId, nameof(resourceId), errors);
        ValidateOptionalFilterText(correlationId, nameof(correlationId), errors);

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            AddValidationError(errors, nameof(from), "from must be earlier than or equal to to.");
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateOptionalFilterText(
        string? value,
        string target,
        IDictionary<string, List<string>> errors)
    {
        if (value is { Length: > ListQueryParameters.MaxSearchTextLength })
        {
            AddValidationError(errors, target, $"{target} must be {ListQueryParameters.MaxSearchTextLength} characters or fewer.");
        }
    }

    private static void AddValidationError(
        IDictionary<string, List<string>> errors,
        string target,
        string message)
    {
        if (!errors.TryGetValue(target, out var messages))
        {
            messages = [];
            errors[target] = messages;
        }

        messages.Add(message);
    }

    private static IResult InvalidAuditLogQuery(
        HttpContext httpContext,
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        string? actor,
        string? resourceType,
        string? resourceId,
        string? correlationId,
        DateTimeOffset? from,
        DateTimeOffset? to)
        => ApiResponseResults.ValidationFailure(
            httpContext,
            ValidateAuditLogQueryParameters(page, pageSize, status, sortBy, orderBy, q, actor, resourceType, resourceId, correlationId, from, to));

    private static IResult InvalidQuery(
        HttpContext httpContext,
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        IReadOnlyList<string> allowedStatuses,
        IReadOnlyList<string> allowedSortBy)
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

        return ApiResponseResults.ValidationFailure(httpContext, parameters.Validate(allowedStatuses, allowedSortBy));
    }
}
