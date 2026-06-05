using SeoIntelligence.Application.Common;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Api.Common;

internal static class ApiResponseResults
{
    public static IResult Ok<T>(HttpContext context, T data, ApiResponseMeta? meta = null)
        => Results.Json(ApiResponseEnvelope<T>.Success(context.GetCorrelationId(), data, meta));

    public static IResult Created<T>(HttpContext context, T data, ApiResponseMeta? meta = null)
        => Results.Json(
            ApiResponseEnvelope<T>.Success(context.GetCorrelationId(), data, meta),
            statusCode: StatusCodes.Status201Created);

    public static IResult Accepted<T>(HttpContext context, T data, ApiResponseMeta? meta = null)
        => Results.Json(
            ApiResponseEnvelope<T>.Success(context.GetCorrelationId(), data, meta),
            statusCode: StatusCodes.Status202Accepted);

    public static IResult Paged<T>(HttpContext context, PagedResult<T> page)
        => Ok(
            context,
            page.Items,
            new ApiResponseMeta(Page: new PageMeta(
                page.Page,
                page.PageSize,
                page.TotalCount,
                page.TotalPages)));

    public static IResult FromResult<T>(HttpContext context, Result<T> result)
        => result.IsSuccess
            ? Ok(context, result.Value!)
            : FromError(context, result.Error!);

    public static IResult FromCreatedResult<T>(HttpContext context, Result<T> result)
        => result.IsSuccess
            ? Created(context, result.Value!)
            : FromError(context, result.Error!);

    public static IResult FromAcceptedResult<T>(HttpContext context, Result<T> result, ApiResponseMeta? meta = null)
        => result.IsSuccess
            ? Accepted(context, result.Value!, meta)
            : FromError(context, result.Error!);

    public static IResult FromPagedResult<T>(HttpContext context, Result<PagedResult<T>> result)
        => result.IsSuccess
            ? Paged(context, result.Value!)
            : FromError(context, result.Error!);

    public static IResult ValidationFailure(
        HttpContext context,
        IReadOnlyDictionary<string, string[]> validationErrors)
        => Failure(
            context,
            StatusCodes.Status400BadRequest,
            validationErrors.SelectMany(pair => pair.Value.Select(message =>
                new ApiError("Validation.Failed", message, pair.Key))).ToArray());

    public static IResult FromError(HttpContext context, Error error)
    {
        var statusCode = error.Code switch
        {
            ErrorCode.ValidationFailed => StatusCodes.Status400BadRequest,
            ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCode.NotFound => StatusCodes.Status404NotFound,
            ErrorCode.Conflict => StatusCodes.Status409Conflict,
            ErrorCode.Gone => StatusCodes.Status410Gone,
            ErrorCode.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorCode.ExternalTemporaryFailure => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        var errors = ToApiErrors(error);
        return Failure(context, statusCode, errors);
    }

    public static IResult Failure(HttpContext context, int statusCode, IReadOnlyList<ApiError> errors)
        => Results.Json(
            ApiResponseEnvelope<object>.Failure(context.GetCorrelationId(), errors),
            statusCode: statusCode);

    private static IReadOnlyList<ApiError> ToApiErrors(Error error)
    {
        if (error.Details is null || error.Details.Count == 0)
        {
            return [new ApiError(ToErrorCode(error.Code), error.Message)];
        }

        return error.Details
            .SelectMany(pair => pair.Value.Select(message => new ApiError(ToErrorCode(error.Code), message, pair.Key)))
            .ToArray();
    }

    private static string ToErrorCode(ErrorCode code)
        => code switch
        {
            ErrorCode.ValidationFailed => "Validation.Failed",
            ErrorCode.NotFound => "Resource.NotFound",
            ErrorCode.Conflict => "Resource.Conflict",
            ErrorCode.Gone => "Resource.Gone",
            ErrorCode.Forbidden => "Scope.Forbidden",
            ErrorCode.RateLimited => "RateLimit.Exceeded",
            ErrorCode.CreditInsufficient => "External.CreditInsufficient",
            ErrorCode.ExternalTemporaryFailure => "External.TemporaryFailure",
            ErrorCode.ExternalFatalFailure => "External.FatalFailure",
            ErrorCode.SecretUnavailable => "Secret.Unavailable",
            ErrorCode.OperationCanceled => "Operation.Canceled",
            _ => "Common.Unexpected"
        };
}
