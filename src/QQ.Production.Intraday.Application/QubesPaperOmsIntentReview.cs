using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperPreTradeRiskResultCategory
{
    AcceptedForPaperReview,
    BlockedMissingPromotion,
    BlockedMissingMark,
    BlockedStaleMark,
    BlockedMissingActual,
    BlockedUnsupportedInstrument,
    BlockedNonApprovedInstrument,
    BlockedLimitExceeded,
    BlockedIntentExecutable,
    BlockedNoOMS,
    InconclusiveSafe
}

public enum PaperOmsReviewStatus
{
    AcceptedForPaperReview,
    Blocked,
    InconclusiveSafe
}

public sealed record PaperPreTradeRiskLimits(
    decimal MaxAbsoluteDeltaWeightPerInstrument,
    decimal MaxAbsoluteTargetWeightPerInstrument,
    decimal MaxGrossNotionalChange,
    decimal MaxPerInstrumentNotionalChange,
    IReadOnlySet<string> ApprovedSymbols);

public sealed record PaperOmsIntentCandidate(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal? CurrentNotional,
    decimal? TargetNotional,
    decimal? DeltaNotional,
    IntentSide IntentSide,
    IReadOnlyList<IntentStatus> IntentStatuses,
    bool IsExplicitlyNonExecutable);

public sealed record PaperPreTradeRiskCheck(
    string Name,
    bool Passed,
    PaperPreTradeRiskResultCategory Result,
    string Message);

public sealed record PaperOmsIntentReviewLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal? DeltaNotional,
    IntentSide IntentSide,
    PaperPreTradeRiskResultCategory Result,
    PaperOmsReviewStatus Status,
    IReadOnlyList<PaperPreTradeRiskCheck> Checks,
    bool IsExecutable,
    bool CreatesOrder,
    string Reason);

public sealed record PaperOmsIntentReviewRequest(
    QubesIntradayCycleFixtureResult Cycle,
    IntradayCycleOperatorReviewResult OperatorReview,
    PaperPreTradeRiskLimits Limits,
    IReadOnlyList<PaperOmsIntentCandidate>? IntentCandidates = null);

public sealed record PaperOmsIntentReviewReport(
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOmsReviewStatus Status,
    int IntentCount,
    int AcceptedIntentCount,
    int BlockedIntentCount,
    decimal GrossNotionalChange,
    IReadOnlyList<PaperOmsIntentReviewLine> Lines,
    bool PromoteToPaperReadyGatePresent,
    bool CycleWithoutPromotionBlocked,
    bool MissingStaleMarkAcknowledgementPreserved,
    bool DriftAcknowledgementPreserved,
    bool QubesLineagePreserved,
    bool RebalanceIntentsRemainNonExecutable,
    bool CreatedOmsOrder,
    bool CreatedParentOrder,
    bool CreatedChildOrder,
    bool CreatedBrokerOrder,
    bool CreatedExecutableOrder,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool MutatedLiveTradingState);

public sealed class PaperOmsIntentReviewService
{
    public PaperOmsIntentReviewReport Review(PaperOmsIntentReviewRequest request)
    {
        var candidates = request.IntentCandidates ?? CreateCandidates(request.Cycle.TheoreticalPortfolioDiff.RebalanceIntents);
        var promotionValid = IsValidPaperPromotion(request.OperatorReview.Decision);
        var grossNotionalChange = candidates.Sum(x => Math.Abs(x.DeltaNotional ?? 0m));
        var markStatusByInstrument = request.Cycle.TheoreticalPnl.InstrumentDetails.ToDictionary(x => x.InstrumentId, x => x.PnLStatus);
        var reconciliationByInstrument = request.Cycle.ReconciliationComparator.ReconciliationLines.ToDictionary(x => x.InstrumentId, x => x.Status);
        var lines = candidates
            .Select(candidate => ReviewCandidate(
                candidate,
                promotionValid,
                request.Limits,
                grossNotionalChange,
                markStatusByInstrument,
                reconciliationByInstrument))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var blocked = lines.Count(x => x.Status == PaperOmsReviewStatus.Blocked);
        var status = !promotionValid
            ? PaperOmsReviewStatus.Blocked
            : blocked > 0
                ? PaperOmsReviewStatus.InconclusiveSafe
                : PaperOmsReviewStatus.AcceptedForPaperReview;

        return new PaperOmsIntentReviewReport(
            request.Cycle.CycleRunId,
            request.Cycle.QubesRunId.Value,
            request.OperatorReview.Decision.OperatorDecisionId,
            status,
            lines.Length,
            lines.Count(x => x.Status == PaperOmsReviewStatus.AcceptedForPaperReview),
            blocked,
            grossNotionalChange,
            lines,
            PromoteToPaperReadyGatePresent: promotionValid,
            CycleWithoutPromotionBlocked: !promotionValid,
            request.OperatorReview.Decision.MissingStaleMarksAcknowledged,
            request.OperatorReview.Decision.DriftAcknowledged,
            request.Cycle.Persistence.RawRows.Count > 0 &&
                request.Cycle.Persistence.NormalizedRows.Count > 0 &&
                request.Cycle.Persistence.AuditBatch.ModelWeightBatchId is not null &&
                request.Cycle.Persistence.AuditBatch.PromotedModelRunId is not null,
            lines.All(x => !x.IsExecutable) && request.Cycle.RebalanceIntentsRemainNonExecutable,
            CreatedOmsOrder: false,
            CreatedParentOrder: false,
            CreatedChildOrder: false,
            CreatedBrokerOrder: false,
            CreatedExecutableOrder: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            MutatedLiveTradingState: false);
    }

    public static IReadOnlyList<PaperOmsIntentCandidate> CreateCandidates(
        IReadOnlyList<NonExecutableRebalanceIntentLine> intents)
        => intents.Select(x => new PaperOmsIntentCandidate(
                x.InstrumentId,
                x.Symbol,
                x.CurrentWeight,
                x.TargetWeight,
                x.DeltaWeight,
                x.CurrentNotional,
                x.TargetNotional,
                x.DeltaNotional,
                x.IntentSide,
                x.IntentStatuses,
                !x.IsExecutable))
            .ToArray();

    private static bool IsValidPaperPromotion(IntradayCycleOperatorDecision decision)
        => decision.DecisionType == OperatorDecisionType.PromoteToPaperReady &&
           decision.DecisionStatus == OperatorDecisionStatus.Recorded &&
           decision.ResultingCycleReviewStatus == CycleReviewStatus.PaperReadyNoExternal &&
           decision.GateAccepted &&
           decision.PromotionIsNoExternalPaperReadyOnly &&
           !decision.EnablesLiveTrading &&
           !decision.SubmitsOrders &&
           !decision.CreatesExecutableOrder;

    private static PaperOmsIntentReviewLine ReviewCandidate(
        PaperOmsIntentCandidate candidate,
        bool promotionValid,
        PaperPreTradeRiskLimits limits,
        decimal grossNotionalChange,
        IReadOnlyDictionary<InstrumentId, PnLComputationStatus> markStatusByInstrument,
        IReadOnlyDictionary<InstrumentId, QubesReconciliationLineStatus> reconciliationByInstrument)
    {
        markStatusByInstrument.TryGetValue(candidate.InstrumentId, out var markStatus);
        reconciliationByInstrument.TryGetValue(candidate.InstrumentId, out var reconciliationStatus);
        var checks = new List<PaperPreTradeRiskCheck>
        {
            Check("PromoteToPaperReady decision", promotionValid, PaperPreTradeRiskResultCategory.BlockedMissingPromotion, "Cycle requires a valid PromoteToPaperReady operator decision."),
            Check("Intent explicitly non-executable", IsIntentNonExecutable(candidate), PaperPreTradeRiskResultCategory.BlockedIntentExecutable, "Paper review rejects executable intents."),
            Check("BlockedNoOMS preserved", candidate.IntentStatuses.Contains(IntentStatus.BlockedNoOMS), PaperPreTradeRiskResultCategory.BlockedNoOMS, "Paper review requires BlockedNoOMS status."),
            Check("Missing current mark", markStatus != PnLComputationStatus.MissingMark, PaperPreTradeRiskResultCategory.BlockedMissingMark, "No-external missing mark blocks the affected paper review line."),
            Check("Stale current mark", markStatus != PnLComputationStatus.StaleMark, PaperPreTradeRiskResultCategory.BlockedStaleMark, "No-external stale mark blocks the affected paper review line."),
            Check("Missing actual", reconciliationStatus != QubesReconciliationLineStatus.MissingActual, PaperPreTradeRiskResultCategory.BlockedMissingActual, "Missing actual fixture position blocks the affected paper review line."),
            Check("Supported instrument", IsSupportedFxUsd(candidate.Symbol), PaperPreTradeRiskResultCategory.BlockedUnsupportedInstrument, "Only normalized USD-quote FX paper intents are supported."),
            Check("Approved paper universe", limits.ApprovedSymbols.Contains(candidate.Symbol), PaperPreTradeRiskResultCategory.BlockedNonApprovedInstrument, "Instrument is not in the no-external paper approved universe."),
            Check("Max absolute delta weight", Math.Abs(candidate.DeltaWeight) <= limits.MaxAbsoluteDeltaWeightPerInstrument, PaperPreTradeRiskResultCategory.BlockedLimitExceeded, "Delta weight exceeds paper review limit."),
            Check("Max absolute target weight", Math.Abs(candidate.TargetWeight) <= limits.MaxAbsoluteTargetWeightPerInstrument, PaperPreTradeRiskResultCategory.BlockedLimitExceeded, "Target weight exceeds paper review limit."),
            Check("Max per-instrument notional change", Math.Abs(candidate.DeltaNotional ?? 0m) <= limits.MaxPerInstrumentNotionalChange, PaperPreTradeRiskResultCategory.BlockedLimitExceeded, "Per-instrument notional change exceeds paper review limit."),
            Check("Max gross notional change", grossNotionalChange <= limits.MaxGrossNotionalChange, PaperPreTradeRiskResultCategory.BlockedLimitExceeded, "Gross notional change exceeds paper review limit.")
        };
        var failed = checks.FirstOrDefault(x => !x.Passed);
        var result = failed?.Result ?? PaperPreTradeRiskResultCategory.AcceptedForPaperReview;
        var status = result == PaperPreTradeRiskResultCategory.AcceptedForPaperReview
            ? PaperOmsReviewStatus.AcceptedForPaperReview
            : PaperOmsReviewStatus.Blocked;

        return new PaperOmsIntentReviewLine(
            candidate.InstrumentId,
            candidate.Symbol,
            candidate.CurrentWeight,
            candidate.TargetWeight,
            candidate.DeltaWeight,
            candidate.DeltaNotional,
            candidate.IntentSide,
            result,
            status,
            checks,
            !IsIntentNonExecutable(candidate),
            CreatesOrder: false,
            failed?.Message ?? "Intent accepted for no-external paper review only.");
    }

    private static PaperPreTradeRiskCheck Check(
        string name,
        bool passed,
        PaperPreTradeRiskResultCategory failureResult,
        string message)
        => new(name, passed, passed ? PaperPreTradeRiskResultCategory.AcceptedForPaperReview : failureResult, message);

    private static bool IsIntentNonExecutable(PaperOmsIntentCandidate candidate)
        => candidate.IsExplicitlyNonExecutable &&
           candidate.IntentStatuses.Contains(IntentStatus.TheoreticalOnly) &&
           candidate.IntentStatuses.Contains(IntentStatus.NotExecutable) &&
           candidate.IntentStatuses.Contains(IntentStatus.BlockedNoOMS);

    private static bool IsSupportedFxUsd(string symbol)
        => symbol.Length == 6 &&
           symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase) &&
           symbol.All(c => c is >= 'A' and <= 'Z');
}
