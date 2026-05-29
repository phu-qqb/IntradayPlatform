using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperLedgerStateArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task R024_paper_ledger_state_can_be_archived_no_externally()
    {
        var result = await ArchiveAsync();

        Assert.True(result.Persisted);
        Assert.Equal(PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState, result.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task Archive_preserves_paper_ledger_state_id()
    {
        var state = CreateState();
        var result = await ArchiveAsync(state);

        Assert.Equal(state.PaperLedgerStateId, result.ArchiveRecord.PaperLedgerStateId);
    }

    [Fact]
    public async Task Archive_preserves_paper_ledger_commit_id()
    {
        var state = CreateState();
        var result = await ArchiveAsync(state);

        Assert.Equal(state.PaperLedgerCommitId, result.ArchiveRecord.PaperLedgerCommitId);
    }

    [Fact]
    public async Task Archive_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await ArchiveAsync();

        Assert.Equal("cycle-r025-sample", result.ArchiveRecord.CycleRunId);
        Assert.Equal("qubes-r025-sample", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_operator_decision_id()
    {
        var state = CreateState();
        var result = await ArchiveAsync(state);

        Assert.Equal(state.OperatorDecisionId, result.ArchiveRecord.OperatorDecisionId);
    }

    [Fact]
    public async Task Archive_lines_include_audusd()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "AUDUSD" && x.EndingPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Archive_lines_include_eurusd()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "EURUSD" && x.EndingPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Archive_lines_include_gbpusd()
    {
        var result = await ArchiveAsync();

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "GBPUSD" && x.EndingPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Operator_report_includes_paper_fixture_state_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesPaperFixtureStateDisclaimer);
        Assert.Contains("Paper ledger fixture state only", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_production_ledger_mutation_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoProductionLedgerMutationDisclaimer);
        Assert.Contains("no production ledger mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_live_position_mutation_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoLivePositionMutationDisclaimer);
        Assert.Contains("no live position mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_broker_position_mutation_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoBrokerPositionMutationDisclaimer);
        Assert.Contains("no broker position mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_trading_state_mutation_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoTradingStateMutationDisclaimer);
        Assert.Contains("no trading-state mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_order_fill_execution_report_route_or_submission_disclaimer()
    {
        var result = await ArchiveAsync();

        Assert.True(result.OperatorReport.IncludesNoOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
    }

    [Fact]
    public async Task Next_cycle_continuity_baseline_references_paper_ledger_fixture_state()
    {
        var result = await ArchiveAsync();

        Assert.Equal("PaperLedgerFixture", result.NextCycleBaselineReference.NextCycleBaselineType);
        Assert.Equal("R024 committed paper ledger state", result.NextCycleBaselineReference.BaselineSource);
        Assert.False(result.NextCycleBaselineReference.BaselineIsProduction);
        Assert.False(result.NextCycleBaselineReference.BaselineIsBroker);
        Assert.False(result.NextCycleBaselineReference.BaselineIsLiveTrading);
    }

    [Fact]
    public async Task Next_cycle_continuity_does_not_run_a_new_cycle()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.NewCycleRan);
        Assert.False(result.NextCycleBaselineReference.NewCycleRan);
    }

    [Fact]
    public async Task Next_cycle_continuity_does_not_ingest_a_new_qubes_batch()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ContinuityGate.NewQubesBatchIngested);
        Assert.False(result.NextCycleBaselineReference.NewQubesBatchIngested);
    }

    [Fact]
    public async Task Next_cycle_continuity_does_not_mutate_paper_ledger_again()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ArchiveRecord.PaperLedgerMutatedAgain);
        Assert.False(result.ContinuityGate.PaperLedgerMutatedAgain);
    }

    [Fact]
    public async Task Duplicate_archive_handling_is_idempotent()
    {
        var state = CreateState();
        var repository = new InMemoryPaperLedgerStateArchiveRepository();
        var service = new PaperLedgerStateArchiveService(repository, new FixedClock(ProducedAt));

        var first = await service.ArchiveAsync(state, CancellationToken.None);
        var second = await service.ArchiveAsync(state, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(PaperLedgerStateArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task No_live_broker_production_or_trading_state_is_mutated()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ArchiveRecord.LivePositionStateMutated);
        Assert.False(result.ArchiveRecord.BrokerPositionStateMutated);
        Assert.False(result.ArchiveRecord.ProductionLedgerStateMutated);
        Assert.False(result.ArchiveRecord.TradingStateMutated);
    }

    [Fact]
    public async Task No_fills_execution_reports_or_orders_are_created()
    {
        var result = await ArchiveAsync();

        Assert.False(result.ArchiveRecord.FillCreated);
        Assert.False(result.ArchiveRecord.ExecutionReportCreated);
        Assert.False(result.ArchiveRecord.OmsOrderCreated);
        Assert.False(result.ArchiveRecord.ParentOrderCreated);
        Assert.False(result.ArchiveRecord.ChildOrderCreated);
        Assert.False(result.ArchiveRecord.BrokerOrderCreated);
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
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var api = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"));
        var worker = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"));

        Assert.Contains("\"AllowLiveTrading\": false", api);
        Assert.Contains("\"AllowExternalConnections\": false", api);
        Assert.Contains("\"RequireFakeExecutionGateway\": true", api);
        Assert.Contains("\"AllowLiveTrading\": false", worker);
        Assert.Contains("\"AllowExternalConnections\": false", worker);
        Assert.Contains("\"RequireFakeExecutionGateway\": true", worker);
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

    private static async Task<PaperLedgerStateArchiveResult> ArchiveAsync()
        => await ArchiveAsync(CreateState());

    private static async Task<PaperLedgerStateArchiveResult> ArchiveAsync(PaperLedgerState state)
        => await new PaperLedgerStateArchiveService(
                new InMemoryPaperLedgerStateArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(state, CancellationToken.None);

    private static PaperLedgerState CreateState()
    {
        var commitId = new PaperLedgerCommitId("paper-ledger-commit-r025-sample");
        var stateId = new PaperLedgerStateId($"{commitId.Value}:paper-ledger-state");
        var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r025-sample");
        var positionPreviewId = new PaperPositionPreviewId("paper-position-preview-r025-sample");
        var simulationResultId = new PaperSimulationResultId("paper-simulation-result-r025-sample");
        var simulationPlanId = new PaperSimulationPlanId("paper-simulation-plan-r025-sample");
        var executionPlanId = new PaperExecutionPlanId("paper-execution-plan-r025-sample");
        var operatorDecisionId = new OperatorDecisionId("decision-r025-approve-ledger-commit-readiness");
        var lines = new[]
        {
            Line(stateId, commitId, previewId, operatorDecisionId, "AUDUSD", "AUD", 131000m),
            Line(stateId, commitId, previewId, operatorDecisionId, "EURUSD", "EUR", 124000m),
            Line(stateId, commitId, previewId, operatorDecisionId, "GBPUSD", "GBP", -368000m)
        };
        var summary = new PaperLedgerCommitSummary(
            AppliedLineCount: 3,
            BlockedLineCount: 10,
            LivePositionMutationCount: 0,
            BrokerPositionMutationCount: 0,
            ProductionLedgerMutationCount: 0,
            TradingStateMutationCount: 0,
            OrderCount: 0,
            FillCount: 0,
            ExecutionReportCount: 0,
            BrokerRouteCount: 0,
            SafetyStatus: "PaperLedgerFixtureStateOnly");

        return new PaperLedgerState(
            stateId,
            commitId,
            previewId,
            positionPreviewId,
            simulationResultId,
            simulationPlanId,
            executionPlanId,
            "cycle-r025-sample",
            "qubes-r025-sample",
            operatorDecisionId,
            ProducedAt,
            PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
            "PaperLedgerFixtureStateOnly",
            lines,
            Array.Empty<BlockedPaperReviewLineRecord>(),
            summary,
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            OperatorDecisionLineagePreserved: true,
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
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
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

    private static PaperLedgerStateLine Line(
        PaperLedgerStateId stateId,
        PaperLedgerCommitId commitId,
        PaperPositionLedgerPreviewId previewId,
        OperatorDecisionId operatorDecisionId,
        string symbol,
        string quantityCurrency,
        decimal endingQuantity)
        => new(
            new PaperLedgerStateLineId($"{stateId.Value}:{symbol}:line"),
            stateId,
            commitId,
            previewId,
            "cycle-r025-sample",
            "qubes-r025-sample",
            operatorDecisionId,
            InstrumentId.New(),
            symbol,
            quantityCurrency,
            StartingPaperQuantity: 0m,
            AppliedPaperDeltaQuantity: endingQuantity,
            EndingPaperQuantity: endingQuantity,
            PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
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

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateArchive.cs");

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
