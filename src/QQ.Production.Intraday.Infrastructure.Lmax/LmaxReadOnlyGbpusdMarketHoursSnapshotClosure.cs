using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public enum LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification
{
    CompletedWithBook,
    CompletedWithEmptyBook,
    FailedSafe,
    UnsafeFail
}

public sealed record LmaxReadOnlyGbpusdMarketHoursSnapshotClosureResult(
    LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision Decision,
    LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification Classification,
    string Status,
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    bool SnapshotReceived,
    int EntryCount,
    double? BestBid,
    double? BestAsk,
    double? Mid,
    bool NoSensitiveContent,
    IReadOnlyList<string> Issues);

public sealed record LmaxReadOnlyGbpusdMarketHoursReplayReport(
    string ReplayStatus,
    int ObservationCount,
    int BlockingObservationCount,
    int WarningObservationCount,
    string MutationGuard,
    bool RuntimeShadowReplaySubmit,
    bool ExternalConnectionAttempted,
    bool NoSensitiveContent);

public static class LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OrderPattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCaptureReportRequest|OrderStatusRequest|SubmitOrder)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdMarketHoursSnapshotClosureResult ReviewArtifact(
        LmaxReadOnlyGbpusdManualSnapshotResult result,
        string rawArtifactText = "")
    {
        var validation = LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(result, rawArtifactText);
        var issues = validation.Checks
            .Where(x => x.Decision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL)
            .Select(x => $"{x.Name}: {x.Detail}")
            .ToList();

        var sensitiveScanText = NormalizeAllowedSanitizedCredentialMetadata(rawArtifactText);
        if (SensitivePattern.IsMatch(sensitiveScanText))
        {
            issues.Add("Artifact contains credential-shaped or raw FIX content.");
        }

        if (OrderPattern.IsMatch(rawArtifactText))
        {
            issues.Add("Artifact contains order/trading message surface.");
        }

        var classification = Classify(result, validation.FinalDecision, issues.Count);
        var decision = classification switch
        {
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithBook => LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS,
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithEmptyBook => LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.FailedSafe => LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            _ => LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL
        };

        return new(
            decision,
            classification,
            result.Status,
            result.Symbol,
            result.SlashSymbol,
            result.SecurityId,
            result.SecurityIdSource,
            result.SnapshotReceived,
            result.EntryCount,
            result.BestBid,
            result.BestAsk,
            result.Mid,
            result.NoSensitiveContent,
            issues);
    }

    public static LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision ValidateMarketDataOnlyPreviewJson(
        string previewJson,
        bool expectEmptyBookWarning)
    {
        if (SensitivePattern.IsMatch(previewJson) || OrderPattern.IsMatch(previewJson))
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
        }

        using var document = JsonDocument.Parse(previewJson);
        var root = document.RootElement;
        if (GetString(root, "schemaVersion") != "lmax-fix-lifecycle-evidence-v1"
            || GetString(root, "evidenceMode") != "MarketDataOnly"
            || GetString(root, "instrument") != "GBPUSD"
            || GetString(root, "slashSymbol") != "GBP/USD"
            || GetString(root, "securityId") != "4002"
            || GetBool(root, "shadowReplaySubmitAttempted")
            || GetBool(root, "tradingMutationAttempted")
            || GetBool(root, "orderSubmissionAttempted")
            || !GetBool(root, "noSensitiveContent"))
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
        }

        if (!root.TryGetProperty("marketData", out var marketData)
            || !GetBool(marketData, "snapshotReceived")
            || GetInt(marketData, "entryCount") < 0)
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
        }

        var entryCount = GetInt(marketData, "entryCount");
        var status = GetString(marketData, "status");
        if (expectEmptyBookWarning)
        {
            var warnings = root.TryGetProperty("warnings", out var warningsElement) && warningsElement.ValueKind == JsonValueKind.Array
                ? warningsElement.GetArrayLength()
                : 0;
            return status == "EmptyBook" && entryCount == 0 && warnings > 0
                ? LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
        }

        return status == "Ok" && entryCount > 0
            ? LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS
            : LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
    }

    public static LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision ValidateReplayReport(
        LmaxReadOnlyGbpusdMarketHoursReplayReport report)
    {
        return report.ReplayStatus == "Completed"
               && report.ObservationCount == 0
               && report.BlockingObservationCount == 0
               && report.WarningObservationCount == 0
               && report.MutationGuard == "Unchanged"
               && !report.RuntimeShadowReplaySubmit
               && !report.ExternalConnectionAttempted
               && report.NoSensitiveContent
            ? LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS
            : LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL;
    }

    private static LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification Classify(
        LmaxReadOnlyGbpusdManualSnapshotResult result,
        LmaxReadOnlyGbpusdManualSnapshotResultDecision resultDecision,
        int issueCount)
    {
        if (resultDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL || issueCount > 0)
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.UnsafeFail;
        }

        if (result.Status == "CompletedWithEmptyBook")
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithEmptyBook;
        }

        if (result.Status.StartsWith("FailedSafe", StringComparison.OrdinalIgnoreCase)
            || result.Status.StartsWith("Blocked", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.FailedSafe;
        }

        return LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithBook;
    }

    private static string NormalizeAllowedSanitizedCredentialMetadata(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string[] allowedMarkers =
        [
            "LMAX_DEMO_FIX_USERNAME",
            "LMAX_DEMO_FIX_PASSWORD",
            "LMAX_DEMO_SENDER_COMP_ID",
            "LMAX_DEMO_TARGET_COMP_ID",
            "usernamePresent",
            "passwordPresent",
            "senderCompIdPresent",
            "targetCompIdPresent",
            "usernameLength",
            "passwordLength",
            "senderCompIdLength",
            "targetCompIdLength",
            "sameSenderCompIdSourceLabel",
            "sameTargetCompIdSourceLabel",
            "senderCompIdMismatchSuspected",
            "targetCompIdMismatchSuspected"
        ];

        var normalized = text;
        foreach (var marker in allowedMarkers)
        {
            normalized = Regex.Replace(normalized, Regex.Escape(marker), "SANITIZED_METADATA_MARKER", RegexOptions.IgnoreCase);
        }

        return normalized;
    }

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool GetBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
