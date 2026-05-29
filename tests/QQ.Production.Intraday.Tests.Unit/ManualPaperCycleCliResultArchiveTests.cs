using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ManualPaperCycleCliResultArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset ArchivedAt = ProducedAt.AddMinutes(1);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Valid_manual_no_external_cli_invocation_runs_exactly_one_cycle()
    {
        var result = await ArchiveAsync();

        Assert.Equal(ManualPaperCycleCliArchiveStatus.ArchivedNoExternal, result.ArchiveRecord.ArchiveStatus);
        Assert.Equal(1, result.ArchiveRecord.ExecutionCount);
        Assert.Equal(1, result.ArchiveRecord.CliInvocationCount);
        Assert.False(result.ArchiveRecord.MoreThanOneCliInvocationRun);
        Assert.False(result.ArchiveRecord.MoreThanOneCycleRun);
    }

    [Fact]
    public async Task Archive_preserves_cli_invocation_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("cycle-r032-cli-manual-paper-fixture:cli-invocation-result-archive", result.ArchiveRecord.CliInvocationId.Value);
    }

    [Fact]
    public async Task Archive_preserves_requested_cycle_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("cycle-r032-cli-manual-paper-fixture", result.ArchiveRecord.RequestedCycleRunId);
    }

    [Fact]
    public async Task Archive_preserves_qubes_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("qubes-r032-cli-fixture", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_preflight_status()
    {
        var result = await ArchiveAsync();

        Assert.Equal(PaperCyclePreflightStatus.ReadyNoExternal, result.ArchiveRecord.PreflightStatus);
    }

    [Fact]
    public async Task Archive_preserves_execution_count_one()
    {
        var result = await ArchiveAsync();

        Assert.Equal(1, result.ArchiveRecord.ExecutionCount);
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
    public async Task Archive_preserves_target_vs_current_diff()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "AUDUSD" && x.DeltaNotional == 17690m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "EURUSD" && x.DeltaNotional == 12236m);
        Assert.Contains(result.ArchiveRecord.TargetVsCurrentDiffLines, x => x.Symbol == "GBPUSD" && x.DeltaNotional == 213616m);
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
    public async Task Operator_report_includes_no_automatic_execution_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoAutomaticExecutionDisclaimer);
        Assert.Contains("No automatic execution", result.OperatorReport.Markdown);
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
    public async Task Operator_report_includes_no_order_fill_execution_report_route_submission_disclaimer()
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
    public async Task Repeated_use_gate_emits_ready_or_safe_hold()
    {
        var result = await ArchiveAsync();

        ManualPaperCycleCliReadinessStatus[] statuses =
        [
            ManualPaperCycleCliReadinessStatus.ManualCliReadyForRepeatedOperatorUseNoExternal,
            ManualPaperCycleCliReadinessStatus.HeldForReview,
            ManualPaperCycleCliReadinessStatus.HeldForPreflightIssue,
            ManualPaperCycleCliReadinessStatus.HeldForMissingBaseline,
            ManualPaperCycleCliReadinessStatus.HeldForMissingQubesFixture,
            ManualPaperCycleCliReadinessStatus.InconclusiveSafe
        ];

        Assert.Contains(result.RepeatedUseGate.ReadinessStatus, statuses);
    }

    [Fact]
    public async Task Repeated_use_gate_does_not_authorize_scheduler_service_polling_or_automatic_execution()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RepeatedUseGate.AuthorizesScheduler);
        Assert.False(result.RepeatedUseGate.AuthorizesService);
        Assert.False(result.RepeatedUseGate.AuthorizesPolling);
        Assert.False(result.RepeatedUseGate.AuthorizesAutomaticExecution);
    }

    [Fact]
    public async Task Repeated_use_gate_does_not_run_another_cycle()
    {
        var result = await ArchiveAsync();

        Assert.False(result.RepeatedUseGate.RunsAnotherCycle);
    }

    [Fact]
    public async Task Duplicate_requested_cycle_run_id_returns_duplicate_or_existing_result()
    {
        var service = new ManualPaperCycleCliResultArchiveService(
            new InMemoryManualPaperCycleCliResultArchiveRepository(),
            new FixedClock(ArchivedAt));
        var cliResult = await CreateCliResultAsync();

        var first = await service.ArchiveAsync(cliResult, CancellationToken.None);
        var second = await service.ArchiveAsync(cliResult, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(ManualPaperCycleCliArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task Cli_does_not_commit_paper_ledger_state()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.NoPaperLedgerCommit);
        Assert.True(result.ArchiveRecord.NoPaperLedgerMutation);
    }

    [Fact]
    public async Task Cli_does_not_create_orders_fills_execution_reports_routes_or_submissions()
    {
        var result = await ArchiveAsync();

        Assert.True(result.ArchiveRecord.NoOrderCreated);
        Assert.True(result.ArchiveRecord.NoFillCreated);
        Assert.True(result.ArchiveRecord.NoExecutionReportCreated);
        Assert.True(result.ArchiveRecord.NoBrokerRoute);
        Assert.True(result.ArchiveRecord.NoSubmission);
    }

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
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

    private static async Task<ManualPaperCycleCliResultArchiveResult> ArchiveAsync()
        => await new ManualPaperCycleCliResultArchiveService(
                new InMemoryManualPaperCycleCliResultArchiveRepository(),
                new FixedClock(ArchivedAt))
            .ArchiveAsync(await CreateCliResultAsync(), CancellationToken.None);

    private static async Task<ManualPaperCycleCliResult> CreateCliResultAsync()
    {
        var services = CreateServices();
        return await services.Surface.RunAsync(ValidArgs(), CreateContext(), CancellationToken.None);
    }

    private static string[] ValidArgs()
        =>
        [
            "--mode", "ManualNoExternal",
            "--requested-cycle-run-id", "cycle-r032-cli-manual-paper-fixture",
            "--qubes-run-id", "qubes-r032-cli-fixture",
            "--qubes-fixture-path", "fixtures/qubes-fx/r032-cli-manual-cycle-fixture.csv",
            "--prior-paper-ledger-state-id", "paper-ledger-commit-r025-sample:paper-ledger-state",
            "--prior-continuity-gate-id", "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            "--requested-by", "operator-sanitized",
            "--expected-cadence-minutes", "15",
            "--output-artifacts-dir", "artifacts/readiness/pms-ems-oms-integration",
            "--allow-synthetic-pms-fixture", "true",
            "--allow-not-qubes-economic-output-fixture", "true",
            "--no-order", "true",
            "--no-route", "true",
            "--no-fill", "true",
            "--no-broker", "true",
            "--no-fix", "true",
            "--no-executable-schedule", "true",
            "--no-live-state-mutation", "true",
            "--no-ledger-commit", "true",
            "--no-paper-ledger-commit", "true"
        ];

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
        var runner = new ManualPaperCycleFixtureRunner(
            new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
            cycleService,
            new InMemoryManualPaperCycleRunResultRepository());
        var surface = new ManualPaperCycleCliSurface(
            new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
            runner);

        return new TestServices(surface, idsBySymbol);
    }

    private static ManualPaperCycleCliExecutionContext CreateContext()
    {
        var services = CreateServices();
        var archive = CreateArchive();
        return new ManualPaperCycleCliExecutionContext(
            CreateContinuityGate(),
            CreateBaselineReference(archive),
            archive,
            ProducedAt,
            EffectiveAt,
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            ["AUDUSD Curncy;0.150000", "EURUSD Curncy;0.150000", "GBPUSD Curncy;-0.260000"],
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1);
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
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ManualPaperCycleCliResultArchive.cs");

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
        ManualPaperCycleCliSurface Surface,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol);
}
