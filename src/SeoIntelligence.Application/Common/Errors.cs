namespace SeoIntelligence.Application.Common;

public enum ErrorCode
{
    ValidationFailed,
    NotFound,
    Conflict,
    Forbidden,
    RateLimited,
    CreditInsufficient,
    ExternalTemporaryFailure,
    ExternalFatalFailure,
    SecretUnavailable,
    OperationCanceled,
    Unexpected
}

public sealed record Error(
    ErrorCode Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error Validation(string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(ErrorCode.ValidationFailed, message, details);
}
