using System.Globalization;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum ManualPaperCycleArchiveStatus
{
    ArchivedNoExternal,
    DuplicateReturned,
    RejectedInvalidManualCycle,
    InconclusiveSafe
}

public enum ManualRollingReadinessStatus
{
    ManualRollingReadyNoExternal,
    HeldForReview,
    HeldForMissingBaseline,
    HeldForPreflightFailure,
    HeldForMissingMarks,
    HeldForDrift,
    InconclusiveSafe
}

public sealed record ManualPaperCycleRunArchiveId(string Value);

public sealed record ManualPaperCycleResultArchiveRecord(
    ManualPaperCycleRunArchiveId ArchiveId,
    string RequestedCycleRunId,
    string QubesRunId,
    PaperCycleRunMode RunMode,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    PaperCyclePreflightStatus PreflightStatus,
    ManualPaperCycleRunStatus CycleStatus,
    ManualPaperCycleArchiveStatus ArchiveStatus,
    string SafetyStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string BaselineReference,
    int RawRowCount,
    int NormalizedRowCount,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> PaperBaselineLines,
    IReadOnlyList<SecondCycleTargetPortfolioLine> TargetPortfolioLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleTheoreticalPnlLine> TheoreticalPnlLines,
    IReadOnlyList<SecondCycleReconciliationLine> ReconciliationLines,
    IReadOnlyList<SecondCycleTheoreticalVsRealLine> TheoreticalVsRealLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    bool RebalanceIntentsRemainNonExecutable,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool R028ContractLineagePreserved,
    bool R027ContinuityGateLineagePreserved,
    bool R025PaperBaselineLineagePreserved,
    bool LedgerArchiveCommitPreviewLineagePreserved,
    bool SimulationResultPlanLineagePreserved,
    bool ExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool NoExternal,
    bool NoSchedulerServicePolling,
    bool NoAutomaticExecution,
    bool NoNewCycleRun,
    bool NoNewQubesBatchIngest,
    bool NoPaperLedgerCommit,
    bool NoPaperLedgerMutation,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoOrderCreated,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoBrokerRoute,
    bool NoSubmission);

public sealed record ManualPaperCycleOperatorRollingReport(
    ManualPaperCycleRunArchiveId ArchiveId,
    string RequestedCycleRunId,
    string QubesRunId,
    ManualRollingReadinessStatus RollingReadinessStatus,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> PaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    bool IncludesManualOperatorTriggeredDisclaimer,
    bool IncludesNoSchedulerDisclaimer,
    bool IncludesNoServiceDisclaimer,
    bool IncludesNoPollingDisclaimer,
    bool IncludesNoBrokerCallDisclaimer,
    bool IncludesNoLiveMarketDataDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    bool IncludesNoLiveBrokerProductionTradingMutationDisclaimer,
    bool IncludesNoPaperLedgerCommitDisclaimer,
    string Markdown);

public sealed record ManualPaperCycleRollingReadinessGate(
    string GateId,
    ManualPaperCycleRunArchiveId ArchiveId,
    ManualRollingReadinessStatus RollingReadinessStatus,
    bool RepeatedOperatorTriggeredManualRunsAllowed,
    bool AuthorizesScheduler,
    bool AuthorizesService,
    bool AuthorizesPolling,
    bool AuthorizesAutomaticExecution,
    bool AuthorizesBrokerCall,
    bool AuthorizesLiveTrading,
    bool AuthorizesOrderCreationOrSubmission,
    bool AuthorizesFills,
    bool AuthorizesExecutionReports,
    bool AuthorizesPaperLedgerCommit,
    bool RunsAnotherCycle,
    bool IngestsAnotherQubesBatch,
    bool MutatesPaperLedgerState,
    bool NoExternal);

public sealed record ManualPaperCycleResultArchiveResult(
    ManualPaperCycleResultArchiveRecord ArchiveRecord,
    ManualPaperCycleOperatorRollingReport OperatorReport,
    ManualPaperCycleRollingReadinessGate RollingReadinessGate,
    bool Persisted,
    bool AlreadyArchived);

public interface IManualPaperCycleResultArchiveRepository
{
    Task<ManualPaperCycleResultArchiveRecord?> GetByArchiveIdAsync(
        ManualPaperCycleRunArchiveId archiveId,
        CancellationToken cancellationToken);

    Task AddAsync(ManualPaperCycleResultArchiveRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryManualPaperCycleResultArchiveRepository : IManualPaperCycleResultArchiveRepository
{
    private readonly List<ManualPaperCycleResultArchiveRecord> records = [];

    public Task<ManualPaperCycleResultArchiveRecord?> GetByArchiveIdAsync(
        ManualPaperCycleRunArchiveId archiveId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.ArchiveId == archiveId));

    public Task AddAsync(ManualPaperCycleResultArchiveRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.ArchiveId == record.ArchiveId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class ManualPaperCycleResultArchiveService(
    IManualPaperCycleResultArchiveRepository repository,
    IClock clock)
{
    public async Task<ManualPaperCycleResultArchiveResult> ArchiveAsync(
        ManualPaperCycleRunResult manualCycleResult,
        CancellationToken cancellationToken)
    {
        var archiveId = new ManualPaperCycleRunArchiveId($"{manualCycleResult.ManualCycleRunResultId}:archive");
        var existing = await repository.GetByArchiveIdAsync(archiveId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = ManualPaperCycleArchiveStatus.DuplicateReturned };
            var duplicateGate = CreateRollingGate(duplicate);
            return new ManualPaperCycleResultArchiveResult(
                duplicate,
                ManualPaperCycleRollingReportRenderer.Render(duplicate, duplicateGate.RollingReadinessStatus),
                duplicateGate,
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateArchive(archiveId, manualCycleResult, clock.UtcNow);
        if (record.ArchiveStatus is ManualPaperCycleArchiveStatus.ArchivedNoExternal)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        var gate = CreateRollingGate(record);
        return new ManualPaperCycleResultArchiveResult(
            record,
            ManualPaperCycleRollingReportRenderer.Render(record, gate.RollingReadinessStatus),
            gate,
            Persisted: record.ArchiveStatus is ManualPaperCycleArchiveStatus.ArchivedNoExternal,
            AlreadyArchived: false);
    }

    private static ManualPaperCycleResultArchiveRecord CreateArchive(
        ManualPaperCycleRunArchiveId archiveId,
        ManualPaperCycleRunResult result,
        DateTimeOffset createdAtUtc)
    {
        var valid = result.RunStatus is ManualPaperCycleRunStatus.CompletedNoExternalFixture &&
                    result.Preflight.PreflightStatus is PaperCyclePreflightStatus.ReadyNoExternal &&
                    result.ManualCycleExecutionCount == 1 &&
                    !result.MultipleCyclesRun &&
                    result.RebalanceIntentsRemainNonExecutable &&
                    !result.StartedSchedulerServicePolling &&
                    !result.UsedBrokerOrLiveMarketData &&
                    !result.PaperLedgerStateCommittedOrMutated &&
                    !result.CreatedOrder &&
                    !result.CreatedFill &&
                    !result.CreatedExecutionReport &&
                    !result.CreatedBrokerRoute &&
                    !result.SubmittedOrder &&
                    !result.MutatedLivePositionState &&
                    !result.MutatedBrokerPositionState &&
                    !result.MutatedProductionLedgerState &&
                    !result.MutatedTradingState;

        return new ManualPaperCycleResultArchiveRecord(
            archiveId,
            result.Request.RequestedCycleRunId,
            result.Request.QubesRunId?.Value ?? string.Empty,
            result.Request.RunMode,
            result.Request.RequestedBy,
            result.Request.RequestedAtUtc,
            result.Preflight.PreflightStatus,
            result.RunStatus,
            valid ? ManualPaperCycleArchiveStatus.ArchivedNoExternal : ManualPaperCycleArchiveStatus.RejectedInvalidManualCycle,
            "NoExternalManualPaperCycleResultArchiveOnly",
            createdAtUtc,
            result.CycleResult.CycleCompletedAtUtc,
            result.Request.BaselineReference?.NextCycleBaselineReferenceId ?? string.Empty,
            result.CycleResult.QubesWeights.RawInputRowCount,
            result.CycleResult.QubesWeights.NormalizedOutputRowCount,
            result.CycleResult.CurrentPaperBaselineLines,
            result.CycleResult.TargetPortfolioLines,
            result.CycleResult.TargetVsCurrentDiffLines,
            result.CycleResult.TheoreticalPnlLines,
            result.CycleResult.ReconciliationLines,
            result.CycleResult.TheoreticalVsRealLines,
            result.CycleResult.RebalanceIntents,
            result.RebalanceIntentsRemainNonExecutable,
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            R028ContractLineagePreserved: true,
            R027ContinuityGateLineagePreserved: true,
            R025PaperBaselineLineagePreserved: true,
            LedgerArchiveCommitPreviewLineagePreserved: true,
            SimulationResultPlanLineagePreserved: true,
            ExecutionPlanLineagePreserved: true,
            PaperCandidateLineagePreserved: true,
            RiskLineagePreserved: true,
            RebalanceIntentLineagePreserved: true,
            LotSizingLineagePreserved: true,
            NoExternal: true,
            NoSchedulerServicePolling: true,
            NoAutomaticExecution: true,
            NoNewCycleRun: true,
            NoNewQubesBatchIngest: true,
            NoPaperLedgerCommit: true,
            NoPaperLedgerMutation: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoOrderCreated: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoBrokerRoute: true,
            NoSubmission: true);
    }

    private static ManualPaperCycleRollingReadinessGate CreateRollingGate(ManualPaperCycleResultArchiveRecord record)
    {
        var status = record.ArchiveStatus is not ManualPaperCycleArchiveStatus.ArchivedNoExternal
            ? ManualRollingReadinessStatus.HeldForReview
            : record.PreflightStatus is not PaperCyclePreflightStatus.ReadyNoExternal
                ? ManualRollingReadinessStatus.HeldForPreflightFailure
                : record.PaperBaselineLines.Count == 0
                    ? ManualRollingReadinessStatus.HeldForMissingBaseline
                    : record.TheoreticalPnlLines.Any(x => x.PnLStatus is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark)
                        ? ManualRollingReadinessStatus.HeldForMissingMarks
                        : ManualRollingReadinessStatus.ManualRollingReadyNoExternal;

        return new ManualPaperCycleRollingReadinessGate(
            $"{record.ArchiveId.Value}:manual-rolling-readiness-gate",
            record.ArchiveId,
            status,
            RepeatedOperatorTriggeredManualRunsAllowed: status is ManualRollingReadinessStatus.ManualRollingReadyNoExternal,
            AuthorizesScheduler: false,
            AuthorizesService: false,
            AuthorizesPolling: false,
            AuthorizesAutomaticExecution: false,
            AuthorizesBrokerCall: false,
            AuthorizesLiveTrading: false,
            AuthorizesOrderCreationOrSubmission: false,
            AuthorizesFills: false,
            AuthorizesExecutionReports: false,
            AuthorizesPaperLedgerCommit: false,
            RunsAnotherCycle: false,
            IngestsAnotherQubesBatch: false,
            MutatesPaperLedgerState: false,
            NoExternal: true);
    }
}

public static class ManualPaperCycleRollingReportRenderer
{
    public static ManualPaperCycleOperatorRollingReport Render(
        ManualPaperCycleResultArchiveRecord record,
        ManualRollingReadinessStatus rollingReadinessStatus)
    {
        var markdown = BuildMarkdown(record, rollingReadinessStatus);
        return new ManualPaperCycleOperatorRollingReport(
            record.ArchiveId,
            record.RequestedCycleRunId,
            record.QubesRunId,
            rollingReadinessStatus,
            record.PaperBaselineLines,
            record.TargetVsCurrentDiffLines,
            record.RebalanceIntents,
            IncludesManualOperatorTriggeredDisclaimer: true,
            IncludesNoSchedulerDisclaimer: true,
            IncludesNoServiceDisclaimer: true,
            IncludesNoPollingDisclaimer: true,
            IncludesNoBrokerCallDisclaimer: true,
            IncludesNoLiveMarketDataDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            IncludesNoLiveBrokerProductionTradingMutationDisclaimer: true,
            IncludesNoPaperLedgerCommitDisclaimer: true,
            markdown);
    }

    private static string BuildMarkdown(
        ManualPaperCycleResultArchiveRecord record,
        ManualRollingReadinessStatus rollingReadinessStatus)
    {
        var lines = new List<string>
        {
            "# R030 Manual Paper Cycle Rolling Report",
            string.Empty,
            $"Manual cycle: {record.RequestedCycleRunId}",
            $"Qubes run: {record.QubesRunId}",
            $"Archive status: {record.ArchiveStatus}",
            $"Rolling readiness: {rollingReadinessStatus}",
            string.Empty,
            "Manual operator-triggered cycle only.",
            "No scheduler.",
            "No service.",
            "No polling.",
            "No broker call.",
            "No live market data.",
            "No orders.",
            "No fills.",
            "No execution reports.",
            "No routes.",
            "No submissions.",
            "No live/broker/production/trading mutation.",
            "No paper ledger commit.",
            string.Empty,
            "## Paper Baseline"
        };

        lines.AddRange(record.PaperBaselineLines.Select(line =>
            $"- {line.Symbol}: {line.CurrentPaperQuantity.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency}"));

        lines.Add(string.Empty);
        lines.Add("## Target Vs Current");
        lines.AddRange(record.TargetVsCurrentDiffLines.Select(line =>
            $"- {line.Symbol}: delta notional {line.DeltaNotional?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"));

        lines.Add(string.Empty);
        lines.Add("## Rebalance Intents");
        lines.AddRange(record.RebalanceIntents.Select(intent =>
            $"- {intent.Symbol}: {intent.IntentSide}, non-executable={(!intent.IsExecutable).ToString(CultureInfo.InvariantCulture)}"));

        return string.Join(Environment.NewLine, lines);
    }
}
