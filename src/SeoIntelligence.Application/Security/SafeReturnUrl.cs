namespace SeoIntelligence.Application.Security;

public static class SafeReturnUrl
{
    public const string Fallback = "/";

    /// <summary>
    /// Reduces a caller-supplied return URL to a same-site absolute path. Anything that could send
    /// the browser to another origin after sign-in falls back to the application root, so a
    /// crafted link cannot turn the login page into an open redirect.
    /// </summary>
    public static string Resolve(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Fallback;
        }

        var candidate = returnUrl.Trim();

        // A leading "//" or an embedded scheme separator both let a browser treat the value as an
        // absolute URL on another host, and backslashes are normalised to slashes by some browsers.
        if (!candidate.StartsWith('/')
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.StartsWith("/\\", StringComparison.Ordinal)
            || candidate.Contains("://", StringComparison.Ordinal)
            || candidate.Contains(':', StringComparison.Ordinal))
        {
            return Fallback;
        }

        return candidate;
    }
}
