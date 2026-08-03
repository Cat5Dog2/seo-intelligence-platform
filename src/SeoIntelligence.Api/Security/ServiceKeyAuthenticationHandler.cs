using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Api.Security;

/// <summary>
/// Authenticates the Web host against the API using the <c>X-Service-Key</c> header. The expected
/// value is read from the Secret Store and compared in constant time. Failures are rendered with
/// the common response envelope so callers see the same error shape as every other API failure.
/// </summary>
public sealed class ServiceKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ISecretStore secretStore,
    IOptions<ServiceAuthenticationOptions> serviceAuthenticationOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ServiceAuthenticationOptions.HeaderName, out var providedValues))
        {
            return AuthenticateResult.NoResult();
        }

        var provided = providedValues.ToString();
        if (string.IsNullOrEmpty(provided))
        {
            return AuthenticateResult.Fail("The service key is empty.");
        }

        var secretRef = new SecretReference(serviceAuthenticationOptions.Value.ServiceKeyRef);
        var expected = await secretStore.GetAsync(secretRef, Context.RequestAborted);
        if (expected is null)
        {
            Logger.LogError(
                "The API service key secret {secret_ref} is not available, so no caller can authenticate.",
                secretRef.Name);
            return AuthenticateResult.Fail("The service key is not configured.");
        }

        if (!IsMatch(provided, expected.Value))
        {
            return AuthenticateResult.Fail("The service key is invalid.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, ServiceKeyAuthenticationDefaults.SchemeName)],
            ServiceKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, ServiceKeyAuthenticationDefaults.SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        => WriteFailureAsync(
            StatusCodes.Status401Unauthorized,
            new ApiError("Auth.Unauthorized", "A valid service key is required."));

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => WriteFailureAsync(
            StatusCodes.Status403Forbidden,
            new ApiError("Auth.Forbidden", "The service key is not allowed to access this resource."));

    private static bool IsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private async Task WriteFailureAsync(int statusCode, ApiError error)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";

        var envelope = ApiResponseEnvelope<object>.Failure(Context.GetCorrelationId(), [error]);
        await Response.WriteAsJsonAsync(envelope, Context.RequestAborted);
    }
}
