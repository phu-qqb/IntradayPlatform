using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public enum PaperExecutionPlanOperatorAction
{
    ApproveForPaperSimulation,
    Hold,
    Reject,
    RequestPlanFix,
    RequestRiskReview
}

public enum PaperExecutionPlanApprovalStatus
{
    Recorded,
    RejectedByGate,
    DuplicateReturned,
    InconclusiveSafe
}

public enum PaperSimulationReadinessStatus
{
    SimulationReadyNoExternal,
    HeldForMissingMarks,
    HeldForDrift,
    HeldForBlockedLines,
    HeldForRiskReview,
    Rejected,
    InconclusiveSafe
}

public enum PaperPlanApprovalReasonCategory
{
    AcceptedForSimulationReadiness,
    HeldDueToBlockedLines,
    HeldDueToMissingMarks,
    HeldDueToDrift,
    HeldDueToRiskReview,
    RejectedDueToExecutablePlan,
    RejectedDueToSubmittedPlan,
    RejectedDueToValidationFailure,
    InconclusiveSafe
}

public sealed record PaperExecutionPlanApprovalRequest(
    OperatorDecisionId OperatorDecisionId,
    PaperExecutionPlanOperatorAction DecisionType,
    string ReviewedBy,
    PaperPlanApprovalReasonCategory ReasonCategory,
    string? CommentSanitized,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged);

public sealed record PaperExecutionPlanApprovalDecision(
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    DateTimeOffset ReviewedAtUtc,
    string ReviewedBy,
    PaperExecutionPlanOperatorAction DecisionType,
    PaperExecutionPlanApprovalStatus DecisionStatus,
    PaperPlanApprovalReasonCategory ReasonCategory,
    string? CommentSanitized,
    PaperSimulationReadinessStatus ResultingSimulationReadinessStatus,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged,
    bool QubesLineagePreserved,
    bool PlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool SimulationReadinessOnly,
    bool PaperOnly,
    bool NoExternal,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool EnablesLiveTrading,
    bool StartsApiOrWorker,
    bool StartsSchedulerOrBackgroundJob,
    bool CallsBrokerGateway,
    bool RequestsLiveMarketData,
    bool CreatesExecutableOrder,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool SubmitsOrders,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool RunsPaperSimulation,
    bool CreatesSimulationFill,
    bool CreatesSimulationExecutionReport,
    bool MutatesLiveTradingState,
    bool GateAccepted,
    string GateMessage);

public sealed record PaperExecutionPlanApprovalResult(
    PaperExecutionPlanApprovalDecision Decision,
    PaperExecutionPlanBatchRecord ArchiveRecord,
    bool Persisted,
    bool AlreadyRecorded);

public interface IPaperExecutionPlanApprovalRepository
{
    Task<PaperExecutionPlanApprovalDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperExecutionPlanApprovalDecision decision, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperExecutionPlanApprovalRepository : IPaperExecutionPlanApprovalRepository
{
    private readonly List<PaperExecutionPlanApprovalDecision> decisions = [];

    public Task<PaperExecutionPlanApprovalDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken)
        => Task.FromResult(decisions.FirstOrDefault(x => x.OperatorDecisionId == decisionId));

    public Task AddAsync(PaperExecutionPlanApprovalDecision decision, CancellationToken cancellationToken)
    {
        if (decisions.Any(x => x.OperatorDecisionId == decision.OperatorDecisionId))
        {
            return Task.CompletedTask;
        }

        decisions.Add(decision);
        return Task.CompletedTask;
    }
}

public sealed class PaperExecutionPlanApprovalService(
    IPaperExecutionPlanApprovalRepository repository,
    IClock clock)
{
    public async Task<PaperExecutionPlanApprovalResult> ReviewAsync(
        PaperExecutionPlanBatchRecord archive,
        PaperExecutionPlanApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByDecisionIdAsync(request.OperatorDecisionId, cancellationToken);
        if (existing is not null)
        {
            return new PaperExecutionPlanApprovalResult(
                existing with { DecisionStatus = PaperExecutionPlanApprovalStatus.DuplicateReturned },
                archive,
                Persisted: false,
                AlreadyRecorded: true);
        }

        var decision = CreateDecision(archive, request, clock.UtcNow);
        if (decision.GateAccepted)
        {
            await repository.AddAsync(decision, cancellationToken);
            return new PaperExecutionPlanApprovalResult(decision, archive, Persisted: true, AlreadyRecorded: false);
        }

        return new PaperExecutionPlanApprovalResult(decision, archive, Persisted: false, AlreadyRecorded: false);
    }

    private static PaperExecutionPlanApprovalDecision CreateDecision(
        PaperExecutionPlanBatchRecord archive,
        PaperExecutionPlanApprovalRequest request,
        DateTimeOffset reviewedAtUtc)
    {
        var gate = EvaluateGate(archive, request);
        var reviewedBy = SanitizeReviewedBy(request.ReviewedBy);
        var comment = string.IsNullOrWhiteSpace(request.CommentSanitized)
            ? null
            : SanitizeComment(request.CommentSanitized);

        return new PaperExecutionPlanApprovalDecision(
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            request.OperatorDecisionId,
            archive.PaperOrderCandidateBatchId,
            reviewedAtUtc,
            reviewedBy,
            request.DecisionType,
            gate.Accepted ? PaperExecutionPlanApprovalStatus.Recorded : PaperExecutionPlanApprovalStatus.RejectedByGate,
            request.ReasonCategory,
            comment,
            gate.ReadinessStatus,
            request.BlockedLinesAcknowledged,
            request.MissingStaleMarksAcknowledged,
            request.DriftAcknowledged,
            archive.QubesLineagePreserved,
            true,
            archive.PaperCandidateLineagePreserved,
            archive.RiskLineagePreserved,
            archive.RebalanceIntentLineagePreserved,
            archive.LotSizingLineagePreserved,
            archive.MissingStaleMarkWarningsPreserved,
            archive.DriftAcknowledgementPreserved,
            request.DecisionType == PaperExecutionPlanOperatorAction.ApproveForPaperSimulation && gate.ReadinessStatus == PaperSimulationReadinessStatus.SimulationReadyNoExternal,
            archive.PlanModes.Contains(PaperExecutionPlanMode.PaperOnly),
            archive.NoExternal,
            archive.PlanModes.Contains(PaperExecutionPlanMode.NonExecutable),
            archive.PlanLines.All(x => x.NotAnOrder),
            archive.PlanLines.All(x => x.NotSubmitted),
            archive.PlanLines.All(x => x.NoBrokerRoute),
            EnablesLiveTrading: false,
            StartsApiOrWorker: false,
            StartsSchedulerOrBackgroundJob: false,
            CallsBrokerGateway: false,
            RequestsLiveMarketData: false,
            CreatesExecutableOrder: false,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            SubmitsOrders: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            RunsPaperSimulation: false,
            CreatesSimulationFill: false,
            CreatesSimulationExecutionReport: false,
            MutatesLiveTradingState: false,
            gate.Accepted,
            gate.Message);
    }

    private static (bool Accepted, PaperSimulationReadinessStatus ReadinessStatus, string Message) EvaluateGate(
        PaperExecutionPlanBatchRecord archive,
        PaperExecutionPlanApprovalRequest request)
    {
        if (archive.ArchiveStatus is PaperExecutionPlanArchiveStatus.RejectedInvalidPlan)
        {
            return (false, PaperSimulationReadinessStatus.Rejected, "Rejected invalid archived plan; simulation readiness not granted.");
        }

        if (!archive.PlanModes.Contains(PaperExecutionPlanMode.PaperOnly) ||
            !archive.PlanModes.Contains(PaperExecutionPlanMode.NoExternal) ||
            !archive.PlanModes.Contains(PaperExecutionPlanMode.NonExecutable) ||
            !archive.NoExternal)
        {
            return (false, PaperSimulationReadinessStatus.InconclusiveSafe, "Archived plan is not paper-only/no-external/non-executable.");
        }

        if (archive.CreatedOmsOrder || archive.CreatedParentOrder || archive.CreatedChildOrder || archive.CreatedBrokerOrder)
        {
            return (false, PaperSimulationReadinessStatus.Rejected, "Plan created an order-like record and is rejected safely.");
        }

        if (archive.SubmittedOrders || archive.PlanLines.Any(x => !x.NotSubmitted || !x.NoBrokerRoute))
        {
            return (false, PaperSimulationReadinessStatus.Rejected, "Submitted or routed plan is rejected safely.");
        }

        if (archive.CreatedFill || archive.CreatedExecutionReport)
        {
            return (false, PaperSimulationReadinessStatus.Rejected, "Plan created fills or execution reports and is rejected safely.");
        }

        if (archive.PlanLines.Any(x => !x.PaperOnly || !x.NonExecutable || !x.NotAnOrder))
        {
            return (false, PaperSimulationReadinessStatus.Rejected, "Executable or order-like plan line is rejected safely.");
        }

        if (request.DecisionType == PaperExecutionPlanOperatorAction.ApproveForPaperSimulation &&
            archive.BlockedLineCount > 0 &&
            !request.BlockedLinesAcknowledged)
        {
            return (false, PaperSimulationReadinessStatus.HeldForBlockedLines, "Blocked lines must be acknowledged before simulation readiness.");
        }

        if (request.DecisionType == PaperExecutionPlanOperatorAction.ApproveForPaperSimulation &&
            archive.MissingStaleMarkWarningsPreserved &&
            !request.MissingStaleMarksAcknowledged)
        {
            return (false, PaperSimulationReadinessStatus.HeldForMissingMarks, "Missing/stale mark warnings must be acknowledged before simulation readiness.");
        }

        if (request.DecisionType == PaperExecutionPlanOperatorAction.ApproveForPaperSimulation &&
            archive.DriftAcknowledgementPreserved &&
            !request.DriftAcknowledged)
        {
            return (false, PaperSimulationReadinessStatus.HeldForDrift, "Drift acknowledgement is required before simulation readiness.");
        }

        return request.DecisionType switch
        {
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation => (
                true,
                PaperSimulationReadinessStatus.SimulationReadyNoExternal,
                "Approved for no-external simulation readiness only; no simulation, orders, routes, fills, or execution reports are created."),
            PaperExecutionPlanOperatorAction.Hold => (
                true,
                archive.MissingStaleMarkWarningsPreserved
                    ? PaperSimulationReadinessStatus.HeldForMissingMarks
                    : PaperSimulationReadinessStatus.HeldForBlockedLines,
                "Plan held no-externally for operator follow-up."),
            PaperExecutionPlanOperatorAction.Reject => (
                true,
                PaperSimulationReadinessStatus.Rejected,
                "Plan rejected no-externally; no simulation or execution path created."),
            PaperExecutionPlanOperatorAction.RequestPlanFix => (
                true,
                PaperSimulationReadinessStatus.InconclusiveSafe,
                "Plan fix requested no-externally; no scheduler, replay, or simulation is started."),
            PaperExecutionPlanOperatorAction.RequestRiskReview => (
                true,
                PaperSimulationReadinessStatus.HeldForRiskReview,
                "Risk review requested no-externally; no order or simulation path is created."),
            _ => (
                false,
                PaperSimulationReadinessStatus.InconclusiveSafe,
                "Unknown paper plan decision rejected safely.")
        };
    }

    private static string SanitizeReviewedBy(string reviewedBy)
    {
        var value = string.IsNullOrWhiteSpace(reviewedBy) ? "operator-placeholder" : reviewedBy.Trim();
        return value.Length <= 64 ? value : value[..64];
    }

    private static string SanitizeComment(string comment)
    {
        var sanitized = comment
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }
}
