using System.Security.Cryptography;
using System.Text;
using SeoIntelligence.Application.Common;

namespace SeoIntelligence.Application.Sharing;

public interface IShareTokenService
{
    Result<ShareTokenIssueResult> Issue(DateTimeOffset expiresAt, DateTimeOffset now);

    ShareTokenValidationResult Validate(
        string? token,
        string? storedHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset now);

    string HashToken(string token);
}

public sealed record ShareTokenIssueResult(
    string Token,
    string TokenHash,
    DateTimeOffset ExpiresAt);

public sealed record ShareTokenValidationResult(
    ShareTokenValidationStatus Status,
    int HttpStatusCode);

public enum ShareTokenValidationStatus
{
    Valid,
    Unknown,
    Expired,
    Revoked
}

public sealed class ShareTokenService : IShareTokenService
{
    private const int TokenBytes = 32;

    public Result<ShareTokenIssueResult> Issue(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
        {
            return Result<ShareTokenIssueResult>.Failure(Error.Validation(
                "shareExpiresAt must be a future datetime.",
                new Dictionary<string, string[]>
                {
                    ["shareExpiresAt"] = ["shareExpiresAt must be greater than the current time."]
                }));
        }

        var token = GenerateToken();
        return Result<ShareTokenIssueResult>.Success(new ShareTokenIssueResult(
            token,
            HashToken(token),
            expiresAt));
    }

    public ShareTokenValidationResult Validate(
        string? token,
        string? storedHash,
        DateTimeOffset? expiresAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(storedHash) ||
            !HashesMatch(HashToken(token.Trim()), storedHash.Trim()))
        {
            return new ShareTokenValidationResult(ShareTokenValidationStatus.Unknown, 404);
        }

        if (revokedAt.HasValue)
        {
            return new ShareTokenValidationResult(ShareTokenValidationStatus.Revoked, 410);
        }

        if (!expiresAt.HasValue || expiresAt.Value <= now)
        {
            return new ShareTokenValidationResult(ShareTokenValidationStatus.Expired, 410);
        }

        return new ShareTokenValidationResult(ShareTokenValidationStatus.Valid, 200);
    }

    public string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool HashesMatch(string currentHash, string storedHash)
    {
        var current = Encoding.UTF8.GetBytes(currentHash);
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return current.Length == stored.Length &&
            CryptographicOperations.FixedTimeEquals(current, stored);
    }
}
