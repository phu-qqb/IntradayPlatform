using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperLedgerStateCommitTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task R023_approved_paper_ledger_preview_can_commit_to_paper_ledger_fixture_state()
    {
        var result = await CommitAsync();

        Assert.True(result.Persisted);
        Assert.Equal(PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal, result.State.CommitStatus);
        Assert.True(result.State.FixtureState);
    }

    [Fact]
    public async Task Commit_requires_paper_ledger_commit_ready_no_external()
    {
        var archive = CreateArchive();
        var approval = CreateApproval(archive) with
        {
            ResultingReadinessStatus = PaperLedgerCommitReadinessStatus.HeldForBlockedLines,
            DecisionType = PaperLedgerCommitDecisionType.Hold
        };

        var result = await CommitAsync(archive, approval);

        Assert.False(result.Persisted);
        Assert.Equal(PaperLedgerCommitStatus.RejectedMissingApproval, result.State.CommitStatus);
    }

    [Fact]
    public async Task Audusd_ending_paper_quantity_is_applied()
    {
        var result = await CommitAsync();

        Assert.Contains(result.State.Lines, x => x.CurrencyOrSymbol == "AUDUSD" && x.EndingPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Eurusd_ending_paper_quantity_is_applied()
    {
        var result = await CommitAsync();

        Assert.Contains(result.State.Lines, x => x.CurrencyOrSymbol == "EURUSD" && x.EndingPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Gbpusd_ending_paper_quantity_is_applied()
    {
        var result = await CommitAsync();

        Assert.Contains(result.State.Lines, x => x.CurrencyOrSymbol == "GBPUSD" && x.EndingPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Commit_mutates_only_paper_ledger_fixture_state()
    {
        var result = await CommitAsync();

        Assert.True(result.State.PaperOnly);
        Assert.True(result.State.NoExternal);
        Assert.True(result.State.FixtureState);
        Assert.True(result.State.NotProductionLedger);
        Assert.True(result.State.NotBrokerPosition);
        Assert.True(result.State.NotTradingState);
    }

    [Fact]
    public async Task Live_position_mutation_count_remains_zero()
    {
        var result = await CommitAsync();

        Assert.Equal(0, result.State.Summary.LivePositionMutationCount);
        Assert.False(result.State.LivePositionStateMutated);
    }

    [Fact]
    public async Task Broker_position_mutation_count_remains_zero()
    {
        var result = await CommitAsync();

        Assert.Equal(0, result.State.Summary.BrokerPositionMutationCount);
        Assert.False(result.State.BrokerPositionStateMutated);
    }

    [Fact]
    public async Task Production_ledger_mutation_count_remains_zero()
    {
        var result = await CommitAsync();

        Assert.Equal(0, result.State.Summary.ProductionLedgerMutationCount);
        Assert.False(result.State.ProductionLedgerStateMutated);
    }

    [Fact]
    public async Task Trading_state_mutation_count_remains_zero()
    {
        var result = await CommitAsync();

        Assert.Equal(0, result.State.Summary.TradingStateMutationCount);
        Assert.False(result.State.TradingStateMutated);
    }

    [Fact]
    public async Task Order_fill_and_execution_report_counts_remain_zero()
    {
        var result = await CommitAsync();

        Assert.Equal(0, result.State.Summary.OrderCount);
        Assert.Equal(0, result.State.Summary.FillCount);
        Assert.Equal(0, result.State.Summary.ExecutionReportCount);
        Assert.Equal(0, result.State.Summary.BrokerRouteCount);
    }

    [Fact]
    public async Task Duplicate_commit_is_idempotent()
    {
        var archive = CreateArchive();
        var approval = CreateApproval(archive);
        var repository = new InMemoryPaperLedgerStateRepository();
        var service = new PaperLedgerCommitService(repository, new FixedClock(ProducedAt));
        var commitId = new PaperLedgerCommitId("paper-ledger-commit-r024-sample");

        var first = await service.CommitAsync(archive, approval, commitId, CancellationToken.None);
        var second = await service.CommitAsync(archive, approval, commitId, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second.AlreadyCommitted);
        Assert.Equal(PaperLedgerCommitStatus.DuplicateReturned, second.State.CommitStatus);
    }

    [Fact]
    public async Task Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var result = await CommitAsync();

        Assert.True(result.State.QubesLineagePreserved);
        Assert.True(result.State.CycleLineagePreserved);
        Assert.True(result.State.OperatorDecisionLineagePreserved);
        Assert.True(result.State.LedgerPreviewLineagePreserved);
        Assert.True(result.State.PositionPreviewLineagePreserved);
        Assert.True(result.State.SimulationResultLineagePreserved);
        Assert.True(result.State.SimulationPlanLineagePreserved);
        Assert.True(result.State.PaperExecutionPlanLineagePreserved);
        Assert.True(result.State.PaperCandidateLineagePreserved);
        Assert.True(result.State.RiskLineagePreserved);
        Assert.True(result.State.RebalanceIntentLineagePreserved);
        Assert.True(result.State.LotSizingLineagePreserved);
    }

    [Fact]
    public void Commit_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
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
    public void Commit_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_oms_or_broker_orders_are_created()
    {
        var result = await CommitAsync();

        Assert.False(result.State.OmsOrderCreated);
        Assert.False(result.State.ParentOrderCreated);
        Assert.False(result.State.ChildOrderCreated);
        Assert.False(result.State.BrokerOrderCreated);
        Assert.False(result.State.OrderStateCreated);
    }

    [Fact]
    public async Task No_fills_or_execution_reports_are_created()
    {
        var result = await CommitAsync();

        Assert.False(result.State.FillCreated);
        Assert.False(result.State.ExecutionReportCreated);
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

    private static async Task<PaperLedgerCommitResult> CommitAsync()
    {
        var archive = CreateArchive();
        return await CommitAsync(archive, CreateApproval(archive));
    }

    private static async Task<PaperLedgerCommitResult> CommitAsync(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessDecision approval)
        => await new PaperLedgerCommitService(
                new InMemoryPaperLedgerStateRepository(),
                new FixedClock(ProducedAt))
            .CommitAsync(
                archive,
                approval,
                new PaperLedgerCommitId("paper-ledger-commit-r024-sample"),
                CancellationToken.None);

    private static PaperLedgerPreviewRecord CreateArchive()
    {
        var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r024-sample");
        var positionPreviewId = new PaperPositionPreviewId("paper-position-preview-r024-sample");
        var simulationResultId = new PaperSimulationResultId("paper-simulation-result-r024-sample");
        var simulationPlanId = new PaperSimulationPlanId("paper-simulation-plan-r024-sample");
        var executionPlanId = new PaperExecutionPlanId("paper-execution-plan-r024-sample");
        var operatorDecisionId = new OperatorDecisionId("decision-r024-approve-ledger-commit-readiness");
        var lines = new[]
        {
            Line(previewId, positionPreviewId, simulationResultId, operatorDecisionId, "AUDUSD", "AUD", 131000m),
            Line(previewId, positionPreviewId, simulationResultId, operatorDecisionId, "EURUSD", "EUR", 124000m),
            Line(previewId, positionPreviewId, simulationResultId, operatorDecisionId, "GBPUSD", "GBP", -368000m)
        };

        return new PaperLedgerPreviewRecord(
            new PaperLedgerPreviewArchiveId("paper-ledger-preview-r024-sample:archive"),
            previewId,
            positionPreviewId,
            simulationResultId,
            simulationPlanId,
            executionPlanId,
            "cycle-r024-sample",
            "qubes-r024-sample",
            operatorDecisionId,
            PaperPositionLedgerPreviewStatus.PaperLedgerPreviewReady,
            ProducedAt,
            PreviewLineCount: 3,
            BlockedLineCount: 10,
            SafetyStatus: "PaperLedgerPreviewOnly",
            PaperLedgerPreviewArchiveStatus.ArchivedNoExternal,
            lines,
            Array.Empty<BlockedPaperReviewLineRecord>(),
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            OperatorDecisionLineagePreserved: true,
            PositionPreviewLineagePreserved: true,
            LedgerPreviewLineagePreserved: true,
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
            SimulatedOnly: true,
            PreviewOnly: true,
            NoExternal: true,
            NoPaperLedgerCommit: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            LivePositionMutationCount: 0,
            BrokerPositionMutationCount: 0,
            ProductionLedgerMutationCount: 0,
            TradingStateMutationCount: 0,
            PaperLedgerStateCommitted: false,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            ProductionLedgerStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }

    private static PaperLedgerPreviewLineRecord Line(
        PaperPositionLedgerPreviewId previewId,
        PaperPositionPreviewId positionPreviewId,
        PaperSimulationResultId simulationResultId,
        OperatorDecisionId operatorDecisionId,
        string symbol,
        string quantityCurrency,
        decimal delta)
        => new(
            new PaperPositionLedgerPreviewLineId($"{previewId.Value}:{symbol}:line"),
            positionPreviewId,
            simulationResultId,
            "cycle-r024-sample",
            "qubes-r024-sample",
            operatorDecisionId,
            InstrumentId.New(),
            symbol,
            StartingPaperQuantity: 0m,
            SimulatedDeltaQuantity: delta,
            PreviewEndingPaperQuantity: delta,
            quantityCurrency,
            PaperPositionLedgerPreviewStatus.PaperLedgerPreviewApplied,
            $"paperLedgerPreviewLine={symbol}",
            PaperOnly: true,
            PreviewOnly: true,
            NoPaperLedgerCommit: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true);

    private static PaperLedgerCommitReadinessDecision CreateApproval(PaperLedgerPreviewRecord archive)
        => new(
            archive.PaperPositionLedgerPreviewId,
            archive.PaperPositionPreviewId,
            archive.PaperSimulationResultId,
            archive.PaperSimulationPlanId,
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            archive.OperatorDecisionId,
            ProducedAt,
            "operator-placeholder",
            PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness,
            PaperLedgerCommitDecisionStatus.Recorded,
            PaperLedgerCommitReadinessReasonCategory.AcceptedForPaperLedgerCommitReadiness,
            "Approved for paper ledger fixture state commit only.",
            PaperLedgerCommitReadinessStatus.PaperLedgerCommitReadyNoExternal,
            BlockedLinesAcknowledged: true,
            MissingStaleMarksAcknowledged: true,
            DriftAcknowledged: true,
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            OperatorDecisionLineagePreserved: true,
            PositionPreviewLineagePreserved: true,
            LedgerPreviewLineagePreserved: true,
            SimulationResultLineagePreserved: true,
            SimulationPlanLineagePreserved: true,
            PaperExecutionPlanLineagePreserved: true,
            PaperCandidateLineagePreserved: true,
            RiskLineagePreserved: true,
            RebalanceIntentLineagePreserved: true,
            LotSizingLineagePreserved: true,
            NoPaperLedgerCommit: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            CreatesFill: false,
            CreatesExecutionReport: false,
            CreatesOmsOrder: false,
            CreatesBrokerOrder: false,
            SubmitsOrders: false,
            CallsBrokerGateway: false,
            RequestsLiveMarketData: false,
            StartsApiOrWorker: false,
            StartsSchedulerOrBackgroundJob: false,
            GateAccepted: true,
            GateMessage: "Paper ledger commit readiness recorded; no paper ledger state was committed.");

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateCommit.cs");

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
