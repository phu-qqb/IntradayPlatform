using System.Globalization;
using System.Text;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperSimulationResultArchiveStatus
{
    ArchivedNoExternal,
    ArchivedWithBlockedLines,
    DuplicateReturned,
    RejectedInvalidResult,
    InconclusiveSafe
}

public sealed record PaperSimulationResultArchiveId(string Value);

public sealed record PaperSimulationResultLineRecord(
    PaperSimulationResultLineId PaperSimulationResultLineId,
    PaperExecutionPlanLineId PaperExecutionPlanLineId,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    PaperSimulationOutcomeCategory SimulatedOutcomeCategory,
    decimal? SimulatedAppliedQuantity,
    decimal? SimulatedNotionalImpact,
    PaperSimulationSlippageCategory SimulatedSlippageCategory,
    PaperSimulationFeeCategory SimulatedFeeCategory,
    PaperSimulationLineResultStatus SimulatedLineStatus,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute);

public sealed record PaperSimulationResultRecord(
    PaperSimulationResultArchiveId PaperSimulationResultArchiveId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperSimulationResultStatus ResultStatus,
    string SafetyStatus,
    DateTimeOffset CreatedAtUtc,
    int SimulatedAppliedLines,
    int BlockedLines,
    int RealFillCount,
    int ExecutionReportCount,
    int OrderCount,
    int BrokerRouteCount,
    PaperSimulationResultArchiveStatus ArchiveStatus,
    IReadOnlyList<PaperSimulationResultLineRecord> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLineRecords,
    PaperSimulationPostTradePreview PostTradePreview,
    PaperSimulationReconciliationPreview ReconciliationPreview,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool BlockedLineAcknowledgementPreserved,
    bool OperatorApprovalAcknowledgementPreserved,
    bool PaperOnly,
    bool NoExternal,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
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
    bool MutatedBrokerState);

public sealed record PaperSimulationOperatorReportLine(
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    decimal? SimulatedAppliedQuantity,
    string QuantityCurrency,
    PaperSimulationOutcomeCategory Outcome,
    PaperSimulationLineResultStatus Status,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute);

public sealed record PaperSimulationOperatorReport(
    PaperSimulationResultId PaperSimulationResultId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperSimulationResultArchiveStatus ArchiveStatus,
    string SafetyStatus,
    IReadOnlyList<PaperSimulationOperatorReportLine> Lines,
    int SimulatedAppliedLines,
    int BlockedLines,
    int RealFillCount,
    int ExecutionReportCount,
    int OrderCount,
    int BrokerRouteCount,
    bool IncludesPaperOnlyDisclaimer,
    bool IncludesNoRealFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoOmsOrderDisclaimer,
    bool IncludesNoBrokerOrderDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    bool IncludesNoBrokerRouteDisclaimer,
    bool IncludesNoLiveStateMutationDisclaimer,
    bool IncludesBlockedLinesSummary,
    bool IncludesPostTradePreview,
    bool IncludesReconciliationPreview,
    string Markdown);

public sealed record PaperSimulationResultArchiveResult(
    PaperSimulationResultRecord ArchiveRecord,
    PaperSimulationOperatorReport OperatorReport,
    bool Persisted,
    bool AlreadyArchived);

public interface IPaperSimulationResultArchiveRepository
{
    Task<PaperSimulationResultRecord?> GetByResultIdAsync(
        PaperSimulationResultId resultId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperSimulationResultRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperSimulationResultArchiveRepository : IPaperSimulationResultArchiveRepository
{
    private readonly List<PaperSimulationResultRecord> records = [];

    public Task<PaperSimulationResultRecord?> GetByResultIdAsync(
        PaperSimulationResultId resultId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.PaperSimulationResultId == resultId));

    public Task AddAsync(PaperSimulationResultRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.PaperSimulationResultId == record.PaperSimulationResultId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class PaperSimulationResultArchiveService(
    IPaperSimulationResultArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperSimulationResultArchiveResult> ArchiveAsync(
        PaperSimulationFixtureResult result,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByResultIdAsync(result.PaperSimulationResultId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = PaperSimulationResultArchiveStatus.DuplicateReturned };
            return new PaperSimulationResultArchiveResult(
                duplicate,
                PaperSimulationOperatorReportRenderer.Render(duplicate),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(result, clock.UtcNow);
        if (record.ArchiveStatus is not PaperSimulationResultArchiveStatus.RejectedInvalidResult)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        return new PaperSimulationResultArchiveResult(
            record,
            PaperSimulationOperatorReportRenderer.Render(record),
            Persisted: record.ArchiveStatus is not PaperSimulationResultArchiveStatus.RejectedInvalidResult,
            AlreadyArchived: false);
    }

    private static PaperSimulationResultRecord CreateRecord(
        PaperSimulationFixtureResult result,
        DateTimeOffset createdAtUtc)
    {
        var valid = result.ResultStatus == PaperSimulationResultStatus.CompletedNoExternalFixture &&
            result.PaperOnly &&
            result.NoExternal &&
            result.NonExecutable &&
            result.NotAnOrder &&
            result.NotSubmitted &&
            result.NoBrokerRoute &&
            result.NoRealFillCreated &&
            result.NoExecutionReportCreated &&
            result.NoOrderCreated &&
            result.NoLiveStateMutation &&
            !result.RealFillEntityCreated &&
            !result.BrokerExecutionReportEntityCreated &&
            !result.OmsOrderCreated &&
            !result.ParentOrderCreated &&
            !result.ChildOrderCreated &&
            !result.BrokerOrderCreated &&
            !result.OrderStateCreated &&
            !result.SubmittedOrders &&
            !result.CalledBrokerGateway &&
            !result.RequestedLiveMarketData &&
            !result.MutatedLiveTradingState &&
            !result.MutatedLivePositionState &&
            !result.MutatedBrokerState;
        var lines = result.Lines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PaperSimulationResultLineRecord(
                x.PaperSimulationResultLineId,
                x.PaperExecutionPlanLineId,
                x.InstrumentId,
                x.NormalizedSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.QuantityCurrency,
                x.SimulatedOutcomeCategory,
                x.SimulatedAppliedQuantity,
                x.SimulatedNotionalImpact,
                x.SimulatedSlippageCategory,
                x.SimulatedFeeCategory,
                x.SimulatedLineStatus,
                x.NoFillCreated,
                x.NoExecutionReportCreated,
                x.NoOrderCreated,
                x.NoBrokerRoute))
            .ToArray();
        var archiveStatus = valid
            ? result.BlockedLines.Count > 0
                ? PaperSimulationResultArchiveStatus.ArchivedWithBlockedLines
                : PaperSimulationResultArchiveStatus.ArchivedNoExternal
            : PaperSimulationResultArchiveStatus.RejectedInvalidResult;

        return new PaperSimulationResultRecord(
            new PaperSimulationResultArchiveId($"{result.PaperSimulationResultId.Value}:archive"),
            result.PaperSimulationResultId,
            result.PaperSimulationPlanId,
            result.PaperExecutionPlanId,
            result.CycleRunId,
            result.QubesRunId,
            result.OperatorDecisionId,
            result.ResultStatus,
            result.Summary.SafetyStatus,
            createdAtUtc,
            result.Summary.SimulatedAppliedLines,
            result.Summary.BlockedLines,
            result.Summary.RealFillCount,
            result.Summary.ExecutionReportCount,
            result.Summary.OrderCount,
            result.Summary.BrokerRouteCount,
            archiveStatus,
            lines,
            result.BlockedLines,
            result.PostTradePreview,
            result.ReconciliationPreview,
            result.QubesLineagePreserved,
            result.CycleLineagePreserved,
            result.OperatorDecisionLineagePreserved,
            result.PlanLineagePreserved,
            result.PlanLineagePreserved,
            result.PaperCandidateLineagePreserved,
            result.RiskLineagePreserved,
            result.RebalanceIntentLineagePreserved,
            result.LotSizingLineagePreserved,
            result.MissingStaleMarkWarningsPreserved,
            result.DriftAcknowledgementPreserved,
            result.BlockedLineAcknowledgementPreserved,
            result.OperatorApprovalAcknowledgementPreserved,
            PaperOnly: true,
            NoExternal: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
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
            MutatedBrokerState: false);
    }
}

public static class PaperSimulationOperatorReportRenderer
{
    public static PaperSimulationOperatorReport Render(PaperSimulationResultRecord record)
    {
        var lines = record.Lines
            .Select(x => new PaperSimulationOperatorReportLine(
                x.NormalizedSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.SimulatedAppliedQuantity,
                x.QuantityCurrency,
                x.SimulatedOutcomeCategory,
                x.SimulatedLineStatus,
                x.NoFillCreated,
                x.NoExecutionReportCreated,
                x.NoOrderCreated,
                x.NoBrokerRoute))
            .ToArray();

        return new PaperSimulationOperatorReport(
            record.PaperSimulationResultId,
            record.CycleRunId,
            record.QubesRunId,
            record.OperatorDecisionId,
            record.ArchiveStatus,
            record.SafetyStatus,
            lines,
            record.SimulatedAppliedLines,
            record.BlockedLines,
            record.RealFillCount,
            record.ExecutionReportCount,
            record.OrderCount,
            record.BrokerRouteCount,
            IncludesPaperOnlyDisclaimer: true,
            IncludesNoRealFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoOmsOrderDisclaimer: true,
            IncludesNoBrokerOrderDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            IncludesNoBrokerRouteDisclaimer: true,
            IncludesNoLiveStateMutationDisclaimer: true,
            IncludesBlockedLinesSummary: true,
            IncludesPostTradePreview: true,
            IncludesReconciliationPreview: true,
            Markdown: RenderMarkdown(record, lines));
    }

    private static string RenderMarkdown(
        PaperSimulationResultRecord record,
        IReadOnlyList<PaperSimulationOperatorReportLine> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Paper Simulation Operator Report");
        builder.AppendLine();
        builder.AppendLine($"- PaperSimulationResultId: {record.PaperSimulationResultId.Value}");
        builder.AppendLine($"- CycleRunId: {record.CycleRunId}");
        builder.AppendLine($"- QubesRunId: {record.QubesRunId}");
        builder.AppendLine($"- ArchiveStatus: {record.ArchiveStatus}");
        builder.AppendLine($"- SafetyStatus: {record.SafetyStatus}");
        builder.AppendLine();
        builder.AppendLine("Paper simulation only. No real fills, no execution reports, no OMS orders, no broker orders, no submissions, no broker route, and no live state mutation.");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine($"- SimulatedAppliedLines: {record.SimulatedAppliedLines.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- BlockedLines: {record.BlockedLines.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- RealFillCount: {record.RealFillCount.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- ExecutionReportCount: {record.ExecutionReportCount.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- OrderCount: {record.OrderCount.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- BrokerRouteCount: {record.BrokerRouteCount.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine();
        builder.AppendLine("## Lines");
        foreach (var line in lines)
        {
            builder.AppendLine($"- {line.NormalizedSymbol} {line.Side} {line.SimulatedAppliedQuantity?.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency} ({line.Status})");
        }

        builder.AppendLine();
        builder.AppendLine("## Previews");
        builder.AppendLine("- Post-trade preview: simulated-only; no live position mutation.");
        builder.AppendLine("- Reconciliation preview: simulated-only; no live reconciliation claim.");

        return builder.ToString();
    }
}
