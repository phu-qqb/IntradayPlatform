using System.Globalization;
using System.Text;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperLedgerPreviewArchiveStatus
{
    ArchivedNoExternal,
    DuplicateReturned,
    RejectedInvalidPreview,
    InconclusiveSafe
}

public enum PaperLedgerCommitDecisionType
{
    ApprovePaperLedgerCommitReadiness,
    Hold,
    Reject,
    RequestLedgerFix,
    RequestRiskReview
}

public enum PaperLedgerCommitDecisionStatus
{
    Recorded,
    RejectedByGate,
    DuplicateReturned,
    InconclusiveSafe
}

public enum PaperLedgerCommitReadinessStatus
{
    PaperLedgerCommitReadyNoExternal,
    HeldForRiskReview,
    HeldForMissingMarks,
    HeldForDrift,
    HeldForBlockedLines,
    Rejected,
    InconclusiveSafe
}

public enum PaperLedgerCommitReadinessReasonCategory
{
    AcceptedForPaperLedgerCommitReadiness,
    HeldDueToBlockedLines,
    HeldDueToMissingMarks,
    HeldDueToDrift,
    HeldDueToRiskReview,
    RejectedDueToValidationFailure,
    RejectedDueToMutationRisk,
    InconclusiveSafe
}

public sealed record PaperLedgerPreviewArchiveId(string Value);

public sealed record PaperLedgerPreviewLineRecord(
    PaperPositionLedgerPreviewLineId PaperPositionLedgerPreviewLineId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    InstrumentId InstrumentId,
    string CurrencyOrSymbol,
    decimal StartingPaperQuantity,
    decimal SimulatedDeltaQuantity,
    decimal PreviewEndingPaperQuantity,
    string QuantityCurrency,
    PaperPositionLedgerPreviewStatus LedgerPreviewStatus,
    string SourceLineageReference,
    bool PaperOnly,
    bool PreviewOnly,
    bool NoPaperLedgerCommit,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted);

public sealed record PaperLedgerPreviewRecord(
    PaperLedgerPreviewArchiveId PaperLedgerPreviewArchiveId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperPositionLedgerPreviewStatus PreviewStatus,
    DateTimeOffset CreatedAtUtc,
    int PreviewLineCount,
    int BlockedLineCount,
    string SafetyStatus,
    PaperLedgerPreviewArchiveStatus ArchiveStatus,
    IReadOnlyList<PaperLedgerPreviewLineRecord> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool PositionPreviewLineagePreserved,
    bool LedgerPreviewLineagePreserved,
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
    bool PreviewOnly,
    bool NoExternal,
    bool NoPaperLedgerCommit,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted,
    int LivePositionMutationCount,
    int BrokerPositionMutationCount,
    int ProductionLedgerMutationCount,
    int TradingStateMutationCount,
    bool PaperLedgerStateCommitted,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool ProductionLedgerStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool BrokerRouteCreated);

public sealed record PaperLedgerPreviewOperatorReport(
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    PaperLedgerPreviewArchiveStatus ArchiveStatus,
    string SafetyStatus,
    IReadOnlyList<PaperLedgerPreviewLineRecord> Lines,
    bool IncludesPaperLedgerPreviewOnlyDisclaimer,
    bool IncludesSimulatedOnlyDisclaimer,
    bool IncludesNoPaperLedgerCommitDisclaimer,
    bool IncludesNoLivePositionMutationDisclaimer,
    bool IncludesNoBrokerPositionMutationDisclaimer,
    bool IncludesNoProductionLedgerMutationDisclaimer,
    bool IncludesNoTradingStateMutationDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoBrokerRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    string Markdown);

public sealed record PaperLedgerPreviewArchiveResult(
    PaperLedgerPreviewRecord ArchiveRecord,
    PaperLedgerPreviewOperatorReport OperatorReport,
    bool Persisted,
    bool AlreadyArchived);

public sealed record PaperLedgerCommitReadinessRequest(
    OperatorDecisionId OperatorDecisionId,
    PaperLedgerCommitDecisionType DecisionType,
    string ReviewedBy,
    PaperLedgerCommitReadinessReasonCategory ReasonCategory,
    string? CommentSanitized,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged);

public sealed record PaperLedgerCommitReadinessDecision(
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset ReviewedAtUtc,
    string ReviewedBy,
    PaperLedgerCommitDecisionType DecisionType,
    PaperLedgerCommitDecisionStatus DecisionStatus,
    PaperLedgerCommitReadinessReasonCategory ReasonCategory,
    string? CommentSanitized,
    PaperLedgerCommitReadinessStatus ResultingReadinessStatus,
    bool BlockedLinesAcknowledged,
    bool MissingStaleMarksAcknowledged,
    bool DriftAcknowledged,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool PositionPreviewLineagePreserved,
    bool LedgerPreviewLineagePreserved,
    bool SimulationResultLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool NoPaperLedgerCommit,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool CreatesOmsOrder,
    bool CreatesBrokerOrder,
    bool SubmitsOrders,
    bool CallsBrokerGateway,
    bool RequestsLiveMarketData,
    bool StartsApiOrWorker,
    bool StartsSchedulerOrBackgroundJob,
    bool GateAccepted,
    string GateMessage);

public sealed record PaperLedgerCommitReadinessResult(
    PaperLedgerCommitReadinessDecision Decision,
    PaperLedgerPreviewRecord ArchiveRecord,
    bool Persisted,
    bool AlreadyRecorded);

public interface IPaperLedgerPreviewArchiveRepository
{
    Task<PaperLedgerPreviewRecord?> GetByPreviewIdAsync(
        PaperPositionLedgerPreviewId previewId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperLedgerPreviewRecord record, CancellationToken cancellationToken);
}

public interface IPaperLedgerCommitReadinessDecisionRepository
{
    Task<PaperLedgerCommitReadinessDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperLedgerCommitReadinessDecision decision, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperLedgerPreviewArchiveRepository : IPaperLedgerPreviewArchiveRepository
{
    private readonly List<PaperLedgerPreviewRecord> records = [];

    public Task<PaperLedgerPreviewRecord?> GetByPreviewIdAsync(
        PaperPositionLedgerPreviewId previewId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.PaperPositionLedgerPreviewId == previewId));

    public Task AddAsync(PaperLedgerPreviewRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.PaperPositionLedgerPreviewId == record.PaperPositionLedgerPreviewId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPaperLedgerCommitReadinessDecisionRepository : IPaperLedgerCommitReadinessDecisionRepository
{
    private readonly List<PaperLedgerCommitReadinessDecision> decisions = [];

    public Task<PaperLedgerCommitReadinessDecision?> GetByDecisionIdAsync(
        OperatorDecisionId decisionId,
        CancellationToken cancellationToken)
        => Task.FromResult(decisions.FirstOrDefault(x => x.OperatorDecisionId == decisionId));

    public Task AddAsync(PaperLedgerCommitReadinessDecision decision, CancellationToken cancellationToken)
    {
        if (decisions.Any(x => x.OperatorDecisionId == decision.OperatorDecisionId))
        {
            return Task.CompletedTask;
        }

        decisions.Add(decision);
        return Task.CompletedTask;
    }
}

public sealed class PaperLedgerPreviewArchiveService(
    IPaperLedgerPreviewArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperLedgerPreviewArchiveResult> ArchiveAsync(
        PaperPositionLedgerPreview preview,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByPreviewIdAsync(preview.PaperPositionLedgerPreviewId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = PaperLedgerPreviewArchiveStatus.DuplicateReturned };
            return new PaperLedgerPreviewArchiveResult(
                duplicate,
                PaperLedgerPreviewReportRenderer.Render(duplicate),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(preview, clock.UtcNow);
        if (record.ArchiveStatus is not PaperLedgerPreviewArchiveStatus.RejectedInvalidPreview)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        return new PaperLedgerPreviewArchiveResult(
            record,
            PaperLedgerPreviewReportRenderer.Render(record),
            Persisted: record.ArchiveStatus is not PaperLedgerPreviewArchiveStatus.RejectedInvalidPreview,
            AlreadyArchived: false);
    }

    private static PaperLedgerPreviewRecord CreateRecord(
        PaperPositionLedgerPreview preview,
        DateTimeOffset createdAtUtc)
    {
        var valid = preview.PreviewStatus == PaperPositionLedgerPreviewStatus.PaperLedgerPreviewReady &&
            preview.PaperOnly &&
            preview.PreviewOnly &&
            preview.NoExternal &&
            preview.NoLivePositionMutation &&
            preview.NoBrokerPositionMutation &&
            preview.NoProductionLedgerMutation &&
            preview.NoTradingStateMutation &&
            preview.NoFillCreated &&
            preview.NoExecutionReportCreated &&
            preview.NoOrderCreated &&
            preview.NoBrokerRoute &&
            preview.NotSubmitted &&
            preview.Summary.LivePositionMutationCount == 0 &&
            preview.Summary.BrokerPositionMutationCount == 0 &&
            preview.Summary.ProductionLedgerMutationCount == 0 &&
            preview.Summary.TradingStateMutationCount == 0 &&
            !preview.LivePositionStateMutated &&
            !preview.BrokerPositionStateMutated &&
            !preview.ProductionLedgerStateMutated &&
            !preview.TradingStateMutated &&
            !preview.FillCreated &&
            !preview.ExecutionReportCreated &&
            !preview.OmsOrderCreated &&
            !preview.BrokerOrderCreated &&
            !preview.OrderStateCreated &&
            !preview.SubmittedOrders &&
            !preview.BrokerRouteCreated;
        var lines = preview.Lines
            .OrderBy(x => x.CurrencyOrSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PaperLedgerPreviewLineRecord(
                x.PaperPositionLedgerPreviewLineId,
                x.PaperPositionPreviewId,
                x.PaperSimulationResultId,
                x.CycleRunId,
                x.QubesRunId,
                x.OperatorDecisionId,
                x.InstrumentId,
                x.CurrencyOrSymbol,
                x.StartingPaperQuantity,
                x.SimulatedDeltaQuantity,
                x.PreviewEndingPaperQuantity,
                x.QuantityCurrency,
                x.LedgerPreviewStatus,
                x.SourceLineageReference,
                x.PaperOnly,
                x.PreviewOnly,
                NoPaperLedgerCommit: true,
                x.NoLivePositionMutation,
                x.NoBrokerPositionMutation,
                x.NoProductionLedgerMutation,
                x.NoTradingStateMutation,
                x.NoFillCreated,
                x.NoExecutionReportCreated,
                x.NoOrderCreated,
                x.NoBrokerRoute,
                x.NotSubmitted))
            .ToArray();

        return new PaperLedgerPreviewRecord(
            new PaperLedgerPreviewArchiveId($"{preview.PaperPositionLedgerPreviewId.Value}:archive"),
            preview.PaperPositionLedgerPreviewId,
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
            preview.BlockedLines.Count,
            "PaperLedgerPreviewOnly",
            valid ? PaperLedgerPreviewArchiveStatus.ArchivedNoExternal : PaperLedgerPreviewArchiveStatus.RejectedInvalidPreview,
            lines,
            preview.BlockedLines,
            preview.QubesLineagePreserved,
            preview.CycleLineagePreserved,
            preview.OperatorDecisionLineagePreserved,
            preview.PositionPreviewLineagePreserved,
            LedgerPreviewLineagePreserved: true,
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
            PreviewOnly: true,
            NoExternal: true,
            NoPaperLedgerCommit: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            LivePositionMutationCount: 0,
            BrokerPositionMutationCount: 0,
            ProductionLedgerMutationCount: 0,
            TradingStateMutationCount: 0,
            PaperLedgerStateCommitted: false,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            ProductionLedgerStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }
}

public static class PaperLedgerPreviewReportRenderer
{
    public static PaperLedgerPreviewOperatorReport Render(PaperLedgerPreviewRecord record)
        => new(
            record.PaperPositionLedgerPreviewId,
            record.CycleRunId,
            record.QubesRunId,
            record.OperatorDecisionId,
            record.ArchiveStatus,
            record.SafetyStatus,
            record.Lines,
            IncludesPaperLedgerPreviewOnlyDisclaimer: true,
            IncludesSimulatedOnlyDisclaimer: true,
            IncludesNoPaperLedgerCommitDisclaimer: true,
            IncludesNoLivePositionMutationDisclaimer: true,
            IncludesNoBrokerPositionMutationDisclaimer: true,
            IncludesNoProductionLedgerMutationDisclaimer: true,
            IncludesNoTradingStateMutationDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoBrokerRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            Markdown: RenderMarkdown(record));

    private static string RenderMarkdown(PaperLedgerPreviewRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Paper Ledger Preview Report");
        builder.AppendLine();
        builder.AppendLine($"- PaperPositionLedgerPreviewId: {record.PaperPositionLedgerPreviewId.Value}");
        builder.AppendLine($"- CycleRunId: {record.CycleRunId}");
        builder.AppendLine($"- QubesRunId: {record.QubesRunId}");
        builder.AppendLine($"- ArchiveStatus: {record.ArchiveStatus}");
        builder.AppendLine($"- SafetyStatus: {record.SafetyStatus}");
        builder.AppendLine();
        builder.AppendLine("Paper ledger preview only. Simulated-only. No paper ledger commit yet. no live position mutation, no broker position mutation, no production ledger mutation, no trading-state mutation, no fills, no execution reports, no orders, no broker routes, and no submissions.");
        builder.AppendLine();
        builder.AppendLine("## Preview Lines");
        foreach (var line in record.Lines)
        {
            builder.AppendLine($"- {line.CurrencyOrSymbol}: {line.StartingPaperQuantity.ToString(CultureInfo.InvariantCulture)} + {line.SimulatedDeltaQuantity.ToString(CultureInfo.InvariantCulture)} = {line.PreviewEndingPaperQuantity.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency}");
        }

        return builder.ToString();
    }
}

public sealed class PaperLedgerCommitReadinessService(
    IPaperLedgerCommitReadinessDecisionRepository repository,
    IClock clock)
{
    public async Task<PaperLedgerCommitReadinessResult> ReviewAsync(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByDecisionIdAsync(request.OperatorDecisionId, cancellationToken);
        if (existing is not null)
        {
            return new PaperLedgerCommitReadinessResult(
                existing with { DecisionStatus = PaperLedgerCommitDecisionStatus.DuplicateReturned },
                archive,
                Persisted: false,
                AlreadyRecorded: true);
        }

        var decision = CreateDecision(archive, request, clock.UtcNow);
        if (decision.GateAccepted)
        {
            await repository.AddAsync(decision, cancellationToken);
        }

        return new PaperLedgerCommitReadinessResult(
            decision,
            archive,
            Persisted: decision.GateAccepted,
            AlreadyRecorded: false);
    }

    private static PaperLedgerCommitReadinessDecision CreateDecision(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessRequest request,
        DateTimeOffset reviewedAtUtc)
    {
        var gate = EvaluateGate(archive, request);
        return new PaperLedgerCommitReadinessDecision(
            archive.PaperPositionLedgerPreviewId,
            archive.PaperPositionPreviewId,
            archive.PaperSimulationResultId,
            archive.PaperSimulationPlanId,
            archive.PaperExecutionPlanId,
            archive.CycleRunId,
            archive.QubesRunId,
            request.OperatorDecisionId,
            reviewedAtUtc,
            SanitizeReviewedBy(request.ReviewedBy),
            request.DecisionType,
            gate.Accepted ? PaperLedgerCommitDecisionStatus.Recorded : PaperLedgerCommitDecisionStatus.RejectedByGate,
            request.ReasonCategory,
            string.IsNullOrWhiteSpace(request.CommentSanitized) ? null : SanitizeComment(request.CommentSanitized),
            gate.ReadinessStatus,
            request.BlockedLinesAcknowledged,
            request.MissingStaleMarksAcknowledged,
            request.DriftAcknowledged,
            archive.QubesLineagePreserved,
            archive.CycleLineagePreserved,
            archive.OperatorDecisionLineagePreserved,
            archive.PositionPreviewLineagePreserved,
            archive.LedgerPreviewLineagePreserved,
            archive.SimulationResultLineagePreserved,
            archive.SimulationPlanLineagePreserved,
            archive.PaperExecutionPlanLineagePreserved,
            archive.PaperCandidateLineagePreserved,
            archive.RiskLineagePreserved,
            archive.RebalanceIntentLineagePreserved,
            archive.LotSizingLineagePreserved,
            NoPaperLedgerCommit: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            CreatesFill: false,
            CreatesExecutionReport: false,
            CreatesOmsOrder: false,
            CreatesBrokerOrder: false,
            SubmitsOrders: false,
            CallsBrokerGateway: false,
            RequestsLiveMarketData: false,
            StartsApiOrWorker: false,
            StartsSchedulerOrBackgroundJob: false,
            gate.Accepted,
            gate.Message);
    }

    private static (bool Accepted, PaperLedgerCommitReadinessStatus ReadinessStatus, string Message) EvaluateGate(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessRequest request)
    {
        if (archive.ArchiveStatus is PaperLedgerPreviewArchiveStatus.RejectedInvalidPreview)
        {
            return (false, PaperLedgerCommitReadinessStatus.Rejected, "Invalid paper ledger preview archive rejected.");
        }

        if (!archive.NoPaperLedgerCommit ||
            archive.PaperLedgerStateCommitted)
        {
            return (false, PaperLedgerCommitReadinessStatus.Rejected, "Paper ledger state was already committed.");
        }

        if (archive.SafetyStatus != "PaperLedgerPreviewOnly" ||
            !archive.PaperOnly ||
            !archive.SimulatedOnly ||
            !archive.PreviewOnly ||
            !archive.NoExternal ||
            !archive.NoLivePositionMutation ||
            !archive.NoBrokerPositionMutation ||
            !archive.NoProductionLedgerMutation ||
            !archive.NoTradingStateMutation ||
            !archive.NoFillCreated ||
            !archive.NoExecutionReportCreated ||
            !archive.NoOrderCreated ||
            !archive.NoBrokerRoute ||
            !archive.NotSubmitted)
        {
            return (false, PaperLedgerCommitReadinessStatus.InconclusiveSafe, "Archive is not paper-ledger-preview-only.");
        }

        if (archive.LivePositionMutationCount != 0 ||
            archive.BrokerPositionMutationCount != 0 ||
            archive.ProductionLedgerMutationCount != 0 ||
            archive.TradingStateMutationCount != 0 ||
            archive.LivePositionStateMutated ||
            archive.BrokerPositionStateMutated ||
            archive.ProductionLedgerStateMutated ||
            archive.TradingStateMutated)
        {
            return (false, PaperLedgerCommitReadinessStatus.Rejected, "Mutation risk detected.");
        }

        if (archive.FillCreated ||
            archive.ExecutionReportCreated ||
            archive.OmsOrderCreated ||
            archive.BrokerOrderCreated ||
            archive.OrderStateCreated ||
            archive.SubmittedOrders ||
            archive.BrokerRouteCreated)
        {
            return (false, PaperLedgerCommitReadinessStatus.Rejected, "Order, fill, route, or submission risk detected.");
        }

        if (request.DecisionType == PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness &&
            archive.BlockedLineCount > 0 &&
            !request.BlockedLinesAcknowledged)
        {
            return (false, PaperLedgerCommitReadinessStatus.HeldForBlockedLines, "Blocked lines must be acknowledged.");
        }

        if (request.DecisionType == PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness &&
            archive.MissingStaleMarkWarningsPreserved &&
            !request.MissingStaleMarksAcknowledged)
        {
            return (false, PaperLedgerCommitReadinessStatus.HeldForMissingMarks, "Missing/stale marks must be acknowledged.");
        }

        if (request.DecisionType == PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness &&
            archive.DriftAcknowledgementPreserved &&
            !request.DriftAcknowledged)
        {
            return (false, PaperLedgerCommitReadinessStatus.HeldForDrift, "Drift must be acknowledged.");
        }

        return request.DecisionType switch
        {
            PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness => (
                true,
                PaperLedgerCommitReadinessStatus.PaperLedgerCommitReadyNoExternal,
                "Paper ledger commit readiness recorded; no paper ledger state was committed."),
            PaperLedgerCommitDecisionType.Hold => (
                true,
                PaperLedgerCommitReadinessStatus.HeldForBlockedLines,
                "Paper ledger preview held no-externally."),
            PaperLedgerCommitDecisionType.Reject => (
                true,
                PaperLedgerCommitReadinessStatus.Rejected,
                "Paper ledger preview rejected no-externally."),
            PaperLedgerCommitDecisionType.RequestLedgerFix => (
                true,
                PaperLedgerCommitReadinessStatus.InconclusiveSafe,
                "Ledger fix requested no-externally."),
            PaperLedgerCommitDecisionType.RequestRiskReview => (
                true,
                PaperLedgerCommitReadinessStatus.HeldForRiskReview,
                "Risk review requested no-externally."),
            _ => (
                false,
                PaperLedgerCommitReadinessStatus.InconclusiveSafe,
                "Unknown decision rejected safely.")
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
