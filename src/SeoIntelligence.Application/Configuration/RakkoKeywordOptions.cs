namespace SeoIntelligence.Application.Configuration;

public sealed class RakkoKeywordOptions
{
    public const string SectionName = "RakkoKeyword";
    public const string MockMode = "Mock";
    public const string RealMode = "Real";
    public const string ProviderName = "rakko_keyword";

    public string Mode { get; set; } = MockMode;

    public string BaseUrl { get; set; } = "https://api.rakkokeyword.com";

    public string ApiKeySecretRef { get; set; } = "rakko-keyword-api-key-dev";

    public int TimeoutSeconds { get; set; } = 30;

    public int LongTimeoutSeconds { get; set; } = 60;

    public string UserAgentProduct { get; set; } = "SeoIntelligence";

    public string UserAgentVersion { get; set; } = "0.1.0";

    public string EnvironmentName { get; set; } = "Development";

    public int RawDataRetentionMonths { get; set; } = 24;

    public int? MockStatusCode { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.Equals(Mode, MockMode, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Mode, RealMode, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("RakkoKeyword:Mode must be Mock or Real.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("RakkoKeyword:BaseUrl must be an absolute HTTP or HTTPS URI.");
        }

        if (string.Equals(Mode, RealMode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(ApiKeySecretRef))
        {
            errors.Add("RakkoKeyword:ApiKeySecretRef is required when RakkoKeyword:Mode is Real.");
        }

        if (TimeoutSeconds <= 0)
        {
            errors.Add("RakkoKeyword:TimeoutSeconds must be greater than 0.");
        }

        if (LongTimeoutSeconds < TimeoutSeconds)
        {
            errors.Add("RakkoKeyword:LongTimeoutSeconds must be greater than or equal to RakkoKeyword:TimeoutSeconds.");
        }

        if (string.IsNullOrWhiteSpace(UserAgentProduct))
        {
            errors.Add("RakkoKeyword:UserAgentProduct is required.");
        }

        if (string.IsNullOrWhiteSpace(UserAgentVersion))
        {
            errors.Add("RakkoKeyword:UserAgentVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            errors.Add("RakkoKeyword:EnvironmentName is required.");
        }

        if (RawDataRetentionMonths <= 0)
        {
            errors.Add("RakkoKeyword:RawDataRetentionMonths must be greater than 0.");
        }

        if (MockStatusCode is < 100 or > 599)
        {
            errors.Add("RakkoKeyword:MockStatusCode must be between 100 and 599.");
        }

        return errors;
    }
}
