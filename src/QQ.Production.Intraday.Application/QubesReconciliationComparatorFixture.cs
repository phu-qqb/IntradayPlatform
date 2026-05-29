using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum QubesReconciliationLineStatus
{
    InSync,
    Drift,
    MissingActual,
    MissingTarget,
    MissingTheoretical,
    MissingMark,
    InconclusiveSafe
}

public sealed record QubesTargetActualReconciliationLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TargetWeight,
    decimal ActualWeight,
    decimal WeightDifference,
    decimal TargetNotional,
    decimal ActualNotional,
    decimal NotionalDifference,
    QubesReconciliationLineStatus Status,
    ReconciliationSeverity Severity,
    PnLComputationStatus? MarkEffectStatus,
    string Reason);

public sealed record QubesTheoreticalVsRealComparatorLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TheoreticalWeight,
    decimal ActualWeight,
    decimal WeightDifference,
    decimal TheoreticalNotional,
    decimal ActualNotional,
    decimal NotionalDifference,
    decimal TheoreticalPnL,
    decimal ActualPnL,
    decimal PnLDifference,
    QubesReconciliationLineStatus Status,
    string Reason);

public sealed record QubesReconciliationComparatorRequest(
    QubesTheoreticalPortfolioDiffResult R003Diff,
    QubesTheoreticalPnlFixtureResult R004Pnl,
    PersistQubesWeightsResult QubesPersistence,
    PortfolioSnapshot ActualPortfolioFixture,
    PnLSnapshot ActualPnlFixture,
    IReadOnlyDictionary<InstrumentId, string> SymbolsByInstrument,
    DateTimeOffset CreatedAtUtc,
    decimal WeightTolerance,
    decimal NotionalTolerance,
    decimal PnLTolerance);

public sealed record QubesReconciliationComparatorResult(
    QubesRunId QubesRunId,
    ModelWeightSourceSystem SourceSystem,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    int RawInputRowCount,
    int NormalizedOutputRowCount,
    QubesWeightAuditBatch AuditBatch,
    IReadOnlyList<QubesRawWeightAuditRow> RawAuditRows,
    IReadOnlyList<QubesNormalizedWeightAuditRow> NormalizedAuditRows,
    PortfolioSnapshot TheoreticalTargetPortfolioSnapshot,
    PortfolioSnapshot ActualPortfolioFixture,
    PnLSnapshot TheoreticalPnLSnapshot,
    PnLSnapshot ActualPnlFixture,
    ReconciliationReport FoundationReconciliationReport,
    TheoreticalVsRealReport FoundationTheoreticalVsRealReport,
    IReadOnlyList<QubesTargetActualReconciliationLine> ReconciliationLines,
    IReadOnlyList<QubesTheoreticalVsRealComparatorLine> ComparatorLines,
    bool UsedPersistedQubesLineage,
    bool UsedNoExternalActualPortfolioFixture,
    bool UsedNoExternalActualPnlFixture,
    bool ActualFixtureIsBrokerReportedLiveState,
    bool UsedLiveMarketData,
    bool CalledBrokerGateway,
    bool CreatedExecutableOrder,
    bool RebalanceIntentsRemainNonExecutable)
{
    public bool HasDrift => ReconciliationLines.Any(x => x.Status == QubesReconciliationLineStatus.Drift) ||
                            ComparatorLines.Any(x => x.Status == QubesReconciliationLineStatus.Drift);
}

public static class ActualPortfolioFixtureFactory
{
    public static PortfolioSnapshot CreateWithDeterministicDrifts(
        PortfolioSnapshot target,
        IReadOnlyDictionary<InstrumentId, string> symbolsByInstrument,
        DateTimeOffset snapshotTimestampUtc)
    {
        var positions = new List<PortfolioPosition>();

        foreach (var targetPosition in target.Positions)
        {
            var symbol = symbolsByInstrument.TryGetValue(targetPosition.InstrumentId, out var mapped)
                ? mapped
                : targetPosition.InstrumentId.Value.ToString("N");

            if (symbol.Equals("JPYUSD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var actualWeight = symbol.ToUpperInvariant() switch
            {
                "EURUSD" => targetPosition.WeightExposure + 0.010000m,
                "GBPUSD" => targetPosition.WeightExposure - 0.010000m,
                "NOKUSD" => targetPosition.WeightExposure + 0.005000m,
                _ => targetPosition.WeightExposure
            };
            var actualNotional = target.PortfolioNotional * actualWeight;

            positions.Add(new PortfolioPosition(
                targetPosition.InstrumentId,
                0m,
                null,
                actualNotional,
                actualWeight,
                new Dictionary<string, decimal> { ["USD"] = actualNotional }));
        }

        return new PortfolioSnapshot(
            snapshotTimestampUtc,
            PortfolioStateSource.Simulated,
            target.PortfolioNotional,
            positions,
            [new CashComponent("USD", target.PortfolioNotional - positions.Sum(x => x.NotionalExposure))]);
    }
}

public static class ActualPnlFixtureFactory
{
    public static PnLSnapshot Create(
        PortfolioSnapshot actualFixture,
        PnLSnapshot theoreticalPnl,
        IReadOnlyDictionary<InstrumentId, string> symbolsByInstrument,
        DateTimeOffset snapshotTimestampUtc)
    {
        if (actualFixture.StateSource == PortfolioStateSource.BrokerReported)
        {
            throw new DomainRuleViolationException("R005 actual PnL fixture must not be broker-reported live state.");
        }

        var theoreticalByInstrument = theoreticalPnl.Instruments.ToDictionary(x => x.InstrumentId);
        var rows = new List<InstrumentPnL>();

        foreach (var position in actualFixture.Positions)
        {
            var symbol = symbolsByInstrument.TryGetValue(position.InstrumentId, out var mapped)
                ? mapped
                : position.InstrumentId.Value.ToString("N");
            theoreticalByInstrument.TryGetValue(position.InstrumentId, out var theoreticalRow);

            if (theoreticalRow is { Status: PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark or PnLComputationStatus.Inconclusive })
            {
                rows.Add(new InstrumentPnL(position.InstrumentId, 0m, 0m, theoreticalRow.Status, "Actual fixture PnL inherits safe mark status from no-external theoretical fixture."));
                continue;
            }

            var theoreticalUnrealized = theoreticalRow?.UnrealizedPnL ?? 0m;
            var actualUnrealized = symbol.ToUpperInvariant() switch
            {
                "EURUSD" => theoreticalUnrealized + 100m,
                "GBPUSD" => theoreticalUnrealized - 200m,
                _ => theoreticalUnrealized
            };

            rows.Add(new InstrumentPnL(position.InstrumentId, actualUnrealized, 0m, PnLComputationStatus.Computed, "Actual fixture PnL computed from deterministic no-external fixture deltas."));
        }

        var computedUnrealized = rows.Where(x => x.Status == PnLComputationStatus.Computed).Sum(x => x.UnrealizedPnL);
        var status = rows.Any(x => x.Status == PnLComputationStatus.MissingMark)
            ? PnLComputationStatus.MissingMark
            : rows.Any(x => x.Status == PnLComputationStatus.StaleMark)
                ? PnLComputationStatus.StaleMark
                : rows.Any(x => x.Status == PnLComputationStatus.Inconclusive)
                    ? PnLComputationStatus.Inconclusive
                    : PnLComputationStatus.Computed;

        return new PnLSnapshot(
            snapshotTimestampUtc,
            PnLSource.Simulated,
            new PortfolioPnL(computedUnrealized, 0m, computedUnrealized),
            rows,
            status);
    }
}

public sealed class QubesReconciliationComparatorFixtureService
{
    public QubesReconciliationComparatorResult Create(QubesReconciliationComparatorRequest request)
    {
        Validate(request);

        var target = request.R003Diff.TargetPortfolioSnapshot;
        var actual = request.ActualPortfolioFixture;
        var reconciliation = new PortfolioReconciler().Reconcile(
            target,
            actual,
            request.WeightTolerance,
            request.NotionalTolerance,
            request.CreatedAtUtc);
        var comparator = new TheoreticalVsRealComparator().Compare(
            target,
            actual,
            request.R004Pnl.TheoreticalPnLSnapshot,
            request.ActualPnlFixture,
            request.WeightTolerance,
            request.NotionalTolerance,
            request.PnLTolerance,
            request.CreatedAtUtc);
        var reconciliationLines = BuildReconciliationLines(request, target, actual);
        var comparatorLines = BuildComparatorLines(request, target, actual);

        return new QubesReconciliationComparatorResult(
            request.R003Diff.QubesRunId,
            request.R003Diff.SourceSystem,
            request.R003Diff.ProducedAtUtc,
            request.R003Diff.EffectiveAtUtc,
            request.R003Diff.CadenceMinutes,
            request.R003Diff.RawInputRowCount,
            request.R003Diff.NormalizedOutputRowCount,
            request.QubesPersistence.AuditBatch,
            request.QubesPersistence.RawRows,
            request.QubesPersistence.NormalizedRows,
            target,
            actual,
            request.R004Pnl.TheoreticalPnLSnapshot,
            request.ActualPnlFixture,
            reconciliation,
            comparator,
            reconciliationLines,
            comparatorLines,
            UsedPersistedQubesLineage: true,
            UsedNoExternalActualPortfolioFixture: actual.StateSource == PortfolioStateSource.Simulated,
            UsedNoExternalActualPnlFixture: request.ActualPnlFixture.Source == PnLSource.Simulated,
            ActualFixtureIsBrokerReportedLiveState: actual.StateSource == PortfolioStateSource.BrokerReported,
            UsedLiveMarketData: false,
            CalledBrokerGateway: false,
            CreatedExecutableOrder: false,
            RebalanceIntentsRemainNonExecutable: request.R003Diff.RebalanceIntents.All(x => !x.IsExecutable));
    }

    private static void Validate(QubesReconciliationComparatorRequest request)
    {
        if (request.R003Diff.SourceSystem != ModelWeightSourceSystem.Qubes ||
            request.R004Pnl.SourceSystem != ModelWeightSourceSystem.Qubes ||
            request.QubesPersistence.AuditBatch.SourceSystem != ModelWeightSourceSystem.Qubes)
        {
            throw new DomainRuleViolationException("R005 reconciliation requires Qubes source metadata throughout the pipeline.");
        }

        if (request.R003Diff.CadenceMinutes != 15 ||
            request.R004Pnl.CadenceMinutes != 15 ||
            request.QubesPersistence.AuditBatch.CadenceMinutes != 15)
        {
            throw new DomainRuleViolationException("R005 reconciliation requires 15-minute Qubes cadence throughout the pipeline.");
        }

        if (request.R003Diff.QubesRunId.Value != request.R004Pnl.QubesRunId.Value ||
            request.R003Diff.QubesRunId.Value != request.QubesPersistence.AuditBatch.QubesRunId)
        {
            throw new DomainRuleViolationException("R005 reconciliation requires preserved QubesRunId lineage.");
        }

        if (request.QubesPersistence.RawRows.Count == 0 || request.QubesPersistence.NormalizedRows.Count == 0)
        {
            throw new DomainRuleViolationException("R005 reconciliation requires persisted raw and normalized Qubes audit rows.");
        }

        if (request.QubesPersistence.AuditBatch.ModelWeightBatchId is null ||
            request.QubesPersistence.AuditBatch.PromotedModelRunId is null ||
            request.QubesPersistence.NormalizedRows.All(x => x.TargetWeightInstrumentId is null))
        {
            throw new DomainRuleViolationException("R005 reconciliation requires ModelWeightBatch, ModelRun, and TargetWeight linkage.");
        }

        if (request.ActualPortfolioFixture.StateSource == PortfolioStateSource.BrokerReported)
        {
            throw new DomainRuleViolationException("R005 actual portfolio must be a no-external fixture, not broker-reported live state.");
        }

        if (request.R004Pnl.UsedLiveBrokerMarketData || request.R004Pnl.CalledBrokerGateway || request.R004Pnl.CreatedExecutableOrder)
        {
            throw new DomainRuleViolationException("R005 cannot consume live broker market data, broker gateways, or executable orders.");
        }

        if (request.R003Diff.RebalanceIntents.Any(x => x.IsExecutable))
        {
            throw new DomainRuleViolationException("R005 requires R003 rebalance intents to remain non-executable.");
        }
    }

    private static IReadOnlyList<QubesTargetActualReconciliationLine> BuildReconciliationLines(
        QubesReconciliationComparatorRequest request,
        PortfolioSnapshot target,
        PortfolioSnapshot actual)
    {
        var targetByInstrument = target.Positions.ToDictionary(x => x.InstrumentId);
        var actualByInstrument = actual.Positions.ToDictionary(x => x.InstrumentId);
        var pnlByInstrument = request.R004Pnl.TheoreticalPnLSnapshot.Instruments.ToDictionary(x => x.InstrumentId);
        var instruments = targetByInstrument.Keys.Concat(actualByInstrument.Keys).Distinct();

        return instruments.Select(instrumentId =>
            {
                targetByInstrument.TryGetValue(instrumentId, out var targetPosition);
                actualByInstrument.TryGetValue(instrumentId, out var actualPosition);
                pnlByInstrument.TryGetValue(instrumentId, out var pnlRow);
                var targetWeight = targetPosition?.WeightExposure ?? 0m;
                var actualWeight = actualPosition?.WeightExposure ?? 0m;
                var targetNotional = targetPosition?.NotionalExposure ?? 0m;
                var actualNotional = actualPosition?.NotionalExposure ?? 0m;
                var weightDifference = actualWeight - targetWeight;
                var notionalDifference = actualNotional - targetNotional;
                var status = Classify(targetPosition, actualPosition, pnlRow, weightDifference, notionalDifference, request.WeightTolerance, request.NotionalTolerance);

                return new QubesTargetActualReconciliationLine(
                    instrumentId,
                    Symbol(request.SymbolsByInstrument, instrumentId),
                    targetWeight,
                    actualWeight,
                    weightDifference,
                    targetNotional,
                    actualNotional,
                    notionalDifference,
                    status,
                    Severity(status),
                    pnlRow?.Status,
                    Reason(status));
            })
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<QubesTheoreticalVsRealComparatorLine> BuildComparatorLines(
        QubesReconciliationComparatorRequest request,
        PortfolioSnapshot theoretical,
        PortfolioSnapshot actual)
    {
        var theoreticalByInstrument = theoretical.Positions.ToDictionary(x => x.InstrumentId);
        var actualByInstrument = actual.Positions.ToDictionary(x => x.InstrumentId);
        var theoreticalPnlByInstrument = request.R004Pnl.TheoreticalPnLSnapshot.Instruments.ToDictionary(x => x.InstrumentId);
        var actualPnlByInstrument = request.ActualPnlFixture.Instruments.ToDictionary(x => x.InstrumentId);
        var instruments = theoreticalByInstrument.Keys.Concat(actualByInstrument.Keys).Distinct();

        return instruments.Select(instrumentId =>
            {
                theoreticalByInstrument.TryGetValue(instrumentId, out var theoreticalPosition);
                actualByInstrument.TryGetValue(instrumentId, out var actualPosition);
                theoreticalPnlByInstrument.TryGetValue(instrumentId, out var theoreticalPnl);
                actualPnlByInstrument.TryGetValue(instrumentId, out var actualPnl);
                var theoreticalWeight = theoreticalPosition?.WeightExposure ?? 0m;
                var actualWeight = actualPosition?.WeightExposure ?? 0m;
                var theoreticalNotional = theoreticalPosition?.NotionalExposure ?? 0m;
                var actualNotional = actualPosition?.NotionalExposure ?? 0m;
                var theoreticalUnrealized = theoreticalPnl?.UnrealizedPnL ?? 0m;
                var actualUnrealized = actualPnl?.UnrealizedPnL ?? 0m;
                var weightDifference = actualWeight - theoreticalWeight;
                var notionalDifference = actualNotional - theoreticalNotional;
                var pnlDifference = actualUnrealized - theoreticalUnrealized;
                var markStatus = actualPnl?.Status ?? theoreticalPnl?.Status;
                var status = Classify(theoreticalPosition, actualPosition, markStatus, weightDifference, notionalDifference, pnlDifference, request.WeightTolerance, request.NotionalTolerance, request.PnLTolerance);

                return new QubesTheoreticalVsRealComparatorLine(
                    instrumentId,
                    Symbol(request.SymbolsByInstrument, instrumentId),
                    theoreticalWeight,
                    actualWeight,
                    weightDifference,
                    theoreticalNotional,
                    actualNotional,
                    notionalDifference,
                    theoreticalUnrealized,
                    actualUnrealized,
                    pnlDifference,
                    status,
                    Reason(status));
            })
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static QubesReconciliationLineStatus Classify(
        PortfolioPosition? target,
        PortfolioPosition? actual,
        InstrumentPnL? pnl,
        decimal weightDifference,
        decimal notionalDifference,
        decimal weightTolerance,
        decimal notionalTolerance)
    {
        if (target is null)
        {
            return QubesReconciliationLineStatus.MissingTarget;
        }

        if (actual is null)
        {
            return QubesReconciliationLineStatus.MissingActual;
        }

        if (pnl?.Status is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark)
        {
            return QubesReconciliationLineStatus.MissingMark;
        }

        if (pnl?.Status == PnLComputationStatus.Inconclusive)
        {
            return QubesReconciliationLineStatus.InconclusiveSafe;
        }

        return Math.Abs(weightDifference) > weightTolerance || Math.Abs(notionalDifference) > notionalTolerance
            ? QubesReconciliationLineStatus.Drift
            : QubesReconciliationLineStatus.InSync;
    }

    private static QubesReconciliationLineStatus Classify(
        PortfolioPosition? theoretical,
        PortfolioPosition? actual,
        PnLComputationStatus? markStatus,
        decimal weightDifference,
        decimal notionalDifference,
        decimal pnlDifference,
        decimal weightTolerance,
        decimal notionalTolerance,
        decimal pnlTolerance)
    {
        if (theoretical is null)
        {
            return QubesReconciliationLineStatus.MissingTheoretical;
        }

        if (actual is null)
        {
            return QubesReconciliationLineStatus.MissingActual;
        }

        if (markStatus is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark)
        {
            return QubesReconciliationLineStatus.MissingMark;
        }

        if (markStatus == PnLComputationStatus.Inconclusive)
        {
            return QubesReconciliationLineStatus.InconclusiveSafe;
        }

        return Math.Abs(weightDifference) > weightTolerance ||
               Math.Abs(notionalDifference) > notionalTolerance ||
               Math.Abs(pnlDifference) > pnlTolerance
            ? QubesReconciliationLineStatus.Drift
            : QubesReconciliationLineStatus.InSync;
    }

    private static ReconciliationSeverity Severity(QubesReconciliationLineStatus status) => status switch
    {
        QubesReconciliationLineStatus.MissingActual => ReconciliationSeverity.Blocking,
        QubesReconciliationLineStatus.MissingTarget => ReconciliationSeverity.Warning,
        QubesReconciliationLineStatus.Drift => ReconciliationSeverity.Warning,
        QubesReconciliationLineStatus.MissingMark => ReconciliationSeverity.Warning,
        QubesReconciliationLineStatus.InconclusiveSafe => ReconciliationSeverity.Warning,
        _ => ReconciliationSeverity.Info
    };

    private static string Reason(QubesReconciliationLineStatus status) => status switch
    {
        QubesReconciliationLineStatus.MissingActual => "Target instrument has no actual fixture position.",
        QubesReconciliationLineStatus.MissingTarget => "Actual fixture position has no target.",
        QubesReconciliationLineStatus.Drift => "Actual fixture differs from theoretical target beyond tolerance.",
        QubesReconciliationLineStatus.MissingMark => "No-external mark fixture is missing or stale, so the row is safe-inconclusive for PnL.",
        QubesReconciliationLineStatus.InconclusiveSafe => "No-external fixture data is inconclusive and was not fabricated.",
        _ => "Actual fixture is within tolerance of theoretical target."
    };

    private static string Symbol(IReadOnlyDictionary<InstrumentId, string> symbolsByInstrument, InstrumentId instrumentId)
        => symbolsByInstrument.TryGetValue(instrumentId, out var symbol)
            ? symbol
            : instrumentId.Value.ToString("N");
}
