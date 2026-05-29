using System.Globalization;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperExecutionPlanArchiveStatus
{
    ArchivedNoExternal,
    ArchivedWithBlockedLines,
    DuplicateReturned,
    RejectedInvalidPlan,
    InconclusiveSafe
}

public enum PaperExecutionPlanBlotterStatus
{
    PaperPlanArchived,
    PaperLineReady,
    PaperLineBlocked,
    PaperLineNonExecutable,
    PaperLineInconclusiveSafe
}

public sealed record PaperExecutionPlanLineRecord(
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
    PaperQuantityShapeCategory QuantityShapeCategory,
    PaperLotSizingStatus QuantityStatus,
    PaperExecutionStyleShape ExecutionStyleShape,
    PaperExecutionTimeInForceShape TimeInForceShape,
    string SequencingGroup,
    int Priority,
    PaperExecutionPlanLineStatus PlanLineStatus,
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

public sealed record PaperExecutionPlanBatchRecord(
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    string PaperRiskReviewId,
    IReadOnlyList<PaperExecutionPlanMode> PlanModes,
    PaperExecutionPlanStatus PlanStatus,
    DateTimeOffset CreatedAtUtc,
    int ReadyLineCount,
    int BlockedLineCount,
    string SafetyStatus,
    PaperExecutionPlanArchiveStatus ArchiveStatus,
    IReadOnlyList<PaperExecutionPlanLineRecord> PlanLines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool RiskLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool NoExternal,
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

public sealed record PaperExecutionPlanBlotterLine(
    PaperExecutionPlanLineId PlanLineId,
    PaperExecutionPlanId PlanId,
    PaperOrderCandidateId CandidateId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string Instrument,
    string PaperTradableSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    PaperExecutionStyleShape ExecutionStyleShape,
    PaperExecutionTimeInForceShape TimeInForceShape,
    PaperExecutionPlanBlotterStatus Status,
    string NonExecutableReason,
    string RiskReference,
    string LineageReference,
    bool PaperOnly,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NonExecutable,
    bool NotSubmitted,
    bool NoFill,
    bool NoExecutionReport);

public sealed record PaperExecutionPlanBlotter(
    PaperExecutionPlanId PlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperExecutionPlanStatus PlanStatus,
    IReadOnlyList<PaperExecutionPlanBlotterLine> Lines,
    IReadOnlyList<string> BlockedLineSummaries,
    IReadOnlyList<string> Disclaimers,
    string NextActionRecommendation);

public sealed record PaperExecutionPlanArchiveResult(
    PaperExecutionPlanBatchRecord ArchiveRecord,
    PaperExecutionPlanBlotter Blotter,
    string BlotterMarkdown,
    bool Persisted,
    bool AlreadyArchived);

public interface IPaperExecutionPlanArchiveRepository
{
    Task<PaperExecutionPlanBatchRecord?> GetByPlanIdAsync(
        PaperExecutionPlanId planId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperExecutionPlanBatchRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperExecutionPlanArchiveRepository : IPaperExecutionPlanArchiveRepository
{
    private readonly List<PaperExecutionPlanBatchRecord> records = [];

    public Task<PaperExecutionPlanBatchRecord?> GetByPlanIdAsync(
        PaperExecutionPlanId planId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.PaperExecutionPlanId == planId));

    public Task AddAsync(PaperExecutionPlanBatchRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.PaperExecutionPlanId == record.PaperExecutionPlanId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class PaperExecutionPlanArchiveService(
    IPaperExecutionPlanArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperExecutionPlanArchiveResult> ArchiveAsync(
        PaperExecutionPlanBatch plan,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByPlanIdAsync(plan.PaperExecutionPlanId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = PaperExecutionPlanArchiveStatus.DuplicateReturned };
            var existingBlotter = PaperExecutionPlanBlotterRenderer.CreateBlotter(duplicate);
            return new PaperExecutionPlanArchiveResult(
                duplicate,
                existingBlotter,
                PaperExecutionPlanBlotterRenderer.RenderMarkdown(existingBlotter),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(plan, clock.UtcNow);
        if (record.ArchiveStatus is not PaperExecutionPlanArchiveStatus.RejectedInvalidPlan)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        var blotter = PaperExecutionPlanBlotterRenderer.CreateBlotter(record);
        return new PaperExecutionPlanArchiveResult(
            record,
            blotter,
            PaperExecutionPlanBlotterRenderer.RenderMarkdown(blotter),
            Persisted: record.ArchiveStatus is not PaperExecutionPlanArchiveStatus.RejectedInvalidPlan,
            AlreadyArchived: false);
    }

    private static PaperExecutionPlanBatchRecord CreateRecord(
        PaperExecutionPlanBatch plan,
        DateTimeOffset createdAtUtc)
    {
        var lines = plan.Lines.Select(x => new PaperExecutionPlanLineRecord(
                x.PaperExecutionPlanLineId,
                x.PaperOrderCandidateId,
                x.SourceRebalanceIntentId,
                x.RiskReviewReference,
                x.InstrumentId,
                x.NormalizedSymbol,
                x.PaperTradableSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.QuantityCurrency,
                x.NotionalCurrency,
                x.LotSize,
                x.QuantityRoundingMode,
                x.QuantityShapeCategory,
                x.QuantityStatus,
                x.ExecutionStyleShape,
                x.TimeInForceShape,
                x.SequencingGroup,
                x.Priority,
                x.PlanLineStatus,
                x.BlockReason,
                x.NonExecutableReason,
                x.PaperOnly,
                x.NonExecutable,
                x.NotAnOrder,
                x.NotSubmitted,
                x.NoBrokerRoute,
                x.CreatesOmsOrder,
                x.CreatesParentOrder,
                x.CreatesChildOrder,
                x.CreatesBrokerOrder,
                x.CreatesFill,
                x.CreatesExecutionReport))
            .ToArray();
        var invalidPlan = !plan.PaperOnly ||
            !plan.NonExecutable ||
            !plan.NotAnOrder ||
            !plan.NotSubmitted ||
            !plan.NoBrokerRoute ||
            plan.CreatedOmsOrder ||
            plan.CreatedParentOrder ||
            plan.CreatedChildOrder ||
            plan.CreatedBrokerOrder ||
            plan.CreatedFill ||
            plan.CreatedExecutionReport ||
            plan.SubmittedOrders ||
            plan.CalledBrokerGateway ||
            plan.RequestedLiveMarketData ||
            plan.StartedApiOrWorker ||
            plan.StartedSchedulerOrBackgroundJob ||
            plan.MutatedLiveTradingState ||
            lines.Any(x =>
                !x.PaperOnly ||
                !x.NonExecutable ||
                !x.NotAnOrder ||
                !x.NotSubmitted ||
                !x.NoBrokerRoute ||
                x.CreatesOmsOrder ||
                x.CreatesParentOrder ||
                x.CreatesChildOrder ||
                x.CreatesBrokerOrder ||
                x.CreatesFill ||
                x.CreatesExecutionReport);
        var status = invalidPlan
            ? PaperExecutionPlanArchiveStatus.RejectedInvalidPlan
            : plan.BlockedLines.Count > 0
                ? PaperExecutionPlanArchiveStatus.ArchivedWithBlockedLines
                : PaperExecutionPlanArchiveStatus.ArchivedNoExternal;

        return new PaperExecutionPlanBatchRecord(
            plan.PaperExecutionPlanId,
            plan.CycleRunId,
            plan.QubesRunId,
            plan.OperatorDecisionId,
            plan.PaperOrderCandidateBatchId,
            plan.PaperRiskReviewId,
            plan.PlanModes,
            plan.PlanStatus,
            createdAtUtc,
            lines.Count(x => x.PlanLineStatus == PaperExecutionPlanLineStatus.PaperLineReady),
            plan.BlockedLines.Count + lines.Count(x => x.PlanLineStatus != PaperExecutionPlanLineStatus.PaperLineReady),
            "NoExternalPlanShapeOnly",
            status,
            lines,
            plan.BlockedLines,
            plan.QubesLineagePreserved,
            plan.OperatorDecisionLineagePreserved,
            plan.RiskLineagePreserved,
            plan.PaperCandidateLineagePreserved,
            plan.RebalanceIntentLineagePreserved,
            plan.LotSizingLineagePreserved,
            plan.MissingStaleMarkWarningsPreserved,
            plan.DriftAcknowledgementPreserved,
            !invalidPlan,
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
}

public static class PaperExecutionPlanBlotterRenderer
{
    public static PaperExecutionPlanBlotter CreateBlotter(PaperExecutionPlanBatchRecord record)
    {
        var lines = record.PlanLines
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PaperExecutionPlanBlotterLine(
                x.PaperExecutionPlanLineId,
                record.PaperExecutionPlanId,
                x.PaperOrderCandidateId,
                record.CycleRunId,
                record.QubesRunId,
                record.OperatorDecisionId,
                x.NormalizedSymbol,
                x.PaperTradableSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.QuantityCurrency,
                x.ExecutionStyleShape,
                x.TimeInForceShape,
                ToBlotterStatus(x),
                x.NonExecutableReason,
                x.RiskReviewReference.SourceRiskReviewLineId,
                $"candidate={x.PaperOrderCandidateId.Value}; intent={x.SourceRebalanceIntentId}; risk={x.RiskReviewReference.SourceRiskReviewLineId}",
                x.PaperOnly,
                x.NotAnOrder,
                x.NoBrokerRoute,
                x.NonExecutable,
                x.NotSubmitted,
                !x.CreatesFill,
                !x.CreatesExecutionReport))
            .ToArray();

        return new PaperExecutionPlanBlotter(
            record.PaperExecutionPlanId,
            record.CycleRunId,
            record.QubesRunId,
            record.OperatorDecisionId,
            record.PlanStatus,
            lines,
            record.BlockedLines
                .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Symbol}: {x.Result} ({x.Reason})")
                .ToArray(),
            [
                "Paper-only execution plan archive.",
                "No order created.",
                "No broker route exists.",
                "Plan lines are not executable.",
                "Plan lines were not submitted.",
                "No fills were created.",
                "No execution reports were created."
            ],
            "Proceed to R015 operator approval and no-external simulation-readiness work; do not create OMS orders or broker routes.");
    }

    public static string RenderMarkdown(PaperExecutionPlanBlotter blotter)
    {
        var rows = blotter.Lines.Count == 0
            ? "| None | | | | | | | | | | | | | | | | | | | | | | |"
            : string.Join(Environment.NewLine, blotter.Lines.Select(x => string.Join(" | ", [
                $"| {x.PlanLineId.Value}",
                x.PlanId.Value,
                x.CycleRunId,
                x.QubesRunId,
                x.OperatorDecisionId.Value,
                x.Instrument,
                x.PaperTradableSymbol,
                x.Side.ToString(),
                FormatNullable(x.PaperBaseQuantity),
                x.QuantityCurrency,
                x.ExecutionStyleShape.ToString(),
                x.TimeInForceShape.ToString(),
                x.Status.ToString(),
                x.NonExecutableReason,
                x.RiskReference,
                x.LineageReference,
                x.PaperOnly.ToString(CultureInfo.InvariantCulture),
                x.NoOrderCreated.ToString(CultureInfo.InvariantCulture),
                x.NoBrokerRoute.ToString(CultureInfo.InvariantCulture),
                x.NonExecutable.ToString(CultureInfo.InvariantCulture),
                x.NotSubmitted.ToString(CultureInfo.InvariantCulture),
                x.NoFill.ToString(CultureInfo.InvariantCulture),
                $"{x.NoExecutionReport.ToString(CultureInfo.InvariantCulture)} |"
            ])));
        var blocked = blotter.BlockedLineSummaries.Count == 0
            ? "- None."
            : string.Join(Environment.NewLine, blotter.BlockedLineSummaries.Select(x => $"- {x}"));
        var disclaimers = string.Join(Environment.NewLine, blotter.Disclaimers.Select(x => $"- {x}"));

        return string.Join(Environment.NewLine, [
            "# Paper Execution Plan Blotter",
            "",
            $"PaperExecutionPlanId: {blotter.PlanId.Value}",
            $"CycleRunId: {blotter.CycleRunId}",
            $"QubesRunId: {blotter.QubesRunId}",
            $"OperatorDecisionId: {blotter.OperatorDecisionId.Value}",
            $"PlanStatus: {blotter.PlanStatus}",
            "",
            "## Plan Lines",
            "| PlanLineId | PlanId | Cycle | Qubes | OperatorDecision | Instrument | PaperTradableSymbol | Side | PaperBaseQuantity | QuantityCurrency | ExecutionStyleShape | TIFShape | Status | NonExecutableReason | RiskReference | Lineage | PaperOnly | NoOrderCreated | NoBrokerRoute | NonExecutable | NotSubmitted | NoFill | NoExecutionReport |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
            rows,
            "",
            "## Blocked R011 Lines",
            blocked,
            "",
            "## No-External / No-Order / No-Route Disclaimer",
            disclaimers,
            "",
            "## Next Action",
            blotter.NextActionRecommendation,
            ""
        ]);
    }

    private static PaperExecutionPlanBlotterStatus ToBlotterStatus(PaperExecutionPlanLineRecord line)
        => line.PlanLineStatus switch
        {
            PaperExecutionPlanLineStatus.PaperLineReady => PaperExecutionPlanBlotterStatus.PaperLineReady,
            PaperExecutionPlanLineStatus.PaperLineNonExecutable => PaperExecutionPlanBlotterStatus.PaperLineNonExecutable,
            PaperExecutionPlanLineStatus.PaperLineRequiresMark or
                PaperExecutionPlanLineStatus.PaperLineRequiresLotSizing or
                PaperExecutionPlanLineStatus.PaperLineRequiresInstrumentConvention or
                PaperExecutionPlanLineStatus.PaperLineBlockedByRisk => PaperExecutionPlanBlotterStatus.PaperLineBlocked,
            _ => PaperExecutionPlanBlotterStatus.PaperLineInconclusiveSafe
        };

    private static string FormatNullable(decimal? value)
        => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
}
