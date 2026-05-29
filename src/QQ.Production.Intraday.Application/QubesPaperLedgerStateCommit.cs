using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperLedgerCommitStatus
{
    PaperLedgerCommittedNoExternal,
    DuplicateReturned,
    RejectedInvalidPreview,
    RejectedMissingApproval,
    InconclusiveSafe
}

public enum PaperLedgerSafetyStatus
{
    PaperOnly,
    NoExternal,
    FixtureState,
    NotProductionLedger,
    NotBrokerPosition,
    NotTradingState
}

public sealed record PaperLedgerCommitId(string Value);

public sealed record PaperLedgerStateId(string Value);

public sealed record PaperLedgerStateLineId(string Value);

public sealed record PaperLedgerStateLine(
    PaperLedgerStateLineId PaperLedgerStateLineId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    InstrumentId InstrumentId,
    string CurrencyOrSymbol,
    string QuantityCurrency,
    decimal StartingPaperQuantity,
    decimal AppliedPaperDeltaQuantity,
    decimal EndingPaperQuantity,
    PaperLedgerCommitStatus CommitStatus,
    string SafetyStatus,
    string SourceLineageReference,
    bool PaperOnly,
    bool NoExternal,
    bool FixtureState,
    bool NotProductionLedger,
    bool NotBrokerPosition,
    bool NotTradingState,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted);

public sealed record PaperLedgerCommitSummary(
    int AppliedLineCount,
    int BlockedLineCount,
    int LivePositionMutationCount,
    int BrokerPositionMutationCount,
    int ProductionLedgerMutationCount,
    int TradingStateMutationCount,
    int OrderCount,
    int FillCount,
    int ExecutionReportCount,
    int BrokerRouteCount,
    string SafetyStatus);

public sealed record PaperLedgerState(
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset CreatedAtUtc,
    PaperLedgerCommitStatus CommitStatus,
    string SafetyStatus,
    IReadOnlyList<PaperLedgerStateLine> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    PaperLedgerCommitSummary Summary,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool LedgerPreviewLineagePreserved,
    bool PositionPreviewLineagePreserved,
    bool SimulationResultLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool PaperOnly,
    bool NoExternal,
    bool FixtureState,
    bool NotProductionLedger,
    bool NotBrokerPosition,
    bool NotTradingState,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool ProductionLedgerStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool ParentOrderCreated,
    bool ChildOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool BrokerRouteCreated);

public sealed record PaperLedgerCommitResult(
    PaperLedgerState State,
    bool Persisted,
    bool AlreadyCommitted);

public interface IPaperLedgerStateRepository
{
    Task<PaperLedgerState?> GetByCommitIdAsync(
        PaperLedgerCommitId commitId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperLedgerState state, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperLedgerStateRepository : IPaperLedgerStateRepository
{
    private readonly List<PaperLedgerState> states = [];

    public Task<PaperLedgerState?> GetByCommitIdAsync(
        PaperLedgerCommitId commitId,
        CancellationToken cancellationToken)
        => Task.FromResult(states.FirstOrDefault(x => x.PaperLedgerCommitId == commitId));

    public Task AddAsync(PaperLedgerState state, CancellationToken cancellationToken)
    {
        if (states.Any(x => x.PaperLedgerCommitId == state.PaperLedgerCommitId))
        {
            return Task.CompletedTask;
        }

        states.Add(state);
        return Task.CompletedTask;
    }
}

public sealed class PaperLedgerCommitService(
    IPaperLedgerStateRepository repository,
    IClock clock)
{
    public async Task<PaperLedgerCommitResult> CommitAsync(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessDecision approval,
        PaperLedgerCommitId commitId,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByCommitIdAsync(commitId, cancellationToken);
        if (existing is not null)
        {
            return new PaperLedgerCommitResult(
                existing with { CommitStatus = PaperLedgerCommitStatus.DuplicateReturned },
                Persisted: false,
                AlreadyCommitted: true);
        }

        var state = CreateState(archive, approval, commitId, clock.UtcNow);
        if (state.CommitStatus is PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal)
        {
            await repository.AddAsync(state, cancellationToken);
        }

        return new PaperLedgerCommitResult(
            state,
            Persisted: state.CommitStatus is PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
            AlreadyCommitted: false);
    }

    private static PaperLedgerState CreateState(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessDecision approval,
        PaperLedgerCommitId commitId,
        DateTimeOffset createdAtUtc)
    {
        var status = EvaluateStatus(archive, approval);
        var stateId = new PaperLedgerStateId($"{commitId.Value}:paper-ledger-state");
        var lines = status is PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal
            ? archive.Lines
                .OrderBy(x => x.CurrencyOrSymbol, StringComparer.OrdinalIgnoreCase)
                .Select(x => CreateLine(stateId, commitId, archive, x, status))
                .ToArray()
            : [];
        var appliedLineCount = lines.Count(x => x.CommitStatus is PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal);
        var summary = new PaperLedgerCommitSummary(
            appliedLineCount,
            archive.BlockedLineCount,
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
            archive.PaperPositionLedgerPreviewId,
            archive.PaperPositionPreviewId,
            archive.PaperSimulationResultId,
            archive.PaperSimulationPlanId,
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            approval.OperatorDecisionId,
            createdAtUtc,
            status,
            "PaperLedgerFixtureStateOnly",
            lines,
            archive.BlockedLines,
            summary,
            archive.QubesLineagePreserved,
            archive.CycleLineagePreserved,
            archive.OperatorDecisionLineagePreserved && approval.OperatorDecisionLineagePreserved,
            archive.LedgerPreviewLineagePreserved,
            archive.PositionPreviewLineagePreserved,
            archive.SimulationResultLineagePreserved,
            archive.SimulationPlanLineagePreserved,
            archive.PaperExecutionPlanLineagePreserved,
            archive.PaperCandidateLineagePreserved,
            archive.RiskLineagePreserved,
            archive.RebalanceIntentLineagePreserved,
            archive.LotSizingLineagePreserved,
            archive.MissingStaleMarkWarningsPreserved,
            archive.DriftAcknowledgementPreserved,
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

    private static PaperLedgerCommitStatus EvaluateStatus(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessDecision approval)
    {
        if (archive.ArchiveStatus is not PaperLedgerPreviewArchiveStatus.ArchivedNoExternal ||
            archive.SafetyStatus != "PaperLedgerPreviewOnly" ||
            !archive.PaperOnly ||
            !archive.SimulatedOnly ||
            !archive.PreviewOnly ||
            !archive.NoExternal)
        {
            return PaperLedgerCommitStatus.RejectedInvalidPreview;
        }

        if (approval.DecisionType is not PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness ||
            approval.DecisionStatus is not PaperLedgerCommitDecisionStatus.Recorded ||
            approval.ResultingReadinessStatus is not PaperLedgerCommitReadinessStatus.PaperLedgerCommitReadyNoExternal ||
            !approval.GateAccepted)
        {
            return PaperLedgerCommitStatus.RejectedMissingApproval;
        }

        if (archive.LivePositionMutationCount != 0 ||
            archive.BrokerPositionMutationCount != 0 ||
            archive.ProductionLedgerMutationCount != 0 ||
            archive.TradingStateMutationCount != 0 ||
            archive.LivePositionStateMutated ||
            archive.BrokerPositionStateMutated ||
            archive.ProductionLedgerStateMutated ||
            archive.TradingStateMutated ||
            archive.FillCreated ||
            archive.ExecutionReportCreated ||
            archive.OmsOrderCreated ||
            archive.BrokerOrderCreated ||
            archive.OrderStateCreated ||
            archive.SubmittedOrders ||
            archive.BrokerRouteCreated)
        {
            return PaperLedgerCommitStatus.InconclusiveSafe;
        }

        return PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal;
    }

    private static PaperLedgerStateLine CreateLine(
        PaperLedgerStateId stateId,
        PaperLedgerCommitId commitId,
        PaperLedgerPreviewRecord archive,
        PaperLedgerPreviewLineRecord line,
        PaperLedgerCommitStatus status)
        => new(
            new PaperLedgerStateLineId($"{stateId.Value}:{line.CurrencyOrSymbol}:line"),
            stateId,
            commitId,
            archive.PaperPositionLedgerPreviewId,
            archive.CycleRunId,
            archive.QubesRunId,
            archive.OperatorDecisionId,
            line.InstrumentId,
            line.CurrencyOrSymbol,
            line.QuantityCurrency,
            line.StartingPaperQuantity,
            line.SimulatedDeltaQuantity,
            line.PreviewEndingPaperQuantity,
            status,
            "PaperLedgerFixtureStateOnly",
            line.SourceLineageReference,
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
}
