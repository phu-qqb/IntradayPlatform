using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = configuration.GetValue("Worker:PollInterval", TimeSpan.FromMinutes(15));
        if (configuration.GetValue("Worker:ProcessImmediatelyOnStartup", true))
        {
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
        }

        using var timer = new PeriodicTimer(pollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
        }
    }

    private async Task PromoteWeightsIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("ModelWeights:Enabled", true) || !configuration.GetValue("ModelWeights:PromoteReadyBatches", false))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IModelWeightPromotionService>();
        var limit = configuration.GetValue("ModelWeights:PromotionLimit", 10);
        var results = await service.PromoteReadyBatchesAsync(limit, cancellationToken);
        logger.LogInformation("Model weight promotion polling result: Count={Count} Promoted={Promoted}", results.Count, results.Count(x => x.Succeeded));
    }

    private async Task ProcessOnce(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ProcessModelRunService>();
        var result = await service.ProcessNextAsync(cancellationToken);
        logger.LogInformation("Model run polling result: {Message} Processed={Processed} Blocked={Blocked}", result.Message, result.Processed, result.Blocked);
    }

    private async Task BuildBarsIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("MarketDataBars:Enabled", false))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<PlatformState>();
        var builder = scope.ServiceProvider.GetRequiredService<IBarBuilderService>();
        var venueName = configuration.GetValue("MarketDataBars:Venue", "LMAX") ?? "LMAX";
        var venue = state.Venues.Single(x => x.Name == venueName);
        var result = await builder.BuildLatestFifteenMinuteBarsAsync(venue.Id, cancellationToken);
        logger.LogInformation("Market data bar build result: Status={Status} Created={Created} Updated={Updated} Error={Error}", result.Status, result.BarsCreated, result.BarsUpdated, result.ErrorMessage);
    }
}
