using System.Text.RegularExpressions;

namespace SeoIntelligence.Application.Ai;

public interface IPromptRedactor
{
    PromptRedactionResult Redact(string? prompt);
}

public sealed record PromptRedactionResult(
    string RedactedPrompt,
    string RedactionStatus,
    IReadOnlyList<string> MatchedCategories);

public sealed partial class SensitivePromptRedactor : IPromptRedactor
{
    public const string CleanStatus = "clean";
    public const string RedactedStatus = "redacted";

    private static readonly RedactionRule[] Rules =
    [
        new("discord_webhook", DiscordWebhookRegex()),
        new("authorization_header", AuthorizationHeaderRegex()),
        new("secret_assignment", SecretAssignmentRegex()),
        new("oauth_token", OAuthTokenRegex()),
        new("email", EmailRegex()),
        new("phone_number", PhoneNumberRegex())
    ];

    public PromptRedactionResult Redact(string? prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return new PromptRedactionResult(string.Empty, CleanStatus, []);
        }

        var redacted = prompt;
        var categories = new List<string>();
        foreach (var rule in Rules)
        {
            if (!rule.Pattern.IsMatch(redacted))
            {
                continue;
            }

            categories.Add(rule.Category);
            redacted = rule.Pattern.Replace(redacted, $"[REDACTED:{rule.Category}]");
        }

        return new PromptRedactionResult(
            redacted,
            categories.Count == 0 ? CleanStatus : RedactedStatus,
            categories.Distinct(StringComparer.Ordinal).ToArray());
    }

    [GeneratedRegex(@"https://(?:discord(?:app)?\.com)/api/webhooks/[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiscordWebhookRegex();

    [GeneratedRegex(@"\bAuthorization\s*:\s*(?:Bearer|Basic)\s+[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"\b(?:api[_-]?key|secret|client[_-]?secret|password|token|webhook[_-]?url)\s*[:=]\s*[""']?[^,\s;""'}]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"\b(?:oauth|refresh|access)[_-]?token\s*[:=]\s*[""']?[^,\s;""'}]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OAuthTokenRegex();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:\+?\d[\d\-() ]{8,}\d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneNumberRegex();

    private sealed record RedactionRule(string Category, Regex Pattern);
}
