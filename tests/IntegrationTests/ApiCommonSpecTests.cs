using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IntegrationTests;

public sealed class ApiCommonSpecTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiStatusReturnsCommonEnvelopeAndPreservesCorrelationId()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ApiFactory(storagePath);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.Add("X-Correlation-Id", "test-correlation-1");

        try
        {
            using var response = await client.SendAsync(request);
            using var document = await ReadJsonAsync(response);
            var root = document.RootElement;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
            Assert.Equal("test-correlation-1", values.Single());
            Assert.Equal("test-correlation-1", root.GetProperty("requestId").GetString());
            Assert.True(root.GetProperty("result").GetBoolean());
            Assert.Equal("SeoIntelligence.Api", root.GetProperty("data").GetProperty("service").GetString());
            Assert.Empty(root.GetProperty("errors").EnumerateArray());
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiStatusRejectsInvalidListQueryWithCommonErrorEnvelope()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ApiFactory(storagePath);
        using var client = CreateClient(factory);

        try
        {
            using var response = await client.GetAsync("/api?page=0&pageSize=201&orderBy=sideways");
            using var document = await ReadJsonAsync(response);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(document.RootElement.GetProperty("result").GetBoolean());
            Assert.Null(document.RootElement.GetProperty("data").GetString());
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "page");
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "pageSize");
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "orderBy");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthzAndReadyzReturnEnvelope()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ApiFactory(storagePath);
        using var client = CreateClient(factory);

        try
        {
            using var healthz = await client.GetAsync("/healthz");
            using var readyz = await client.GetAsync("/readyz");
            using var healthDocument = await ReadJsonAsync(healthz);
            using var readinessDocument = await ReadJsonAsync(readyz);

            Assert.Equal(HttpStatusCode.OK, healthz.StatusCode);
            Assert.True(healthDocument.RootElement.GetProperty("result").GetBoolean());
            Assert.Equal("Healthy", healthDocument.RootElement.GetProperty("data").GetProperty("status").GetString());

            Assert.Equal(HttpStatusCode.OK, readyz.StatusCode);
            Assert.True(readinessDocument.RootElement.GetProperty("result").GetBoolean());
            Assert.Equal("Healthy", readinessDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            var infrastructureData = readinessDocument.RootElement
                .GetProperty("data")
                .GetProperty("checks")
                .GetProperty("infrastructure")
                .GetProperty("data");
            Assert.True(infrastructureData.TryGetProperty("db", out _));
            Assert.True(infrastructureData.TryGetProperty("redis", out _));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OpenApiV1JsonReturnsDocument()
    {
        var storagePath = CreateTempStoragePath();
        await using var factory = new ApiFactory(storagePath);
        using var client = CreateClient(factory);

        try
        {
            using var response = await client.GetAsync("/openapi/v1.json");
            using var document = await ReadJsonAsync(response);
            var root = document.RootElement;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("3.0.3", root.GetProperty("openapi").GetString());
            Assert.Equal("SeoIntelligence API", root.GetProperty("info").GetProperty("title").GetString());
            Assert.True(root.GetProperty("paths").TryGetProperty("/api", out _));
            Assert.True(root.GetProperty("paths").TryGetProperty("/healthz", out _));
            Assert.True(root.GetProperty("paths").TryGetProperty("/readyz", out _));
        }
        finally
        {
            DeleteTempStoragePath(storagePath);
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static HttpClient CreateClient(ApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-api-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class ApiFactory(string storagePath) : WebApplicationFactory<Program>
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
