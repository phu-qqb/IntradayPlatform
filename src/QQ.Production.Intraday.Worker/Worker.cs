using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<Worker> logger,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = configuration.GetValue("Worker:PollInterval", TimeSpan.FromMinutes(15));
        var lmaxDemoCycleEnabled = configuration.GetValue("LmaxDemoCycle:Enabled", false);
        if (configuration.GetValue("Worker:ProcessImmediatelyOnStartup", true))
        {
            if (lmaxDemoCycleEnabled)
            {
                await RunLmaxDemoCycleAsync(stoppingToken);
                applicationLifetime.StopApplication();
                return;
            }

            await IngestLmaxCanonicalSnapshotsIfEnabled(stoppingToken);
            await IngestLegacyAnubisPortfolioIfEnabled(stoppingToken);
            await IngestLegacyAnubisWeightsIfEnabled(stoppingToken);
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
            await RunIntradaySchedulerIfEnabled(stoppingToken);
        }

        using var timer = new PeriodicTimer(pollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await IngestLmaxCanonicalSnapshotsIfEnabled(stoppingToken);
            await IngestLegacyAnubisPortfolioIfEnabled(stoppingToken);
            await IngestLegacyAnubisWeightsIfEnabled(stoppingToken);
            await PromoteWeightsIfEnabled(stoppingToken);
            await ProcessOnce(stoppingToken);
            await BuildBarsIfEnabled(stoppingToken);
            await RunLocalSchedulerIfEnabled(stoppingToken);
            await RunIntradaySchedulerIfEnabled(stoppingToken);
        }
    }

    private async Task RunLmaxDemoCycleAsync(CancellationToken cancellationToken)
    {
        var manifestPath = configuration["LmaxDemoCycle:ManifestPath"]
            ?? throw new InvalidOperationException("LMAX_DEMO_CYCLE_MANIFEST_PATH_REQUIRED");
        var manifestFullPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(manifestFullPath))
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_MANIFEST_NOT_FOUND");

        var manifestBytes = await File.ReadAllBytesAsync(manifestFullPath, cancellationToken);
        var manifestSha256 = Convert.ToHexString(SHA256.HashData(manifestBytes));
        var resultPath = manifestFullPath + ".result.json";
        if (File.Exists(resultPath))
        {
            var prior = await File.ReadAllTextAsync(resultPath, cancellationToken);
            if (prior.Contains(manifestSha256, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("LMAX Demo cycle manifest was already finalized: ManifestPath={ManifestPath} ManifestSha256={ManifestSha256}",
                    manifestFullPath, manifestSha256);
                return;
            }

            throw new InvalidOperationException("LMAX_DEMO_CYCLE_RESULT_MANIFEST_MISMATCH");
        }

        LmaxDemoCycleManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<LmaxDemoCycleManifest>(manifestBytes, JsonOptions)
                ?? throw new InvalidOperationException("LMAX_DEMO_CYCLE_MANIFEST_INVALID");
            ValidateManifest(manifest);

            using var scope = scopeFactory.CreateScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<ILmaxDemoCycleCoordinator>();
            var result = await coordinator.RunAsync(ToRequest(manifest), cancellationToken);
            await WriteCycleResultAsync(resultPath, manifestSha256, Required(manifest.CycleId, "CycleId"), "Completed", result, null, cancellationToken);
            logger.LogInformation(
                "LMAX Demo cycle completed: CycleId={CycleId} BatchId={BatchId} ModelRunId={ModelRunId} ProcessingStatus={ProcessingStatus}",
                result.CycleId,
                result.PortfolioWeights.Batch.Id.Value,
                result.Promotion?.ModelRunId?.Value,
                result.Processing?.Status);
        }
        catch (Exception exception)
        {
            var cycleId = manifestBytes.Length == 0 ? "unknown" : TryReadCycleId(manifestBytes);
            await WriteCycleResultAsync(resultPath, manifestSha256, cycleId, "Failed", null, exception.GetType().Name + ":" + exception.Message, cancellationToken);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static LmaxDemoCycleCoordinatorRequest ToRequest(LmaxDemoCycleManifest manifest)
        => new(
            Required(manifest.CycleId, "CycleId"),
            new LmaxCanonicalSnapshotIngestionRequest(
                Required(manifest.CaptureRunRoot, "CaptureRunRoot"),
                Required(manifest.ExpectedFinalManifestSha256, "ExpectedFinalManifestSha256"),
                manifest.DecisionAtUtc,
                TimeSpan.FromSeconds(manifest.MaximumSourceAgeSeconds)),
            new LegacyAnubisPortfolioWeightIngestionRequest(
                RequiredProgrammes(manifest.Programmes).Select(ToContribution).ToArray(),
                Required(manifest.FundCode, "FundCode"),
                Required(manifest.ModelName, "ModelName"),
                manifest.DecisionAtUtc,
                manifest.EffectiveAtUtc,
                manifest.NavUsd,
                manifest.TargetQuantityMode));

    private static LegacyAnubisProgrammeContribution ToContribution(LmaxDemoCycleProgrammeManifest programme)
        => new(
            Required(programme.ProgramName, "ProgramName"),
            programme.UniverseId,
            programme.ModelId,
            Required(programme.Session, "Session"),
            programme.FrequencyMinutes,
            programme.Coefficient,
            programme.State,
            programme.AsOfUtc,
            programme.ExecDeskWeightFilePath,
            programme.ExpectedExecDeskWeightFileSha256,
            programme.AggregatedWeightsFilePath,
            programme.ExpectedAggregatedWeightsFileSha256,
            programme.Reason);

    private static void ValidateManifest(LmaxDemoCycleManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, "lmax-demo-cycle-v1", StringComparison.Ordinal))
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_SCHEMA_VERSION_INVALID");
        if (string.IsNullOrWhiteSpace(manifest.CycleId))
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_ID_REQUIRED");
        if (manifest.DecisionAtUtc.Offset != TimeSpan.Zero || manifest.EffectiveAtUtc.Offset != TimeSpan.Zero ||
            manifest.EffectiveAtUtc < manifest.DecisionAtUtc)
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_TIMESTAMPS_UTC_REQUIRED");
        if (manifest.MaximumSourceAgeSeconds <= 0 || manifest.NavUsd <= 0)
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_NUMERICAL_CONFIGURATION_INVALID");
        RequiredProgrammes(manifest.Programmes);
    }

    private static async Task WriteCycleResultAsync(
        string resultPath,
        string manifestSha256,
        string cycleId,
        string status,
        LmaxDemoCycleCoordinatorResult? result,
        string? error,
        CancellationToken cancellationToken)
    {
        var output = new
        {
            schemaVersion = "lmax-demo-cycle-result-v1",
            cycleId,
            status,
            manifestSha256,
            completedAtUtc = DateTimeOffset.UtcNow,
            batchId = result?.PortfolioWeights.Batch.Id.Value,
            modelRunId = result?.Promotion?.ModelRunId?.Value,
            validationSucceeded = result?.Validation.Succeeded,
            processingStatus = result?.Processing?.Status.ToString(),
            processingBlockedReason = result?.Processing?.BlockedReason?.ToString(),
            processingMessage = result?.Processing?.Message,
            orderCount = result?.Processing?.OrderCount,
            executionReportCount = result?.Processing?.ExecutionReportCount,
            fillCount = result?.Processing?.FillCount,
            reconciliationBreakCount = result?.Processing?.ReconciliationBreakCount,
            error
        };
        var tempPath = resultPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(output, JsonOptions), Encoding.UTF8, cancellationToken);
        File.Move(tempPath, resultPath, overwrite: false);
    }

    private static string Required(string? value, string name)
        => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"LMAX_DEMO_CYCLE_{name.ToUpperInvariant()}_REQUIRED");

    private static IReadOnlyList<LmaxDemoCycleProgrammeManifest> RequiredProgrammes(
        IReadOnlyList<LmaxDemoCycleProgrammeManifest>? programmes)
    {
        var required = new[] { "INFX7", "INFX8", "INFX9", "INFX10" };
        if (programmes is null || programmes.Count != required.Length ||
            required.Any(name => programmes.Count(programme =>
                string.Equals(programme.ProgramName, name, StringComparison.Ordinal)) != 1))
            throw new InvalidOperationException("LMAX_DEMO_CYCLE_PROGRAMME_SET_INVALID");

        return programmes;
    }

    private static string TryReadCycleId(byte[] manifestBytes)
    {
        try
        {
            return JsonSerializer.Deserialize<LmaxDemoCycleManifest>(manifestBytes, JsonOptions)?.CycleId ?? "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    private sealed record LmaxDemoCycleManifest(
        string? SchemaVersion,
        string? CycleId,
        string? CaptureRunRoot,
        string? ExpectedFinalManifestSha256,
        DateTimeOffset DecisionAtUtc,
        DateTimeOffset EffectiveAtUtc,
        int MaximumSourceAgeSeconds,
        string? FundCode,
        string? ModelName,
        decimal NavUsd,
        TargetQuantityMode TargetQuantityMode,
        IReadOnlyList<LmaxDemoCycleProgrammeManifest>? Programmes);

    private sealed record LmaxDemoCycleProgrammeManifest(
        string? ProgramName,
        int UniverseId,
        int ModelId,
        string? Session,
        int FrequencyMinutes,
        decimal Coefficient,
        LegacyAnubisProgrammeContributionState State,
        DateTimeOffset? AsOfUtc,
        string? ExecDeskWeightFilePath,
        string? ExpectedExecDeskWeightFileSha256,
        string? AggregatedWeightsFilePath,
        string? ExpectedAggregatedWeightsFileSha256,
        string? Reason);

    private async Task IngestLmaxCanonicalSnapshotsIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("LmaxCanonicalSnapshotIngestion:Enabled", false))
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

        var maximumSourceAgeSeconds = configuration.GetValue("LmaxCanonicalSnapshotIngestion:MaximumSourceAgeSeconds", 0);
        if (maximumSourceAgeSeconds <= 0)
            throw new InvalidOperationException("LMAX_CANONICAL_SNAPSHOT_INGESTION_MAXIMUM_SOURCE_AGE_SECONDS_POSITIVE_REQUIRED");

        var request = new LmaxCanonicalSnapshotIngestionRequest(
            Required(configuration, "LmaxCanonicalSnapshotIngestion:CaptureRunRoot"),
            Required(configuration, "LmaxCanonicalSnapshotIngestion:ExpectedFinalManifestSha256"),
            RequiredUtc(configuration, "LmaxCanonicalSnapshotIngestion:DecisionAtUtc"),
            TimeSpan.FromSeconds(maximumSourceAgeSeconds));

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILmaxCanonicalSnapshotIngestionService>();
        var result = await service.IngestAsync(request, cancellationToken);
        logger.LogInformation(
            "Canonical LMAX snapshot handoff: RecorderRunId={RecorderRunId} Imported={Imported} AlreadyPersisted={AlreadyPersisted} Symbols={Symbols} FinalManifestSha256={FinalManifestSha256}",
            result.RecorderRunId, result.ImportedSnapshotCount, result.AlreadyPersistedSnapshotCount,
            string.Join(',', result.Symbols), result.FinalManifestSha256);
    }

    private async Task IngestLegacyAnubisPortfolioIfEnabled(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("LegacyAnubisPortfolio:Enabled", false))
            return;
        if (configuration.GetValue("LegacyAnubisWeights:Enabled", false))
            throw new InvalidOperationException("LEGACY_ANUBIS_SINGLE_AND_PORTFOLIO_IMPORTERS_CANNOT_BOTH_BE_ENABLED");

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

        LegacyAnubisProgrammeContribution Contribution(
            string name, int universe, int model, string session, int frequency, decimal coefficient)
        {
            var root = $"LegacyAnubisPortfolio:Programmes:{name}";
            var stateText = Required(configuration, $"{root}:State");
            if (!Enum.TryParse<LegacyAnubisProgrammeContributionState>(stateText, true, out var state))
                throw new InvalidOperationException($"{root.Replace(':', '_').ToUpperInvariant()}_STATE_INVALID");
            var reason = configuration[$"{root}:Reason"];
            return state == LegacyAnubisProgrammeContributionState.Present
                ? new(name, universe, model, session, frequency, coefficient, state,
                    RequiredUtc(configuration, $"{root}:AsOfUtc"),
                    Required(configuration, $"{root}:ExecDeskWeightFilePath"),
                    Required(configuration, $"{root}:ExpectedExecDeskWeightFileSha256"),
                    Required(configuration, $"{root}:AggregatedWeightsFilePath"),
                    Required(configuration, $"{root}:ExpectedAggregatedWeightsFileSha256"),
                    reason)
                : new(name, universe, model, session, frequency, coefficient, state, Reason: reason);
        }

        var request = new LegacyAnubisPortfolioWeightIngestionRequest(
            [
                Contribution("INFX7", 54, 10, "US", 15, 4.5m),
                Contribution("INFX8", 57, 11, "US", 30, 2.1m),
                Contribution("INFX9", 58, 12, "EU", 15, 1.4m),
                Contribution("INFX10", 59, 13, "EU", 60, 0.6m)
            ],
            configuration.GetValue("LegacyAnubisPortfolio:FundCode", "QQ Intraday Fund")!,
            configuration.GetValue("LegacyAnubisPortfolio:ModelName", "IntradayFxPortfolio")!,
            RequiredUtc(configuration, "LegacyAnubisPortfolio:DecisionAtUtc"),
            RequiredUtc(configuration, "LegacyAnubisPortfolio:EffectiveAtUtc"),
            configuration.GetValue("LegacyAnubisPortfolio:NavUsd", 1_000_000m),
            configuration.GetValue("LegacyAnubisPortfolio:TargetQuantityMode", TargetQuantityMode.PortfolioBaseCurrencyNotional));

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILegacyAnubisPortfolioWeightIngestionService>();
        var result = await service.IngestAsync(request, cancellationToken);
        logger.LogInformation(
            "Legacy Anubis four-programme portfolio batch: BatchId={BatchId} Present={Present} Absent={Absent} SourceRows={SourceRows} ExecutableRows={ExecutableRows} AlreadyExisted={AlreadyExisted} PortfolioLineageSha256={PortfolioLineageSha256}",
            result.Batch.Id.Value, result.PresentProgrammeCount, result.AbsentProgrammeCount, result.SourceRowCount,
            result.ExecutableRowCount, result.AlreadyExisted, result.PortfolioLineageSha256);
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
