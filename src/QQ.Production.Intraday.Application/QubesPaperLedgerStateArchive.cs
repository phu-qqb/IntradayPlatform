using System.Globalization;
using System.Text;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperLedgerStateArchiveStatus
{
    ArchivedNoExternal,
    ArchivedPaperFixtureState,
    DuplicateReturned,
    RejectedInvalidState,
    InconclusiveSafe
}

public sealed record PaperLedgerStateArchiveId(string Value);

public sealed record PaperLedgerStateLineArchiveRecord(
    PaperLedgerStateLineId PaperLedgerStateLineId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    InstrumentId InstrumentId,
    string CurrencyOrSymbol,
    string QuantityCurrency,
    decimal EndingPaperQuantity,
    string SafetyStatus,
    string SourceLineageReference,
    bool PaperOnly,
    bool NoExternal,
    bool FixtureState,
    bool NotProductionLedger,
    bool NotBrokerPosition,
    bool NotTradingState,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted);

public sealed record PaperLedgerStateArchiveRecord(
    PaperLedgerStateArchiveId PaperLedgerStateArchiveId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset CreatedAtUtc,
    PaperLedgerCommitStatus StateStatus,
    string SafetyStatus,
    int LineCount,
    int BlockedLineCount,
    PaperLedgerStateArchiveStatus ArchiveStatus,
    IReadOnlyList<PaperLedgerStateLineArchiveRecord> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool LedgerCommitLineagePreserved,
    bool LedgerPreviewLineagePreserved,
    bool PositionPreviewLineagePreserved,
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
    bool NoExternal,
    bool FixtureState,
    bool NotProductionLedger,
    bool NotBrokerPosition,
    bool NotTradingState,
    bool NoProductionLedgerMutation,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted,
    bool PaperLedgerMutatedAgain,
    bool NewCycleRan,
    bool NewQubesBatchIngested,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool ProductionLedgerStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool ParentOrderCreated,
    bool ChildOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool BrokerRouteCreated);

public sealed record PaperLedgerStateOperatorReport(
    PaperLedgerStateArchiveId PaperLedgerStateArchiveId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    string CycleRunId,
    string QubesRunId,
    PaperLedgerStateArchiveStatus ArchiveStatus,
    string SafetyStatus,
    IReadOnlyList<PaperLedgerStateLineArchiveRecord> Lines,
    bool IncludesPaperFixtureStateDisclaimer,
    bool IncludesNoProductionLedgerMutationDisclaimer,
    bool IncludesNoLivePositionMutationDisclaimer,
    bool IncludesNoBrokerPositionMutationDisclaimer,
    bool IncludesNoTradingStateMutationDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoBrokerRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    string Markdown);

public sealed record PaperNextCycleBaselineReference(
    string NextCycleBaselineReferenceId,
    PaperLedgerStateArchiveId PaperLedgerStateArchiveId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    string CycleRunId,
    string QubesRunId,
    string NextCycleBaselineType,
    string BaselineSource,
    bool BaselineIsProduction,
    bool BaselineIsBroker,
    bool BaselineIsLiveTrading,
    bool PaperOnly,
    bool NoExternal,
    bool FixtureState,
    bool NewCycleRan,
    bool NewQubesBatchIngested,
    bool PaperLedgerMutatedAgain);

public sealed record PaperNextCycleContinuityGate(
    string ContinuityGateId,
    PaperLedgerStateArchiveId PaperLedgerStateArchiveId,
    string NextCycleBaselineReferenceId,
    string GateStatus,
    bool NextNoExternalCycleMayUsePaperLedgerFixtureAsCurrentState,
    bool NewCycleRan,
    bool NewQubesBatchIngested,
    bool PaperLedgerMutatedAgain,
    bool NoExternal,
    bool NoBrokerCall,
    bool NoLiveMarketData,
    bool NoSchedulerOrService,
    bool NoOrderFillReportRouteOrSubmission);

public sealed record PaperLedgerStateArchiveResult(
    PaperLedgerStateArchiveRecord ArchiveRecord,
    PaperLedgerStateOperatorReport OperatorReport,
    PaperNextCycleBaselineReference NextCycleBaselineReference,
    PaperNextCycleContinuityGate ContinuityGate,
    bool Persisted,
    bool AlreadyArchived);

public interface IPaperLedgerStateArchiveRepository
{
    Task<PaperLedgerStateArchiveRecord?> GetByArchiveIdAsync(
        PaperLedgerStateArchiveId archiveId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperLedgerStateArchiveRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperLedgerStateArchiveRepository : IPaperLedgerStateArchiveRepository
{
    private readonly List<PaperLedgerStateArchiveRecord> archives = [];

    public Task<PaperLedgerStateArchiveRecord?> GetByArchiveIdAsync(
        PaperLedgerStateArchiveId archiveId,
        CancellationToken cancellationToken)
        => Task.FromResult(archives.FirstOrDefault(x => x.PaperLedgerStateArchiveId == archiveId));

    public Task AddAsync(PaperLedgerStateArchiveRecord record, CancellationToken cancellationToken)
    {
        if (archives.Any(x => x.PaperLedgerStateArchiveId == record.PaperLedgerStateArchiveId))
        {
            return Task.CompletedTask;
        }

        archives.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class PaperLedgerStateArchiveService(
    IPaperLedgerStateArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperLedgerStateArchiveResult> ArchiveAsync(
        PaperLedgerState state,
        CancellationToken cancellationToken)
    {
        var archiveId = new PaperLedgerStateArchiveId($"{state.PaperLedgerStateId.Value}:archive");
        var existing = await repository.GetByArchiveIdAsync(archiveId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = PaperLedgerStateArchiveStatus.DuplicateReturned };
            return new PaperLedgerStateArchiveResult(
                duplicate,
                PaperLedgerStateReportRenderer.Render(duplicate),
                CreateBaselineReference(duplicate),
                CreateContinuityGate(duplicate),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateArchive(archiveId, state, clock.UtcNow);
        if (record.ArchiveStatus is PaperLedgerStateArchiveStatus.ArchivedNoExternal or PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        return new PaperLedgerStateArchiveResult(
            record,
            PaperLedgerStateReportRenderer.Render(record),
            CreateBaselineReference(record),
            CreateContinuityGate(record),
            Persisted: record.ArchiveStatus is PaperLedgerStateArchiveStatus.ArchivedNoExternal or PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState,
            AlreadyArchived: false);
    }

    private static PaperLedgerStateArchiveRecord CreateArchive(
        PaperLedgerStateArchiveId archiveId,
        PaperLedgerState state,
        DateTimeOffset createdAtUtc)
    {
        var status = IsValidState(state)
            ? PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState
            : PaperLedgerStateArchiveStatus.RejectedInvalidState;
        var lines = status is PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState
            ? state.Lines.Select(CreateLine).ToArray()
            : [];

        return new PaperLedgerStateArchiveRecord(
            archiveId,
            state.PaperLedgerStateId,
            state.PaperLedgerCommitId,
            state.PaperPositionLedgerPreviewId,
            state.PaperPositionPreviewId,
            state.PaperSimulationResultId,
            state.PaperSimulationPlanId,
            state.PaperExecutionPlanId,
            state.CycleRunId,
            state.QubesRunId,
            state.OperatorDecisionId,
            createdAtUtc,
            state.CommitStatus,
            state.SafetyStatus,
            lines.Length,
            state.BlockedLines.Count,
            status,
            lines,
            state.BlockedLines,
            state.QubesLineagePreserved,
            state.CycleLineagePreserved,
            state.OperatorDecisionLineagePreserved,
            LedgerCommitLineagePreserved: true,
            state.LedgerPreviewLineagePreserved,
            state.PositionPreviewLineagePreserved,
            state.SimulationResultLineagePreserved,
            state.SimulationPlanLineagePreserved,
            state.PaperExecutionPlanLineagePreserved,
            state.PaperCandidateLineagePreserved,
            state.RiskLineagePreserved,
            state.RebalanceIntentLineagePreserved,
            state.LotSizingLineagePreserved,
            state.MissingStaleMarkWarningsPreserved,
            state.DriftAcknowledgementPreserved,
            state.PaperOnly,
            state.NoExternal,
            state.FixtureState,
            state.NotProductionLedger,
            state.NotBrokerPosition,
            state.NotTradingState,
            state.NoProductionLedgerMutation,
            state.NoLivePositionMutation,
            state.NoBrokerPositionMutation,
            state.NoTradingStateMutation,
            state.NoFillCreated,
            state.NoExecutionReportCreated,
            state.NoOrderCreated,
            state.NoBrokerRoute,
            state.NotSubmitted,
            PaperLedgerMutatedAgain: false,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            state.LivePositionStateMutated,
            state.BrokerPositionStateMutated,
            state.ProductionLedgerStateMutated,
            state.TradingStateMutated,
            state.FillCreated,
            state.ExecutionReportCreated,
            state.OmsOrderCreated,
            state.ParentOrderCreated,
            state.ChildOrderCreated,
            state.BrokerOrderCreated,
            state.OrderStateCreated,
            state.SubmittedOrders,
            state.BrokerRouteCreated);
    }

    private static bool IsValidState(PaperLedgerState state)
        => state.CommitStatus is PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal &&
           state.SafetyStatus == "PaperLedgerFixtureStateOnly" &&
           state.PaperOnly &&
           state.NoExternal &&
           state.FixtureState &&
           state.NotProductionLedger &&
           state.NotBrokerPosition &&
           state.NotTradingState &&
           state.NoProductionLedgerMutation &&
           state.NoLivePositionMutation &&
           state.NoBrokerPositionMutation &&
           state.NoTradingStateMutation &&
           state.NoFillCreated &&
           state.NoExecutionReportCreated &&
           state.NoOrderCreated &&
           state.NoBrokerRoute &&
           state.NotSubmitted &&
           !state.LivePositionStateMutated &&
           !state.BrokerPositionStateMutated &&
           !state.ProductionLedgerStateMutated &&
           !state.TradingStateMutated &&
           !state.FillCreated &&
           !state.ExecutionReportCreated &&
           !state.OmsOrderCreated &&
           !state.ParentOrderCreated &&
           !state.ChildOrderCreated &&
           !state.BrokerOrderCreated &&
           !state.OrderStateCreated &&
           !state.SubmittedOrders &&
           !state.BrokerRouteCreated;

    private static PaperLedgerStateLineArchiveRecord CreateLine(PaperLedgerStateLine line)
        => new(
            line.PaperLedgerStateLineId,
            line.PaperLedgerStateId,
            line.PaperLedgerCommitId,
            line.PaperPositionLedgerPreviewId,
            line.CycleRunId,
            line.QubesRunId,
            line.OperatorDecisionId,
            line.InstrumentId,
            line.CurrencyOrSymbol,
            line.QuantityCurrency,
            line.EndingPaperQuantity,
            line.SafetyStatus,
            line.SourceLineageReference,
            line.PaperOnly,
            line.NoExternal,
            line.FixtureState,
            line.NotProductionLedger,
            line.NotBrokerPosition,
            line.NotTradingState,
            line.NoLivePositionMutation,
            line.NoBrokerPositionMutation,
            line.NoProductionLedgerMutation,
            line.NoTradingStateMutation,
            line.NoFillCreated,
            line.NoExecutionReportCreated,
            line.NoOrderCreated,
            line.NoBrokerRoute,
            line.NotSubmitted);

    private static PaperNextCycleBaselineReference CreateBaselineReference(PaperLedgerStateArchiveRecord record)
        => new(
            $"{record.PaperLedgerStateArchiveId.Value}:next-cycle-baseline",
            record.PaperLedgerStateArchiveId,
            record.PaperLedgerStateId,
            record.PaperLedgerCommitId,
            record.CycleRunId,
            record.QubesRunId,
            "PaperLedgerFixture",
            "R024 committed paper ledger state",
            BaselineIsProduction: false,
            BaselineIsBroker: false,
            BaselineIsLiveTrading: false,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            PaperLedgerMutatedAgain: false);

    private static PaperNextCycleContinuityGate CreateContinuityGate(PaperLedgerStateArchiveRecord record)
        => new(
            $"{record.PaperLedgerStateArchiveId.Value}:next-cycle-continuity-gate",
            record.PaperLedgerStateArchiveId,
            $"{record.PaperLedgerStateArchiveId.Value}:next-cycle-baseline",
            "NextCycleContinuityReadyNoExternal",
            NextNoExternalCycleMayUsePaperLedgerFixtureAsCurrentState: true,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            PaperLedgerMutatedAgain: false,
            NoExternal: true,
            NoBrokerCall: true,
            NoLiveMarketData: true,
            NoSchedulerOrService: true,
            NoOrderFillReportRouteOrSubmission: true);
}

public static class PaperLedgerStateReportRenderer
{
    public static PaperLedgerStateOperatorReport Render(PaperLedgerStateArchiveRecord record)
        => new(
            record.PaperLedgerStateArchiveId,
            record.PaperLedgerStateId,
            record.PaperLedgerCommitId,
            record.CycleRunId,
            record.QubesRunId,
            record.ArchiveStatus,
            record.SafetyStatus,
            record.Lines,
            IncludesPaperFixtureStateDisclaimer: true,
            IncludesNoProductionLedgerMutationDisclaimer: true,
            IncludesNoLivePositionMutationDisclaimer: true,
            IncludesNoBrokerPositionMutationDisclaimer: true,
            IncludesNoTradingStateMutationDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoBrokerRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            Markdown: RenderMarkdown(record));

    private static string RenderMarkdown(PaperLedgerStateArchiveRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Paper Ledger State Report");
        builder.AppendLine();
        builder.AppendLine($"- PaperLedgerStateArchiveId: {record.PaperLedgerStateArchiveId.Value}");
        builder.AppendLine($"- PaperLedgerStateId: {record.PaperLedgerStateId.Value}");
        builder.AppendLine($"- PaperLedgerCommitId: {record.PaperLedgerCommitId.Value}");
        builder.AppendLine($"- CycleRunId: {record.CycleRunId}");
        builder.AppendLine($"- QubesRunId: {record.QubesRunId}");
        builder.AppendLine($"- ArchiveStatus: {record.ArchiveStatus}");
        builder.AppendLine($"- SafetyStatus: {record.SafetyStatus}");
        builder.AppendLine();
        builder.AppendLine("Paper ledger fixture state only. no production ledger mutation, no live position mutation, no broker position mutation, no trading-state mutation, no orders, no fills, no execution reports, no broker routes, and no submissions.");
        builder.AppendLine();
        builder.AppendLine("## State Lines");
        foreach (var line in record.Lines)
        {
            builder.AppendLine($"- {line.CurrencyOrSymbol}: {line.EndingPaperQuantity.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency}");
        }

        return builder.ToString();
    }
}
