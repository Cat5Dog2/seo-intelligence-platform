namespace SeoIntelligence.Application.Configuration;

/// <summary>
/// Configures the shared-secret authentication between the Web host and the API. The Web host is
/// the only HTTP client of the API, so a single service key is enough; the actual value lives in
/// the Secret Store and only its reference name is configured here.
/// </summary>
public sealed class ServiceAuthenticationOptions
{
    public const string SectionName = "ServiceAuthentication";

    public const string DefaultServiceKeyRef = "ApiServiceKey";

    public const string HeaderName = "X-Service-Key";

    public string ServiceKeyRef { get; set; } = DefaultServiceKeyRef;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ServiceKeyRef))
        {
            errors.Add($"{SectionName}:{nameof(ServiceKeyRef)} is required.");
        }

        return errors;
    }
}
