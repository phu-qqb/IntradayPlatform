using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public enum StratTakenGate { PASS, WARN, FAIL }
public enum StratTakenUnderpopulationConclusion { YES, NO, UNKNOWN }

public sealed record StratTakenStrategyRecord(int Ordinal, int Suid, bool SentinelMinus999Detected);

public sealed record StratTakenBinaryIntegrityReport(
    string RunKey,
    string? Path,
    bool Exists,
    long FileSizeBytes,
    int? StrategyCount,
    int RecordSizeBytes,
    int SuidOffsetBytes,
    long? ExpectedSizeBytes,
    long TrailingBytes,
    bool IsTruncated,
    string STRATTAKEN_BINARY_GATE,
    IReadOnlyList<string> Issues);

public sealed record StratTakenPopulationComparison(
    int? CurrentCount,
    int? FullCount,
    decimal? RetainedRatio,
    int? MissingSuidCount,
    decimal? SuidCoverageRatio);

public sealed record StratTakenPopulationReport(
    string RunKey,
    int StrategyCount,
    int DistinctSuidCount,
    int? MinSuid,
    int? MaxSuid,
    IReadOnlyDictionary<int, int> SuidDistribution,
    IReadOnlyList<KeyValuePair<int, int>> TopSuidByStrategyCount,
    StratTakenPopulationComparison? FullReferenceComparison,
    bool LooksLikeSmokeFixture,
    string STRATTAKEN_POPULATION_GATE,
    IReadOnlyList<string> Issues);

public sealed record StratTakenPackageShapeReport(
    string? PackageRoot,
    bool Exists,
    int? SubUniverseCount,
    int? InstrumentCount,
    int? VariableCount,
    int? TimeSeriesFileCount,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Issues);

public sealed record StratTakenCompatibilityReport(
    StratTakenPackageShapeReport PackageShape,
    int PresentStrategyCount,
    int SuidCompatibleStrategyCount,
    int SuidRejectedStrategyCount,
    int VariableAvailableStrategyCount,
    int VariableRejectedStrategyCount,
    int ShapeCompatibleStrategyCount,
    int ShapeRejectedStrategyCount,
    int SignalConditionEvaluableCount,
    int SignalConditionNotEvaluableCount,
    int SentinelMinus999ExposureCount,
    int FinalPotentiallyEligibleStrategyCount,
    string STRATTAKEN_PACKAGE_COMPATIBILITY_GATE,
    string STRATTAKEN_ATTRITION_GATE,
    string STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS,
    IReadOnlyList<string> Issues);

public sealed record StratTakenCodePathFinding(
    string File,
    int Line,
    string? MethodOrScope,
    string Pattern,
    string? ConfigKey,
    string? CapValue,
    string Snippet,
    string Impact);

public sealed record StratTakenCodePathReport(
    string RunKey,
    string RepositoryRoot,
    IReadOnlyList<string> SearchPatterns,
    IReadOnlyList<StratTakenCodePathFinding> Findings,
    string STRATTAKEN_CODE_PATH_GATE,
    IReadOnlyList<string> Issues);

public sealed record StratTakenAttritionReport(
    string RunKey,
    int ParsedStrategyCount,
    int SuidCompatibleStrategyCount,
    int SuidRejectedStrategyCount,
    int VariableAvailableStrategyCount,
    int VariableRejectedStrategyCount,
    int ShapeCompatibleStrategyCount,
    int ShapeRejectedStrategyCount,
    int SignalConditionEvaluableCount,
    int SignalConditionNotEvaluableCount,
    int SentinelMinus999ExposureCount,
    int FinalPotentiallyEligibleStrategyCount,
    string STRATTAKEN_ATTRITION_GATE,
    string STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS,
    IReadOnlyList<string> Issues);

public sealed record StratTakenPopulationAuditRequest(
    string RunKey,
    string? PackageRoot,
    string? StratTakenPath,
    string? FullStratTakenReferencePath,
    string RepositoryRoot,
    int SuidOffsetBytes = 0,
    bool NoExecution = true);

public sealed record StratTakenPopulationAuditPackage(
    StratTakenBinaryIntegrityReport BinaryIntegrity,
    StratTakenPopulationReport Population,
    StratTakenCompatibilityReport Compatibility,
    StratTakenAttritionReport Attrition,
    StratTakenCodePathReport CodePath,
    IReadOnlyList<string> SafetyAssertions);

public static class StratTakenPopulationAudit
{
    public const int StrategyRecordSizeBytes = 108;
    private const int SmokeFixtureStrategyThreshold = 100;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] CodePathPatterns =
    [
        "StratTaken",
        "MaatStratTaken",
        "StrategyCount",
        "SUID",
        "SubUniverse",
        "Sub-Universe",
        "reduced",
        "tiny",
        "richer",
        "fixture",
        "Take(",
        "Skip(",
        "Where(",
        "MaxStrategies",
        "StrategyLimit",
        "whitelist",
        "filter",
        "selectedStrategies"
    ];

    public static StratTakenPopulationAuditPackage Audit(StratTakenPopulationAuditRequest request)
    {
        var stratTakenPath = ResolveStratTakenPath(request.PackageRoot, request.StratTakenPath);
        var currentRead = ReadStratTaken(stratTakenPath, request.SuidOffsetBytes, request.RunKey);
        var fullRead = string.IsNullOrWhiteSpace(request.FullStratTakenReferencePath)
            ? null
            : ReadStratTaken(request.FullStratTakenReferencePath, request.SuidOffsetBytes, request.RunKey);
        var packageShape = InspectPackageShape(request.PackageRoot);
        var population = BuildPopulationReport(request.RunKey, currentRead, fullRead);
        var compatibility = BuildCompatibilityReport(currentRead, packageShape, population);
        var attrition = new StratTakenAttritionReport(
            request.RunKey,
            compatibility.PresentStrategyCount,
            compatibility.SuidCompatibleStrategyCount,
            compatibility.SuidRejectedStrategyCount,
            compatibility.VariableAvailableStrategyCount,
            compatibility.VariableRejectedStrategyCount,
            compatibility.ShapeCompatibleStrategyCount,
            compatibility.ShapeRejectedStrategyCount,
            compatibility.SignalConditionEvaluableCount,
            compatibility.SignalConditionNotEvaluableCount,
            compatibility.SentinelMinus999ExposureCount,
            compatibility.FinalPotentiallyEligibleStrategyCount,
            compatibility.STRATTAKEN_ATTRITION_GATE,
            compatibility.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS,
            compatibility.Issues);
        var codePath = BuildCodePathReport(request.RunKey, request.RepositoryRoot);

        return new StratTakenPopulationAuditPackage(
            currentRead.BinaryIntegrity,
            population,
            compatibility,
            attrition,
            codePath,
            SafetyAssertions(request.NoExecution));
    }

    public static async Task WritePackageAsync(string validationDirectory, StratTakenPopulationAuditPackage package, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(validationDirectory);
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strattaken_binary_integrity_report.json"] = JsonSerializer.Serialize(package.BinaryIntegrity, JsonOptions),
            ["strattaken_binary_integrity_report.md"] = RenderBinaryMarkdown(package.BinaryIntegrity, package.SafetyAssertions),
            ["strattaken_population_report.json"] = JsonSerializer.Serialize(package.Population, JsonOptions),
            ["strattaken_population_report.md"] = RenderPopulationMarkdown(package.Population, package.SafetyAssertions),
            ["strattaken_attrition_report.json"] = JsonSerializer.Serialize(new { package.Attrition, PackageCompatibility = package.Compatibility }, JsonOptions),
            ["strattaken_attrition_report.md"] = RenderAttritionMarkdown(package.Attrition, package.Compatibility, package.SafetyAssertions),
            ["strattaken_code_path_report.json"] = JsonSerializer.Serialize(package.CodePath, JsonOptions),
            ["strattaken_code_path_report.md"] = RenderCodePathMarkdown(package.CodePath, package.SafetyAssertions)
        };

        foreach (var artifact in artifacts)
        {
            await File.WriteAllTextAsync(Path.Combine(validationDirectory, artifact.Key), artifact.Value, Encoding.UTF8, cancellationToken);
        }

        var manifest = new
        {
            packageKind = "QubesIntradayStratTakenPopulationAttritionAudit",
            packageVersion = "1.0",
            packageIdentity = "RunKey",
            RunKey = package.BinaryIntegrity.RunKey,
            STRATTAKEN_BINARY_GATE = package.BinaryIntegrity.STRATTAKEN_BINARY_GATE,
            STRATTAKEN_POPULATION_GATE = package.Population.STRATTAKEN_POPULATION_GATE,
            STRATTAKEN_PACKAGE_COMPATIBILITY_GATE = package.Compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE,
            STRATTAKEN_ATTRITION_GATE = package.Attrition.STRATTAKEN_ATTRITION_GATE,
            STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS = package.Attrition.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS,
            readOnly = true,
            noExecution = true,
            noLegacyAhiGenerated = true,
            entries = artifacts.Keys.Select(name => new { path = name, role = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "artifact" : "report" }).ToArray(),
            supportFiles = new[] { "hashes.json", "manifest.sha256" }
        };
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8, cancellationToken);

        var hashes = artifacts.Keys
            .Append("manifest.json")
            .Select(name => new { path = name, sha256 = Sha256File(Path.Combine(validationDirectory, name)) })
            .ToArray();
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "manifest.sha256"), hashes.Single(x => x.path == "manifest.json").sha256 + "  manifest.json" + Environment.NewLine, Encoding.ASCII, cancellationToken);
    }

    public static string? ResolveStratTakenPath(string? packageRoot, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(packageRoot, "*StratTaken*.bin", SearchOption.AllDirectories)
            .OrderBy(x => Path.GetFileName(x).Contains("All", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static StratTakenReadResult ReadStratTaken(string? path, int suidOffsetBytes, string runKey)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            issues.Add("StratTakenFileAbsent");
            return new StratTakenReadResult(
                new StratTakenBinaryIntegrityReport(runKey, path, false, 0, null, StrategyRecordSizeBytes, suidOffsetBytes, null, 0, false, StratTakenGate.FAIL.ToString(), issues),
                []);
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 4)
        {
            issues.Add("StratTakenHeaderTruncated");
            return new StratTakenReadResult(
                new StratTakenBinaryIntegrityReport(runKey, path, true, bytes.Length, null, StrategyRecordSizeBytes, suidOffsetBytes, null, 0, true, StratTakenGate.FAIL.ToString(), issues),
                []);
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        if (count < 0)
        {
            issues.Add("StrategyCountNegative");
        }

        long? expectedSize = count < 0 ? null : 4L + count * (long)StrategyRecordSizeBytes;
        var isTruncated = expectedSize is not null && bytes.Length < expectedSize.Value;
        var trailingBytes = expectedSize is not null && bytes.Length > expectedSize.Value ? bytes.Length - expectedSize.Value : 0;
        if (isTruncated) issues.Add("StratTakenTruncated");
        if (trailingBytes > 0) issues.Add("StratTakenTrailingBytes");
        if (count == 0) issues.Add("StrategyCountZero");
        if (count is > 0 and < SmokeFixtureStrategyThreshold) issues.Add("StrategyCountAnomalouslyLow");
        if (suidOffsetBytes < 0 || suidOffsetBytes > StrategyRecordSizeBytes - 4) issues.Add("SuidOffsetOutOfRecordRange");

        var readableCount = count > 0
            ? Math.Min(count, Math.Max(0, (bytes.Length - 4) / StrategyRecordSizeBytes))
            : 0;
        var records = new List<StratTakenStrategyRecord>(readableCount);
        if (suidOffsetBytes >= 0 && suidOffsetBytes <= StrategyRecordSizeBytes - 4)
        {
            for (var ordinal = 0; ordinal < readableCount; ordinal++)
            {
                var recordOffset = 4 + ordinal * StrategyRecordSizeBytes;
                var suid = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(recordOffset + suidOffsetBytes, 4));
                var sentinel = ContainsMinus999(bytes.AsSpan(recordOffset, StrategyRecordSizeBytes));
                records.Add(new StratTakenStrategyRecord(ordinal, suid, sentinel));
            }
        }

        var gate = issues.Any(x => x is "StratTakenTruncated" or "StratTakenHeaderTruncated" or "StrategyCountNegative" or "SuidOffsetOutOfRecordRange")
            ? StratTakenGate.FAIL
            : issues.Any(x => x is "StrategyCountZero" or "StrategyCountAnomalouslyLow" or "StratTakenTrailingBytes")
                ? StratTakenGate.WARN
                : StratTakenGate.PASS;

        return new StratTakenReadResult(
            new StratTakenBinaryIntegrityReport(runKey, path, true, bytes.Length, count, StrategyRecordSizeBytes, suidOffsetBytes, expectedSize, trailingBytes, isTruncated, gate.ToString(), issues),
            records);
    }

    private static bool ContainsMinus999(ReadOnlySpan<byte> record)
    {
        for (var offset = 0; offset <= record.Length - 4; offset += 4)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(record[offset..(offset + 4)]) == -999)
            {
                return true;
            }
        }

        return false;
    }

    private static StratTakenPopulationReport BuildPopulationReport(string runKey, StratTakenReadResult current, StratTakenReadResult? full)
    {
        var issues = new List<string>();
        var count = current.BinaryIntegrity.StrategyCount ?? 0;
        var distribution = current.Records
            .GroupBy(x => x.Suid)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Count());
        var top = distribution
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(10)
            .ToArray();
        var looksSmoke = count is > 0 and < SmokeFixtureStrategyThreshold || distribution.Count <= 1 && count < 1_000;
        if (looksSmoke) issues.Add("CurrentStratTakenLooksLikeSmokeFixture");

        var comparison = full is null
            ? null
            : BuildComparison(current, full);
        if (comparison?.RetainedRatio < 0.05m) issues.Add("CurrentRetainsLessThanFivePercentOfFullReference");

        var gate = current.BinaryIntegrity.STRATTAKEN_BINARY_GATE == StratTakenGate.FAIL.ToString() || count == 0
            ? StratTakenGate.FAIL
            : looksSmoke || comparison?.RetainedRatio < 0.05m
                ? StratTakenGate.WARN
                : StratTakenGate.PASS;

        return new StratTakenPopulationReport(
            runKey,
            count,
            distribution.Count,
            distribution.Count == 0 ? null : distribution.Keys.Min(),
            distribution.Count == 0 ? null : distribution.Keys.Max(),
            distribution,
            top,
            comparison,
            looksSmoke,
            gate.ToString(),
            issues);
    }

    private static StratTakenPopulationComparison BuildComparison(StratTakenReadResult current, StratTakenReadResult full)
    {
        var currentCount = current.BinaryIntegrity.StrategyCount;
        var fullCount = full.BinaryIntegrity.StrategyCount;
        decimal? retainedRatio = currentCount is not null && fullCount is > 0
            ? decimal.Round(currentCount.Value / (decimal)fullCount.Value, 6)
            : null;
        var currentSuids = current.Records.Select(x => x.Suid).ToHashSet();
        var fullSuids = full.Records.Select(x => x.Suid).ToHashSet();
        var missingSuidCount = fullSuids.Count - fullSuids.Count(currentSuids.Contains);
        decimal? suidCoverageRatio = fullSuids.Count > 0
            ? decimal.Round(fullSuids.Count(currentSuids.Contains) / (decimal)fullSuids.Count, 6)
            : null;
        return new StratTakenPopulationComparison(currentCount, fullCount, retainedRatio, missingSuidCount, suidCoverageRatio);
    }

    private static StratTakenPackageShapeReport InspectPackageShape(string? packageRoot)
    {
        var evidence = new List<string>();
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
        {
            issues.Add("PackageRootAbsentOrNotProvided");
            return new StratTakenPackageShapeReport(packageRoot, false, null, null, null, null, evidence, issues);
        }

        var files = Directory.EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories).ToArray();
        int? subUniverseCount = CountFiles(files, "sub", "universe");
        int? instrumentCount = null;
        int? variableCount = null;
        var timeSeriesFiles = files.Count(x => ContainsAny(Path.GetFileName(x), "time", "series", "bar", "m15", "m30", "h1"));

        foreach (var json in files.Where(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Take(200))
        {
            TryInspectJson(json, evidence, ref subUniverseCount, ref instrumentCount, ref variableCount);
        }

        if (subUniverseCount is null or 0) issues.Add("SubUniverseShapeUnknown");
        if (instrumentCount is null or 0) issues.Add("InstrumentMappingUnknown");
        if (variableCount is null or 0) issues.Add("VariableShapeUnknown");
        if (timeSeriesFiles == 0) issues.Add("TimeSeriesShapeUnknown");

        return new StratTakenPackageShapeReport(packageRoot, true, subUniverseCount, instrumentCount, variableCount, timeSeriesFiles, evidence, issues);
    }

    private static int? CountFiles(IReadOnlyList<string> files, params string[] fragments)
    {
        var count = files.Count(x => fragments.All(fragment => Path.GetFileName(x).Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        return count == 0 ? null : count;
    }

    private static void TryInspectJson(string path, List<string> evidence, ref int? subUniverseCount, ref int? instrumentCount, ref int? variableCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            InspectElement(path, doc.RootElement, evidence, ref subUniverseCount, ref instrumentCount, ref variableCount);
        }
        catch (JsonException)
        {
            evidence.Add($"{path}: JSON parse skipped.");
        }
    }

    private static void InspectElement(string path, JsonElement element, List<string> evidence, ref int? subUniverseCount, ref int? instrumentCount, ref int? variableCount)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var name = property.Name;
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var value))
            {
                if (name.Contains("subUniverseCount", StringComparison.OrdinalIgnoreCase) || name.Equals("subUniverses", StringComparison.OrdinalIgnoreCase))
                {
                    subUniverseCount = MaxNullable(subUniverseCount, value);
                    evidence.Add($"{path}: {name}={value}");
                }
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var length = property.Value.GetArrayLength();
                if (name.Contains("subUniverse", StringComparison.OrdinalIgnoreCase))
                {
                    subUniverseCount = MaxNullable(subUniverseCount, length);
                    evidence.Add($"{path}: {name} array length={length}");
                }
                else if (name.Contains("instrument", StringComparison.OrdinalIgnoreCase) || name.Contains("symbol", StringComparison.OrdinalIgnoreCase) || name.Contains("universe", StringComparison.OrdinalIgnoreCase))
                {
                    instrumentCount = MaxNullable(instrumentCount, length);
                    evidence.Add($"{path}: {name} array length={length}");
                }
                else if (name.Contains("variable", StringComparison.OrdinalIgnoreCase) || name.Contains("feature", StringComparison.OrdinalIgnoreCase) || name.Contains("varName", StringComparison.OrdinalIgnoreCase))
                {
                    variableCount = MaxNullable(variableCount, length);
                    evidence.Add($"{path}: {name} array length={length}");
                }
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                InspectElement(path, property.Value, evidence, ref subUniverseCount, ref instrumentCount, ref variableCount);
            }
        }
    }

    private static int MaxNullable(int? current, int candidate)
        => current is null ? candidate : Math.Max(current.Value, candidate);

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static StratTakenCompatibilityReport BuildCompatibilityReport(
        StratTakenReadResult current,
        StratTakenPackageShapeReport packageShape,
        StratTakenPopulationReport population)
    {
        var issues = new List<string>();
        var present = current.Records.Count;
        var suidCompatible = packageShape.SubUniverseCount is > 0
            ? current.Records.Count(x => x.Suid >= 0 && x.Suid < packageShape.SubUniverseCount.Value)
            : 0;
        var suidRejected = packageShape.SubUniverseCount is > 0 ? present - suidCompatible : present;
        if (suidRejected > 0) issues.Add("SuidOutOfRangeForPackageShape");

        var variableAvailable = packageShape.VariableCount is > 0 ? suidCompatible : 0;
        var variableRejected = suidCompatible - variableAvailable;
        if (variableRejected > 0 || packageShape.VariableCount is null or 0) issues.Add("VariableAvailabilityUnknownOrMissing");

        var shapeCompatible = packageShape.Exists && packageShape.SubUniverseCount is > 0 && packageShape.InstrumentCount is > 0
            ? variableAvailable
            : 0;
        var shapeRejected = variableAvailable - shapeCompatible + (packageShape.Exists ? 0 : present);
        if (shapeCompatible == 0 && present > 0) issues.Add("PackageShapeMismatchOrUnknown");

        var signalEvaluable = packageShape.TimeSeriesFileCount is > 0 ? shapeCompatible : 0;
        var signalNotEvaluable = shapeCompatible - signalEvaluable;
        if (signalNotEvaluable > 0 || packageShape.TimeSeriesFileCount is null or 0) issues.Add("SignalConditionStaticInputsUnknown");

        var sentinel = current.Records.Count(x => x.SentinelMinus999Detected);
        var finalEligible = Math.Max(0, signalEvaluable - sentinel);
        if (finalEligible == 0 && present > 0) issues.Add("TotalStaticAttritionBeforeWeights");

        var compatibilityGate = current.BinaryIntegrity.STRATTAKEN_BINARY_GATE == StratTakenGate.FAIL.ToString() || present == 0 || suidRejected > 0
            ? StratTakenGate.FAIL
            : packageShape.Issues.Count > 0
                ? StratTakenGate.WARN
                : StratTakenGate.PASS;
        var attritionGate = finalEligible == 0
            ? StratTakenGate.FAIL
            : packageShape.Issues.Count > 0 || signalNotEvaluable > 0
                ? StratTakenGate.WARN
                : StratTakenGate.PASS;
        var conclusion = ExplainUnderpopulation(population, compatibilityGate, finalEligible, present, suidRejected, issues);

        return new StratTakenCompatibilityReport(
            packageShape,
            present,
            suidCompatible,
            suidRejected,
            variableAvailable,
            variableRejected,
            shapeCompatible,
            Math.Max(0, shapeRejected),
            signalEvaluable,
            signalNotEvaluable,
            sentinel,
            finalEligible,
            compatibilityGate.ToString(),
            attritionGate.ToString(),
            conclusion.ToString(),
            issues.Concat(packageShape.Issues).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static StratTakenUnderpopulationConclusion ExplainUnderpopulation(
        StratTakenPopulationReport population,
        StratTakenGate compatibilityGate,
        int finalEligible,
        int present,
        int suidRejected,
        IReadOnlyList<string> issues)
    {
        if (present == 0 || finalEligible == 0 && (population.LooksLikeSmokeFixture || suidRejected > 0 || compatibilityGate == StratTakenGate.FAIL))
        {
            return StratTakenUnderpopulationConclusion.YES;
        }

        if (population.STRATTAKEN_POPULATION_GATE == StratTakenGate.PASS.ToString() &&
            compatibilityGate == StratTakenGate.PASS &&
            finalEligible > 0)
        {
            return StratTakenUnderpopulationConclusion.NO;
        }

        return issues.Contains("SignalConditionStaticInputsUnknown") ? StratTakenUnderpopulationConclusion.UNKNOWN : StratTakenUnderpopulationConclusion.UNKNOWN;
    }

    private static StratTakenCodePathReport BuildCodePathReport(string runKey, string repositoryRoot)
    {
        var findings = new List<StratTakenCodePathFinding>();
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot))
        {
            issues.Add("RepositoryRootAbsent");
            return new StratTakenCodePathReport(runKey, repositoryRoot, CodePathPatterns, findings, StratTakenGate.WARN.ToString(), issues);
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs", ".ps1", ".mjs", ".json", ".md", ".txt", ".cmd", ".bat" };
        foreach (var file in Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
                     .Where(x => allowedExtensions.Contains(Path.GetExtension(x)) && !IsIgnoredPath(repositoryRoot, x)))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var pattern = CodePathPatterns.FirstOrDefault(x => line.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (pattern is null)
                {
                    continue;
                }

                findings.Add(new StratTakenCodePathFinding(
                    Path.GetRelativePath(repositoryRoot, file),
                    index + 1,
                    FindScope(lines, index),
                    pattern,
                    ExtractConfigKey(line),
                    ExtractCapValue(line),
                    line.Trim(),
                    ImpactFor(line)));
            }
        }

        if (findings.Count == 0) issues.Add("NoStratTakenCodePathFound");
        var gate = findings.Count == 0 ? StratTakenGate.WARN : StratTakenGate.PASS;
        return new StratTakenCodePathReport(runKey, repositoryRoot, CodePathPatterns, findings.Take(500).ToArray(), gate.ToString(), issues);
    }

    private static bool IsIgnoredPath(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        return relative.StartsWith(".git", StringComparison.OrdinalIgnoreCase) ||
               relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               relative.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               relative.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindScope(IReadOnlyList<string> lines, int startIndex)
    {
        for (var index = startIndex; index >= 0 && index >= startIndex - 80; index--)
        {
            var line = lines[index].Trim();
            if (Regex.IsMatch(line, @"\b(class|record|struct|void|Task|async|function|static)\b") && line.Contains('(') || Regex.IsMatch(line, @"\bclass\s+\w+|\bfunction\s+\w+"))
            {
                return line;
            }
        }

        return null;
    }

    private static string? ExtractConfigKey(string line)
    {
        var match = Regex.Match(line, "\"(?<key>[^\"]*(StratTaken|Strategy|SUID|SubUniverse|MaxStrategies|StrategyLimit)[^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static string? ExtractCapValue(string line)
    {
        var take = Regex.Match(line, @"\b(Take|Skip)\s*\(\s*(?<value>\d+)\s*\)", RegexOptions.IgnoreCase);
        if (take.Success) return $"{take.Groups[1].Value}({take.Groups["value"].Value})";
        var cap = Regex.Match(line, @"\b(MaxStrategies|StrategyLimit)\b\s*[:=]\s*(?<value>\d+)", RegexOptions.IgnoreCase);
        return cap.Success ? $"{cap.Groups[1].Value}={cap.Groups["value"].Value}" : null;
    }

    private static string ImpactFor(string line)
    {
        if (line.Contains("Take(", StringComparison.OrdinalIgnoreCase) || line.Contains("MaxStrategies", StringComparison.OrdinalIgnoreCase) || line.Contains("StrategyLimit", StringComparison.OrdinalIgnoreCase))
        {
            return "Possible cap or truncation point; may reduce strategy population before weights.";
        }

        if (line.Contains("reduced", StringComparison.OrdinalIgnoreCase) || line.Contains("tiny", StringComparison.OrdinalIgnoreCase) || line.Contains("fixture", StringComparison.OrdinalIgnoreCase))
        {
            return "Fixture or reduced package reference; may be smoke-only rather than economic population.";
        }

        if (line.Contains("Where(", StringComparison.OrdinalIgnoreCase) || line.Contains("filter", StringComparison.OrdinalIgnoreCase) || line.Contains("whitelist", StringComparison.OrdinalIgnoreCase))
        {
            return "Possible filter point; may reject strategies before weight calculation.";
        }

        return "Potential StratTaken selection, loading, generation, or compatibility path.";
    }

    private static IReadOnlyList<string> SafetyAssertions(bool noExecution)
        =>
        [
            "Read-only audit: StratTaken and package inputs are opened for read only.",
            "No A.txt, H.txt, I.txt, FlatBar, replay, simulation, manager, Anubis, PMS, OMS, EMS, DB migration, DB write, artifact mutation, or economic behavior change is performed.",
            noExecution ? "--no-execution is true; the tool performs static inspection only." : "--no-execution was not true; reports remain static and no runtime execution path exists."
        ];

    private static string RenderBinaryMarkdown(StratTakenBinaryIntegrityReport report, IReadOnlyList<string> safety)
    {
        var builder = Header("StratTaken Binary Integrity Report", report.RunKey);
        builder.AppendLine($"STRATTAKEN_BINARY_GATE: `{report.STRATTAKEN_BINARY_GATE}`");
        builder.AppendLine($"Path: `{report.Path}`");
        builder.AppendLine($"Exists: `{report.Exists}`");
        builder.AppendLine($"File size bytes: `{report.FileSizeBytes}`");
        builder.AppendLine($"StrategyCount: `{report.StrategyCount}`");
        builder.AppendLine($"Expected size bytes: `{report.ExpectedSizeBytes}`");
        builder.AppendLine($"Record size bytes: `{report.RecordSizeBytes}`");
        builder.AppendLine($"SUID offset bytes: `{report.SuidOffsetBytes}`");
        AppendIssues(builder, report.Issues);
        AppendSafety(builder, safety);
        return builder.ToString();
    }

    private static string RenderPopulationMarkdown(StratTakenPopulationReport report, IReadOnlyList<string> safety)
    {
        var builder = Header("StratTaken Population Report", report.RunKey);
        builder.AppendLine($"STRATTAKEN_POPULATION_GATE: `{report.STRATTAKEN_POPULATION_GATE}`");
        builder.AppendLine($"StrategyCount: `{report.StrategyCount}`");
        builder.AppendLine($"Distinct SUID count: `{report.DistinctSuidCount}`");
        builder.AppendLine($"SUID min/max: `{report.MinSuid}` / `{report.MaxSuid}`");
        builder.AppendLine($"Looks like smoke fixture: `{report.LooksLikeSmokeFixture}`");
        if (report.FullReferenceComparison is not null)
        {
            builder.AppendLine($"Full reference count: `{report.FullReferenceComparison.FullCount}`");
            builder.AppendLine($"Retained ratio: `{report.FullReferenceComparison.RetainedRatio}`");
            builder.AppendLine($"Missing SUID count: `{report.FullReferenceComparison.MissingSuidCount}`");
            builder.AppendLine($"SUID coverage ratio: `{report.FullReferenceComparison.SuidCoverageRatio}`");
        }

        builder.AppendLine();
        builder.AppendLine("| SUID | Strategy count |");
        builder.AppendLine("| ---: | ---: |");
        foreach (var item in report.TopSuidByStrategyCount)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {item.Key} | {item.Value} |");
        }

        AppendIssues(builder, report.Issues);
        AppendSafety(builder, safety);
        return builder.ToString();
    }

    private static string RenderAttritionMarkdown(StratTakenAttritionReport report, StratTakenCompatibilityReport compatibility, IReadOnlyList<string> safety)
    {
        var builder = Header("StratTaken Attrition Report", report.RunKey);
        builder.AppendLine($"STRATTAKEN_PACKAGE_COMPATIBILITY_GATE: `{compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE}`");
        builder.AppendLine($"STRATTAKEN_ATTRITION_GATE: `{report.STRATTAKEN_ATTRITION_GATE}`");
        builder.AppendLine($"STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS: `{report.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS}`");
        builder.AppendLine($"Package root: `{compatibility.PackageShape.PackageRoot}`");
        builder.AppendLine($"Sub-universe count: `{compatibility.PackageShape.SubUniverseCount}`");
        builder.AppendLine($"Instrument count: `{compatibility.PackageShape.InstrumentCount}`");
        builder.AppendLine($"Variable count: `{compatibility.PackageShape.VariableCount}`");
        builder.AppendLine($"Time-series file count: `{compatibility.PackageShape.TimeSeriesFileCount}`");
        builder.AppendLine();
        builder.AppendLine("Does StratTaken underpopulation or incompatibility plausibly explain ZeroOnly?");
        builder.AppendLine($"`{report.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS}`");
        builder.AppendLine();
        builder.AppendLine("| Counter | Value |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| parsed_strategy_count | {report.ParsedStrategyCount} |");
        builder.AppendLine($"| suid_compatible_strategy_count | {report.SuidCompatibleStrategyCount} |");
        builder.AppendLine($"| suid_rejected_strategy_count | {report.SuidRejectedStrategyCount} |");
        builder.AppendLine($"| variable_available_strategy_count | {report.VariableAvailableStrategyCount} |");
        builder.AppendLine($"| variable_rejected_strategy_count | {report.VariableRejectedStrategyCount} |");
        builder.AppendLine($"| shape_compatible_strategy_count | {report.ShapeCompatibleStrategyCount} |");
        builder.AppendLine($"| shape_rejected_strategy_count | {report.ShapeRejectedStrategyCount} |");
        builder.AppendLine($"| signal_condition_evaluable_count | {report.SignalConditionEvaluableCount} |");
        builder.AppendLine($"| signal_condition_not_evaluable_count | {report.SignalConditionNotEvaluableCount} |");
        builder.AppendLine($"| sentinel_minus_999_exposure_count | {report.SentinelMinus999ExposureCount} |");
        builder.AppendLine($"| final_potentially_eligible_strategy_count | {report.FinalPotentiallyEligibleStrategyCount} |");
        AppendIssues(builder, report.Issues);
        AppendSafety(builder, safety);
        return builder.ToString();
    }

    private static string RenderCodePathMarkdown(StratTakenCodePathReport report, IReadOnlyList<string> safety)
    {
        var builder = Header("StratTaken Code Path Report", report.RunKey);
        builder.AppendLine($"STRATTAKEN_CODE_PATH_GATE: `{report.STRATTAKEN_CODE_PATH_GATE}`");
        builder.AppendLine($"Repository root: `{report.RepositoryRoot}`");
        builder.AppendLine();
        builder.AppendLine("| File | Line | Scope | Pattern | Config key | Cap | Impact | Snippet |");
        builder.AppendLine("| --- | ---: | --- | --- | --- | --- | --- | --- |");
        foreach (var finding in report.Findings)
        {
            builder.AppendLine($"| {finding.File} | {finding.Line} | {Escape(finding.MethodOrScope)} | {Escape(finding.Pattern)} | {Escape(finding.ConfigKey)} | {Escape(finding.CapValue)} | {Escape(finding.Impact)} | {Escape(finding.Snippet)} |");
        }

        AppendIssues(builder, report.Issues);
        AppendSafety(builder, safety);
        return builder.ToString();
    }

    private static StringBuilder Header(string title, string runKey)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"RunKey: `{runKey}`");
        builder.AppendLine();
        return builder;
    }

    private static void AppendIssues(StringBuilder builder, IReadOnlyList<string> issues)
    {
        builder.AppendLine();
        builder.AppendLine("Issues:");
        foreach (var issue in issues)
        {
            builder.AppendLine($"- {issue}");
        }
    }

    private static void AppendSafety(StringBuilder builder, IReadOnlyList<string> safety)
    {
        builder.AppendLine();
        builder.AppendLine("Safety assertions:");
        foreach (var assertion in safety)
        {
            builder.AppendLine($"- {assertion}");
        }
    }

    private static string Escape(string? value)
        => (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record StratTakenReadResult(StratTakenBinaryIntegrityReport BinaryIntegrity, IReadOnlyList<StratTakenStrategyRecord> Records);
}
