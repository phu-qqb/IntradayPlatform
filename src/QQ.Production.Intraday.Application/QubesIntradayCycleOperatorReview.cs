using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum OperatorDecisionType
{
    Approve,
    Hold,
    Reject,
    RequestDataFix,
    PromoteToPaperReady
}

public enum OperatorDecisionStatus
{
    Recorded,
    RejectedByGate,
    DuplicateReturned
}

public enum OperatorDecisionReasonCategory
{
    AcceptedForPaperReview,
    HeldDueToMissingMarks,
    HeldDueToDrift,
    RejectedDueToValidationFailure,
    DataFixRequested,
    InconclusiveSafe
}

public enum CycleReviewStatus
{
    ApprovedForPaperReview,
    Held,
    Rejected,
    DataFixRequested,
    PaperReadyNoExternal,
    InconclusiveSafe
}

public sealed record OperatorDecisionId(string Value)
{
    public static OperatorDecisionId New() => new(Guid.NewGuid().ToString("N"));
}

public sealed record IntradayCycleOperatorDecisionRequest(
    OperatorDecisionId OperatorDecisionId,
    OperatorDecisionType DecisionType,
    string ReviewedBy,
    OperatorDecisionReasonCategory ReasonCategory,
    string? CommentSanitized,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged);

public sealed record IntradayCycleOperatorDecision(
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    OperatorDecisionType DecisionType,
    OperatorDecisionStatus DecisionStatus,
    DateTimeOffset ReviewedAtUtc,
    string ReviewedBy,
    OperatorDecisionReasonCategory ReasonCategory,
    string? CommentSanitized,
    CycleReviewStatus ResultingCycleReviewStatus,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged,
    bool MissingStaleMarkWarningsPreserved,
    bool TheoreticalVsRealDriftPreserved,
    bool RebalanceIntentsRemainNonExecutable,
    bool PromotionIsNoExternalPaperReadyOnly,
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
    bool MutatesLiveTradingState,
    bool GateAccepted,
    string GateMessage);

public sealed record IntradayCycleOperatorReviewResult(
    IntradayCycleOperatorDecision Decision,
    IntradayCycleArchiveRecord ArchiveRecord,
    bool Persisted,
    bool AlreadyRecorded);

public interface IIntradayCycleOperatorDecisionRepository
{
    Task<IntradayCycleOperatorDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken);

    Task AddAsync(IntradayCycleOperatorDecision decision, CancellationToken cancellationToken);
}

public sealed class InMemoryIntradayCycleOperatorDecisionRepository : IIntradayCycleOperatorDecisionRepository
{
    private readonly List<IntradayCycleOperatorDecision> decisions = [];

    public Task<IntradayCycleOperatorDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken)
        => Task.FromResult(decisions.FirstOrDefault(x => x.OperatorDecisionId == decisionId));

    public Task AddAsync(IntradayCycleOperatorDecision decision, CancellationToken cancellationToken)
    {
        if (decisions.Any(x => x.OperatorDecisionId == decision.OperatorDecisionId))
        {
            return Task.CompletedTask;
        }

        decisions.Add(decision);
        return Task.CompletedTask;
    }
}

public sealed class IntradayCycleOperatorReviewService(
    IIntradayCycleOperatorDecisionRepository repository,
    IClock clock)
{
    public async Task<IntradayCycleOperatorReviewResult> ReviewAsync(
        IntradayCycleArchiveRecord archive,
        IntradayCycleOperatorDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByDecisionIdAsync(request.OperatorDecisionId, cancellationToken);
        if (existing is not null)
        {
            return new IntradayCycleOperatorReviewResult(
                existing with { DecisionStatus = OperatorDecisionStatus.DuplicateReturned },
                archive,
                Persisted: false,
                AlreadyRecorded: true);
        }

        var decision = CreateDecision(archive, request, clock.UtcNow);
        if (decision.GateAccepted)
        {
            await repository.AddAsync(decision, cancellationToken);
            return new IntradayCycleOperatorReviewResult(decision, archive, Persisted: true, AlreadyRecorded: false);
        }

        return new IntradayCycleOperatorReviewResult(decision, archive, Persisted: false, AlreadyRecorded: false);
    }

    private static IntradayCycleOperatorDecision CreateDecision(
        IntradayCycleArchiveRecord archive,
        IntradayCycleOperatorDecisionRequest request,
        DateTimeOffset reviewedAtUtc)
    {
        var gate = EvaluateGate(archive, request);
        var reviewedBy = SanitizeReviewedBy(request.ReviewedBy);
        var comment = string.IsNullOrWhiteSpace(request.CommentSanitized)
            ? null
            : SanitizeComment(request.CommentSanitized);

        return new IntradayCycleOperatorDecision(
            archive.CycleRunId,
            archive.QubesRunId,
            request.OperatorDecisionId,
            request.DecisionType,
            gate.Accepted ? OperatorDecisionStatus.Recorded : OperatorDecisionStatus.RejectedByGate,
            reviewedAtUtc,
            reviewedBy,
            request.ReasonCategory,
            comment,
            gate.ReviewStatus,
            request.MissingStaleMarksAcknowledged,
            request.DriftAcknowledged,
            archive.MissingOrStaleMarkWarningCount > 0,
            archive.ComparatorStatus == TheoreticalVsRealStatus.Drift,
            !archive.RebalanceIntentsExecutable,
            request.DecisionType == OperatorDecisionType.PromoteToPaperReady && gate.Accepted,
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
            MutatesLiveTradingState: false,
            gate.Accepted,
            gate.Message);
    }

    private static (bool Accepted, CycleReviewStatus ReviewStatus, string Message) EvaluateGate(
        IntradayCycleArchiveRecord archive,
        IntradayCycleOperatorDecisionRequest request)
    {
        if (!archive.NoExternal)
        {
            return (false, CycleReviewStatus.InconclusiveSafe, "Archive is not no-external; decision rejected safely.");
        }

        if (archive.CycleStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase) &&
            request.DecisionType == OperatorDecisionType.PromoteToPaperReady)
        {
            return (false, CycleReviewStatus.Rejected, "Validation failure cycles cannot be promoted.");
        }

        if (request.DecisionType == OperatorDecisionType.PromoteToPaperReady &&
            archive.MissingOrStaleMarkWarningCount > 0 &&
            !request.MissingStaleMarksAcknowledged)
        {
            return (false, CycleReviewStatus.InconclusiveSafe, "Missing/stale marks must be acknowledged before paper-ready promotion.");
        }

        if (request.DecisionType == OperatorDecisionType.PromoteToPaperReady &&
            archive.ComparatorStatus == TheoreticalVsRealStatus.Drift &&
            !request.DriftAcknowledged)
        {
            return (false, CycleReviewStatus.InconclusiveSafe, "Theoretical-vs-real drift must be acknowledged before paper-ready promotion.");
        }

        return request.DecisionType switch
        {
            OperatorDecisionType.Approve => (
                true,
                CycleReviewStatus.ApprovedForPaperReview,
                "Approved for no-external paper review only; live trading and order submission remain disabled."),
            OperatorDecisionType.Hold => (
                true,
                CycleReviewStatus.Held,
                "Cycle held no-externally for operator follow-up."),
            OperatorDecisionType.Reject => (
                true,
                CycleReviewStatus.Rejected,
                "Cycle rejected no-externally; no execution path created."),
            OperatorDecisionType.RequestDataFix => (
                true,
                CycleReviewStatus.DataFixRequested,
                "Data fix requested no-externally; no replay, broker call, or scheduler started."),
            OperatorDecisionType.PromoteToPaperReady => (
                true,
                CycleReviewStatus.PaperReadyNoExternal,
                "Promoted to paper-ready only; no live trading, order submission, or broker interaction is enabled."),
            _ => (
                false,
                CycleReviewStatus.InconclusiveSafe,
                "Unknown decision type rejected safely.")
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
