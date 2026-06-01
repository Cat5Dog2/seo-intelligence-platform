using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class DataExportEndpoints
{
    public static IEndpointRouteBuilder MapDataExportEndpoints(this IEndpointRouteBuilder app)
    {
        var exports = app.MapGroup("/api/projects/{projectId:guid}/exports");

        exports.MapPost("/csv", CreateCsvExportAsync);
        exports.MapGet("/{exportId:guid}", GetExportAsync);
        exports.MapGet("/{exportId:guid}/download", CreateDownloadUrlAsync);

        return app;
    }

    private static async Task<IResult> CreateCsvExportAsync(
        Guid projectId,
        [FromBody] DataExportRequest request,
        [FromServices] IDataTransferService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateCsvExportAsync(
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

    private static async Task<IResult> GetExportAsync(
        Guid projectId,
        Guid exportId,
        [FromServices] IDataTransferService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetExportAsync(
                CreateContext(contextService, httpContext, projectId),
                exportId,
                cancellationToken));

    private static async Task<IResult> CreateDownloadUrlAsync(
        Guid projectId,
        Guid exportId,
        [FromServices] IDataTransferService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.CreateDownloadUrlAsync(
                CreateContext(contextService, httpContext, projectId),
                exportId,
                cancellationToken));

    private static ProjectContext CreateContext(
        IProjectContextService contextService,
        HttpContext httpContext,
        Guid projectId)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            projectId,
            httpContext.GetCorrelationId());
}
