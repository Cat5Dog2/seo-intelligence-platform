using System.Net;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Web.Services;

public sealed record ApiClientResult<T>(
    bool IsSuccess,
    T? Data,
    IReadOnlyList<ApiError> Errors,
    ApiResponseMeta Meta,
    HttpStatusCode? StatusCode = null,
    string? RequestId = null)
{
    public static ApiClientResult<T> Success(
        T? data,
        ApiResponseMeta meta,
        HttpStatusCode statusCode,
        string? requestId)
        => new(true, data, [], meta, statusCode, requestId);

    public static ApiClientResult<T> Failure(
        IReadOnlyList<ApiError> errors,
        ApiResponseMeta? meta = null,
        HttpStatusCode? statusCode = null,
        string? requestId = null)
        => new(false, default, errors, meta ?? ApiResponseMeta.Empty, statusCode, requestId);

    public string ErrorSummary
        => Errors.Count == 0
            ? "API request failed."
            : string.Join(" ", Errors.Select(error => error.Message));
}
