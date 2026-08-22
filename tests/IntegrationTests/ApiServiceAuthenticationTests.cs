using System.Net;
using System.Text.Json;
using IntegrationTests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Configuration;

namespace IntegrationTests;

public sealed class ApiServiceAuthenticationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiRejectsRequestWithoutServiceKeyUsingCommonErrorEnvelope()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = CreateClientWithoutServiceKey(factory);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
            request.Headers.Add("X-Correlation-Id", "auth-correlation-1");

            using var response = await client.SendAsync(request);
            using var document = await ReadJsonAsync(response);
            var root = document.RootElement;

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(root.GetProperty("result").GetBoolean());
            Assert.Equal("auth-correlation-1", root.GetProperty("requestId").GetString());

            var error = Assert.Single(root.GetProperty("errors").EnumerateArray().ToArray());
            Assert.Equal("Auth.Unauthorized", error.GetProperty("code").GetString());
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiRejectsRequestWithIncorrectServiceKey()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = CreateClientWithoutServiceKey(factory);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
            request.Headers.Add(ServiceAuthenticationOptions.HeaderName, "not-the-configured-key");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiAcceptsRequestWithConfiguredServiceKey()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = factory.CreateClient();

        try
        {
            using var response = await client.GetAsync("/api");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    [Trait("Category", "Integration")]
    public async Task HealthEndpointsStayReachableWithoutServiceKey(string path)
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = CreateClientWithoutServiceKey(factory);

        try
        {
            using var response = await client.GetAsync(path);

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReportShareEndpointStaysReachableWithoutServiceKey()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = CreateClientWithoutServiceKey(factory);

        try
        {
            // The share token is the access control here, so an unknown token must be reported as
            // a missing resource rather than as a missing service key.
            using var response = await client.GetAsync("/api/report-shares/unknown-share-token");

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UnauthorizedResponseDoesNotRevealTheConfiguredServiceKey()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);
        using var client = CreateClientWithoutServiceKey(factory);

        try
        {
            using var response = await client.GetAsync("/api/projects");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(ServiceKeyApiFactory.TestServiceKey, content, StringComparison.Ordinal);
            Assert.DoesNotContain(
                ServiceKeyApiFactory.TestServiceKey,
                string.Join(' ', response.Headers.SelectMany(header => header.Value)),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ExactlyFourEndpointsOptOutOfServiceKeyAuthentication()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ServiceAuthenticationApiFactory(storagePath);

        // Forces the host to build so the endpoint graph is available.
        using var client = factory.CreateClient();

        var endpointSources = factory.Services.GetServices<EndpointDataSource>();
        var anonymousRoutes = endpointSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => $"{DescribeMethods(endpoint)} {endpoint.RoutePattern.RawText}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Both share endpoints are anonymous because the recipient of a share link holds no
        // service key: one returns the report metadata, the other the file itself. The share
        // token is the access control for both, and both revalidate it.
        Assert.Equal(
            [
                "GET /api/report-shares/{token}",
                "GET /api/report-shares/{token}/content",
                "GET /healthz",
                "GET /readyz"
            ],
            anonymousRoutes);

        try
        {
            DeleteTempStoragePath(storagePath);
        }
        catch (IOException)
        {
            // The storage directory is best-effort cleanup for this metadata-only assertion.
        }
    }

    private static string DescribeMethods(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return methods is null || methods.Count == 0
            ? "ANY"
            : string.Join(",", methods.Order(StringComparer.Ordinal));
    }

    private static HttpClient CreateClientWithoutServiceKey(ServiceAuthenticationApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove(ServiceAuthenticationOptions.HeaderName);
        return client;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(
            string.IsNullOrWhiteSpace(content),
            $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-auth-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class ServiceAuthenticationApiFactory(string storagePath) : ServiceKeyApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "",
                    ["Redis:ConnectionString"] = "",
                    ["Storage:Provider"] = "Local",
                    ["Storage:BasePath"] = storagePath,
                    ["Storage:BucketName"] = "seo-intelligence",
                    ["SecretStore:Provider"] = "Configuration",
                    ["SecretStore:ConfigurationPrefix"] = "Secrets",
                    ["Hangfire:Storage"] = "PostgreSQL",
                    ["OpenTelemetry:ServiceName"] = "IntegrationTests"
                });
            });
        }
    }
}
