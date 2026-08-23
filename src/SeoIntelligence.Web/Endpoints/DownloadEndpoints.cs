using SeoIntelligence.Application.Security;
using SeoIntelligence.Web.Services;

namespace SeoIntelligence.Web.Endpoints;

/// <summary>
/// Browser-facing download routes.
/// <para>
/// The API requires the service key on every business endpoint, so a link pointing straight at it
/// would answer 401 in a browser. These routes are the bridge: they authenticate the operator by
/// the Identity cookie, then fetch the file from the API with the service key the Web host holds
/// and stream it through. The API stays the only component that reads storage, and the browser
/// never needs a key.
/// </para>
/// </summary>
public static class DownloadEndpoints
{
    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var downloads = endpoints
            .MapGroup("/downloads/projects/{projectId:guid}")
            .RequireAuthorization(ApplicationPolicies.RequireAdmin);

        downloads.MapGet("/exports/{exportId:guid}", GetExportAsync);
        downloads.MapGet("/reports/{reportId:guid}", GetReportAsync);

        return endpoints;
    }

    private static Task<IResult> GetExportAsync(
        Guid projectId,
        Guid exportId,
        ISeoIntelligenceApiClient apiClient,
        CancellationToken cancellationToken)
        => StreamAsync(
            () => apiClient.DownloadExportAsync(projectId, exportId, cancellationToken),
            cancellationToken);

    private static Task<IResult> GetReportAsync(
        Guid projectId,
        Guid reportId,
        ISeoIntelligenceApiClient apiClient,
        CancellationToken cancellationToken)
        => StreamAsync(
            () => apiClient.DownloadReportAsync(projectId, reportId, cancellationToken),
            cancellationToken);

    private static async Task<IResult> StreamAsync(
        Func<Task<ApiClientResult<ApiFileResponse>>> fetch,
        CancellationToken cancellationToken)
    {
        var result = await fetch();
        if (!result.IsSuccess || result.Data is not { } file)
        {
            // The API's status code is passed through so a missing export stays a 404 and an
            // unfinished one stays a 409, rather than every failure looking the same.
            return Results.Problem(
                title: "ファイルを取得できませんでした。",
                detail: result.Errors.Count > 0
                    ? string.Join(" ", result.Errors.Select(error => error.Message))
                    : "APIからファイルを取得できませんでした。",
                statusCode: (int?)result.StatusCode ?? StatusCodes.Status502BadGateway);
        }

        // The response is disposed once the body has been written; Results.Stream disposes the
        // stream it is given, and that releases the underlying HTTP response with it.
        return Results.Stream(
            async output =>
            {
                using (file)
                {
                    await file.Content.CopyToAsync(output, cancellationToken);
                }
            },
            contentType: file.ContentType,
            fileDownloadName: file.FileName);
    }
}
