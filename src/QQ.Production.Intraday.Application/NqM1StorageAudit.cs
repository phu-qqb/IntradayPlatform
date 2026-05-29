using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record NqM1StorageLocation(
    string LocationType,
    string Location,
    string Status,
    long? RowCount,
    string? FirstTimestamp,
    string? LastTimestamp,
    IReadOnlyList<string> Contracts,
    string M1Confidence,
    string M1Evidence,
    string? SampleHash,
    IReadOnlyList<string> Warnings);

public sealed record NqM1DbTableReport(
    string Database,
    string TableName,
    long? RowCount,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> TimestampColumns,
    IReadOnlyList<string> SymbolColumns,
    IReadOnlyList<string> TimeframeColumns,
    IReadOnlyList<string> OhlcColumns,
    IReadOnlyList<string> VolumeColumns,
    string? FirstTimestamp,
    string? LastTimestamp,
    IReadOnlyList<NqM1SymbolSample> SymbolSamples,
    string M1Confidence,
    string M1Evidence,
    string Status,
    IReadOnlyList<string> Reasons);

public sealed record NqM1DbDiscoveryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> DatabasesScanned,
    IReadOnlyList<NqM1DbTableReport> Tables);

public sealed record NqM1FileReport(
    string Path,
    long SizeBytes,
    string Format,
    IReadOnlyList<string> Columns,
    string? InferredSymbolOrContract,
    string? InferredTimeframe,
    long? RowCount,
    string? FirstTimestamp,
    string? LastTimestamp,
    string M1Confidence,
    string M1Evidence,
    string Status,
    string? SampleHash,
    IReadOnlyList<string> Warnings);

public sealed record NqM1FileDiscoveryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string RepoRoot,
    IReadOnlyList<string> DirectoriesScanned,
    IReadOnlyList<NqM1FileReport> Files);

public sealed record NqM1ConfigDiscovery(
    IReadOnlyList<string> DbConfigured,
    IReadOnlyList<string> DbAlternativesDetected,
    IReadOnlyList<string> DataDirectoriesDetected,
    IReadOnlyList<string> ArtifactPathsDetected,
    IReadOnlyList<string> CandidateFilesMentioned,
    IReadOnlyList<string> CandidateTablesMentioned);

public sealed record NqM1StorageInventoryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    NqM1ConfigDiscovery ConfigDiscovery,
    IReadOnlyList<NqM1StorageLocation> Locations,
    string NqM1DataExists,
    int NqM1DataLocationCount,
    string? NqM1CanonicalLocation,
    string NqM1DuplicateStorageStatus,
    string NqM1Exportable,
    string NqTicksAvailable,
    bool M1IsNotTickLevel,
    string SafeToExportM1ForYannik,
    IReadOnlyList<string> Warnings);

public sealed record NqM1DuplicateStorageReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string DuplicateStorageStatus,
    string? CanonicalLocation,
    IReadOnlyList<NqM1DuplicateCandidate> Candidates,
    IReadOnlyList<string> Recommendations);

public sealed record NqM1DuplicateCandidate(
    string Left,
    string Right,
    string Assessment,
    string Reason);

public sealed record NqM1SymbolSample(
    string Column,
    string Value,
    long? RowCount,
    IReadOnlyList<string> ResolvedSymbols,
    string Classification,
    bool IsContinuous,
    string Reason);

public sealed record NqM1SymbolClassification(string Kind, bool IsContinuous, string Reason);

public static class NqM1SymbolClassifier
{
    private static readonly Regex NqContract = new(@"^(?:[A-Z_]+:)?[/@]?NQ[FGHJKMNQUVXZ](?:\d{2}|\d{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MnqContract = new(@"^(?:[A-Z_]+:)?[/@]?MNQ[FGHJKMNQUVXZ](?:\d{2}|\d{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NqContinuous = new(@"^(?:[A-Z_]+:)?[/@]?NQ(?:1!?|_CONT|\.c(?:\.0)?|=F)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MnqContinuous = new(@"^(?:[A-Z_]+:)?[/@]?MNQ(?:1!?|_CONT|\.c(?:\.0)?|=F)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static NqM1SymbolClassification Classify(string? raw, IReadOnlyList<string>? resolvedSymbols = null)
    {
        if (resolvedSymbols is { Count: > 0 })
        {
            foreach (var resolved in resolvedSymbols)
            {
                var resolvedClassification = Classify(resolved);
                if (resolvedClassification.Kind is "NQ" or "MNQ")
                {
                    return resolvedClassification with { Reason = $"resolved symbol {resolved}: {resolvedClassification.Reason}" };
                }
            }
        }

        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0) return new NqM1SymbolClassification("UNKNOWN", false, "empty value");
        if (MnqContract.IsMatch(value)) return new NqM1SymbolClassification("MNQ", false, "MNQ futures contract");
        if (MnqContinuous.IsMatch(value)) return new NqM1SymbolClassification("MNQ", IsContinuous(value), "MNQ root or continuous format");
        if (NqContract.IsMatch(value)) return new NqM1SymbolClassification("NQ", false, "NQ futures contract");
        if (NqContinuous.IsMatch(value)) return new NqM1SymbolClassification("NQ", IsContinuous(value), "NQ root or continuous format");
        if (value.Contains("NASDAQ", StringComparison.OrdinalIgnoreCase)) return new NqM1SymbolClassification("AMBIGUOUS_NASDAQ", false, "Nasdaq-like but not an NQ futures format");
        return value.Contains("NQ", StringComparison.OrdinalIgnoreCase)
            ? new NqM1SymbolClassification("NEAR_NQ", false, "contains NQ but did not match explicit futures formats")
            : new NqM1SymbolClassification("OTHER", false, "not NQ-like");
    }

    private static bool IsContinuous(string value)
        => value.Contains('1') || value.Contains("_CONT", StringComparison.OrdinalIgnoreCase) || value.Contains(".c", StringComparison.OrdinalIgnoreCase) || value.Contains("=F", StringComparison.OrdinalIgnoreCase);
}

public static class NqM1StorageAuditService
{
    public static NqM1StorageInventoryReport BuildInventory(
        string runKey,
        NqM1ConfigDiscovery config,
        NqM1DbDiscoveryReport dbReport,
        NqM1FileDiscoveryReport fileReport,
        string nqTicksAvailable)
    {
        var locations = dbReport.Tables
            .Where(IsNqM1)
            .Select(ToLocation)
            .Concat(fileReport.Files.Where(IsNqM1).Select(ToLocation))
            .OrderBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var duplicate = BuildDuplicateReport(runKey, locations);
        var warnings = new List<string>();
        if (locations.Any(x => x.Contracts.Any(c => NqM1SymbolClassifier.Classify(c).Kind == "MNQ")))
        {
            warnings.Add("MNQ-like values were detected but are not counted as NQ M1 locations.");
        }

        var exportable = locations.Length == 0 ? "NO" :
            duplicate.DuplicateStorageStatus is "NONE" or "UNKNOWN" ? "YES" : "UNKNOWN";
        var safe = locations.Length == 0 ? "NO" :
            duplicate.DuplicateStorageStatus == "NONE" ? "YES" : "UNKNOWN";

        return new NqM1StorageInventoryReport(
            runKey,
            DateTimeOffset.UtcNow,
            config,
            locations,
            locations.Length > 0 ? "YES" : "NO",
            locations.Length,
            ChooseCanonicalLocation(locations),
            duplicate.DuplicateStorageStatus,
            exportable,
            nqTicksAvailable,
            true,
            safe,
            warnings);
    }

    public static NqM1DuplicateStorageReport BuildDuplicateReport(string runKey, IReadOnlyList<NqM1StorageLocation> locations)
    {
        var candidates = new List<NqM1DuplicateCandidate>();
        for (var leftIndex = 0; leftIndex < locations.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < locations.Count; rightIndex++)
            {
                var left = locations[leftIndex];
                var right = locations[rightIndex];
                candidates.Add(Compare(left, right));
            }
        }

        var status = candidates.Any(x => x.Assessment == "CONFIRMED") ? "CONFIRMED" :
            candidates.Any(x => x.Assessment == "LIKELY") ? "LIKELY" :
            candidates.Any(x => x.Assessment == "POSSIBLE") ? "POSSIBLE" :
            locations.Count <= 1 ? "NONE" : "UNKNOWN";
        var recommendations = new List<string>();
        if (locations.Count == 0) recommendations.Add("No NQ M1 source was found; do not export M1 for Yannik yet.");
        else if (status == "NONE") recommendations.Add("Use the single clear NQ M1 source as the canonical source for a later NqM1BarExport.");
        else recommendations.Add("Choose and document one canonical NQ M1 source before exporting; duplicate storage can cause accidental mixing.");

        return new NqM1DuplicateStorageReport(runKey, DateTimeOffset.UtcNow, status, ChooseCanonicalLocation(locations), candidates, recommendations);
    }

    public static string HashSample(IEnumerable<string> lines)
    {
        var text = string.Join("\n", lines.Take(32));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    public static (string Confidence, string Evidence) InferM1(string? name, IReadOnlyDictionary<string, string?> metadata, IReadOnlyList<DateTimeOffset> orderedTimestamps)
    {
        if (metadata.Values.Any(v => v is not null && IsM1Value(v)))
        {
            return ("HIGH", "TIMEFRAME_COLUMN");
        }

        if (!string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name, @"(^|[^A-Z0-9])(M1|1M|1MIN|1_MIN|1-MIN|MINUTE)([^A-Z0-9]|$)", RegexOptions.IgnoreCase))
        {
            return ("MEDIUM", "FILE_NAME");
        }

        if (orderedTimestamps.Count >= 4)
        {
            var deltas = orderedTimestamps.OrderBy(x => x).Zip(orderedTimestamps.OrderBy(x => x).Skip(1), (a, b) => (b - a).TotalSeconds).ToArray();
            var minuteDeltas = deltas.Count(x => Math.Abs(x - 60d) < 0.001d);
            if (minuteDeltas >= Math.Max(3, deltas.Length * 6 / 10))
            {
                return ("MEDIUM", "TIMESTAMP_DELTA");
            }
        }

        return ("NONE", "UNKNOWN");
    }

    public static string DetermineTableStatus(bool hasNq, bool hasMnq, bool isM1, bool hasOhlc, bool isEmpty, bool isTickLike)
    {
        if (isEmpty) return "BAR_TABLE_EMPTY";
        if (isTickLike && !hasOhlc) return "TICK_TABLE_NOT_M1";
        if (!hasOhlc) return "UNSUPPORTED_SCHEMA";
        if (hasNq && isM1) return "M1_NQ_CANDIDATE";
        if (hasNq && !isM1) return "NQ_NOT_M1";
        if (hasMnq) return "M1_NON_NQ";
        return "M1_NON_NQ";
    }

    private static bool IsM1Value(string value)
        => value.Equals("M1", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("1m", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("1 min", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("1 minute", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("OneMinute", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("0", StringComparison.OrdinalIgnoreCase);

    private static bool IsNqM1(NqM1DbTableReport table)
        => table.Status == "M1_NQ_CANDIDATE";

    private static bool IsNqM1(NqM1FileReport file)
        => file.Status == "NQ_M1_FILE_FOUND";

    private static NqM1StorageLocation ToLocation(NqM1DbTableReport table)
        => new("DB", $"{table.Database}:{table.TableName}", table.Status, table.RowCount, table.FirstTimestamp, table.LastTimestamp, ExtractNqContracts(table.SymbolSamples), table.M1Confidence, table.M1Evidence, BuildDbSampleHash(table), table.Reasons);

    private static NqM1StorageLocation ToLocation(NqM1FileReport file)
        => new("FILE", file.Path, file.Status, file.RowCount, file.FirstTimestamp, file.LastTimestamp, string.IsNullOrWhiteSpace(file.InferredSymbolOrContract) ? [] : [file.InferredSymbolOrContract], file.M1Confidence, file.M1Evidence, file.SampleHash, file.Warnings);

    private static string BuildDbSampleHash(NqM1DbTableReport table)
        => HashSample([table.Database, table.TableName, table.RowCount?.ToString() ?? "", table.FirstTimestamp ?? "", table.LastTimestamp ?? "", string.Join(",", ExtractNqContracts(table.SymbolSamples))]);

    private static IReadOnlyList<string> ExtractNqContracts(IEnumerable<NqM1SymbolSample> samples)
        => samples.Where(x => x.Classification == "NQ").SelectMany(x => x.ResolvedSymbols.Count > 0 ? x.ResolvedSymbols : [x.Value]).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    private static string? ChooseCanonicalLocation(IReadOnlyList<NqM1StorageLocation> locations)
        => locations.OrderByDescending(x => x.LocationType == "DB").ThenByDescending(x => x.RowCount ?? 0).ThenBy(x => x.Location, StringComparer.OrdinalIgnoreCase).FirstOrDefault()?.Location;

    private static NqM1DuplicateCandidate Compare(NqM1StorageLocation left, NqM1StorageLocation right)
    {
        var sameContracts = left.Contracts.Intersect(right.Contracts, StringComparer.OrdinalIgnoreCase).Any();
        var samePeriod = string.Equals(left.FirstTimestamp, right.FirstTimestamp, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(left.LastTimestamp, right.LastTimestamp, StringComparison.OrdinalIgnoreCase);
        var sameRows = left.RowCount is not null && left.RowCount == right.RowCount;
        var sameHash = !string.IsNullOrWhiteSpace(left.SampleHash) && left.SampleHash == right.SampleHash;
        if (sameContracts && samePeriod && sameRows && sameHash) return new(left.Location, right.Location, "CONFIRMED", "same contract, period, row count, and sample hash");
        if (sameContracts && samePeriod && sameRows) return new(left.Location, right.Location, "LIKELY", "same contract, period, and row count");
        if (sameContracts && samePeriod) return new(left.Location, right.Location, "POSSIBLE", "same contract and period with differing row count or sample hash");
        return new(left.Location, right.Location, "NONE", "different contracts, periods, or counts");
    }
}
