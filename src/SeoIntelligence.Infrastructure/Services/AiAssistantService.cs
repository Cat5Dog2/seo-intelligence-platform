using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Ai;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class AiAssistantService(
    SeoIntelligenceDbContext dbContext,
    IPromptRedactor promptRedactor,
    IAiContentService aiContentService,
    IAuditLogWriter auditLogWriter,
    IJobQueueClient jobQueueClient,
    TimeProvider timeProvider)
    : IAiAssistantService
{
    public const string JobType = "AiAssistantJob";
    public const string MessageResourceType = "ai_message";
    public const string SessionResourceType = "ai_session";
    public const string ArtifactType = "ai_message";

    private const int MaxMessageLength = 8_000;
    private const int MaxAllowedToolCount = 12;
    private const int MaxAllowedToolLength = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "keyword-discovery",
        "search-volume",
        "rank-results",
        "competitor-analysis",
        "content-analysis",
        "brief-generation",
        "rewrite-analysis",
        "report-summary"
    };

    public async Task<Result<AiChatResponse>> ChatAsync(
        ProjectExecutionContext context,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var message = NormalizeMessage(request.Message, errors);
        var allowedTools = NormalizeAllowedTools(request.AllowedTools, errors);
        var referenceScope = NormalizeReferenceScope(request.ReferenceScope, errors);

        if (!context.ProjectId.HasValue)
        {
            errors.Add("projectId", "projectId is required.");
        }

        if (errors.HasErrors)
        {
            return ValidationFailure<AiChatResponse>(errors);
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<AiChatResponse>(ErrorCode.NotFound, "Project was not found.");
        }

        var redaction = promptRedactor.Redact(message);
        var now = NowUtc();
        AiSessionEntity session;
        if (request.ConversationId.HasValue)
        {
            var existingSession = await dbContext.AiSessions
                .SingleOrDefaultAsync(
                    entity =>
                        entity.WorkspaceId == context.WorkspaceId &&
                        entity.ProjectId == project.Id &&
                        entity.Id == request.ConversationId.Value,
                    cancellationToken);
            if (existingSession is null)
            {
                return Failure<AiChatResponse>(ErrorCode.NotFound, "AI conversation was not found.");
            }

            session = existingSession;
        }
        else
        {
            session = new AiSessionEntity
            {
                Id = UuidV7.New(),
                WorkspaceId = context.WorkspaceId,
                ProjectId = project.Id,
                Actor = context.Actor,
                Title = BuildSessionTitle(redaction.RedactedPrompt),
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.AiSessions.Add(session);
        }

        var requestSnapshot = new AiAssistantRequestSnapshot(
            Version: 1,
            Prompt: redaction.RedactedPrompt,
            AllowedTools: allowedTools,
            ReferenceScope: referenceScope,
            MatchedRedactionCategories: redaction.MatchedCategories);
        var snapshotJson = JsonSerializer.SerializeToElement(requestSnapshot, JsonOptions);
        var messageEntity = new AiMessageEntity
        {
            Id = UuidV7.New(),
            SessionId = session.Id,
            MessageRole = "assistant",
            Prompt = redaction.RedactedPrompt,
            Response = string.Empty,
            ToolCallsJson = "[]",
            ReferenceDataJson = snapshotJson.GetRawText(),
            RedactionStatus = redaction.RedactionStatus,
            ReviewStatus = StatusValues.Pending,
            TokenUsage = "{}",
            CreatedAt = now
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
            NextRunAt = now,
            ResultResourceType = MessageResourceType,
            ResultResourceId = messageEntity.Id,
            RequestHash = HashText(snapshotJson.GetRawText()),
            RequestedBy = context.Actor,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AiMessages.Add(messageEntity);
        dbContext.Jobs.Add(job);
        session.UpdatedAt = now;
        AddJobQueuedAudit(context, job);
        AddAiAudit(
            context,
            AuditLogActionNames.AiChatQueued,
            messageEntity,
            new
            {
                before = (object?)null,
                after = ToMessageAuditSnapshot(messageEntity, job.Id),
                request = requestSnapshot
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await jobQueueClient.EnqueueAsync(job.Id, "analysis", cancellationToken);

        return Result<AiChatResponse>.Success(MapResponse(messageEntity, job.Id));
    }

    public async Task<Result<AiChatResponse>> GenerateResponseAsync(
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
            return Failure<AiChatResponse>(ErrorCode.NotFound, "AI assistant job was not found.");
        }

        if (!job.ResultResourceId.HasValue ||
            !string.Equals(job.ResultResourceType, MessageResourceType, StringComparison.Ordinal))
        {
            return Failure<AiChatResponse>(ErrorCode.Conflict, "AI assistant job does not reference an AI message.");
        }

        var message = await dbContext.AiMessages
            .SingleOrDefaultAsync(entity => entity.Id == job.ResultResourceId.Value, cancellationToken);
        if (message is null)
        {
            return Failure<AiChatResponse>(ErrorCode.NotFound, "AI message was not found.");
        }

        var session = await dbContext.AiSessions
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.ProjectId == context.ProjectId &&
                    entity.Id == message.SessionId,
                cancellationToken);
        if (session is null)
        {
            return Failure<AiChatResponse>(ErrorCode.NotFound, "AI session was not found.");
        }

        if (!string.IsNullOrWhiteSpace(message.Response))
        {
            return Result<AiChatResponse>.Success(MapResponse(message, job.Id));
        }

        var snapshot = DeserializeOrDefault<AiAssistantRequestSnapshot>(message.ReferenceDataJson);
        if (snapshot is null)
        {
            return Failure<AiChatResponse>(ErrorCode.Conflict, "AI assistant request payload was invalid.");
        }

        var referenceData = await BuildReferenceDataAsync(context, session, snapshot, cancellationToken);
        var generated = await aiContentService.GenerateAsync(
            new AiContentRequest(
                message.Prompt,
                referenceData,
                snapshot.AllowedTools),
            cancellationToken);
        if (!generated.IsSuccess)
        {
            return Result<AiChatResponse>.Failure(generated.Error!);
        }

        var before = ToMessageAuditSnapshot(message, job.Id);
        message.Response = generated.Value!.Response;
        message.ToolCallsJson = JsonSerializer.Serialize(generated.Value.ToolCalls, JsonOptions);
        message.ReferenceDataJson = referenceData.GetRawText();
        message.TokenUsage = generated.Value.TokenUsage.GetRawText();
        message.ReviewStatus = StatusValues.Pending;
        session.UpdatedAt = NowUtc();

        dbContext.ArtifactVersions.Add(new ArtifactVersionEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = context.ProjectId,
            ArtifactType = ArtifactType,
            ArtifactId = message.Id,
            VersionNo = 1,
            ContentHash = HashText(message.Response),
            ContentUri = null,
            ContentJson = JsonSerializer.Serialize(new
            {
                response = message.Response,
                toolCalls = generated.Value.ToolCalls,
                referenceData,
                tokenUsage = generated.Value.TokenUsage
            }, JsonOptions),
            CreatedBy = context.Actor,
            ReviewStatus = message.ReviewStatus,
            ChangeSummary = "Generated AI assistant draft.",
            CreatedAt = session.UpdatedAt
        });
        AddAiAudit(
            context,
            AuditLogActionNames.AiChatCompleted,
            message,
            new
            {
                before,
                after = ToMessageAuditSnapshot(message, job.Id)
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AiChatResponse>.Success(MapResponse(message, job.Id));
    }

    public async Task RecordAiFailureAsync(
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

        var aiMessage = await dbContext.AiMessages
            .SingleOrDefaultAsync(entity => entity.Id == job.ResultResourceId.Value, cancellationToken);
        if (aiMessage is null)
        {
            return;
        }

        AddAiAudit(
            context,
            AuditLogActionNames.AiChatFailed,
            aiMessage,
            new
            {
                after = ToMessageAuditSnapshot(aiMessage, jobId),
                error = message
            });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<JsonElement> BuildReferenceDataAsync(
        ProjectExecutionContext context,
        AiSessionEntity session,
        AiAssistantRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var projectId = context.ProjectId!.Value;
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == projectId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                entity.DefaultLocation,
                entity.DefaultLanguage
            })
            .SingleAsync(cancellationToken);
        var rewriteTasks = await dbContext.RewriteTasks
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.Status == StatusValues.Active)
            .OrderByDescending(entity => entity.PriorityScore)
            .ThenByDescending(entity => entity.UpdatedAt)
            .Take(5)
            .Select(entity => new
            {
                taskId = entity.Id,
                entity.TargetUrl,
                entity.PriorityScore,
                entity.Status,
                reason = ParseJsonElement(entity.ReasonJson, "{}")
            })
            .ToArrayAsync(cancellationToken);
        var cannibalizationCandidates = await dbContext.CannibalizationCandidates
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId && entity.Status == StatusValues.Active)
            .OrderByDescending(entity => entity.SeverityScore)
            .ThenByDescending(entity => entity.DetectedAt)
            .Take(5)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                candidate => candidate.KeywordId,
                keyword => keyword.Id,
                (candidate, keyword) => new
                {
                    candidateId = candidate.Id,
                    keyword = keyword.NormalizedText,
                    candidate.PrimaryUrl,
                    candidate.SeverityScore,
                    competingUrls = ParseJsonElement(candidate.CompetingUrlsJson, "[]"),
                    recommendation = ParseJsonElement(candidate.RecommendationJson, "{}")
                })
            .ToArrayAsync(cancellationToken);
        var reports = await dbContext.Reports
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(5)
            .Select(entity => new
            {
                reportId = entity.Id,
                entity.ReportType,
                entity.Period,
                entity.Format,
                entity.Status,
                hasFile = entity.FileUri != null,
                hasShareUrl = entity.ShareTokenHash != null
            })
            .ToArrayAsync(cancellationToken);
        var briefs = await dbContext.ArticleBriefs
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.UpdatedAt)
            .Take(5)
            .Select(entity => new
            {
                briefId = entity.Id,
                entity.Title,
                entity.Status,
                entity.ReviewStatus,
                entity.CurrentVersion
            })
            .ToArrayAsync(cancellationToken);
        var topKeywords = await dbContext.ProjectKeywordScores
            .AsNoTracking()
            .Where(entity => entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.OpportunityScore)
            .Take(5)
            .Join(
                dbContext.Keywords.AsNoTracking(),
                score => score.KeywordId,
                keyword => keyword.Id,
                (score, keyword) => new
                {
                    keywordId = keyword.Id,
                    keyword = keyword.NormalizedText,
                    score.OpportunityScore,
                    score.Location,
                    score.Language
                })
            .ToArrayAsync(cancellationToken);

        return JsonSerializer.SerializeToElement(
            new
            {
                project,
                session = new
                {
                    sessionId = session.Id,
                    session.Actor,
                    session.Title
                },
                request = new
                {
                    allowedTools = snapshot.AllowedTools,
                    referenceScope = snapshot.ReferenceScope,
                    redactionCategories = snapshot.MatchedRedactionCategories
                },
                topKeywords,
                rewriteTasks,
                cannibalizationCandidates,
                reports,
                briefs
            },
            JsonOptions);
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
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
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

    private void AddAiAudit(
        ProjectExecutionContext context,
        string action,
        AiMessageEntity message,
        object beforeAfter)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.AiMessage,
                message.Id.ToString("D"),
                beforeAfter));

    private static AiChatResponse MapResponse(AiMessageEntity entity, Guid jobId)
        => new(
            entity.SessionId,
            entity.Id,
            jobId,
            string.IsNullOrWhiteSpace(entity.Response)
                ? "AI response generation has been queued."
                : entity.Response,
            ParseJsonArray(entity.ToolCallsJson),
            ParseJsonElement(entity.ReferenceDataJson, "{}"),
            ParseJsonElement(entity.TokenUsage, "{}"),
            entity.RedactionStatus,
            entity.ReviewStatus);

    private static object ToMessageAuditSnapshot(AiMessageEntity entity, Guid jobId)
        => new
        {
            sessionId = entity.SessionId,
            messageId = entity.Id,
            jobId,
            role = entity.MessageRole,
            prompt = entity.Prompt,
            hasResponse = !string.IsNullOrWhiteSpace(entity.Response),
            toolCallCount = ParseJsonArray(entity.ToolCallsJson).Count,
            redactionStatus = entity.RedactionStatus,
            reviewStatus = entity.ReviewStatus,
            hasTokenUsage = !string.IsNullOrWhiteSpace(entity.TokenUsage) && entity.TokenUsage != "{}",
            createdAt = entity.CreatedAt
        };

    private static string? NormalizeMessage(string? value, ValidationErrors errors)
    {
        var message = OptionalText(value);
        if (message is null)
        {
            errors.Add("message", "message is required.");
            return null;
        }

        if (message.Length > MaxMessageLength)
        {
            errors.Add("message", $"message must be {MaxMessageLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
            return null;
        }

        return message;
    }

    private static IReadOnlyList<string> NormalizeAllowedTools(
        IReadOnlyList<string>? tools,
        ValidationErrors errors)
    {
        if (tools is null || tools.Count == 0)
        {
            return [];
        }

        if (tools.Count > MaxAllowedToolCount)
        {
            errors.Add("allowedTools", $"allowedTools must contain {MaxAllowedToolCount.ToString(CultureInfo.InvariantCulture)} items or fewer.");
        }

        var normalized = new List<string>();
        foreach (var tool in tools)
        {
            var value = OptionalText(tool)?.ToLowerInvariant();
            if (value is null)
            {
                errors.Add("allowedTools", "allowedTools must not contain empty values.");
                continue;
            }

            if (value.Length > MaxAllowedToolLength)
            {
                errors.Add("allowedTools", $"allowedTools values must be {MaxAllowedToolLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
                continue;
            }

            if (!SupportedTools.Contains(value))
            {
                errors.Add("allowedTools", $"allowedTools contains unsupported tool '{value}'.");
                continue;
            }

            if (!normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private PromptReferenceScope NormalizeReferenceScope(JsonElement? referenceScope, ValidationErrors errors)
    {
        if (!referenceScope.HasValue || referenceScope.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new PromptReferenceScope(null, SensitivePromptRedactor.CleanStatus, []);
        }

        var raw = referenceScope.Value.GetRawText();
        if (raw.Length > 16_384)
        {
            errors.Add("referenceScope", "referenceScope must be 16384 characters or fewer.");
            return new PromptReferenceScope(null, SensitivePromptRedactor.CleanStatus, []);
        }

        var redacted = promptRedactor.Redact(raw);
        return new PromptReferenceScope(
            redacted.RedactedPrompt,
            redacted.RedactionStatus,
            redacted.MatchedCategories);
    }

    private static string BuildSessionTitle(string prompt)
    {
        var normalized = prompt.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= 80)
        {
            return normalized.Length == 0 ? "AI assistant session" : normalized;
        }

        return normalized[..80];
    }

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static JsonElement ParseJsonElement(string? json, string fallback)
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

    private static IReadOnlyList<JsonElement> ParseJsonArray(string? json)
    {
        var element = ParseJsonElement(json, "[]");
        return element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Select(item => item.Clone()).ToArray()
            : [];
    }

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

    private sealed record AiAssistantRequestSnapshot(
        int Version,
        string Prompt,
        IReadOnlyList<string> AllowedTools,
        PromptReferenceScope ReferenceScope,
        IReadOnlyList<string> MatchedRedactionCategories);

    private sealed record PromptReferenceScope(
        string? RedactedJson,
        string RedactionStatus,
        IReadOnlyList<string> MatchedCategories);

}

internal sealed class DeterministicAiContentService : IAiContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<Result<AiContentResponse>> GenerateAsync(
        AiContentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selectedTools = SelectTools(request.Prompt, request.AllowedTools).ToArray();
        var response = BuildResponse(request.Prompt, request.ReferenceData, selectedTools);
        var toolCalls = selectedTools
            .Select(tool => new AiToolCall(
                tool,
                JsonSerializer.SerializeToElement(
                    new
                    {
                        mode = "deterministic_draft",
                        evidence = BuildToolEvidence(tool, request.ReferenceData)
                    },
                    JsonOptions)))
            .ToArray();
        var tokenUsage = JsonSerializer.SerializeToElement(
            new
            {
                inputTokens = EstimateTokens(request.Prompt) + EstimateTokens(request.ReferenceData?.GetRawText()),
                outputTokens = EstimateTokens(response),
                totalTokens = EstimateTokens(request.Prompt) + EstimateTokens(request.ReferenceData?.GetRawText()) + EstimateTokens(response)
            },
            JsonOptions);

        return Task.FromResult(Result<AiContentResponse>.Success(new AiContentResponse(
            response,
            toolCalls,
            tokenUsage)));
    }

    private static IEnumerable<string> SelectTools(string prompt, IReadOnlyList<string>? allowedTools)
    {
        var promptLower = prompt.ToLowerInvariant();
        var inferred = new List<string>();

        AddIfMatches(inferred, promptLower, "keyword-discovery", "keyword", "キーワード", "調査", "search");
        AddIfMatches(inferred, promptLower, "rank-results", "rank", "順位", "ranking");
        AddIfMatches(inferred, promptLower, "brief-generation", "brief", "記事", "構成", "faq", "タイトル");
        AddIfMatches(inferred, promptLower, "rewrite-analysis", "rewrite", "リライト", "差分", "cannibal", "カニバリ");
        AddIfMatches(inferred, promptLower, "report-summary", "report", "レポート", "summary", "要約");
        AddIfMatches(inferred, promptLower, "competitor-analysis", "competitor", "競合");
        AddIfMatches(inferred, promptLower, "content-analysis", "content", "コンテンツ", "見出し");

        if (allowedTools is { Count: > 0 })
        {
            var allowed = allowedTools.ToHashSet(StringComparer.OrdinalIgnoreCase);
            inferred = inferred.Where(allowed.Contains).ToList();
            if (inferred.Count == 0)
            {
                inferred.AddRange(allowedTools.Take(3));
            }
        }

        return inferred.Count == 0 ? ["content-analysis"] : inferred.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddIfMatches(List<string> tools, string prompt, string tool, params string[] needles)
    {
        if (needles.Any(needle => prompt.Contains(needle, StringComparison.OrdinalIgnoreCase)) &&
            !tools.Contains(tool, StringComparer.OrdinalIgnoreCase))
        {
            tools.Add(tool);
        }
    }

    private static string BuildResponse(string prompt, JsonElement? referenceData, IReadOnlyList<string> selectedTools)
    {
        var projectName = TryReadString(referenceData, "project", "name") ?? "the selected project";
        var rewriteCount = TryReadArrayLength(referenceData, "rewriteTasks");
        var cannibalizationCount = TryReadArrayLength(referenceData, "cannibalizationCandidates");
        var reportCount = TryReadArrayLength(referenceData, "reports");
        var briefCount = TryReadArrayLength(referenceData, "briefs");
        var keywordCount = TryReadArrayLength(referenceData, "topKeywords");

        var builder = new StringBuilder();
        builder.Append("Draft AI response for ").Append(projectName).Append(". ");
        builder.Append("Used tools: ").Append(string.Join(", ", selectedTools)).Append(". ");
        builder.Append("Reference data includes ")
            .Append(keywordCount.ToString(CultureInfo.InvariantCulture)).Append(" top keywords, ")
            .Append(rewriteCount.ToString(CultureInfo.InvariantCulture)).Append(" rewrite tasks, ")
            .Append(cannibalizationCount.ToString(CultureInfo.InvariantCulture)).Append(" cannibalization candidates, ")
            .Append(reportCount.ToString(CultureInfo.InvariantCulture)).Append(" reports, and ")
            .Append(briefCount.ToString(CultureInfo.InvariantCulture)).Append(" briefs. ");
        builder.Append("Treat this output as a human-review draft. ");
        builder.Append("Request summary: ").Append(SummarizePrompt(prompt));
        return builder.ToString();
    }

    private static object BuildToolEvidence(string tool, JsonElement? referenceData)
        => new
        {
            tool,
            topKeywordCount = TryReadArrayLength(referenceData, "topKeywords"),
            rewriteTaskCount = TryReadArrayLength(referenceData, "rewriteTasks"),
            cannibalizationCandidateCount = TryReadArrayLength(referenceData, "cannibalizationCandidates"),
            reportCount = TryReadArrayLength(referenceData, "reports"),
            briefCount = TryReadArrayLength(referenceData, "briefs")
        };

    private static string SummarizePrompt(string prompt)
    {
        var normalized = prompt.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    private static string? TryReadString(JsonElement? element, string objectName, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(objectName, out var child) ||
            child.ValueKind != JsonValueKind.Object ||
            !child.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static int TryReadArrayLength(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return property.GetArrayLength();
    }

    private static int EstimateTokens(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(value.Length / 4.0));
}

internal sealed class AiAssistantJob(
    SeoIntelligenceDbContext dbContext,
    AiAssistantService aiAssistantService,
    IJobService jobService,
    IProjectContextService contextService,
    ILogger<AiAssistantJob> logger)
{
    public const string JobType = AiAssistantService.JobType;

    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == jobId);
        if (job is null)
        {
            logger.LogWarning("AI assistant job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(job.WorkspaceId, job.ProjectId);
        if (job.ProjectId is null ||
            !string.Equals(job.ResultResourceType, AiAssistantService.MessageResourceType, StringComparison.Ordinal) ||
            !job.ResultResourceId.HasValue)
        {
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "invalid_job_payload", "AI assistant job payload was missing."));
            return;
        }

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(job.ResultResourceId.Value.ToString("N"), TimeSpan.FromMinutes(10)));
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "AI assistant job {job_id} could not start: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var result = await aiAssistantService.GenerateResponseAsync(context, jobId);
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
                    new JobResultResource(AiAssistantService.MessageResourceType, result.Value!.MessageId)));
            if (!completed.IsSuccess)
            {
                logger.LogWarning(
                    "AI assistant job {job_id} could not be marked succeeded: {message}",
                    jobId,
                    completed.Error?.Message);
            }
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "AI assistant job {job_id} could not persist response.", jobId);
            await aiAssistantService.RecordAiFailureAsync(
                context,
                jobId,
                "AI assistant could not persist response.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("AI assistant could not persist response."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "AI assistant job {job_id} failed unexpectedly.", jobId);
            await aiAssistantService.RecordAiFailureAsync(
                context,
                jobId,
                "AI assistant failed unexpectedly.");
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(JobFailureKind.Unexpected, null, "unexpected", "AI assistant failed unexpectedly."));
        }
    }

    private async Task RecordFailureAsync(ProjectExecutionContext context, Guid jobId, Error error)
    {
        await aiAssistantService.RecordAiFailureAsync(context, jobId, error.Message);
        await jobService.RecordFailureAsync(
            context,
            jobId,
            error.Code switch
            {
                ErrorCode.Conflict or ErrorCode.ValidationFailed or ErrorCode.NotFound
                    => JobFailure.FromHttpStatusCode(400, "ai_assistant", error.Message),
                ErrorCode.ExternalTemporaryFailure or ErrorCode.RateLimited
                    => JobFailure.FromHttpStatusCode(503, "ai_assistant", error.Message),
                _ => new JobFailure(JobFailureKind.Unexpected, null, "ai_assistant", error.Message)
            });
    }
}
