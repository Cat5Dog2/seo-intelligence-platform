namespace SeoIntelligence.Application.Configuration;

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";
    public const string PostgreSqlStorage = "PostgreSQL";

    public string Storage { get; set; } = PostgreSqlStorage;

    public string[] Queues { get; set; } =
    [
        "default",
        "external-api",
        "polling",
        "analysis",
        "exports",
        "notifications"
    ];

    public int? WorkerCount { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.Equals(Storage, PostgreSqlStorage, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Hangfire:Storage must be PostgreSQL.");
        }

        if (Queues.Length == 0 || Queues.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Hangfire:Queues must contain at least one queue name.");
        }

        if (WorkerCount is <= 0)
        {
            errors.Add("Hangfire:WorkerCount must be greater than zero when configured.");
        }

        return errors;
    }
}
