using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesSecondCycleContinuityArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 15, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R026_second_cycle_output_can_be_archived_reported_no_externally()
    {
        var result = await ArchiveAsync();

        Assert.True(result.Persisted);
        Assert.Equal(SecondCyclePaperContinuityArchiveStatus.ArchivedNoExternal, result.ArchiveRecord.ArchiveStatus);
        Assert.True(result.ArchiveRecord.NoExternal);
    }

    [Fact]
    public async Task Archive_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("cycle-r026-second-paper-baseline", result.ArchiveRecord.SecondCycleRunId);
        Assert.Equal("qubes-r026-second-cycle", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_r025_paper_baseline_reference()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.PaperBaselineFromR025);
        Assert.Equal("R024 committed paper ledger state", result.ArchiveRecord.PaperBaselineInput.BaselineSource);
        Assert.Equal("PaperLedgerFixture", result.ArchiveRecord.PaperBaselineInput.NextCycleBaselineType);
        Assert.False(result.ArchiveRecord.PaperBaselineInput.BaselineIsProduction);
        Assert.False(result.ArchiveRecord.PaperBaselineInput.BaselineIsBroker);
    }

    [Fact]
    public async Task Current_baseline_includes_audusd_131000_aud()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.CurrentPaperBaselineLines, x => x.Symbol == "AUDUSD" && x.CurrentPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Current_baseline_includes_eurusd_124000_eur()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.CurrentPaperBaselineLines, x => x.Symbol == "EURUSD" && x.CurrentPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Current_baseline_includes_gbpusd_negative_368000_gbp()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.CurrentPaperBaselineLines, x => x.Symbol == "GBPUSD" && x.CurrentPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Target_vs_current_deltas_are_preserved()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "AUDUSD" && x.DeltaNotional == 7690m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "EURUSD" && x.DeltaNotional == 22236m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "GBPUSD" && x.DeltaNotional == 233616m);
    }

    [Fact]
    public async Task Theoretical_pnl_is_archived()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.TheoreticalPnlLines.Count);
        Assert.All(result.ArchiveRecord.TheoreticalPnlLines, x => Assert.Equal(PnLComputationStatus.Computed, x.PnLStatus));
    }

    [Fact]
    public async Task Reconciliation_is_archived()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.ReconciliationLines.Count);
        Assert.All(result.ArchiveRecord.ReconciliationLines, x => Assert.Equal(QubesReconciliationLineStatus.Drift, x.Status));
    }

    [Fact]
    public async Task Theoretical_vs_real_report_is_archived()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.TheoreticalVsRealLines.Count);
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "AUDUSD");
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "EURUSD");
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "GBPUSD");
    }

    [Fact]
    public async Task Rebalance_intents_remain_non_executable()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.RebalanceIntentsRemainNonExecutable);
        Assert.All(result.ArchiveRecord.RebalanceIntents, intent =>
        {
            Assert.False(intent.IsExecutable);
            Assert.Contains(IntentStatus.TheoreticalOnly, intent.IntentStatuses);
            Assert.Contains(IntentStatus.NotExecutable, intent.IntentStatuses);
            Assert.Contains(IntentStatus.BlockedNoOMS, intent.IntentStatuses);
        });
    }

    [Fact]
    public async Task Operator_report_includes_no_live_no_order_no_fill_no_route_disclaimers()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesPaperLedgerFixtureBaselineDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoLiveBrokerProductionTradingMutationDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoPaperLedgerCommitDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerCallDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoLiveMarketDataDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
        Assert.Contains("No live/broker/production/trading state mutation", result.OperatorReport.Markdown);
        Assert.Contains("No paper ledger commit in R026/R027", result.OperatorReport.Markdown);
        Assert.Contains("No orders", result.OperatorReport.Markdown);
        Assert.Contains("No fills", result.OperatorReport.Markdown);
        Assert.Contains("No broker routes", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Paper_continuity_gate_produces_ready_or_safe_hold_status()
    {
        var result = await ArchiveAsync();

        Assert.True(new[]
        {
            PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
            PaperCycleContinuityStatus.HeldForReview,
            PaperCycleContinuityStatus.HeldForMissingMarks,
            PaperCycleContinuityStatus.HeldForDrift,
            PaperCycleContinuityStatus.InconclusiveSafe
        }.Contains(result.ContinuityGate.ContinuityStatus));
    }

    [Fact]
    public async Task Gate_does_not_start_scheduler_service_or_polling()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.StartsSchedulerOrService);
        Assert.True(result.ArchiveRecord.NoSchedulerServicePolling);
    }

    [Fact]
    public async Task Gate_does_not_run_another_cycle()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.RunsAnotherCycle);
        Assert.True(result.ArchiveRecord.NoNewCycleRun);
    }

    [Fact]
    public async Task Gate_does_not_ingest_new_qubes_batch()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.IngestsNewQubesBatch);
        Assert.True(result.ArchiveRecord.NoNewQubesBatchIngest);
    }

    [Fact]
    public async Task Gate_does_not_mutate_paper_ledger_state()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.MutatesPaperLedgerState);
        Assert.True(result.ArchiveRecord.NoPaperLedgerMutation);
        Assert.False(result.ArchiveRecord.PaperLedgerStateCommittedOrMutated);
    }

    [Fact]
    public async Task Gate_does_not_mutate_live_broker_production_or_trading_state()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.MutatesLivePositionState);
        Assert.False(result.ContinuityGate.MutatesBrokerPositionState);
        Assert.False(result.ContinuityGate.MutatesProductionLedgerState);
        Assert.False(result.ContinuityGate.MutatesTradingState);
    }

    [Fact]
    public async Task Gate_does_not_create_orders_fills_or_execution_reports()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.CreatesOrderCandidates);
        Assert.False(result.ContinuityGate.CreatesExecutionPlans);
        Assert.False(result.ContinuityGate.CreatesOrders);
        Assert.False(result.ContinuityGate.CreatesFills);
        Assert.False(result.ContinuityGate.CreatesExecutionReports);
        Assert.False(result.ContinuityGate.CreatesRoutes);
        Assert.False(result.ContinuityGate.SubmitsOrders);
    }

    [Fact]
    public async Task Duplicate_archive_handling_is_idempotent()
    {
        var cycle = await CreateSecondCycleAsync();
        var repository = new InMemorySecondCyclePaperContinuityArchiveRepository();
        var service = new SecondCyclePaperContinuityArchiveService(repository, new FixedClock(ProducedAt));

        var first = await service.ArchiveAsync(cycle, CancellationToken.None);
        var second = await service.ArchiveAsync(cycle, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(SecondCyclePaperContinuityArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
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

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
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
    public void Archive_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    private static async Task<SecondCyclePaperContinuityArchiveResult> ArchiveAsync()
        => await new SecondCyclePaperContinuityArchiveService(
                new InMemorySecondCyclePaperContinuityArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(await CreateSecondCycleAsync(), CancellationToken.None);

    private static async Task<PaperBaselineSecondCycleResult> CreateSecondCycleAsync()
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
        var service = new PaperBaselineSecondCycleService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(new InMemoryQubesWeightAuditRepository(), clock),
            new InMemoryPaperBaselineSecondCycleRepository());
        var archive = CreateR025Archive();

        return await service.RunSecondCycleAsync(
            CreateR025BaselineReference(archive),
            archive,
            "cycle-r026-second-paper-baseline",
            new QubesRunId("qubes-r026-second-cycle"),
            ProducedAt,
            EffectiveAt,
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            [
                "AUDUSD Curncy;0.140000",
                "EURUSD Curncy;0.160000",
                "GBPUSD Curncy;-0.240000"
            ],
            idsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1,
            CancellationToken.None);
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

    private static PaperLedgerStateArchiveRecord CreateR025Archive()
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
            ArchiveLine(stateId, commitId, previewId, decisionId, "AUDUSD", "AUD", 131000m),
            ArchiveLine(stateId, commitId, previewId, decisionId, "EURUSD", "EUR", 124000m),
            ArchiveLine(stateId, commitId, previewId, decisionId, "GBPUSD", "GBP", -368000m)
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

    private static PaperNextCycleBaselineReference CreateR025BaselineReference(PaperLedgerStateArchiveRecord archive)
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
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesSecondCycleContinuityArchive.cs");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }
}
