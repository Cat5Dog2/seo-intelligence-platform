namespace SeoIntelligence.Application.Configuration;

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";
    public const string PostgreSqlStorage = "PostgreSQL";

    public string Storage { get; set; } = PostgreSqlStorage;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.Equals(Storage, PostgreSqlStorage, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Hangfire:Storage must be PostgreSQL.");
        }

        return errors;
    }
}
