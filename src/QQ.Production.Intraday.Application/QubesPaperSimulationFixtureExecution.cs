using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperSimulationResultStatus
{
    CompletedNoExternalFixture,
    PaperInconclusiveSafe,
    DuplicateReturned,
    RejectedDryRunNotReady
}

public enum PaperSimulationLineResultStatus
{
    PaperApplied,
    PaperBlocked,
    PaperInconclusiveSafe
}

public enum PaperSimulationOutcomeCategory
{
    PaperApplied,
    PaperBlocked,
    PaperInconclusiveSafe
}

public enum PaperSimulationSlippageCategory
{
    FixtureSlippageApplied,
    NotComputed
}

public enum PaperSimulationFeeCategory
{
    FixtureFeeApplied,
    NotComputed
}

public sealed record PaperSimulationResultId(string Value);

public sealed record PaperSimulationResultLineId(string Value);

public sealed record PaperSimulationFixtureResultLine(
    PaperSimulationResultLineId PaperSimulationResultLineId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationDryRunLineResultId PaperSimulationDryRunLineResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanLineId PaperExecutionPlanLineId,
    PaperOrderCandidateId PaperOrderCandidateId,
    string SourceRebalanceIntentId,
    PaperOrderCandidateRiskReference RiskReviewReference,
    string LotSizingReference,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    decimal? SimulatedAppliedQuantity,
    decimal? SimulatedNotionalImpact,
    PaperSimulationOutcomeCategory SimulatedOutcomeCategory,
    PaperSimulationSlippageCategory SimulatedSlippageCategory,
    PaperSimulationFeeCategory SimulatedFeeCategory,
    PaperSimulationLineResultStatus SimulatedLineStatus,
    bool ResultIsPaperOnly,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NonExecutable,
    bool RealFillEntityCreated,
    bool BrokerExecutionReportEntityCreated,
    bool OmsOrderCreated,
    bool ParentOrderCreated,
    bool ChildOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool Submitted);

public sealed record PaperSimulationFixtureSummary(
    int TotalLines,
    int SimulatedAppliedLines,
    int BlockedLines,
    int RealFillCount,
    int ExecutionReportCount,
    int OrderCount,
    int BrokerRouteCount,
    string SimulationState,
    string SafetyStatus);

public sealed record PaperSimulationPositionDeltaPreview(
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperQuantityDelta,
    string QuantityCurrency,
    bool SimulatedOnly,
    bool LivePositionStateMutated,
    bool BrokerStateMutated,
    bool TradingStateMutated);

public sealed record PaperSimulationReconciliationPreview(
    string PreviewStatus,
    int PreviewLineCount,
    bool ExpectedTargetDriftPreviewCreated,
    bool LiveReconciliationClaimCreated,
    bool LivePositionStateMutated,
    bool BrokerStateMutated,
    bool TradingStateMutated);

public sealed record PaperSimulationPostTradePreview(
    IReadOnlyList<PaperSimulationPositionDeltaPreview> PositionDeltas,
    bool SimulatedOnly,
    bool LivePositionStateMutated,
    bool BrokerStateMutated,
    bool TradingStateMutated);

public sealed record PaperSimulationFixtureResult(
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationDryRunResultId PaperSimulationDryRunResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    DateTimeOffset CreatedAtUtc,
    PaperSimulationResultStatus ResultStatus,
    PaperSimulationFixtureSummary Summary,
    PaperSimulationDryRunAssumptionReport AssumptionReport,
    IReadOnlyList<PaperSimulationFixtureResultLine> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    PaperSimulationPostTradePreview PostTradePreview,
    PaperSimulationReconciliationPreview ReconciliationPreview,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool PlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool BlockedLineAcknowledgementPreserved,
    bool MissingStaleMarkAcknowledgementPreserved,
    bool OperatorApprovalAcknowledgementPreserved,
    bool PaperOnly,
    bool NoExternal,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool ResultIsPaperOnly,
    bool NoRealFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoLiveStateMutation,
    bool RealFillEntityCreated,
    bool BrokerExecutionReportEntityCreated,
    bool OmsOrderCreated,
    bool ParentOrderCreated,
    bool ChildOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool MutatedLiveTradingState,
    bool MutatedLivePositionState,
    bool MutatedBrokerState,
    bool ReplayOrShadowReplayIntroduced);

public sealed record PaperSimulationFixtureExecutionResult(
    PaperSimulationFixtureResult Result,
    bool Persisted,
    bool AlreadyExecuted);

public interface IPaperSimulationFixtureResultRepository
{
    Task<PaperSimulationFixtureResult?> GetByResultIdAsync(
        PaperSimulationResultId resultId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperSimulationFixtureResult result, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperSimulationFixtureResultRepository : IPaperSimulationFixtureResultRepository
{
    private readonly List<PaperSimulationFixtureResult> results = [];

    public Task<PaperSimulationFixtureResult?> GetByResultIdAsync(
        PaperSimulationResultId resultId,
        CancellationToken cancellationToken)
        => Task.FromResult(results.FirstOrDefault(x => x.PaperSimulationResultId == resultId));

    public Task AddAsync(PaperSimulationFixtureResult result, CancellationToken cancellationToken)
    {
        if (results.Any(x => x.PaperSimulationResultId == result.PaperSimulationResultId))
        {
            return Task.CompletedTask;
        }

        results.Add(result);
        return Task.CompletedTask;
    }
}

public sealed class PaperSimulationFixtureExecutor(
    IPaperSimulationFixtureResultRepository repository,
    IClock clock)
{
    public async Task<PaperSimulationFixtureExecutionResult> ExecuteAsync(
        PaperSimulationDryRunResult dryRun,
        CancellationToken cancellationToken)
    {
        var resultId = new PaperSimulationResultId($"{dryRun.PaperSimulationDryRunResultId.Value}:paper-simulation-fixture-result");
        var existing = await repository.GetByResultIdAsync(resultId, cancellationToken);
        if (existing is not null)
        {
            return new PaperSimulationFixtureExecutionResult(
                existing with { ResultStatus = PaperSimulationResultStatus.DuplicateReturned },
                Persisted: false,
                AlreadyExecuted: true);
        }

        var result = CreateResult(resultId, dryRun, clock.UtcNow);
        if (result.ResultStatus is not PaperSimulationResultStatus.RejectedDryRunNotReady)
        {
            await repository.AddAsync(result, cancellationToken);
        }

        return new PaperSimulationFixtureExecutionResult(
            result,
            Persisted: result.ResultStatus is not PaperSimulationResultStatus.RejectedDryRunNotReady,
            AlreadyExecuted: false);
    }

    private static PaperSimulationFixtureResult CreateResult(
        PaperSimulationResultId resultId,
        PaperSimulationDryRunResult dryRun,
        DateTimeOffset createdAtUtc)
    {
        var ready = dryRun.ResultStatus == PaperSimulationDryRunResultStatus.DryRunResultShapeReady &&
            dryRun.PaperOnly &&
            dryRun.NoExternal &&
            dryRun.NonExecutable &&
            dryRun.NotAnOrder &&
            dryRun.NotSubmitted &&
            dryRun.NoBrokerRoute &&
            dryRun.NoFillCreated &&
            dryRun.NoExecutionReportCreated &&
            dryRun.NoOrderCreated &&
            !dryRun.CreatedOmsOrder &&
            !dryRun.CreatedParentOrder &&
            !dryRun.CreatedChildOrder &&
            !dryRun.CreatedBrokerOrder &&
            !dryRun.CreatedOrderState &&
            !dryRun.CreatedFill &&
            !dryRun.CreatedExecutionReport &&
            !dryRun.SubmittedOrders &&
            !dryRun.CalledBrokerGateway &&
            !dryRun.RequestedLiveMarketData &&
            !dryRun.MutatedLiveTradingState;
        var lines = dryRun.Lines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => CreateLine(resultId, dryRun.PaperSimulationPlanId, x))
            .ToArray();
        var postTradePreview = CreatePostTradePreview(lines);
        var summary = new PaperSimulationFixtureSummary(
            TotalLines: lines.Length,
            SimulatedAppliedLines: lines.Count(x => x.SimulatedLineStatus == PaperSimulationLineResultStatus.PaperApplied),
            BlockedLines: dryRun.BlockedLines.Count,
            RealFillCount: 0,
            ExecutionReportCount: 0,
            OrderCount: 0,
            BrokerRouteCount: 0,
            SimulationState: ready ? "CompletedNoExternalFixture" : "PaperInconclusiveSafe",
            SafetyStatus: "PaperSimulationOnly");

        return new PaperSimulationFixtureResult(
            resultId,
            dryRun.PaperSimulationDryRunResultId,
            dryRun.PaperSimulationPlanId,
            dryRun.PaperExecutionPlanId,
            dryRun.CycleRunId,
            dryRun.QubesRunId,
            dryRun.OperatorDecisionId,
            dryRun.PaperOrderCandidateBatchId,
            createdAtUtc,
            ready ? PaperSimulationResultStatus.CompletedNoExternalFixture : PaperSimulationResultStatus.RejectedDryRunNotReady,
            summary,
            dryRun.AssumptionReport,
            lines,
            dryRun.BlockedLines,
            postTradePreview,
            new PaperSimulationReconciliationPreview(
                PreviewStatus: "ExpectedTargetDriftPreviewOnly",
                PreviewLineCount: lines.Length,
                ExpectedTargetDriftPreviewCreated: true,
                LiveReconciliationClaimCreated: false,
                LivePositionStateMutated: false,
                BrokerStateMutated: false,
                TradingStateMutated: false),
            dryRun.QubesLineagePreserved,
            dryRun.CycleLineagePreserved,
            dryRun.OperatorDecisionLineagePreserved,
            dryRun.PlanLineagePreserved,
            dryRun.PaperCandidateLineagePreserved,
            dryRun.RiskLineagePreserved,
            dryRun.RebalanceIntentLineagePreserved,
            dryRun.LotSizingLineagePreserved,
            dryRun.MissingStaleMarkWarningsPreserved,
            dryRun.DriftAcknowledgementPreserved,
            dryRun.BlockedLineAcknowledgementPreserved,
            dryRun.MissingStaleMarkAcknowledgementPreserved,
            dryRun.OperatorApprovalAcknowledgementPreserved,
            PaperOnly: true,
            NoExternal: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            ResultIsPaperOnly: true,
            NoRealFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoLiveStateMutation: true,
            RealFillEntityCreated: false,
            BrokerExecutionReportEntityCreated: false,
            OmsOrderCreated: false,
            ParentOrderCreated: false,
            ChildOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            MutatedLiveTradingState: false,
            MutatedLivePositionState: false,
            MutatedBrokerState: false,
            ReplayOrShadowReplayIntroduced: false);
    }

    private static PaperSimulationFixtureResultLine CreateLine(
        PaperSimulationResultId resultId,
        PaperSimulationPlanId planId,
        PaperSimulationDryRunLineResult line)
    {
        var appliedQuantity = line.ResultLineStatus == PaperSimulationDryRunLineStatus.ResultLineNotSimulated
            ? line.PaperBaseQuantity
            : null;

        return new PaperSimulationFixtureResultLine(
            new PaperSimulationResultLineId($"{resultId.Value}:{line.NormalizedSymbol}:paper-result-line"),
            resultId,
            line.PaperSimulationDryRunLineResultId,
            planId,
            line.PaperExecutionPlanLineId,
            line.PaperOrderCandidateId,
            line.SourceRebalanceIntentId,
            line.RiskReviewReference,
            line.LotSizingReference,
            line.InstrumentId,
            line.NormalizedSymbol,
            line.Side,
            line.PaperBaseQuantity,
            line.QuantityCurrency,
            appliedQuantity,
            SimulatedNotionalImpact: null,
            appliedQuantity is not null ? PaperSimulationOutcomeCategory.PaperApplied : PaperSimulationOutcomeCategory.PaperInconclusiveSafe,
            appliedQuantity is not null ? PaperSimulationSlippageCategory.FixtureSlippageApplied : PaperSimulationSlippageCategory.NotComputed,
            appliedQuantity is not null ? PaperSimulationFeeCategory.FixtureFeeApplied : PaperSimulationFeeCategory.NotComputed,
            appliedQuantity is not null ? PaperSimulationLineResultStatus.PaperApplied : PaperSimulationLineResultStatus.PaperInconclusiveSafe,
            ResultIsPaperOnly: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NonExecutable: true,
            RealFillEntityCreated: false,
            BrokerExecutionReportEntityCreated: false,
            OmsOrderCreated: false,
            ParentOrderCreated: false,
            ChildOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            Submitted: false);
    }

    private static PaperSimulationPostTradePreview CreatePostTradePreview(
        IReadOnlyList<PaperSimulationFixtureResultLine> lines)
        => new(
            lines.Select(x => new PaperSimulationPositionDeltaPreview(
                x.InstrumentId,
                x.NormalizedSymbol,
                x.Side,
                x.Side == IntentSide.Sell ? -x.SimulatedAppliedQuantity : x.SimulatedAppliedQuantity,
                x.QuantityCurrency,
                SimulatedOnly: true,
                LivePositionStateMutated: false,
                BrokerStateMutated: false,
                TradingStateMutated: false)).ToArray(),
            SimulatedOnly: true,
            LivePositionStateMutated: false,
            BrokerStateMutated: false,
            TradingStateMutated: false);
}
