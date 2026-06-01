namespace SeoIntelligence.Application.Auditing;

public static class AuditLogActionNames
{
    public const string ApiCredentialCreated = "api_credential.created";
    public const string ApiCredentialUpdated = "api_credential.updated";
    public const string ApiCredentialDisabled = "api_credential.disabled";
    public const string ApiCredentialEnabled = "api_credential.enabled";
    public const string ApiCredentialRotated = "api_credential.rotated";

    public const string ExternalApiExecuted = "external_api.executed";
    public const string CsvExportCreated = "csv_export.created";
    public const string CsvDownloadUrlIssued = "csv_export.download_url_issued";
    public const string CsvDownloaded = "csv_export.downloaded";
    public const string JobQueued = "job.queued";
    public const string JobStarted = "job.started";
    public const string JobFailed = "job.failed";
    public const string JobCanceled = "job.canceled";
    public const string JobRetried = "job.retried";
}

public static class AuditLogResourceTypes
{
    public const string ApiCredential = "api_credential";
    public const string ExternalApiCall = "external_api_call";
    public const string CsvExport = "csv_export";
    public const string Job = "job";
}
