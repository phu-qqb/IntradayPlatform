using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdManualSnapshotResultDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdManualSnapshotResult(
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
    string SourceFinalReadinessFile,
    int MarketDataSnapshotCount = 0,
    int MarketDataRequestRejectCount = 0,
    int BusinessMessageRejectCount = 0,
    int RejectCount = 0,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<string>? Errors = null);

public sealed record LmaxReadOnlyGbpusdManualSnapshotResultCheck(
    string Name,
    LmaxReadOnlyGbpusdManualSnapshotResultDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdManualSnapshotResultValidation(
    LmaxReadOnlyGbpusdManualSnapshotResultDecision FinalDecision,
    LmaxReadOnlyGbpusdManualSnapshotResult Result,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotResultCheck> Checks);

public sealed record LmaxReadOnlyGbpusdManualSnapshotResultReview(
    LmaxReadOnlyGbpusdManualSnapshotResultDecision FinalDecision,
    int ResultCount,
    int PassCount,
    int WarningCount,
    int FailCount,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotResultValidation> Validations);

public static class LmaxReadOnlyGbpusdManualSnapshotResultValidator
{
    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OrderPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|submitorder|order submission)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdManualSnapshotResultValidation Validate(LmaxReadOnlyGbpusdManualSnapshotResult result, string rawArtifactText = "")
    {
        var completedWithBook = result.Status is "Completed" or "CompletedWithWarnings";
        var completedWithEmptyBook = result.Status == "CompletedWithEmptyBook";
        var safeFailure = result.Status.StartsWith("FailedSafe", StringComparison.OrdinalIgnoreCase) || result.Status.StartsWith("Blocked", StringComparison.OrdinalIgnoreCase);
        var errors = result.Errors ?? [];
        var checks = new List<LmaxReadOnlyGbpusdManualSnapshotResultCheck>
        {
            Check("GbpusdOnly", result.Symbol == "GBPUSD" && result.SlashSymbol == "GBP/USD", "Result must be for GBPUSD / GBP/USD."),
            Check("SecurityId4002", result.SecurityId == "4002", "GBPUSD SecurityID must be 4002."),
            Check("SecurityIdSource8", result.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", result.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", result.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("SnapshotPlusUpdates", result.RequestMode == "SnapshotPlusUpdates", "RequestMode must be SnapshotPlusUpdates."),
            Check("SecurityIdOnly", result.SymbolEncodingMode == "SecurityIdOnly", "SymbolEncodingMode must be SecurityIdOnly."),
            Check("MarketDepthOne", result.MarketDepth == 1, "MarketDepth must be 1."),
            Check("KnownSafeStatus", completedWithBook || completedWithEmptyBook || safeFailure, "Status must be completed, completed empty-book, or failed-safe/blocked."),
            Check("CompletedHasSnapshot", !completedWithBook || (result.SnapshotReceived && result.BestBid.HasValue && result.BestAsk.HasValue && result.Mid.HasValue && result.EntryCount > 0), "Completed result must include top-of-book snapshot data."),
            Check("CompletedBookHasOneSnapshotAndNoRejects", !completedWithBook || (result.MarketDataSnapshotCount == 1 && result.MarketDataRequestRejectCount == 0 && result.BusinessMessageRejectCount == 0 && result.RejectCount == 0 && errors.Count == 0), "Completed result must have one MarketDataSnapshot and no FIX rejects/errors."),
            Check("EmptyBookHasAcceptedSnapshot", !completedWithEmptyBook || (result.LogonSucceeded && result.SnapshotRequestAttempted && result.SnapshotReceived && result.EntryCount == 0), "CompletedWithEmptyBook must have logon, request, received snapshot, and zero entries."),
            Check("EmptyBookHasNoTopOfBook", !completedWithEmptyBook || (!result.BestBid.HasValue && !result.BestAsk.HasValue && !result.Mid.HasValue), "CompletedWithEmptyBook must not contain bid/ask/mid values."),
            Check("EmptyBookHasOneSnapshotAndNoRejects", !completedWithEmptyBook || (result.MarketDataSnapshotCount == 1 && result.MarketDataRequestRejectCount == 0 && result.BusinessMessageRejectCount == 0 && result.RejectCount == 0 && errors.Count == 0), "CompletedWithEmptyBook must have one MarketDataSnapshot and no FIX rejects/errors."),
            Check("NoOrderSubmission", !result.OrderSubmissionAttempted, "Order submission must be false."),
            Check("NoShadowReplaySubmit", !result.ShadowReplaySubmitAttempted, "Shadow replay submit must be false."),
            Check("NoTradingMutation", !result.TradingMutationAttempted, "Trading mutation must be false."),
            Check("NoScheduler", !result.SchedulerStarted, "Scheduler must be false."),
            Check("CredentialValuesNotReturned", !result.CredentialValuesReturned, "Credential values must not be returned."),
            Check("NoSensitiveContent", result.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("Redacted", result.RedactionStatus == "Redacted", "redactionStatus must be Redacted."),
            Check("SourceFinalReadinessPresent", !string.IsNullOrWhiteSpace(result.SourceFinalReadinessFile), "sourceFinalReadinessFile must be present.")
        };
        if (SensitivePattern.IsMatch(rawArtifactText)) checks.Add(Fail("NoSensitiveArtifactText", "Artifact contains credential-shaped content."));
        if (OrderPattern.IsMatch(rawArtifactText)) checks.Add(Fail("NoOrderSurfaceArtifactText", "Artifact contains order/trading message surface."));
        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL)
            ? LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL
            : completedWithEmptyBook
                ? LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS;
        return new(final, result, checks);
    }

    public static LmaxReadOnlyGbpusdManualSnapshotResultReview Review(IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotResultValidation> validations)
    {
        var failCount = validations.Count(x => x.FinalDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL);
        var warningCount = validations.Count(x => x.FinalDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS_WITH_KNOWN_WARNINGS);
        var passCount = validations.Count(x => x.FinalDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS);
        var final = failCount > 0
            ? LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL
            : warningCount > 0
                ? LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS;
        return new(final, validations.Count, passCount, warningCount, failCount, validations);
    }

    private static LmaxReadOnlyGbpusdManualSnapshotResultCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdManualSnapshotResultCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, detail);
}
