using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Secrets;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Services;

namespace IntegrationTests;

public sealed class ManagementApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProjectAndSiteEndpointsArchiveRestoreAndRejectCrossProjectSiteAccess()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            var projectAId = await CreateProjectAsync(client, "Project A");
            var projectBId = await CreateProjectAsync(client, "Project B");

            using (var archiveResponse = await client.DeleteAsync($"/api/projects/{projectAId}"))
            using (var archiveDocument = await ReadJsonAsync(archiveResponse))
            {
                Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
                Assert.Equal("archived", archiveDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
                Assert.NotEqual(JsonValueKind.Null, archiveDocument.RootElement.GetProperty("data").GetProperty("archivedAt").ValueKind);
            }

            using (var activeProjectsResponse = await client.GetAsync("/api/projects"))
            using (var activeProjectsDocument = await ReadJsonAsync(activeProjectsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, activeProjectsResponse.StatusCode);
                var activeProjectIds = activeProjectsDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("projectId").GetGuid())
                    .ToArray();

                Assert.DoesNotContain(projectAId, activeProjectIds);
                Assert.Contains(projectBId, activeProjectIds);
            }

            using (var restoreResponse = await client.PostAsync($"/api/projects/{projectAId}/restore", content: null))
            using (var restoreDocument = await ReadJsonAsync(restoreResponse))
            {
                Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
                Assert.Equal("active", restoreDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, restoreDocument.RootElement.GetProperty("data").GetProperty("archivedAt").ValueKind);
            }

            var siteId = await CreateSiteAsync(client, projectBId);

            using (var crossProjectResponse = await client.GetAsync($"/api/projects/{projectAId}/sites/{siteId}"))
            using (var crossProjectDocument = await ReadJsonAsync(crossProjectResponse))
            {
                Assert.Equal(HttpStatusCode.NotFound, crossProjectResponse.StatusCode);
                Assert.False(crossProjectDocument.RootElement.GetProperty("result").GetBoolean());
                Assert.Equal("Resource.NotFound", crossProjectDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            using (var archiveSiteResponse = await client.DeleteAsync($"/api/projects/{projectBId}/sites/{siteId}"))
            using (var archiveSiteDocument = await ReadJsonAsync(archiveSiteResponse))
            {
                Assert.Equal(HttpStatusCode.OK, archiveSiteResponse.StatusCode);
                Assert.Equal("archived", archiveSiteDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var activeSitesResponse = await client.GetAsync($"/api/projects/{projectBId}/sites"))
            using (var activeSitesDocument = await ReadJsonAsync(activeSitesResponse))
            {
                Assert.Equal(HttpStatusCode.OK, activeSitesResponse.StatusCode);
                Assert.Empty(activeSitesDocument.RootElement.GetProperty("data").EnumerateArray());
            }

            using (var restoreSiteResponse = await client.PostAsync($"/api/projects/{projectBId}/sites/{siteId}/restore", content: null))
            using (var restoreSiteDocument = await ReadJsonAsync(restoreSiteResponse))
            {
                Assert.Equal(HttpStatusCode.OK, restoreSiteResponse.StatusCode);
                Assert.Equal("active", restoreSiteDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AdminEndpointsManageWorkspaceCredentialsNotificationsAndAuditLists()
    {
        await using var discord = FakeDiscordWebhookServer.Start(HttpStatusCode.NoContent);
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString()
        });
        using var client = CreateClient(factory);

        try
        {
            using (var workspaceResponse = await client.PutAsJsonAsync("/api/admin/workspace", new
            {
                name = "MVP Workspace",
                defaultLocation = "JP",
                defaultLanguage = "ja",
                retentionSettings = new { externalApiRawDataMonths = 24 },
                notificationDefaults = new { discordEnabled = true }
            }))
            using (var workspaceDocument = await ReadJsonAsync(workspaceResponse))
            {
                Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
                Assert.Equal("MVP Workspace", workspaceDocument.RootElement.GetProperty("data").GetProperty("name").GetString());
                Assert.True(workspaceDocument.RootElement.GetProperty("data").GetProperty("notificationDefaults").GetProperty("discordEnabled").GetBoolean());
            }

            Guid credentialId;
            using (var credentialResponse = await client.PostAsJsonAsync("/api/admin/api-credentials", new
            {
                provider = "rakko_keyword",
                keyRef = "rakko-keyword-api-key-dev"
            }))
            using (var credentialDocument = await ReadJsonAsync(credentialResponse))
            {
                Assert.Equal(HttpStatusCode.Created, credentialResponse.StatusCode);
                var credentialData = credentialDocument.RootElement.GetProperty("data");
                credentialId = credentialData.GetProperty("credentialId").GetGuid();
                Assert.Equal("rakko-keyword-api-key-dev", credentialData.GetProperty("keyRef").GetString());
                Assert.False(credentialData.TryGetProperty("secretValue", out _));
            }

            using (var rotateResponse = await client.PostAsJsonAsync($"/api/admin/api-credentials/{credentialId}/rotate", new
            {
                newKeyRef = "rakko-keyword-api-key-rotated"
            }))
            using (var rotateDocument = await ReadJsonAsync(rotateResponse))
            {
                Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
                Assert.Equal("rakko-keyword-api-key-rotated", rotateDocument.RootElement.GetProperty("data").GetProperty("keyRef").GetString());
            }

            using (var disableResponse = await client.DeleteAsync($"/api/admin/api-credentials/{credentialId}"))
            using (var disableDocument = await ReadJsonAsync(disableResponse))
            {
                Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
                Assert.Equal("disabled", disableDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var enableResponse = await client.PostAsync($"/api/admin/api-credentials/{credentialId}/enable", content: null))
            using (var enableDocument = await ReadJsonAsync(enableResponse))
            {
                Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
                Assert.Equal("active", enableDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            Guid channelId;
            using (var channelResponse = await client.PostAsJsonAsync("/api/admin/notification-channels", new
            {
                projectId = (Guid?)null,
                channelType = "discord",
                name = "MVP Alerts",
                webhookSecretRef = "discord-webhook-dev",
                eventTypes = new[] { "job_failed", "credit_low" }
            }))
            using (var channelDocument = await ReadJsonAsync(channelResponse))
            {
                Assert.Equal(HttpStatusCode.Created, channelResponse.StatusCode);
                var channelData = channelDocument.RootElement.GetProperty("data");
                channelId = channelData.GetProperty("channelId").GetGuid();
                Assert.Equal("discord-webhook-dev", channelData.GetProperty("webhookSecretRef").GetString());
                Assert.False(channelData.TryGetProperty("webhookUrl", out _));
            }

            Guid deliveryId;
            using (var testResponse = await client.PostAsync($"/api/admin/notification-channels/{channelId}/test", content: null))
            using (var testDocument = await ReadJsonAsync(testResponse))
            {
                Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
                var deliveryData = testDocument.RootElement.GetProperty("data");
                deliveryId = deliveryData.GetProperty("deliveryId").GetGuid();
                Assert.Equal("succeeded", deliveryData.GetProperty("status").GetString());
                var requestBody = Assert.Single(discord.RequestBodies);
                Assert.Contains("SEO Intelligence test notification", requestBody, StringComparison.Ordinal);
            }

            using (var deliveryResponse = await client.GetAsync($"/api/admin/notification-deliveries/{deliveryId}"))
            using (var deliveryDocument = await ReadJsonAsync(deliveryResponse))
            {
                Assert.Equal(HttpStatusCode.OK, deliveryResponse.StatusCode);
                Assert.Equal("test", deliveryDocument.RootElement.GetProperty("data").GetProperty("eventType").GetString());
            }

            using (var externalCallsResponse = await client.GetAsync("/api/admin/external-api-calls"))
            using (var externalCallsDocument = await ReadJsonAsync(externalCallsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, externalCallsResponse.StatusCode);
                Assert.True(externalCallsDocument.RootElement.GetProperty("result").GetBoolean());
                Assert.Empty(externalCallsDocument.RootElement.GetProperty("data").EnumerateArray());
            }

            using (var auditLogsResponse = await client.GetAsync("/api/admin/audit-logs"))
            using (var auditLogsDocument = await ReadJsonAsync(auditLogsResponse))
            {
                Assert.Equal(HttpStatusCode.OK, auditLogsResponse.StatusCode);
                Assert.True(auditLogsDocument.RootElement.GetProperty("result").GetBoolean());
                var actions = auditLogsDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("action").GetString())
                    .ToArray();

                Assert.Contains(AuditLogActionNames.ApiCredentialCreated, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialRotated, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialDisabled, actions);
                Assert.Contains(AuditLogActionNames.ApiCredentialEnabled, actions);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Operational")]
    public async Task NotificationTestRetryKeepsRetryingThenFailsAfterFiveRetryableWebhookFailures()
    {
        await using var discord = FakeDiscordWebhookServer.Start(
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests);
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString()
        });
        using var client = CreateClient(factory);

        try
        {
            Guid channelId;
            using (var channelResponse = await client.PostAsJsonAsync("/api/admin/notification-channels", new
            {
                projectId = (Guid?)null,
                channelType = "discord",
                name = "Retrying Alerts",
                webhookSecretRef = "discord-webhook-dev",
                eventTypes = new[] { "job_failed", "credit_low" }
            }))
            using (var channelDocument = await ReadJsonAsync(channelResponse))
            {
                Assert.Equal(HttpStatusCode.Created, channelResponse.StatusCode);
                channelId = channelDocument.RootElement.GetProperty("data").GetProperty("channelId").GetGuid();
            }

            Guid deliveryId;
            using (var testResponse = await client.PostAsync($"/api/admin/notification-channels/{channelId}/test", content: null))
            using (var testDocument = await ReadJsonAsync(testResponse))
            {
                Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
                var deliveryData = testDocument.RootElement.GetProperty("data");
                deliveryId = deliveryData.GetProperty("deliveryId").GetGuid();
                Assert.Equal("retrying", deliveryData.GetProperty("status").GetString());
                Assert.Equal(1, deliveryData.GetProperty("retryCount").GetInt32());
                Assert.NotEqual(JsonValueKind.Null, deliveryData.GetProperty("nextRetryAt").ValueKind);
            }

            JsonElement retryData = default;
            for (var attempt = 2; attempt <= 5; attempt++)
            {
                using var retryResponse = await client.PostAsync($"/api/admin/notification-deliveries/{deliveryId}/retry", content: null);
                using var retryDocument = await ReadJsonAsync(retryResponse);

                Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
                retryData = retryDocument.RootElement.GetProperty("data").Clone();
                Assert.Equal(attempt, retryData.GetProperty("retryCount").GetInt32());
            }

            Assert.Equal("failed", retryData.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, retryData.GetProperty("nextRetryAt").ValueKind);
            Assert.Equal(5, discord.RequestBodies.Count);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Operational")]
    public async Task SearchVolume402MockRecordsFatalJobCreditLowNotificationAndAudit()
    {
        await using var discord = FakeDiscordWebhookServer.Start(HttpStatusCode.NoContent);
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString(),
            ["RakkoKeyword:Mode"] = "Mock",
            ["RakkoKeyword:MockStatusCode"] = "402"
        });
        using var client = CreateClient(factory);

        try
        {
            var projectId = await CreateProjectAsync(client, "Credit Alert Project");

            await CreateNotificationChannelAsync(client, projectId, "Credit Alerts", "job_failed", "credit_low");

            var jobId = await RegisterSearchVolumeJobAsync(client, projectId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedFatal, job.Status);

                var delivery = await dbContext.NotificationDeliveries.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal("credit_low", delivery.EventType);
                Assert.Equal(StatusValues.Succeeded, delivery.Status);
                Assert.Equal(projectId, delivery.ProjectId);

                var externalCall = await dbContext.ExternalApiCalls.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal(402, externalCall.StatusCode);

                var auditLog = await dbContext.AuditLogs.AsNoTracking().SingleAsync(entity =>
                    entity.Action == AuditLogActionNames.JobFailed &&
                    entity.ResourceId == jobId.ToString("D"));
                Assert.Equal(AuditLogResourceTypes.Job, auditLog.ResourceType);
            }

            Assert.Single(discord.RequestBodies);
            Assert.Contains("credit_low", discord.RequestBodies[0], StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Operational")]
    public async Task SearchVolume403MockRecordsFatalJobFailureNotificationAndAudit()
    {
        await using var discord = FakeDiscordWebhookServer.Start(HttpStatusCode.NoContent);
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString(),
            ["RakkoKeyword:Mode"] = "Mock",
            ["RakkoKeyword:MockStatusCode"] = "403"
        });
        using var client = CreateClient(factory);

        try
        {
            var projectId = await CreateProjectAsync(client, "Forbidden API Project");
            await CreateNotificationChannelAsync(client, projectId, "Job Failure Alerts", "job_failed", "credit_low");
            var jobId = await RegisterSearchVolumeJobAsync(client, projectId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedFatal, job.Status);
                Assert.Equal(0, job.RetryCount);
                Assert.Null(job.NextRunAt);

                var delivery = await dbContext.NotificationDeliveries.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal("job_failed", delivery.EventType);
                Assert.Equal(StatusValues.Succeeded, delivery.Status);
                Assert.Equal(projectId, delivery.ProjectId);

                var externalCall = await dbContext.ExternalApiCalls.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal(403, externalCall.StatusCode);
                Assert.Equal("forbidden", externalCall.ErrorCode);

                var auditLog = await dbContext.AuditLogs.AsNoTracking().SingleAsync(entity =>
                    entity.Action == AuditLogActionNames.JobFailed &&
                    entity.ResourceId == jobId.ToString("D"));
                Assert.Equal(AuditLogResourceTypes.Job, auditLog.ResourceType);
            }

            Assert.Single(discord.RequestBodies);
            Assert.Contains("job_failed", discord.RequestBodies[0], StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Operational")]
    public async Task SearchVolume429MockRecordsRetryableJobWithBackoffAndAudit()
    {
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["RakkoKeyword:Mode"] = "Mock",
            ["RakkoKeyword:MockStatusCode"] = "429"
        });
        using var client = CreateClient(factory);

        try
        {
            var projectId = await CreateProjectAsync(client, "Rate Limited Project");
            var jobId = await RegisterSearchVolumeJobAsync(client, projectId);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedRetryable, job.Status);
                Assert.Equal(1, job.RetryCount);
                Assert.NotNull(job.NextRunAt);
                Assert.Null(job.CompletedAt);
                Assert.Contains("\"httpStatusCode\":429", job.ErrorJson, StringComparison.Ordinal);
                Assert.Contains("\"retryable\":true", job.ErrorJson, StringComparison.Ordinal);

                var externalCall = await dbContext.ExternalApiCalls.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal(429, externalCall.StatusCode);
                Assert.Equal("rate_limited", externalCall.ErrorCode);

                Assert.True(await dbContext.AuditLogs.AsNoTracking().AnyAsync(entity =>
                    entity.Action == AuditLogActionNames.JobFailed &&
                    entity.ResourceId == jobId.ToString("D")));
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Category", "Operational")]
    [InlineData(500, 3)]
    [InlineData(503, 5)]
    public async Task SearchVolumeRetryableExternalMockBecomesFatalAfterRetryLimit(int statusCode, int maxRetryCount)
    {
        await using var discord = FakeDiscordWebhookServer.Start(HttpStatusCode.NoContent);
        await using var factory = new ManagementApiFactory(new Dictionary<string, string?>
        {
            ["Secrets:discord-webhook-dev"] = discord.Url.ToString(),
            ["RakkoKeyword:Mode"] = "Mock",
            ["RakkoKeyword:MockStatusCode"] = statusCode.ToString()
        });
        using var client = CreateClient(factory);

        try
        {
            var projectId = await CreateProjectAsync(client, $"Retry Limit {statusCode}");
            var jobId = await RegisterSearchVolumeJobAsync(client, projectId);

            await SetQueuedRetryStateAsync(factory, jobId, maxRetryCount - 1);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var retryableJob = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedRetryable, retryableJob.Status);
                Assert.Equal(maxRetryCount, retryableJob.RetryCount);
                Assert.NotNull(retryableJob.NextRunAt);
                Assert.Null(retryableJob.CompletedAt);
            }

            await CreateNotificationChannelAsync(client, projectId, "Final Failure Alerts", "job_failed");
            await SetQueuedRetryStateAsync(factory, jobId, maxRetryCount);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var fatalJob = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                Assert.Equal(StatusValues.FailedFatal, fatalJob.Status);
                Assert.Equal(maxRetryCount + 1, fatalJob.RetryCount);
                Assert.Null(fatalJob.NextRunAt);
                Assert.NotNull(fatalJob.CompletedAt);
                Assert.Contains($"\"httpStatusCode\":{statusCode}", fatalJob.ErrorJson, StringComparison.Ordinal);
                Assert.Contains("\"retryable\":false", fatalJob.ErrorJson, StringComparison.Ordinal);

                var externalCalls = await dbContext.ExternalApiCalls
                    .AsNoTracking()
                    .Where(entity => entity.JobId == jobId)
                    .OrderBy(entity => entity.CreatedAt)
                    .ToArrayAsync();
                Assert.Equal(2, externalCalls.Length);
                Assert.All(externalCalls, call => Assert.Equal(statusCode, call.StatusCode));

                var delivery = await dbContext.NotificationDeliveries.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                Assert.Equal("job_failed", delivery.EventType);
                Assert.Equal(StatusValues.Succeeded, delivery.Status);
            }

            Assert.Single(discord.RequestBodies);
            Assert.Contains("job_failed", discord.RequestBodies[0], StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task ApiCredentialSecretValueIsStoredAsReferenceAndNeverReturnedOrAudited()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            const string inputSecret = "do-not-leak-api-key-value";
            const string correlationId = "corr-secret-create";

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/api-credentials")
            {
                Content = JsonContent.Create(new
                {
                    provider = "rakko_keyword",
                    secretValue = inputSecret
                })
            };
            createRequest.Headers.Add("X-Correlation-Id", correlationId);

            using var createResponse = await client.SendAsync(createRequest);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            using var createDocument = JsonDocument.Parse(createContent);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.True(!createContent.Contains(inputSecret, StringComparison.Ordinal), "Create response contained the input secret.");

            var credentialData = createDocument.RootElement.GetProperty("data");
            var credentialId = credentialData.GetProperty("credentialId").GetGuid();
            var keyRef = credentialData.GetProperty("keyRef").GetString();
            Assert.False(credentialData.TryGetProperty("secretValue", out _));
            Assert.False(string.IsNullOrWhiteSpace(keyRef));
            Assert.StartsWith("api-credential-rakko-keyword-", keyRef);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var credential = await dbContext.ApiCredentials
                    .AsNoTracking()
                    .SingleAsync(entity => entity.Id == credentialId);

                Assert.Equal(keyRef, credential.KeyRef);
                Assert.True(!string.Equals(inputSecret, credential.KeyRef, StringComparison.Ordinal), "DB key_ref contained the input secret.");

                var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
                var storedSecret = await secretStore.GetAsync(new SecretReference(keyRef!));
                Assert.NotNull(storedSecret);
                Assert.True(string.Equals(inputSecret, storedSecret!.Value, StringComparison.Ordinal), "Secret Store did not return the original input secret.");

                var auditPayloads = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(entity => entity.CorrelationId == correlationId)
                    .Select(entity => entity.BeforeAfterJson)
                    .ToArrayAsync();

                Assert.NotEmpty(auditPayloads);
                Assert.All(auditPayloads, payload =>
                    Assert.True(!payload.Contains(inputSecret, StringComparison.Ordinal), "Audit log contained the input secret."));
            }

            using (var auditResponse = await client.GetAsync($"/api/admin/audit-logs?resourceType=api_credential&resourceId={credentialId}&correlation_id={correlationId}&actor=developer&from=2000-01-01T00:00:00Z&to=2999-01-01T00:00:00Z"))
            {
                var auditContent = await auditResponse.Content.ReadAsStringAsync();
                using var auditDocument = JsonDocument.Parse(auditContent);

                Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
                Assert.True(!auditContent.Contains(inputSecret, StringComparison.Ordinal), "Audit response contained the input secret.");
                var auditItem = Assert.Single(auditDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal(AuditLogActionNames.ApiCredentialCreated, auditItem.GetProperty("action").GetString());
                Assert.Equal(correlationId, auditItem.GetProperty("correlationId").GetString());
            }

            const string rejectedSecret = "rejected-do-not-leak";
            using var rejectedResponse = await client.PostAsJsonAsync("/api/admin/api-credentials", new
            {
                provider = "rakko_keyword",
                keyRef = "existing-ref",
                secretValue = rejectedSecret
            });
            var rejectedContent = await rejectedResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, rejectedResponse.StatusCode);
            Assert.True(!rejectedContent.Contains(rejectedSecret, StringComparison.Ordinal), "Validation response contained the rejected secret.");
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AuditLogSearchFiltersActorResourceCorrelationAndPeriod()
    {
        await using var factory = new ManagementApiFactory();
        using var client = CreateClient(factory);

        try
        {
            const string correlationId = "corr-audit-filter";
            var externalCallId = Guid.NewGuid().ToString("D");
            var csvExportId = Guid.NewGuid().ToString("D");
            var jobId = Guid.NewGuid().ToString("D");

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var contextService = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
                var context = contextService.Create(SeoIntelligenceSeedData.DefaultWorkspaceId, correlationId: correlationId);

                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.ExternalApiExecuted,
                        AuditLogResourceTypes.ExternalApiCall,
                        externalCallId,
                        new { after = new { provider = "rakko_keyword", endpoint = "/v1/suggest" } }));
                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.CsvExportCreated,
                        AuditLogResourceTypes.CsvExport,
                        csvExportId,
                        new { after = new { format = "csv", status = "queued" } }));
                await auditWriter.RecordAsync(
                    context,
                    new AuditLogWriteRequest(
                        AuditLogActionNames.JobCanceled,
                        AuditLogResourceTypes.Job,
                        jobId,
                        new { before = new { status = "waiting_external" }, after = new { status = "canceled" } }));
            }

            using (var correlationResponse = await client.GetAsync($"/api/admin/audit-logs?actor=developer&correlation_id={correlationId}&from=2000-01-01T00:00:00Z&to=2999-01-01T00:00:00Z&pageSize=10"))
            using (var correlationDocument = await ReadJsonAsync(correlationResponse))
            {
                Assert.Equal(HttpStatusCode.OK, correlationResponse.StatusCode);
                var actions = correlationDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("action").GetString())
                    .ToArray();

                Assert.Contains(AuditLogActionNames.ExternalApiExecuted, actions);
                Assert.Contains(AuditLogActionNames.CsvExportCreated, actions);
                Assert.Contains(AuditLogActionNames.JobCanceled, actions);
            }

            using (var csvResponse = await client.GetAsync($"/api/admin/audit-logs?resourceType=csv_export&resourceId={csvExportId}&correlationId={correlationId}"))
            using (var csvDocument = await ReadJsonAsync(csvResponse))
            {
                Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
                var item = Assert.Single(csvDocument.RootElement.GetProperty("data").EnumerateArray());
                Assert.Equal(AuditLogActionNames.CsvExportCreated, item.GetProperty("action").GetString());
                Assert.Equal(AuditLogResourceTypes.CsvExport, item.GetProperty("resourceType").GetString());
                Assert.Equal(csvExportId, item.GetProperty("resourceId").GetString());
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name,
            defaultLocation = "JP",
            defaultLanguage = "ja",
            kpi = new { organicSessions = 1000 },
            memo = "integration test"
        });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("active", document.RootElement.GetProperty("data").GetProperty("status").GetString());
        return document.RootElement.GetProperty("data").GetProperty("projectId").GetGuid();
    }

    private static async Task CreateNotificationChannelAsync(
        HttpClient client,
        Guid projectId,
        string name,
        params string[] eventTypes)
    {
        using var channelResponse = await client.PostAsJsonAsync("/api/admin/notification-channels", new
        {
            projectId,
            channelType = "discord",
            name,
            webhookSecretRef = "discord-webhook-dev",
            eventTypes
        });
        using var channelDocument = await ReadJsonAsync(channelResponse);

        Assert.Equal(HttpStatusCode.Created, channelResponse.StatusCode);
        Assert.Equal(projectId, channelDocument.RootElement.GetProperty("data").GetProperty("projectId").GetGuid());
    }

    private static async Task<Guid> RegisterSearchVolumeJobAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/search-volume/jobs",
            new
            {
                keywords = new[] { "seo" },
                location = "JP",
                language = "ja",
                aggregationPeriodMonths = 12,
                seoDifficulty = true
            });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
    }

    private static async Task SetQueuedRetryStateAsync(ManagementApiFactory factory, Guid jobId, int retryCount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var job = await dbContext.Jobs.SingleAsync(entity => entity.Id == jobId);
        var now = DateTime.UtcNow;
        job.Status = StatusValues.Queued;
        job.RetryCount = retryCount;
        job.NextRunAt = now;
        job.ErrorJson = null;
        job.CompletedAt = null;
        job.UpdatedAt = now;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> CreateSiteAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/sites", new
        {
            domain = " HTTPS://WWW.Example.COM:443/topics?x=1 ",
            canonicalUrl = "WWW.Example.COM:443/topics?x=1#section",
            type = "own",
            memo = "owned site"
        });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("www.example.com", data.GetProperty("domain").GetString());
        Assert.Equal("https://www.example.com/topics?x=1", data.GetProperty("canonicalUrl").GetString());
        return data.GetProperty("siteId").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static HttpClient CreateClient(ManagementApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-management-api-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class ManagementApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");
        private readonly IReadOnlyDictionary<string, string?> _additionalConfiguration;

        public string StoragePath { get; } = CreateTempStoragePath();

        public ManagementApiFactory(IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
        {
            _additionalConfiguration = additionalConfiguration ?? new Dictionary<string, string?>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "",
                    ["Redis:ConnectionString"] = "",
                    ["Storage:Provider"] = "Local",
                    ["Storage:BasePath"] = StoragePath,
                    ["Storage:BucketName"] = "seo-intelligence",
                    ["SecretStore:Provider"] = "Configuration",
                    ["SecretStore:ConfigurationPrefix"] = "Secrets",
                    ["Hangfire:Storage"] = "PostgreSQL",
                    ["OpenTelemetry:ServiceName"] = "IntegrationTests"
                };

                foreach (var pair in _additionalConfiguration)
                {
                    settings[pair.Key] = pair.Value;
                }

                configuration.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                if (_additionalConfiguration.TryGetValue("RakkoKeyword:MockStatusCode", out var mockStatusCode) &&
                    int.TryParse(mockStatusCode, out var parsedMockStatusCode))
                {
                    services.Configure<RakkoKeywordOptions>(options => options.MockStatusCode = parsedMockStatusCode);
                }

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class FakeDiscordWebhookServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Queue<HttpStatusCode> _responses;
        private readonly Task _acceptLoop;

        private FakeDiscordWebhookServer(TcpListener listener, Queue<HttpStatusCode> responses)
        {
            _listener = listener;
            _responses = responses;
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            Url = new Uri($"http://127.0.0.1:{endpoint.Port}/discord");
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public Uri Url { get; }

        public List<string> RequestBodies { get; } = [];

        public static FakeDiscordWebhookServer Start(params HttpStatusCode[] responses)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new FakeDiscordWebhookServer(
                listener,
                new Queue<HttpStatusCode>(responses.Length == 0 ? [HttpStatusCode.NoContent] : responses));
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();

            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _cancellation.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                await HandleClientAsync(client, _cancellation.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            await using var stream = client.GetStream();
            var buffer = new byte[8192];
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var bodyStart = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (bodyStart >= 0)
            {
                RequestBodies.Add(request[(bodyStart + 4)..]);
            }

            var status = _responses.Count > 0 ? _responses.Dequeue() : HttpStatusCode.NoContent;
            var reason = status == HttpStatusCode.NoContent ? "No Content" : "Too Many Requests";
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {reason}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
            await stream.WriteAsync(response, cancellationToken);
        }
    }
}
