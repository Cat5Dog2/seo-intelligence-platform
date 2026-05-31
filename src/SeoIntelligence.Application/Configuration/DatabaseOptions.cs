namespace SeoIntelligence.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string DefaultConnectionName = "Default";

    public string? ConnectionString { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            errors.Add("ConnectionStrings:Default is required.");
        }

        return errors;
    }
}
