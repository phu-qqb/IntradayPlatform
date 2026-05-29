using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperOrderCandidateStatus
{
    PaperCandidateReady,
    PaperCandidateBlocked,
    PaperCandidateRequiresMark,
    PaperCandidateRequiresLotSizing,
    PaperCandidateNotExecutable,
    PaperCandidateInconclusiveSafe
}

public enum PaperQuantityShapeCategory
{
    DeltaNotionalShapeOnly,
    QuantityRequiresMarkOrLotSizing,
    NoQuantityNoOp,
    NotExecutable
}

public enum PaperOrderTypeShapeCategory
{
    MarketShapeOnly,
    LimitShapeRequiresPrice,
    NotSpecified,
    NotExecutable
}

public enum PaperTimeInForceShapeCategory
{
    DayShapeOnly,
    ImmediateOrCancelShapeOnly,
    NotSpecified,
    NotExecutable
}

public sealed record PaperOrderCandidateId(string Value);

public sealed record PaperOrderCandidateRiskReference(
    OperatorDecisionId OperatorDecisionId,
    string SourceRiskReviewLineId,
    PaperPreTradeRiskResultCategory RiskResult,
    PaperOmsReviewStatus RiskReviewStatus);

public sealed record PaperOrderCandidateLine(
    PaperOrderCandidateId PaperOrderCandidateId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string SourceRebalanceIntentId,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal TargetWeight,
    decimal CurrentWeight,
    decimal DeltaWeight,
    decimal? TargetNotional,
    decimal? CurrentNotional,
    decimal? DeltaNotional,
    PaperQuantityShapeCategory QuantityShapeCategory,
    PaperOrderTypeShapeCategory OrderTypeShapeCategory,
    PaperTimeInForceShapeCategory TimeInForceShapeCategory,
    PaperOrderCandidateStatus CandidateStatus,
    string NonExecutableReason,
    PaperOrderCandidateRiskReference RiskReviewReference,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool CreatesFill,
    bool CreatesExecutionReport);

public sealed record BlockedPaperReviewLine(
    string SourceRiskReviewLineId,
    InstrumentId InstrumentId,
    string Symbol,
    PaperPreTradeRiskResultCategory Result,
    PaperOmsReviewStatus Status,
    string Reason);

public sealed record PaperOrderCandidateShapeRequest(
    QubesIntradayCycleFixtureResult Cycle,
    IntradayCycleOperatorReviewResult OperatorReview,
    PaperOmsIntentReviewReport PaperReviewReport);

public sealed record PaperOrderCandidateBatch(
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    int AcceptedPaperReviewLineCount,
    int BlockedPaperReviewLineCount,
    IReadOnlyList<PaperOrderCandidateLine> Candidates,
    IReadOnlyList<BlockedPaperReviewLine> BlockedLines,
    bool QubesLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool CandidatesAreNonExecutable,
    bool CandidatesAreNotOrders,
    bool CandidatesAreNotSubmitted,
    bool CandidatesHaveNoBrokerRoute,
    bool CreatedOmsOrder,
    bool CreatedParentOrder,
    bool CreatedChildOrder,
    bool CreatedBrokerOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool MutatedLiveTradingState);

public sealed class PaperOrderCandidateShapeService
{
    public PaperOrderCandidateBatch Create(PaperOrderCandidateShapeRequest request)
    {
        var accepted = request.PaperReviewReport.Lines
            .Where(x => x.Status == PaperOmsReviewStatus.AcceptedForPaperReview &&
                        x.Result == PaperPreTradeRiskResultCategory.AcceptedForPaperReview)
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var blocked = request.PaperReviewReport.Lines
            .Where(x => x.Status != PaperOmsReviewStatus.AcceptedForPaperReview ||
                        x.Result != PaperPreTradeRiskResultCategory.AcceptedForPaperReview)
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BlockedPaperReviewLine(
                RiskReviewLineId(request.PaperReviewReport.CycleRunId, x.Symbol),
                x.InstrumentId,
                x.Symbol,
                x.Result,
                x.Status,
                x.Reason))
            .ToArray();
        var candidates = accepted
            .Where(x => Math.Abs(x.DeltaWeight) > 0m)
            .Select(x => CreateCandidate(request, x))
            .ToArray();

        return new PaperOrderCandidateBatch(
            request.PaperReviewReport.CycleRunId,
            request.PaperReviewReport.QubesRunId,
            request.PaperReviewReport.OperatorDecisionId,
            accepted.Length,
            blocked.Length,
            candidates,
            blocked,
            QubesLineagePreserved: request.Cycle.Persistence.RawRows.Count > 0 &&
                request.Cycle.Persistence.NormalizedRows.Count > 0 &&
                request.Cycle.Persistence.AuditBatch.ModelWeightBatchId is not null &&
                request.Cycle.Persistence.AuditBatch.PromotedModelRunId is not null,
            OperatorDecisionLineagePreserved: request.OperatorReview.Decision.OperatorDecisionId == request.PaperReviewReport.OperatorDecisionId &&
                request.OperatorReview.Decision.ResultingCycleReviewStatus == CycleReviewStatus.PaperReadyNoExternal,
            RiskLineagePreserved: candidates.All(x => !string.IsNullOrWhiteSpace(x.RiskReviewReference.SourceRiskReviewLineId)),
            RebalanceIntentLineagePreserved: candidates.All(x => !string.IsNullOrWhiteSpace(x.SourceRebalanceIntentId)),
            request.OperatorReview.Decision.MissingStaleMarkWarningsPreserved,
            request.OperatorReview.Decision.DriftAcknowledged,
            candidates.All(x => x.NonExecutable),
            candidates.All(x => x.NotAnOrder),
            candidates.All(x => x.NotSubmitted),
            candidates.All(x => x.NoBrokerRoute),
            CreatedOmsOrder: false,
            CreatedParentOrder: false,
            CreatedChildOrder: false,
            CreatedBrokerOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            MutatedLiveTradingState: false);
    }

    private static PaperOrderCandidateLine CreateCandidate(
        PaperOrderCandidateShapeRequest request,
        PaperOmsIntentReviewLine line)
    {
        var side = line.DeltaWeight > 0m
            ? IntentSide.Buy
            : line.DeltaWeight < 0m
                ? IntentSide.Sell
                : IntentSide.None;
        var quantityShape = side == IntentSide.None
            ? PaperQuantityShapeCategory.NoQuantityNoOp
            : PaperQuantityShapeCategory.QuantityRequiresMarkOrLotSizing;
        var status = side == IntentSide.None
            ? PaperOrderCandidateStatus.PaperCandidateReady
            : PaperOrderCandidateStatus.PaperCandidateRequiresLotSizing;

        return new PaperOrderCandidateLine(
            new PaperOrderCandidateId($"{request.PaperReviewReport.CycleRunId}:{line.Symbol}:paper-candidate"),
            request.PaperReviewReport.CycleRunId,
            request.PaperReviewReport.QubesRunId,
            request.PaperReviewReport.OperatorDecisionId,
            SourceRebalanceIntentId(request.PaperReviewReport.CycleRunId, line.Symbol),
            line.InstrumentId,
            line.Symbol,
            side,
            line.TargetWeight,
            line.CurrentWeight,
            line.DeltaWeight,
            line.TargetWeight == 0m ? 0m : line.TargetWeight * 1_000_000m,
            line.CurrentWeight == 0m ? 0m : line.CurrentWeight * 1_000_000m,
            line.DeltaNotional,
            quantityShape,
            PaperOrderTypeShapeCategory.NotExecutable,
            PaperTimeInForceShapeCategory.NotExecutable,
            status,
            "Paper-only candidate shape; not an OMS order, not a broker order, not submitted, and no broker route exists.",
            new PaperOrderCandidateRiskReference(
                request.PaperReviewReport.OperatorDecisionId,
                RiskReviewLineId(request.PaperReviewReport.CycleRunId, line.Symbol),
                line.Result,
                line.Status),
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false);
    }

    private static string SourceRebalanceIntentId(string cycleRunId, string symbol)
        => $"{cycleRunId}:{symbol}:rebalance-intent";

    private static string RiskReviewLineId(string cycleRunId, string symbol)
        => $"{cycleRunId}:{symbol}:paper-risk-line";
}
