using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperExecutionPlanMode
{
    PaperOnly,
    NoExternal,
    NonExecutable
}

public enum PaperExecutionPlanStatus
{
    PaperPlanReady,
    PaperPlanPartiallyReady,
    PaperPlanBlocked,
    PaperPlanInconclusiveSafe
}

public enum PaperExecutionPlanLineStatus
{
    PaperLineReady,
    PaperLineRequiresMark,
    PaperLineRequiresLotSizing,
    PaperLineRequiresInstrumentConvention,
    PaperLineBlockedByRisk,
    PaperLineNonExecutable
}

public enum PaperExecutionStyleShape
{
    MarketShapeOnly,
    LimitShapeRequiresPrice,
    VWAPShapeOnly,
    NotSpecified,
    NotExecutable
}

public enum PaperExecutionTimeInForceShape
{
    DayShapeOnly,
    IOCShapeOnly,
    NotSpecified,
    NotExecutable
}

public sealed record PaperExecutionPlanId(string Value);

public sealed record PaperExecutionPlanLineId(string Value);

public sealed record PaperExecutionPlanLineShape(
    PaperExecutionPlanLineId PaperExecutionPlanLineId,
    PaperExecutionPlanId PaperExecutionPlanId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    PaperOrderCandidateId PaperOrderCandidateId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string SourceRebalanceIntentId,
    PaperOrderCandidateRiskReference RiskReviewReference,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    string PaperTradableSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    string NotionalCurrency,
    decimal? LotSize,
    PaperQuantityRoundingMode QuantityRoundingMode,
    PaperQuantityShapeCategory QuantityShapeCategory,
    PaperLotSizingStatus QuantityStatus,
    PaperExecutionPlanLineStatus PlanLineStatus,
    PaperExecutionStyleShape ExecutionStyleShape,
    PaperExecutionTimeInForceShape TimeInForceShape,
    string SequencingGroup,
    int Priority,
    string? BlockReason,
    string NonExecutableReason,
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

public sealed record PaperExecutionPlanBatch(
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    string PaperRiskReviewId,
    IReadOnlyList<PaperExecutionPlanMode> PlanModes,
    PaperExecutionPlanStatus PlanStatus,
    IReadOnlyList<PaperExecutionPlanLineShape> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    int ReadyLineCount,
    int BlockedLineCount,
    bool QubesLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool RiskLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
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

public sealed class PaperExecutionPlanShapeService
{
    public PaperExecutionPlanBatch Create(PaperLotSizedCandidateBatch lotSizedBatch)
    {
        var planId = new PaperExecutionPlanId($"{lotSizedBatch.CycleRunId}:paper-execution-plan");
        var lines = lotSizedBatch.SizedCandidates
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select((candidate, index) => CreateLine(planId, candidate, index + 1))
            .ToArray();
        var ready = lines.Count(x => x.PlanLineStatus == PaperExecutionPlanLineStatus.PaperLineReady);
        var blocked = lines.Length - ready + lotSizedBatch.BlockedLines.Count;
        var status = ready == lines.Length && lotSizedBatch.BlockedLines.Count == 0
            ? PaperExecutionPlanStatus.PaperPlanReady
            : ready > 0
                ? PaperExecutionPlanStatus.PaperPlanPartiallyReady
                : PaperExecutionPlanStatus.PaperPlanBlocked;

        return new PaperExecutionPlanBatch(
            planId,
            lotSizedBatch.CycleRunId,
            lotSizedBatch.QubesRunId,
            lotSizedBatch.OperatorDecisionId,
            lotSizedBatch.PaperOrderCandidateBatchId,
            lotSizedBatch.PaperRiskReviewId,
            [PaperExecutionPlanMode.PaperOnly, PaperExecutionPlanMode.NoExternal, PaperExecutionPlanMode.NonExecutable],
            status,
            lines,
            lotSizedBatch.BlockedLines,
            ready,
            blocked,
            lotSizedBatch.QubesLineagePreserved,
            lotSizedBatch.OperatorDecisionLineagePreserved,
            lotSizedBatch.RiskLineagePreserved,
            lines.All(x => !string.IsNullOrWhiteSpace(x.PaperOrderCandidateId.Value)),
            lotSizedBatch.RebalanceIntentLineagePreserved && lines.All(x => !string.IsNullOrWhiteSpace(x.SourceRebalanceIntentId)),
            lines.All(x => x.QuantityStatus == PaperLotSizingStatus.PaperSized || x.BlockReason is not null),
            lotSizedBatch.MissingStaleMarkWarningsPreserved,
            lotSizedBatch.DriftAcknowledgementPreserved,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
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

    private static PaperExecutionPlanLineShape CreateLine(
        PaperExecutionPlanId planId,
        PaperLotSizedCandidate candidate,
        int priority)
    {
        var lineStatus = ToLineStatus(candidate.SizingStatus);
        var ready = lineStatus == PaperExecutionPlanLineStatus.PaperLineReady;

        return new PaperExecutionPlanLineShape(
            new PaperExecutionPlanLineId($"{planId.Value}:{candidate.NormalizedSymbol}:line"),
            planId,
            candidate.PaperOrderCandidateBatchId,
            candidate.PaperOrderCandidateId,
            candidate.CycleRunId,
            candidate.QubesRunId,
            candidate.OperatorDecisionId,
            candidate.SourceRebalanceIntentId,
            candidate.RiskReviewReference,
            candidate.InstrumentId,
            candidate.NormalizedSymbol,
            candidate.PaperTradableSymbol ?? candidate.NormalizedSymbol,
            candidate.Side,
            candidate.QuantityShape.AbsoluteBaseQuantityRounded,
            candidate.QuantityShape.QuantityCurrency,
            candidate.InstrumentConvention?.NotionalCurrency ?? "USD",
            candidate.QuantityShape.LotSize,
            candidate.QuantityShape.RoundingMode,
            PaperQuantityShapeCategory.DeltaNotionalShapeOnly,
            candidate.QuantityShape.Status,
            lineStatus,
            ready ? PaperExecutionStyleShape.MarketShapeOnly : PaperExecutionStyleShape.NotExecutable,
            ready ? PaperExecutionTimeInForceShape.DayShapeOnly : PaperExecutionTimeInForceShape.NotExecutable,
            "default-paper-sequence",
            priority,
            ready ? null : candidate.QuantityShape.StatusReason,
            "Paper execution plan shape only; not an OMS order, not a broker order, not submitted, and no broker route exists.",
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

    private static PaperExecutionPlanLineStatus ToLineStatus(PaperLotSizingStatus status)
        => status switch
        {
            PaperLotSizingStatus.PaperSized => PaperExecutionPlanLineStatus.PaperLineReady,
            PaperLotSizingStatus.RequiresMark => PaperExecutionPlanLineStatus.PaperLineRequiresMark,
            PaperLotSizingStatus.RequiresLotSizing => PaperExecutionPlanLineStatus.PaperLineRequiresLotSizing,
            PaperLotSizingStatus.RequiresInstrumentConvention => PaperExecutionPlanLineStatus.PaperLineRequiresInstrumentConvention,
            PaperLotSizingStatus.BlockedByRisk => PaperExecutionPlanLineStatus.PaperLineBlockedByRisk,
            _ => PaperExecutionPlanLineStatus.PaperLineNonExecutable
        };
}
