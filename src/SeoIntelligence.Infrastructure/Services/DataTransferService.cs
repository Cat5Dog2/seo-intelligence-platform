using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class DataTransferService(
    SeoIntelligenceDbContext dbContext,
    IObjectStorage objectStorage,
    IAuditLogWriter auditLogWriter,
    IJobQueueClient jobQueueClient,
    TimeProvider timeProvider)
    : IDataTransferService
    , IDataImportService
{
    public const string JobType = "DataExportJob";
    public const string ImportJobType = "DataImportJob";
    public const string CsvExportResourceType = "csv_export";
    public const string DataExportResourceType = "data_export";
    public const string DataImportResourceType = "data_import";
    private const string CsvFormat = "csv";
    private const string ExcelFormat = "excel";
    private const string XlsxFormat = "xlsx";
    private const string StrictValidationMode = "strict";
    private const int MaxImportRows = 50_000;
    private const long MaxImportFileBytes = 20L * 1024 * 1024;
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] KeywordMetricsColumns =
    [
        "keyword",
        "searchVolume",
        "seoDifficulty",
        "cpc",
        "competition",
        "opportunityScore",
        "location",
        "language",
        "fetchedAt"
    ];
    private static readonly string[] SearchVolumeResultColumns =
    [
        "keyword",
        "searchVolume",
        "seoDifficulty",
        "cpc",
        "competition",
        "dataSource",
        "cacheHit",
        "jobId"
    ];
    private static readonly string[] KeywordCandidateColumns =
    [
        "keyword",
        "source",
        "suggestClass",
        "seed",
        "engine",
        "firstSeenRange",
        "createdAt"
    ];
    private static readonly string[] ExternalApiCallColumns =
    [
        "provider",
        "endpoint",
        "statusCode",
        "consumedCredit",
        "cacheHit",
        "errorCode",
        "createdAt"
    ];
    private static readonly Dictionary<string, string[]> SupportedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["keyword_metrics"] = KeywordMetricsColumns,
        ["search_volume_results"] = SearchVolumeResultColumns,
        ["keyword_candidates"] = KeywordCandidateColumns,
        ["external_api_calls"] = ExternalApiCallColumns
    };
    private static readonly HashSet<string> SupportedExportFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        CsvFormat,
        ExcelFormat,
        XlsxFormat
    };
    private static readonly Dictionary<string, string[]> RequiredImportColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["keywords"] = ["keyword"],
        ["rankings"] = ["keyword", "target", "position"],
        ["competitors"] = ["domain"],
        ["briefs"] = ["title"],
        ["tasks"] = ["targetUrl"]
    };
    private static readonly HashSet<string> SupportedImportFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        CsvFormat,
        ExcelFormat,
        XlsxFormat
    };

    public Task<Result<JobReference>> CreateCsvExportAsync(
        ProjectExecutionContext context,
        DataExportRequest request,
        CancellationToken cancellationToken = default)
        => CreateExportCoreAsync(context, request with { Format = CsvFormat }, forceCsv: true, cancellationToken);

    public Task<Result<JobReference>> CreateExportAsync(
        ProjectExecutionContext context,
        DataExportRequest request,
        CancellationToken cancellationToken = default)
        => CreateExportCoreAsync(context, request, forceCsv: false, cancellationToken);

    private async Task<Result<JobReference>> CreateExportCoreAsync(
        ProjectExecutionContext context,
        DataExportRequest request,
        bool forceCsv,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var exportType = NormalizeExportType(request.ExportType, errors);
        var format = forceCsv ? CsvFormat : NormalizeExportFormat(request.Format, errors);
        var filterJson = NormalizeFilterJson(request.Filter, errors);
        var columns = exportType is null
            ? []
            : NormalizeColumns(exportType, request.Columns, errors);

        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (errors.HasErrors)
        {
            return ValidationFailure<JobReference>(errors);
        }

        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId!.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
        if (!projectExists)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        using var filterDocument = JsonDocument.Parse(filterJson);
        var snapshot = new DataExportRequestSnapshot(
            Version: 1,
            ExportType: exportType!,
            Format: format!,
            Filter: filterDocument.RootElement.Clone(),
            Columns: columns);
        var payload = JsonSerializer.SerializeToElement(snapshot, JsonOptions);
        var requestHash = HashText(payload.GetRawText());
        var now = NowUtc();
        var export = new DataExportEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            ExportType = exportType!,
            Format = format!,
            FilterJson = payload.GetRawText(),
            Status = StatusValues.Queued,
            RequestedBy = context.Actor,
            CreatedAt = now
        };
        var job = new JobEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            JobType = JobType,
            Status = StatusValues.Queued,
            Progress = 0,
            RetryCount = 0,
            NextRunAt = now,
            ResultResourceType = forceCsv ? CsvExportResourceType : DataExportResourceType,
            ResultResourceId = export.Id,
            RequestHash = requestHash,
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.DataExports.Add(export);
        dbContext.Jobs.Add(job);
        AddJobQueuedAudit(context, job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobQueueClient.EnqueueAsync(job.Id, "exports", cancellationToken);

        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<DataExportDetails>> GetExportAsync(
        ProjectExecutionContext context,
        Guid exportId,
        CancellationToken cancellationToken = default)
    {
        var export = await FindExportAsync(context, exportId, asTracking: false, cancellationToken);
        return export is null
            ? Failure<DataExportDetails>(ErrorCode.NotFound, "Data export was not found.")
            : Result<DataExportDetails>.Success(MapExport(export));
    }

    public async Task<Result<DataExportDownload>> CreateDownloadUrlAsync(
        ProjectExecutionContext context,
        Guid exportId,
        CancellationToken cancellationToken = default)
    {
        var export = await FindExportAsync(context, exportId, asTracking: false, cancellationToken);
        if (export is null)
        {
            return Failure<DataExportDownload>(ErrorCode.NotFound, "Data export was not found.");
        }

        if (!string.Equals(export.Status, StatusValues.Succeeded, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(export.FileUri))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "Data export is not ready for download.");
        }

        if (!TryGetObjectKey(export.FileUri, out var key))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "Data export file URI is invalid.");
        }

        if (!await objectStorage.ExistsAsync(key, cancellationToken))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "Data export file was not found in storage.");
        }

        var expiresAt = NowUtc().Add(DownloadUrlTtl);
        var downloadUrl = BuildDownloadUrl(export.FileUri, expiresAt);
        AddExportAudit(
            context,
            ExportDownloadUrlIssuedAction(export.Format),
            export,
            new
            {
                export = ToExportAuditSnapshot(export),
                downloadUrl,
                expiresAt
            });
        AddExportAudit(
            context,
            ExportDownloadedAction(export.Format),
            export,
            new
            {
                export = ToExportAuditSnapshot(export),
                downloadUrl,
                expiresAt,
                via = "short_lived_url"
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DataExportDownload>.Success(new DataExportDownload(export.Id, downloadUrl, expiresAt));
    }

    public Task<Result<DataExportDetails>> GenerateCsvExportAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
        => GenerateExportAsync(context, jobId, cancellationToken);

    public async Task<Result<DataExportDetails>> GenerateExportAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == JobType,
                cancellationToken);
        if (job is null)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "Data export job was not found.");
        }

        if (!job.ResultResourceId.HasValue ||
            (!string.Equals(job.ResultResourceType, CsvExportResourceType, StringComparison.Ordinal) &&
                !string.Equals(job.ResultResourceType, DataExportResourceType, StringComparison.Ordinal)))
        {
            return Failure<DataExportDetails>(ErrorCode.Conflict, "Data export job does not reference a data export.");
        }

        var export = await FindExportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (export is null)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "Data export was not found.");
        }

        var snapshot = ReadSnapshot(export);
        if (snapshot is null)
        {
            return Failure<DataExportDetails>(ErrorCode.Conflict, "Data export request payload was invalid.");
        }

        var table = await BuildCsvTableAsync(context, snapshot, cancellationToken);
        var fileBytes = string.Equals(snapshot.Format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? WriteCsv(table)
            : TabularDataFile.WriteXlsx(table.Columns, table.Rows);
        await using var content = new MemoryStream(fileBytes, writable: false);
        var stored = await objectStorage.PutAsync(
            new StoragePutRequest(
                new StorageObjectKey(BuildExportObjectKey(context, export.Id, snapshot.Format)),
                content,
                GetExportContentType(snapshot.Format)),
            cancellationToken);

        var before = ToExportAuditSnapshot(export);
        var now = NowUtc();
        export.FileUri = stored.Uri;
        export.Status = StatusValues.Succeeded;
        export.CompletedAt = now;
        AddExportAudit(
            context,
            ExportCreatedAction(snapshot.Format),
            export,
            new
            {
                before,
                after = ToExportAuditSnapshot(export)
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DataExportDetails>.Success(MapExport(export));
    }

    public async Task RecordExportFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
        string status,
        string message,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == JobType,
                cancellationToken);
        if (job?.ResultResourceId is null)
        {
            return;
        }

        var export = await FindExportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (export is null || string.Equals(export.Status, StatusValues.Succeeded, StringComparison.Ordinal))
        {
            return;
        }

        var before = ToExportAuditSnapshot(export);
        export.Status = status;
        export.CompletedAt = NowUtc();
        AddExportAudit(
            context,
            ExportCreatedAction(export.Format),
            export,
            new
            {
                before,
                after = ToExportAuditSnapshot(export),
                error = message
            });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<ImportUploadUrlDetails>> CreateUploadUrlAsync(
        ProjectExecutionContext context,
        ImportUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var importType = NormalizeImportType(request.ImportType, errors);
        var format = NormalizeImportFormat(request.Format, errors);
        var fileName = NormalizeImportFileName(request.FileName, format, errors);

        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (errors.HasErrors)
        {
            return ValidationFailure<ImportUploadUrlDetails>(errors);
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ImportUploadUrlDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var key = new StorageObjectKey(
            $"imports/{context.WorkspaceId:N}/{project.Id:N}/incoming/{UuidV7.New():N}/{fileName}");
        var sourceFileUri = $"storage://local/{key.Value}";
        var expiresAt = NowOffset().Add(UploadUrlTtl);
        var uploadUrl = BuildDownloadUrl(sourceFileUri, expiresAt.UtcDateTime);

        _ = importType;
        return Result<ImportUploadUrlDetails>.Success(new ImportUploadUrlDetails(uploadUrl, sourceFileUri, expiresAt));
    }

    public async Task<Result<JobReference>> RegisterImportAsync(
        ProjectExecutionContext context,
        ImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var importType = NormalizeImportType(request.ImportType, errors);
        var format = NormalizeImportFormat(request.Format, errors);
        var sourceFileUri = NormalizeSourceFileUri(request.SourceFileUri, errors);
        var validationMode = NormalizeValidationMode(request.ValidationMode, errors);

        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (errors.HasErrors)
        {
            return ValidationFailure<JobReference>(errors);
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<JobReference>(ErrorCode.NotFound, "Project was not found.");
        }

        if (!TryGetObjectKey(sourceFileUri!, out var key) ||
            !await objectStorage.ExistsAsync(key, cancellationToken))
        {
            return Failure<JobReference>(ErrorCode.Conflict, "Import source file was not found in storage.");
        }

        var snapshot = new DataImportRequestSnapshot(
            Version: 1,
            ImportType: importType!,
            Format: format!,
            SourceFileUri: sourceFileUri!,
            ValidationMode: validationMode!);
        var payload = JsonSerializer.SerializeToElement(snapshot, JsonOptions);
        var requestHash = HashText(payload.GetRawText());
        var now = NowUtc();
        var import = new DataImportEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = project.Id,
            ImportType = importType!,
            Format = format!,
            SourceFileUri = sourceFileUri!,
            Status = StatusValues.Queued,
            ValidationErrorsJson = "[]",
            RequestedBy = context.Actor,
            CreatedAt = now
        };
        var job = new JobEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = project.Id,
            JobType = ImportJobType,
            Status = StatusValues.Queued,
            Progress = 0,
            RetryCount = 0,
            NextRunAt = now,
            ResultResourceType = DataImportResourceType,
            ResultResourceId = import.Id,
            RequestHash = requestHash,
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.DataImports.Add(import);
        dbContext.Jobs.Add(job);
        AddJobQueuedAudit(context, job);
        AddDataImportAudit(
            context,
            AuditLogActionNames.DataImportRegistered,
            import,
            new { before = (object?)null, after = ToImportAuditSnapshot(import) });
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobQueueClient.EnqueueAsync(job.Id, "exports", cancellationToken);

        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<DataImportDetails>> GetImportAsync(
        ProjectExecutionContext context,
        Guid importId,
        CancellationToken cancellationToken = default)
    {
        var import = await FindImportAsync(context, importId, asTracking: false, cancellationToken);
        return import is null
            ? Failure<DataImportDetails>(ErrorCode.NotFound, "Data import was not found.")
            : Result<DataImportDetails>.Success(MapImport(import));
    }

    public async Task<Result<PagedResult<DataImportErrorDetails>>> GetImportErrorsAsync(
        ProjectExecutionContext context,
        Guid importId,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var import = await FindImportAsync(context, importId, asTracking: false, cancellationToken);
        if (import is null)
        {
            return Failure<PagedResult<DataImportErrorDetails>>(ErrorCode.NotFound, "Data import was not found.");
        }

        var errors = DeserializeImportErrors(import.ValidationErrorsJson)
            .Select(MapImportError)
            .ToArray();
        var q = OptionalText(query.Q);
        if (q is not null)
        {
            errors = errors
                .Where(error =>
                    error.Target.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    error.Message.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var page = query.EffectivePage;
        var paged = errors
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToArray();
        return Result<PagedResult<DataImportErrorDetails>>.Success(
            new PagedResult<DataImportErrorDetails>(paged, page.Page, page.PageSize, errors.LongLength));
    }

    public async Task<Result<DataImportDetails>> ExecuteImportAsync(
        ProjectExecutionContext context,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == ImportJobType,
                cancellationToken);
        if (job is null)
        {
            return Failure<DataImportDetails>(ErrorCode.NotFound, "Data import job was not found.");
        }

        if (!job.ResultResourceId.HasValue ||
            !string.Equals(job.ResultResourceType, DataImportResourceType, StringComparison.Ordinal))
        {
            return Failure<DataImportDetails>(ErrorCode.Conflict, "Data import job does not reference a data import.");
        }

        var import = await FindImportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (import is null)
        {
            return Failure<DataImportDetails>(ErrorCode.NotFound, "Data import was not found.");
        }

        var snapshot = ReadImportSnapshot(import);
        if (snapshot is null)
        {
            return Failure<DataImportDetails>(ErrorCode.Conflict, "Data import request payload was invalid.");
        }

        if (!TryGetObjectKey(snapshot.SourceFileUri, out var sourceKey))
        {
            return Failure<DataImportDetails>(ErrorCode.Conflict, "Import source file URI is invalid.");
        }

        ImportedTable? table = null;
        try
        {
            await using var source = await objectStorage.OpenReadAsync(sourceKey, cancellationToken);
            if (!source.CanSeek || source.Length <= MaxImportFileBytes)
            {
                table = await ReadImportTableAsync(source, snapshot.Format, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return Failure<DataImportDetails>(ErrorCode.Conflict, "Import source file could not be read.");
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<DataImportDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var validationErrors = table is null
            ? new List<StoredImportValidationError>
            {
                CreateImportError("file", $"Import file size must be {MaxImportFileBytes} bytes or less.")
            }
            : ValidateImportTable(table, snapshot.ImportType);
        if (validationErrors.Count == 0)
        {
            await ApplyImportAsync(context, project, jobId, snapshot.ImportType, table!, validationErrors, cancellationToken);
        }

        var before = ToImportAuditSnapshot(import);
        var now = NowUtc();
        import.ValidationErrorsJson = JsonSerializer.Serialize(validationErrors, JsonOptions);
        import.Status = validationErrors.Count == 0 ? StatusValues.Succeeded : StatusValues.FailedFatal;
        import.CompletedAt = now;
        AddDataImportAudit(
            context,
            validationErrors.Count == 0 ? AuditLogActionNames.DataImportCompleted : AuditLogActionNames.DataImportFailed,
            import,
            new
            {
                before,
                after = ToImportAuditSnapshot(import),
                errorCount = validationErrors.Count
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return validationErrors.Count == 0
            ? Result<DataImportDetails>.Success(MapImport(import))
            : Failure<DataImportDetails>(ErrorCode.ValidationFailed, "Import validation failed.");
    }

    public async Task RecordImportFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
        string status,
        string message,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == jobId &&
                    entity.JobType == ImportJobType,
                cancellationToken);
        if (job?.ResultResourceId is null)
        {
            return;
        }

        var import = await FindImportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (import is null || string.Equals(import.Status, StatusValues.Succeeded, StringComparison.Ordinal))
        {
            return;
        }

        var before = ToImportAuditSnapshot(import);
        import.Status = status;
        import.CompletedAt = NowUtc();
        import.ValidationErrorsJson = JsonSerializer.Serialize(
            new[] { CreateImportError("job", message) },
            JsonOptions);
        AddDataImportAudit(
            context,
            AuditLogActionNames.DataImportFailed,
            import,
            new
            {
                before,
                after = ToImportAuditSnapshot(import),
                error = message
            });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CsvExportTable> BuildCsvTableAsync(
        ProjectExecutionContext context,
        DataExportRequestSnapshot snapshot,
        CancellationToken cancellationToken)
        => snapshot.ExportType switch
        {
            "keyword_metrics" => await BuildKeywordMetricsTableAsync(context, snapshot, cancellationToken),
            "search_volume_results" => await BuildSearchVolumeResultsTableAsync(context, snapshot, cancellationToken),
            "keyword_candidates" => await BuildKeywordCandidatesTableAsync(context, snapshot, cancellationToken),
            "external_api_calls" => await BuildExternalApiCallsTableAsync(context, snapshot, cancellationToken),
            _ => new CsvExportTable(snapshot.Columns, [])
        };

    private async Task<CsvExportTable> BuildKeywordMetricsTableAsync(
        ProjectExecutionContext context,
        DataExportRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var projectId = context.ProjectId!.Value;
        var filters = ExportFilters.From(snapshot.Filter);
        var scores = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                score => score.KeywordId,
                keyword => keyword.Id,
                (score, keyword) => new
                {
                    score.KeywordId,
                    Keyword = keyword.NormalizedText,
                    score.Location,
                    score.Language,
                    score.OpportunityScore
                })
            .ToArrayAsync(cancellationToken);
        var keywordIds = scores.Select(score => score.KeywordId).Distinct().ToArray();
        var metrics = await dbContext.KeywordMetrics
            .AsNoTracking()
            .Where(entity => keywordIds.Contains(entity.KeywordId))
            .ToArrayAsync(cancellationToken);

        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var score in scores)
        {
            var metric = metrics
                .Where(entity =>
                    entity.KeywordId == score.KeywordId &&
                    string.Equals(entity.Location, score.Location, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entity.Language, score.Language, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entity => entity.FetchedAt)
                .FirstOrDefault();
            if (metric is null)
            {
                continue;
            }

            if (!MatchesCommonFilters(score.Keyword, score.Location, score.Language, filters) ||
                (filters.MinSearchVolume.HasValue && metric.SearchVolume < filters.MinSearchVolume.Value) ||
                (filters.MinOpportunityScore.HasValue && score.OpportunityScore < filters.MinOpportunityScore.Value))
            {
                continue;
            }

            rows.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["keyword"] = score.Keyword,
                ["searchVolume"] = metric.SearchVolume.ToString(CultureInfo.InvariantCulture),
                ["seoDifficulty"] = FormatDecimal(metric.SeoDifficulty),
                ["cpc"] = FormatDecimal(metric.Cpc),
                ["competition"] = FormatDecimal(metric.Competition),
                ["opportunityScore"] = FormatDecimal(score.OpportunityScore),
                ["location"] = score.Location,
                ["language"] = score.Language,
                ["fetchedAt"] = FormatDateTime(metric.FetchedAt)
            });
        }

        return new CsvExportTable(snapshot.Columns, rows.OrderBy(row => row["keyword"], StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private async Task<CsvExportTable> BuildSearchVolumeResultsTableAsync(
        ProjectExecutionContext context,
        DataExportRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var projectId = context.ProjectId!.Value;
        var filters = ExportFilters.From(snapshot.Filter);
        var jobIdFilter = filters.JobId;
        var projections = await dbContext.SearchVolumeResults
            .AsNoTracking()
            .Join(
                dbContext.Jobs.AsNoTracking().Where(job =>
                    job.WorkspaceId == context.WorkspaceId &&
                    job.ProjectId == projectId &&
                    job.JobType == SearchVolumeService.RegisterJobType),
                result => result.JobId,
                job => job.Id,
                (result, job) => new { Result = result, Job = job })
            .Join(
                dbContext.Keywords.AsNoTracking(),
                item => item.Result.KeywordId,
                keyword => keyword.Id,
                (item, keyword) => new { item.Result, item.Job, Keyword = keyword.NormalizedText })
            .ToArrayAsync(cancellationToken);
        var rows = new List<IReadOnlyDictionary<string, string?>>();

        foreach (var projection in projections)
        {
            if (jobIdFilter.HasValue && projection.Job.Id != jobIdFilter.Value)
            {
                continue;
            }

            var metrics = DeserializeOrDefault<CsvMetricsSnapshot>(projection.Result.MetricsSnapshotJson)
                ?? new CsvMetricsSnapshot(null, null, null, null, null);
            if (!MatchesTextFilter(projection.Keyword, filters.Q) ||
                (filters.MinSearchVolume.HasValue && (metrics.SearchVolume ?? 0) < filters.MinSearchVolume.Value))
            {
                continue;
            }

            rows.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["keyword"] = projection.Keyword,
                ["searchVolume"] = FormatNullableInt(metrics.SearchVolume),
                ["seoDifficulty"] = FormatNullableDecimal(metrics.SeoDifficulty),
                ["cpc"] = FormatNullableDecimal(metrics.Cpc),
                ["competition"] = FormatNullableDecimal(metrics.Competition),
                ["dataSource"] = projection.Result.DataSource,
                ["cacheHit"] = projection.Result.CacheHit ? "true" : "false",
                ["jobId"] = projection.Job.Id.ToString("D")
            });
        }

        return new CsvExportTable(snapshot.Columns, rows.OrderBy(row => row["keyword"], StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private async Task<CsvExportTable> BuildKeywordCandidatesTableAsync(
        ProjectExecutionContext context,
        DataExportRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var projectId = context.ProjectId!.Value;
        var filters = ExportFilters.From(snapshot.Filter);
        var seedQuery = dbContext.KeywordSeeds.AsNoTracking().Where(seed => seed.ProjectId == projectId);
        var suggestions = await dbContext.KeywordSuggestions
            .AsNoTracking()
            .Join(seedQuery, suggestion => suggestion.SeedId, seed => seed.Id, (suggestion, seed) => new { suggestion, seed })
            .Join(
                dbContext.Keywords.AsNoTracking(),
                item => item.suggestion.KeywordId,
                keyword => keyword.Id,
                (item, keyword) => new
                {
                    Keyword = keyword.NormalizedText,
                    item.suggestion.SuggestClass,
                    Seed = item.seed.Seed,
                    item.suggestion.Engine,
                    item.suggestion.FirstSeenRange,
                    item.suggestion.CreatedAt
                })
            .ToArrayAsync(cancellationToken);
        var related = await dbContext.RelatedKeywords
            .AsNoTracking()
            .Join(seedQuery, related => related.SeedId, seed => seed.Id, (related, seed) => new { related, seed })
            .Join(
                dbContext.Keywords.AsNoTracking(),
                item => item.related.KeywordId,
                keyword => keyword.Id,
                (item, keyword) => new
                {
                    Keyword = keyword.NormalizedText,
                    Seed = item.seed.Seed,
                    item.related.CreatedAt
                })
            .ToArrayAsync(cancellationToken);
        var questions = await dbContext.Questions
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .Select(entity => new
            {
                Keyword = entity.QuestionText,
                entity.Source,
                entity.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var suggestionRows = suggestions.Select(item => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = item.Keyword,
            ["source"] = "suggest",
            ["suggestClass"] = item.SuggestClass,
            ["seed"] = item.Seed,
            ["engine"] = item.Engine,
            ["firstSeenRange"] = item.FirstSeenRange,
            ["createdAt"] = FormatDateTime(item.CreatedAt)
        });
        var relatedRows = related.Select(item => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = item.Keyword,
            ["source"] = "related",
            ["suggestClass"] = null,
            ["seed"] = item.Seed,
            ["engine"] = null,
            ["firstSeenRange"] = null,
            ["createdAt"] = FormatDateTime(item.CreatedAt)
        });
        var questionRows = questions.Select(item => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = item.Keyword,
            ["source"] = item.Source,
            ["suggestClass"] = null,
            ["seed"] = null,
            ["engine"] = null,
            ["firstSeenRange"] = null,
            ["createdAt"] = FormatDateTime(item.CreatedAt)
        });

        var rows = suggestionRows
            .Concat<IReadOnlyDictionary<string, string?>>(relatedRows)
            .Concat(questionRows)
            .Where(row => MatchesTextFilter(row["keyword"], filters.Q))
            .Where(row => filters.Source is null || string.Equals(row["source"], filters.Source, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row["keyword"], StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CsvExportTable(snapshot.Columns, rows);
    }

    private async Task<CsvExportTable> BuildExternalApiCallsTableAsync(
        ProjectExecutionContext context,
        DataExportRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var filters = ExportFilters.From(snapshot.Filter);
        var calls = await dbContext.ExternalApiCalls
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.ProjectId == context.ProjectId)
            .OrderByDescending(entity => entity.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var rows = calls
            .Where(call => filters.Provider is null || string.Equals(call.Provider, filters.Provider, StringComparison.OrdinalIgnoreCase))
            .Where(call => !filters.StatusCode.HasValue || call.StatusCode == filters.StatusCode.Value)
            .Select(call => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = call.Provider,
                ["endpoint"] = call.Endpoint,
                ["statusCode"] = call.StatusCode.ToString(CultureInfo.InvariantCulture),
                ["consumedCredit"] = FormatDecimal(call.ConsumedCredit),
                ["cacheHit"] = call.CacheHit ? "true" : "false",
                ["errorCode"] = call.ErrorCode,
                ["createdAt"] = FormatDateTime(call.CreatedAt)
            })
            .ToArray();

        return new CsvExportTable(snapshot.Columns, rows);
    }

    private async Task<DataExportEntity?> FindExportAsync(
        ProjectExecutionContext context,
        Guid exportId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var source = asTracking ? dbContext.DataExports : dbContext.DataExports.AsNoTracking();
        source = source.Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == exportId);

        if (context.ProjectId.HasValue)
        {
            source = source.Where(entity => entity.ProjectId == context.ProjectId.Value);
        }

        return await source.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<DataImportEntity?> FindImportAsync(
        ProjectExecutionContext context,
        Guid importId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var source = asTracking ? dbContext.DataImports : dbContext.DataImports.AsNoTracking();
        source = source.Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == importId);

        if (context.ProjectId.HasValue)
        {
            source = source.Where(entity => entity.ProjectId == context.ProjectId.Value);
        }

        return await source.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProjectEntity?> FindActiveProjectAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        return await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
    }

    private static string? NormalizeExportType(string? value, ValidationErrors errors)
    {
        var exportType = OptionalText(value)?.ToLowerInvariant();
        if (exportType is null)
        {
            errors.Add(nameof(DataExportRequest.ExportType), "exportType is required.");
            return null;
        }

        if (!SupportedColumns.ContainsKey(exportType))
        {
            errors.Add(nameof(DataExportRequest.ExportType), "exportType must be keyword_metrics, search_volume_results, keyword_candidates, or external_api_calls.");
            return null;
        }

        return exportType;
    }

    private static string? NormalizeExportFormat(string? value, ValidationErrors errors)
    {
        var format = OptionalText(value)?.ToLowerInvariant() ?? CsvFormat;
        if (string.Equals(format, XlsxFormat, StringComparison.OrdinalIgnoreCase))
        {
            return ExcelFormat;
        }

        if (!SupportedExportFormats.Contains(format))
        {
            errors.Add(nameof(DataExportRequest.Format), "format must be csv or excel.");
            return null;
        }

        return format;
    }

    private static string NormalizeFilterJson(JsonElement? filter, ValidationErrors errors)
    {
        if (!filter.HasValue || filter.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        if (filter.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add(nameof(DataExportRequest.Filter), "filter must be a JSON object.");
            return "{}";
        }

        return filter.Value.GetRawText();
    }

    private static IReadOnlyList<string> NormalizeColumns(
        string exportType,
        IReadOnlyList<string>? requestedColumns,
        ValidationErrors errors)
    {
        var allowed = SupportedColumns[exportType];
        var normalized = new List<string>();

        foreach (var requested in requestedColumns ?? [])
        {
            var value = OptionalText(requested);
            if (value is null)
            {
                continue;
            }

            var column = allowed.FirstOrDefault(allowedColumn => string.Equals(allowedColumn, value, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                errors.Add(nameof(DataExportRequest.Columns), $"columns contains unsupported column '{value}'.");
                continue;
            }

            if (!normalized.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(column);
            }
        }

        return normalized.Count == 0 ? allowed : normalized.ToArray();
    }

    private static DataExportRequestSnapshot? ReadSnapshot(DataExportEntity export)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<DataExportRequestSnapshot>(export.FilterJson, JsonOptions);
            if (snapshot is null ||
                !SupportedExportFormats.Contains(snapshot.Format) ||
                !SupportedColumns.ContainsKey(snapshot.ExportType) ||
                snapshot.Columns.Count == 0)
            {
                return null;
            }

            return snapshot with
            {
                ExportType = snapshot.ExportType.ToLowerInvariant(),
                Format = string.Equals(snapshot.Format, XlsxFormat, StringComparison.OrdinalIgnoreCase) ? ExcelFormat : snapshot.Format.ToLowerInvariant(),
                Columns = NormalizeStoredColumns(snapshot.ExportType, snapshot.Columns)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> NormalizeStoredColumns(string exportType, IReadOnlyList<string> columns)
    {
        var allowed = SupportedColumns[exportType];
        return columns
            .Select(column => allowed.FirstOrDefault(allowedColumn => string.Equals(allowedColumn, column, StringComparison.OrdinalIgnoreCase)))
            .Where(column => column is not null)
            .Select(column => column!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeImportType(string? value, ValidationErrors errors)
    {
        var importType = OptionalText(value)?.ToLowerInvariant();
        importType = importType switch
        {
            "keyword" => "keywords",
            "rank" or "ranking" => "rankings",
            "competitor" => "competitors",
            "brief" => "briefs",
            "task" or "rewrite_task" or "rewrite_tasks" => "tasks",
            _ => importType
        };

        if (importType is null)
        {
            errors.Add(nameof(ImportRequest.ImportType), "importType is required.");
            return null;
        }

        if (!RequiredImportColumns.ContainsKey(importType))
        {
            errors.Add(nameof(ImportRequest.ImportType), "importType must be keywords, rankings, competitors, briefs, or tasks.");
            return null;
        }

        return importType;
    }

    private static string? NormalizeImportFormat(string? value, ValidationErrors errors)
    {
        var format = OptionalText(value)?.ToLowerInvariant();
        if (string.Equals(format, XlsxFormat, StringComparison.OrdinalIgnoreCase))
        {
            return ExcelFormat;
        }

        if (format is null)
        {
            errors.Add(nameof(ImportRequest.Format), "format is required.");
            return null;
        }

        if (!SupportedImportFormats.Contains(format))
        {
            errors.Add(nameof(ImportRequest.Format), "format must be csv or excel.");
            return null;
        }

        return format;
    }

    private static string? NormalizeImportFileName(string? value, string? format, ValidationErrors errors)
    {
        var fileName = OptionalText(value);
        if (fileName is null)
        {
            errors.Add(nameof(ImportUploadUrlRequest.FileName), "fileName is required.");
            return null;
        }

        var sanitized = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitized) ||
            sanitized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errors.Add(nameof(ImportUploadUrlRequest.FileName), "fileName is invalid.");
            return null;
        }

        if (format is not null)
        {
            var extension = Path.GetExtension(sanitized);
            var expected = string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase) ? ".csv" : ".xlsx";
            if (!string.Equals(extension, expected, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(nameof(ImportUploadUrlRequest.FileName), $"fileName extension must be {expected}.");
            }
        }

        return sanitized;
    }

    private static string? NormalizeSourceFileUri(string? value, ValidationErrors errors)
    {
        var sourceFileUri = OptionalText(value);
        if (sourceFileUri is null)
        {
            errors.Add(nameof(ImportRequest.SourceFileUri), "sourceFileUri is required.");
            return null;
        }

        if (!TryGetObjectKey(sourceFileUri, out _))
        {
            errors.Add(nameof(ImportRequest.SourceFileUri), "sourceFileUri must be a storage URI.");
            return null;
        }

        return sourceFileUri;
    }

    private static string? NormalizeValidationMode(string? value, ValidationErrors errors)
    {
        var validationMode = OptionalText(value)?.ToLowerInvariant() ?? StrictValidationMode;
        if (validationMode is not StrictValidationMode)
        {
            errors.Add(nameof(ImportRequest.ValidationMode), "validationMode must be strict.");
            return null;
        }

        return validationMode;
    }

    private static DataImportRequestSnapshot? ReadImportSnapshot(DataImportEntity import)
    {
        var payload = new DataImportRequestSnapshot(
            Version: 1,
            ImportType: import.ImportType,
            Format: import.Format,
            SourceFileUri: import.SourceFileUri,
            ValidationMode: StrictValidationMode);

        var normalizedFormat = string.Equals(payload.Format, XlsxFormat, StringComparison.OrdinalIgnoreCase)
            ? ExcelFormat
            : payload.Format.ToLowerInvariant();
        if (!RequiredImportColumns.ContainsKey(payload.ImportType) ||
            !SupportedImportFormats.Contains(normalizedFormat) ||
            !TryGetObjectKey(payload.SourceFileUri, out _))
        {
            return null;
        }

        return payload with
        {
            ImportType = payload.ImportType.ToLowerInvariant(),
            Format = normalizedFormat,
            ValidationMode = OptionalText(payload.ValidationMode)?.ToLowerInvariant() ?? StrictValidationMode
        };
    }

    private static async Task<ImportedTable> ReadImportTableAsync(
        Stream source,
        string format,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync(cancellationToken);
            return TabularDataFile.ReadCsv(content);
        }

        return TabularDataFile.ReadXlsx(source);
    }

    private static List<StoredImportValidationError> ValidateImportTable(ImportedTable table, string importType)
    {
        var errors = new List<StoredImportValidationError>();
        if (table.Columns.Count == 0)
        {
            errors.Add(CreateImportError("header", "Import file must contain a header row."));
            return errors;
        }

        foreach (var requiredColumn in RequiredImportColumns[importType])
        {
            if (!table.Columns.Contains(requiredColumn, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(CreateImportError($"header.{requiredColumn}", $"Required column '{requiredColumn}' is missing."));
            }
        }

        if (table.Rows.Count > MaxImportRows)
        {
            errors.Add(CreateImportError("rows", $"Import file must contain {MaxImportRows} rows or fewer."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        foreach (var row in table.Rows)
        {
            ValidateImportRow(row, importType, errors);
        }

        return errors;
    }

    private static void ValidateImportRow(
        ImportedRow row,
        string importType,
        List<StoredImportValidationError> errors)
    {
        switch (importType)
        {
            case "keywords":
                ValidateRequiredText(row, "keyword", errors);
                break;
            case "rankings":
                ValidateRequiredText(row, "keyword", errors);
                ValidateRequiredText(row, "target", errors);
                ValidateInt(row, "position", 1, 100, required: true, errors);
                ValidateDecimal(row, "estimatedTraffic", 0, required: false, errors);
                ValidateDateTime(row, "checkedAt", required: false, errors);
                break;
            case "competitors":
                if (ValidateRequiredText(row, "domain", errors))
                {
                    ValidateDomain(row, "domain", errors);
                }

                ValidateDecimal(row, "duplicateRate", 0, required: false, errors);
                ValidateDecimal(row, "estimatedTraffic", 0, required: false, errors);
                ValidateDecimal(row, "trafficValue", 0, required: false, errors);
                ValidateInt(row, "keywordCount", 0, int.MaxValue, required: false, errors);
                break;
            case "briefs":
                ValidateRequiredText(row, "title", errors);
                ValidateJson(row, "contentJson", required: false, errors);
                ValidateAllowedText(row, "reviewStatus", ["pending", "reviewed", "rejected"], required: false, errors);
                ValidateAllowedText(row, "status", ["draft", StatusValues.Active, StatusValues.Archived, "completed"], required: false, errors);
                break;
            case "tasks":
                if (ValidateRequiredText(row, "targetUrl", errors))
                {
                    ValidateUrl(row, "targetUrl", errors);
                }

                ValidateDecimal(row, "priorityScore", 0, required: false, errors);
                ValidateJson(row, "reasonJson", required: false, errors);
                ValidateAllowedText(row, "status", ["draft", StatusValues.Active, StatusValues.Archived, "completed"], required: false, errors);
                break;
        }
    }

    private async Task ApplyImportAsync(
        ProjectExecutionContext context,
        ProjectEntity project,
        Guid jobId,
        string importType,
        ImportedTable table,
        List<StoredImportValidationError> errors,
        CancellationToken cancellationToken)
    {
        switch (importType)
        {
            case "keywords":
                await ApplyKeywordImportAsync(project, table, cancellationToken);
                break;
            case "rankings":
                await ApplyRankingImportAsync(project, jobId, table, cancellationToken);
                break;
            case "competitors":
                await ApplyCompetitorImportAsync(project, table, cancellationToken);
                break;
            case "briefs":
                await ApplyBriefImportAsync(context, project, table, cancellationToken);
                break;
            case "tasks":
                ApplyTaskImport(project, table);
                break;
            default:
                errors.Add(CreateImportError("importType", "Unsupported import type."));
                break;
        }
    }

    private async Task ApplyKeywordImportAsync(
        ProjectEntity project,
        ImportedTable table,
        CancellationToken cancellationToken)
    {
        var seenSeeds = await dbContext.KeywordSeeds
            .Where(entity => entity.ProjectId == project.Id)
            .Select(entity => entity.Seed)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        foreach (var row in table.Rows)
        {
            var language = GetCell(row, "language") ?? project.DefaultLanguage;
            var keyword = await GetOrCreateKeywordAsync(GetCell(row, "keyword")!, language, cancellationToken);
            if (seenSeeds.Add(keyword.NormalizedText))
            {
                dbContext.KeywordSeeds.Add(new KeywordSeedEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = project.Id,
                    Seed = keyword.NormalizedText,
                    Source = GetCell(row, "source") ?? "import",
                    Memo = GetCell(row, "memo"),
                    CreatedAt = NowUtc()
                });
            }
        }
    }

    private async Task ApplyRankingImportAsync(
        ProjectEntity project,
        Guid jobId,
        ImportedTable table,
        CancellationToken cancellationToken)
    {
        foreach (var row in table.Rows)
        {
            var language = GetCell(row, "language") ?? project.DefaultLanguage;
            var keyword = await GetOrCreateKeywordAsync(GetCell(row, "keyword")!, language, cancellationToken);
            dbContext.RankResults.Add(new RankResultEntity
            {
                Id = UuidV7.New(),
                JobId = jobId,
                ProjectId = project.Id,
                KeywordId = keyword.Id,
                Target = GetCell(row, "target")!,
                Position = ReadInt(row, "position")!.Value,
                RankedUrl = GetCell(row, "rankedUrl") ?? GetCell(row, "target")!,
                EstimatedTraffic = ReadDecimal(row, "estimatedTraffic") ?? 0,
                MetricsSnapshotJson = BuildImportMetricsSnapshot(row),
                ContractScopeKey = "import",
                CheckedAt = ReadDateTime(row, "checkedAt") ?? NowUtc()
            });
        }
    }

    private async Task ApplyCompetitorImportAsync(
        ProjectEntity project,
        ImportedTable table,
        CancellationToken cancellationToken)
    {
        var existingSites = await dbContext.CompetitorSites
            .Where(entity => entity.ProjectId == project.Id)
            .Select(entity => entity.Domain)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in table.Rows)
        {
            var domain = UrlNormalizer.NormalizeDomain(GetCell(row, "domain")!);
            var duplicateRate = ReadDecimal(row, "duplicateRate") ?? 0;
            var estimatedTraffic = ReadDecimal(row, "estimatedTraffic") ?? 0;
            var source = GetCell(row, "source") ?? "import";
            var now = NowUtc();

            if (existingSites.Add(domain))
            {
                dbContext.CompetitorSites.Add(new CompetitorSiteEntity
                {
                    Id = UuidV7.New(),
                    ProjectId = project.Id,
                    Domain = domain,
                    Source = source,
                    DuplicateRate = duplicateRate,
                    EstimatedTraffic = estimatedTraffic,
                    CreatedAt = now
                });
            }

            dbContext.CompetitiveResults.Add(new CompetitiveResultEntity
            {
                Id = UuidV7.New(),
                ProjectId = project.Id,
                SiteDomain = domain,
                EstimatedTraffic = estimatedTraffic,
                TrafficValue = ReadDecimal(row, "trafficValue") ?? 0,
                KeywordCount = ReadInt(row, "keywordCount") ?? 0,
                DuplicateRate = duplicateRate,
                UniqueCountsJson = "{}",
                CreatedAt = now
            });
        }
    }

    private async Task ApplyBriefImportAsync(
        ProjectExecutionContext context,
        ProjectEntity project,
        ImportedTable table,
        CancellationToken cancellationToken)
    {
        foreach (var row in table.Rows)
        {
            var targetKeyword = GetCell(row, "targetKeyword") is { } keywordText
                ? await GetOrCreateKeywordAsync(keywordText, GetCell(row, "language") ?? project.DefaultLanguage, cancellationToken)
                : null;
            var contentJson = NormalizeContentJson(row);
            var reviewStatus = GetCell(row, "reviewStatus")?.ToLowerInvariant() ?? StatusValues.Pending;
            var now = NowUtc();
            var brief = new ArticleBriefEntity
            {
                Id = UuidV7.New(),
                ProjectId = project.Id,
                Title = GetCell(row, "title")!,
                TargetKeywordId = targetKeyword?.Id,
                CurrentVersion = 1,
                ContentJson = contentJson,
                ReviewStatus = reviewStatus,
                Status = GetCell(row, "status")?.ToLowerInvariant() ?? "draft",
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ArticleBriefs.Add(brief);
            dbContext.ArtifactVersions.Add(new ArtifactVersionEntity
            {
                Id = UuidV7.New(),
                WorkspaceId = context.WorkspaceId,
                ProjectId = project.Id,
                ArtifactType = ContentAnalysisService.ArticleBriefArtifactType,
                ArtifactId = brief.Id,
                VersionNo = 1,
                ContentHash = HashText($"{brief.Title}\n{brief.ReviewStatus}\n{brief.ContentJson}"),
                ContentJson = brief.ContentJson,
                CreatedBy = context.Actor,
                ReviewStatus = brief.ReviewStatus,
                ChangeSummary = "Imported from CSV/Excel.",
                CreatedAt = now
            });
        }
    }

    private void ApplyTaskImport(ProjectEntity project, ImportedTable table)
    {
        foreach (var row in table.Rows)
        {
            var targetUrl = UrlNormalizer.NormalizeUrl(GetCell(row, "targetUrl")!);
            dbContext.RewriteTasks.Add(new RewriteTaskEntity
            {
                Id = UuidV7.New(),
                ProjectId = project.Id,
                TargetUrl = targetUrl,
                PriorityScore = ReadDecimal(row, "priorityScore") ?? 0,
                ReasonJson = NormalizeReasonJson(row),
                Status = GetCell(row, "status")?.ToLowerInvariant() ?? StatusValues.Active,
                AssigneeActor = GetCell(row, "assigneeActor") ?? SystemActor.Developer,
                Memo = GetCell(row, "memo"),
                CreatedAt = NowUtc(),
                UpdatedAt = NowUtc()
            });
        }
    }

    private async Task<KeywordEntity> GetOrCreateKeywordAsync(
        string value,
        string language,
        CancellationToken cancellationToken)
    {
        var normalized = KeywordNormalizer.Normalize(value);
        var hash = HashText(normalized);
        var existing = await dbContext.Keywords
            .FirstOrDefaultAsync(
                entity => entity.Language == language && entity.TextHash == hash,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var added = dbContext.Keywords.Local.FirstOrDefault(entity => entity.Language == language && entity.TextHash == hash);
        if (added is not null)
        {
            return added;
        }

        var keyword = new KeywordEntity
        {
            Id = UuidV7.New(),
            NormalizedText = normalized,
            Language = language,
            TextHash = hash,
            CreatedAt = NowUtc()
        };
        dbContext.Keywords.Add(keyword);
        return keyword;
    }

    private static bool ValidateRequiredText(
        ImportedRow row,
        string column,
        List<StoredImportValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(GetCell(row, column)))
        {
            return true;
        }

        errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
        return false;
    }

    private static void ValidateAllowedText(
        ImportedRow row,
        string column,
        IEnumerable<string> allowedValues,
        bool required,
        List<StoredImportValidationError> errors)
    {
        var value = GetCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
            }

            return;
        }

        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} is invalid."));
        }
    }

    private static void ValidateInt(
        ImportedRow row,
        string column,
        int min,
        int max,
        bool required,
        List<StoredImportValidationError> errors)
    {
        var value = GetCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
            }

            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min ||
            parsed > max)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be an integer between {min} and {max}."));
        }
    }

    private static void ValidateDecimal(
        ImportedRow row,
        string column,
        decimal min,
        bool required,
        List<StoredImportValidationError> errors)
    {
        var value = GetCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
            }

            return;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be a number greater than or equal to {min}."));
        }
    }

    private static void ValidateDateTime(
        ImportedRow row,
        string column,
        bool required,
        List<StoredImportValidationError> errors)
    {
        var value = GetCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
            }

            return;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be an ISO-8601 date-time."));
        }
    }

    private static void ValidateJson(
        ImportedRow row,
        string column,
        bool required,
        List<StoredImportValidationError> errors)
    {
        var value = GetCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} is required."));
            }

            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be valid JSON."));
        }
    }

    private static void ValidateDomain(
        ImportedRow row,
        string column,
        List<StoredImportValidationError> errors)
    {
        try
        {
            _ = UrlNormalizer.NormalizeDomain(GetCell(row, column)!);
        }
        catch (ArgumentException)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be a valid domain or URL."));
        }
        catch (UriFormatException)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be a valid domain or URL."));
        }
    }

    private static void ValidateUrl(
        ImportedRow row,
        string column,
        List<StoredImportValidationError> errors)
    {
        try
        {
            var url = UrlNormalizer.NormalizeUrl(GetCell(row, column)!);
            var uri = new Uri(url, UriKind.Absolute);
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be an http or https URL."));
            }
        }
        catch (ArgumentException)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be an http or https URL."));
        }
        catch (UriFormatException)
        {
            errors.Add(CreateImportError(RowTarget(row, column), $"{column} must be an http or https URL."));
        }
    }

    private static string? GetCell(ImportedRow row, string column)
        => row.Cells.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int? ReadInt(ImportedRow row, string column)
        => int.TryParse(GetCell(row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static decimal? ReadDecimal(ImportedRow row, string column)
        => decimal.TryParse(GetCell(row, column), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static DateTime? ReadDateTime(ImportedRow row, string column)
        => DateTimeOffset.TryParse(GetCell(row, column), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value.UtcDateTime
            : null;

    private static string NormalizeContentJson(ImportedRow row)
    {
        if (GetCell(row, "contentJson") is { } contentJson)
        {
            using var document = JsonDocument.Parse(contentJson);
            return document.RootElement.GetRawText();
        }

        return JsonSerializer.Serialize(
            new
            {
                body = GetCell(row, "content") ?? string.Empty
            },
            JsonOptions);
    }

    private static string NormalizeReasonJson(ImportedRow row)
    {
        if (GetCell(row, "reasonJson") is { } reasonJson)
        {
            using var document = JsonDocument.Parse(reasonJson);
            return document.RootElement.GetRawText();
        }

        return JsonSerializer.Serialize(
            new
            {
                source = "import",
                memo = GetCell(row, "memo")
            },
            JsonOptions);
    }

    private static string BuildImportMetricsSnapshot(ImportedRow row)
        => JsonSerializer.Serialize(
            new
            {
                source = "import",
                searchVolume = ReadInt(row, "searchVolume"),
                seoDifficulty = ReadDecimal(row, "seoDifficulty"),
                cpc = ReadDecimal(row, "cpc"),
                competition = ReadDecimal(row, "competition")
            },
            JsonOptions);

    private static string RowTarget(ImportedRow row, string column)
        => $"rows[{row.RowNumber}].{column}";

    private static StoredImportValidationError CreateImportError(string target, string message)
        => new(target, message, JsonSerializer.SerializeToElement(new { target }, JsonOptions));

    private static IReadOnlyList<StoredImportValidationError> DeserializeImportErrors(string json)
        => DeserializeOrDefault<StoredImportValidationError[]>(json) ?? [];

    private static DataImportErrorDetails MapImportError(StoredImportValidationError error)
        => new(error.Target, error.Message, error.Evidence);

    private static JsonElement ParseJsonElement(string json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? fallback : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse(fallback);
            return document.RootElement.Clone();
        }
    }

    private static byte[] WriteCsv(CsvExportTable table)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, table.Columns);

        foreach (var row in table.Rows)
        {
            AppendCsvLine(
                builder,
                table.Columns.Select(column => row.TryGetValue(column, out var value) ? value : null));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendCsvLine(StringBuilder builder, IEnumerable<string?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(value));
            first = false;
        }

        builder.AppendLine();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = TabularDataFile.SanitizeFormulaText(value);
        return sanitized.Contains('"') ||
            sanitized.Contains(',') ||
            sanitized.Contains('\n') ||
            sanitized.Contains('\r')
            ? $"\"{sanitized.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : sanitized;
    }

    private static bool MatchesCommonFilters(string keyword, string location, string language, ExportFilters filters)
        => MatchesTextFilter(keyword, filters.Q) &&
            (filters.Location is null || string.Equals(location, filters.Location, StringComparison.OrdinalIgnoreCase)) &&
            (filters.Language is null || string.Equals(language, filters.Language, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesTextFilter(string? value, string? q)
        => q is null || (value?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string BuildExportObjectKey(ProjectExecutionContext context, Guid exportId, string format)
        => $"exports/{context.WorkspaceId:N}/{context.ProjectId!.Value:N}/{exportId:N}.{ExportFileExtension(format)}";

    private static string ExportFileExtension(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";

    private static string GetExportContentType(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? "text/csv; charset=utf-8"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static bool TryGetObjectKey(string fileUri, out StorageObjectKey key)
    {
        key = default;
        if (!Uri.TryCreate(fileUri, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "storage", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        key = new StorageObjectKey(path);
        return true;
    }

    private static string BuildDownloadUrl(string fileUri, DateTime expiresAt)
    {
        var separator = fileUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return string.Concat(
            fileUri,
            separator,
            "expiresAt=",
            Uri.EscapeDataString(expiresAt.ToString("O", CultureInfo.InvariantCulture)));
    }

    private void AddJobQueuedAudit(ProjectExecutionContext context, JobEntity job)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                AuditLogActionNames.JobQueued,
                AuditLogResourceTypes.Job,
                job.Id.ToString("D"),
                new
                {
                    before = (object?)null,
                    after = new
                    {
                        jobType = job.JobType,
                        status = job.Status,
                        progress = job.Progress,
                        retryCount = job.RetryCount,
                        nextRunAt = job.NextRunAt,
                        projectId = job.ProjectId,
                        requestHash = job.RequestHash,
                        resultResourceType = job.ResultResourceType,
                        resultResourceId = job.ResultResourceId
                    }
                }));

    private void AddExportAudit(
        ProjectExecutionContext context,
        string action,
        DataExportEntity export,
        object? beforeAfter)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                ExportResourceType(export.Format),
                export.Id.ToString("D"),
                beforeAfter is null
                    ? new { after = ToExportAuditSnapshot(export) }
                    : beforeAfter));

    private void AddDataImportAudit(
        ProjectExecutionContext context,
        string action,
        DataImportEntity import,
        object? beforeAfter)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.DataImport,
                import.Id.ToString("D"),
                beforeAfter is null
                    ? new { after = ToImportAuditSnapshot(import) }
                    : beforeAfter));

    private static string ExportResourceType(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? AuditLogResourceTypes.CsvExport
            : AuditLogResourceTypes.DataExport;

    private static string ExportCreatedAction(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? AuditLogActionNames.CsvExportCreated
            : AuditLogActionNames.DataExportCreated;

    private static string ExportDownloadUrlIssuedAction(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? AuditLogActionNames.CsvDownloadUrlIssued
            : AuditLogActionNames.DataExportDownloadUrlIssued;

    private static string ExportDownloadedAction(string format)
        => string.Equals(format, CsvFormat, StringComparison.OrdinalIgnoreCase)
            ? AuditLogActionNames.CsvDownloaded
            : AuditLogActionNames.DataExportDownloaded;

    private static object ToExportAuditSnapshot(DataExportEntity entity)
        => new
        {
            exportType = entity.ExportType,
            format = entity.Format,
            status = entity.Status,
            projectId = entity.ProjectId,
            fileUri = entity.FileUri,
            requestedBy = entity.RequestedBy,
            completedAt = entity.CompletedAt
        };

    private static object ToImportAuditSnapshot(DataImportEntity entity)
        => new
        {
            importType = entity.ImportType,
            format = entity.Format,
            status = entity.Status,
            projectId = entity.ProjectId,
            sourceFileUri = entity.SourceFileUri,
            requestedBy = entity.RequestedBy,
            completedAt = entity.CompletedAt
        };

    private static DataExportDetails MapExport(DataExportEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.ExportType,
            entity.Format,
            entity.Status,
            entity.FileUri,
            entity.CreatedAt,
            entity.CompletedAt);

    private static DataImportDetails MapImport(DataImportEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.ImportType,
            entity.Format,
            entity.SourceFileUri,
            entity.Status,
            ParseJsonElement(entity.ValidationErrorsJson, "[]"),
            entity.RequestedBy,
            entity.CreatedAt,
            entity.CompletedAt);

    private static T? DeserializeOrDefault<T>(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private DateTimeOffset NowOffset()
        => timeProvider.GetUtcNow();

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatNullableInt(int? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatNullableDecimal(decimal? value)
        => value.HasValue ? FormatDecimal(value.Value) : string.Empty;

    private static string FormatDecimal(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime value)
        => UtcDateTime.EnsureUtc(value).ToString("O", CultureInfo.InvariantCulture);

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => errors.Count > 0;

        public void Add(string target, string message)
        {
            var camelTarget = ToCamelCase(target);
            if (!errors.TryGetValue(camelTarget, out var messages))
            {
                messages = [];
                errors[camelTarget] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        private static string ToCamelCase(string value)
        {
            var sanitized = value.Trim();
            return string.IsNullOrEmpty(sanitized)
                ? sanitized
                : char.ToLowerInvariant(sanitized[0]) + sanitized[1..];
        }
    }

    private sealed record DataExportRequestSnapshot(
        int Version,
        string ExportType,
        string Format,
        JsonElement Filter,
        IReadOnlyList<string> Columns);

    private sealed record DataImportRequestSnapshot(
        int Version,
        string ImportType,
        string Format,
        string SourceFileUri,
        string ValidationMode);

    private sealed record CsvExportTable(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

    private sealed record StoredImportValidationError(
        string Target,
        string Message,
        JsonElement? Evidence = null);

    private sealed record CsvMetricsSnapshot(
        int? SearchVolume,
        decimal? SeoDifficulty,
        decimal? Cpc,
        decimal? Competition,
        string? FirstSeenRange);

    private sealed record ExportFilters(
        string? Q,
        string? Location,
        string? Language,
        decimal? MinSearchVolume,
        decimal? MinOpportunityScore,
        Guid? JobId,
        string? Source,
        string? Provider,
        int? StatusCode)
    {
        public static ExportFilters From(JsonElement filter)
            => new(
                ReadString(filter, "q"),
                ReadString(filter, "location"),
                ReadString(filter, "language"),
                ReadDecimal(filter, "minSearchVolume"),
                ReadDecimal(filter, "minOpportunityScore"),
                ReadGuid(filter, "jobId"),
                ReadString(filter, "source"),
                ReadString(filter, "provider"),
                ReadInt(filter, "statusCode"));

        private static string? ReadString(JsonElement filter, string propertyName)
            => filter.ValueKind == JsonValueKind.Object &&
                filter.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String
                ? OptionalText(property.GetString())
                : null;

        private static decimal? ReadDecimal(JsonElement filter, string propertyName)
        {
            if (filter.ValueKind != JsonValueKind.Object ||
                !filter.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
                JsonValueKind.String when decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) => value,
                _ => null
            };
        }

        private static int? ReadInt(JsonElement filter, string propertyName)
        {
            if (filter.ValueKind != JsonValueKind.Object ||
                !filter.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt32(out var value) => value,
                JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
                _ => null
            };
        }

        private static Guid? ReadGuid(JsonElement filter, string propertyName)
            => filter.ValueKind == JsonValueKind.Object &&
                filter.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                Guid.TryParse(property.GetString(), out var value)
                ? value
                : null;
    }
}

internal sealed class DataExportJob(
    SeoIntelligenceDbContext dbContext,
    DataTransferService dataTransferService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<DataExportJob> logger)
{
    public const string JobType = DataTransferService.JobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Data export job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId?.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Data export job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await dataTransferService.GenerateExportAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await dataTransferService.RecordExportFailureAsync(
                    context,
                    jobId,
                    StatusValues.FailedFatal,
                    result.Error!.Message);
                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
                return;
            }

            var completed = await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(
                    100,
                    new JobResultResource(job.ResultResourceType ?? DataTransferService.DataExportResourceType, result.Value!.ExportId)));
            if (!completed.IsSuccess)
            {
                logger.LogWarning(
                    "Data export job {job_id} could not be marked succeeded: {message}",
                    jobId,
                    completed.Error?.Message);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Data export job {job_id} could not persist state.", jobId);
            await dataTransferService.RecordExportFailureAsync(
                context,
                jobId,
                StatusValues.FailedRetryable,
                "CSV export could not persist state.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("CSV export could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Data export job {job_id} failed unexpectedly.", jobId);
            await dataTransferService.RecordExportFailureAsync(
                context,
                jobId,
                StatusValues.FailedFatal,
                "CSV export failed unexpectedly.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "CSV export failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
        => error.Code switch
        {
            ErrorCode.Conflict or ErrorCode.ValidationFailed or ErrorCode.NotFound
                => JobFailure.FromHttpStatusCode(400, "data_export", error.Message),
            ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited
                => JobFailure.FromHttpStatusCode(503, "data_export", error.Message),
            _ => new JobFailure(JobFailureKind.Unexpected, null, "data_export", error.Message)
        };
}

internal sealed class DataImportJob(
    SeoIntelligenceDbContext dbContext,
    DataTransferService dataTransferService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<DataImportJob> logger)
{
    public const string JobType = DataTransferService.ImportJobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Data import job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId?.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Data import job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await dataTransferService.ExecuteImportAsync(context, jobId);
            if (!result.IsSuccess)
            {
                if (result.Error!.Code != ErrorCode.ValidationFailed)
                {
                    await dataTransferService.RecordImportFailureAsync(
                        context,
                        jobId,
                        StatusValues.FailedFatal,
                        result.Error.Message);
                }

                await jobService.RecordFailureAsync(context, jobId, ToJobFailure(result.Error!));
                return;
            }

            var completed = await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(
                    100,
                    new JobResultResource(DataTransferService.DataImportResourceType, result.Value!.ImportId)));
            if (!completed.IsSuccess)
            {
                logger.LogWarning(
                    "Data import job {job_id} could not be marked succeeded: {message}",
                    jobId,
                    completed.Error?.Message);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Data import job {job_id} could not persist state.", jobId);
            await dataTransferService.RecordImportFailureAsync(
                context,
                jobId,
                StatusValues.FailedRetryable,
                "Data import could not persist state.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Data import could not persist state."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Data import job {job_id} failed unexpectedly.", jobId);
            await dataTransferService.RecordImportFailureAsync(
                context,
                jobId,
                StatusValues.FailedFatal,
                "Data import failed unexpectedly.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Data import failed unexpectedly."));
        }
    }

    private static JobFailure ToJobFailure(Error error)
        => error.Code switch
        {
            ErrorCode.Conflict or ErrorCode.ValidationFailed or ErrorCode.NotFound
                => JobFailure.FromHttpStatusCode(400, "data_import", error.Message),
            ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited
                => JobFailure.FromHttpStatusCode(503, "data_import", error.Message),
            _ => new JobFailure(JobFailureKind.Unexpected, null, "data_import", error.Message)
        };
}
