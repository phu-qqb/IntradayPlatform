using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public sealed record OperationalSourceCodeInventoryItem(
    string SourceFile,
    int SourceLine,
    string SourceIdentity,
    string SourceExactCode,
    string Pattern,
    string FactKind,
    string CatalogDisposition,
    string? CatalogExactCode,
    string? ExclusionReason);

public static partial class OperationalStatusCodeScanner
{
    private static readonly string[] SourceRoots =
    [
        "src/QQ.Production.Intraday.Infrastructure.PostgreSql",
        "src/QQ.Production.Intraday.Infrastructure.Lmax",
        "src/QQ.Production.Intraday.Application",
        "tools/QQ.Production.Intraday.Tools.OperationalReporting",
        "tests/QQ.Production.Intraday.Tests.Unit"
    ];

    private static readonly string[] SignalTokens =
    [
        "FAILED", "FAILURE", "BLOCK", "BREAK", "ALERT", "NO_GO", "MISSING",
        "STALE", "UNKNOWN", "INCOMPLETE", "MISMATCH", "REJECTED", "VIOLATION",
        "CONFLICT", "GAP", "UNOBSERVABLE", "NOT_FLAT", "NOT_TERMINAL"
    ];

    private static readonly string[] DynamicFamilies =
    [
        "ARCH7B_DUPLICATE_EXEC_ID_CONFLICT",
        "ARCH7B_CONFLICTING_FIX_SEQUENCE",
        "ARCH7B_FIX_SEQUENCE_GAP"
    ];

    private static readonly HashSet<string> ExplicitExclusions = new(
    [
        "REPORTING_CSV_COLUMN_COUNT_MISMATCH",
        "REPORTING_OUTPUT_DIRECTORY_NOT_EMPTY",
        "REPORTING_OPERATIONAL_CALENDAR_IMPOSSIBLE",
        "REPORTING_AS_OF_NOT_UTC",
        "REPORTING_DATABASE_VALUE_MISSING",
        "REPORTING_ECONOMIC_PROJECTION_JSON_INVALID",
        "REPORTING_ID_LIST_JSON_INVALID",
        "REPORTING_REPOSITORY_COMMIT_INVALID",
        "REPORTING_INCLUDE_HISTORY_OUT_OF_RANGE",
        "REPORTING_TRANSACTION_NOT_READ_ONLY",
        "BLOCKED_MISSING_SOURCE",
        "BLOCKED_AUTHORITY_UNPROVEN",
        "RPT2_SECURITY_MAPPING_MISSING",
        "PROVEN_WITH_EXPLICIT_AUTHORITY_GAPS",
        "RPT2_ROADMAP_MANIFEST_MISSING",
        "RPT2_SOURCE_SNAPSHOT_SHA_MISMATCH",
        "RPT2_DRIFT_MODEL_RUN_LINEAGE_MISSING"
    ], StringComparer.Ordinal);

    public static IReadOnlyList<OperationalSourceCodeInventoryItem> ScanAuthoritativeSource(
        string? repositoryRoot = null)
    {
        var root = repositoryRoot is null
            ? FindRepositoryRoot()
            : Path.GetFullPath(repositoryRoot);
        var catalog = OperationalStatusCodeCatalog.All
            .ToDictionary(value => value.ExactCode, StringComparer.Ordinal);
        var baselinePath = Path.Combine(root, "tools",
            "QQ.Production.Intraday.Tools.OperationalReporting",
            "operational-source-code-baseline.txt");
        var baseline = File.ReadAllLines(baselinePath).ToHashSet(StringComparer.Ordinal);
        var result = new List<OperationalSourceCodeInventoryItem>();
        foreach (var relativeRoot in SourceRoots)
        {
            var directory = Path.Combine(root, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*.cs",
                         SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (!IsAuthoritativeFile(relative)) continue;
                var lines = File.ReadAllLines(path);
                for (var index = 0; index < lines.Length; index++)
                {
                    foreach (Match match in QuotedCode().Matches(lines[index]))
                    {
                        var code = match.Groups["code"].Value;
                        if (!SignalTokens.Any(code.Contains)) continue;
                        var exact = catalog.ContainsKey(code);
                        var family = DynamicFamilies.FirstOrDefault(prefix =>
                            code.StartsWith(prefix, StringComparison.Ordinal));
                        var excluded = ExplicitExclusions.Contains(code);
                        var knownGeneric = baseline.Contains(code);
                        var disposition = exact ? "CATALOG_EXACT" :
                            family is not null ? "CATALOG_DYNAMIC_FAMILY" :
                            excluded ? "EXCLUDED" :
                            knownGeneric ? "CATALOG_DYNAMIC_FAMILY" : "UNCLASSIFIED";
                        result.Add(new(
                            relative,
                            index + 1,
                            $"{relative}:{index + 1}",
                            code,
                            match.Value,
                            InferFactKind(code),
                            disposition,
                            exact ? code : family ?? (knownGeneric
                                ? "REPORTING_UNCATALOGUED_SOURCE_CODE" : null),
                            excluded
                                ? "Reporting implementation guard; not a persisted operational source fact."
                                : null));
                    }
                }
            }
        }
        return result
            .DistinctBy(value => (value.SourceFile, value.SourceLine, value.SourceExactCode))
            .OrderBy(value => value.SourceFile, StringComparer.Ordinal)
            .ThenBy(value => value.SourceLine)
            .ThenBy(value => value.SourceExactCode, StringComparer.Ordinal)
            .ToArray();
    }

    public static void RequireComplete(
        IReadOnlyList<OperationalSourceCodeInventoryItem> inventory)
    {
        var missing = inventory.Where(value => value.CatalogDisposition == "UNCLASSIFIED")
            .Select(value => $"{value.SourceIdentity}:{value.SourceExactCode}")
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                $"REPORTING_SOURCE_CODE_INVENTORY_INCOMPLETE:{string.Join(',', missing)}");
    }

    private static bool IsAuthoritativeFile(string relative)
    {
        if (!relative.StartsWith("tests/", StringComparison.Ordinal)) return true;
        var name = Path.GetFileName(relative);
        return name.StartsWith("Arch6f", StringComparison.Ordinal) ||
               name.StartsWith("Arch7a", StringComparison.Ordinal) ||
               name.StartsWith("Arch7b", StringComparison.Ordinal) ||
               name.StartsWith("AnubisInfx", StringComparison.Ordinal);
    }

    private static string InferFactKind(string code)
    {
        if (code.Contains("RECONCILIATION", StringComparison.Ordinal) ||
            code.Contains("NOT_FLAT", StringComparison.Ordinal))
            return OperationalFactKinds.ReconciliationBreak;
        if (code.Contains("RISK", StringComparison.Ordinal))
            return OperationalFactKinds.RiskBlockingBreak;
        if (code.Contains("SLOT", StringComparison.Ordinal) ||
            code.Contains("CAPTURE", StringComparison.Ordinal))
            return OperationalFactKinds.SlotFailureCode;
        if (code.Contains("ALERT", StringComparison.Ordinal))
            return OperationalFactKinds.OperationalAlert;
        return OperationalFactKinds.StatusCode;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("REPORTING_REPOSITORY_ROOT_NOT_FOUND");
    }

    [GeneratedRegex("\"(?<code>[A-Z][A-Z0-9_:-]{4,})\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex QuotedCode();
}
