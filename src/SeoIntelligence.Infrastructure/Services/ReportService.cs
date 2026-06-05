using System.Globalization;
using System.Net;
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
using SeoIntelligence.Application.Sharing;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class ReportService(
    SeoIntelligenceDbContext dbContext,
    IObjectStorage objectStorage,
    IAuditLogWriter auditLogWriter,
    IShareTokenService shareTokenService,
    IJobQueueClient jobQueueClient,
    INotificationService notificationService,
    TimeProvider timeProvider)
    : IReportService
{
    public const string JobType = "MonthlyReportJob";
    public const string ReportResourceType = "report";
    public const string ReportArtifactType = "report";

    private const int MaxSectionCount = 20;
    private const int MaxSectionLength = 50;
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DefaultSections =
    [
        "summary",
        "keyword_metrics",
        "rankings",
        "rewrite",
        "cannibalization"
    ];
    private static readonly HashSet<string> SupportedReportTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "monthly",
        "competitor_gap",
        "article_brief",
        "rank"
    };
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf",
        "excel"
    };

    public async Task<Result<JobReference>> CreateReportAsync(
        ProjectExecutionContext context,
        ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var reportType = NormalizeReportType(request.ReportType, errors);
        var period = NormalizePeriod(request.Period, errors);
        var format = NormalizeFormat(request.Format, errors);
        var sections = NormalizeSections(request.Sections, errors);
        var now = NowOffset();

        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (request.ShareExpiresAt.HasValue && request.ShareExpiresAt.Value <= now)
        {
            errors.Add("shareExpiresAt", "shareExpiresAt must be greater than the current time.");
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

        var snapshot = new ReportRequestSnapshot(
            Version: 1,
            ReportType: reportType!,
            Period: period!,
            Format: format!,
            Sections: sections,
            ShareExpiresAt: request.ShareExpiresAt?.ToUniversalTime());
        var payload = JsonSerializer.SerializeToElement(snapshot, JsonOptions);
        var requestHash = HashText(payload.GetRawText());
        var nowUtc = now.UtcDateTime;
        var report = new ReportEntity
        {
            Id = UuidV7.New(),
            ProjectId = project.Id,
            ReportType = reportType!,
            Period = period!,
            Format = format!,
            CurrentVersion = 0,
            Status = "draft",
            GeneratedBy = context.Actor,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        var job = new JobEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = project.Id,
            JobType = JobType,
            Status = StatusValues.Queued,
            Progress = 0,
            RetryCount = 0,
            NextRunAt = nowUtc,
            ResultResourceType = ReportResourceType,
            ResultResourceId = report.Id,
            RequestHash = requestHash,
            RequestedBy = context.Actor,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        dbContext.Reports.Add(report);
        dbContext.Jobs.Add(job);
        AddJobQueuedAudit(context, job);
        AddReportAudit(
            context,
            AuditLogActionNames.ReportGenerationQueued,
            report,
            new
            {
                before = (object?)null,
                after = ToReportAuditSnapshot(report),
                request = snapshot
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobQueueClient.EnqueueAsync(job.Id, "exports", cancellationToken);

        return Result<JobReference>.Success(new JobReference(job.Id, job.Status));
    }

    public async Task<Result<ReportDetails>> GetReportAsync(
        ProjectExecutionContext context,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await FindReportAsync(context, reportId, asTracking: false, cancellationToken);
        return report is null
            ? Failure<ReportDetails>(ErrorCode.NotFound, "Report was not found.")
            : Result<ReportDetails>.Success(MapReport(report));
    }

    public async Task<Result<ReportDownload>> CreateDownloadUrlAsync(
        ProjectExecutionContext context,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await FindReportAsync(context, reportId, asTracking: true, cancellationToken);
        if (report is null)
        {
            return Failure<ReportDownload>(ErrorCode.NotFound, "Report was not found.");
        }

        var readiness = await ValidateReportFileAsync(report, cancellationToken);
        if (!readiness.IsSuccess)
        {
            return Result<ReportDownload>.Failure(readiness.Error!);
        }

        var expiresAt = NowOffset().Add(DownloadUrlTtl);
        var downloadUrl = BuildDownloadUrl(report.FileUri!, expiresAt);
        AddReportAudit(
            context,
            AuditLogActionNames.ReportDownloadUrlIssued,
            report,
            new
            {
                report = ToReportAuditSnapshot(report),
                downloadUrl,
                expiresAt
            });
        AddReportAudit(
            context,
            AuditLogActionNames.ReportDownloaded,
            report,
            new
            {
                report = ToReportAuditSnapshot(report),
                downloadUrl,
                expiresAt,
                via = "short_lived_url"
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ReportDownload>.Success(new ReportDownload(report.Id, downloadUrl, expiresAt));
    }

    public async Task<Result<ReportShareDetails>> ShareReportAsync(
        ProjectExecutionContext context,
        Guid reportId,
        ReportShareRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await FindReportAsync(context, reportId, asTracking: true, cancellationToken);
        if (report is null)
        {
            return Failure<ReportShareDetails>(ErrorCode.NotFound, "Report was not found.");
        }

        var readiness = await ValidateReportFileAsync(report, cancellationToken);
        if (!readiness.IsSuccess)
        {
            return Result<ReportShareDetails>.Failure(readiness.Error!);
        }

        var issued = shareTokenService.Issue(request.ShareExpiresAt.ToUniversalTime(), NowOffset());
        if (!issued.IsSuccess)
        {
            return Result<ReportShareDetails>.Failure(issued.Error!);
        }

        var before = ToReportAuditSnapshot(report);
        report.ShareTokenHash = issued.Value!.TokenHash;
        report.ShareExpiresAt = issued.Value.ExpiresAt.UtcDateTime;
        report.ShareRevokedAt = null;
        report.UpdatedAt = NowUtc();
        var shareUrl = BuildShareUrl(issued.Value.Token);
        AddReportAudit(
            context,
            AuditLogActionNames.ReportShareIssued,
            report,
            new
            {
                before,
                after = ToReportAuditSnapshot(report),
                shareUrlReturnedOnce = true
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ReportShareDetails>.Success(new ReportShareDetails(
            report.Id,
            shareUrl,
            ToOffset(report.ShareExpiresAt),
            ToOffset(report.ShareRevokedAt),
            "active"));
    }

    public async Task<Result<ReportShareDetails>> RevokeShareAsync(
        ProjectExecutionContext context,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await FindReportAsync(context, reportId, asTracking: true, cancellationToken);
        if (report is null)
        {
            return Failure<ReportShareDetails>(ErrorCode.NotFound, "Report was not found.");
        }

        if (string.IsNullOrWhiteSpace(report.ShareTokenHash))
        {
            return Failure<ReportShareDetails>(ErrorCode.Conflict, "Report share is not active.");
        }

        var before = ToReportAuditSnapshot(report);
        report.ShareRevokedAt = NowUtc();
        report.UpdatedAt = report.ShareRevokedAt.Value;
        AddReportAudit(
            context,
            AuditLogActionNames.ReportShareRevoked,
            report,
            new
            {
                before,
                after = ToReportAuditSnapshot(report)
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ReportShareDetails>.Success(new ReportShareDetails(
            report.Id,
            null,
            ToOffset(report.ShareExpiresAt),
            ToOffset(report.ShareRevokedAt),
            "revoked"));
    }

    public async Task<Result<ReportShareAccessDetails>> GetSharedReportAsync(
        ProjectExecutionContext context,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = OptionalText(token);
        var tokenHash = normalizedToken is null ? "empty" : shareTokenService.HashToken(normalizedToken);
        var report = await dbContext.Reports
            .FirstOrDefaultAsync(entity => entity.ShareTokenHash == tokenHash, cancellationToken);

        if (report is null)
        {
            AddReportShareAudit(
                context,
                tokenHash,
                ShareTokenValidationStatus.Unknown,
                "unknown_or_tampered");
            await dbContext.SaveChangesAsync(cancellationToken);
            return Failure<ReportShareAccessDetails>(ErrorCode.NotFound, "Report share was not found.");
        }

        var validation = shareTokenService.Validate(
            normalizedToken,
            report.ShareTokenHash,
            ToOffset(report.ShareExpiresAt),
            ToOffset(report.ShareRevokedAt),
            NowOffset());
        if (validation.Status != ShareTokenValidationStatus.Valid)
        {
            AddReportAudit(
                context with { ProjectId = report.ProjectId },
                AuditLogActionNames.ReportShareAccessRejected,
                report,
                new
                {
                    report = ToReportAuditSnapshot(report),
                    reason = validation.Status.ToString().ToLowerInvariant()
                });
            await dbContext.SaveChangesAsync(cancellationToken);
            return validation.Status == ShareTokenValidationStatus.Unknown
                ? Failure<ReportShareAccessDetails>(ErrorCode.NotFound, "Report share was not found.")
                : Failure<ReportShareAccessDetails>(ErrorCode.Gone, "Report share is expired or revoked.");
        }

        var readiness = await ValidateReportFileAsync(report, cancellationToken);
        if (!readiness.IsSuccess)
        {
            return Result<ReportShareAccessDetails>.Failure(readiness.Error!);
        }

        var expiresAt = NowOffset().Add(DownloadUrlTtl);
        var downloadUrl = BuildDownloadUrl(report.FileUri!, expiresAt);
        var reportContext = context with { ProjectId = report.ProjectId };
        AddReportAudit(
            reportContext,
            AuditLogActionNames.ReportShareAccessed,
            report,
            new
            {
                report = ToReportAuditSnapshot(report),
                downloadUrl,
                downloadExpiresAt = expiresAt
            });
        AddReportAudit(
            reportContext,
            AuditLogActionNames.ReportDownloaded,
            report,
            new
            {
                report = ToReportAuditSnapshot(report),
                downloadUrl,
                expiresAt,
                via = "share_url"
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ReportShareAccessDetails>.Success(new ReportShareAccessDetails(
            report.Id,
            report.ReportType,
            report.Period,
            report.Format,
            downloadUrl,
            expiresAt));
    }

    public async Task<Result<ReportDetails>> GenerateReportAsync(
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
            return Failure<ReportDetails>(ErrorCode.NotFound, "Report job was not found.");
        }

        if (!job.ResultResourceId.HasValue ||
            !string.Equals(job.ResultResourceType, ReportResourceType, StringComparison.Ordinal))
        {
            return Failure<ReportDetails>(ErrorCode.Conflict, "Report job does not reference a report.");
        }

        var report = await FindReportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (report is null)
        {
            return Failure<ReportDetails>(ErrorCode.NotFound, "Report was not found.");
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ReportDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var snapshot = await BuildReportContentSnapshotAsync(project, report, cancellationToken);
        var contentJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var bytes = string.Equals(report.Format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? BuildPdf(snapshot)
            : BuildExcel(snapshot);
        var key = BuildReportObjectKey(context, report);
        await using var stream = new MemoryStream(bytes, writable: false);
        var stored = await objectStorage.PutAsync(
            new StoragePutRequest(key, stream, ResolveContentType(report.Format)),
            cancellationToken);

        var before = ToReportAuditSnapshot(report);
        var now = NowUtc();
        report.FileUri = stored.Uri;
        report.CurrentVersion += 1;
        report.Status = "completed";
        report.UpdatedAt = now;

        dbContext.ArtifactVersions.Add(new ArtifactVersionEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = report.ProjectId,
            ArtifactType = ReportArtifactType,
            ArtifactId = report.Id,
            VersionNo = report.CurrentVersion,
            ContentHash = HashBytes(bytes),
            ContentUri = stored.Uri,
            ContentJson = contentJson,
            CreatedBy = context.Actor,
            ReviewStatus = "completed",
            ChangeSummary = "Generated report artifact.",
            CreatedAt = now
        });
        AddReportAudit(
            context,
            AuditLogActionNames.ReportCreated,
            report,
            new
            {
                before,
                after = ToReportAuditSnapshot(report),
                artifact = new
                {
                    versionNo = report.CurrentVersion,
                    contentHash = HashBytes(bytes),
                    contentUri = stored.Uri,
                    stored.ContentType,
                    stored.Length
                }
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ReportDetails>.Success(MapReport(report));
    }

    public async Task RecordReportFailureAsync(
        ProjectExecutionContext context,
        Guid jobId,
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

        var report = await FindReportAsync(context, job.ResultResourceId.Value, asTracking: true, cancellationToken);
        if (report is null ||
            string.Equals(report.Status, "completed", StringComparison.Ordinal))
        {
            return;
        }

        report.UpdatedAt = NowUtc();
        AddReportAudit(
            context,
            AuditLogActionNames.ReportCreated,
            report,
            new
            {
                report = ToReportAuditSnapshot(report),
                error = message
            });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueReportCompletedNotificationAsync(
        ProjectExecutionContext context,
        Guid jobId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await FindReportAsync(context, reportId, asTracking: false, cancellationToken);
        if (report is null ||
            !string.Equals(report.Status, "completed", StringComparison.Ordinal))
        {
            return;
        }

        await notificationService.EnqueueAsync(
            context,
            new NotificationRequest(
                NotificationService.ReportCompletedEventType,
                ResourceType: AuditLogResourceTypes.Report,
                ResourceId: report.Id,
                Message: BuildReportCompletedNotificationMessage(report),
                JobId: jobId),
            cancellationToken);
    }

    private async Task<Result> ValidateReportFileAsync(
        ReportEntity report,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(report.Status, "completed", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(report.FileUri))
        {
            return Result.Failure(new Error(ErrorCode.Conflict, "Report is not ready for download."));
        }

        if (!TryGetObjectKey(report.FileUri, out var key))
        {
            return Result.Failure(new Error(ErrorCode.Conflict, "Report file URI is invalid."));
        }

        if (!await objectStorage.ExistsAsync(key, cancellationToken))
        {
            return Result.Failure(new Error(ErrorCode.Conflict, "Report file was not found in storage."));
        }

        return Result.Success();
    }

    private async Task<ReportContentSnapshot> BuildReportContentSnapshotAsync(
        ProjectEntity project,
        ReportEntity report,
        CancellationToken cancellationToken)
    {
        var topKeywords = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .OrderByDescending(entity => entity.OpportunityScore)
            .Take(10)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                score => score.KeywordId,
                keyword => keyword.Id,
                (score, keyword) => new ReportKeywordRow(
                    keyword.NormalizedText,
                    score.OpportunityScore,
                    score.Location,
                    score.Language))
            .ToArrayAsync(cancellationToken);
        var rankRows = await dbContext.RankResults
            .AsNoTracking()
            .Where(entity => entity.ProjectId == project.Id)
            .Select(entity => new { entity.Target, entity.Position })
            .ToArrayAsync(cancellationToken);
        var rankSummary = rankRows
            .GroupBy(entity => entity.Target)
            .Select(group => new ReportRankTargetSummary(
                group.Key,
                group.Count(),
                group.Average(entity => entity.Position)))
            .OrderBy(row => row.Target)
            .Take(10)
            .ToArray();
        var rewriteTaskCount = await dbContext.RewriteTasks
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == project.Id, cancellationToken);
        var activeRewriteTaskCount = await dbContext.RewriteTasks
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == project.Id && entity.Status == StatusValues.Active, cancellationToken);
        var cannibalizationCount = await dbContext.CannibalizationCandidates
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == project.Id, cancellationToken);
        var activeCannibalizationCount = await dbContext.CannibalizationCandidates
            .AsNoTracking()
            .CountAsync(entity => entity.ProjectId == project.Id && entity.Status == StatusValues.Active, cancellationToken);

        return new ReportContentSnapshot(
            GeneratedAt: NowOffset(),
            ProjectId: project.Id,
            ProjectName: project.Name,
            ReportId: report.Id,
            ReportType: report.ReportType,
            Period: report.Period,
            Format: report.Format,
            TopKeywords: topKeywords,
            RankTargets: rankSummary,
            RewriteTaskCount: rewriteTaskCount,
            ActiveRewriteTaskCount: activeRewriteTaskCount,
            CannibalizationCandidateCount: cannibalizationCount,
            ActiveCannibalizationCandidateCount: activeCannibalizationCount);
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
            .FirstOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
    }

    private async Task<ReportEntity?> FindReportAsync(
        ProjectExecutionContext context,
        Guid reportId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        var source = asTracking ? dbContext.Reports : dbContext.Reports.AsNoTracking();
        return await source
            .Where(entity => entity.ProjectId == context.ProjectId.Value)
            .FirstOrDefaultAsync(entity => entity.Id == reportId, cancellationToken);
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

    private void AddReportAudit(
        ProjectExecutionContext context,
        string action,
        ReportEntity report,
        object? beforeAfter)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.Report,
                report.Id.ToString("D"),
                beforeAfter ?? new { after = ToReportAuditSnapshot(report) }));

    private void AddReportShareAudit(
        ProjectExecutionContext context,
        string tokenHash,
        ShareTokenValidationStatus status,
        string reason)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                AuditLogActionNames.ReportShareAccessRejected,
                AuditLogResourceTypes.ReportShare,
                tokenHash,
                new
                {
                    after = new
                    {
                        status = status.ToString().ToLowerInvariant(),
                        reason
                    }
                }));

    private static object ToReportAuditSnapshot(ReportEntity entity)
        => new
        {
            reportType = entity.ReportType,
            period = entity.Period,
            format = entity.Format,
            currentVersion = entity.CurrentVersion,
            fileUri = entity.FileUri,
            shareExpiresAt = entity.ShareExpiresAt,
            shareRevokedAt = entity.ShareRevokedAt,
            hasShareToken = !string.IsNullOrWhiteSpace(entity.ShareTokenHash),
            status = entity.Status,
            generatedBy = entity.GeneratedBy,
            projectId = entity.ProjectId
        };

    private static ReportDetails MapReport(ReportEntity entity)
        => new(
            entity.Id,
            entity.ProjectId,
            entity.ReportType,
            entity.Period,
            entity.Format,
            entity.CurrentVersion,
            entity.FileUri,
            ToOffset(entity.ShareExpiresAt),
            ToOffset(entity.ShareRevokedAt),
            entity.Status,
            entity.GeneratedBy,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static byte[] BuildPdf(ReportContentSnapshot snapshot)
    {
        var lines = BuildReportLines(snapshot).Take(34).ToArray();
        var streamBuilder = new StringBuilder();
        streamBuilder.AppendLine("BT");
        streamBuilder.AppendLine("/F1 11 Tf");
        streamBuilder.AppendLine("72 760 Td");
        foreach (var line in lines)
        {
            streamBuilder.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
            streamBuilder.AppendLine("0 -16 Td");
        }

        streamBuilder.AppendLine("ET");
        var content = Encoding.ASCII.GetBytes(streamBuilder.ToString());
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n{Encoding.ASCII.GetString(content)}endstream"
        };

        var builder = new StringBuilder();
        builder.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };
        foreach (var (obj, index) in objects.Select((value, index) => (value, index + 1)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index.ToString(CultureInfo.InvariantCulture)).AppendLine(" 0 obj");
            builder.AppendLine(obj);
            builder.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.AppendLine("xref");
        builder.Append("0 ").AppendLine((objects.Length + 1).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("0000000000", CultureInfo.InvariantCulture)).AppendLine(" 00000 n ");
        }

        builder.AppendLine("trailer");
        builder.Append("<< /Size ").Append(objects.Length + 1).AppendLine(" /Root 1 0 R >>");
        builder.AppendLine("startxref");
        builder.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] BuildExcel(ReportContentSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<html><head><meta charset=\"utf-8\"></head><body>");
        builder.AppendLine("<table>");
        foreach (var line in BuildReportLines(snapshot))
        {
            builder.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(line))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</table>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><th>keyword</th><th>opportunityScore</th><th>location</th><th>language</th></tr>");
        foreach (var keyword in snapshot.TopKeywords)
        {
            builder.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(keyword.Keyword))
                .Append("</td><td>")
                .Append(FormatDecimal(keyword.OpportunityScore))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(keyword.Location))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(keyword.Language))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static IEnumerable<string> BuildReportLines(ReportContentSnapshot snapshot)
    {
        yield return "SEO Intelligence Report";
        yield return $"Project: {snapshot.ProjectName}";
        yield return $"Report: {snapshot.ReportType} / {snapshot.Period} / {snapshot.Format}";
        yield return $"Generated: {snapshot.GeneratedAt:O}";
        yield return $"Top keyword count: {snapshot.TopKeywords.Count.ToString(CultureInfo.InvariantCulture)}";
        yield return $"Rewrite tasks: {snapshot.ActiveRewriteTaskCount.ToString(CultureInfo.InvariantCulture)} active / {snapshot.RewriteTaskCount.ToString(CultureInfo.InvariantCulture)} total";
        yield return $"Cannibalization candidates: {snapshot.ActiveCannibalizationCandidateCount.ToString(CultureInfo.InvariantCulture)} active / {snapshot.CannibalizationCandidateCount.ToString(CultureInfo.InvariantCulture)} total";
        yield return "Top keywords";

        foreach (var keyword in snapshot.TopKeywords)
        {
            yield return $"- {keyword.Keyword}: {FormatDecimal(keyword.OpportunityScore)} ({keyword.Location}/{keyword.Language})";
        }

        yield return "Rank target summary";
        foreach (var target in snapshot.RankTargets)
        {
            yield return $"- {target.Target}: {target.ResultCount.ToString(CultureInfo.InvariantCulture)} results, average position {FormatDecimal(Convert.ToDecimal(target.AveragePosition, CultureInfo.InvariantCulture))}";
        }
    }

    private static string EscapePdfText(string value)
    {
        var ascii = new string(value.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
        return ascii
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static StorageObjectKey BuildReportObjectKey(ProjectExecutionContext context, ReportEntity report)
    {
        var extension = string.Equals(report.Format, "pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "xls";
        return new StorageObjectKey($"reports/{context.WorkspaceId:N}/{report.ProjectId:N}/{report.Period}/{report.Id:N}.{extension}");
    }

    private static string ResolveContentType(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/vnd.ms-excel; charset=utf-8";

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

    private static string BuildDownloadUrl(string fileUri, DateTimeOffset expiresAt)
    {
        var separator = fileUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return string.Concat(
            fileUri,
            separator,
            "expiresAt=",
            Uri.EscapeDataString(expiresAt.ToString("O", CultureInfo.InvariantCulture)));
    }

    private static string BuildShareUrl(string token)
        => $"/api/report-shares/{Uri.EscapeDataString(token)}";

    private static string BuildReportCompletedNotificationMessage(ReportEntity report)
        => string.Join(
            Environment.NewLine,
            [
                "[report_completed] SEO report generation completed.",
                $"Report: {report.ReportType} {report.Period} ({report.Format})",
                $"Resource: {AuditLogResourceTypes.Report}/{report.Id:D}"
            ]);

    private static string? NormalizeReportType(string? value, ValidationErrors errors)
    {
        var reportType = OptionalText(value)?.ToLowerInvariant();
        if (reportType is null)
        {
            errors.Add("reportType", "reportType is required.");
            return null;
        }

        if (!SupportedReportTypes.Contains(reportType))
        {
            errors.Add("reportType", "reportType must be monthly, competitor_gap, article_brief, or rank.");
            return null;
        }

        return reportType;
    }

    private static string? NormalizePeriod(string? value, ValidationErrors errors)
    {
        var period = OptionalText(value);
        if (period is null)
        {
            errors.Add("period", "period is required.");
            return null;
        }

        if (!DateTime.TryParseExact(
            period,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            errors.Add("period", "period must use YYYY-MM.");
            return null;
        }

        return period;
    }

    private static string? NormalizeFormat(string? value, ValidationErrors errors)
    {
        var format = OptionalText(value)?.ToLowerInvariant();
        if (format is null)
        {
            errors.Add("format", "format is required.");
            return null;
        }

        if (!SupportedFormats.Contains(format))
        {
            errors.Add("format", "format must be pdf or excel.");
            return null;
        }

        return format;
    }

    private static IReadOnlyList<string> NormalizeSections(
        IReadOnlyList<string>? sections,
        ValidationErrors errors)
    {
        if (sections is null || sections.Count == 0)
        {
            return DefaultSections;
        }

        if (sections.Count > MaxSectionCount)
        {
            errors.Add("sections", $"sections must contain {MaxSectionCount.ToString(CultureInfo.InvariantCulture)} items or fewer.");
        }

        var normalized = new List<string>();
        foreach (var section in sections)
        {
            var value = OptionalText(section);
            if (value is null)
            {
                errors.Add("sections", "sections must not contain empty values.");
                continue;
            }

            if (value.Length > MaxSectionLength)
            {
                errors.Add("sections", $"sections values must be {MaxSectionLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
                continue;
            }

            var sectionName = value.ToLowerInvariant();
            if (!normalized.Contains(sectionName, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(sectionName);
            }
        }

        return normalized.Count == 0 ? DefaultSections : normalized;
    }

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private DateTimeOffset NowOffset()
        => timeProvider.GetUtcNow();

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatDecimal(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string HashText(string value)
        => HashBytes(Encoding.UTF8.GetBytes(value));

    private static string HashBytes(byte[] value)
        => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => errors.Count > 0;

        public void Add(string target, string message)
        {
            if (!errors.TryGetValue(target, out var messages))
            {
                messages = [];
                errors[target] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ReportRequestSnapshot(
        int Version,
        string ReportType,
        string Period,
        string Format,
        IReadOnlyList<string> Sections,
        DateTimeOffset? ShareExpiresAt);

    private sealed record ReportContentSnapshot(
        DateTimeOffset GeneratedAt,
        Guid ProjectId,
        string ProjectName,
        Guid ReportId,
        string ReportType,
        string Period,
        string Format,
        IReadOnlyList<ReportKeywordRow> TopKeywords,
        IReadOnlyList<ReportRankTargetSummary> RankTargets,
        int RewriteTaskCount,
        int ActiveRewriteTaskCount,
        int CannibalizationCandidateCount,
        int ActiveCannibalizationCandidateCount);

    private sealed record ReportKeywordRow(
        string Keyword,
        decimal OpportunityScore,
        string Location,
        string Language);

    private sealed record ReportRankTargetSummary(
        string Target,
        int ResultCount,
        double AveragePosition);
}

internal sealed class MonthlyReportJob(
    SeoIntelligenceDbContext dbContext,
    ReportService reportService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<MonthlyReportJob> logger)
{
    public const string JobType = ReportService.JobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("Monthly report job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, ReportService.ReportResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "Monthly report job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Monthly report job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await reportService.GenerateReportAsync(context, jobId);
            if (!result.IsSuccess)
            {
                await RecordFailureAsync(context, jobId, result.Error!);
                return;
            }

            var completed = await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(
                    100,
                    new JobResultResource(ReportService.ReportResourceType, result.Value!.ReportId)));
            if (!completed.IsSuccess)
            {
                logger.LogWarning(
                    "Monthly report job {job_id} could not be marked succeeded: {message}",
                    jobId,
                    completed.Error?.Message);
                return;
            }

            await reportService.EnqueueReportCompletedNotificationAsync(
                context,
                jobId,
                result.Value.ReportId);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Monthly report job {job_id} could not persist results.", jobId);
            await reportService.RecordReportFailureAsync(
                context,
                jobId,
                "Monthly report could not persist results.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Monthly report could not persist results."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Monthly report job {job_id} failed unexpectedly.", jobId);
            await reportService.RecordReportFailureAsync(
                context,
                jobId,
                "Monthly report failed unexpectedly.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "Monthly report failed unexpectedly."));
        }
    }

    private async Task RecordFailureAsync(ProjectExecutionContext context, Guid jobId, Error error)
    {
        await reportService.RecordReportFailureAsync(context, jobId, error.Message);
        await jobService.RecordFailureAsync(
            context,
            jobId,
            error.Code switch
            {
                ErrorCode.Conflict or ErrorCode.ValidationFailed or ErrorCode.NotFound
                    => JobFailure.FromHttpStatusCode(400, "monthly_report", error.Message),
                ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited
                    => JobFailure.FromHttpStatusCode(503, "monthly_report", error.Message),
                _ => new JobFailure(JobFailureKind.Unexpected, null, "monthly_report", error.Message)
            });
    }
}
