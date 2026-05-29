using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperSimulationDryRunResultStatus
{
    DryRunResultShapeReady,
    SimulationNotRun,
    NoFillCreated,
    NoExecutionReportCreated,
    NoOrderCreated,
    NoBrokerRoute,
    InconclusiveSafe
}

public enum PaperSimulationDryRunLineStatus
{
    ResultLineNotSimulated,
    ResultLineBlocked,
    ResultLineInconclusiveSafe
}

public enum PaperSimulatedOutcomeCategory
{
    SimulationNotRun,
    NoFillCreated,
    NoExecutionReportCreated,
    NoOrderCreated,
    NoBrokerRoute,
    InconclusiveSafe
}

public enum PaperSimulatedFillStatus
{
    SimulationNotRun,
    NoFillCreated
}

public enum PaperSimulatedPnLImpactStatus
{
    SimulationNotRun,
    NotComputed
}

public enum PaperSimulationDryRunArchiveStatus
{
    ShapePreparedNoExternal,
    DuplicateReturned,
    RejectedSimulationPlanNotReady,
    InconclusiveSafe
}

public sealed record PaperSimulationDryRunResultId(string Value);

public sealed record PaperSimulationDryRunLineResultId(string Value);

public sealed record PaperSimulationDryRunAssumptionReport(
    string SimulationMode,
    string FillModel,
    string SlippageModel,
    string FeeModel,
    string LatencyModel,
    string MarketDataSource,
    string ExecutionVenue,
    string BrokerRoute,
    bool FixtureOnly,
    bool NoExternal,
    bool NoBrokerRoute,
    bool SimulationNotRun,
    IReadOnlyList<PaperSimulationAssumptionCategory> AssumptionCategories);

public sealed record PaperSimulationDryRunSummary(
    int TotalLines,
    int ReadyLines,
    int BlockedLines,
    string SimulationState,
    int FillCount,
    int ExecutionReportCount,
    int OrderCount,
    int BrokerRouteCount,
    string SafetyStatus);

public sealed record PaperSimulationDryRunLineResult(
    PaperSimulationDryRunLineResultId PaperSimulationDryRunLineResultId,
    PaperSimulationDryRunResultId PaperSimulationDryRunResultId,
    PaperSimulationPlanLineId PaperSimulationPlanLineId,
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
    string NotionalCurrency,
    PaperExecutionStyleShape IntendedExecutionStyleShape,
    PaperExecutionTimeInForceShape IntendedTimeInForceShape,
    PaperSimulatedOutcomeCategory SimulatedOutcomeCategory,
    PaperSimulatedFillStatus SimulatedFillStatus,
    PaperSimulatedPnLImpactStatus SimulatedPnLImpactStatus,
    PaperSimulationAssumptionCategory SlippageAssumptionCategory,
    PaperSimulationAssumptionCategory FeeAssumptionCategory,
    PaperSimulationDryRunLineStatus ResultLineStatus,
    bool SimulationNotRun,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NonExecutable,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool Submitted);

public sealed record PaperSimulationDryRunResult(
    PaperSimulationDryRunResultId PaperSimulationDryRunResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    IReadOnlyList<PaperExecutionPlanLineId> PaperExecutionPlanLineIds,
    IReadOnlyList<PaperOrderCandidateRiskReference> RiskReviewReferences,
    IReadOnlyList<string> LotSizingReferences,
    DateTimeOffset CreatedAtUtc,
    PaperSimulationDryRunResultStatus ResultStatus,
    PaperSimulationDryRunArchiveStatus ArchiveStatus,
    PaperSimulationDryRunAssumptionReport AssumptionReport,
    PaperSimulationDryRunSummary Summary,
    IReadOnlyList<PaperSimulationDryRunLineResult> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
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
    bool SimulationNotRun,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool CreatedOmsOrder,
    bool CreatedParentOrder,
    bool CreatedChildOrder,
    bool CreatedBrokerOrder,
    bool CreatedOrderState,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool RanPaperSimulation,
    bool MutatedLiveTradingState);

public sealed record PaperSimulationDryRunResultShapeResult(
    PaperSimulationDryRunResult Result,
    bool Persisted,
    bool AlreadyPrepared);

public interface IPaperSimulationDryRunResultRepository
{
    Task<PaperSimulationDryRunResult?> GetByResultIdAsync(
        PaperSimulationDryRunResultId resultId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperSimulationDryRunResult result, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperSimulationDryRunResultRepository : IPaperSimulationDryRunResultRepository
{
    private readonly List<PaperSimulationDryRunResult> results = [];

    public Task<PaperSimulationDryRunResult?> GetByResultIdAsync(
        PaperSimulationDryRunResultId resultId,
        CancellationToken cancellationToken)
        => Task.FromResult(results.FirstOrDefault(x => x.PaperSimulationDryRunResultId == resultId));

    public Task AddAsync(PaperSimulationDryRunResult result, CancellationToken cancellationToken)
    {
        if (results.Any(x => x.PaperSimulationDryRunResultId == result.PaperSimulationDryRunResultId))
        {
            return Task.CompletedTask;
        }

        results.Add(result);
        return Task.CompletedTask;
    }
}

public sealed class PaperSimulationDryRunResultShapeService(
    IPaperSimulationDryRunResultRepository repository,
    IClock clock)
{
    public async Task<PaperSimulationDryRunResultShapeResult> CreateAsync(
        PaperSimulationPlan plan,
        CancellationToken cancellationToken)
    {
        var resultId = new PaperSimulationDryRunResultId($"{plan.PaperSimulationPlanId.Value}:dry-run-result-shape");
        var existing = await repository.GetByResultIdAsync(resultId, cancellationToken);
        if (existing is not null)
        {
            return new PaperSimulationDryRunResultShapeResult(
                existing with { ArchiveStatus = PaperSimulationDryRunArchiveStatus.DuplicateReturned },
                Persisted: false,
                AlreadyPrepared: true);
        }

        var result = CreateResult(resultId, plan, clock.UtcNow);
        if (result.ArchiveStatus is not PaperSimulationDryRunArchiveStatus.RejectedSimulationPlanNotReady)
        {
            await repository.AddAsync(result, cancellationToken);
        }

        return new PaperSimulationDryRunResultShapeResult(
            result,
            Persisted: result.ArchiveStatus is not PaperSimulationDryRunArchiveStatus.RejectedSimulationPlanNotReady,
            AlreadyPrepared: false);
    }

    private static PaperSimulationDryRunResult CreateResult(
        PaperSimulationDryRunResultId resultId,
        PaperSimulationPlan plan,
        DateTimeOffset createdAtUtc)
    {
        var planReady = plan.ReadinessStatus == PaperSimulationPlanReadinessStatus.PaperSimulationPlanReady &&
            plan.ArchiveStatus is PaperSimulationPlanArchiveStatus.PreparedNoExternal or PaperSimulationPlanArchiveStatus.DuplicateReturned &&
            plan.PaperOnly &&
            plan.NoExternal &&
            plan.NonExecutable &&
            plan.NotAnOrder &&
            plan.NotSubmitted &&
            plan.NoBrokerRoute &&
            plan.SimulationNotRun &&
            plan.NoFillCreated &&
            plan.NoExecutionReportCreated &&
            !plan.CreatedOmsOrder &&
            !plan.CreatedParentOrder &&
            !plan.CreatedChildOrder &&
            !plan.CreatedBrokerOrder &&
            !plan.CreatedFill &&
            !plan.CreatedExecutionReport &&
            !plan.SubmittedOrders &&
            !plan.RanPaperSimulation;
        var lines = plan.Lines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => CreateLine(resultId, x))
            .ToArray();
        var summary = new PaperSimulationDryRunSummary(
            TotalLines: lines.Length,
            ReadyLines: lines.Count(x => x.ResultLineStatus == PaperSimulationDryRunLineStatus.ResultLineNotSimulated),
            BlockedLines: plan.BlockedLines.Count,
            SimulationState: "NotRun",
            FillCount: 0,
            ExecutionReportCount: 0,
            OrderCount: 0,
            BrokerRouteCount: 0,
            SafetyStatus: "NoExternalResultShapeOnly");

        return new PaperSimulationDryRunResult(
            resultId,
            plan.PaperSimulationPlanId,
            plan.PaperExecutionPlanId,
            plan.CycleRunId,
            plan.QubesRunId,
            plan.OperatorDecisionId,
            plan.PaperOrderCandidateBatchId,
            plan.Lines.Select(x => x.PaperExecutionPlanLineId).ToArray(),
            plan.Lines.Select(x => x.RiskReviewReference).Distinct().ToArray(),
            plan.Lines.Select(x => x.LotSizingReference).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            createdAtUtc,
            planReady
                ? PaperSimulationDryRunResultStatus.DryRunResultShapeReady
                : PaperSimulationDryRunResultStatus.InconclusiveSafe,
            planReady
                ? PaperSimulationDryRunArchiveStatus.ShapePreparedNoExternal
                : PaperSimulationDryRunArchiveStatus.RejectedSimulationPlanNotReady,
            CreateAssumptionReport(plan.Assumptions),
            summary,
            lines,
            plan.BlockedLines,
            plan.QubesLineagePreserved,
            plan.CycleLineagePreserved,
            plan.OperatorDecisionLineagePreserved,
            plan.PaperExecutionPlanLineagePreserved,
            plan.PaperCandidateLineagePreserved,
            plan.RiskLineagePreserved,
            plan.RebalanceIntentLineagePreserved,
            plan.LotSizingLineagePreserved,
            plan.MissingStaleMarkWarningsPreserved,
            plan.DriftAcknowledgementPreserved,
            plan.BlockedLineAcknowledgementPreserved,
            plan.MissingStaleMarkAcknowledgementPreserved,
            plan.OperatorApprovalAcknowledgementPreserved,
            PaperOnly: true,
            NoExternal: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            SimulationNotRun: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            CreatedOmsOrder: false,
            CreatedParentOrder: false,
            CreatedChildOrder: false,
            CreatedBrokerOrder: false,
            CreatedOrderState: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            RanPaperSimulation: false,
            MutatedLiveTradingState: false);
    }

    private static PaperSimulationDryRunLineResult CreateLine(
        PaperSimulationDryRunResultId resultId,
        PaperSimulationPlanLine line)
        => new(
            new PaperSimulationDryRunLineResultId($"{resultId.Value}:{line.NormalizedSymbol}:line-result"),
            resultId,
            line.PaperSimulationPlanLineId,
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
            line.NotionalCurrency,
            line.ExecutionStyleShape,
            line.TimeInForceShape,
            PaperSimulatedOutcomeCategory.SimulationNotRun,
            PaperSimulatedFillStatus.NoFillCreated,
            PaperSimulatedPnLImpactStatus.NotComputed,
            PaperSimulationAssumptionCategory.FixtureOnly,
            PaperSimulationAssumptionCategory.FixtureOnly,
            ToResultLineStatus(line.LineReadinessStatus),
            SimulationNotRun: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NonExecutable: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            Submitted: false);

    private static PaperSimulationDryRunLineStatus ToResultLineStatus(PaperSimulationLineReadinessStatus status)
        => status switch
        {
            PaperSimulationLineReadinessStatus.SimulationLineReady => PaperSimulationDryRunLineStatus.ResultLineNotSimulated,
            PaperSimulationLineReadinessStatus.SimulationLineBlocked => PaperSimulationDryRunLineStatus.ResultLineBlocked,
            _ => PaperSimulationDryRunLineStatus.ResultLineInconclusiveSafe
        };

    private static PaperSimulationDryRunAssumptionReport CreateAssumptionReport(
        PaperSimulationAssumptionSet assumptions)
        => new(
            assumptions.SimulationMode,
            assumptions.FillModel,
            assumptions.SlippageModel,
            assumptions.FeeModel,
            assumptions.LatencyModel,
            assumptions.MarketDataSource,
            assumptions.ExecutionVenue,
            assumptions.BrokerRoute,
            assumptions.FixtureOnly,
            assumptions.NoExternal,
            assumptions.NoBrokerRoute,
            assumptions.SimulationNotRun,
            assumptions.AssumptionCategories);
}
