using System.Globalization;
using System.Text;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperPositionPreviewArchiveStatus
{
    ArchivedNoExternal,
    DuplicateReturned,
    RejectedInvalidPreview,
    InconclusiveSafe
}

public sealed record PaperPositionPreviewArchiveId(string Value);

public sealed record PaperPositionPreviewLineRecord(
    PaperPositionPreviewLineId PaperPositionPreviewLineId,
    PaperSimulationResultLineId SourcePaperSimulationResultLineId,
    string SourcePaperExecutionPlanLineId,
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

public sealed record PaperPositionPreviewRecord(
    PaperPositionPreviewArchiveId PaperPositionPreviewArchiveId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperPositionPreviewStatus PreviewStatus,
    DateTimeOffset CreatedAtUtc,
    int PreviewLineCount,
    string SafetyStatus,
    PaperPositionPreviewArchiveStatus ArchiveStatus,
    IReadOnlyList<PaperPositionPreviewLineRecord> Lines,
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
    bool NotSubmitted,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool BrokerRouteCreated);

public sealed record PaperPositionPreviewOperatorReportLine(
    string NormalizedSymbol,
    IntentSide Side,
    decimal? PaperBaseQuantity,
    string QuantityCurrency,
    decimal? SimulatedPositionDelta,
    PaperPositionPreviewStatus PreviewStatus,
    string SourceLineageReference);

public sealed record PaperPositionPreviewOperatorReport(
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperPositionPreviewArchiveStatus ArchiveStatus,
    string SafetyStatus,
    IReadOnlyList<PaperPositionPreviewOperatorReportLine> Lines,
    int PreviewLineCount,
    bool IncludesPaperPositionPreviewOnlyDisclaimer,
    bool IncludesSimulatedOnlyDisclaimer,
    bool IncludesNoLivePositionMutationDisclaimer,
    bool IncludesNoBrokerPositionMutationDisclaimer,
    bool IncludesNoTradingStateMutationDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoBrokerRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    string Markdown);

public sealed record PaperPositionPreviewArchiveResult(
    PaperPositionPreviewRecord ArchiveRecord,
    PaperPositionPreviewOperatorReport OperatorReport,
    bool Persisted,
    bool AlreadyArchived);

public interface IPaperPositionPreviewArchiveRepository
{
    Task<PaperPositionPreviewRecord?> GetByPreviewIdAsync(
        PaperPositionPreviewId previewId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperPositionPreviewRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperPositionPreviewArchiveRepository : IPaperPositionPreviewArchiveRepository
{
    private readonly List<PaperPositionPreviewRecord> records = [];

    public Task<PaperPositionPreviewRecord?> GetByPreviewIdAsync(
        PaperPositionPreviewId previewId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.PaperPositionPreviewId == previewId));

    public Task AddAsync(PaperPositionPreviewRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.PaperPositionPreviewId == record.PaperPositionPreviewId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class PaperPositionPreviewArchiveService(
    IPaperPositionPreviewArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperPositionPreviewArchiveResult> ArchiveAsync(
        PaperPositionPreview preview,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByPreviewIdAsync(preview.PaperPositionPreviewId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = PaperPositionPreviewArchiveStatus.DuplicateReturned };
            return new PaperPositionPreviewArchiveResult(
                duplicate,
                PaperPositionPreviewReportRenderer.Render(duplicate),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(preview, clock.UtcNow);
        if (record.ArchiveStatus is not PaperPositionPreviewArchiveStatus.RejectedInvalidPreview)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        return new PaperPositionPreviewArchiveResult(
            record,
            PaperPositionPreviewReportRenderer.Render(record),
            Persisted: record.ArchiveStatus is not PaperPositionPreviewArchiveStatus.RejectedInvalidPreview,
            AlreadyArchived: false);
    }

    private static PaperPositionPreviewRecord CreateRecord(
        PaperPositionPreview preview,
        DateTimeOffset createdAtUtc)
    {
        var valid = preview.PreviewStatus == PaperPositionPreviewStatus.PaperPositionPreviewReady &&
            preview.PaperOnly &&
            preview.SimulatedOnly &&
            preview.NoLivePositionMutation &&
            preview.NoBrokerPositionMutation &&
            preview.NoTradingStateMutation &&
            preview.NoFillCreated &&
            preview.NoExecutionReportCreated &&
            preview.NoOrderCreated &&
            preview.NoBrokerRoute &&
            !preview.LivePositionStateMutated &&
            !preview.BrokerPositionStateMutated &&
            !preview.TradingStateMutated &&
            !preview.FillCreated &&
            !preview.ExecutionReportCreated &&
            !preview.OmsOrderCreated &&
            !preview.BrokerOrderCreated &&
            !preview.OrderStateCreated &&
            !preview.SubmittedOrders &&
            preview.Lines.All(x =>
                x.PaperOnly &&
                x.SimulatedOnly &&
                x.NoLivePositionMutation &&
                x.NoBrokerPositionMutation &&
                x.NoTradingStateMutation &&
                x.NoFillCreated &&
                x.NoExecutionReportCreated &&
                x.NoOrderCreated);

        var lines = preview.Lines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PaperPositionPreviewLineRecord(
                x.PaperPositionPreviewLineId,
                x.PaperSimulationResultLineId,
                ExtractExecutionPlanLineId(x.SourceLineageReference),
                x.InstrumentId,
                x.NormalizedSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.QuantityCurrency,
                x.SimulatedPositionDelta,
                x.PreviewStatus,
                x.SourceLineageReference,
                x.PaperOnly,
                x.SimulatedOnly,
                x.NoLivePositionMutation,
                x.NoBrokerPositionMutation,
                x.NoTradingStateMutation,
                x.NoFillCreated,
                x.NoExecutionReportCreated,
                x.NoOrderCreated))
            .ToArray();

        return new PaperPositionPreviewRecord(
            new PaperPositionPreviewArchiveId($"{preview.PaperPositionPreviewId.Value}:archive"),
            preview.PaperPositionPreviewId,
            preview.PaperSimulationResultId,
            preview.PaperSimulationPlanId,
            preview.PaperExecutionPlanId,
            preview.CycleRunId,
            preview.QubesRunId,
            preview.OperatorDecisionId,
            preview.PreviewStatus,
            createdAtUtc,
            lines.Length,
            "PaperPositionPreviewOnly",
            valid ? PaperPositionPreviewArchiveStatus.ArchivedNoExternal : PaperPositionPreviewArchiveStatus.RejectedInvalidPreview,
            lines,
            preview.BlockedLines,
            preview.QubesLineagePreserved,
            preview.CycleLineagePreserved,
            preview.OperatorDecisionLineagePreserved,
            preview.SimulationResultLineagePreserved,
            preview.SimulationPlanLineagePreserved,
            preview.PaperExecutionPlanLineagePreserved,
            preview.PaperCandidateLineagePreserved,
            preview.RiskLineagePreserved,
            preview.RebalanceIntentLineagePreserved,
            preview.LotSizingLineagePreserved,
            preview.MissingStaleMarkWarningsPreserved,
            preview.DriftAcknowledgementPreserved,
            PaperOnly: true,
            SimulatedOnly: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }

    private static string ExtractExecutionPlanLineId(string sourceLineageReference)
    {
        const string Key = "paperExecutionPlanLine=";
        var index = sourceLineageReference.IndexOf(Key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return "unknown-paper-execution-plan-line";
        }

        var value = sourceLineageReference[(index + Key.Length)..].Trim();
        var separator = value.IndexOf(';', StringComparison.Ordinal);
        return separator < 0 ? value : value[..separator].Trim();
    }
}

public static class PaperPositionPreviewReportRenderer
{
    public static PaperPositionPreviewOperatorReport Render(PaperPositionPreviewRecord record)
    {
        var lines = record.Lines
            .Select(x => new PaperPositionPreviewOperatorReportLine(
                x.NormalizedSymbol,
                x.Side,
                x.PaperBaseQuantity,
                x.QuantityCurrency,
                x.SimulatedPositionDelta,
                x.PreviewStatus,
                x.SourceLineageReference))
            .ToArray();

        return new PaperPositionPreviewOperatorReport(
            record.PaperPositionPreviewId,
            record.PaperSimulationResultId,
            record.CycleRunId,
            record.QubesRunId,
            record.OperatorDecisionId,
            record.ArchiveStatus,
            record.SafetyStatus,
            lines,
            record.PreviewLineCount,
            IncludesPaperPositionPreviewOnlyDisclaimer: true,
            IncludesSimulatedOnlyDisclaimer: true,
            IncludesNoLivePositionMutationDisclaimer: true,
            IncludesNoBrokerPositionMutationDisclaimer: true,
            IncludesNoTradingStateMutationDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoBrokerRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            Markdown: RenderMarkdown(record, lines));
    }

    private static string RenderMarkdown(
        PaperPositionPreviewRecord record,
        IReadOnlyList<PaperPositionPreviewOperatorReportLine> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Paper Position Preview Report");
        builder.AppendLine();
        builder.AppendLine($"- PaperPositionPreviewId: {record.PaperPositionPreviewId.Value}");
        builder.AppendLine($"- PaperSimulationResultId: {record.PaperSimulationResultId.Value}");
        builder.AppendLine($"- CycleRunId: {record.CycleRunId}");
        builder.AppendLine($"- QubesRunId: {record.QubesRunId}");
        builder.AppendLine($"- ArchiveStatus: {record.ArchiveStatus}");
        builder.AppendLine($"- SafetyStatus: {record.SafetyStatus}");
        builder.AppendLine();
        builder.AppendLine("Paper position preview only. Simulated-only. no live position mutation, no broker position mutation, no trading-state mutation, no fills, no execution reports, no orders, no broker routes, and no submissions.");
        builder.AppendLine();
        builder.AppendLine("## Preview Lines");
        foreach (var line in lines)
        {
            builder.AppendLine($"- {line.NormalizedSymbol} {line.Side} delta {line.SimulatedPositionDelta?.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency} ({line.PreviewStatus})");
        }

        builder.AppendLine();
        builder.AppendLine("## Lineage");
        builder.AppendLine("- Qubes, cycle, operator decision, simulation result, plan, candidate, risk, rebalance intent, and lot-sizing lineage are preserved.");

        return builder.ToString();
    }
}
