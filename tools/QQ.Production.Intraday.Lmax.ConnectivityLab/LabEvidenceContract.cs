using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public enum LmaxEvidenceContractIssueSeverity
{
    Info,
    Warning,
    Error
}

public enum LmaxEvidenceMode
{
    EmptyReadOnly,
    MarketDataOnly,
    TradeCaptureOnly,
    OrderStatusOnly,
    ProtocolRejectOnly,
    MixedReadOnly,
    SyntheticLifecycle
}

public sealed record LmaxEvidenceContractIssue(
    LmaxEvidenceContractIssueSeverity Severity,
    string Path,
    string Code,
    string Message);

public sealed record LmaxEvidenceContractValidationResult(
    string SchemaVersion,
    LmaxEvidenceMode EvidenceMode,
    bool IsValid,
    IReadOnlyList<LmaxEvidenceContractIssue> Issues,
    string NormalizedJson)
{
    public int ErrorCount => Issues.Count(x => x.Severity == LmaxEvidenceContractIssueSeverity.Error);
    public int WarningCount => Issues.Count(x => x.Severity == LmaxEvidenceContractIssueSeverity.Warning);
}

public static class LmaxEvidenceContractValidator
{
    public const string SchemaVersion = "lmax-fix-lifecycle-evidence-v1";

    private static readonly string[] RequiredStringFields =
    [
        "schemaVersion",
        "source",
        "inputSource",
        "reason",
        "captureMode",
        "redaction"
    ];

    private static readonly string[] RequiredArrayFields =
    [
        "executionReports",
        "orderStatuses",
        "tradeCaptureReports",
        "protocolRejects"
    ];

    private static readonly string[] SensitiveMarkers =
    [
        "554=",
        "password",
        "authorization",
        "bearer ",
        "x-api-key",
        "api-key",
        "secret",
        "token"
    ];

    public static LmaxEvidenceContractValidationResult ValidateAndNormalize(string json)
    {
        var issues = new List<LmaxEvidenceContractIssue>();
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            issues.Add(Error("$", "InvalidJson", ex.Message));
            return new(string.Empty, LmaxEvidenceMode.EmptyReadOnly, false, issues, string.Empty);
        }

        if (node is not JsonObject root)
        {
            issues.Add(Error("$", "RootNotObject", "Evidence root must be a JSON object."));
            return new(string.Empty, LmaxEvidenceMode.EmptyReadOnly, false, issues, json);
        }

        if (ContainsSensitiveEvidence(json))
        {
            issues.Add(Error("$", "SensitiveContent", "Evidence contains credential-like or secret-like content."));
        }

        NormalizeRoot(root, issues);
        ValidateRoot(root, issues);
        ValidateReports(root, issues);
        var evidenceMode = InferEvidenceMode(root);
        root["evidenceMode"] = evidenceMode.ToString();
        issues.Add(Info("$", "EvidenceModeInferred", $"Inferred evidence mode: {evidenceMode}."));

        var normalizedJson = root.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        if (ContainsSensitiveEvidence(normalizedJson))
        {
            issues.Add(Error("$", "SensitiveContentAfterNormalization", "Normalized evidence contains credential-like or secret-like content."));
        }

        var schemaVersion = root["schemaVersion"]?.GetValue<string>() ?? string.Empty;
        return new(schemaVersion, evidenceMode, issues.All(x => x.Severity != LmaxEvidenceContractIssueSeverity.Error), issues, normalizedJson);
    }

    public static bool ContainsSensitiveEvidence(string json)
    {
        foreach (var marker in SensitiveMarkers)
        {
            if (json.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void NormalizeRoot(JsonObject root, List<LmaxEvidenceContractIssue> issues)
    {
        if (root["orderStatuses"] is null && root["orderStatusReports"] is JsonArray legacyStatuses)
        {
            root["orderStatuses"] = legacyStatuses.DeepClone();
            root.Remove("orderStatusReports");
            issues.Add(Warning("$.orderStatusReports", "LegacyOrderStatusReports", "Legacy orderStatusReports was normalized to orderStatuses."));
        }

        if (root["inputSource"]?.GetValueKind() == JsonValueKind.Number)
        {
            root["inputSource"] = "LabEvidenceFile";
            issues.Add(Warning("$.inputSource", "NumericInputSource", "Numeric inputSource was normalized to LabEvidenceFile."));
        }

        foreach (var name in RequiredArrayFields)
        {
            if (root[name] is null)
            {
                root[name] = new JsonArray();
                issues.Add(Warning($"$.{name}", "MissingArrayNormalized", $"{name} was missing and was normalized to an empty array."));
            }
        }

        NormalizeTradeCaptureReports(root["tradeCaptureReports"] as JsonArray, issues);
        NormalizeSideArray(root["executionReports"] as JsonArray, "$.executionReports", issues);
        NormalizeSideArray(root["tradeCaptureReports"] as JsonArray, "$.tradeCaptureReports", issues);
    }

    private static void NormalizeTradeCaptureReports(JsonArray? reports, List<LmaxEvidenceContractIssue> issues)
    {
        if (reports is null) return;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i] is not JsonObject report) continue;
            var path = $"$.tradeCaptureReports[{i}]";

            if (report["tradeDate"]?.GetValue<string>() is { } tradeDate && TryNormalizeTradeDate(tradeDate, out var normalizedDate) && !string.Equals(tradeDate, normalizedDate, StringComparison.Ordinal))
            {
                report["tradeDate"] = normalizedDate;
                issues.Add(Warning($"{path}.tradeDate", "CompactTradeDateNormalized", "Compact FIX tradeDate was normalized to yyyy-MM-dd."));
            }

            if (!report.ContainsKey("tradeUti"))
            {
                report["tradeUti"] = null;
                issues.Add(Info($"{path}.tradeUti", "MissingTradeUtiAdded", "Missing TradeUTI was normalized to explicit null."));
            }
        }
    }

    private static void NormalizeSideArray(JsonArray? reports, string path, List<LmaxEvidenceContractIssue> issues)
    {
        if (reports is null) return;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i] is not JsonObject report) continue;
            if (report["side"]?.GetValue<string>() is not { } side) continue;
            var normalized = side switch
            {
                "1" => "Buy",
                "2" => "Sell",
                _ => null
            };
            if (normalized is not null)
            {
                report["side"] = normalized;
                issues.Add(Warning($"{path}[{i}].side", "RawFixSideNormalized", "Raw FIX side was normalized to Buy/Sell."));
            }
        }
    }

    private static void ValidateRoot(JsonObject root, List<LmaxEvidenceContractIssue> issues)
    {
        foreach (var field in RequiredStringFields)
        {
            if (root[field]?.GetValueKind() != JsonValueKind.String || string.IsNullOrWhiteSpace(root[field]?.GetValue<string>()))
            {
                issues.Add(Error($"$.{field}", "RequiredFieldMissing", $"{field} is required."));
            }
        }

        if (!string.Equals(root["schemaVersion"]?.GetValue<string>(), SchemaVersion, StringComparison.Ordinal))
        {
            issues.Add(Error("$.schemaVersion", "UnsupportedSchemaVersion", $"schemaVersion must be {SchemaVersion}."));
        }

        if (!string.Equals(root["inputSource"]?.GetValue<string>(), "LabEvidenceFile", StringComparison.Ordinal)
            && !string.Equals(root["inputSource"]?.GetValue<string>(), "SyntheticFixture", StringComparison.Ordinal))
        {
            issues.Add(Error("$.inputSource", "UnsupportedInputSource", "inputSource must be LabEvidenceFile or SyntheticFixture."));
        }

        foreach (var field in RequiredArrayFields)
        {
            if (root[field] is not JsonArray)
            {
                issues.Add(Error($"$.{field}", "ArrayRequired", $"{field} must be a JSON array."));
            }
        }

        if (root["orderStatusReports"] is not null)
        {
            issues.Add(Warning("$.orderStatusReports", "LegacyPropertyPresent", "Legacy orderStatusReports is accepted only for normalization; generated evidence must use orderStatuses."));
        }
    }

    private static void ValidateReports(JsonObject root, List<LmaxEvidenceContractIssue> issues)
    {
        ValidateTradeCaptureReports(root["tradeCaptureReports"] as JsonArray, issues);
        ValidateSideValues(root["executionReports"] as JsonArray, "$.executionReports", issues);
        ValidateSideValues(root["tradeCaptureReports"] as JsonArray, "$.tradeCaptureReports", issues);
        ValidateTimestampValues(root["executionReports"] as JsonArray, "$.executionReports", "transactTimeUtc", issues);
        ValidateTimestampValues(root["orderStatuses"] as JsonArray, "$.orderStatuses", "transactTimeUtc", issues);
        ValidateTimestampValues(root["tradeCaptureReports"] as JsonArray, "$.tradeCaptureReports", "transactTimeUtc", issues);
    }

    private static LmaxEvidenceMode InferEvidenceMode(JsonObject root)
    {
        var executionReportCount = CountArray(root["executionReports"]);
        var orderStatusCount = CountArray(root["orderStatuses"]);
        var tradeCaptureCount = CountArray(root["tradeCaptureReports"]);
        var protocolRejectCount = CountArray(root["protocolRejects"]);
        var hasMarketData = HasMarketData(root["marketData"]);
        var captureMode = root["captureMode"]?.GetValue<string>() ?? string.Empty;

        if (captureMode.Contains("Lifecycle", StringComparison.OrdinalIgnoreCase)
            || (executionReportCount > 0 && orderStatusCount > 0 && tradeCaptureCount > 0))
        {
            return LmaxEvidenceMode.SyntheticLifecycle;
        }

        if (protocolRejectCount > 0 && executionReportCount == 0 && orderStatusCount == 0 && tradeCaptureCount == 0)
        {
            return LmaxEvidenceMode.ProtocolRejectOnly;
        }

        if (tradeCaptureCount > 0 && executionReportCount == 0 && orderStatusCount == 0 && protocolRejectCount == 0)
        {
            return LmaxEvidenceMode.TradeCaptureOnly;
        }

        if (orderStatusCount > 0 && executionReportCount == 0 && tradeCaptureCount == 0 && protocolRejectCount == 0)
        {
            return LmaxEvidenceMode.OrderStatusOnly;
        }

        if (executionReportCount == 0 && orderStatusCount == 0 && tradeCaptureCount == 0 && protocolRejectCount == 0)
        {
            return hasMarketData ? LmaxEvidenceMode.MarketDataOnly : LmaxEvidenceMode.EmptyReadOnly;
        }

        return LmaxEvidenceMode.MixedReadOnly;
    }

    private static int CountArray(JsonNode? node)
        => node is JsonArray array ? array.Count : 0;

    private static bool HasMarketData(JsonNode? node)
    {
        if (node is not JsonObject marketData)
        {
            return false;
        }

        if (marketData["snapshotReceived"]?.GetValueKind() == JsonValueKind.True)
        {
            return true;
        }

        if (marketData["entries"] is JsonArray { Count: > 0 })
        {
            return true;
        }

        if (marketData["entryCount"]?.GetValueKind() == JsonValueKind.Number
            && marketData["entryCount"]!.GetValue<int>() > 0)
        {
            return true;
        }

        return marketData["bestBid"] is not null || marketData["bestAsk"] is not null || marketData["mid"] is not null;
    }

    private static void ValidateTradeCaptureReports(JsonArray? reports, List<LmaxEvidenceContractIssue> issues)
    {
        if (reports is null) return;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i] is not JsonObject report) continue;
            var path = $"$.tradeCaptureReports[{i}]";

            if (report["tradeDate"]?.GetValueKind() == JsonValueKind.String
                && !DateOnly.TryParseExact(report["tradeDate"]!.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                issues.Add(Error($"{path}.tradeDate", "InvalidTradeDate", "tradeDate must use yyyy-MM-dd."));
            }

            if (!report.ContainsKey("tradeUti"))
            {
                issues.Add(Error($"{path}.tradeUti", "TradeUtiPropertyMissing", "tradeUti must be present and may be null."));
            }

            foreach (var numeric in new[] { "lastQty", "lastPx" })
            {
                if (report[numeric] is not null && report[numeric]!.GetValueKind() != JsonValueKind.Number)
                {
                    issues.Add(Error($"{path}.{numeric}", "NumericFieldInvalid", $"{numeric} must be numeric."));
                }
            }

            if (report["payload"] is JsonObject payload && payload["securityId"] is null && report["symbol"] is null)
            {
                issues.Add(Warning($"{path}.payload.securityId", "MissingSecurityId", "securityId is recommended for LMAX replay evidence."));
            }
        }
    }

    private static void ValidateSideValues(JsonArray? reports, string path, List<LmaxEvidenceContractIssue> issues)
    {
        if (reports is null) return;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i] is not JsonObject report || report["side"]?.GetValueKind() != JsonValueKind.String) continue;
            var side = report["side"]!.GetValue<string>();
            if (!string.Equals(side, "Buy", StringComparison.Ordinal) && !string.Equals(side, "Sell", StringComparison.Ordinal))
            {
                issues.Add(Error($"{path}[{i}].side", "InvalidSide", "side must be Buy or Sell."));
            }
        }
    }

    private static void ValidateTimestampValues(JsonArray? reports, string path, string fieldName, List<LmaxEvidenceContractIssue> issues)
    {
        if (reports is null) return;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i] is not JsonObject report || report[fieldName] is null || report[fieldName]!.GetValueKind() == JsonValueKind.Null) continue;
            if (report[fieldName]!.GetValueKind() != JsonValueKind.String || !DateTimeOffset.TryParse(report[fieldName]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
            {
                issues.Add(Error($"{path}[{i}].{fieldName}", "InvalidTimestamp", $"{fieldName} must be an ISO timestamp."));
            }
        }
    }

    private static bool TryNormalizeTradeDate(string value, out string normalized)
    {
        if (DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compact))
        {
            normalized = compact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            normalized = iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = value;
        return false;
    }

    private static LmaxEvidenceContractIssue Error(string path, string code, string message)
        => new(LmaxEvidenceContractIssueSeverity.Error, path, code, message);

    private static LmaxEvidenceContractIssue Warning(string path, string code, string message)
        => new(LmaxEvidenceContractIssueSeverity.Warning, path, code, message);

    private static LmaxEvidenceContractIssue Info(string path, string code, string message)
        => new(LmaxEvidenceContractIssueSeverity.Info, path, code, message);
}
