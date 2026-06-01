using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class KeywordDiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapKeywordDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var keywordDiscovery = app.MapGroup("/api/projects/{projectId:guid}/keyword-discovery");
        keywordDiscovery.MapPost("/suggest", SuggestAsync);
        return app;
    }

    private static async Task<IResult> SuggestAsync(
        Guid projectId,
        [FromBody] KeywordDiscoveryRequest request,
        [FromServices] IKeywordDiscoveryService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.DiscoverAsync(
            contextService.Create(
                SeoIntelligenceSeedData.DefaultWorkspaceId,
                projectId,
                httpContext.GetCorrelationId()),
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResponseResults.FromError(httpContext, result.Error!);
        }

        var discovery = result.Value!;
        var meta = new ApiResponseMeta(
            JobId: discovery.JobId,
            ConsumedCredit: discovery.ConsumedCredit);
        return discovery.IsAccepted
            ? ApiResponseResults.Accepted(httpContext, discovery, meta)
            : ApiResponseResults.Ok(httpContext, discovery, meta);
    }
}
