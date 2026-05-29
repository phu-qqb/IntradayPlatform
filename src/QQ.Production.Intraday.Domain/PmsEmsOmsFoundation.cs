using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

public readonly record struct QubesRunId(string Value)
{
    public override string ToString() => Value;
}

public enum TargetWeightsCadence
{
    FifteenMinutes
}

public enum TargetWeightsValidationStatus
{
    Unknown,
    Accepted,
    Warning,
    Rejected
}

public sealed record TargetWeightsBatch(
    QubesRunId QubesRunId,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveFromUtc,
    TargetWeightsCadence Cadence,
    IReadOnlyList<TargetWeight> Weights,
    string Source,
    int Version,
    TargetWeightsValidationStatus ValidationStatus)
{
    public int CadenceMinutes => Cadence == TargetWeightsCadence.FifteenMinutes ? 15 : 0;
}

public sealed record TargetWeight(
    InstrumentId InstrumentId,
    decimal Weight,
    string Source,
    int Version,
    TargetWeightsValidationStatus ValidationStatus);

public sealed record TargetWeightsValidationResult(
    TargetWeightsValidationStatus Status,
    IReadOnlyList<string> Issues,
    decimal GrossWeight,
    bool IsAccepted)
{
    public static TargetWeightsValidationResult Accepted(decimal grossWeight)
        => new(TargetWeightsValidationStatus.Accepted, [], grossWeight, true);

    public static TargetWeightsValidationResult Rejected(IReadOnlyList<string> issues, decimal grossWeight)
        => new(TargetWeightsValidationStatus.Rejected, issues, grossWeight, false);
}

public sealed class TargetWeightsBatchValidator
{
    public TargetWeightsValidationResult Validate(TargetWeightsBatch batch, DateTimeOffset nowUtc, TimeSpan maxAge)
    {
        var issues = new List<string>();

        if (batch.ProducedAtUtc.Offset != TimeSpan.Zero || batch.EffectiveFromUtc.Offset != TimeSpan.Zero)
        {
            issues.Add("Target weight timestamps must be UTC.");
        }

        if (batch.Cadence != TargetWeightsCadence.FifteenMinutes)
        {
            issues.Add("Target weight cadence must be 15 minutes.");
        }

        if (nowUtc - batch.ProducedAtUtc > maxAge)
        {
            issues.Add("Target weights are stale.");
        }

        if (batch.Weights.Count == 0)
        {
            issues.Add("Target weights batch has no rows.");
        }

        if (batch.Weights.Select(x => x.InstrumentId).Distinct().Count() != batch.Weights.Count)
        {
            issues.Add("Target weights batch has duplicate instruments.");
        }

        if (batch.Weights.Any(x => x.ValidationStatus == TargetWeightsValidationStatus.Rejected))
        {
            issues.Add("Target weights batch contains rejected rows.");
        }

        var grossWeight = batch.Weights.Sum(x => Math.Abs(x.Weight));
        if (grossWeight > 1m)
        {
            issues.Add("Target weights gross exposure exceeds 1.0.");
        }

        return issues.Count == 0
            ? TargetWeightsValidationResult.Accepted(grossWeight)
            : TargetWeightsValidationResult.Rejected(issues, grossWeight);
    }
}

public enum ApprovedInstrumentAssetClass
{
    SpotFx
}

public enum ApprovedInstrumentValidationStatus
{
    LiveReadOnlyMarketDataSucceeded,
    PausedTlsBoundaryInconclusiveNotFailed,
    NotProvenNotFailed
}

public sealed record ApprovedInstrument(
    InstrumentId InstrumentId,
    string InternalInstrumentKey,
    string BrokerSymbol,
    ApprovedInstrumentAssetClass AssetClass,
    string? BrokerVenue,
    string? SecurityId,
    string? SecurityIdSource,
    ApprovedInstrumentValidationStatus ValidationStatus,
    string ScopeNote);

public static class PmsEmsOmsR001ApprovedUniverse
{
    public static IReadOnlyList<ApprovedInstrument> Create(
        InstrumentId gbpusd,
        InstrumentId eurgbp,
        InstrumentId audusd,
        InstrumentId usdjpy)
        =>
        [
            new(gbpusd, "GBPUSD", "GBPUSD", ApprovedInstrumentAssetClass.SpotFx, "LMAX", null, null, ApprovedInstrumentValidationStatus.LiveReadOnlyMarketDataSucceeded, "R203 live read-only MarketData succeeded."),
            new(eurgbp, "EURGBP", "EURGBP", ApprovedInstrumentAssetClass.SpotFx, "LMAX", null, null, ApprovedInstrumentValidationStatus.LiveReadOnlyMarketDataSucceeded, "R207 live read-only MarketData succeeded."),
            new(audusd, "AUDUSD", "AUDUSD", ApprovedInstrumentAssetClass.SpotFx, "LMAX", null, null, ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, "Paused as TLS-boundary inconclusive, not failed; not required for R001."),
            new(usdjpy, "USDJPY", "USDJPY", ApprovedInstrumentAssetClass.SpotFx, "LMAX", "4004", "8", ApprovedInstrumentValidationStatus.NotProvenNotFailed, "Not proven, not failed; caveat preserved with SecurityID=4004 and SecurityIDSource=8.")
        ];
}

public enum PortfolioStateSource
{
    Theoretical,
    Actual,
    BrokerReported,
    Simulated,
    Unknown
}

public sealed record CashComponent(
    string Currency,
    decimal Amount);

public sealed record PortfolioPosition(
    InstrumentId InstrumentId,
    decimal Quantity,
    decimal? AverageEntryMark,
    decimal NotionalExposure,
    decimal WeightExposure,
    IReadOnlyDictionary<string, decimal> CurrencyExposure);

public sealed record PortfolioSnapshot(
    DateTimeOffset SnapshotTimestampUtc,
    PortfolioStateSource StateSource,
    decimal PortfolioNotional,
    IReadOnlyList<PortfolioPosition> Positions,
    IReadOnlyList<CashComponent> CashComponents);

public sealed class TargetPortfolioSnapshotFactory
{
    public PortfolioSnapshot Create(TargetWeightsBatch batch, decimal portfolioNotional, DateTimeOffset snapshotTimestampUtc)
    {
        var positions = batch.Weights.Select(weight =>
            new PortfolioPosition(
                weight.InstrumentId,
                0m,
                null,
                portfolioNotional * weight.Weight,
                weight.Weight,
                new Dictionary<string, decimal> { ["USD"] = portfolioNotional * weight.Weight }))
            .ToArray();

        return new PortfolioSnapshot(
            snapshotTimestampUtc,
            PortfolioStateSource.Theoretical,
            portfolioNotional,
            positions,
            [new CashComponent("USD", portfolioNotional - positions.Sum(x => x.NotionalExposure))]);
    }
}

public enum MarketDataEvidenceCategory
{
    InternalFixtureSafe,
    SanitizedDerivedMark,
    Missing
}

public enum MarketDataStalenessCategory
{
    Fresh,
    Stale,
    Missing
}

public sealed record MarketDataMark(
    InstrumentId InstrumentId,
    DateTimeOffset MarkTimestampUtc,
    decimal? Mid,
    string Source,
    MarketDataEvidenceCategory EvidenceCategory,
    MarketDataStalenessCategory StalenessCategory);

public enum PnLSource
{
    Theoretical,
    Actual,
    Simulated
}

public enum PnLComputationStatus
{
    Computed,
    MissingMark,
    MissingPosition,
    StaleMark,
    Inconclusive
}

public sealed record InstrumentPnL(
    InstrumentId InstrumentId,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    PnLComputationStatus Status,
    string Reason);

public sealed record PortfolioPnL(
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal TotalPnL);

public sealed record PnLSnapshot(
    DateTimeOffset SnapshotTimestampUtc,
    PnLSource Source,
    PortfolioPnL PortfolioPnL,
    IReadOnlyList<InstrumentPnL> Instruments,
    PnLComputationStatus Status);

public sealed class PnLCalculator
{
    public PnLSnapshot Calculate(
        PortfolioSnapshot snapshot,
        IReadOnlyList<MarketDataMark> marks,
        DateTimeOffset nowUtc,
        TimeSpan maxMarkAge)
    {
        if (snapshot.Positions.Count == 0)
        {
            return new PnLSnapshot(
                nowUtc,
                ToPnLSource(snapshot.StateSource),
                new PortfolioPnL(0m, 0m, 0m),
                [],
                PnLComputationStatus.MissingPosition);
        }

        var marksByInstrument = marks.ToDictionary(x => x.InstrumentId);
        var rows = new List<InstrumentPnL>();

        foreach (var position in snapshot.Positions)
        {
            if (!marksByInstrument.TryGetValue(position.InstrumentId, out var mark) || mark.Mid is null)
            {
                rows.Add(new InstrumentPnL(position.InstrumentId, 0m, 0m, PnLComputationStatus.MissingMark, "Market data mark is missing."));
                continue;
            }

            if (mark.StalenessCategory == MarketDataStalenessCategory.Stale || nowUtc - mark.MarkTimestampUtc > maxMarkAge)
            {
                rows.Add(new InstrumentPnL(position.InstrumentId, 0m, 0m, PnLComputationStatus.StaleMark, "Market data mark is stale."));
                continue;
            }

            if (position.AverageEntryMark is null)
            {
                rows.Add(new InstrumentPnL(position.InstrumentId, 0m, 0m, PnLComputationStatus.Inconclusive, "Average entry mark is missing."));
                continue;
            }

            rows.Add(new InstrumentPnL(
                position.InstrumentId,
                position.Quantity * (mark.Mid.Value - position.AverageEntryMark.Value),
                0m,
                PnLComputationStatus.Computed,
                "PnL computed from internal mark and position."));
        }

        var totalUnrealized = rows.Sum(x => x.UnrealizedPnL);
        var status = rows.Any(x => x.Status == PnLComputationStatus.MissingMark)
            ? PnLComputationStatus.MissingMark
            : rows.Any(x => x.Status == PnLComputationStatus.StaleMark)
                ? PnLComputationStatus.StaleMark
                : rows.Any(x => x.Status == PnLComputationStatus.Inconclusive)
                    ? PnLComputationStatus.Inconclusive
                    : PnLComputationStatus.Computed;

        return new PnLSnapshot(
            nowUtc,
            ToPnLSource(snapshot.StateSource),
            new PortfolioPnL(totalUnrealized, 0m, totalUnrealized),
            rows,
            status);
    }

    private static PnLSource ToPnLSource(PortfolioStateSource source) => source switch
    {
        PortfolioStateSource.Actual => PnLSource.Actual,
        PortfolioStateSource.Theoretical => PnLSource.Theoretical,
        _ => PnLSource.Simulated
    };
}

public enum ReconciliationItemType
{
    TargetVsCurrentDifference,
    PositionMismatch,
    WeightMismatch,
    NotionalMismatch,
    MissingTarget,
    MissingPosition,
    MissingMarketData,
    StaleWeights,
    StaleMarks
}

public enum ReconciliationSeverity
{
    Info,
    Warning,
    Blocking
}

public sealed record ReconciliationItem(
    InstrumentId? InstrumentId,
    ReconciliationItemType ItemType,
    ReconciliationSeverity Severity,
    decimal? Difference,
    string Description);

public sealed record ReconciliationReport(
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ReconciliationItem> Items,
    bool HasBlockingItems);

public sealed class PortfolioReconciler
{
    public ReconciliationReport Reconcile(
        PortfolioSnapshot target,
        PortfolioSnapshot current,
        decimal weightTolerance,
        decimal notionalTolerance,
        DateTimeOffset createdAtUtc)
    {
        var items = new List<ReconciliationItem>();
        var targetByInstrument = target.Positions.ToDictionary(x => x.InstrumentId);
        var currentByInstrument = current.Positions.ToDictionary(x => x.InstrumentId);
        var instruments = targetByInstrument.Keys.Concat(currentByInstrument.Keys).Distinct();

        foreach (var instrumentId in instruments)
        {
            var hasTarget = targetByInstrument.TryGetValue(instrumentId, out var targetPosition);
            var hasCurrent = currentByInstrument.TryGetValue(instrumentId, out var currentPosition);

            if (!hasTarget)
            {
                items.Add(new ReconciliationItem(instrumentId, ReconciliationItemType.MissingTarget, ReconciliationSeverity.Warning, null, "Current position has no target."));
                continue;
            }

            if (!hasCurrent)
            {
                items.Add(new ReconciliationItem(instrumentId, ReconciliationItemType.MissingPosition, ReconciliationSeverity.Blocking, null, "Target position has no current position."));
                continue;
            }

            var weightDifference = currentPosition!.WeightExposure - targetPosition!.WeightExposure;
            if (Math.Abs(weightDifference) > weightTolerance)
            {
                items.Add(new ReconciliationItem(instrumentId, ReconciliationItemType.WeightMismatch, ReconciliationSeverity.Warning, weightDifference, "Current weight differs from target weight."));
            }

            var notionalDifference = currentPosition.NotionalExposure - targetPosition.NotionalExposure;
            if (Math.Abs(notionalDifference) > notionalTolerance)
            {
                items.Add(new ReconciliationItem(instrumentId, ReconciliationItemType.NotionalMismatch, ReconciliationSeverity.Warning, notionalDifference, "Current notional differs from target notional."));
            }
        }

        return new ReconciliationReport(createdAtUtc, items, items.Any(x => x.Severity == ReconciliationSeverity.Blocking));
    }
}

public enum TheoreticalVsRealStatus
{
    InSync,
    Drift,
    MissingActual,
    MissingTheoretical,
    Inconclusive
}

public sealed record TheoreticalVsRealDifference(
    InstrumentId InstrumentId,
    decimal WeightDifference,
    decimal NotionalDifference,
    decimal PnLDifference);

public sealed record TheoreticalVsRealReport(
    DateTimeOffset CreatedAtUtc,
    PortfolioSnapshot? TheoreticalPortfolioSnapshot,
    PortfolioSnapshot? ActualPortfolioSnapshot,
    IReadOnlyList<TheoreticalVsRealDifference> Differences,
    TheoreticalVsRealStatus Status);

public sealed class TheoreticalVsRealComparator
{
    public TheoreticalVsRealReport Compare(
        PortfolioSnapshot? theoretical,
        PortfolioSnapshot? actual,
        PnLSnapshot? theoreticalPnL,
        PnLSnapshot? actualPnL,
        decimal weightTolerance,
        decimal notionalTolerance,
        decimal pnlTolerance,
        DateTimeOffset createdAtUtc)
    {
        if (theoretical is null)
        {
            return new TheoreticalVsRealReport(createdAtUtc, null, actual, [], TheoreticalVsRealStatus.MissingTheoretical);
        }

        if (actual is null)
        {
            return new TheoreticalVsRealReport(createdAtUtc, theoretical, null, [], TheoreticalVsRealStatus.MissingActual);
        }

        var theoreticalByInstrument = theoretical.Positions.ToDictionary(x => x.InstrumentId);
        var actualByInstrument = actual.Positions.ToDictionary(x => x.InstrumentId);
        var theoreticalPnlByInstrument = theoreticalPnL?.Instruments.ToDictionary(x => x.InstrumentId) ?? [];
        var actualPnlByInstrument = actualPnL?.Instruments.ToDictionary(x => x.InstrumentId) ?? [];
        var instruments = theoreticalByInstrument.Keys.Concat(actualByInstrument.Keys).Distinct();
        var differences = new List<TheoreticalVsRealDifference>();

        foreach (var instrumentId in instruments)
        {
            theoreticalByInstrument.TryGetValue(instrumentId, out var theoreticalPosition);
            actualByInstrument.TryGetValue(instrumentId, out var actualPosition);
            theoreticalPnlByInstrument.TryGetValue(instrumentId, out var theoreticalPnlRow);
            actualPnlByInstrument.TryGetValue(instrumentId, out var actualPnlRow);

            differences.Add(new TheoreticalVsRealDifference(
                instrumentId,
                (actualPosition?.WeightExposure ?? 0m) - (theoreticalPosition?.WeightExposure ?? 0m),
                (actualPosition?.NotionalExposure ?? 0m) - (theoreticalPosition?.NotionalExposure ?? 0m),
                (actualPnlRow?.UnrealizedPnL ?? 0m) - (theoreticalPnlRow?.UnrealizedPnL ?? 0m)));
        }

        var status = differences.Any(x =>
            Math.Abs(x.WeightDifference) > weightTolerance ||
            Math.Abs(x.NotionalDifference) > notionalTolerance ||
            Math.Abs(x.PnLDifference) > pnlTolerance)
            ? TheoreticalVsRealStatus.Drift
            : TheoreticalVsRealStatus.InSync;

        return new TheoreticalVsRealReport(createdAtUtc, theoretical, actual, differences, status);
    }
}

public enum IntentSide
{
    Buy,
    Sell,
    None
}

public enum IntentStatus
{
    TheoreticalOnly,
    NotExecutable,
    BlockedNoOMS
}

public sealed record RebalanceIntentLine(
    InstrumentId InstrumentId,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal CurrentNotional,
    decimal TargetNotional,
    decimal DeltaNotional,
    IntentSide IntentSide);

public sealed record RebalanceIntent(
    DateTimeOffset CreatedAtUtc,
    IntentStatus IntentStatus,
    IReadOnlyList<RebalanceIntentLine> Lines)
{
    public bool IsExecutable => false;
}

public sealed class RebalanceIntentCalculator
{
    public RebalanceIntent Calculate(
        PortfolioSnapshot current,
        PortfolioSnapshot target,
        DateTimeOffset createdAtUtc,
        decimal notionalTolerance)
    {
        var currentByInstrument = current.Positions.ToDictionary(x => x.InstrumentId);
        var targetByInstrument = target.Positions.ToDictionary(x => x.InstrumentId);
        var instruments = currentByInstrument.Keys.Concat(targetByInstrument.Keys).Distinct();
        var lines = new List<RebalanceIntentLine>();

        foreach (var instrumentId in instruments)
        {
            currentByInstrument.TryGetValue(instrumentId, out var currentPosition);
            targetByInstrument.TryGetValue(instrumentId, out var targetPosition);

            var currentWeight = currentPosition?.WeightExposure ?? 0m;
            var targetWeight = targetPosition?.WeightExposure ?? 0m;
            var currentNotional = currentPosition?.NotionalExposure ?? 0m;
            var targetNotional = targetPosition?.NotionalExposure ?? 0m;
            var deltaNotional = targetNotional - currentNotional;
            var side = Math.Abs(deltaNotional) <= notionalTolerance
                ? IntentSide.None
                : deltaNotional > 0m
                    ? IntentSide.Buy
                    : IntentSide.Sell;

            lines.Add(new RebalanceIntentLine(
                instrumentId,
                currentWeight,
                targetWeight,
                targetWeight - currentWeight,
                currentNotional,
                targetNotional,
                deltaNotional,
                side));
        }

        return new RebalanceIntent(createdAtUtc, IntentStatus.NotExecutable, lines);
    }
}
