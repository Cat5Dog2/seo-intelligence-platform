using SeoIntelligence.Application.Diagnostics;

namespace SeoIntelligence.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = logger.BeginScope(new SeoIntelligenceLogContext().ToScopeDictionary());
        logger.LogInformation("SeoIntelligence worker host started.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
