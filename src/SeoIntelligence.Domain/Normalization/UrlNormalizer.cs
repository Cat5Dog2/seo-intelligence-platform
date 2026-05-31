using System.Globalization;
using System.Text;

namespace SeoIntelligence.Domain.Normalization;

public static class UrlNormalizer
{
    private static readonly IdnMapping IdnMapping = new();

    public static string NormalizeDomain(string value)
    {
        var normalized = NormalizeText(value);
        var uri = CreateUri(normalized);
        var host = uri?.Host ?? normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return NormalizeHost(host);
    }

    public static string NormalizeUrl(string value)
    {
        var uri = CreateUri(NormalizeText(value))
            ?? throw new ArgumentException("URL must include a valid host.", nameof(value));

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = NormalizeHost(uri.Host),
            Fragment = string.Empty
        };

        if (IsDefaultPort(builder.Scheme, builder.Port))
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim().Normalize(NormalizationForm.FormKC);
    }

    private static Uri? CreateUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Host))
        {
            return absolute;
        }

        return Uri.TryCreate($"https://{value}", UriKind.Absolute, out var withDefaultScheme)
            && !string.IsNullOrWhiteSpace(withDefaultScheme.Host)
                ? withDefaultScheme
                : null;
    }

    private static string NormalizeHost(string host)
        => IdnMapping.GetAscii(host.Trim().TrimEnd('.')).ToLowerInvariant();

    private static bool IsDefaultPort(string scheme, int port)
        => (scheme == Uri.UriSchemeHttp && port == 80)
            || (scheme == Uri.UriSchemeHttps && port == 443);
}
