namespace QQ.Production.Intraday.Application;

public sealed record CanonicalMarketDataSourceCandidate(
    string CandidateId,
    string Classification,
    bool ContainsMarketPrices,
    bool ContainsWeights,
    bool InstrumentMetadataOnly,
    bool FixtureOrPrototypeOnly,
    bool SandboxOnly,
    bool ProductionReady,
    IReadOnlyList<string> Symbols);

public sealed record CanonicalMarketDataSourceSelection(
    string Classification,
    string? SelectedCandidateId,
    IReadOnlyList<string> Warnings);

public sealed class CanonicalMarketDataSourceSelector
{
    public CanonicalMarketDataSourceSelection Select(IReadOnlyList<CanonicalMarketDataSourceCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var eligible = candidates
            .Where(candidate => candidate.ContainsMarketPrices)
            .Where(candidate => !candidate.ContainsWeights)
            .Where(candidate => !candidate.InstrumentMetadataOnly)
            .Where(candidate => !candidate.FixtureOrPrototypeOnly)
            .Where(candidate => candidate.Symbols.Contains("AUDUSD", StringComparer.Ordinal)
                && candidate.Symbols.Contains("EURUSD", StringComparer.Ordinal)
                && candidate.Symbols.Contains("GBPUSD", StringComparer.Ordinal))
            .ToArray();

        var trueSource = eligible.FirstOrDefault(candidate => candidate.Classification == "TRUE_MARKETDATA_SOURCE_CANDIDATE" && !candidate.SandboxOnly);
        if (trueSource is not null)
        {
            return new CanonicalMarketDataSourceSelection("TRUE_GOLDEN_SOURCE_SELECTED", trueSource.CandidateId, []);
        }

        var sandboxSource = eligible.FirstOrDefault(candidate => candidate.Classification == "TRUE_MARKETDATA_SOURCE_CANDIDATE" && candidate.SandboxOnly);
        if (sandboxSource is not null)
        {
            return new CanonicalMarketDataSourceSelection(
                "SANDBOX_GOLDEN_SOURCE_SELECTED_WITH_WARNINGS",
                sandboxSource.CandidateId,
                ["Sandbox/research scope only; not accounting or production."]);
        }

        if (candidates.Any(candidate => candidate.FixtureOrPrototypeOnly))
        {
            return new CanonicalMarketDataSourceSelection("ONLY_FIXTURE_OR_PROTOTYPE_SOURCE_AVAILABLE", null, ["No non-fixture market-price source selected."]);
        }

        return new CanonicalMarketDataSourceSelection("NO_USABLE_GOLDEN_SOURCE_FOUND", null, ["No eligible market-price source covers AUDUSD/EURUSD/GBPUSD."]);
    }
}

public sealed record CanonicalMarketDataSnapshotContract(
    string? MarketDataSnapshotId,
    string SnapshotType,
    string SnapshotScope,
    bool SandboxOnly,
    bool NotProduction,
    bool NotAccounting,
    DateTimeOffset CanonicalTargetCloseUtc,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    IReadOnlyList<string> Symbols,
    bool ContainsMarketPrices,
    bool ContainsQuotes,
    bool ContainsMarks,
    IReadOnlyList<string> SourceArtifacts);

public sealed record CanonicalMarketDataSnapshotValidation(
    bool IsValid,
    IReadOnlyList<string> Issues);

public static class CanonicalMarketDataSnapshotValidator
{
    public static CanonicalMarketDataSnapshotValidation Validate(CanonicalMarketDataSnapshotContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(contract.MarketDataSnapshotId))
        {
            issues.Add("MarketDataSnapshotIdMissing");
        }

        if (!contract.SandboxOnly || !contract.NotProduction || !contract.NotAccounting)
        {
            issues.Add("BoundaryFlagsIncomplete");
        }

        if (!IsQuarterHour(contract.CanonicalTargetCloseUtc))
        {
            issues.Add("CanonicalTargetCloseNotQuarterHourUtc");
        }

        if (contract.WindowStartUtc >= contract.WindowEndUtc || contract.CanonicalTargetCloseUtc != contract.WindowEndUtc)
        {
            issues.Add("WindowDoesNotEndAtCanonicalClose");
        }

        foreach (var symbol in new[] { "AUDUSD", "EURUSD", "GBPUSD" })
        {
            if (!contract.Symbols.Contains(symbol, StringComparer.Ordinal))
            {
                issues.Add($"RequiredSymbolMissing:{symbol}");
            }
        }

        if (!contract.ContainsMarketPrices || !contract.ContainsQuotes)
        {
            issues.Add("MarketPriceQuoteEvidenceMissing");
        }

        if (contract.SourceArtifacts.Count == 0)
        {
            issues.Add("SourceArtifactsMissing");
        }

        return new CanonicalMarketDataSnapshotValidation(issues.Count == 0, issues);
    }

    private static bool IsQuarterHour(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero
            && value.Second == 0
            && value.Millisecond == 0
            && value.Minute is 0 or 15 or 30 or 45;
    }
}

public static class CanonicalMarketDataConsumerRules
{
    public static bool ConsumerBindingHasSnapshotId(string? marketDataSnapshotId)
    {
        return !string.IsNullOrWhiteSpace(marketDataSnapshotId);
    }

    public static string SizingPriceBasisStatus(bool containsMarketPrices, bool containsQuotes)
    {
        return containsMarketPrices && containsQuotes ? "READY_WITH_WARNINGS" : "BLOCKED_MISSING_PRICE";
    }

    public static string DbRoleStatus(bool hasSourceManifest)
    {
        return hasSourceManifest
            ? "DB_PROJECTION_LAYER_OVER_GOLDEN_SOURCE"
            : "DB_UNAVAILABLE_BUT_NOT_BLOCKING_GOLDEN_SOURCE";
    }
}
