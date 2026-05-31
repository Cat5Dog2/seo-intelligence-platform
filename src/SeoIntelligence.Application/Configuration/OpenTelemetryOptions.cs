namespace SeoIntelligence.Application.Configuration;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "SeoIntelligence";

    public string? OtlpEndpoint { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            errors.Add("OpenTelemetry:ServiceName is required.");
        }

        if (!string.IsNullOrWhiteSpace(OtlpEndpoint)
            && !IsHttpUri(OtlpEndpoint))
        {
            errors.Add("OpenTelemetry:OtlpEndpoint must be an absolute URI.");
        }

        return errors;
    }

    private static bool IsHttpUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
