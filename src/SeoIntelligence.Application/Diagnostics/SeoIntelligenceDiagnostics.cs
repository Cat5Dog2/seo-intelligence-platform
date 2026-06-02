using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SeoIntelligence.Application.Diagnostics;

public static class SeoIntelligenceDiagnostics
{
    public const string ServiceName = "SeoIntelligence";
    public const string ActivitySourceName = "SeoIntelligence";
    public const string MeterName = "SeoIntelligence";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> JobDurationP95 = Meter.CreateHistogram<double>(
        "job_duration_p95",
        unit: "ms",
        description: "Job execution duration. Exporters should calculate p95 from this distribution.");

    private static readonly Counter<long> ExternalApi429Count = Meter.CreateCounter<long>(
        "external_api_429_count",
        unit: "{call}",
        description: "External API calls that returned HTTP 429.");

    private static readonly Counter<long> ExternalApi402Count = Meter.CreateCounter<long>(
        "external_api_402_count",
        unit: "{call}",
        description: "External API calls that returned HTTP 402.");

    private static readonly Counter<double> ExternalApiCreditConsumed = Meter.CreateCounter<double>(
        "external_api_credit_consumed",
        unit: "{credit}",
        description: "External API credit consumed.");

    private static readonly Counter<long> NotificationFailureCount = Meter.CreateCounter<long>(
        "notification_failure_count",
        unit: "{delivery}",
        description: "Notification deliveries that failed or entered retry.");

    private static readonly Counter<long> RetryCountByJobType = Meter.CreateCounter<long>(
        "retry_count_by_job_type",
        unit: "{retry}",
        description: "Job retries grouped by job type.");

    public static ObservableGauge<double> CreateJobSuccessRateGauge(Func<IEnumerable<Measurement<double>>> observe)
        => Meter.CreateObservableGauge(
            "job_success_rate",
            observe,
            unit: "%",
            description: "Succeeded jobs divided by succeeded, failed, and canceled jobs in the last hour.");

    public static ObservableGauge<long> CreateJobQueueDepthGauge(Func<IEnumerable<Measurement<long>>> observe)
        => Meter.CreateObservableGauge(
            "job_queue_depth",
            observe,
            unit: "{job}",
            description: "Queued and waiting_external jobs.");

    public static void RecordJobDuration(string jobType, string status, DateTime createdAt, DateTime completedAt)
    {
        if (completedAt < createdAt)
        {
            return;
        }

        var tags = new TagList
        {
            { "job_type", NormalizeTagValue(jobType) },
            { "status", NormalizeTagValue(status) }
        };

        JobDurationP95.Record((completedAt - createdAt).TotalMilliseconds, tags);
    }

    public static void RecordJobRetry(string jobType, string source)
    {
        var tags = new TagList
        {
            { "job_type", NormalizeTagValue(jobType) },
            { "source", NormalizeTagValue(source) }
        };

        RetryCountByJobType.Add(1, tags);
    }

    public static void RecordExternalApiCall(
        string provider,
        string endpoint,
        int statusCode,
        decimal consumedCredit,
        bool cacheHit)
    {
        var tags = new TagList
        {
            { "provider", NormalizeTagValue(provider) },
            { "endpoint", NormalizeTagValue(endpoint) },
            { "status_code", statusCode },
            { "cache_hit", cacheHit }
        };

        if (statusCode == 429)
        {
            ExternalApi429Count.Add(1, tags);
        }

        if (statusCode == 402)
        {
            ExternalApi402Count.Add(1, tags);
        }

        if (consumedCredit > 0)
        {
            ExternalApiCreditConsumed.Add(decimal.ToDouble(consumedCredit), tags);
        }
    }

    public static void RecordNotificationFailure(string eventType, string status)
    {
        var tags = new TagList
        {
            { "event_type", NormalizeTagValue(eventType) },
            { "status", NormalizeTagValue(status) }
        };

        NotificationFailureCount.Add(1, tags);
    }

    private static string NormalizeTagValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
}
