using SeoIntelligence.Application.Configuration;

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
}
