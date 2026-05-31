namespace SeoIntelligence.Domain.Common;

public static class UtcDateTime
{
    public static DateTimeOffset Now()
        => TimeProvider.System.GetUtcNow();

    public static DateTimeOffset EnsureUtc(DateTimeOffset value)
        => value.ToUniversalTime();

    public static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
        => value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("DateTimeOffset must use UTC offset.", parameterName);
}
