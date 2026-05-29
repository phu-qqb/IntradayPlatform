using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ManualPaperCycleResultArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset ArchivedAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R029_manual_cycle_result_can_be_archived_no_externally()
    {
        var result = await ArchiveAsync();

        Assert.True(result.Persisted);
        Assert.Equal(ManualPaperCycleArchiveStatus.ArchivedNoExternal, result.ArchiveRecord.ArchiveStatus);
        Assert.True(result.ArchiveRecord.NoExternal);
    }

    [Fact]
    public async Task Archive_preserves_requested_cycle_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("cycle-r029-manual-paper-fixture", result.ArchiveRecord.RequestedCycleRunId);
    }

    [Fact]
    public async Task Archive_preserves_qubes_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("qubes-r029-manual-fixture", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_manual_no_external_run_mode()
    {
        var result = await ArchiveAsync();

        Assert.Equal(PaperCycleRunMode.ManualNoExternal, result.ArchiveRecord.RunMode);
    }

    [Fact]
    public async Task Archive_preserves_preflight_status()
    {
        var result = await ArchiveAsync();

        Assert.Equal(PaperCyclePreflightStatus.ReadyNoExternal, result.ArchiveRecord.PreflightStatus);
    }

    [Fact]
    public async Task Archive_preserves_paper_baseline_input()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.PaperBaselineLines, x => x.Symbol == "AUDUSD" && x.CurrentPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
        Assert.Contains(result.ArchiveRecord.PaperBaselineLines, x => x.Symbol == "EURUSD" && x.CurrentPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
        Assert.Contains(result.ArchiveRecord.PaperBaselineLines, x => x.Symbol == "GBPUSD" && x.CurrentPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Archive_preserves_target_vs_current_deltas()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "AUDUSD" && x.DeltaNotional == 17690m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "EURUSD" && x.DeltaNotional == 12236m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "GBPUSD" && x.DeltaNotional == 213616m);
    }

    [Fact]
    public async Task Archive_preserves_theoretical_pnl()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.TheoreticalPnlLines.Count);
        Assert.All(result.ArchiveRecord.TheoreticalPnlLines, x => Assert.Equal(PnLComputationStatus.Computed, x.PnLStatus));
    }

    [Fact]
    public async Task Archive_preserves_reconciliation()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.ReconciliationLines.Count);
        Assert.All(result.ArchiveRecord.ReconciliationLines, x => Assert.Equal(QubesReconciliationLineStatus.Drift, x.Status));
    }

    [Fact]
    public async Task Archive_preserves_theoretical_vs_real_report()
    {
        var result = await ArchiveAsync();

        Assert.Equal(3, result.ArchiveRecord.TheoreticalVsRealLines.Count);
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "AUDUSD");
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "EURUSD");
        Assert.Contains(result.ArchiveRecord.TheoreticalVsRealLines, x => x.Symbol == "GBPUSD");
    }

    [Fact]
    public async Task Archive_preserves_non_executable_rebalance_intents()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.RebalanceIntentsRemainNonExecutable);
        Assert.All(result.ArchiveRecord.RebalanceIntents, x => Assert.False(x.IsExecutable));
    }

    [Fact]
    public async Task Operator_report_includes_no_scheduler_service_polling_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoSchedulerDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoServiceDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoPollingDisclaimer);
        Assert.Contains("No scheduler", result.OperatorReport.Markdown);
        Assert.Contains("No service", result.OperatorReport.Markdown);
        Assert.Contains("No polling", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_broker_and_no_live_market_data_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoBrokerCallDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoLiveMarketDataDisclaimer);
        Assert.Contains("No broker call", result.OperatorReport.Markdown);
        Assert.Contains("No live market data", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_order_fill_report_route_submission_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
    }

    [Fact]
    public async Task Operator_report_includes_no_paper_ledger_commit_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoPaperLedgerCommitDisclaimer);
        Assert.Contains("No paper ledger commit", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Rolling_readiness_gate_emits_ready_or_safe_hold()
    {
        var result = await ArchiveAsync();

        Assert.True(new[]
        {
            ManualRollingReadinessStatus.ManualRollingReadyNoExternal,
            ManualRollingReadinessStatus.HeldForReview,
            ManualRollingReadinessStatus.HeldForMissingBaseline,
            ManualRollingReadinessStatus.HeldForPreflightFailure,
            ManualRollingReadinessStatus.HeldForMissingMarks,
            ManualRollingReadinessStatus.HeldForDrift,
            ManualRollingReadinessStatus.InconclusiveSafe
        }.Contains(result.RollingReadinessGate.RollingReadinessStatus));
    }

    [Fact]
    public async Task Rolling_readiness_gate_does_not_authorize_scheduler_service_polling_or_automatic_execution()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RollingReadinessGate.AuthorizesScheduler);
        Assert.False(result.RollingReadinessGate.AuthorizesService);
        Assert.False(result.RollingReadinessGate.AuthorizesPolling);
        Assert.False(result.RollingReadinessGate.AuthorizesAutomaticExecution);
    }

    [Fact]
    public async Task Rolling_readiness_gate_does_not_run_another_cycle()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RollingReadinessGate.RunsAnotherCycle);
        Assert.True(result.ArchiveRecord.NoNewCycleRun);
    }

    [Fact]
    public async Task Rolling_readiness_gate_does_not_ingest_new_qubes_batch()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RollingReadinessGate.IngestsAnotherQubesBatch);
        Assert.True(result.ArchiveRecord.NoNewQubesBatchIngest);
    }

    [Fact]
    public async Task Rolling_readiness_gate_does_not_mutate_paper_ledger()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RollingReadinessGate.MutatesPaperLedgerState);
        Assert.True(result.ArchiveRecord.NoPaperLedgerCommit);
        Assert.True(result.ArchiveRecord.NoPaperLedgerMutation);
    }

    [Fact]
    public async Task Duplicate_archive_handling_is_idempotent()
    {
        var manualResult = await CreateManualCycleResultAsync();
        var repository = new InMemoryManualPaperCycleResultArchiveRepository();
        var service = new ManualPaperCycleResultArchiveService(repository, new FixedClock(ArchivedAt));

        var first = await service.ArchiveAsync(manualResult, CancellationToken.None);
        var second = await service.ArchiveAsync(manualResult, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(ManualPaperCycleArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
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
    public void Archive_source_introduces_no_scheduler_timer_polling_service_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_orders_fills_execution_reports_routes_or_submissions_are_created()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.NoOrderCreated);
        Assert.True(result.ArchiveRecord.NoFillCreated);
        Assert.True(result.ArchiveRecord.NoExecutionReportCreated);
        Assert.True(result.ArchiveRecord.NoBrokerRoute);
        Assert.True(result.ArchiveRecord.NoSubmission);
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

    private static async Task<ManualPaperCycleResultArchiveResult> ArchiveAsync()
        => await new ManualPaperCycleResultArchiveService(
                new InMemoryManualPaperCycleResultArchiveRepository(),
                new FixedClock(ArchivedAt))
            .ArchiveAsync(await CreateManualCycleResultAsync(), CancellationToken.None);

    private static async Task<ManualPaperCycleRunResult> CreateManualCycleResultAsync()
    {
        var services = CreateRunnerServices();
        var archive = CreateLedgerArchive();
        return (await services.Runner.ExecuteOneAsync(
            CreateRequest(CreateBaselineReference(archive)),
            CreateContinuityGate(),
            CreateBaselineReference(archive),
            archive,
            ProducedAt,
            ArchivedAt,
            ProducedAt,
            ArchivedAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            [
                "AUDUSD Curncy;0.150000",
                "EURUSD Curncy;0.150000",
                "GBPUSD Curncy;-0.260000"
            ],
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1,
            CancellationToken.None)).RunResult;
    }

    private static TestServices CreateRunnerServices()
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
        var cycleService = new PaperBaselineSecondCycleService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(new InMemoryQubesWeightAuditRepository(), clock),
            new InMemoryPaperBaselineSecondCycleRepository());
        var runner = new ManualPaperCycleFixtureRunner(
            new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
            cycleService,
            new InMemoryManualPaperCycleRunResultRepository());

        return new TestServices(runner, idsBySymbol);
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

    private static ManualPaperCycleRunRequest CreateRequest(PaperNextCycleBaselineReference baselineReference)
        => new(
            "cycle-r029-manual-paper-fixture",
            new QubesRunId("qubes-r029-manual-fixture"),
            "operator-sanitized",
            ProducedAt,
            15,
            "fixtures/qubes-fx/r029-manual-cycle-fixture.csv",
            "qubes-fixture-r029-manual",
            baselineReference,
            baselineReference.PaperLedgerStateId,
            "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            PaperCycleRunMode.ManualNoExternal,
            QubesInputIsFixtureNoExternal: true,
            SchedulerOrServiceRequested: false,
            LiveBoundaryRequested: false,
            ExecuteNowRequested: false);

    private static PaperCycleContinuityDecisionGate CreateContinuityGate()
        => new(
            "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            new SecondCyclePaperContinuityArchiveId("cycle-r026-second-paper-baseline:paper-continuity-archive"),
            PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
            FutureManualPaperCyclesMayUseLatestPaperLedgerFixtureBaseline: true,
            StartsSchedulerOrService: false,
            RunsAnotherCycle: false,
            IngestsNewQubesBatch: false,
            MutatesPaperLedgerState: false,
            MutatesLivePositionState: false,
            MutatesBrokerPositionState: false,
            MutatesProductionLedgerState: false,
            MutatesTradingState: false,
            CreatesOrderCandidates: false,
            CreatesExecutionPlans: false,
            CreatesOrders: false,
            CreatesFills: false,
            CreatesExecutionReports: false,
            CreatesRoutes: false,
            SubmitsOrders: false,
            NoExternal: true,
            PreservesNonExecutableRebalanceIntents: true);

    private static PaperLedgerStateArchiveRecord CreateLedgerArchive()
    {
        var stateArchiveId = new PaperLedgerStateArchiveId("paper-ledger-commit-r025-sample:paper-ledger-state:archive");
        var stateId = new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state");
        var commitId = new PaperLedgerCommitId("paper-ledger-commit-r025-sample");
        var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r025-sample");
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
            new PaperPositionPreviewId("paper-position-preview-r025-sample"),
            new PaperSimulationResultId("paper-simulation-result-r025-sample"),
            new PaperSimulationPlanId("paper-simulation-plan-r025-sample"),
            new PaperExecutionPlanId("paper-execution-plan-r025-sample"),
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
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true);

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
            false,
            false,
            false,
            true,
            true,
            true,
            false,
            false,
            false);

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ManualPaperCycleResultArchive.cs");

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
        ManualPaperCycleFixtureRunner Runner,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol);
}
