using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class NotificationService(
    SeoIntelligenceDbContext dbContext,
    NotificationDeliveryJob notificationDeliveryJob,
    INotificationDeliveryScheduler notificationDeliveryScheduler,
    TimeProvider timeProvider)
    : INotificationService
{
    public const string JobFailedEventType = "job_failed";
    public const string CreditLowEventType = "credit_low";
    public const string RankAlertEventType = "rank_alert";
    public const string ReportCompletedEventType = "report_completed";
    public const string TestEventType = "test";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<NotificationDeliveryDetails>> SendTestAsync(
        ProjectExecutionContext context,
        Guid channelId,
        CancellationToken cancellationToken = default)
    {
        var channel = await FindNotificationChannelAsync(context.WorkspaceId, channelId, cancellationToken);
        if (channel is null)
        {
            return Failure<NotificationDeliveryDetails>(ErrorCode.NotFound, "Notification channel was not found.");
        }

        if (!string.Equals(channel.Status, StatusValues.Active, StringComparison.OrdinalIgnoreCase))
        {
            return Failure<NotificationDeliveryDetails>(ErrorCode.Conflict, "Notification channel is disabled.");
        }

        var content = "SEO Intelligence test notification.";
        var delivery = CreateDelivery(
            context,
            channel,
            TestEventType,
            jobId: null,
            resourceType: "notification_channel",
            resourceId: channel.Id.ToString("D"),
            content);

        dbContext.NotificationDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationDeliveryJob.ExecuteAsync(delivery.Id, cancellationToken);
        return await GetDeliveryDetailsAsync(context.WorkspaceId, delivery.Id, cancellationToken);
    }

    public async Task<Result<NotificationResult>> EnqueueAsync(
        ProjectExecutionContext context,
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var eventType = NormalizeEventType(request.EventType);
        if (eventType is null || !IsSupportedEventType(eventType))
        {
            return Result<NotificationResult>.Failure(Error.Validation(
                "Validation failed.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["eventType"] = ["eventType must be job_failed, credit_low, rank_alert, or report_completed."]
                }));
        }

        var channels = await ResolveChannelsAsync(context, request.ChannelId, eventType, cancellationToken);
        if (channels.Count == 0)
        {
            return Result<NotificationResult>.Success(new NotificationResult(null, NotificationDeliveryStatus.Succeeded));
        }

        var content = TrimContent(request.Message);
        var deliveries = channels
            .Select(channel => CreateDelivery(
                context,
                channel,
                eventType,
                request.JobId,
                request.ResourceType,
                request.ResourceId?.ToString("D"),
                content))
            .ToArray();

        dbContext.NotificationDeliveries.AddRange(deliveries);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            if (!await notificationDeliveryScheduler.EnqueueAsync(delivery.Id, cancellationToken))
            {
                await notificationDeliveryJob.ExecuteAsync(delivery.Id, cancellationToken);
            }
        }

        var first = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == deliveries[0].Id, cancellationToken);
        return Result<NotificationResult>.Success(new NotificationResult(first.Id, ToNotificationDeliveryStatus(first.Status)));
    }

    public async Task<Result<NotificationDeliveryDetails>> RetryAsync(
        ProjectExecutionContext context,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var delivery = await dbContext.NotificationDeliveries
            .FirstOrDefaultAsync(
                entity => entity.WorkspaceId == context.WorkspaceId && entity.Id == deliveryId,
                cancellationToken);
        if (delivery is null)
        {
            return Failure<NotificationDeliveryDetails>(ErrorCode.NotFound, "Notification delivery was not found.");
        }

        if (!string.Equals(delivery.Status, StatusValues.Failed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(delivery.Status, StatusValues.Retrying, StringComparison.OrdinalIgnoreCase))
        {
            return Failure<NotificationDeliveryDetails>(ErrorCode.Conflict, "Only failed or retrying notification deliveries can be retried.");
        }

        delivery.Status = StatusValues.Retrying;
        delivery.NextRetryAt = timeProvider.GetUtcNow().UtcDateTime;
        delivery.CorrelationId = context.CorrelationId ?? delivery.CorrelationId;
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationDeliveryJob.ExecuteAsync(delivery.Id, cancellationToken);
        return await GetDeliveryDetailsAsync(context.WorkspaceId, delivery.Id, cancellationToken);
    }

    private async Task<Result<NotificationDeliveryDetails>> GetDeliveryDetailsAsync(
        Guid workspaceId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == deliveryId, cancellationToken);

        return delivery is null
            ? Failure<NotificationDeliveryDetails>(ErrorCode.NotFound, "Notification delivery was not found.")
            : Result<NotificationDeliveryDetails>.Success(MapNotificationDelivery(delivery));
    }

    private async Task<NotificationChannelEntity?> FindNotificationChannelAsync(
        Guid workspaceId,
        Guid channelId,
        CancellationToken cancellationToken)
        => await dbContext.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.WorkspaceId == workspaceId && entity.Id == channelId, cancellationToken);

    private async Task<IReadOnlyList<NotificationChannelEntity>> ResolveChannelsAsync(
        ProjectExecutionContext context,
        Guid? channelId,
        string eventType,
        CancellationToken cancellationToken)
    {
        var source = dbContext.NotificationChannels
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ChannelType == "discord" &&
                entity.Status == StatusValues.Active);

        source = channelId.HasValue
            ? source.Where(entity => entity.Id == channelId.Value)
            : source.Where(entity => entity.ProjectId == null || entity.ProjectId == context.ProjectId);

        var channels = await source.ToArrayAsync(cancellationToken);
        return channels
            .Where(channel => SupportsEventType(channel, eventType))
            .OrderByDescending(channel => channel.ProjectId.HasValue)
            .ThenBy(channel => channel.CreatedAt)
            .ToArray();
    }

    private NotificationDeliveryEntity CreateDelivery(
        ProjectExecutionContext context,
        NotificationChannelEntity channel,
        string eventType,
        Guid? jobId,
        string? resourceType,
        string? resourceId,
        string content)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new NotificationDeliveryEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = channel.ProjectId ?? context.ProjectId,
            ChannelId = channel.Id,
            JobId = jobId,
            ResourceType = OptionalText(resourceType),
            ResourceId = OptionalText(resourceId),
            EventType = eventType,
            PayloadHash = HashText(content),
            Status = StatusValues.Pending,
            RetryCount = 0,
            CorrelationId = context.CorrelationId,
            CreatedAt = now
        };
    }

    private static bool SupportsEventType(NotificationChannelEntity channel, string eventType)
    {
        var values = DeserializeStringArray(channel.EventTypesJson);
        return values.Any(value => string.Equals(value, eventType, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] DeserializeStringArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsSupportedEventType(string eventType)
        => string.Equals(eventType, JobFailedEventType, StringComparison.Ordinal)
            || string.Equals(eventType, CreditLowEventType, StringComparison.Ordinal)
            || string.Equals(eventType, RankAlertEventType, StringComparison.Ordinal)
            || string.Equals(eventType, ReportCompletedEventType, StringComparison.Ordinal);

    private static string? NormalizeEventType(string? value)
        => OptionalText(value)?.ToLowerInvariant();

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimContent(string content)
    {
        var trimmed = string.IsNullOrWhiteSpace(content) ? "SEO Intelligence notification." : content.Trim();
        return trimmed.Length <= 1800 ? trimmed : trimmed[..1800];
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static NotificationDeliveryStatus ToNotificationDeliveryStatus(string status)
        => status switch
        {
            StatusValues.Retrying => NotificationDeliveryStatus.Retrying,
            StatusValues.Succeeded => NotificationDeliveryStatus.Succeeded,
            StatusValues.Failed => NotificationDeliveryStatus.Failed,
            _ => NotificationDeliveryStatus.Pending
        };

    private static NotificationDeliveryDetails MapNotificationDelivery(NotificationDeliveryEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.ChannelId,
            entity.JobId,
            entity.ResourceType,
            entity.ResourceId,
            entity.EventType,
            entity.PayloadHash,
            entity.Status,
            entity.ErrorMessage,
            entity.RetryCount,
            entity.NextRetryAt,
            entity.SentAt,
            entity.DeliveredAt,
            entity.CorrelationId,
            entity.CreatedAt);

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));
}

internal sealed class NotificationDeliveryJob(
    SeoIntelligenceDbContext dbContext,
    ISecretStore secretStore,
    IDiscordWebhookClient discordWebhookClient,
    INotificationDeliveryScheduler notificationDeliveryScheduler,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryJob> logger)
{
    public const int MaxRetryCount = 5;

    public async Task ExecuteAsync(Guid deliveryId)
        => await ExecuteAsync(deliveryId, CancellationToken.None);

    public async Task ExecuteAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await dbContext.NotificationDeliveries
            .FirstOrDefaultAsync(entity => entity.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            logger.LogWarning("Notification delivery {delivery_id} was dequeued but no delivery row was found.", deliveryId);
            return;
        }

        if (string.Equals(delivery.Status, StatusValues.Succeeded, StringComparison.Ordinal))
        {
            return;
        }

        var channel = await dbContext.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.WorkspaceId == delivery.WorkspaceId && entity.Id == delivery.ChannelId,
                cancellationToken);
        if (channel is null)
        {
            await RecordFailureAsync(delivery, "Notification channel was not found.", retryable: false, cancellationToken);
            return;
        }

        if (!string.Equals(channel.Status, StatusValues.Active, StringComparison.OrdinalIgnoreCase))
        {
            await RecordFailureAsync(delivery, "Notification channel is disabled.", retryable: false, cancellationToken);
            return;
        }

        var content = await BuildDiscordContentAsync(delivery, cancellationToken);
        delivery.PayloadHash = HashText(content);
        delivery.SentAt = NowUtc();

        var webhookUrl = await ResolveWebhookUrlAsync(channel, cancellationToken);
        if (!webhookUrl.IsSuccess)
        {
            await RecordFailureAsync(delivery, webhookUrl.ErrorMessage!, retryable: false, cancellationToken);
            return;
        }

        var result = await discordWebhookClient.SendAsync(webhookUrl.Url!, content, cancellationToken);
        if (result.IsSuccess)
        {
            delivery.Status = StatusValues.Succeeded;
            delivery.ErrorMessage = null;
            delivery.NextRetryAt = null;
            delivery.DeliveredAt = NowUtc();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await RecordFailureAsync(delivery, result.ErrorMessage, result.IsRetryable, cancellationToken);
    }

    private async Task<WebhookUrlResolution> ResolveWebhookUrlAsync(
        NotificationChannelEntity channel,
        CancellationToken cancellationToken)
    {
        SecretValue? secret;
        try
        {
            secret = await secretStore.GetAsync(new SecretReference(channel.WebhookSecretRef), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Discord webhook secret {secret_ref} could not be read.", channel.WebhookSecretRef);
            return WebhookUrlResolution.Failure("Discord webhook secret could not be read.");
        }

        if (secret is null)
        {
            return WebhookUrlResolution.Failure("Discord webhook secret was not found.");
        }

        if (!Uri.TryCreate(secret.Value, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            return WebhookUrlResolution.Failure("Discord webhook secret value was not a valid absolute URL.");
        }

        return WebhookUrlResolution.Success(url);
    }

    private async Task<string> BuildDiscordContentAsync(
        NotificationDeliveryEntity delivery,
        CancellationToken cancellationToken)
    {
        var job = delivery.JobId.HasValue
            ? await dbContext.Jobs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity => entity.WorkspaceId == delivery.WorkspaceId && entity.Id == delivery.JobId.Value,
                    cancellationToken)
            : null;

        var message = delivery.EventType switch
        {
            NotificationService.CreditLowEventType => BuildCreditLowContent(delivery, job),
            NotificationService.JobFailedEventType => BuildJobFailedContent(delivery, job),
            NotificationService.RankAlertEventType => await BuildRankAlertContentAsync(delivery, job, cancellationToken),
            NotificationService.ReportCompletedEventType => await BuildReportCompletedContentAsync(delivery, job, cancellationToken),
            NotificationService.TestEventType => "SEO Intelligence test notification.",
            _ => $"SEO Intelligence notification: {delivery.EventType}"
        };

        return message.Length <= 1800 ? message : message[..1800];
    }

    private static string BuildCreditLowContent(NotificationDeliveryEntity delivery, JobEntity? job)
        => string.Join(
            Environment.NewLine,
            NonEmptyLines(
                "[credit_low] Rakko Keyword API credit is insufficient.",
                job is null ? null : $"Job: {job.JobType} ({job.Id:D})",
                job is null ? null : $"Status: {job.Status}",
                ExtractFailureMessage(job),
                BuildResourceLine(delivery)));

    private static string BuildJobFailedContent(NotificationDeliveryEntity delivery, JobEntity? job)
        => string.Join(
            Environment.NewLine,
            NonEmptyLines(
                "[job_failed] SEO Intelligence job failed.",
                job is null ? null : $"Job: {job.JobType} ({job.Id:D})",
                job is null ? null : $"Status: {job.Status}",
                ExtractFailureMessage(job),
                BuildResourceLine(delivery)));

    private async Task<string> BuildRankAlertContentAsync(
        NotificationDeliveryEntity delivery,
        JobEntity? job,
        CancellationToken cancellationToken)
    {
        AlertEventEntity? alertEvent = null;
        KeywordEntity? keyword = null;
        if (string.Equals(delivery.ResourceType, RankMonitoringService.AlertEventResourceType, StringComparison.Ordinal) &&
            Guid.TryParse(delivery.ResourceId, out var alertEventId))
        {
            alertEvent = await dbContext.AlertEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == alertEventId, cancellationToken);
            if (alertEvent?.KeywordId.HasValue == true)
            {
                keyword = await dbContext.Keywords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entity => entity.Id == alertEvent.KeywordId.Value, cancellationToken);
            }
        }

        var current = ParseJson(alertEvent?.CurrentValueJson);
        var previous = ParseJson(alertEvent?.PreviousValueJson);
        return string.Join(
            Environment.NewLine,
            NonEmptyLines(
                "[rank_alert] Rank alert triggered.",
                alertEvent is null ? null : $"Event: {alertEvent.EventType} ({alertEvent.Id:D})",
                keyword is null ? null : $"Keyword: {keyword.NormalizedText}",
                GetJsonString(current, "target") is { } target ? $"Target: {target}" : null,
                GetJsonInt(current, "position") is { } position ? $"Position: {position.ToString(CultureInfo.InvariantCulture)}" : null,
                GetJsonInt(previous, "position") is { } previousPosition ? $"Previous: {previousPosition.ToString(CultureInfo.InvariantCulture)}" : null,
                GetJsonString(current, "rankedUrl") is { } rankedUrl ? $"Ranked URL: {rankedUrl}" : null,
                job is null ? null : $"Job: {job.JobType} ({job.Id:D})",
                BuildResourceLine(delivery)));
    }

    private async Task<string> BuildReportCompletedContentAsync(
        NotificationDeliveryEntity delivery,
        JobEntity? job,
        CancellationToken cancellationToken)
    {
        ReportEntity? report = null;
        if (string.Equals(delivery.ResourceType, AuditLogResourceTypes.Report, StringComparison.Ordinal) &&
            Guid.TryParse(delivery.ResourceId, out var reportId))
        {
            report = await dbContext.Reports
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == reportId, cancellationToken);
        }

        return string.Join(
            Environment.NewLine,
            NonEmptyLines(
                "[report_completed] SEO report generation completed.",
                report is null ? null : $"Report: {report.ReportType} {report.Period} ({report.Format})",
                report?.FileUri is null ? null : $"File: {report.FileUri}",
                job is null ? null : $"Job: {job.JobType} ({job.Id:D})",
                BuildResourceLine(delivery)));
    }

    private static string? ExtractFailureMessage(JobEntity? job)
    {
        if (string.IsNullOrWhiteSpace(job?.ErrorJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(job.ErrorJson);
            return document.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String
                ? $"Message: {message.GetString()}"
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildResourceLine(NotificationDeliveryEntity delivery)
        => string.IsNullOrWhiteSpace(delivery.ResourceType) || string.IsNullOrWhiteSpace(delivery.ResourceId)
            ? null
            : $"Resource: {delivery.ResourceType}/{delivery.ResourceId}";

    private static IEnumerable<string> NonEmptyLines(params string?[] values)
        => values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetJsonString(JsonElement? element, string propertyName)
        => element.HasValue &&
            element.Value.ValueKind == JsonValueKind.Object &&
            element.Value.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static int? GetJsonInt(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private async Task RecordFailureAsync(
        NotificationDeliveryEntity delivery,
        string errorMessage,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var now = NowUtc();
        delivery.RetryCount += 1;
        delivery.ErrorMessage = TrimError(errorMessage);
        delivery.DeliveredAt = null;

        if (retryable && delivery.RetryCount < MaxRetryCount)
        {
            var delay = CalculateBackoff(delivery.RetryCount);
            delivery.Status = StatusValues.Retrying;
            delivery.NextRetryAt = now + delay;
            await dbContext.SaveChangesAsync(cancellationToken);
            SeoIntelligenceDiagnostics.RecordNotificationFailure(delivery.EventType, delivery.Status);
            await notificationDeliveryScheduler.ScheduleRetryAsync(delivery.Id, delay, cancellationToken);
            return;
        }

        delivery.Status = StatusValues.Failed;
        delivery.NextRetryAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        SeoIntelligenceDiagnostics.RecordNotificationFailure(delivery.EventType, delivery.Status);
    }

    private static TimeSpan CalculateBackoff(int retryCount)
    {
        var cappedRetry = Math.Clamp(retryCount, 1, MaxRetryCount);
        var seconds = 30 * Math.Pow(2, cappedRetry - 1);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string TrimError(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "Discord notification failed." : value.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record WebhookUrlResolution(bool IsSuccess, Uri? Url, string? ErrorMessage)
    {
        public static WebhookUrlResolution Success(Uri url) => new(true, url, null);

        public static WebhookUrlResolution Failure(string errorMessage) => new(false, null, errorMessage);
    }
}

internal interface IDiscordWebhookClient
{
    Task<DiscordWebhookSendResult> SendAsync(Uri webhookUrl, string content, CancellationToken cancellationToken = default);
}

internal sealed class DiscordWebhookClient(
    HttpClient httpClient,
    ILogger<DiscordWebhookClient> logger)
    : IDiscordWebhookClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DiscordWebhookSendResult> SendAsync(
        Uri webhookUrl,
        string content,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { content }, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode is >= 200 and <= 299)
            {
                return DiscordWebhookSendResult.Success();
            }

            var statusCode = (int)response.StatusCode;
            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || statusCode >= 500;
            return DiscordWebhookSendResult.Failure(
                retryable,
                $"Discord webhook returned HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DiscordWebhookSendResult.Failure(retryable: true, "Discord webhook timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Discord webhook request failed.");
            return DiscordWebhookSendResult.Failure(retryable: true, "Discord webhook request failed.");
        }
    }
}

internal sealed record DiscordWebhookSendResult(bool IsSuccess, bool IsRetryable, string ErrorMessage)
{
    public static DiscordWebhookSendResult Success() => new(true, false, string.Empty);

    public static DiscordWebhookSendResult Failure(bool retryable, string errorMessage) => new(false, retryable, errorMessage);
}

internal interface INotificationDeliveryScheduler
{
    Task<bool> EnqueueAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(Guid deliveryId, TimeSpan delay, CancellationToken cancellationToken = default);
}

internal sealed class HangfireNotificationDeliveryScheduler(
    IServiceProvider serviceProvider,
    ILogger<HangfireNotificationDeliveryScheduler> logger)
    : INotificationDeliveryScheduler
{
    public Task<bool> EnqueueAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = serviceProvider.GetService<IBackgroundJobClient>();
        if (client is null)
        {
            logger.LogDebug("Hangfire is not configured; notification delivery {delivery_id} will be executed inline.", deliveryId);
            return Task.FromResult(false);
        }

        client.Create(
            Job.FromExpression<NotificationDeliveryJob>(job => job.ExecuteAsync(deliveryId)),
            new EnqueuedState("notifications"));
        return Task.FromResult(true);
    }

    public Task ScheduleRetryAsync(Guid deliveryId, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = serviceProvider.GetService<IBackgroundJobClient>();
        if (client is null)
        {
            logger.LogDebug("Hangfire is not configured; notification delivery retry {delivery_id} was not scheduled.", deliveryId);
            return Task.CompletedTask;
        }

        client.Schedule<NotificationDeliveryJob>(job => job.ExecuteAsync(deliveryId), delay);
        return Task.CompletedTask;
    }
}
