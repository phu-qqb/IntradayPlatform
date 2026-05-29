using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ManualPaperCycleRunnerContractTests
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Manual_runner_contract_requires_prior_paper_continuity_ready()
    {
        var result = await DefineAsync(request: CreateRequest(), priorContinuityGate: CreateContinuityGate());

        Assert.Equal(PaperCycleContinuityStatus.PaperContinuityReadyNoExternal, result.Contract.PriorContinuityStatus);
        Assert.True(result.Contract.R027ContinuityDecisionPreserved);
        Assert.Equal(PaperCyclePreflightStatus.ReadyNoExternal, result.Contract.Preflight.PreflightStatus);
    }

    [Fact]
    public async Task Manual_runner_contract_requires_prior_paper_ledger_baseline()
    {
        var result = await DefineAsync(CreateRequest(includeBaselineReference: false), CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.HeldMissingPriorBaseline, result.Contract.Preflight.PreflightStatus);
        Assert.Contains(ManualPaperCycleFailureCategory.MissingPriorBaseline, result.Contract.Preflight.MissingPreconditions);
    }

    [Fact]
    public async Task Manual_runner_contract_requires_qubes_run_id()
    {
        var result = await DefineAsync(CreateRequest(includeQubesRunId: false), CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.HeldMissingQubesInput, result.Contract.Preflight.PreflightStatus);
        Assert.Contains(ManualPaperCycleFailureCategory.MissingQubesInput, result.Contract.Preflight.MissingPreconditions);
    }

    [Fact]
    public async Task Manual_runner_contract_requires_15_minute_cadence()
    {
        var result = await DefineAsync(CreateRequest(expectedCadenceMinutes: 30), CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.HeldInvalidCadence, result.Contract.Preflight.PreflightStatus);
        Assert.Equal("InvalidCadence", result.Contract.Preflight.CadenceStatus);
    }

    [Fact]
    public async Task Manual_runner_contract_rejects_missing_qubes_input()
    {
        var result = await DefineAsync(CreateRequest(qubesFixturePath: null, qubesBatchReference: null), CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.HeldMissingQubesInput, result.Contract.Preflight.PreflightStatus);
        Assert.Equal("MissingOrUnsafeQubesInput", result.Contract.Preflight.QubesInputStatus);
    }

    [Fact]
    public async Task Manual_runner_contract_rejects_scheduler_service_polling_mode()
    {
        var result = await DefineAsync(
            CreateRequest(runMode: PaperCycleRunMode.SchedulerRequested, schedulerOrServiceRequested: true),
            CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.RejectedUnsafeExternalActionRisk, result.Contract.Preflight.PreflightStatus);
        Assert.Contains(ManualPaperCycleFailureCategory.UnsafeExternalActionRisk, result.Contract.Preflight.MissingPreconditions);
        Assert.False(result.Contract.Preflight.StartsSchedulerOrService);
    }

    [Fact]
    public async Task Manual_runner_contract_is_no_external()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());

        Assert.True(result.Contract.NoExternal);
        Assert.True(result.Contract.SafetyGate.NoExternal);
        Assert.True(result.Contract.SafetyGate.NoBroker);
        Assert.True(result.Contract.SafetyGate.NoLiveMarketData);
    }

    [Fact]
    public async Task Manual_runner_shape_does_not_execute_a_cycle()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());

        Assert.False(result.Contract.Preflight.ExecutesCycle);
        Assert.True(result.Contract.SafetyGate.NoCycleExecution);
        Assert.True(result.Contract.ExpectedOutput.ExecutesNoCycle);
    }

    [Fact]
    public async Task Manual_runner_shape_does_not_ingest_new_qubes_batch()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());

        Assert.False(result.Contract.Preflight.IngestsNewQubesBatch);
        Assert.True(result.Contract.SafetyGate.NoQubesIngest);
    }

    [Fact]
    public async Task Manual_runner_shape_does_not_mutate_paper_ledger_state()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());

        Assert.False(result.Contract.Preflight.MutatesPaperLedgerState);
        Assert.True(result.Contract.SafetyGate.NoPaperLedgerMutation);
    }

    [Fact]
    public async Task Manual_runner_shape_does_not_create_orders_fills_or_execution_reports()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());

        Assert.False(result.Contract.Preflight.CreatesOrders);
        Assert.False(result.Contract.Preflight.CreatesFills);
        Assert.False(result.Contract.Preflight.CreatesExecutionReports);
        Assert.True(result.Contract.SafetyGate.NoOrder);
        Assert.True(result.Contract.SafetyGate.NoFill);
        Assert.True(result.Contract.SafetyGate.NoExecutionReport);
    }

    [Fact]
    public async Task Manual_runner_shape_does_not_call_broker_or_market_data()
    {
        var result = await DefineAsync(CreateRequest(liveBoundaryRequested: true), CreateContinuityGate());

        Assert.Equal(PaperCyclePreflightStatus.RejectedUnsafeExternalActionRisk, result.Contract.Preflight.PreflightStatus);
        Assert.False(result.Contract.Preflight.CallsBrokerOrLiveMarketData);
        Assert.True(result.Contract.SafetyGate.NoBroker);
        Assert.True(result.Contract.SafetyGate.NoLiveMarketData);
    }

    [Fact]
    public async Task Expected_output_shape_includes_target_diff_pnl_reconciliation_theoretical_vs_real_and_rebalance_intents()
    {
        var result = await DefineAsync(CreateRequest(), CreateContinuityGate());
        var output = result.Contract.ExpectedOutput;

        Assert.True(output.TargetPortfolioShapeIncluded);
        Assert.True(output.TargetVsCurrentDiffShapeIncluded);
        Assert.True(output.TheoreticalPnlShapeIncluded);
        Assert.True(output.ReconciliationShapeIncluded);
        Assert.True(output.TheoreticalVsRealShapeIncluded);
        Assert.True(output.NonExecutableRebalanceIntentShapeIncluded);
        Assert.True(output.OperatorReportShapeIncluded);
        Assert.True(output.RebalanceIntentsMustRemainNonExecutable);
    }

    [Fact]
    public async Task Duplicate_requested_cycle_run_id_is_idempotent()
    {
        var repository = new InMemoryManualPaperCycleRunnerContractRepository();
        var service = new ManualPaperCycleRunnerContractService(repository, new FixedClock(RequestedAt));
        var request = CreateRequest();

        var first = await service.DefineAsync(request, CreateContinuityGate(), CancellationToken.None);
        var second = await service.DefineAsync(request, CreateContinuityGate(), CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyDefined);
        Assert.Equal(PaperCycleIdempotencyStatus.DuplicateReturned, second.Contract.Preflight.IdempotencyStatus);
        Assert.Equal(PaperCyclePreflightStatus.HeldDuplicateCycleRunId, second.Contract.Preflight.PreflightStatus);
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
        var apiSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Contract_source_introduces_no_runtime_boundary_or_scheduler_primitives()
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

    private static async Task<ManualPaperCycleRunnerContractResult> DefineAsync(
        ManualPaperCycleRunRequest request,
        PaperCycleContinuityDecisionGate? priorContinuityGate)
        => await new ManualPaperCycleRunnerContractService(
                new InMemoryManualPaperCycleRunnerContractRepository(),
                new FixedClock(RequestedAt))
            .DefineAsync(request, priorContinuityGate, CancellationToken.None);

    private static ManualPaperCycleRunRequest CreateRequest(
        bool includeQubesRunId = true,
        int expectedCadenceMinutes = 15,
        string? qubesFixturePath = "fixtures/qubes-fx/r028-manual-cycle-fixture.csv",
        string? qubesBatchReference = "qubes-fixture-r028-manual",
        bool includeBaselineReference = true,
        PaperNextCycleBaselineReference? baselineReference = null,
        PaperCycleRunMode runMode = PaperCycleRunMode.ManualNoExternal,
        bool schedulerOrServiceRequested = false,
        bool liveBoundaryRequested = false,
        bool executeNowRequested = false)
    {
        baselineReference = includeBaselineReference ? baselineReference ?? CreateBaselineReference() : null;

        return new ManualPaperCycleRunRequest(
            "cycle-r028-manual-paper-shape",
            includeQubesRunId ? new QubesRunId("qubes-r028-manual-fixture") : null,
            "operator-sanitized",
            RequestedAt,
            expectedCadenceMinutes,
            qubesFixturePath,
            qubesBatchReference,
            baselineReference,
            baselineReference is null ? null : new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state"),
            "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            runMode,
            QubesInputIsFixtureNoExternal: true,
            schedulerOrServiceRequested,
            liveBoundaryRequested,
            executeNowRequested);
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

    private static PaperNextCycleBaselineReference CreateBaselineReference()
        => new(
            "paper-next-cycle-baseline-r025",
            new PaperLedgerStateArchiveId("paper-ledger-commit-r025-sample:paper-ledger-state:archive"),
            new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state"),
            new PaperLedgerCommitId("paper-ledger-commit-r025-sample"),
            "cycle-r025-sample",
            "qubes-r025-sample",
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
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ManualPaperCycleRunnerContract.cs");

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
