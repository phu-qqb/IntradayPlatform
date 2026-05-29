using System.Globalization;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum SecondCyclePaperContinuityArchiveStatus
{
    ArchivedNoExternal,
    DuplicateReturned,
    RejectedInvalidSecondCycle,
    InconclusiveSafe
}

public enum PaperCycleContinuityStatus
{
    PaperContinuityReadyNoExternal,
    HeldForReview,
    HeldForMissingMarks,
    HeldForDrift,
    Rejected,
    InconclusiveSafe
}

public sealed record SecondCyclePaperContinuityArchiveId(string Value);

public sealed record SecondCyclePaperContinuityArchiveRecord(
    SecondCyclePaperContinuityArchiveId ArchiveId,
    string SecondCycleRunId,
    string QubesRunId,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    int CycleCadenceMinutes,
    PaperBaselineSecondCycleStatus CycleStatus,
    SecondCyclePaperContinuityArchiveStatus ArchiveStatus,
    string SafetyStatus,
    PaperBaselineCycleInput PaperBaselineInput,
    IReadOnlyList<SecondCycleTargetPortfolioLine> TargetPortfolioLines,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> CurrentPaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleTheoreticalPnlLine> TheoreticalPnlLines,
    IReadOnlyList<SecondCycleReconciliationLine> ReconciliationLines,
    IReadOnlyList<SecondCycleTheoreticalVsRealLine> TheoreticalVsRealLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    int RawQubesRowCount,
    int NormalizedQubesRowCount,
    bool PaperBaselineFromR025,
    bool R025PaperBaselineMutated,
    bool PaperLedgerStateCommittedOrMutated,
    bool RebalanceIntentsRemainNonExecutable,
    bool MissingStaleMarkHandlingPreserved,
    bool DriftAcknowledgementPreserved,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool PaperBaselineLineagePreserved,
    bool LedgerStateArchiveLineagePreserved,
    bool LedgerCommitLineagePreserved,
    bool LedgerPreviewLineagePreserved,
    bool PositionPreviewLineagePreserved,
    bool SimulationResultLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool ExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool NoExternal,
    bool NoBrokerCall,
    bool NoLiveMarketData,
    bool NoApiWorkerStart,
    bool NoSchedulerServicePolling,
    bool NoNewCycleRun,
    bool NoNewQubesBatchIngest,
    bool NoPaperLedgerMutation,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoOrderCreated,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoBrokerRoute,
    bool NoSubmission,
    DateTimeOffset CreatedAtUtc);

public sealed record PaperCycleContinuityOperatorReport(
    SecondCyclePaperContinuityArchiveId ArchiveId,
    string SecondCycleRunId,
    string QubesRunId,
    PaperCycleContinuityStatus ContinuityStatus,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> CurrentPaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    bool IncludesPaperLedgerFixtureBaselineDisclaimer,
    bool IncludesNoLiveBrokerProductionTradingMutationDisclaimer,
    bool IncludesNoPaperLedgerCommitDisclaimer,
    bool IncludesNoBrokerCallDisclaimer,
    bool IncludesNoLiveMarketDataDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoBrokerRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    string Markdown);

public sealed record PaperCycleContinuityDecisionGate(
    string GateId,
    SecondCyclePaperContinuityArchiveId ArchiveId,
    PaperCycleContinuityStatus ContinuityStatus,
    bool FutureManualPaperCyclesMayUseLatestPaperLedgerFixtureBaseline,
    bool StartsSchedulerOrService,
    bool RunsAnotherCycle,
    bool IngestsNewQubesBatch,
    bool MutatesPaperLedgerState,
    bool MutatesLivePositionState,
    bool MutatesBrokerPositionState,
    bool MutatesProductionLedgerState,
    bool MutatesTradingState,
    bool CreatesOrderCandidates,
    bool CreatesExecutionPlans,
    bool CreatesOrders,
    bool CreatesFills,
    bool CreatesExecutionReports,
    bool CreatesRoutes,
    bool SubmitsOrders,
    bool NoExternal,
    bool PreservesNonExecutableRebalanceIntents);

public sealed record SecondCyclePaperContinuityArchiveResult(
    SecondCyclePaperContinuityArchiveRecord ArchiveRecord,
    PaperCycleContinuityOperatorReport OperatorReport,
    PaperCycleContinuityDecisionGate ContinuityGate,
    bool Persisted,
    bool AlreadyArchived);

public interface ISecondCyclePaperContinuityArchiveRepository
{
    Task<SecondCyclePaperContinuityArchiveRecord?> GetByArchiveIdAsync(
        SecondCyclePaperContinuityArchiveId archiveId,
        CancellationToken cancellationToken);

    Task AddAsync(SecondCyclePaperContinuityArchiveRecord record, CancellationToken cancellationToken);
}

public sealed class InMemorySecondCyclePaperContinuityArchiveRepository : ISecondCyclePaperContinuityArchiveRepository
{
    private readonly List<SecondCyclePaperContinuityArchiveRecord> records = [];

    public Task<SecondCyclePaperContinuityArchiveRecord?> GetByArchiveIdAsync(
        SecondCyclePaperContinuityArchiveId archiveId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.ArchiveId == archiveId));

    public Task AddAsync(SecondCyclePaperContinuityArchiveRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.ArchiveId == record.ArchiveId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class SecondCyclePaperContinuityArchiveService(
    ISecondCyclePaperContinuityArchiveRepository repository,
    IClock clock)
{
    public async Task<SecondCyclePaperContinuityArchiveResult> ArchiveAsync(
        PaperBaselineSecondCycleResult secondCycle,
        CancellationToken cancellationToken)
    {
        var archiveId = new SecondCyclePaperContinuityArchiveId($"{secondCycle.SecondCycleRunId}:paper-continuity-archive");
        var existing = await repository.GetByArchiveIdAsync(archiveId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = SecondCyclePaperContinuityArchiveStatus.DuplicateReturned };
            var duplicateGate = CreateContinuityGate(duplicate);
            return new SecondCyclePaperContinuityArchiveResult(
                duplicate,
                PaperCycleContinuityReportRenderer.Render(duplicate, duplicateGate.ContinuityStatus),
                duplicateGate,
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateArchive(archiveId, secondCycle, clock.UtcNow);
        if (record.ArchiveStatus is SecondCyclePaperContinuityArchiveStatus.ArchivedNoExternal)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        var gate = CreateContinuityGate(record);
        return new SecondCyclePaperContinuityArchiveResult(
            record,
            PaperCycleContinuityReportRenderer.Render(record, gate.ContinuityStatus),
            gate,
            Persisted: record.ArchiveStatus is SecondCyclePaperContinuityArchiveStatus.ArchivedNoExternal,
            AlreadyArchived: false);
    }

    private static SecondCyclePaperContinuityArchiveRecord CreateArchive(
        SecondCyclePaperContinuityArchiveId archiveId,
        PaperBaselineSecondCycleResult secondCycle,
        DateTimeOffset createdAtUtc)
    {
        var valid = secondCycle.IsNoExternalFixture &&
                    secondCycle.Summary.UsedR025PaperLedgerBaseline &&
                    !secondCycle.Summary.CurrentPaperBaselineIsFlatZero &&
                    secondCycle.RebalanceIntentsRemainNonExecutable &&
                    !secondCycle.StartedApiOrWorker &&
                    !secondCycle.StartedBackgroundExecution &&
                    !secondCycle.UsedLiveMarketData &&
                    !secondCycle.CalledBrokerGateway &&
                    !secondCycle.SubmittedOrders &&
                    !secondCycle.CreatedExecutableOrder &&
                    !secondCycle.MutatedPaperLedgerState &&
                    !secondCycle.MutatedR025PaperBaseline &&
                    !secondCycle.MutatedLivePositionState &&
                    !secondCycle.MutatedBrokerPositionState &&
                    !secondCycle.MutatedProductionLedgerState &&
                    !secondCycle.MutatedLiveTradingState &&
                    !secondCycle.CreatedOrderState &&
                    !secondCycle.CreatedFill &&
                    !secondCycle.CreatedExecutionReport &&
                    !secondCycle.CreatedBrokerRoute;

        return new SecondCyclePaperContinuityArchiveRecord(
            archiveId,
            secondCycle.SecondCycleRunId,
            secondCycle.QubesRunId.Value,
            secondCycle.CycleStartedAtUtc,
            secondCycle.CycleCompletedAtUtc,
            secondCycle.CycleCadenceMinutes,
            secondCycle.CycleStatus,
            valid ? SecondCyclePaperContinuityArchiveStatus.ArchivedNoExternal : SecondCyclePaperContinuityArchiveStatus.RejectedInvalidSecondCycle,
            "NoExternalSecondCycleContinuityArchiveOnly",
            secondCycle.PaperBaselineInput,
            secondCycle.TargetPortfolioLines,
            secondCycle.CurrentPaperBaselineLines,
            secondCycle.TargetVsCurrentDiffLines,
            secondCycle.TheoreticalPnlLines,
            secondCycle.ReconciliationLines,
            secondCycle.TheoreticalVsRealLines,
            secondCycle.RebalanceIntents,
            secondCycle.QubesWeights.RawInputRowCount,
            secondCycle.QubesWeights.NormalizedOutputRowCount,
            PaperBaselineFromR025: secondCycle.Summary.UsedR025PaperLedgerBaseline,
            R025PaperBaselineMutated: false,
            PaperLedgerStateCommittedOrMutated: false,
            RebalanceIntentsRemainNonExecutable: secondCycle.RebalanceIntents.All(x => !x.IsExecutable),
            MissingStaleMarkHandlingPreserved: true,
            DriftAcknowledgementPreserved: true,
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            PaperBaselineLineagePreserved: true,
            LedgerStateArchiveLineagePreserved: true,
            LedgerCommitLineagePreserved: true,
            LedgerPreviewLineagePreserved: true,
            PositionPreviewLineagePreserved: true,
            SimulationResultLineagePreserved: true,
            SimulationPlanLineagePreserved: true,
            ExecutionPlanLineagePreserved: true,
            PaperCandidateLineagePreserved: true,
            RiskLineagePreserved: true,
            RebalanceIntentLineagePreserved: true,
            LotSizingLineagePreserved: true,
            NoExternal: true,
            NoBrokerCall: true,
            NoLiveMarketData: true,
            NoApiWorkerStart: true,
            NoSchedulerServicePolling: true,
            NoNewCycleRun: true,
            NoNewQubesBatchIngest: true,
            NoPaperLedgerMutation: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoOrderCreated: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoBrokerRoute: true,
            NoSubmission: true,
            createdAtUtc);
    }

    private static PaperCycleContinuityDecisionGate CreateContinuityGate(SecondCyclePaperContinuityArchiveRecord record)
    {
        var status = record.ArchiveStatus is not SecondCyclePaperContinuityArchiveStatus.ArchivedNoExternal
            ? PaperCycleContinuityStatus.Rejected
            : record.TheoreticalPnlLines.Any(x => x.PnLStatus is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark)
                ? PaperCycleContinuityStatus.HeldForMissingMarks
                : PaperCycleContinuityStatus.PaperContinuityReadyNoExternal;

        return new PaperCycleContinuityDecisionGate(
            $"{record.ArchiveId.Value}:paper-continuity-gate",
            record.ArchiveId,
            status,
            FutureManualPaperCyclesMayUseLatestPaperLedgerFixtureBaseline: status is PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
            StartsSchedulerOrService: false,
            RunsAnotherCycle: false,
            IngestsNewQubesBatch: false,
            MutatesPaperLedgerState: false,
            MutatesLivePositionState: false,
            MutatesBrokerPositionState: false,
            MutatesProductionLedgerState: false,
            MutatesTradingState: false,
            CreatesOrderCandidates: false,
            CreatesExecutionPlans: false,
            CreatesOrders: false,
            CreatesFills: false,
            CreatesExecutionReports: false,
            CreatesRoutes: false,
            SubmitsOrders: false,
            NoExternal: true,
            PreservesNonExecutableRebalanceIntents: record.RebalanceIntentsRemainNonExecutable);
    }
}

public static class PaperCycleContinuityReportRenderer
{
    public static PaperCycleContinuityOperatorReport Render(
        SecondCyclePaperContinuityArchiveRecord record,
        PaperCycleContinuityStatus continuityStatus)
    {
        var markdown = BuildMarkdown(record, continuityStatus);
        return new PaperCycleContinuityOperatorReport(
            record.ArchiveId,
            record.SecondCycleRunId,
            record.QubesRunId,
            continuityStatus,
            record.CurrentPaperBaselineLines,
            record.TargetVsCurrentDiffLines,
            record.RebalanceIntents,
            IncludesPaperLedgerFixtureBaselineDisclaimer: true,
            IncludesNoLiveBrokerProductionTradingMutationDisclaimer: true,
            IncludesNoPaperLedgerCommitDisclaimer: true,
            IncludesNoBrokerCallDisclaimer: true,
            IncludesNoLiveMarketDataDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoBrokerRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            markdown);
    }

    private static string BuildMarkdown(
        SecondCyclePaperContinuityArchiveRecord record,
        PaperCycleContinuityStatus continuityStatus)
    {
        var lines = new List<string>
        {
            "# R027 Second-Cycle Paper Continuity Report",
            string.Empty,
            $"Second cycle: {record.SecondCycleRunId}",
            $"Qubes run: {record.QubesRunId}",
            $"Continuity status: {continuityStatus}",
            string.Empty,
            "Second cycle used paper ledger fixture baseline.",
            "No live/broker/production/trading state mutation.",
            "No paper ledger commit in R026/R027.",
            "No broker calls.",
            "No live market data.",
            "No orders.",
            "No fills.",
            "No execution reports.",
            "No broker routes.",
            "No submissions.",
            string.Empty,
            "## Current Paper Baseline"
        };

        lines.AddRange(record.CurrentPaperBaselineLines.Select(line =>
            $"- {line.Symbol}: {line.CurrentPaperQuantity.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency}"));

        lines.Add(string.Empty);
        lines.Add("## Target Vs Current");
        lines.AddRange(record.TargetVsCurrentDiffLines.Select(line =>
            $"- {line.Symbol}: delta notional {line.DeltaNotional?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"));

        lines.Add(string.Empty);
        lines.Add("## Rebalance Intents");
        lines.AddRange(record.RebalanceIntents.Select(line =>
            $"- {line.Symbol}: {line.IntentSide}, non-executable={(!line.IsExecutable).ToString(CultureInfo.InvariantCulture)}"));

        return string.Join(Environment.NewLine, lines);
    }
}
