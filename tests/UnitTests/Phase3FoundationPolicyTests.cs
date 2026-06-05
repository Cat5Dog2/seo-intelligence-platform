using SeoIntelligence.Application.Ai;
using SeoIntelligence.Application.Sharing;
using SeoIntelligence.Application.Common;

namespace UnitTests;

public sealed class Phase3FoundationPolicyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Security")]
    public void PromptRedactorRemovesSecretsWebhooksAndPersonalData()
    {
        const string prompt = """
        apiKey=rk_live_123456789
        Discord: https://discord.com/api/webhooks/123456789/abcdef
        Authorization: Bearer eySecretToken
        Contact owner@example.com or +1-415-555-0123 before publishing.
        """;
        var redactor = new SensitivePromptRedactor();

        var result = redactor.Redact(prompt);

        Assert.Equal(SensitivePromptRedactor.RedactedStatus, result.RedactionStatus);
        Assert.Contains("secret_assignment", result.MatchedCategories);
        Assert.Contains("discord_webhook", result.MatchedCategories);
        Assert.Contains("authorization_header", result.MatchedCategories);
        Assert.Contains("email", result.MatchedCategories);
        Assert.Contains("phone_number", result.MatchedCategories);
        Assert.DoesNotContain("rk_live_123456789", result.RedactedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("discord.com/api/webhooks", result.RedactedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eySecretToken", result.RedactedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("owner@example.com", result.RedactedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+1-415-555-0123", result.RedactedPrompt, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Security")]
    public void PromptRedactorLeavesCleanPromptUnchanged()
    {
        const string prompt = "Summarize ranking movements for project articles.";
        var redactor = new SensitivePromptRedactor();

        var result = redactor.Redact(prompt);

        Assert.Equal(SensitivePromptRedactor.CleanStatus, result.RedactionStatus);
        Assert.Empty(result.MatchedCategories);
        Assert.Equal(prompt, result.RedactedPrompt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Security")]
    public void ShareTokenServiceIssuesFutureTokenAndStoresOnlyHash()
    {
        var service = new ShareTokenService();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");

        var result = service.Issue(now.AddDays(7), now);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(result.Value!.Token, result.Value.TokenHash);
        Assert.Equal(64, result.Value.TokenHash.Length);
        Assert.DoesNotContain(result.Value.Token, result.Value.TokenHash, StringComparison.Ordinal);

        var validation = service.Validate(
            result.Value.Token,
            result.Value.TokenHash,
            result.Value.ExpiresAt,
            revokedAt: null,
            now);
        Assert.Equal(ShareTokenValidationStatus.Valid, validation.Status);
        Assert.Equal(200, validation.HttpStatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Security")]
    public void ShareTokenServiceRejectsExpiredRevokedAndTamperedTokens()
    {
        var service = new ShareTokenService();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");
        var issued = service.Issue(now.AddHours(1), now).Value!;

        var tampered = service.Validate("tampered", issued.TokenHash, issued.ExpiresAt, null, now);
        var expired = service.Validate(issued.Token, issued.TokenHash, now.AddSeconds(-1), null, now);
        var revoked = service.Validate(issued.Token, issued.TokenHash, issued.ExpiresAt, now.AddMinutes(-1), now);

        Assert.Equal(ShareTokenValidationStatus.Unknown, tampered.Status);
        Assert.Equal(404, tampered.HttpStatusCode);
        Assert.Equal(ShareTokenValidationStatus.Expired, expired.Status);
        Assert.Equal(410, expired.HttpStatusCode);
        Assert.Equal(ShareTokenValidationStatus.Revoked, revoked.Status);
        Assert.Equal(410, revoked.HttpStatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ShareTokenServiceRejectsPastExpiryWhenIssuing()
    {
        var service = new ShareTokenService();
        var now = DateTimeOffset.Parse("2026-06-05T00:00:00Z");

        var result = service.Issue(now, now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Contains("shareExpiresAt", result.Error.Details!.Keys);
    }
}
