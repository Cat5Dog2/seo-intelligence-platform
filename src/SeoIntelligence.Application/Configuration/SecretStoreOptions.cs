namespace SeoIntelligence.Application.Configuration;

public sealed class SecretStoreOptions
{
    public const string SectionName = "SecretStore";
    public const string ConfigurationProvider = "Configuration";

    public string Provider { get; set; } = ConfigurationProvider;

    public string ConfigurationPrefix { get; set; } = "Secrets";

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.Equals(Provider, ConfigurationProvider, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("SecretStore:Provider must be Configuration.");
        }

        if (string.IsNullOrWhiteSpace(ConfigurationPrefix))
        {
            errors.Add("SecretStore:ConfigurationPrefix is required.");
        }

        return errors;
    }
}
