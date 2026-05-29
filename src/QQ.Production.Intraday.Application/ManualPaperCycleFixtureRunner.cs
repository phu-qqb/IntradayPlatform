using System.Globalization;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum ManualPaperCycleRunStatus
{
    CompletedNoExternalFixture,
    DuplicateReturned,
    RejectedPreflightFailed,
    InconclusiveSafe
}

public sealed record ManualPaperCycleRunResult(
    string ManualCycleRunResultId,
    ManualPaperCycleRunStatus RunStatus,
    ManualPaperCycleRunRequest Request,
    ManualPaperCyclePreflightResult Preflight,
    PaperBaselineSecondCycleResult CycleResult,
    int ManualCycleExecutionCount,
    bool MultipleCyclesRun,
    bool PriorPaperContinuityReadyNoExternal,
    bool UsedLatestPaperLedgerFixtureBaseline,
    bool PaperLedgerStateCommittedOrMutated,
    bool StartedSchedulerServicePolling,
    bool UsedBrokerOrLiveMarketData,
    bool CreatedOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool CreatedBrokerRoute,
    bool SubmittedOrder,
    bool MutatedLivePositionState,
    bool MutatedBrokerPositionState,
    bool MutatedProductionLedgerState,
    bool MutatedTradingState,
    bool RebalanceIntentsRemainNonExecutable,
    bool QubesLineagePreserved,
    bool BaselineLineagePreserved,
    bool AudUsdTlsBoundaryInconclusiveNotFailed,
    bool UsdJpyCaveatPreserved);

public sealed record ManualPaperCycleOperatorReport(
    string ManualCycleRunResultId,
    string RequestedCycleRunId,
    string QubesRunId,
    ManualPaperCycleRunStatus RunStatus,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> CurrentPaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    bool IncludesManualNoExternalDisclaimer,
    bool IncludesNoSchedulerServicePollingDisclaimer,
    bool IncludesNoBrokerCallDisclaimer,
    bool IncludesNoLiveMarketDataDisclaimer,
    bool IncludesNoPaperLedgerCommitDisclaimer,
    bool IncludesNoOrderDisclaimer,
    bool IncludesNoFillDisclaimer,
    bool IncludesNoExecutionReportDisclaimer,
    bool IncludesNoRouteDisclaimer,
    bool IncludesNoSubmissionDisclaimer,
    bool IncludesNoLiveBrokerProductionTradingMutationDisclaimer,
    string Markdown);

public sealed record ManualPaperCycleFixtureExecutionResult(
    ManualPaperCycleRunResult RunResult,
    ManualPaperCycleOperatorReport OperatorReport,
    bool Persisted,
    bool AlreadyExecuted);

public interface IManualPaperCycleRunResultRepository
{
    Task<ManualPaperCycleRunResult?> GetByRequestedCycleRunIdAsync(
        string requestedCycleRunId,
        CancellationToken cancellationToken);

    Task AddAsync(ManualPaperCycleRunResult result, CancellationToken cancellationToken);
}

public sealed class InMemoryManualPaperCycleRunResultRepository : IManualPaperCycleRunResultRepository
{
    private readonly List<ManualPaperCycleRunResult> results = [];

    public Task<ManualPaperCycleRunResult?> GetByRequestedCycleRunIdAsync(
        string requestedCycleRunId,
        CancellationToken cancellationToken)
        => Task.FromResult(results.FirstOrDefault(x =>
            x.Request.RequestedCycleRunId.Equals(requestedCycleRunId, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(ManualPaperCycleRunResult result, CancellationToken cancellationToken)
    {
        if (results.Any(x => x.Request.RequestedCycleRunId.Equals(
                result.Request.RequestedCycleRunId,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        results.Add(result);
        return Task.CompletedTask;
    }
}

public sealed class ManualPaperCycleFixtureRunner(
    ManualPaperCycleRunnerContractService contractService,
    PaperBaselineSecondCycleService cycleService,
    IManualPaperCycleRunResultRepository repository)
{
    public async Task<ManualPaperCycleFixtureExecutionResult> ExecuteOneAsync(
        ManualPaperCycleRunRequest request,
        PaperCycleContinuityDecisionGate priorContinuityGate,
        PaperNextCycleBaselineReference baselineReference,
        PaperLedgerStateArchiveRecord paperLedgerStateArchive,
        DateTimeOffset producedAtUtc,
        DateTimeOffset effectiveAtUtc,
        DateTimeOffset cycleStartedAtUtc,
        DateTimeOffset cycleCompletedAtUtc,
        string fundCode,
        string modelName,
        decimal portfolioNotional,
        IReadOnlyList<string> rawQubesLines,
        IReadOnlyDictionary<string, InstrumentId> instrumentIdsBySymbol,
        decimal weightTolerance,
        decimal notionalTolerance,
        decimal pnlTolerance,
        TimeSpan maxMarkAge,
        int version,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByRequestedCycleRunIdAsync(request.RequestedCycleRunId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { RunStatus = ManualPaperCycleRunStatus.DuplicateReturned };
            return new ManualPaperCycleFixtureExecutionResult(
                duplicate,
                ManualPaperCycleOperatorReportRenderer.Render(duplicate),
                Persisted: false,
                AlreadyExecuted: true);
        }

        var contract = await contractService.DefineAsync(request, priorContinuityGate, cancellationToken);
        if (contract.Contract.Preflight.PreflightStatus is not PaperCyclePreflightStatus.ReadyNoExternal)
        {
            throw new DomainRuleViolationException("R029 manual paper cycle requires a passed no-external R028 preflight.");
        }

        var qubesRunId = request.QubesRunId ??
                         throw new DomainRuleViolationException("R029 manual paper cycle requires QubesRunId.");

        var cycle = await cycleService.RunSecondCycleAsync(
            baselineReference,
            paperLedgerStateArchive,
            request.RequestedCycleRunId,
            qubesRunId,
            producedAtUtc,
            effectiveAtUtc,
            cycleStartedAtUtc,
            cycleCompletedAtUtc,
            fundCode,
            modelName,
            portfolioNotional,
            rawQubesLines,
            instrumentIdsBySymbol,
            weightTolerance,
            notionalTolerance,
            pnlTolerance,
            maxMarkAge,
            version,
            cancellationToken);

        var result = CreateResult(request, contract.Contract.Preflight, cycle);
        await repository.AddAsync(result, cancellationToken);

        return new ManualPaperCycleFixtureExecutionResult(
            result,
            ManualPaperCycleOperatorReportRenderer.Render(result),
            Persisted: true,
            AlreadyExecuted: false);
    }

    private static ManualPaperCycleRunResult CreateResult(
        ManualPaperCycleRunRequest request,
        ManualPaperCyclePreflightResult preflight,
        PaperBaselineSecondCycleResult cycle)
        => new(
            $"{request.RequestedCycleRunId}:manual-cycle-run-result",
            cycle.CycleStatus is PaperBaselineSecondCycleStatus.CompletedNoExternal or PaperBaselineSecondCycleStatus.CompletedWithMissingMarks
                ? ManualPaperCycleRunStatus.CompletedNoExternalFixture
                : ManualPaperCycleRunStatus.InconclusiveSafe,
            request,
            preflight,
            cycle,
            ManualCycleExecutionCount: 1,
            MultipleCyclesRun: false,
            PriorPaperContinuityReadyNoExternal: true,
            UsedLatestPaperLedgerFixtureBaseline: cycle.Summary.UsedR025PaperLedgerBaseline,
            PaperLedgerStateCommittedOrMutated: false,
            StartedSchedulerServicePolling: false,
            UsedBrokerOrLiveMarketData: false,
            CreatedOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            CreatedBrokerRoute: false,
            SubmittedOrder: false,
            MutatedLivePositionState: false,
            MutatedBrokerPositionState: false,
            MutatedProductionLedgerState: false,
            MutatedTradingState: false,
            RebalanceIntentsRemainNonExecutable: cycle.RebalanceIntentsRemainNonExecutable,
            QubesLineagePreserved: cycle.QubesPersistence.Persisted,
            BaselineLineagePreserved: cycle.PaperBaselineInput.NoExternal && cycle.PaperBaselineInput.FixtureState,
            AudUsdTlsBoundaryInconclusiveNotFailed: true,
            UsdJpyCaveatPreserved: true);
}

public static class ManualPaperCycleOperatorReportRenderer
{
    public static ManualPaperCycleOperatorReport Render(ManualPaperCycleRunResult result)
    {
        var markdown = BuildMarkdown(result);
        return new ManualPaperCycleOperatorReport(
            result.ManualCycleRunResultId,
            result.Request.RequestedCycleRunId,
            result.Request.QubesRunId?.Value ?? string.Empty,
            result.RunStatus,
            result.CycleResult.CurrentPaperBaselineLines,
            result.CycleResult.TargetVsCurrentDiffLines,
            result.CycleResult.RebalanceIntents,
            IncludesManualNoExternalDisclaimer: true,
            IncludesNoSchedulerServicePollingDisclaimer: true,
            IncludesNoBrokerCallDisclaimer: true,
            IncludesNoLiveMarketDataDisclaimer: true,
            IncludesNoPaperLedgerCommitDisclaimer: true,
            IncludesNoOrderDisclaimer: true,
            IncludesNoFillDisclaimer: true,
            IncludesNoExecutionReportDisclaimer: true,
            IncludesNoRouteDisclaimer: true,
            IncludesNoSubmissionDisclaimer: true,
            IncludesNoLiveBrokerProductionTradingMutationDisclaimer: true,
            markdown);
    }

    private static string BuildMarkdown(ManualPaperCycleRunResult result)
    {
        var lines = new List<string>
        {
            "# R029 Manual Paper Cycle Operator Report",
            string.Empty,
            $"Manual cycle: {result.Request.RequestedCycleRunId}",
            $"Qubes run: {result.Request.QubesRunId?.Value}",
            $"Run status: {result.RunStatus}",
            string.Empty,
            "Manual no-external paper cycle fixture executed exactly once.",
            "No scheduler, service, polling, timer, background job, or automatic execution.",
            "No broker calls.",
            "No live market data.",
            "No paper ledger commit in R029.",
            "No live/broker/production/trading state mutation.",
            "No orders.",
            "No fills.",
            "No execution reports.",
            "No broker routes.",
            "No submissions.",
            string.Empty,
            "## Paper Baseline"
        };

        lines.AddRange(result.CycleResult.CurrentPaperBaselineLines.Select(line =>
            $"- {line.Symbol}: {line.CurrentPaperQuantity.ToString(CultureInfo.InvariantCulture)} {line.QuantityCurrency}"));

        lines.Add(string.Empty);
        lines.Add("## Target Vs Current");
        lines.AddRange(result.CycleResult.TargetVsCurrentDiffLines.Select(line =>
            $"- {line.Symbol}: delta notional {line.DeltaNotional?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"));

        lines.Add(string.Empty);
        lines.Add("## Rebalance Intents");
        lines.AddRange(result.CycleResult.RebalanceIntents.Select(intent =>
            $"- {intent.Symbol}: {intent.IntentSide}, non-executable={(!intent.IsExecutable).ToString(CultureInfo.InvariantCulture)}"));

        return string.Join(Environment.NewLine, lines);
    }
}
