namespace SeoIntelligence.Contracts.Api;

public sealed record ApiResponseEnvelope<T>(
    string RequestId,
    bool Result,
    T? Data,
    IReadOnlyList<ApiError> Errors,
    ApiResponseMeta Meta)
{
    public static ApiResponseEnvelope<T> Success(string requestId, T data, ApiResponseMeta? meta = null)
        => new(requestId, true, data, [], meta ?? ApiResponseMeta.Empty);

    public static ApiResponseEnvelope<T> Failure(
        string requestId,
        IReadOnlyList<ApiError> errors,
        ApiResponseMeta? meta = null)
        => new(requestId, false, default, errors, meta ?? ApiResponseMeta.Empty);
}

public sealed record ApiError(
    string Code,
    string Message,
    string? Target = null);

public sealed record ApiResponseMeta(
    Guid? JobId = null,
    string? ExternalRequestId = null,
    decimal ConsumedCredit = 0,
    PageMeta? Page = null)
{
    public static ApiResponseMeta Empty { get; } = new();
}

public sealed record PageMeta(
    int Page,
    int PageSize,
    long TotalCount,
    long TotalPages);
