namespace SeoIntelligence.Application.Common;

public sealed class Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success()
        => new(true, null);

    public static Result Failure(Error error)
        => new(false, error);
}

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public Error? Error { get; }

    public static Result<T> Success(T value)
        => new(true, value, null);

    public static Result<T> Failure(Error error)
        => new(false, default, error);
}
