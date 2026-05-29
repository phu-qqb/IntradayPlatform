using System.Globalization;

namespace QQ.Production.Intraday.Application;

public enum SandboxQubesSnapshotType
{
    LocalSandboxMarketDataSnapshot,
    LocalSandboxSignalSnapshot,
    LocalSandboxRiskInputSnapshot,
    LocalFixtureInputSnapshot,
    PrototypeDeterministicInputSnapshot,
    ExternalQubesInputContractOnly,
}

public sealed record SandboxQubesInputSignal(
    string Symbol,
    decimal SignalWeight);

public sealed record SandboxQubesInputSnapshot(
    string SnapshotId,
    SandboxQubesSnapshotType SnapshotType,
    bool SandboxOnly,
    bool NotProduction,
    DateTimeOffset CanonicalTargetCloseUtc,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<SandboxQubesInputSignal> Signals,
    string SourceType,
    string SourceArtifactPath,
    string SourceArtifactHash,
    bool ContainsMarketPrices,
    bool ContainsReturns,
    bool ContainsSignals,
    bool ContainsRiskInputs,
    bool ContainsCovariance,
    bool ContainsWeights,
    string? MarketDataSnapshotId,
    string MarketDataSnapshotStatus,
    string FixtureOrPrototypeStatus,
    string DirectCrossPolicy,
    string ExecutionUniversePolicy);

public sealed record SandboxQubesPrototypeRunRequest(
    SandboxQubesInputSnapshot Snapshot,
    string RunnerVersion);

public sealed record SandboxQubesOutputWeight(
    string Symbol,
    decimal Weight);

public sealed record SandboxQubesOutput(
    string SandboxQubesRunId,
    string QubesOutputId,
    string InputSnapshotId,
    string? MarketDataSnapshotId,
    DateTimeOffset CanonicalTargetCloseUtc,
    IReadOnlyList<SandboxQubesOutputWeight> Weights,
    string WeightUnits,
    bool DirectCrossesPresent,
    string DirectCrossPolicy,
    string RunnerType,
    bool SandboxOnly,
    bool NotProduction,
    bool NotAccounting,
    bool NotExecuted,
    bool NotLedgerCommit);

public sealed record SandboxQubesExecutionTransformLine(
    string SourceSymbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    string Side,
    decimal SourceWeight,
    bool RequiresInversion,
    int? SecurityId,
    string? SecurityIdSource);

public sealed record SandboxQubesExecutionTransformResult(
    string Classification,
    bool DirectCrossesPresent,
    bool DirectCrossExecutionLeakageFound,
    IReadOnlyList<string> DirectCrossSymbolsExcluded,
    IReadOnlyList<SandboxQubesExecutionTransformLine> ExecutionLines);

public sealed record SandboxQubesPmsIntentLine(
    string SourceRebalanceIntentId,
    string Symbol,
    string Side,
    decimal? Quantity,
    string QuantityStatus);

public sealed record SandboxQubesPmsIntentCandidate(
    string NewPmsCycleId,
    DateTimeOffset CanonicalTargetCloseUtc,
    string SandboxAccountProfile,
    string SandboxQubesRunId,
    string QubesOutputId,
    string InputSnapshotId,
    string? MarketDataSnapshotId,
    string ExecutionUniversePolicy,
    string DirectCrossPolicy,
    IReadOnlyList<SandboxQubesPmsIntentLine> Lines,
    string SizingPolicyStatus,
    string CandidateStatus,
    bool ExecutionReady,
    bool SandboxOnly,
    bool NotProduction,
    bool NotAccounting,
    bool NotExecuted,
    bool NotLedgerCommit);

public sealed class SandboxQubesPrototypeRunner
{
    public const string RunnerType = "SandboxQubesPrototype";

    public SandboxQubesOutput Run(SandboxQubesPrototypeRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Snapshot);

        var snapshot = request.Snapshot;
        ValidateSnapshot(snapshot);

        var closeStamp = snapshot.CanonicalTargetCloseUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var orderedWeights = snapshot.Signals
            .OrderBy(signal => signal.Symbol, StringComparer.Ordinal)
            .Select(signal => new SandboxQubesOutputWeight(signal.Symbol, signal.SignalWeight))
            .ToArray();

        return new SandboxQubesOutput(
            SandboxQubesRunId: $"sandbox-qubes-prototype-r005-{closeStamp}-001",
            QubesOutputId: $"qubes-operationalization-r005:prototype-output:{closeStamp}:001",
            InputSnapshotId: snapshot.SnapshotId,
            MarketDataSnapshotId: snapshot.MarketDataSnapshotId,
            CanonicalTargetCloseUtc: snapshot.CanonicalTargetCloseUtc,
            Weights: orderedWeights,
            WeightUnits: "PrototypeSignalWeight",
            DirectCrossesPresent: orderedWeights.Any(weight => SandboxQubesExecutionUniverseTransformer.IsDirectCross(weight.Symbol)),
            DirectCrossPolicy: snapshot.DirectCrossPolicy,
            RunnerType: RunnerType,
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true);
    }

    private static void ValidateSnapshot(SandboxQubesInputSnapshot snapshot)
    {
        if (!snapshot.SandboxOnly || !snapshot.NotProduction)
        {
            throw new InvalidOperationException("Sandbox Qubes prototype input must be sandbox-only and not production.");
        }

        if (snapshot.SnapshotType != SandboxQubesSnapshotType.PrototypeDeterministicInputSnapshot)
        {
            throw new InvalidOperationException("Sandbox Qubes prototype runner only accepts deterministic prototype input snapshots.");
        }

        if (!IsCanonicalQuarterHourClose(snapshot.CanonicalTargetCloseUtc))
        {
            throw new InvalidOperationException("Canonical target close must be a UTC quarter-hour close.");
        }

        if (!snapshot.ContainsSignals || snapshot.Signals.Count == 0)
        {
            throw new InvalidOperationException("Sandbox Qubes prototype input requires deterministic local signals.");
        }
    }

    private static bool IsCanonicalQuarterHourClose(DateTimeOffset closeUtc)
    {
        return closeUtc.Offset == TimeSpan.Zero
            && closeUtc.Second == 0
            && closeUtc.Millisecond == 0
            && closeUtc.Microsecond == 0
            && closeUtc.Nanosecond == 0
            && closeUtc.Minute is 0 or 15 or 30 or 45;
    }
}

public sealed class SandboxQubesExecutionUniverseTransformer
{
    private static readonly IReadOnlyDictionary<string, ExecutionSymbolMapping> ExecutionMappings =
        new Dictionary<string, ExecutionSymbolMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = new("EURUSD", "EURUSD", false, null, null),
            ["AUDUSD"] = new("AUDUSD", "AUDUSD", false, null, null),
            ["GBPUSD"] = new("GBPUSD", "GBPUSD", false, null, null),
            ["NZDUSD"] = new("NZDUSD", "NZDUSD", false, null, null),
            ["USDCAD"] = new("USDCAD", "USDCAD", false, null, null),
            ["USDCHF"] = new("USDCHF", "USDCHF", false, null, null),
            ["JPYUSD"] = new("USDJPY", "JPYUSD", true, 4004, "8"),
        };

    public SandboxQubesExecutionTransformResult Transform(SandboxQubesOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var excludedDirectCrosses = new List<string>();
        var executionLines = new List<SandboxQubesExecutionTransformLine>();

        foreach (var weight in output.Weights)
        {
            if (ExecutionMappings.TryGetValue(weight.Symbol, out var mapping))
            {
                executionLines.Add(new SandboxQubesExecutionTransformLine(
                    SourceSymbol: weight.Symbol,
                    ExecutionTradableSymbol: mapping.ExecutionTradableSymbol,
                    NormalizedPortfolioSymbol: mapping.NormalizedPortfolioSymbol,
                    Side: ToSide(weight.Weight),
                    SourceWeight: weight.Weight,
                    RequiresInversion: mapping.RequiresInversion,
                    SecurityId: mapping.SecurityId,
                    SecurityIdSource: mapping.SecurityIdSource));
                continue;
            }

            if (IsDirectCross(weight.Symbol))
            {
                excludedDirectCrosses.Add(weight.Symbol);
            }
        }

        return new SandboxQubesExecutionTransformResult(
            Classification: "DIRECT_CROSS_POLICY_PRESERVED_BUT_SIZING_MISSING",
            DirectCrossesPresent: excludedDirectCrosses.Count > 0,
            DirectCrossExecutionLeakageFound: false,
            DirectCrossSymbolsExcluded: excludedDirectCrosses.Order(StringComparer.Ordinal).ToArray(),
            ExecutionLines: executionLines
                .OrderBy(line => line.ExecutionTradableSymbol, StringComparer.Ordinal)
                .ToArray());
    }

    public static bool IsDirectCross(string symbol)
    {
        return symbol.Length == 6
            && symbol.All(char.IsAsciiLetter)
            && !ExecutionMappings.ContainsKey(symbol);
    }

    private static string ToSide(decimal weight)
    {
        if (weight > 0m)
        {
            return "BUY";
        }

        if (weight < 0m)
        {
            return "SELL";
        }

        return "NONE";
    }

    private sealed record ExecutionSymbolMapping(
        string ExecutionTradableSymbol,
        string NormalizedPortfolioSymbol,
        bool RequiresInversion,
        int? SecurityId,
        string? SecurityIdSource);
}

public sealed class SandboxQubesPmsIntentCandidateFactory
{
    public SandboxQubesPmsIntentCandidate CreatePreviewOnlyCandidate(
        SandboxQubesOutput output,
        SandboxQubesExecutionTransformResult transform,
        string newPmsCycleId,
        string sandboxAccountProfile,
        string sizingPolicyStatus)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(transform);
        QubesProductionBoundaryGuard.EnsureSandboxPrototypeBlockedForProductionAccounting(output);

        var lines = transform.ExecutionLines
            .Where(line => line.Side is "BUY" or "SELL")
            .Select(line => new SandboxQubesPmsIntentLine(
                SourceRebalanceIntentId: $"{newPmsCycleId}:{line.ExecutionTradableSymbol}:preview-intent",
                Symbol: line.ExecutionTradableSymbol,
                Side: line.Side,
                Quantity: null,
                QuantityStatus: "MissingExplicitSandboxTargetNotional"))
            .OrderBy(line => line.Symbol, StringComparer.Ordinal)
            .ToArray();

        return new SandboxQubesPmsIntentCandidate(
            NewPmsCycleId: newPmsCycleId,
            CanonicalTargetCloseUtc: output.CanonicalTargetCloseUtc,
            SandboxAccountProfile: sandboxAccountProfile,
            SandboxQubesRunId: output.SandboxQubesRunId,
            QubesOutputId: output.QubesOutputId,
            InputSnapshotId: output.InputSnapshotId,
            MarketDataSnapshotId: output.MarketDataSnapshotId,
            ExecutionUniversePolicy: "USDPairOnlyExecutionUniverse",
            DirectCrossPolicy: "DirectCrossSignalOnlyNettingFirstExecutionDisabled",
            Lines: lines,
            SizingPolicyStatus: sizingPolicyStatus,
            CandidateStatus: "PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITIES_MISSING",
            ExecutionReady: false,
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true);
    }
}
