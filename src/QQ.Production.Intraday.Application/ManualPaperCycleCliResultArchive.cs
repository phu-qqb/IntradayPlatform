using System.Globalization;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum ManualPaperCycleCliArchiveStatus
{
    ArchivedNoExternal,
    DuplicateReturned,
    RejectedInvalidCliResult,
    InconclusiveSafe
}

public enum ManualPaperCycleCliReadinessStatus
{
    ManualCliReadyForRepeatedOperatorUseNoExternal,
    HeldForReview,
    HeldForPreflightIssue,
    HeldForMissingBaseline,
    HeldForMissingQubesFixture,
    InconclusiveSafe
}

public sealed record ManualPaperCycleCliInvocationArchiveId(string Value);

public sealed record ManualPaperCycleCliInvocationRecord(
    ManualPaperCycleCliInvocationArchiveId CliInvocationId,
    string RequestedCycleRunId,
    string QubesRunId,
    PaperCycleRunMode RunMode,
    string RequestedBy,
    PaperCyclePreflightStatus PreflightStatus,
    ManualPaperCycleRunStatus CycleStatus,
    ManualPaperCycleCliArchiveStatus ArchiveStatus,
    int ExecutionCount,
    int CliInvocationCount,
    string SafetyStatus,
    string OutputArtifactsDirectory,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset CompletedAtUtc,
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
    bool R030ManualRollingReadinessLineagePreserved,
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
    bool NoBrokerCall,
    bool NoLiveMarketData,
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
    bool NoSubmission,
    bool MoreThanOneCliInvocationRun,
    bool MoreThanOneCycleRun,
    bool NewQubesBatchOutsideCliIngested);

public sealed record ManualPaperCycleCliOperatorReport(
    ManualPaperCycleCliInvocationArchiveId CliInvocationId,
    string RequestedCycleRunId,
    string QubesRunId,
    ManualPaperCycleCliReadinessStatus ReadinessStatus,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> PaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    bool IncludesManualCliInvocationOnlyDisclaimer,
    bool IncludesExactlyOneCycleDisclaimer,
    bool IncludesNoSchedulerDisclaimer,
    bool IncludesNoServiceDisclaimer,
    bool IncludesNoPollingDisclaimer,
    bool IncludesNoAutomaticExecutionDisclaimer,
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

public sealed record ManualPaperCycleCliRepeatedUseGate(
    string GateId,
    ManualPaperCycleCliInvocationArchiveId CliInvocationId,
    ManualPaperCycleCliReadinessStatus ReadinessStatus,
    bool RepeatedManualOperatorUseAllowed,
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
    bool NoExternal);

public sealed record ManualPaperCycleCliResultArchiveResult(
    ManualPaperCycleCliInvocationRecord ArchiveRecord,
    ManualPaperCycleCliOperatorReport OperatorReport,
    ManualPaperCycleCliRepeatedUseGate RepeatedUseGate,
    bool Persisted,
    bool AlreadyArchived);

public interface IManualPaperCycleCliResultArchiveRepository
{
    Task<ManualPaperCycleCliInvocationRecord?> GetByCliInvocationIdAsync(
        ManualPaperCycleCliInvocationArchiveId cliInvocationId,
        CancellationToken cancellationToken);

    Task AddAsync(ManualPaperCycleCliInvocationRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryManualPaperCycleCliResultArchiveRepository : IManualPaperCycleCliResultArchiveRepository
{
    private readonly List<ManualPaperCycleCliInvocationRecord> records = [];

    public Task<ManualPaperCycleCliInvocationRecord?> GetByCliInvocationIdAsync(
        ManualPaperCycleCliInvocationArchiveId cliInvocationId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.CliInvocationId == cliInvocationId));

    public Task AddAsync(ManualPaperCycleCliInvocationRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.CliInvocationId == record.CliInvocationId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class ManualPaperCycleCliResultArchiveService(
    IManualPaperCycleCliResultArchiveRepository repository,
    IClock clock)
{
    public async Task<ManualPaperCycleCliResultArchiveResult> ArchiveAsync(
        ManualPaperCycleCliResult cliResult,
        CancellationToken cancellationToken)
    {
        var requestedCycleRunId = cliResult.ParsedRequest?.Request.RequestedCycleRunId ?? "missing-requested-cycle";
        var cliInvocationId = new ManualPaperCycleCliInvocationArchiveId($"{requestedCycleRunId}:cli-invocation-result-archive");
        var existing = await repository.GetByCliInvocationIdAsync(cliInvocationId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { ArchiveStatus = ManualPaperCycleCliArchiveStatus.DuplicateReturned };
            var duplicateGate = CreateRepeatedUseGate(duplicate);
            return new ManualPaperCycleCliResultArchiveResult(
                duplicate,
                ManualPaperCycleCliOperatorReportRenderer.Render(duplicate, duplicateGate.ReadinessStatus),
                duplicateGate,
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateArchive(cliInvocationId, cliResult, clock.UtcNow);
        if (record.ArchiveStatus is ManualPaperCycleCliArchiveStatus.ArchivedNoExternal)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        var gate = CreateRepeatedUseGate(record);
        return new ManualPaperCycleCliResultArchiveResult(
            record,
            ManualPaperCycleCliOperatorReportRenderer.Render(record, gate.ReadinessStatus),
            gate,
            Persisted: record.ArchiveStatus is ManualPaperCycleCliArchiveStatus.ArchivedNoExternal,
            AlreadyArchived: false);
    }

    private static ManualPaperCycleCliInvocationRecord CreateArchive(
        ManualPaperCycleCliInvocationArchiveId cliInvocationId,
        ManualPaperCycleCliResult result,
        DateTimeOffset createdAtUtc)
    {
        var cycle = result.CycleRunResult?.CycleResult;
        var valid = result.CliStatus is ManualPaperCycleCliStatus.CompletedNoExternal &&
                    result.CycleRunResult is not null &&
                    result.PreflightResult?.PreflightStatus is PaperCyclePreflightStatus.ReadyNoExternal &&
                    result.ParsedRequest?.Request.RunMode is PaperCycleRunMode.ManualNoExternal &&
                    result.CycleExecutionCount == 1 &&
                    result.CycleExecuted &&
                    !result.MoreThanOneCycleAllowed &&
                    !result.PaperLedgerCommitted &&
                    !result.StartedSchedulerServicePolling &&
                    !result.AutomaticExecutionIntroduced &&
                    !result.UsedBrokerOrLiveInput &&
                    !result.CreatedOrder &&
                    !result.CreatedFill &&
                    !result.CreatedExecutionReport &&
                    !result.CreatedRoute &&
                    !result.SubmittedOrder &&
                    !result.MutatedLivePositionState &&
                    !result.MutatedBrokerPositionState &&
                    !result.MutatedProductionLedgerState &&
                    !result.MutatedTradingState &&
                    result.CycleRunResult.RebalanceIntentsRemainNonExecutable;

        return new ManualPaperCycleCliInvocationRecord(
            cliInvocationId,
            result.ParsedRequest?.Request.RequestedCycleRunId ?? string.Empty,
            result.ParsedRequest?.Request.QubesRunId?.Value ?? string.Empty,
            result.ParsedRequest?.Request.RunMode ?? PaperCycleRunMode.LiveRequested,
            result.ParsedRequest?.Request.RequestedBy ?? string.Empty,
            result.PreflightResult?.PreflightStatus ?? PaperCyclePreflightStatus.InconclusiveSafe,
            result.CycleRunResult?.RunStatus ?? ManualPaperCycleRunStatus.InconclusiveSafe,
            valid ? ManualPaperCycleCliArchiveStatus.ArchivedNoExternal : ManualPaperCycleCliArchiveStatus.RejectedInvalidCliResult,
            result.CycleExecutionCount,
            CliInvocationCount: result.CycleExecuted ? 1 : 0,
            "NoExternalManualCliResultArchiveOnly",
            result.ParsedRequest?.OutputArtifactsDirectory ?? string.Empty,
            createdAtUtc,
            cycle?.CycleCompletedAtUtc ?? createdAtUtc,
            cycle?.CurrentPaperBaselineLines ?? [],
            cycle?.TargetPortfolioLines ?? [],
            cycle?.TargetVsCurrentDiffLines ?? [],
            cycle?.TheoreticalPnlLines ?? [],
            cycle?.ReconciliationLines ?? [],
            cycle?.TheoreticalVsRealLines ?? [],
            cycle?.RebalanceIntents ?? [],
            result.CycleRunResult?.RebalanceIntentsRemainNonExecutable ?? false,
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            R028ContractLineagePreserved: true,
            R030ManualRollingReadinessLineagePreserved: true,
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
            NoBrokerCall: true,
            NoLiveMarketData: true,
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
            NoSubmission: true,
            MoreThanOneCliInvocationRun: false,
            MoreThanOneCycleRun: result.MoreThanOneCycleAllowed,
            NewQubesBatchOutsideCliIngested: false);
    }

    private static ManualPaperCycleCliRepeatedUseGate CreateRepeatedUseGate(ManualPaperCycleCliInvocationRecord record)
    {
        var status = record.ArchiveStatus is not ManualPaperCycleCliArchiveStatus.ArchivedNoExternal
            ? ManualPaperCycleCliReadinessStatus.HeldForReview
            : record.PreflightStatus is not PaperCyclePreflightStatus.ReadyNoExternal
                ? ManualPaperCycleCliReadinessStatus.HeldForPreflightIssue
                : record.PaperBaselineLines.Count == 0
                    ? ManualPaperCycleCliReadinessStatus.HeldForMissingBaseline
                    : string.IsNullOrWhiteSpace(record.QubesRunId)
                        ? ManualPaperCycleCliReadinessStatus.HeldForMissingQubesFixture
                        : ManualPaperCycleCliReadinessStatus.ManualCliReadyForRepeatedOperatorUseNoExternal;

        return new ManualPaperCycleCliRepeatedUseGate(
            $"{record.CliInvocationId.Value}:repeated-use-gate",
            record.CliInvocationId,
            status,
            RepeatedManualOperatorUseAllowed: status is ManualPaperCycleCliReadinessStatus.ManualCliReadyForRepeatedOperatorUseNoExternal,
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
            NoExternal: true);
    }
}

public static class ManualPaperCycleCliOperatorReportRenderer
{
    public static ManualPaperCycleCliOperatorReport Render(
        ManualPaperCycleCliInvocationRecord record,
        ManualPaperCycleCliReadinessStatus readinessStatus)
    {
        var markdown = BuildMarkdown(record, readinessStatus);
        return new ManualPaperCycleCliOperatorReport(
            record.CliInvocationId,
            record.RequestedCycleRunId,
            record.QubesRunId,
            readinessStatus,
            record.PaperBaselineLines,
            record.TargetVsCurrentDiffLines,
            record.RebalanceIntents,
            IncludesManualCliInvocationOnlyDisclaimer: true,
            IncludesExactlyOneCycleDisclaimer: true,
            IncludesNoSchedulerDisclaimer: true,
            IncludesNoServiceDisclaimer: true,
            IncludesNoPollingDisclaimer: true,
            IncludesNoAutomaticExecutionDisclaimer: true,
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
        ManualPaperCycleCliInvocationRecord record,
        ManualPaperCycleCliReadinessStatus readinessStatus)
    {
        var lines = new List<string>
        {
            "# R032 Manual CLI Operator Report",
            string.Empty,
            $"CLI invocation: {record.CliInvocationId.Value}",
            $"Manual cycle: {record.RequestedCycleRunId}",
            $"Qubes run: {record.QubesRunId}",
            $"Archive status: {record.ArchiveStatus}",
            $"Repeated-use readiness: {readinessStatus}",
            string.Empty,
            "Manual CLI invocation only.",
            "Exactly one cycle.",
            "No scheduler.",
            "No service.",
            "No polling.",
            "No automatic execution.",
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
