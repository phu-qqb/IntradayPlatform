using System.Globalization;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public sealed record IntradayCycleArchiveRecord(
    string CycleRunId,
    string QubesRunId,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    int CycleCadenceMinutes,
    string CycleStatus,
    string SafetyStatus,
    QubesWeightAuditBatchId QubesAuditBatchId,
    ModelWeightBatchId? ModelWeightBatchId,
    ModelRunId? ModelRunId,
    int RawQubesRowAuditCount,
    int NormalizedWeightAuditCount,
    int TargetPortfolioPositionCount,
    decimal TheoreticalUnrealizedPnl,
    decimal TheoreticalTotalPnl,
    PnLComputationStatus TheoreticalPnlStatus,
    int ReconciliationLineCount,
    int ReconciliationDriftCount,
    int ReconciliationMissingActualCount,
    int ReconciliationMissingMarkCount,
    TheoreticalVsRealStatus ComparatorStatus,
    decimal ActualFixtureTotalPnl,
    decimal PnLDifference,
    int RebalanceIntentCount,
    bool RebalanceIntentsExecutable,
    int MissingOrStaleMarkWarningCount,
    bool NoExternal,
    DateTimeOffset ArchivedAtUtc);

public sealed record OperatorCycleReport(
    string CycleRunId,
    string QubesRunId,
    int CycleCadenceMinutes,
    string CycleStatus,
    string SafetyStatus,
    string TargetPortfolioSummary,
    string TheoreticalPnlSummary,
    string ReconciliationSummary,
    string TheoreticalVsRealSummary,
    string RebalanceIntentSummary,
    IReadOnlyList<string> MissingStaleMarkWarnings,
    IReadOnlyList<string> Disclaimers,
    string NextActionRecommendation);

public sealed record IntradayCycleArchiveResult(
    IntradayCycleArchiveRecord ArchiveRecord,
    OperatorCycleReport OperatorReport,
    string OperatorReportMarkdown,
    bool Persisted,
    bool AlreadyArchived);

public interface IIntradayCycleArchiveRepository
{
    Task<IntradayCycleArchiveRecord?> GetByCycleRunIdAsync(string cycleRunId, CancellationToken cancellationToken);
    Task AddAsync(IntradayCycleArchiveRecord archiveRecord, CancellationToken cancellationToken);
}

public sealed class InMemoryIntradayCycleArchiveRepository : IIntradayCycleArchiveRepository
{
    private readonly List<IntradayCycleArchiveRecord> records = [];

    public Task<IntradayCycleArchiveRecord?> GetByCycleRunIdAsync(string cycleRunId, CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.CycleRunId.Equals(cycleRunId, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(IntradayCycleArchiveRecord archiveRecord, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.CycleRunId.Equals(archiveRecord.CycleRunId, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        records.Add(archiveRecord);
        return Task.CompletedTask;
    }
}

public sealed class IntradayCycleArchiveService(IIntradayCycleArchiveRepository repository, IClock clock)
{
    public async Task<IntradayCycleArchiveResult> ArchiveAsync(
        QubesIntradayCycleFixtureResult cycle,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByCycleRunIdAsync(cycle.CycleRunId, cancellationToken);
        if (existing is not null)
        {
            var existingReport = OperatorCycleReportRenderer.CreateReport(existing);
            return new IntradayCycleArchiveResult(
                existing,
                existingReport,
                OperatorCycleReportRenderer.RenderMarkdown(existingReport),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(cycle, clock.UtcNow);
        await repository.AddAsync(record, cancellationToken);
        var report = OperatorCycleReportRenderer.CreateReport(record);
        return new IntradayCycleArchiveResult(
            record,
            report,
            OperatorCycleReportRenderer.RenderMarkdown(report),
            Persisted: true,
            AlreadyArchived: false);
    }

    private static IntradayCycleArchiveRecord CreateRecord(QubesIntradayCycleFixtureResult cycle, DateTimeOffset archivedAtUtc)
    {
        var reconciliation = cycle.ReconciliationComparator.ReconciliationLines;
        var comparator = cycle.ReconciliationComparator.FoundationTheoreticalVsRealReport;
        var warningCount = cycle.TheoreticalPnl.InstrumentDetails.Count(x =>
            x.PnLStatus is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark);
        var actualTotal = cycle.ReconciliationComparator.ActualPnlFixture.PortfolioPnL.TotalPnL;
        var theoreticalTotal = cycle.TheoreticalPnl.TheoreticalPnLSnapshot.PortfolioPnL.TotalPnL;

        return new IntradayCycleArchiveRecord(
            cycle.CycleRunId,
            cycle.QubesRunId.Value,
            cycle.QubesWeights.ProducedAtUtc,
            cycle.QubesWeights.EffectiveAtUtc,
            cycle.CycleStartedAtUtc,
            cycle.CycleCompletedAtUtc,
            cycle.CycleCadenceMinutes,
            cycle.CycleStatus.ToString(),
            cycle.Status.SafetyStatus,
            cycle.Persistence.AuditBatch.Id,
            cycle.Persistence.AuditBatch.ModelWeightBatchId,
            cycle.Persistence.AuditBatch.PromotedModelRunId,
            cycle.Persistence.RawRows.Count,
            cycle.Persistence.NormalizedRows.Count,
            cycle.TheoreticalPortfolioDiff.TargetPortfolioSnapshot.Positions.Count,
            cycle.TheoreticalPnl.TheoreticalPnLSnapshot.PortfolioPnL.UnrealizedPnL,
            theoreticalTotal,
            cycle.TheoreticalPnl.TheoreticalPnLSnapshot.Status,
            reconciliation.Count,
            reconciliation.Count(x => x.Status == QubesReconciliationLineStatus.Drift),
            reconciliation.Count(x => x.Status == QubesReconciliationLineStatus.MissingActual),
            reconciliation.Count(x => x.Status == QubesReconciliationLineStatus.MissingMark),
            comparator.Status,
            actualTotal,
            actualTotal - theoreticalTotal,
            cycle.TheoreticalPortfolioDiff.RebalanceIntents.Count,
            cycle.TheoreticalPortfolioDiff.RebalanceIntents.Any(x => x.IsExecutable),
            warningCount,
            cycle.IsNoExternalFixture &&
                !cycle.StartedApiOrWorker &&
                !cycle.StartedBackgroundExecution &&
                !cycle.UsedLiveMarketData &&
                !cycle.CalledBrokerGateway &&
                !cycle.SubmittedOrders &&
                !cycle.CreatedExecutableOrder &&
                !cycle.MutatedLiveTradingState,
            archivedAtUtc);
    }
}

public static class OperatorCycleReportRenderer
{
    public static OperatorCycleReport CreateReport(IntradayCycleArchiveRecord archive)
        => new(
            archive.CycleRunId,
            archive.QubesRunId,
            archive.CycleCadenceMinutes,
            archive.CycleStatus,
            archive.SafetyStatus,
            $"{archive.TargetPortfolioPositionCount} theoretical USD-quote target positions archived.",
            $"Theoretical fixture PnL total {FormatMoney(archive.TheoreticalTotalPnl)} with status {archive.TheoreticalPnlStatus}.",
            $"{archive.ReconciliationLineCount} reconciliation rows archived; drift={archive.ReconciliationDriftCount}, missing actual={archive.ReconciliationMissingActualCount}, missing/stale mark={archive.ReconciliationMissingMarkCount}.",
            $"Comparator status {archive.ComparatorStatus}; actual fixture PnL {FormatMoney(archive.ActualFixtureTotalPnl)}; difference {FormatMoney(archive.PnLDifference)}.",
            $"{archive.RebalanceIntentCount} rebalance intents archived as non-executable.",
            archive.MissingOrStaleMarkWarningCount == 0
                ? []
                : [$"{archive.MissingOrStaleMarkWarningCount} missing/stale mark warning rows are preserved and were not hidden."],
            [
                "No external broker call occurred.",
                "No live market data was requested.",
                "No orders were created.",
                "No trading occurred.",
                "All positions, PnL, and actual state in this report are fixture-based unless explicitly marked otherwise."
            ],
            "Proceed to R008 operator review actions: approve, reject, hold, or promote the archived cycle without scheduler, broker calls, or executable orders.");

    public static string RenderMarkdown(OperatorCycleReport report)
    {
        var warnings = report.MissingStaleMarkWarnings.Count == 0
            ? "- None."
            : string.Join(Environment.NewLine, report.MissingStaleMarkWarnings.Select(x => $"- {x}"));
        var disclaimers = string.Join(Environment.NewLine, report.Disclaimers.Select(x => $"- {x}"));

        return string.Join(Environment.NewLine, [
            "# Intraday Cycle Operator Report",
            "",
            $"CycleRunId: {report.CycleRunId}",
            $"QubesRunId: {report.QubesRunId}",
            $"CadenceMinutes: {report.CycleCadenceMinutes}",
            $"CycleStatus: {report.CycleStatus}",
            $"SafetyStatus: {report.SafetyStatus}",
            "",
            "## Target Portfolio",
            report.TargetPortfolioSummary,
            "",
            "## Theoretical PnL",
            report.TheoreticalPnlSummary,
            "",
            "## Reconciliation",
            report.ReconciliationSummary,
            "",
            "## Theoretical vs Real",
            report.TheoreticalVsRealSummary,
            "",
            "## Rebalance Intents",
            report.RebalanceIntentSummary,
            "",
            "## Missing/Stale Mark Warnings",
            warnings,
            "",
            "## No-External / No-Trading Disclaimer",
            disclaimers,
            "",
            "## Next Action",
            report.NextActionRecommendation,
            ""
        ]);
    }

    private static string FormatMoney(decimal value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);
}
