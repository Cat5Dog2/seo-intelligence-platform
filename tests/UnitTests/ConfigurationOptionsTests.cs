using System.Diagnostics.Metrics;
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
    public void HangfireOptionsDefaultQueuesMatchJobDesign()
    {
        var options = new HangfireOptions();

        Assert.Equal(
            ["default", "external-api", "polling", "analysis", "exports", "notifications"],
            options.Queues);
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
    public void RakkoKeywordOptionsDefaultToMockMode()
    {
        var options = new RakkoKeywordOptions();

        var errors = options.Validate();

        Assert.Empty(errors);
        Assert.Equal(RakkoKeywordOptions.MockMode, options.Mode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RakkoKeywordOptionsRequireSecretReferenceForRealMode()
    {
        var options = new RakkoKeywordOptions
        {
            Mode = RakkoKeywordOptions.RealMode,
            ApiKeySecretRef = ""
        };

        var errors = options.Validate();

        Assert.Contains(
            "RakkoKeyword:ApiKeySecretRef is required when RakkoKeyword:Mode is Real.",
            errors);
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

    [Fact]
    [Trait("Category", "Unit")]
    public void DiagnosticsPublishesOperationalMetricNames()
    {
        var publishedNames = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == SeoIntelligenceDiagnostics.MeterName)
            {
                publishedNames.Add(instrument.Name);
                currentListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();
        var now = DateTime.UtcNow;
        SeoIntelligenceDiagnostics.RecordJobDuration("DataExportJob", "succeeded", now.AddSeconds(-2), now);
        SeoIntelligenceDiagnostics.RecordJobRetry("DataExportJob", "manual");
        SeoIntelligenceDiagnostics.RecordExternalApiCall("rakko_keyword", "/v1/search-volume", 429, 1, cacheHit: false);
        SeoIntelligenceDiagnostics.RecordExternalApiCall("rakko_keyword", "/v1/search-volume", 402, 0, cacheHit: false);
        SeoIntelligenceDiagnostics.RecordNotificationFailure("job_failed", "failed");
        _ = SeoIntelligenceDiagnostics.CreateJobSuccessRateGauge(() => [new Measurement<double>(100)]);
        _ = SeoIntelligenceDiagnostics.CreateJobQueueDepthGauge(() => [new Measurement<long>(0)]);
        listener.RecordObservableInstruments();

        Assert.Contains("job_success_rate", publishedNames);
        Assert.Contains("job_queue_depth", publishedNames);
        Assert.Contains("job_duration_p95", publishedNames);
        Assert.Contains("external_api_429_count", publishedNames);
        Assert.Contains("external_api_402_count", publishedNames);
        Assert.Contains("external_api_credit_consumed", publishedNames);
        Assert.Contains("notification_failure_count", publishedNames);
        Assert.Contains("retry_count_by_job_type", publishedNames);
    }
}
