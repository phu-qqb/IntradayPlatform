using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesIntradayCycleFixtureTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Valid_qubes_fifteen_minute_fixture_runs_full_no_external_cycle()
    {
        var result = await CreateCycleAsync();

        Assert.True(result.IsNoExternalFixture);
        Assert.Equal(15, result.CycleCadenceMinutes);
        Assert.True(result.CycleStatus is QubesIntradayCycleStatus.CompletedNoExternal or QubesIntradayCycleStatus.CompletedWithMissingMarks);
        Assert.True(result.QubesWeights.Succeeded);
        Assert.NotEmpty(result.ReconciliationComparator.ReconciliationLines);
    }

    [Fact]
    public async Task Qubes_run_id_is_preserved_end_to_end()
    {
        var result = await CreateCycleAsync("qubes-r006-lineage");

        Assert.Equal("qubes-r006-lineage", result.QubesRunId.Value);
        Assert.Equal(result.QubesRunId, result.QubesWeights.QubesRunId);
        Assert.Equal(result.QubesRunId, result.TheoreticalPortfolioDiff.QubesRunId);
        Assert.Equal(result.QubesRunId, result.TheoreticalPnl.QubesRunId);
        Assert.Equal("qubes-r006-lineage", result.Persistence.AuditBatch.QubesRunId);
        Assert.Equal(result.QubesRunId, result.ReconciliationComparator.QubesRunId);
    }

    [Fact]
    public async Task Raw_and_normalized_rows_persist_through_r004b_lineage()
    {
        var result = await CreateCycleAsync();

        Assert.NotEmpty(result.Persistence.RawRows);
        Assert.NotEmpty(result.Persistence.NormalizedRows);
        Assert.Equal(result.QubesWeights.RawInputRowCount, result.Persistence.RawRows.Count);
        Assert.Equal(result.QubesWeights.NormalizedOutputRowCount, result.Persistence.NormalizedRows.Count);
    }

    [Fact]
    public async Task Modelweightbatch_modelrun_targetweight_linkage_is_present()
    {
        var result = await CreateCycleAsync();

        Assert.NotNull(result.Persistence.AuditBatch.ModelWeightBatchId);
        Assert.NotNull(result.Persistence.AuditBatch.PromotedModelRunId);
        Assert.Equal(result.ModelWeightBatch.Id, result.Persistence.AuditBatch.ModelWeightBatchId);
        Assert.Equal(result.ModelWeightPromotion.ModelRunId, result.Persistence.AuditBatch.PromotedModelRunId);
        Assert.Contains(result.Persistence.NormalizedRows, x => x.TargetWeightInstrumentId is not null);
    }

    [Fact]
    public async Task Theoretical_target_portfolio_is_produced()
    {
        var result = await CreateCycleAsync();

        Assert.Equal(PortfolioStateSource.Theoretical, result.TheoreticalPortfolioDiff.TargetPortfolioSnapshot.StateSource);
        Assert.Equal(13, result.TheoreticalPortfolioDiff.TargetPortfolioSnapshot.Positions.Count);
    }

    [Fact]
    public async Task Theoretical_pnl_fixture_is_produced()
    {
        var result = await CreateCycleAsync();

        Assert.Equal(PnLSource.Theoretical, result.TheoreticalPnl.TheoreticalPnLSnapshot.Source);
        Assert.Equal(6804.66m, Math.Round(result.TheoreticalPnl.TheoreticalPnLSnapshot.PortfolioPnL.UnrealizedPnL, 2));
        Assert.Equal(PnLComputationStatus.MissingMark, result.TheoreticalPnl.TheoreticalPnLSnapshot.Status);
    }

    [Fact]
    public async Task Target_vs_actual_reconciliation_is_produced()
    {
        var result = await CreateCycleAsync();

        Assert.NotEmpty(result.ReconciliationComparator.ReconciliationLines);
        Assert.Contains(result.ReconciliationComparator.ReconciliationLines, x => x.Symbol == "EURUSD" && x.Status == QubesReconciliationLineStatus.Drift);
        Assert.Contains(result.ReconciliationComparator.ReconciliationLines, x => x.Symbol == "JPYUSD" && x.Status == QubesReconciliationLineStatus.MissingActual);
    }

    [Fact]
    public async Task Theoretical_vs_real_report_is_produced()
    {
        var result = await CreateCycleAsync();

        Assert.Equal(TheoreticalVsRealStatus.Drift, result.ReconciliationComparator.FoundationTheoreticalVsRealReport.Status);
        Assert.NotEmpty(result.ReconciliationComparator.ComparatorLines);
        Assert.Contains(result.ReconciliationComparator.ComparatorLines, x => x.Symbol == "AUDUSD" && x.Status == QubesReconciliationLineStatus.InSync);
    }

    [Fact]
    public async Task Non_executable_rebalance_intents_are_produced()
    {
        var result = await CreateCycleAsync();

        Assert.NotEmpty(result.TheoreticalPortfolioDiff.RebalanceIntents);
        Assert.True(result.RebalanceIntentsRemainNonExecutable);
        Assert.DoesNotContain(result.TheoreticalPortfolioDiff.RebalanceIntents, x => x.IsExecutable);
        Assert.All(result.TheoreticalPortfolioDiff.RebalanceIntents, x => Assert.Contains(IntentStatus.BlockedNoOMS, x.IntentStatuses));
    }

    [Fact]
    public async Task Cycle_status_preserves_missing_marks()
    {
        var result = await CreateCycleAsync();

        Assert.Equal(QubesIntradayCycleStatus.CompletedWithMissingMarks, result.CycleStatus);
        Assert.Equal(PnLComputationStatus.MissingMark, result.Status.PnLStatus);
        Assert.Equal("CompletedWithBreaks", result.Status.ReconciliationStatus);
        Assert.Equal("NonExecutable", result.Status.RebalanceIntentStatus);
    }

    [Fact]
    public async Task Missing_and_stale_mark_status_is_preserved_not_hidden()
    {
        var result = await CreateCycleAsync();

        Assert.Contains(result.TheoreticalPnl.InstrumentDetails, x => x.Symbol == "JPYUSD" && x.PnLStatus == PnLComputationStatus.MissingMark);
        Assert.Contains(result.TheoreticalPnl.InstrumentDetails, x => x.Symbol == "NOKUSD" && x.PnLStatus == PnLComputationStatus.StaleMark);
        Assert.Contains(result.ReconciliationComparator.ReconciliationLines, x => x.Symbol == "NOKUSD" && x.Status == QubesReconciliationLineStatus.MissingMark);
    }

    [Fact]
    public async Task Actual_portfolio_remains_fixture_not_broker_reported_live_state()
    {
        var result = await CreateCycleAsync();

        Assert.Equal(PortfolioStateSource.Simulated, result.ReconciliationComparator.ActualPortfolioFixture.StateSource);
        Assert.False(result.ReconciliationComparator.ActualFixtureIsBrokerReportedLiveState);
        Assert.True(result.ReconciliationComparator.UsedNoExternalActualPortfolioFixture);
    }

    [Fact]
    public async Task No_executable_order_is_created()
    {
        var result = await CreateCycleAsync();

        Assert.False(result.CreatedExecutableOrder);
        Assert.False(result.SubmittedOrders);
        Assert.True(result.RebalanceIntentsRemainNonExecutable);
    }

    [Fact]
    public void No_order_submission_or_broker_runtime_path_is_introduced()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleFixture.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("FixSession", source);
    }

    [Fact]
    public void No_service_or_background_execution_is_introduced()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleFixture.cs"));

        Assert.DoesNotContain("AddHostedService", source);
        Assert.DoesNotContain("IHostedService", source);
        Assert.DoesNotContain("BackgroundService", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("Task.Delay", source);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/Program.cs"));
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram);
        Assert.DoesNotContain("RealLmaxGateway", apiProgram + workerProgram);
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var result = await CreateCycleAsync();
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.TheoreticalPortfolioDiff.DiffLines, x => x.Symbol == "AUDUSD");
        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
    }

    [Fact]
    public async Task Duplicate_qubes_run_id_behavior_remains_idempotent()
    {
        var services = CreateServices();
        var first = await RunCycleAsync(services, "qubes-r006-duplicate");
        var second = await RunCycleAsync(services, "qubes-r006-duplicate", "cycle-r006-duplicate-b");

        Assert.True(first.Persistence.Persisted);
        Assert.True(second.Persistence.AlreadyPersisted);
        Assert.Equal(first.Persistence.AuditBatch.Id, second.Persistence.AuditBatch.Id);
        Assert.Equal(first.Persistence.RawRows.Count, second.Persistence.RawRows.Count);
        Assert.Equal(first.Persistence.NormalizedRows.Count, second.Persistence.NormalizedRows.Count);
    }

    private static async Task<QubesIntradayCycleFixtureResult> CreateCycleAsync(string runId = "qubes-r006-sample")
    {
        var services = CreateServices();
        return await RunCycleAsync(services, runId);
    }

    private static async Task<QubesIntradayCycleFixtureResult> RunCycleAsync(TestServices services, string runId, string cycleRunId = "cycle-r006-sample")
        => await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            cycleRunId,
            new QubesRunId(runId),
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines(),
            services.InstrumentIdsBySymbol,
            ProducedAt,
            EffectiveAt,
            0.0001m,
            100m,
            10m,
            TimeSpan.FromMinutes(20),
            1),
            CancellationToken.None);

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r006-reference"),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines())).NormalizedWeights;
        EnsureInstruments(state, normalized.Select(x => x.Symbol));
        var idsBySymbol = normalized.ToDictionary(
            x => x.Symbol,
            x => state.Instruments.Single(instrument => instrument.Symbol.Equals(x.Symbol, StringComparison.OrdinalIgnoreCase)).Id,
            StringComparer.OrdinalIgnoreCase);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var auditRepository = new InMemoryQubesWeightAuditRepository();
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        var cycle = new QubesIntradayCycleFixtureService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(auditRepository, clock));

        return new TestServices(state, idsBySymbol, cycle);
    }

    private static void EnsureInstruments(PlatformState state, IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (state.Instruments.Any(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            state.Instruments.Add(new Instrument(
                InstrumentId.New(),
                symbol,
                AssetClass.FxSpot,
                new Currency(symbol[..3]),
                Currency.Usd,
                5,
                1));
        }
    }

    private static IReadOnlyList<string> SampleLines()
        => File.ReadAllLines(Path.Combine(RepoRoot(), "tests/fixtures/qubes-fx/qubes-fx-weights-r002-sample.csv"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle);
}
