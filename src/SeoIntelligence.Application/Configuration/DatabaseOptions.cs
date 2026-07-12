namespace SeoIntelligence.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string DefaultConnectionName = "Default";
    public const string SectionName = "Database";

    public string? ConnectionString { get; set; }

    // Discrete parts; when Host is set they take precedence over
    // ConnectionStrings:Default. The final connection string is composed in
    // code so operators never hand-escape passwords inside a literal.
    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? Name { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? GssEncryptionMode { get; set; }

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
