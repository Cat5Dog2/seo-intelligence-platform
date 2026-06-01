namespace SeoIntelligence.Domain.Common;

public static class StatusValues
{
    public const string Active = "active";
    public const string Archived = "archived";
    public const string Disabled = "disabled";
    public const string Pending = "pending";
    public const string Retrying = "retrying";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Queued = "queued";
    public const string Running = "running";
    public const string WaitingExternal = "waiting_external";
    public const string FailedRetryable = "failed_retryable";
    public const string FailedFatal = "failed_fatal";
    public const string Canceled = "canceled";
}

public enum LifecycleStatus
{
    Active,
    Archived,
    Disabled
}

public enum NotificationDeliveryStatus
{
    Pending,
    Retrying,
    Succeeded,
    Failed
}

public enum JobStatus
{
    Queued,
    Running,
    WaitingExternal,
    Succeeded,
    FailedRetryable,
    FailedFatal,
    Canceled
}

public static class LifecycleStatusTransitions
{
    public static bool CanTransition(LifecycleStatus current, LifecycleStatus next)
        => current == next
            || (current == LifecycleStatus.Active && next is LifecycleStatus.Archived or LifecycleStatus.Disabled)
            || (current is LifecycleStatus.Archived or LifecycleStatus.Disabled && next == LifecycleStatus.Active);
}

public static class NotificationDeliveryStatusTransitions
{
    public static bool CanTransition(NotificationDeliveryStatus current, NotificationDeliveryStatus next)
        => current == next
            || (current == NotificationDeliveryStatus.Pending
                && next is NotificationDeliveryStatus.Retrying or NotificationDeliveryStatus.Succeeded or NotificationDeliveryStatus.Failed)
            || (current == NotificationDeliveryStatus.Retrying
                && next is NotificationDeliveryStatus.Succeeded or NotificationDeliveryStatus.Failed);
}

public static class JobStatusTransitions
{
    public static bool CanTransition(JobStatus current, JobStatus next)
        => current == next
            || current switch
            {
                JobStatus.Queued => next is JobStatus.Running or JobStatus.Canceled or JobStatus.FailedFatal,
                JobStatus.Running => next is JobStatus.WaitingExternal
                    or JobStatus.Succeeded
                    or JobStatus.FailedRetryable
                    or JobStatus.FailedFatal,
                JobStatus.WaitingExternal => next is JobStatus.Running
                    or JobStatus.Succeeded
                    or JobStatus.FailedRetryable
                    or JobStatus.FailedFatal
                    or JobStatus.Canceled,
                JobStatus.FailedRetryable => next is JobStatus.Queued or JobStatus.Running or JobStatus.FailedFatal or JobStatus.Canceled,
                JobStatus.Succeeded or JobStatus.FailedFatal or JobStatus.Canceled => false,
                _ => false
            };

    public static bool CanCancel(JobStatus current)
        => current is JobStatus.Queued or JobStatus.WaitingExternal;

    public static bool IsTerminal(JobStatus status)
        => status is JobStatus.Succeeded or JobStatus.FailedFatal or JobStatus.Canceled;
}

public static class JobFailureClassifier
{
    public static JobStatus FromHttpStatusCode(int statusCode)
        => statusCode switch
        {
            400 or 402 or 403 => JobStatus.FailedFatal,
            429 or 500 or 503 => JobStatus.FailedRetryable,
            >= 500 and <= 599 => JobStatus.FailedRetryable,
            _ => JobStatus.FailedFatal
        };
}

public static class StatusExtensions
{
    public static JobStatus ToJobStatus(string status)
    {
        if (TryToJobStatus(status, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentOutOfRangeException(nameof(status), status, null);
    }

    public static bool TryToJobStatus(string? status, out JobStatus parsed)
    {
        parsed = default;
        return status?.Trim().ToLowerInvariant() switch
        {
            StatusValues.Queued => Set(JobStatus.Queued, out parsed),
            StatusValues.Running => Set(JobStatus.Running, out parsed),
            StatusValues.WaitingExternal => Set(JobStatus.WaitingExternal, out parsed),
            StatusValues.Succeeded => Set(JobStatus.Succeeded, out parsed),
            StatusValues.FailedRetryable => Set(JobStatus.FailedRetryable, out parsed),
            StatusValues.FailedFatal => Set(JobStatus.FailedFatal, out parsed),
            StatusValues.Canceled => Set(JobStatus.Canceled, out parsed),
            _ => false
        };
    }

    public static string ToStorageValue(this LifecycleStatus status)
        => status switch
        {
            LifecycleStatus.Active => StatusValues.Active,
            LifecycleStatus.Archived => StatusValues.Archived,
            LifecycleStatus.Disabled => StatusValues.Disabled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public static string ToStorageValue(this NotificationDeliveryStatus status)
        => status switch
        {
            NotificationDeliveryStatus.Pending => StatusValues.Pending,
            NotificationDeliveryStatus.Retrying => StatusValues.Retrying,
            NotificationDeliveryStatus.Succeeded => StatusValues.Succeeded,
            NotificationDeliveryStatus.Failed => StatusValues.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public static string ToStorageValue(this JobStatus status)
        => status switch
        {
            JobStatus.Queued => StatusValues.Queued,
            JobStatus.Running => StatusValues.Running,
            JobStatus.WaitingExternal => StatusValues.WaitingExternal,
            JobStatus.Succeeded => StatusValues.Succeeded,
            JobStatus.FailedRetryable => StatusValues.FailedRetryable,
            JobStatus.FailedFatal => StatusValues.FailedFatal,
            JobStatus.Canceled => StatusValues.Canceled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static bool Set(JobStatus value, out JobStatus parsed)
    {
        parsed = value;
        return true;
    }
}
