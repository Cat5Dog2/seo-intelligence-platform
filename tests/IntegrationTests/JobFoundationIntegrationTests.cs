using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.Redis;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class JobFoundationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task JobRegistrationSuppressesDuplicateIdempotencyKeyAndRejectsHashMismatch()
    {
        await using var factory = new JobApiFactory();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IJobService>();
            var context = CreateContext(scope.ServiceProvider);

            using var payload = JsonDocument.Parse("""{"keywords":["seo"],"location":"JP"}""");
            var first = await service.RegisterAsync(
                context,
                new JobRegistrationRequest(
                    "SearchVolumeJob",
                    payload.RootElement,
                    IdempotencyKey: "idem-search-volume-1"));
            var second = await service.RegisterAsync(
                context,
                new JobRegistrationRequest(
                    "SearchVolumeJob",
                    payload.RootElement,
                    IdempotencyKey: "idem-search-volume-1"));

            using var changedPayload = JsonDocument.Parse("""{"keywords":["seo","content"],"location":"JP"}""");
            var conflict = await service.RegisterAsync(
                context,
                new JobRegistrationRequest(
                    "SearchVolumeJob",
                    changedPayload.RootElement,
                    IdempotencyKey: "idem-search-volume-1"));

            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            var jobs = await dbContext.Jobs.AsNoTracking().ToArrayAsync();
            var auditActions = await dbContext.AuditLogs
                .AsNoTracking()
                .Select(entity => entity.Action)
                .ToArrayAsync();

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Value!.JobId, second.Value!.JobId);
            Assert.Single(jobs);
            Assert.Equal(StatusValues.Queued, jobs[0].Status);
            Assert.False(string.IsNullOrWhiteSpace(jobs[0].RequestHash));
            Assert.True(conflict.IsFailure);
            Assert.Equal(ErrorCode.Conflict, conflict.Error!.Code);
            Assert.Equal([AuditLogActionNames.JobQueued], auditActions);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task JobEndpointsCancelWaitingExternalAndRetryRetryableJobWithAudit()
    {
        await using var factory = new JobApiFactory();
        using var client = CreateClient(factory);

        try
        {
            Guid waitingJobId;
            Guid retryableJobId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IJobService>();
                var context = CreateContext(scope.ServiceProvider);

                waitingJobId = (await service.RegisterAsync(
                    context,
                    new JobRegistrationRequest("PollSearchVolumeStatusJob", TargetKey: "request-1"))).Value!.JobId;
                retryableJobId = (await service.RegisterAsync(
                    context,
                    new JobRegistrationRequest("SearchVolumeJob", TargetKey: "keywords-a"))).Value!.JobId;

                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var waitingJob = await dbContext.Jobs.SingleAsync(entity => entity.Id == waitingJobId);
                waitingJob.Status = StatusValues.WaitingExternal;
                waitingJob.NextRunAt = null;
                waitingJob.UpdatedAt = DateTime.UtcNow;
                dbContext.JobExternalRequests.Add(new JobExternalRequestEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = waitingJobId,
                    Endpoint = "/v1/search-volume/request-1/status",
                    ExternalRequestId = "request-1",
                    SequenceNo = 1,
                    Status = StatusValues.WaitingExternal,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                var retryableJob = await dbContext.Jobs.SingleAsync(entity => entity.Id == retryableJobId);
                retryableJob.Status = StatusValues.FailedRetryable;
                retryableJob.ErrorJson = """{"message":"rate limited"}""";
                retryableJob.NextRunAt = DateTime.UtcNow.AddMinutes(5);
                retryableJob.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            using (var cancelResponse = await client.PostAsync($"/api/jobs/{waitingJobId}/cancel", content: null))
            using (var cancelDocument = await ReadJsonAsync(cancelResponse))
            {
                Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
                Assert.Equal(StatusValues.Canceled, cancelDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
                Assert.Equal("request-1", cancelDocument.RootElement.GetProperty("data").GetProperty("externalRequestId").GetString());
                Assert.Equal(waitingJobId, cancelDocument.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());
            }

            using (var detailResponse = await client.GetAsync($"/api/jobs/{waitingJobId}"))
            using (var detailDocument = await ReadJsonAsync(detailResponse))
            {
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
                Assert.Equal(StatusValues.Canceled, detailDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            using (var retryResponse = await client.PostAsync($"/api/jobs/{retryableJobId}/retry", content: null))
            using (var retryDocument = await ReadJsonAsync(retryResponse))
            {
                Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
                var data = retryDocument.RootElement.GetProperty("data");
                Assert.Equal(StatusValues.Queued, data.GetProperty("status").GetString());
                Assert.Equal(1, data.GetProperty("retryCount").GetInt32());
                Assert.Equal(JsonValueKind.Null, data.GetProperty("error").ValueKind);
            }

            using (var rejectedRetryResponse = await client.PostAsync($"/api/jobs/{waitingJobId}/retry", content: null))
            using (var rejectedRetryDocument = await ReadJsonAsync(rejectedRetryResponse))
            {
                Assert.Equal(HttpStatusCode.Conflict, rejectedRetryResponse.StatusCode);
                Assert.Equal("Resource.Conflict", rejectedRetryDocument.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == waitingJobId);
                var auditActions = await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(entity => entity.ResourceType == AuditLogResourceTypes.Job)
                    .Select(entity => entity.Action)
                    .ToArrayAsync();

                Assert.Equal(StatusValues.Canceled, externalRequest.Status);
                Assert.Contains(AuditLogActionNames.JobCanceled, auditActions);
                Assert.Contains(AuditLogActionNames.JobRetried, auditActions);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RecordFailureClassifiesRetryableAndFatalFailures()
    {
        await using var factory = new JobApiFactory();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IJobService>();
            var context = CreateContext(scope.ServiceProvider);

            var retryableJob = await RegisterAndStartAsync(service, context, "SearchVolumeJob", "retryable-target");
            var fatalJob = await RegisterAndStartAsync(service, context, "SearchVolumeJob", "fatal-target");
            var timeoutJob = await RegisterAndStartAsync(service, context, "PollSearchVolumeStatusJob", "timeout-target");
            var databaseTransientJob = await RegisterAndStartAsync(service, context, "DataExportJob", "db-transient-target");

            var retryable = await service.RecordFailureAsync(context, retryableJob, JobFailure.FromHttpStatusCode(429));
            var fatal = await service.RecordFailureAsync(context, fatalJob, JobFailure.FromHttpStatusCode(402));
            var timeout = await service.RecordFailureAsync(context, timeoutJob, JobFailure.Timeout());
            var databaseTransient = await service.RecordFailureAsync(context, databaseTransientJob, JobFailure.DatabaseTransient());

            Assert.True(retryable.IsSuccess);
            Assert.Equal(StatusValues.FailedRetryable, retryable.Value!.Status);
            Assert.Equal(1, retryable.Value.RetryCount);
            Assert.NotNull(retryable.Value.NextRunAt);
            Assert.Equal(JsonValueKind.True, retryable.Value.Error!.Value.GetProperty("retryable").ValueKind);

            Assert.True(fatal.IsSuccess);
            Assert.Equal(StatusValues.FailedFatal, fatal.Value!.Status);
            Assert.NotNull(fatal.Value.CompletedAt);
            Assert.Null(fatal.Value.NextRunAt);

            Assert.True(timeout.IsSuccess);
            Assert.Equal(StatusValues.FailedRetryable, timeout.Value!.Status);
            Assert.NotNull(timeout.Value.NextRunAt);

            Assert.True(databaseTransient.IsSuccess);
            Assert.Equal(StatusValues.FailedRetryable, databaseTransient.Value!.Status);
            Assert.NotNull(databaseTransient.Value.NextRunAt);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RedisLockPreventsDuplicateExecutionForSameProjectJobTypeAndTarget()
    {
        await using var factory = new JobApiFactory(useRedis: true);

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IJobService>();
            var context = CreateContext(scope.ServiceProvider);

            var firstJob = (await service.RegisterAsync(
                context,
                new JobRegistrationRequest("SearchVolumeJob", TargetKey: "same-target"))).Value!.JobId;
            var secondJob = (await service.RegisterAsync(
                context,
                new JobRegistrationRequest("SearchVolumeJob", TargetKey: "same-target"))).Value!.JobId;

            var firstStart = await service.TryStartAsync(
                context,
                firstJob,
                new JobExecutionStartRequest(TargetKey: "same-target"));
            var secondStart = await service.TryStartAsync(
                context,
                secondJob,
                new JobExecutionStartRequest(TargetKey: "same-target"));

            Assert.True(firstStart.IsSuccess);
            Assert.True(secondStart.IsFailure);
            Assert.Equal(ErrorCode.Conflict, secondStart.Error!.Code);

            await firstStart.Value!.DisposeAsync();
            var retrySecondStart = await service.TryStartAsync(
                context,
                secondJob,
                new JobExecutionStartRequest(TargetKey: "same-target"));

            Assert.True(retrySecondStart.IsSuccess);
            await retrySecondStart.Value!.DisposeAsync();
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task<Guid> RegisterAndStartAsync(
        IJobService service,
        ProjectContext context,
        string jobType,
        string targetKey)
    {
        var jobId = (await service.RegisterAsync(
            context,
            new JobRegistrationRequest(jobType, TargetKey: targetKey))).Value!.JobId;
        var start = await service.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(TargetKey: targetKey));

        Assert.True(start.IsSuccess);
        await start.Value!.DisposeAsync();
        return jobId;
    }

    private static ProjectContext CreateContext(IServiceProvider serviceProvider)
        => serviceProvider
            .GetRequiredService<IProjectContextService>()
            .Create(SeoIntelligenceSeedData.DefaultWorkspaceId, correlationId: "corr-job-foundation");

    private static HttpClient CreateClient(JobApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-job-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class JobApiFactory(bool useRedis = false) : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public string StoragePath { get; } = CreateTempStoragePath();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
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
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                if (useRedis)
                {
                    services.AddSingleton<IRedisCoordinator, InMemoryRedisCoordinator>();
                }

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class InMemoryRedisCoordinator : IRedisCoordinator
    {
        private readonly Dictionary<string, string> locks = new(StringComparer.Ordinal);
        private readonly object sync = new();

        public Task<TimeSpan> PingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(TimeSpan.Zero);

        public Task SetStringAsync(RedisKey key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetStringAsync(RedisKey key, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<bool> RemoveAsync(RedisKey key, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IRedisLease?> TryAcquireLockAsync(
            RedisKey key,
            string owner,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                if (locks.ContainsKey(key.Value))
                {
                    return Task.FromResult<IRedisLease?>(null);
                }

                locks[key.Value] = owner;
                return Task.FromResult<IRedisLease?>(new InMemoryRedisLease(this, key, owner));
            }
        }

        private void Release(RedisKey key, string owner)
        {
            lock (sync)
            {
                if (locks.TryGetValue(key.Value, out var currentOwner) &&
                    string.Equals(currentOwner, owner, StringComparison.Ordinal))
                {
                    locks.Remove(key.Value);
                }
            }
        }

        private sealed class InMemoryRedisLease(
            InMemoryRedisCoordinator coordinator,
            RedisKey key,
            string owner)
            : IRedisLease
        {
            public RedisKey Key { get; } = key;

            public string Owner { get; } = owner;

            public ValueTask DisposeAsync()
            {
                coordinator.Release(Key, Owner);
                return ValueTask.CompletedTask;
            }
        }
    }
}
