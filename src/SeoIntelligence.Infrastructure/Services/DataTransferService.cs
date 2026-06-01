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
{
    public const string JobType = "DataExportJob";
    public const string CsvExportResourceType = "csv_export";
    private const string CsvFormat = "csv";
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromMinutes(15);
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

    public async Task<Result<JobReference>> CreateCsvExportAsync(
        ProjectExecutionContext context,
        DataExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var exportType = NormalizeExportType(request.ExportType, errors);
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
            Format: CsvFormat,
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
            Format = CsvFormat,
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
            ResultResourceType = CsvExportResourceType,
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
            ? Failure<DataExportDetails>(ErrorCode.NotFound, "CSV export was not found.")
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
            return Failure<DataExportDownload>(ErrorCode.NotFound, "CSV export was not found.");
        }

        if (!string.Equals(export.Status, StatusValues.Succeeded, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(export.FileUri))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "CSV export is not ready for download.");
        }

        if (!TryGetObjectKey(export.FileUri, out var key))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "CSV export file URI is invalid.");
        }

        if (!await objectStorage.ExistsAsync(key, cancellationToken))
        {
            return Failure<DataExportDownload>(ErrorCode.Conflict, "CSV export file was not found in storage.");
        }

        var expiresAt = NowUtc().Add(DownloadUrlTtl);
        var downloadUrl = BuildDownloadUrl(export.FileUri, expiresAt);
        AddCsvExportAudit(
            context,
            AuditLogActionNames.CsvDownloadUrlIssued,
            export,
            new
            {
                export = ToExportAuditSnapshot(export),
                downloadUrl,
                expiresAt
            });
        AddCsvExportAudit(
            context,
            AuditLogActionNames.CsvDownloaded,
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

    public async Task<Result<DataExportDetails>> GenerateCsvExportAsync(
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
            !string.Equals(job.ResultResourceType, CsvExportResourceType, StringComparison.Ordinal))
        {
            return Failure<DataExportDetails>(ErrorCode.Conflict, "Data export job does not reference a CSV export.");
        }

        var export = await FindExportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (export is null)
        {
            return Failure<DataExportDetails>(ErrorCode.NotFound, "CSV export was not found.");
        }

        var snapshot = ReadSnapshot(export);
        if (snapshot is null)
        {
            return Failure<DataExportDetails>(ErrorCode.Conflict, "CSV export request payload was invalid.");
        }

        var table = await BuildCsvTableAsync(context, snapshot, cancellationToken);
        var csvBytes = WriteCsv(table);
        await using var content = new MemoryStream(csvBytes, writable: false);
        var stored = await objectStorage.PutAsync(
            new StoragePutRequest(
                new StorageObjectKey(BuildExportObjectKey(context, export.Id)),
                content,
                "text/csv; charset=utf-8"),
            cancellationToken);

        var before = ToExportAuditSnapshot(export);
        var now = NowUtc();
        export.FileUri = stored.Uri;
        export.Status = StatusValues.Succeeded;
        export.CompletedAt = now;
        AddCsvExportAudit(
            context,
            AuditLogActionNames.CsvExportCreated,
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
        AddCsvExportAudit(
            context,
            AuditLogActionNames.CsvExportCreated,
            export,
            new
            {
                before,
                after = ToExportAuditSnapshot(export),
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
                !string.Equals(snapshot.Format, CsvFormat, StringComparison.OrdinalIgnoreCase) ||
                !SupportedColumns.ContainsKey(snapshot.ExportType) ||
                snapshot.Columns.Count == 0)
            {
                return null;
            }

            return snapshot with
            {
                ExportType = snapshot.ExportType.ToLowerInvariant(),
                Format = CsvFormat,
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

        return value.Contains('"') ||
            value.Contains(',') ||
            value.Contains('\n') ||
            value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static bool MatchesCommonFilters(string keyword, string location, string language, ExportFilters filters)
        => MatchesTextFilter(keyword, filters.Q) &&
            (filters.Location is null || string.Equals(location, filters.Location, StringComparison.OrdinalIgnoreCase)) &&
            (filters.Language is null || string.Equals(language, filters.Language, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesTextFilter(string? value, string? q)
        => q is null || (value?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string BuildExportObjectKey(ProjectExecutionContext context, Guid exportId)
        => $"exports/{context.WorkspaceId:N}/{context.ProjectId!.Value:N}/{exportId:N}.csv";

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

    private void AddCsvExportAudit(
        ProjectExecutionContext context,
        string action,
        DataExportEntity export,
        object? beforeAfter)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.CsvExport,
                export.Id.ToString("D"),
                beforeAfter is null
                    ? new { after = ToExportAuditSnapshot(export) }
                    : beforeAfter));

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

    private sealed record CsvExportTable(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

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
            var result = await dataTransferService.GenerateCsvExportAsync(context, jobId);
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
                    new JobResultResource(DataTransferService.CsvExportResourceType, result.Value!.ExportId)));
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
