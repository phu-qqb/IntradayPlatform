using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ManualPaperCycleFixtureRunnerTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Manual_runner_executes_exactly_one_no_external_cycle()
    {
        var result = await ExecuteAsync();

        Assert.Equal(ManualPaperCycleRunStatus.CompletedNoExternalFixture, result.RunResult.RunStatus);
        Assert.Equal(1, result.RunResult.ManualCycleExecutionCount);
        Assert.False(result.RunResult.MultipleCyclesRun);
        Assert.True(result.RunResult.PriorPaperContinuityReadyNoExternal);
    }

    [Fact]
    public async Task Manual_run_request_is_preserved()
    {
        var result = await ExecuteAsync();

        Assert.Equal("cycle-r029-manual-paper-fixture", result.RunResult.Request.RequestedCycleRunId);
        Assert.Equal("qubes-r029-manual-fixture", result.RunResult.Request.QubesRunId?.Value);
        Assert.Equal("operator-sanitized", result.RunResult.Request.RequestedBy);
        Assert.Equal(15, result.RunResult.Request.ExpectedCadenceMinutes);
        Assert.Equal(PaperCycleRunMode.ManualNoExternal, result.RunResult.Request.RunMode);
    }

    [Fact]
    public async Task Preflight_pass_is_required_and_preserved()
    {
        var result = await ExecuteAsync();

        Assert.Equal(PaperCyclePreflightStatus.ReadyNoExternal, result.RunResult.Preflight.PreflightStatus);
        Assert.True(result.RunResult.Preflight.PreconditionsSatisfied);
        Assert.Empty(result.RunResult.Preflight.MissingPreconditions);
    }

    [Fact]
    public async Task Failed_preflight_rejects_manual_cycle_execution()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        var baseline = CreateBaselineReference(archive);
        var request = CreateRequest(runMode: PaperCycleRunMode.SchedulerRequested, schedulerOrServiceRequested: true);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => services.Runner.ExecuteOneAsync(
            request,
            CreateContinuityGate(),
            baseline,
            archive,
            ProducedAt,
            EffectiveAt,
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            RawQubesLines(),
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1,
            CancellationToken.None));
    }

    [Fact]
    public async Task Paper_ledger_baseline_is_used_as_current_state()
    {
        var result = await ExecuteAsync();

        Assert.True(result.RunResult.UsedLatestPaperLedgerFixtureBaseline);
        Assert.Contains(result.RunResult.CycleResult.CurrentPaperBaselineLines, x => x.Symbol == "AUDUSD" && x.CurrentPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
        Assert.Contains(result.RunResult.CycleResult.CurrentPaperBaselineLines, x => x.Symbol == "EURUSD" && x.CurrentPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
        Assert.Contains(result.RunResult.CycleResult.CurrentPaperBaselineLines, x => x.Symbol == "GBPUSD" && x.CurrentPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Qubes_fixture_is_ingested_netted_and_persisted_no_externally()
    {
        var result = await ExecuteAsync();

        Assert.Equal("qubes-r029-manual-fixture", result.RunResult.CycleResult.QubesRunId.Value);
        Assert.Equal(3, result.RunResult.CycleResult.QubesWeights.RawInputRowCount);
        Assert.Equal(3, result.RunResult.CycleResult.QubesWeights.NormalizedOutputRowCount);
        Assert.True(result.RunResult.CycleResult.QubesPersistence.Persisted);
        Assert.True(result.RunResult.QubesLineagePreserved);
    }

    [Fact]
    public async Task Target_portfolio_is_produced()
    {
        var result = await ExecuteAsync();

        Assert.Contains(result.RunResult.CycleResult.TargetPortfolioLines, x => x.Symbol == "AUDUSD" && x.TargetNotional == 150000m);
        Assert.Contains(result.RunResult.CycleResult.TargetPortfolioLines, x => x.Symbol == "EURUSD" && x.TargetNotional == 150000m);
        Assert.Contains(result.RunResult.CycleResult.TargetPortfolioLines, x => x.Symbol == "GBPUSD" && x.TargetNotional == -260000m);
    }

    [Fact]
    public async Task Target_vs_current_diff_is_relative_to_paper_baseline()
    {
        var result = await ExecuteAsync();

        Assert.Contains(result.RunResult.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "AUDUSD" && x.CurrentNotional == 132310m && x.DeltaNotional == 17690m);
        Assert.Contains(result.RunResult.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "EURUSD" && x.CurrentNotional == 137764m && x.DeltaNotional == 12236m);
        Assert.Contains(result.RunResult.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "GBPUSD" && x.CurrentNotional == -473616m && x.DeltaNotional == 213616m);
    }

    [Fact]
    public async Task Theoretical_pnl_reconciliation_and_theoretical_vs_real_are_produced()
    {
        var result = await ExecuteAsync();

        Assert.Equal(3, result.RunResult.CycleResult.TheoreticalPnlLines.Count);
        Assert.Equal(3, result.RunResult.CycleResult.ReconciliationLines.Count);
        Assert.Equal(3, result.RunResult.CycleResult.TheoreticalVsRealLines.Count);
        Assert.Equal(PnLComputationStatus.Computed, result.RunResult.CycleResult.TheoreticalPnl.TheoreticalPnLSnapshot.Status);
        Assert.True(result.RunResult.CycleResult.Reconciliation.HasDrift);
    }

    [Fact]
    public async Task Rebalance_intents_remain_non_executable()
    {
        var result = await ExecuteAsync();

        Assert.True(result.RunResult.RebalanceIntentsRemainNonExecutable);
        Assert.All(result.RunResult.CycleResult.RebalanceIntents, intent =>
        {
            Assert.False(intent.IsExecutable);
            Assert.Contains(IntentStatus.TheoreticalOnly, intent.IntentStatuses);
            Assert.Contains(IntentStatus.NotExecutable, intent.IntentStatuses);
            Assert.Contains(IntentStatus.BlockedNoOMS, intent.IntentStatuses);
        });
    }

    [Fact]
    public async Task Operator_cycle_report_contains_required_disclaimers()
    {
        var result = await ExecuteAsync();

        Assert.True(result.OperatorReport.IncludesManualNoExternalDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSchedulerServicePollingDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerCallDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoLiveMarketDataDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoPaperLedgerCommitDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
        Assert.Contains("executed exactly once", result.OperatorReport.Markdown);
        Assert.Contains("No paper ledger commit in R029", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task No_scheduler_service_polling_or_automatic_execution_is_started()
    {
        var result = await ExecuteAsync();

        Assert.False(result.RunResult.StartedSchedulerServicePolling);
        Assert.False(result.RunResult.CycleResult.StartedBackgroundExecution);
        Assert.False(result.RunResult.CycleResult.StartedApiOrWorker);
    }

    [Fact]
    public async Task No_paper_ledger_commit_occurs()
    {
        var result = await ExecuteAsync();

        Assert.False(result.RunResult.PaperLedgerStateCommittedOrMutated);
        Assert.False(result.RunResult.CycleResult.MutatedPaperLedgerState);
        Assert.False(result.RunResult.CycleResult.Summary.PaperLedgerStateCommittedOrMutated);
    }

    [Fact]
    public async Task No_live_broker_production_or_trading_mutation_occurs()
    {
        var result = await ExecuteAsync();

        Assert.False(result.RunResult.MutatedLivePositionState);
        Assert.False(result.RunResult.MutatedBrokerPositionState);
        Assert.False(result.RunResult.MutatedProductionLedgerState);
        Assert.False(result.RunResult.MutatedTradingState);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.LivePositionMutationCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.BrokerPositionMutationCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.ProductionLedgerMutationCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.TradingStateMutationCount);
    }

    [Fact]
    public async Task No_orders_fills_execution_reports_routes_or_submissions_are_created()
    {
        var result = await ExecuteAsync();

        Assert.False(result.RunResult.CreatedOrder);
        Assert.False(result.RunResult.CreatedFill);
        Assert.False(result.RunResult.CreatedExecutionReport);
        Assert.False(result.RunResult.CreatedBrokerRoute);
        Assert.False(result.RunResult.SubmittedOrder);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.OrderCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.FillCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.ExecutionReportCount);
        Assert.Equal(0, result.RunResult.CycleResult.Summary.BrokerRouteCount);
    }

    [Fact]
    public async Task Duplicate_requested_cycle_run_id_returns_existing_result_without_second_execution()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        var baseline = CreateBaselineReference(archive);
        var request = CreateRequest();

        var first = await ExecuteAsync(services, archive, baseline, request);
        var second = await ExecuteAsync(services, archive, baseline, request);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyExecuted);
        Assert.Equal(ManualPaperCycleRunStatus.DuplicateReturned, second.RunResult.RunStatus);
        Assert.Equal(1, second.RunResult.ManualCycleExecutionCount);
        Assert.False(second.RunResult.MultipleCyclesRun);
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
    public void Runner_source_introduces_no_runtime_boundary_scheduler_or_order_primitives()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendOrderAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitOrder", source, StringComparison.Ordinal);
    }

    private static async Task<ManualPaperCycleFixtureExecutionResult> ExecuteAsync()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        var baseline = CreateBaselineReference(archive);
        return await ExecuteAsync(services, archive, baseline, CreateRequest());
    }

    private static async Task<ManualPaperCycleFixtureExecutionResult> ExecuteAsync(
        TestServices services,
        PaperLedgerStateArchiveRecord archive,
        PaperNextCycleBaselineReference baseline,
        ManualPaperCycleRunRequest request)
        => await services.Runner.ExecuteOneAsync(
            request,
            CreateContinuityGate(),
            baseline,
            archive,
            ProducedAt,
            EffectiveAt,
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            RawQubesLines(),
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1,
            CancellationToken.None);

    private static IReadOnlyList<string> RawQubesLines()
        =>
        [
            "AUDUSD Curncy;0.150000",
            "EURUSD Curncy;0.150000",
            "GBPUSD Curncy;-0.260000"
        ];

    private static ManualPaperCycleRunRequest CreateRequest(
        PaperCycleRunMode runMode = PaperCycleRunMode.ManualNoExternal,
        bool schedulerOrServiceRequested = false)
        => new(
            "cycle-r029-manual-paper-fixture",
            new QubesRunId("qubes-r029-manual-fixture"),
            "operator-sanitized",
            ProducedAt,
            15,
            "fixtures/qubes-fx/r029-manual-cycle-fixture.csv",
            "qubes-fixture-r029-manual",
            CreateBaselineReference(CreateArchive()),
            new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state"),
            "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            runMode,
            QubesInputIsFixtureNoExternal: true,
            schedulerOrServiceRequested,
            LiveBoundaryRequested: false,
            ExecuteNowRequested: false);

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
        var cycleService = new PaperBaselineSecondCycleService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(new InMemoryQubesWeightAuditRepository(), clock),
            new InMemoryPaperBaselineSecondCycleRepository());
        var contractService = new ManualPaperCycleRunnerContractService(
            new InMemoryManualPaperCycleRunnerContractRepository(),
            clock);
        var runner = new ManualPaperCycleFixtureRunner(
            contractService,
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

    private static PaperLedgerStateArchiveRecord CreateArchive()
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
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ManualPaperCycleFixtureRunner.cs");

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
