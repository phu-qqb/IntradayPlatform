using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesSecondCyclePaperBaselineTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 15, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private static readonly DateTimeOffset CycleStartedAt = ProducedAt;
    private static readonly DateTimeOffset CycleCompletedAt = EffectiveAt;
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R025_paper_ledger_baseline_can_feed_second_no_external_cycle()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(PaperBaselineSecondCycleStatus.CompletedNoExternal, result.CycleStatus);
        Assert.True(result.Summary.UsedR025PaperLedgerBaseline);
        Assert.True(result.IsNoExternalFixture);
    }

    [Fact]
    public async Task Second_cycle_preserves_new_qubes_run_id_and_15_minute_cadence()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal("cycle-r026-second-paper-baseline", result.SecondCycleRunId);
        Assert.Equal("qubes-r026-second-cycle", result.QubesRunId.Value);
        Assert.Equal(15, result.CycleCadenceMinutes);
        Assert.Equal(15, result.QubesWeights.CadenceMinutes);
    }

    [Fact]
    public async Task Raw_and_normalized_qubes_lineage_persists_for_second_cycle()
    {
        var result = await RunSecondCycleAsync();

        Assert.True(result.QubesPersistence.Persisted);
        Assert.Equal(3, result.QubesPersistence.RawRows.Count);
        Assert.Equal(3, result.QubesPersistence.NormalizedRows.Count);
        Assert.Equal("qubes-r026-second-cycle", result.QubesPersistence.AuditBatch.QubesRunId);
        Assert.NotNull(result.QubesPersistence.AuditBatch.ModelWeightBatchId);
        Assert.NotNull(result.QubesPersistence.AuditBatch.PromotedModelRunId);
    }

    [Fact]
    public async Task Current_portfolio_baseline_uses_r025_paper_ledger_state()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(PortfolioStateSource.Simulated, result.CurrentPaperBaseline.StateSource);
        Assert.Equal(3, result.CurrentPaperBaselineLines.Count);
        Assert.All(result.CurrentPaperBaselineLines, line => Assert.True(line.FromR025PaperLedgerFixture));
        Assert.All(result.CurrentPaperBaselineLines, line => Assert.False(line.FromBrokerState));
        Assert.All(result.CurrentPaperBaselineLines, line => Assert.False(line.FromProductionLedgerState));
    }

    [Fact]
    public async Task Current_baseline_is_not_flat_zero()
    {
        var result = await RunSecondCycleAsync();

        Assert.False(result.Summary.CurrentPaperBaselineIsFlatZero);
        Assert.Contains(result.CurrentPaperBaseline.Positions, x => x.Quantity != 0m);
    }

    [Fact]
    public async Task Audusd_current_quantity_is_131000_aud()
    {
        var result = await RunSecondCycleAsync();

        Assert.Contains(result.CurrentPaperBaselineLines, x => x.Symbol == "AUDUSD" && x.CurrentPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Eurusd_current_quantity_is_124000_eur()
    {
        var result = await RunSecondCycleAsync();

        Assert.Contains(result.CurrentPaperBaselineLines, x => x.Symbol == "EURUSD" && x.CurrentPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Gbpusd_current_quantity_is_negative_368000_gbp()
    {
        var result = await RunSecondCycleAsync();

        Assert.Contains(result.CurrentPaperBaselineLines, x => x.Symbol == "GBPUSD" && x.CurrentPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Target_vs_current_deltas_are_relative_to_paper_baseline()
    {
        var result = await RunSecondCycleAsync();

        var audusd = result.TargetVsCurrentDiffLines.Single(x => x.Symbol == "AUDUSD");
        var eurusd = result.TargetVsCurrentDiffLines.Single(x => x.Symbol == "EURUSD");
        var gbpusd = result.TargetVsCurrentDiffLines.Single(x => x.Symbol == "GBPUSD");

        Assert.Equal(0.132310m, audusd.CurrentWeight);
        Assert.Equal(0.140000m, audusd.TargetWeight);
        Assert.Equal(7690m, audusd.DeltaNotional);
        Assert.Equal(0.137764m, eurusd.CurrentWeight);
        Assert.Equal(0.160000m, eurusd.TargetWeight);
        Assert.Equal(22236m, eurusd.DeltaNotional);
        Assert.Equal(-0.473616m, gbpusd.CurrentWeight);
        Assert.Equal(-0.240000m, gbpusd.TargetWeight);
        Assert.Equal(233616m, gbpusd.DeltaNotional);
    }

    [Fact]
    public async Task Second_cycle_theoretical_portfolio_is_produced()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(3, result.TargetPortfolioLines.Count);
        Assert.Contains(result.TargetPortfolioLines, x => x.Symbol == "AUDUSD" && x.TargetNotional == 140000m);
        Assert.Contains(result.TargetPortfolioLines, x => x.Symbol == "EURUSD" && x.TargetNotional == 160000m);
        Assert.Contains(result.TargetPortfolioLines, x => x.Symbol == "GBPUSD" && x.TargetNotional == -240000m);
    }

    [Fact]
    public async Task Second_cycle_theoretical_pnl_is_produced()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(PnLComputationStatus.Computed, result.TheoreticalPnl.TheoreticalPnLSnapshot.Status);
        Assert.Equal(3, result.TheoreticalPnlLines.Count);
        Assert.All(result.TheoreticalPnlLines, line => Assert.Equal(MarkAvailabilityStatus.Available, line.MarkAvailabilityStatus));
    }

    [Fact]
    public async Task Second_cycle_reconciliation_is_produced()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(3, result.ReconciliationLines.Count);
        Assert.True(result.Reconciliation.HasDrift);
        Assert.All(result.ReconciliationLines, line => Assert.Equal(QubesReconciliationLineStatus.Drift, line.Status));
    }

    [Fact]
    public async Task Second_cycle_theoretical_vs_real_report_is_produced()
    {
        var result = await RunSecondCycleAsync();

        Assert.Equal(3, result.TheoreticalVsRealLines.Count);
        Assert.Contains(result.TheoreticalVsRealLines, x => x.Symbol == "AUDUSD");
        Assert.Contains(result.TheoreticalVsRealLines, x => x.Symbol == "EURUSD");
        Assert.Contains(result.TheoreticalVsRealLines, x => x.Symbol == "GBPUSD");
    }

    [Fact]
    public async Task Second_cycle_rebalance_intents_remain_non_executable()
    {
        var result = await RunSecondCycleAsync();

        Assert.True(result.RebalanceIntentsRemainNonExecutable);
        Assert.Equal(3, result.RebalanceIntents.Count);
        Assert.All(result.RebalanceIntents, intent =>
        {
            Assert.False(intent.IsExecutable);
            Assert.Contains(IntentStatus.TheoreticalOnly, intent.IntentStatuses);
            Assert.Contains(IntentStatus.NotExecutable, intent.IntentStatuses);
            Assert.Contains(IntentStatus.BlockedNoOMS, intent.IntentStatuses);
        });
    }

    [Fact]
    public async Task Missing_stale_mark_handling_is_preserved()
    {
        var result = await RunSecondCycleAsync();

        Assert.True(result.TheoreticalPnl.UsedNoExternalMarkFixture);
        Assert.False(result.TheoreticalPnl.UsedLiveBrokerMarketData);
        Assert.DoesNotContain(result.TheoreticalPnlLines, x => x.PnLStatus is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark);
    }

    [Fact]
    public async Task R025_paper_ledger_baseline_is_not_mutated()
    {
        var result = await RunSecondCycleAsync();

        Assert.False(result.MutatedR025PaperBaseline);
        Assert.False(result.PaperBaselineInput.R025PaperBaselineMutated);
        Assert.Contains(result.PaperBaselineInput.Lines, x => x.CurrencyOrSymbol == "AUDUSD" && x.CurrentPaperQuantity == 131000m);
    }

    [Fact]
    public async Task No_new_paper_ledger_commit_occurs()
    {
        var result = await RunSecondCycleAsync();

        Assert.False(result.MutatedPaperLedgerState);
        Assert.False(result.Summary.PaperLedgerStateCommittedOrMutated);
    }

    [Fact]
    public async Task No_live_broker_production_or_trading_state_mutation_occurs()
    {
        var result = await RunSecondCycleAsync();

        Assert.False(result.MutatedLivePositionState);
        Assert.False(result.MutatedBrokerPositionState);
        Assert.False(result.MutatedProductionLedgerState);
        Assert.False(result.MutatedLiveTradingState);
        Assert.False(result.Summary.LivePositionMutationCount != 0);
        Assert.Equal(0, result.Summary.BrokerPositionMutationCount);
        Assert.Equal(0, result.Summary.ProductionLedgerMutationCount);
        Assert.Equal(0, result.Summary.TradingStateMutationCount);
    }

    [Fact]
    public async Task No_orders_fills_execution_reports_routes_or_submissions_are_created()
    {
        var result = await RunSecondCycleAsync();

        Assert.False(result.CreatedExecutableOrder);
        Assert.False(result.CreatedOrderState);
        Assert.False(result.CreatedFill);
        Assert.False(result.CreatedExecutionReport);
        Assert.False(result.CreatedBrokerRoute);
        Assert.False(result.SubmittedOrders);
        Assert.Equal(0, result.Summary.OrderCount);
        Assert.Equal(0, result.Summary.FillCount);
        Assert.Equal(0, result.Summary.ExecutionReportCount);
        Assert.Equal(0, result.Summary.BrokerRouteCount);
    }

    [Fact]
    public async Task Duplicate_second_cycle_result_is_idempotent()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        var baseline = CreateBaselineReference(archive);

        var first = await RunSecondCycleAsync(services, archive, baseline);
        var second = await RunSecondCycleAsync(services, archive, baseline);

        Assert.Same(first, second);
        Assert.Equal(first.SecondCycleRunId, second.SecondCycleRunId);
    }

    [Fact]
    public void Second_cycle_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendOrderAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitOrder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Second_cycle_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

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

    private static async Task<PaperBaselineSecondCycleResult> RunSecondCycleAsync()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        return await RunSecondCycleAsync(services, archive, CreateBaselineReference(archive));
    }

    private static async Task<PaperBaselineSecondCycleResult> RunSecondCycleAsync(
        TestServices services,
        PaperLedgerStateArchiveRecord archive,
        PaperNextCycleBaselineReference baseline)
        => await services.SecondCycle.RunSecondCycleAsync(
            baseline,
            archive,
            "cycle-r026-second-paper-baseline",
            new QubesRunId("qubes-r026-second-cycle"),
            ProducedAt,
            EffectiveAt,
            CycleStartedAt,
            CycleCompletedAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            [
                "AUDUSD Curncy;0.140000",
                "EURUSD Curncy;0.160000",
                "GBPUSD Curncy;-0.240000"
            ],
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1,
            CancellationToken.None);

    private static IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol()
        => new Dictionary<string, InstrumentId>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUDUSD"] = InstrumentId.New(),
            ["EURUSD"] = InstrumentId.New(),
            ["GBPUSD"] = InstrumentId.New()
        };

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        EnsureInstruments(state, ["AUDUSD", "EURUSD", "GBPUSD"]);
        var idsBySymbol = state.Instruments
            .Where(x => x.Symbol is "AUDUSD" or "EURUSD" or "GBPUSD")
            .ToDictionary(x => x.Symbol, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        var qubesAuditRepository = new InMemoryQubesWeightAuditRepository();
        var secondCycleRepository = new InMemoryPaperBaselineSecondCycleRepository();
        var service = new PaperBaselineSecondCycleService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(qubesAuditRepository, clock),
            secondCycleRepository);

        return new TestServices(service, idsBySymbol);
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

    private static PaperLedgerStateArchiveRecord CreateArchive()
    {
        var stateArchiveId = new PaperLedgerStateArchiveId("paper-ledger-commit-r025-sample:paper-ledger-state:archive");
        var stateId = new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state");
        var commitId = new PaperLedgerCommitId("paper-ledger-commit-r025-sample");
        var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r025-sample");
        var positionPreviewId = new PaperPositionPreviewId("paper-position-preview-r025-sample");
        var simulationResultId = new PaperSimulationResultId("paper-simulation-result-r025-sample");
        var simulationPlanId = new PaperSimulationPlanId("paper-simulation-plan-r025-sample");
        var executionPlanId = new PaperExecutionPlanId("paper-execution-plan-r025-sample");
        var decisionId = new OperatorDecisionId("decision-r025-approve-ledger-commit-readiness");
        var lines = new[]
        {
            ArchiveLine(stateArchiveId, stateId, commitId, previewId, decisionId, "AUDUSD", "AUD", 131000m),
            ArchiveLine(stateArchiveId, stateId, commitId, previewId, decisionId, "EURUSD", "EUR", 124000m),
            ArchiveLine(stateArchiveId, stateId, commitId, previewId, decisionId, "GBPUSD", "GBP", -368000m)
        };

        return new PaperLedgerStateArchiveRecord(
            stateArchiveId,
            stateId,
            commitId,
            previewId,
            positionPreviewId,
            simulationResultId,
            simulationPlanId,
            executionPlanId,
            "cycle-r025-sample",
            "qubes-r025-sample",
            decisionId,
            ProducedAt,
            PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
            "PaperLedgerFixtureStateOnly",
            3,
            10,
            PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState,
            lines,
            Array.Empty<BlockedPaperReviewLineRecord>(),
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            OperatorDecisionLineagePreserved: true,
            LedgerCommitLineagePreserved: true,
            LedgerPreviewLineagePreserved: true,
            PositionPreviewLineagePreserved: true,
            SimulationResultLineagePreserved: true,
            SimulationPlanLineagePreserved: true,
            PaperExecutionPlanLineagePreserved: true,
            PaperCandidateLineagePreserved: true,
            RiskLineagePreserved: true,
            RebalanceIntentLineagePreserved: true,
            LotSizingLineagePreserved: true,
            MissingStaleMarkWarningsPreserved: true,
            DriftAcknowledgementPreserved: true,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NotProductionLedger: true,
            NotBrokerPosition: true,
            NotTradingState: true,
            NoProductionLedgerMutation: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            PaperLedgerMutatedAgain: false,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            ProductionLedgerStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            ParentOrderCreated: false,
            ChildOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }

    private static PaperLedgerStateLineArchiveRecord ArchiveLine(
        PaperLedgerStateArchiveId stateArchiveId,
        PaperLedgerStateId stateId,
        PaperLedgerCommitId commitId,
        PaperPositionLedgerPreviewId previewId,
        OperatorDecisionId decisionId,
        string symbol,
        string currency,
        decimal quantity)
        => new(
            new PaperLedgerStateLineId($"{stateId.Value}:{symbol}:line"),
            stateId,
            commitId,
            previewId,
            "cycle-r025-sample",
            "qubes-r025-sample",
            decisionId,
            InstrumentId.New(),
            symbol,
            currency,
            quantity,
            "PaperLedgerFixtureStateOnly",
            $"paperLedgerStateLine={symbol}",
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NotProductionLedger: true,
            NotBrokerPosition: true,
            NotTradingState: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true);

    private static PaperNextCycleBaselineReference CreateBaselineReference(PaperLedgerStateArchiveRecord archive)
        => new(
            "paper-next-cycle-baseline-r025",
            archive.PaperLedgerStateArchiveId,
            archive.PaperLedgerStateId,
            archive.PaperLedgerCommitId,
            archive.CycleRunId,
            archive.QubesRunId,
            "PaperLedgerFixture",
            "R024 committed paper ledger state",
            BaselineIsProduction: false,
            BaselineIsBroker: false,
            BaselineIsLiveTrading: false,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            PaperLedgerMutatedAgain: false);

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesSecondCyclePaperBaseline.cs");

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
        PaperBaselineSecondCycleService SecondCycle,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol);
}
