using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyAdditionalInstrumentDefinition(
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth);

public sealed record LmaxReadOnlyAdditionalInstrumentManualSnapshotResult(
    string RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool CredentialValuesReturned,
    bool LogonAttempted,
    bool LogonSucceeded,
    bool SnapshotRequestAttempted,
    bool SnapshotReceived,
    bool LogoutAttempted,
    bool LogoutSucceeded,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    double? BestBid,
    double? BestAsk,
    double? Mid,
    int EntryCount,
    long? WaitDurationMs,
    bool NoSensitiveContent,
    string RedactionStatus,
    string SourcePreRunGateFile,
    int MarketDataSnapshotCount = 0,
    int MarketDataRequestRejectCount = 0,
    int BusinessMessageRejectCount = 0,
    int RejectCount = 0,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<string>? Errors = null);

public enum LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public enum LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification
{
    CompletedWithBook,
    CompletedWithEmptyBook,
    FailedSafe,
    UnsafeFail
}

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotClosureReview(
    LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision Decision,
    LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification Classification,
    LmaxReadOnlyAdditionalInstrumentManualSnapshotResult Result,
    IReadOnlyList<string> Issues);

public sealed record LmaxReadOnlyAdditionalInstrumentReplayReport(
    string ReplayStatus,
    int ObservationCount,
    int BlockingObservationCount,
    int WarningObservationCount,
    string MutationGuard,
    bool RuntimeShadowReplaySubmit,
    bool ExternalConnectionAttempted,
    bool NoSensitiveContent);

public static class LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator
{
    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OrderPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|submitorder|order submission)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<LmaxReadOnlyAdditionalInstrumentDefinition> SupportedInstruments { get; } =
    [
        new("GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1),
        new("EURGBP", "EUR/GBP", "4003", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1),
        new("USDJPY", "USD/JPY", "4004", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1),
        new("AUDUSD", "AUD/USD", "4007", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1)
    ];

    public static bool TryGetDefinition(string symbol, out LmaxReadOnlyAdditionalInstrumentDefinition definition)
    {
        definition = SupportedInstruments.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            ?? new("", "", "", "", "", "", "", "", 0);
        return !string.IsNullOrWhiteSpace(definition.Symbol);
    }

    public static LmaxReadOnlyAdditionalInstrumentManualSnapshotResult ReadResultJson(string artifactJson)
    {
        using var document = JsonDocument.Parse(artifactJson);
        var root = document.RootElement;
        var diagnostics = TryGetProperty(root, "diagnostics");
        var counters = diagnostics.HasValue ? TryGetProperty(diagnostics.Value, "messageCounters") : null;
        var status = GetString(root, "status");
        var completedAt = GetDateTime(root, "completedAtUtc") ?? DateTimeOffset.UtcNow;

        return new(
            GetString(root, "runId", "unknown-run"),
            GetDateTime(root, "startedAtUtc") ?? completedAt,
            completedAt,
            status,
            GetString(root, "symbol", GetString(root, "instrument")),
            GetString(root, "slashSymbol"),
            GetString(root, "securityId"),
            GetString(root, "securityIdSource", "8"),
            GetString(root, "environmentName", "Demo"),
            GetString(root, "venueProfileName", "DemoLondon"),
            GetString(root, "requestMode", "SnapshotPlusUpdates"),
            GetString(root, "symbolEncodingMode", "SecurityIdOnly"),
            GetInt(root, "marketDepth", 1),
            GetBool(root, "externalConnectionAttempted"),
            GetBool(root, "credentialReadAttempted"),
            GetBool(root, "credentialValuesReturned"),
            GetBool(root, "logonAttempted"),
            GetBool(root, "logonSucceeded"),
            GetBool(root, "snapshotRequestAttempted"),
            GetBool(root, "snapshotReceived") || GetBool(root, "marketDataSnapshotReceived"),
            GetBool(root, "logoutAttempted"),
            GetBool(root, "logoutSucceeded"),
            GetBool(root, "orderSubmissionAttempted"),
            GetBool(root, "shadowReplaySubmitAttempted"),
            GetBool(root, "tradingMutationAttempted"),
            GetBool(root, "schedulerStarted"),
            GetDouble(root, "bestBid"),
            GetDouble(root, "bestAsk"),
            GetDouble(root, "mid"),
            GetInt(root, "entryCount"),
            GetNullableLong(root, "waitDurationMs"),
            GetBool(root, "noSensitiveContent", true),
            GetString(root, "redactionStatus", "Redacted"),
            GetString(root, "sourceFinalReadinessFile", GetString(root, "sourceFinalPreRunGateFile")),
            GetInt(root, "marketDataSnapshotCount", GetCounter(counters, "MarketDataSnapshot", status is "Completed" or "CompletedWithEmptyBook" ? 1 : 0)),
            GetInt(root, "marketDataRequestRejectCount", GetCounter(counters, "MarketDataRequestReject")),
            GetInt(root, "businessMessageRejectCount", GetCounter(counters, "BusinessMessageReject")),
            GetInt(root, "rejectCount", GetCounter(counters, "Reject")),
            GetStringArray(root, "warnings"),
            GetStringArray(root, "errors"));
    }

    public static LmaxReadOnlyAdditionalInstrumentSnapshotClosureReview ReviewArtifact(
        LmaxReadOnlyAdditionalInstrumentManualSnapshotResult result,
        string rawArtifactText = "")
    {
        var issues = new List<string>();
        if (!TryGetDefinition(result.Symbol, out var definition))
        {
            issues.Add("UnsupportedSymbol: result symbol is not in the additional-instrument allowlist.");
            definition = new(result.Symbol, "", "", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1);
        }

        var completedWithBook = result.Status is "Completed" or "CompletedWithWarnings";
        var completedWithEmptyBook = result.Status == "CompletedWithEmptyBook";
        var safeFailure = result.Status.StartsWith("FailedSafe", StringComparison.OrdinalIgnoreCase)
            || result.Status.StartsWith("Blocked", StringComparison.OrdinalIgnoreCase);
        var errors = result.Errors ?? [];

        Add(issues, "SymbolMatchesAllowlist", result.Symbol == definition.Symbol && result.SlashSymbol == definition.SlashSymbol);
        Add(issues, "SecurityIdMatchesAllowlist", result.SecurityId == definition.SecurityId);
        Add(issues, "SecurityIdSource8", result.SecurityIdSource == "8");
        Add(issues, "DemoEnvironment", result.EnvironmentName == "Demo");
        Add(issues, "DemoLondonVenueProfile", result.VenueProfileName == "DemoLondon");
        Add(issues, "SnapshotPlusUpdates", result.RequestMode == "SnapshotPlusUpdates");
        Add(issues, "SecurityIdOnly", result.SymbolEncodingMode == "SecurityIdOnly");
        Add(issues, "MarketDepthOne", result.MarketDepth == 1);
        Add(issues, "KnownSafeStatus", completedWithBook || completedWithEmptyBook || safeFailure);
        Add(issues, "CompletedHasSnapshot", !completedWithBook || (result.SnapshotReceived && result.BestBid.HasValue && result.BestAsk.HasValue && result.Mid.HasValue && result.EntryCount > 0));
        Add(issues, "CompletedBookHasOneSnapshotAndNoRejects", !completedWithBook || (result.MarketDataSnapshotCount == 1 && result.MarketDataRequestRejectCount == 0 && result.BusinessMessageRejectCount == 0 && result.RejectCount == 0 && errors.Count == 0));
        Add(issues, "EmptyBookHasAcceptedSnapshot", !completedWithEmptyBook || (result.LogonSucceeded && result.SnapshotRequestAttempted && result.SnapshotReceived && result.EntryCount == 0));
        Add(issues, "EmptyBookHasNoTopOfBook", !completedWithEmptyBook || (!result.BestBid.HasValue && !result.BestAsk.HasValue && !result.Mid.HasValue));
        Add(issues, "EmptyBookHasOneSnapshotAndNoRejects", !completedWithEmptyBook || (result.MarketDataSnapshotCount == 1 && result.MarketDataRequestRejectCount == 0 && result.BusinessMessageRejectCount == 0 && result.RejectCount == 0 && errors.Count == 0));
        Add(issues, "NoOrderSubmission", !result.OrderSubmissionAttempted);
        Add(issues, "NoShadowReplaySubmit", !result.ShadowReplaySubmitAttempted);
        Add(issues, "NoTradingMutation", !result.TradingMutationAttempted);
        Add(issues, "NoScheduler", !result.SchedulerStarted);
        Add(issues, "CredentialValuesNotReturned", !result.CredentialValuesReturned);
        Add(issues, "NoSensitiveContent", result.NoSensitiveContent);
        Add(issues, "Redacted", result.RedactionStatus == "Redacted");
        Add(issues, "SourcePreRunGatePresent", !string.IsNullOrWhiteSpace(result.SourcePreRunGateFile));

        var scanText = NormalizeSafeCredentialMetadata(rawArtifactText);
        if (SensitivePattern.IsMatch(scanText))
        {
            issues.Add("NoSensitiveArtifactText: artifact contains credential-shaped content.");
        }

        if (OrderPattern.IsMatch(scanText))
        {
            issues.Add("NoOrderSurfaceArtifactText: artifact contains order/trading message surface.");
        }

        var classification = issues.Count > 0
            ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.UnsafeFail
            : completedWithEmptyBook
                ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithEmptyBook
                : safeFailure
                    ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.FailedSafe
                    : LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithBook;
        var decision = classification switch
        {
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithBook => LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS,
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithEmptyBook => LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.FailedSafe => LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            _ => LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL
        };

        return new(decision, classification, result, issues);
    }

    public static LmaxReadOnlyAdditionalInstrumentSnapshotClosureReview ReviewArtifactJson(string artifactJson)
        => ReviewArtifact(ReadResultJson(artifactJson), artifactJson);

    public static LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision ValidateMarketDataOnlyPreviewJson(
        string previewJson,
        string symbol,
        bool expectEmptyBookWarning = false)
    {
        if (!TryGetDefinition(symbol, out var definition))
        {
            return LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL;
        }

        using var document = JsonDocument.Parse(previewJson);
        var root = document.RootElement;
        var marketData = TryGetProperty(root, "marketData");
        var warnings = GetStringArray(root, "warnings");
        var nonMarketArraysEmpty = GetArrayCount(root, "executionReports") == 0
            && GetArrayCount(root, "orderStatuses") == 0
            && GetArrayCount(root, "tradeCaptureReports") == 0
            && GetArrayCount(root, "protocolRejects") == 0;

        var valid = GetString(root, "evidenceMode") == "MarketDataOnly"
            && GetString(root, "instrument") == definition.Symbol
            && GetString(root, "slashSymbol") == definition.SlashSymbol
            && GetString(root, "securityId") == definition.SecurityId
            && GetString(root, "redactionStatus") == "Redacted"
            && GetBool(root, "noSensitiveContent")
            && !GetBool(root, "shadowReplaySubmitAttempted")
            && !GetBool(root, "tradingMutationAttempted")
            && !GetBool(root, "orderSubmissionAttempted")
            && nonMarketArraysEmpty
            && marketData.HasValue
            && GetBool(marketData.Value, "snapshotReceived");

        var previewScanText = previewJson
            .Replace("orderStatuses", "SAFE_EMPTY_ARRAY", StringComparison.OrdinalIgnoreCase)
            .Replace("orderSubmissionAttempted", "SAFE_FALSE_FLAG", StringComparison.OrdinalIgnoreCase)
            .Replace("tradeCaptureReports", "SAFE_EMPTY_ARRAY", StringComparison.OrdinalIgnoreCase);
        if (!valid || SensitivePattern.IsMatch(previewScanText) || OrderPattern.IsMatch(previewScanText))
        {
            return LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL;
        }

        if (expectEmptyBookWarning)
        {
            return GetString(marketData!.Value, "status") == "EmptyBook"
                && GetInt(marketData.Value, "entryCount") == 0
                && warnings.Count > 0
                ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL;
        }

        return GetInt(marketData!.Value, "entryCount") > 0
            && GetDouble(marketData.Value, "bestBid").HasValue
            && GetDouble(marketData.Value, "bestAsk").HasValue
            && GetDouble(marketData.Value, "mid").HasValue
            ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS
            : LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL;
    }

    public static LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision ValidateReplayReport(LmaxReadOnlyAdditionalInstrumentReplayReport report)
        => report.ReplayStatus == "Completed"
            && report.ObservationCount == 0
            && report.BlockingObservationCount == 0
            && report.WarningObservationCount == 0
            && report.MutationGuard == "Unchanged"
            && !report.RuntimeShadowReplaySubmit
            && !report.ExternalConnectionAttempted
            && report.NoSensitiveContent
                ? LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS
                : LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL;

    private static void Add(List<string> issues, string name, bool pass)
    {
        if (!pass)
        {
            issues.Add($"{name}: check failed.");
        }
    }

    private static string NormalizeSafeCredentialMetadata(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var text = rawText;
        foreach (var safeLabel in new[]
        {
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
            "credentialProfileName"
        })
        {
            text = text.Replace(safeLabel, "SAFE_METADATA", StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private static JsonElement? TryGetProperty(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value : null;

    private static string GetString(JsonElement root, string name, string fallback = "")
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static int GetInt(JsonElement root, string name, int fallback = 0)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static int GetCounter(JsonElement? counters, string name, int fallback = 0)
        => counters.HasValue ? GetInt(counters.Value, name, fallback) : fallback;

    private static long? GetNullableLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.TryGetInt64(out var number) ? number : null;
    }

    private static double? GetDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.TryGetDouble(out var number) ? number : null;
    }

    private static bool GetBool(JsonElement root, string name, bool fallback = false)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static DateTimeOffset? GetDateTime(JsonElement root, string name)
        => root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var timestamp)
                ? timestamp
                : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray().Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private static int GetArrayCount(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
}
