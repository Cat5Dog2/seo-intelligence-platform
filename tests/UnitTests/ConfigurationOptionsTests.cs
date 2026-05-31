using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Application.Storage;

namespace UnitTests;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void StorageOptionsAcceptsLocalProviderWhenBasePathIsPresent()
    {
        var options = new StorageOptions
        {
            Provider = StorageOptions.LocalProvider,
            BasePath = "./.data/storage"
        };

        var errors = options.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StorageOptionsRequiresAbsoluteEndpointForMinioProvider()
    {
        var options = new StorageOptions
        {
            Provider = StorageOptions.MinioProvider,
            Endpoint = "localhost:9000",
            BucketName = "seo-intelligence"
        };

        var errors = options.Validate();

        Assert.Contains(
            "Storage:Endpoint must be an absolute URI when Storage:Provider is MinIO.",
            errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HangfireOptionsRejectsNonPostgreSqlStorage()
    {
        var options = new HangfireOptions
        {
            Storage = "Redis"
        };

        var errors = options.Validate();

        Assert.Equal(["Hangfire:Storage must be PostgreSQL."], errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HangfireOptionsRequiresAtLeastOneQueue()
    {
        var options = new HangfireOptions
        {
            Queues = []
        };

        var errors = options.Validate();

        Assert.Contains("Hangfire:Queues must contain at least one queue name.", errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SecretStoreOptionsRejectsUnsupportedProvider()
    {
        var options = new SecretStoreOptions
        {
            Provider = "PlainTextFile"
        };

        var errors = options.Validate();

        Assert.Equal(["SecretStore:Provider must be Configuration."], errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OpenTelemetryOptionsRequiresAbsoluteOtlpEndpointWhenConfigured()
    {
        var options = new OpenTelemetryOptions
        {
            ServiceName = "SeoIntelligence.Api",
            OtlpEndpoint = "collector:4317"
        };

        var errors = options.Validate();

        Assert.Contains("OpenTelemetry:OtlpEndpoint must be an absolute URI.", errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StorageObjectKeyRejectsPathTraversalSegments()
    {
        var exception = Assert.Throws<ArgumentException>(() => new StorageObjectKey("../raw/response.json"));

        Assert.Contains("relative path segments", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LogContextContainsRequiredStructuredLoggingKeys()
    {
        var workspaceId = Guid.Parse("018f26ab-3f8d-7b4a-8bfb-768ab975f111");
        var projectId = Guid.Parse("018f26ab-3f8d-7b4a-8bfb-768ab975f222");
        var jobId = Guid.Parse("018f26ab-3f8d-7b4a-8bfb-768ab975f333");
        var context = new SeoIntelligenceLogContext(
            workspaceId,
            projectId,
            jobId,
            "rk-request-1",
            "correlation-1");

        var scope = context.ToScopeDictionary();

        Assert.Equal(workspaceId, scope["workspace_id"]);
        Assert.Equal(projectId, scope["project_id"]);
        Assert.Equal(jobId, scope["job_id"]);
        Assert.Equal("rk-request-1", scope["external_request_id"]);
        Assert.Equal("correlation-1", scope["correlation_id"]);
    }
}
