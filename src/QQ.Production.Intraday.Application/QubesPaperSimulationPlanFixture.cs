using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperSimulationPlanReadinessStatus
{
    PaperSimulationPlanReady,
    PaperSimulationPlanBlocked,
    PaperSimulationPlanRequiresAcknowledgement,
    PaperSimulationPlanInconclusiveSafe
}

public enum PaperSimulationLineReadinessStatus
{
    SimulationLineReady,
    SimulationLineBlocked,
    SimulationLineRequiresMark,
    SimulationLineRequiresLotSizing,
    SimulationLineNonExecutable
}

public enum PaperSimulationAssumptionCategory
{
    FixtureOnly,
    NotRunYet,
    NotModeled,
    NoBrokerRoute,
    NoExternal,
    InconclusiveSafe
}

public enum PaperSimulationPlanArchiveStatus
{
    PreparedNoExternal,
    DuplicateReturned,
    RejectedNotSimulationReady,
    InconclusiveSafe
}

public sealed record PaperSimulationPlanId(string Value);

public sealed record PaperSimulationPlanLineId(string Value);

public sealed record PaperSimulationAssumptionSet(
    string SimulationMode,
    string FillModel,
    string SlippageModel,
    string FeeModel,
    string LatencyModel,
    string MarketDataSource,
    string ExecutionVenue,
    string BrokerRoute,
    IReadOnlyList<PaperSimulationAssumptionCategory> AssumptionCategories,
    bool FixtureOnly,
    bool NoExternal,
    bool NoBrokerRoute,
    bool SimulationNotRun);

public sealed record PaperSimulationPlanLine(
    PaperSimulationPlanLineId PaperSimulationPlanLineId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanLineId PaperExecutionPlanLineId,
    PaperOrderCandidateId PaperOrderCandidateId,
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
    PaperLotSizingStatus QuantityStatus,
    PaperExecutionStyleShape ExecutionStyleShape,
    PaperExecutionTimeInForceShape TimeInForceShape,
    PaperSimulationLineReadinessStatus LineReadinessStatus,
    string LotSizingReference,
    bool PaperOnly,
    bool NoExternal,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool SimulationNotRun,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool CreatesFill,
    bool CreatesExecutionReport);

public sealed record PaperSimulationPlan(
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    DateTimeOffset CreatedAtUtc,
    PaperSimulationPlanReadinessStatus ReadinessStatus,
    PaperSimulationPlanArchiveStatus ArchiveStatus,
    PaperSimulationAssumptionSet Assumptions,
    IReadOnlyList<PaperSimulationPlanLine> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
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
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool SimulationNotRun,
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
    bool RanPaperSimulation,
    bool MutatedLiveTradingState);

public sealed record PaperSimulationPlanFixtureResult(
    PaperSimulationPlan Plan,
    bool Persisted,
    bool AlreadyPrepared);

public interface IPaperSimulationPlanRepository
{
    Task<PaperSimulationPlan?> GetByPlanIdAsync(
        PaperSimulationPlanId planId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperSimulationPlan plan, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperSimulationPlanRepository : IPaperSimulationPlanRepository
{
    private readonly List<PaperSimulationPlan> plans = [];

    public Task<PaperSimulationPlan?> GetByPlanIdAsync(
        PaperSimulationPlanId planId,
        CancellationToken cancellationToken)
        => Task.FromResult(plans.FirstOrDefault(x => x.PaperSimulationPlanId == planId));

    public Task AddAsync(PaperSimulationPlan plan, CancellationToken cancellationToken)
    {
        if (plans.Any(x => x.PaperSimulationPlanId == plan.PaperSimulationPlanId))
        {
            return Task.CompletedTask;
        }

        plans.Add(plan);
        return Task.CompletedTask;
    }
}

public sealed class PaperSimulationPlanFixtureService(
    IPaperSimulationPlanRepository repository,
    IClock clock)
{
    public async Task<PaperSimulationPlanFixtureResult> PrepareAsync(
        PaperExecutionPlanApprovalResult approval,
        CancellationToken cancellationToken)
    {
        var planId = new PaperSimulationPlanId($"{approval.Decision.PaperExecutionPlanId.Value}:simulation-plan-fixture");
        var existing = await repository.GetByPlanIdAsync(planId, cancellationToken);
        if (existing is not null)
        {
            return new PaperSimulationPlanFixtureResult(
                existing with { ArchiveStatus = PaperSimulationPlanArchiveStatus.DuplicateReturned },
                Persisted: false,
                AlreadyPrepared: true);
        }

        var plan = CreatePlan(planId, approval, clock.UtcNow);
        if (plan.ArchiveStatus is not PaperSimulationPlanArchiveStatus.RejectedNotSimulationReady)
        {
            await repository.AddAsync(plan, cancellationToken);
        }

        return new PaperSimulationPlanFixtureResult(
            plan,
            Persisted: plan.ArchiveStatus is not PaperSimulationPlanArchiveStatus.RejectedNotSimulationReady,
            AlreadyPrepared: false);
    }

    private static PaperSimulationPlan CreatePlan(
        PaperSimulationPlanId planId,
        PaperExecutionPlanApprovalResult approval,
        DateTimeOffset createdAtUtc)
    {
        var decision = approval.Decision;
        var archive = approval.ArchiveRecord;
        var ready = decision.DecisionStatus == PaperExecutionPlanApprovalStatus.Recorded &&
            decision.ResultingSimulationReadinessStatus == PaperSimulationReadinessStatus.SimulationReadyNoExternal &&
            decision.BlockedLinesAcknowledged &&
            decision.MissingStaleMarksAcknowledged &&
            decision.DriftAcknowledged &&
            decision.PaperOnly &&
            decision.NoExternal &&
            decision.NonExecutable &&
            decision.NotAnOrder &&
            decision.NotSubmitted &&
            decision.NoBrokerRoute &&
            !decision.RunsPaperSimulation &&
            !decision.CreatesFill &&
            !decision.CreatesExecutionReport &&
            !decision.CreatesOmsOrder &&
            !decision.CreatesParentOrder &&
            !decision.CreatesChildOrder &&
            !decision.CreatesBrokerOrder &&
            !decision.SubmitsOrders;
        var lines = archive.PlanLines
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => CreateLine(planId, x))
            .ToArray();

        return new PaperSimulationPlan(
            planId,
            decision.PaperExecutionPlanId,
            decision.CycleRunId,
            decision.QubesRunId,
            decision.OperatorDecisionId,
            decision.PaperOrderCandidateBatchId,
            createdAtUtc,
            ready
                ? PaperSimulationPlanReadinessStatus.PaperSimulationPlanReady
                : PaperSimulationPlanReadinessStatus.PaperSimulationPlanBlocked,
            ready
                ? PaperSimulationPlanArchiveStatus.PreparedNoExternal
                : PaperSimulationPlanArchiveStatus.RejectedNotSimulationReady,
            CreateAssumptions(),
            lines,
            archive.BlockedLines,
            decision.QubesLineagePreserved,
            !string.IsNullOrWhiteSpace(decision.CycleRunId),
            decision.OperatorDecisionId.Value.Length > 0,
            decision.PlanLineagePreserved,
            decision.PaperCandidateLineagePreserved,
            decision.RiskLineagePreserved,
            decision.RebalanceIntentLineagePreserved,
            decision.LotSizingLineagePreserved,
            decision.MissingStaleMarkWarningsPreserved,
            decision.DriftAcknowledgementPreserved,
            decision.BlockedLinesAcknowledged,
            decision.MissingStaleMarksAcknowledged,
            decision.SimulationReadinessOnly,
            PaperOnly: true,
            NoExternal: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            SimulationNotRun: true,
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
            RanPaperSimulation: false,
            MutatedLiveTradingState: false);
    }

    private static PaperSimulationPlanLine CreateLine(
        PaperSimulationPlanId planId,
        PaperExecutionPlanLineRecord line)
        => new(
            new PaperSimulationPlanLineId($"{planId.Value}:{line.NormalizedSymbol}:line"),
            planId,
            line.PaperExecutionPlanLineId,
            line.PaperOrderCandidateId,
            line.SourceRebalanceIntentId,
            line.RiskReviewReference,
            line.InstrumentId,
            line.NormalizedSymbol,
            line.PaperTradableSymbol,
            line.Side,
            line.PaperBaseQuantity,
            line.QuantityCurrency,
            line.NotionalCurrency,
            line.LotSize,
            line.QuantityRoundingMode,
            line.QuantityStatus,
            line.ExecutionStyleShape,
            line.TimeInForceShape,
            ToLineReadiness(line.PlanLineStatus),
            $"planLine={line.PaperExecutionPlanLineId.Value}; quantityStatus={line.QuantityStatus}; lotSize={line.LotSize}",
            PaperOnly: true,
            NoExternal: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            SimulationNotRun: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false);

    private static PaperSimulationLineReadinessStatus ToLineReadiness(PaperExecutionPlanLineStatus status)
        => status switch
        {
            PaperExecutionPlanLineStatus.PaperLineReady => PaperSimulationLineReadinessStatus.SimulationLineReady,
            PaperExecutionPlanLineStatus.PaperLineRequiresMark => PaperSimulationLineReadinessStatus.SimulationLineRequiresMark,
            PaperExecutionPlanLineStatus.PaperLineRequiresLotSizing => PaperSimulationLineReadinessStatus.SimulationLineRequiresLotSizing,
            PaperExecutionPlanLineStatus.PaperLineBlockedByRisk => PaperSimulationLineReadinessStatus.SimulationLineBlocked,
            _ => PaperSimulationLineReadinessStatus.SimulationLineNonExecutable
        };

    private static PaperSimulationAssumptionSet CreateAssumptions()
        => new(
            SimulationMode: "PaperNoExternal",
            FillModel: "NotRunYet",
            SlippageModel: "FixtureOnly",
            FeeModel: "FixtureOnly",
            LatencyModel: "NotModeled",
            MarketDataSource: "FixtureOnly",
            ExecutionVenue: "None",
            BrokerRoute: "None",
            [
                PaperSimulationAssumptionCategory.FixtureOnly,
                PaperSimulationAssumptionCategory.NotRunYet,
                PaperSimulationAssumptionCategory.NotModeled,
                PaperSimulationAssumptionCategory.NoBrokerRoute,
                PaperSimulationAssumptionCategory.NoExternal
            ],
            FixtureOnly: true,
            NoExternal: true,
            NoBrokerRoute: true,
            SimulationNotRun: true);
}
