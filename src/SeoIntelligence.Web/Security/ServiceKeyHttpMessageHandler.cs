using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Secrets;

namespace SeoIntelligence.Web.Security;

/// <summary>
/// Attaches the API service key to every outbound API call. The Web host is the only HTTP client
/// of the API, so this is what lets the API reject anything that did not come from here.
/// </summary>
public sealed class ServiceKeyHttpMessageHandler(
    ISecretStore secretStore,
    IOptions<ServiceAuthenticationOptions> options,
    ILogger<ServiceKeyHttpMessageHandler> logger)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var secretRef = new SecretReference(options.Value.ServiceKeyRef);
        var serviceKey = await secretStore.GetAsync(secretRef, cancellationToken);

        if (serviceKey is null)
        {
            logger.LogError(
                "The API service key secret {secret_ref} is not available, so API calls will be rejected.",
                secretRef.Name);
        }
        else
        {
            request.Headers.Remove(ServiceAuthenticationOptions.HeaderName);
            request.Headers.Add(ServiceAuthenticationOptions.HeaderName, serviceKey.Value);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
