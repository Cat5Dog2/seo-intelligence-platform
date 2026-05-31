namespace SeoIntelligence.Domain.Common;

public static class UuidV7
{
    public static Guid New()
        => Guid.CreateVersion7();

    public static Guid New(DateTimeOffset timestampUtc)
        => Guid.CreateVersion7(UtcDateTime.EnsureUtc(timestampUtc));
}
