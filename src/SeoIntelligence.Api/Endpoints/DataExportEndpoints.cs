using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
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

        exports.MapPost("", CreateExportAsync);
        exports.MapPost("/csv", CreateCsvExportAsync);
        exports.MapGet("/{exportId:guid}", GetExportAsync);
        exports.MapGet("/{exportId:guid}/download", CreateDownloadUrlAsync);
        exports.MapGet("/{exportId:guid}/content", GetExportContentAsync);

        var imports = app.MapGroup("/api/projects/{projectId:guid}/imports");

        imports.MapPost("/upload-url", CreateImportUploadUrlAsync);
        imports.MapPost("", RegisterImportAsync);
        imports.MapGet("/{importId:guid}", GetImportAsync);
        imports.MapGet("/{importId:guid}/errors", GetImportErrorsAsync);

        return app;
    }

    private static async Task<IResult> CreateExportAsync(
        Guid projectId,
        [FromBody] DataExportRequest request,
        [FromServices] IDataTransferService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateExportAsync(
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

    /// <summary>
    /// Streams the generated file itself. A success answer is the bytes rather than the common
    /// response envelope, which cannot carry a binary body; failures still use the envelope.
    /// </summary>
    private static async Task<IResult> GetExportContentAsync(
        Guid projectId,
        Guid exportId,
        [FromServices] IDataTransferService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.OpenExportContentAsync(
            CreateContext(contextService, httpContext, projectId),
            exportId,
            cancellationToken);

        return result.IsSuccess
            ? ApiResponseResults.File(result.Value!)
            : ApiResponseResults.FromError(httpContext, result.Error!);
    }

    private static async Task<IResult> CreateImportUploadUrlAsync(
        Guid projectId,
        [FromBody] ImportUploadUrlRequest request,
        [FromServices] IDataImportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.CreateUploadUrlAsync(
                CreateContext(contextService, httpContext, projectId),
                request,
                cancellationToken));

    private static async Task<IResult> RegisterImportAsync(
        Guid projectId,
        [FromBody] ImportRequest request,
        [FromServices] IDataImportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterImportAsync(
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

    private static async Task<IResult> GetImportAsync(
        Guid projectId,
        Guid importId,
        [FromServices] IDataImportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => ApiResponseResults.FromResult(
            httpContext,
            await service.GetImportAsync(
                CreateContext(contextService, httpContext, projectId),
                importId,
                cancellationToken));

    private static async Task<IResult> GetImportErrorsAsync(
        Guid projectId,
        Guid importId,
        [FromServices] IDataImportService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string? q = null)
    {
        var parameters = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = "all",
            SortBy = "target",
            OrderBy = "asc",
            Q = q
        };
        var validationErrors = parameters.Validate(["all"], ["target"]);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.GetImportErrorsAsync(
                CreateContext(contextService, httpContext, projectId),
                importId,
                new SearchQuery(
                    q,
                    "all",
                    new SortRequest("target", SortDirection.Asc),
                    new PageRequest(page, pageSize)),
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
