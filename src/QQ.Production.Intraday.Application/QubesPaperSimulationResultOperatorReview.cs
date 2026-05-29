using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperSimulationResultDecisionType
{
    ApprovePaperSimulationResult,
    Hold,
    Reject,
    RequestSimulationFix,
    RequestRiskReview,
    PromoteToPaperPositionPreview
}

public enum PaperSimulationResultDecisionStatus
{
    Recorded,
    RejectedByGate,
    DuplicateReturned,
    InconclusiveSafe
}

public enum PaperPositionPreviewStatus
{
    PaperPositionPreviewReady,
    HeldForBlockedLines,
    HeldForMissingMarks,
    HeldForRiskReview,
    Rejected,
    InconclusiveSafe
}

public enum PaperSimulationResultApprovalReasonCategory
{
    AcceptedForPaperPositionPreview,
    HeldDueToBlockedLines,
    HeldDueToMissingMarks,
    HeldDueToDrift,
    HeldDueToRiskReview,
    RejectedDueToSimulationFailure,
    RejectedDueToRealFillRisk,
    RejectedDueToValidationFailure,
    InconclusiveSafe
}

public sealed record PaperSimulationResultOperatorReviewRequest(
    OperatorDecisionId OperatorDecisionId,
    PaperSimulationResultDecisionType DecisionType,
    string ReviewedBy,
    PaperSimulationResultApprovalReasonCategory ReasonCategory,
    string? CommentSanitized,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged);

public sealed record PaperPositionPreviewId(string Value);

public sealed record PaperPositionPreviewLineId(string Value);

public sealed record PaperPositionPreviewLine(
    PaperPositionPreviewLineId PaperPositionPreviewLineId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultLineId PaperSimulationResultLineId,
    PaperSimulationResultId PaperSimulationResultId,
    string CycleRunId,
    string QubesRunId,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    decimal? SimulatedPositionDelta,
    PaperPositionPreviewStatus PreviewStatus,
    string SourceLineageReference,
    bool PaperOnly,
    bool SimulatedOnly,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated);

public sealed record PaperPositionPreview(
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset CreatedAtUtc,
    PaperPositionPreviewStatus PreviewStatus,
    IReadOnlyList<PaperPositionPreviewLine> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
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
    bool SimulatedOnly,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders);

public sealed record PaperSimulationResultApprovalDecision(
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset ReviewedAtUtc,
    string ReviewedBy,
    PaperSimulationResultDecisionType DecisionType,
    PaperSimulationResultDecisionStatus DecisionStatus,
    PaperSimulationResultApprovalReasonCategory ReasonCategory,
    string? CommentSanitized,
    PaperPositionPreviewStatus ResultingPaperPositionPreviewStatus,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool SimulationResultLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool PaperPositionPreviewOnly,
    bool PaperOnly,
    bool NoExternal,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool CreatesOmsOrder,
    bool CreatesBrokerOrder,
    bool CreatesOrderState,
    bool SubmitsOrders,
    bool CallsBrokerGateway,
    bool RequestsLiveMarketData,
    bool StartsApiOrWorker,
    bool StartsSchedulerOrBackgroundJob,
    bool MutatesLiveTradingState,
    bool MutatesLivePositionState,
    bool MutatesBrokerPositionState,
    bool GateAccepted,
    string GateMessage);

public sealed record PaperSimulationResultOperatorReviewResult(
    PaperSimulationResultApprovalDecision Decision,
    PaperSimulationResultRecord ArchiveRecord,
    PaperPositionPreview? PaperPositionPreview,
    bool Persisted,
    bool AlreadyRecorded);

public interface IPaperSimulationResultOperatorDecisionRepository
{
    Task<PaperSimulationResultApprovalDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperSimulationResultApprovalDecision decision, CancellationToken cancellationToken);
}

public interface IPaperPositionPreviewRepository
{
    Task<PaperPositionPreview?> GetByPreviewIdAsync(
        PaperPositionPreviewId previewId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperPositionPreview preview, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperSimulationResultOperatorDecisionRepository : IPaperSimulationResultOperatorDecisionRepository
{
    private readonly List<PaperSimulationResultApprovalDecision> decisions = [];

    public Task<PaperSimulationResultApprovalDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken)
        => Task.FromResult(decisions.FirstOrDefault(x => x.OperatorDecisionId == decisionId));

    public Task AddAsync(PaperSimulationResultApprovalDecision decision, CancellationToken cancellationToken)
    {
        if (decisions.Any(x => x.OperatorDecisionId == decision.OperatorDecisionId))
        {
            return Task.CompletedTask;
        }

        decisions.Add(decision);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPaperPositionPreviewRepository : IPaperPositionPreviewRepository
{
    private readonly List<PaperPositionPreview> previews = [];

    public Task<PaperPositionPreview?> GetByPreviewIdAsync(
        PaperPositionPreviewId previewId,
        CancellationToken cancellationToken)
        => Task.FromResult(previews.FirstOrDefault(x => x.PaperPositionPreviewId == previewId));

    public Task AddAsync(PaperPositionPreview preview, CancellationToken cancellationToken)
    {
        if (previews.Any(x => x.PaperPositionPreviewId == preview.PaperPositionPreviewId))
        {
            return Task.CompletedTask;
        }

        previews.Add(preview);
        return Task.CompletedTask;
    }
}

public sealed class PaperSimulationResultOperatorReviewService(
    IPaperSimulationResultOperatorDecisionRepository decisionRepository,
    IPaperPositionPreviewRepository previewRepository,
    IClock clock)
{
    public async Task<PaperSimulationResultOperatorReviewResult> ReviewAsync(
        PaperSimulationResultRecord archive,
        PaperSimulationResultOperatorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await decisionRepository.GetByDecisionIdAsync(request.OperatorDecisionId, cancellationToken);
        var previewId = CreatePreviewId(archive, request.OperatorDecisionId);
        if (existing is not null)
        {
            return new PaperSimulationResultOperatorReviewResult(
                existing with { DecisionStatus = PaperSimulationResultDecisionStatus.DuplicateReturned },
                archive,
                await previewRepository.GetByPreviewIdAsync(previewId, cancellationToken),
                Persisted: false,
                AlreadyRecorded: true);
        }

        var decision = CreateDecision(archive, request, clock.UtcNow);
        PaperPositionPreview? preview = null;
        if (decision.GateAccepted)
        {
            await decisionRepository.AddAsync(decision, cancellationToken);
            if (decision.DecisionType == PaperSimulationResultDecisionType.PromoteToPaperPositionPreview &&
                decision.ResultingPaperPositionPreviewStatus == PaperPositionPreviewStatus.PaperPositionPreviewReady)
            {
                preview = await previewRepository.GetByPreviewIdAsync(previewId, cancellationToken);
                if (preview is null)
                {
                    preview = CreatePreview(previewId, archive, decision, clock.UtcNow);
                    await previewRepository.AddAsync(preview, cancellationToken);
                }
            }
        }

        return new PaperSimulationResultOperatorReviewResult(
            decision,
            archive,
            preview,
            Persisted: decision.GateAccepted,
            AlreadyRecorded: false);
    }

    private static PaperPositionPreviewId CreatePreviewId(
        PaperSimulationResultRecord archive,
        OperatorDecisionId decisionId)
        => new($"{archive.PaperSimulationResultId.Value}:{decisionId.Value}:paper-position-preview");

    private static PaperSimulationResultApprovalDecision CreateDecision(
        PaperSimulationResultRecord archive,
        PaperSimulationResultOperatorReviewRequest request,
        DateTimeOffset reviewedAtUtc)
    {
        var gate = EvaluateGate(archive, request);
        return new PaperSimulationResultApprovalDecision(
            archive.PaperSimulationResultId,
            archive.PaperSimulationPlanId,
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            request.OperatorDecisionId,
            reviewedAtUtc,
            SanitizeReviewedBy(request.ReviewedBy),
            request.DecisionType,
            gate.Accepted ? PaperSimulationResultDecisionStatus.Recorded : PaperSimulationResultDecisionStatus.RejectedByGate,
            request.ReasonCategory,
            string.IsNullOrWhiteSpace(request.CommentSanitized) ? null : SanitizeComment(request.CommentSanitized),
            gate.PreviewStatus,
            request.BlockedLinesAcknowledged,
            request.MissingStaleMarksAcknowledged,
            request.DriftAcknowledged,
            archive.QubesLineagePreserved,
            archive.CycleLineagePreserved,
            true,
            archive.SimulationPlanLineagePreserved,
            archive.PaperExecutionPlanLineagePreserved,
            archive.PaperCandidateLineagePreserved,
            archive.RiskLineagePreserved,
            archive.RebalanceIntentLineagePreserved,
            archive.LotSizingLineagePreserved,
            request.DecisionType == PaperSimulationResultDecisionType.PromoteToPaperPositionPreview &&
                gate.PreviewStatus == PaperPositionPreviewStatus.PaperPositionPreviewReady,
            archive.PaperOnly,
            archive.NoExternal,
            archive.NonExecutable,
            archive.NotAnOrder,
            archive.NotSubmitted,
            archive.NoBrokerRoute,
            CreatesFill: false,
            CreatesExecutionReport: false,
            CreatesOmsOrder: false,
            CreatesBrokerOrder: false,
            CreatesOrderState: false,
            SubmitsOrders: false,
            CallsBrokerGateway: false,
            RequestsLiveMarketData: false,
            StartsApiOrWorker: false,
            StartsSchedulerOrBackgroundJob: false,
            MutatesLiveTradingState: false,
            MutatesLivePositionState: false,
            MutatesBrokerPositionState: false,
            gate.Accepted,
            gate.Message);
    }

    private static (bool Accepted, PaperPositionPreviewStatus PreviewStatus, string Message) EvaluateGate(
        PaperSimulationResultRecord archive,
        PaperSimulationResultOperatorReviewRequest request)
    {
        if (archive.ArchiveStatus is PaperSimulationResultArchiveStatus.RejectedInvalidResult)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Rejected invalid paper simulation result archive.");
        }

        if (archive.SafetyStatus != "PaperSimulationOnly" ||
            !archive.PaperOnly ||
            !archive.NoExternal ||
            !archive.NonExecutable ||
            !archive.NotAnOrder ||
            !archive.NotSubmitted ||
            !archive.NoBrokerRoute)
        {
            return (false, PaperPositionPreviewStatus.InconclusiveSafe, "Archived result is not paper-only/no-external/non-executable.");
        }

        if (archive.RealFillCount != 0 || archive.RealFillEntityCreated)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Archived result has real fill risk.");
        }

        if (archive.ExecutionReportCount != 0 || archive.BrokerExecutionReportEntityCreated)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Archived result has execution report risk.");
        }

        if (archive.OrderCount != 0 || archive.OmsOrderCreated || archive.ParentOrderCreated || archive.ChildOrderCreated || archive.BrokerOrderCreated || archive.OrderStateCreated)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Archived result has order or order-state risk.");
        }

        if (archive.BrokerRouteCount != 0 || archive.SubmittedOrders || archive.CalledBrokerGateway)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Archived result has route or submission risk.");
        }

        if (!archive.NoLiveStateMutation || archive.MutatedLivePositionState || archive.MutatedBrokerState || archive.MutatedLiveTradingState)
        {
            return (false, PaperPositionPreviewStatus.Rejected, "Archived result has live state mutation risk.");
        }

        if (request.DecisionType == PaperSimulationResultDecisionType.PromoteToPaperPositionPreview &&
            archive.BlockedLines > 0 &&
            !request.BlockedLinesAcknowledged)
        {
            return (false, PaperPositionPreviewStatus.HeldForBlockedLines, "Blocked lines must be acknowledged before paper position preview.");
        }

        if (request.DecisionType == PaperSimulationResultDecisionType.PromoteToPaperPositionPreview &&
            archive.MissingStaleMarkWarningsPreserved &&
            !request.MissingStaleMarksAcknowledged)
        {
            return (false, PaperPositionPreviewStatus.HeldForMissingMarks, "Missing/stale mark warnings must be acknowledged before paper position preview.");
        }

        if (request.DecisionType == PaperSimulationResultDecisionType.PromoteToPaperPositionPreview &&
            archive.DriftAcknowledgementPreserved &&
            !request.DriftAcknowledged)
        {
            return (false, PaperPositionPreviewStatus.HeldForRiskReview, "Drift acknowledgement is required before paper position preview.");
        }

        return request.DecisionType switch
        {
            PaperSimulationResultDecisionType.ApprovePaperSimulationResult => (
                true,
                PaperPositionPreviewStatus.InconclusiveSafe,
                "Paper simulation result approved no-externally without creating a position preview."),
            PaperSimulationResultDecisionType.PromoteToPaperPositionPreview => (
                true,
                PaperPositionPreviewStatus.PaperPositionPreviewReady,
                "Promoted to paper position preview only; no live position, broker position, or trading state is mutated."),
            PaperSimulationResultDecisionType.Hold => (
                true,
                PaperPositionPreviewStatus.HeldForBlockedLines,
                "Paper simulation result held no-externally."),
            PaperSimulationResultDecisionType.Reject => (
                true,
                PaperPositionPreviewStatus.Rejected,
                "Paper simulation result rejected no-externally."),
            PaperSimulationResultDecisionType.RequestSimulationFix => (
                true,
                PaperPositionPreviewStatus.InconclusiveSafe,
                "Simulation fix requested no-externally."),
            PaperSimulationResultDecisionType.RequestRiskReview => (
                true,
                PaperPositionPreviewStatus.HeldForRiskReview,
                "Risk review requested no-externally."),
            _ => (
                false,
                PaperPositionPreviewStatus.InconclusiveSafe,
                "Unknown decision rejected safely.")
        };
    }

    private static PaperPositionPreview CreatePreview(
        PaperPositionPreviewId previewId,
        PaperSimulationResultRecord archive,
        PaperSimulationResultApprovalDecision decision,
        DateTimeOffset createdAtUtc)
        => new(
            previewId,
            archive.PaperSimulationResultId,
            archive.PaperSimulationPlanId,
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            decision.OperatorDecisionId,
            createdAtUtc,
            decision.ResultingPaperPositionPreviewStatus,
            archive.Lines.Select(x => CreatePreviewLine(previewId, archive, x)).ToArray(),
            archive.BlockedLineRecords,
            archive.QubesLineagePreserved,
            archive.CycleLineagePreserved,
            true,
            true,
            archive.SimulationPlanLineagePreserved,
            archive.PaperExecutionPlanLineagePreserved,
            archive.PaperCandidateLineagePreserved,
            archive.RiskLineagePreserved,
            archive.RebalanceIntentLineagePreserved,
            archive.LotSizingLineagePreserved,
            archive.MissingStaleMarkWarningsPreserved,
            archive.DriftAcknowledgementPreserved,
            PaperOnly: true,
            SimulatedOnly: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false);

    private static PaperPositionPreviewLine CreatePreviewLine(
        PaperPositionPreviewId previewId,
        PaperSimulationResultRecord archive,
        PaperSimulationResultLineRecord line)
        => new(
            new PaperPositionPreviewLineId($"{previewId.Value}:{line.NormalizedSymbol}:line"),
            previewId,
            line.PaperSimulationResultLineId,
            archive.PaperSimulationResultId,
            archive.CycleRunId,
            archive.QubesRunId,
            line.InstrumentId,
            line.NormalizedSymbol,
            line.Side,
            line.PaperBaseQuantity,
            line.QuantityCurrency,
            line.Side == IntentSide.Sell ? -line.SimulatedAppliedQuantity : line.SimulatedAppliedQuantity,
            PaperPositionPreviewStatus.PaperPositionPreviewReady,
            $"paperSimulationResultLine={line.PaperSimulationResultLineId.Value}; paperExecutionPlanLine={line.PaperExecutionPlanLineId.Value}",
            PaperOnly: true,
            SimulatedOnly: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true);

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
