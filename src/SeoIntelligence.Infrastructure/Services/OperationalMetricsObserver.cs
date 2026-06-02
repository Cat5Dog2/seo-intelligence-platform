using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class OperationalMetricsObserver : IHostedService
{
    private static readonly string[] OutcomeStatuses =
    [
        StatusValues.Succeeded,
        StatusValues.FailedRetryable,
        StatusValues.FailedFatal,
        StatusValues.Canceled
    ];

    private static readonly string[] QueueDepthStatuses =
    [
        StatusValues.Queued,
        StatusValues.WaitingExternal
    ];

    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<OperationalMetricsObserver> logger;
    private readonly ObservableGauge<double> jobSuccessRate;
    private readonly ObservableGauge<long> jobQueueDepth;

    public OperationalMetricsObserver(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<OperationalMetricsObserver> logger)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
        jobSuccessRate = SeoIntelligenceDiagnostics.CreateJobSuccessRateGauge(ObserveJobSuccessRate);
        jobQueueDepth = SeoIntelligenceDiagnostics.CreateJobQueueDepthGauge(ObserveJobQueueDepth);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = jobSuccessRate;
        _ = jobQueueDepth;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private IEnumerable<Measurement<double>> ObserveJobSuccessRate()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<SeoIntelligenceDbContext>();
            if (dbContext is null)
            {
                return [];
            }

            var from = timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
            var outcomeJobCount = dbContext.Jobs
                .AsNoTracking()
                .Count(entity =>
                    entity.CompletedAt >= from &&
                    OutcomeStatuses.Contains(entity.Status));
            if (outcomeJobCount == 0)
            {
                return [new Measurement<double>(100)];
            }

            var succeededJobCount = dbContext.Jobs
                .AsNoTracking()
                .Count(entity =>
                    entity.CompletedAt >= from &&
                    entity.Status == StatusValues.Succeeded);

            var successRate = succeededJobCount * 100d / outcomeJobCount;
            return [new Measurement<double>(successRate)];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Operational job success rate metric could not be observed.");
            return [];
        }
    }

    private IEnumerable<Measurement<long>> ObserveJobQueueDepth()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<SeoIntelligenceDbContext>();
            if (dbContext is null)
            {
                return [];
            }

            var queueDepth = dbContext.Jobs
                .AsNoTracking()
                .LongCount(entity => QueueDepthStatuses.Contains(entity.Status));

            return [new Measurement<long>(queueDepth)];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Operational job queue depth metric could not be observed.");
            return [];
        }
    }
}
