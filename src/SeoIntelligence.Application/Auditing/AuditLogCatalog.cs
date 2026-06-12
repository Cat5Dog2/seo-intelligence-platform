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
    public const string DataExportCreated = "data_export.created";
    public const string DataExportDownloadUrlIssued = "data_export.download_url_issued";
    public const string DataExportDownloaded = "data_export.downloaded";
    public const string DataImportRegistered = "data_import.registered";
    public const string DataImportCompleted = "data_import.completed";
    public const string DataImportFailed = "data_import.failed";
    public const string ExternalConnectorCreated = "external_connector.created";
    public const string ExternalConnectorUpdated = "external_connector.updated";
    public const string ExternalConnectorDisabled = "external_connector.disabled";
    public const string ExternalConnectorTested = "external_connector.tested";
    public const string ReportGenerationQueued = "report.generation_queued";
    public const string ReportCreated = "report.created";
    public const string ReportDownloadUrlIssued = "report.download_url_issued";
    public const string ReportDownloaded = "report.downloaded";
    public const string ReportShareIssued = "report.share_issued";
    public const string ReportShareRevoked = "report.share_revoked";
    public const string ReportShareAccessed = "report.share_accessed";
    public const string ReportShareAccessRejected = "report.share_access_rejected";
    public const string AiChatQueued = "ai.chat_queued";
    public const string AiChatCompleted = "ai.chat_completed";
    public const string AiChatFailed = "ai.chat_failed";
    public const string JobQueued = "job.queued";
    public const string JobStarted = "job.started";
    public const string JobSucceeded = "job.succeeded";
    public const string JobFailed = "job.failed";
    public const string JobCanceled = "job.canceled";
    public const string JobRetried = "job.retried";
}

public static class AuditLogResourceTypes
{
    public const string ApiCredential = "api_credential";
    public const string ExternalApiCall = "external_api_call";
    public const string CsvExport = "csv_export";
    public const string DataExport = "data_export";
    public const string DataImport = "data_import";
    public const string ExternalConnector = "external_connector";
    public const string ExternalConnectorRun = "external_connector_run";
    public const string Report = "report";
    public const string ReportShare = "report_share";
    public const string AiMessage = "ai_message";
    public const string AiSession = "ai_session";
    public const string Job = "job";
}
