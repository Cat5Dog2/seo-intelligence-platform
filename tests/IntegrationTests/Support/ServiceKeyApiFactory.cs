using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SeoIntelligence.Api.Security;
using SeoIntelligence.Application.Configuration;

namespace IntegrationTests.Support;

/// <summary>
/// Base for API test factories. Every API endpoint requires a service key, so tests need one
/// configured on the host and sent on each client. Configuration is appended in
/// <see cref="CreateHost"/> so derived factories do not have to call back into this class, and the
/// header is added in <see cref="ConfigureClient"/> so it applies to every <c>CreateClient</c> call.
/// <para>
/// The type argument only identifies the API assembly. It is not <c>Program</c> because this test
/// project also references the Web assembly, which declares a <c>Program</c> of its own.
/// </para>
/// </summary>
public abstract class ServiceKeyApiFactory : WebApplicationFactory<ServiceKeyAuthenticationHandler>
{
    public const string TestServiceKey = "integration-test-service-key";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAuthentication:ServiceKeyRef"] = ServiceAuthenticationOptions.DefaultServiceKeyRef,
                [$"Secrets:{ServiceAuthenticationOptions.DefaultServiceKeyRef}"] = TestServiceKey
            });
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        if (!client.DefaultRequestHeaders.Contains(ServiceAuthenticationOptions.HeaderName))
        {
            client.DefaultRequestHeaders.Add(ServiceAuthenticationOptions.HeaderName, TestServiceKey);
        }
    }
}
