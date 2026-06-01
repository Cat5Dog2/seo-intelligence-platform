using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/dashboard", GetDashboardAsync);
        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        Guid projectId,
        [FromServices] IDashboardService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetDashboardAsync(
                contextService.Create(
                    SeoIntelligenceSeedData.DefaultWorkspaceId,
                    projectId,
                    httpContext.GetCorrelationId()),
                cancellationToken));
}
