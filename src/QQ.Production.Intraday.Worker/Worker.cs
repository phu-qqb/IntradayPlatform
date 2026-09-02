using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = configuration.GetValue("Worker:PollInterval", TimeSpan.FromMinutes(15));
        if (configuration.GetValue("Worker:ProcessImmediatelyOnStartup", true))
        {
            await IngestLegacyAnubisWeightsIfEnabled(stoppingToken);
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
            await RunIntradaySchedulerIfEnabled(stoppingToken);
        }

        using var timer = new PeriodicTimer(pollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await IngestLegacyAnubisWeightsIfEnabled(stoppingToken);
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
            await RunLocalSchedulerIfEnabled(stoppingToken);
            await RunIntradaySchedulerIfEnabled(stoppingToken);
        }
    }

    private async Task IngestLegacyAnubisWeightsIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("LegacyAnubisWeights:Enabled", false))
            return;

        static string Required(IConfiguration configuration, string key)
            => configuration[key] ?? throw new InvalidOperationException($"{key.Replace(':', '_').ToUpperInvariant()}_REQUIRED");
        static DateTimeOffset RequiredUtc(IConfiguration configuration, string key)
        {
            var value = Required(configuration, key);
            if (!DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed) || parsed.Offset != TimeSpan.Zero)
                throw new InvalidOperationException($"{key.Replace(':', '_').ToUpperInvariant()}_UTC_REQUIRED");
            return parsed;
        }

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILegacyAnubisWeightIngestionService>();
        var result = await service.IngestAsync(new LegacyAnubisWeightIngestionRequest(
            Required(configuration, "LegacyAnubisWeights:ProgramName"),
            Required(configuration, "LegacyAnubisWeights:ExecDeskWeightFilePath"),
            Required(configuration, "LegacyAnubisWeights:ExpectedExecDeskWeightFileSha256"),
            Required(configuration, "LegacyAnubisWeights:AggregatedWeightsFilePath"),
            Required(configuration, "LegacyAnubisWeights:ExpectedAggregatedWeightsFileSha256"),
            configuration.GetValue("LegacyAnubisWeights:FundCode", "QQ Intraday Fund")!,
            configuration.GetValue("LegacyAnubisWeights:ModelName", "IntradayFxModel")!,
            RequiredUtc(configuration, "LegacyAnubisWeights:AsOfUtc"),
            RequiredUtc(configuration, "LegacyAnubisWeights:EffectiveAtUtc"),
            configuration.GetValue("LegacyAnubisWeights:FrequencyMinutes", 15),
            configuration.GetValue("LegacyAnubisWeights:NavUsd", 1_000_000m),
            configuration.GetValue("LegacyAnubisWeights:TargetQuantityMode", TargetQuantityMode.PortfolioBaseCurrencyNotional)),
            cancellationToken);
        logger.LogInformation("Legacy Anubis manager batch: BatchId={BatchId} SourceRows={SourceRows} ExecutableRows={ExecutableRows} AlreadyExisted={AlreadyExisted} ExecDeskSha256={ExecDeskSha256} AggregatedWeightsSha256={AggregatedWeightsSha256}",
            result.Batch.Id.Value, result.SourceRowCount, result.ExecutableRowCount, result.AlreadyExisted,
            result.ExecDeskWeightFileSha256, result.AggregatedWeightsFileSha256);
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

    private async Task RunLocalSchedulerIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("LocalScheduler:Enabled", false))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IOperationalRunbookRunner>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var schedules = await runner.GetSchedulesAsync(cancellationToken);
        foreach (var schedule in schedules.Where(x => x.IsEnabled && x.NextRunAtUtc is not null && x.NextRunAtUtc <= now))
        {
            var definition = await runner.GetRunbookDefinitionAsync(schedule.RunbookDefinitionId, cancellationToken);
            if (definition is null || !definition.IsEnabled)
            {
                continue;
            }

            await runner.RunRunbookAsync(new RunOperationalRunbookRequest(definition.RunbookType, $"Local scheduler triggered schedule '{schedule.Name}'.", TriggerType: OperationalRunbookTriggerType.LocalScheduler), cancellationToken);
            logger.LogInformation("Local scheduler triggered runbook {RunbookType} from schedule {ScheduleName}", definition.RunbookType, schedule.Name);
        }
    }

    private async Task RunIntradaySchedulerIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Intraday15m:Enabled", false))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<PmsShadowIntradayScheduler>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var coordinatorId = configuration.GetValue<string>("Intraday15m:CoordinatorId")
            ?? $"worker-{Environment.MachineName}";
        var tick = await scheduler.RunClosedSlotAsync(clock.UtcNow, coordinatorId, cancellationToken);
        logger.LogInformation("Intraday 15m slot result: SlotId={SlotId} Claim={Claim} Final={Final}",
            tick.Slot.SlotId, tick.ClaimResult, tick.FinalStatus);
    }
}
