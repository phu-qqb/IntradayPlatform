using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyMarketHoursNextActionDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyMarketHoursNextActionInstrument(
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth);

public sealed record LmaxReadOnlyMarketHoursNextActionPreviousAttempt(
    string Status,
    bool OutsideMarketHours,
    bool Safe,
    bool SnapshotReceived,
    int EntryCount,
    string WarningClassification);

public sealed record LmaxReadOnlyMarketHoursNextActionSourceArtifacts(
    string FinalReadinessFile,
    string MarketHoursRetryReadinessFile,
    string Phase6XReviewFile,
    string DocumentationPackFile);

public sealed record LmaxReadOnlyMarketHoursNextActionIssue(
    string Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyMarketHoursNextActionSummary(
    string SummaryId,
    DateTimeOffset CreatedAtUtc,
    string RecommendedAction,
    string Status,
    LmaxReadOnlyMarketHoursNextActionInstrument SelectedInstrument,
    LmaxReadOnlyMarketHoursNextActionSourceArtifacts SourceArtifacts,
    LmaxReadOnlyMarketHoursNextActionPreviousAttempt PreviousAttempt,
    string FinalReadinessDecision,
    string MarketHoursRetryReadinessDecision,
    string Phase6XReviewDecision,
    string DocumentationPackDecision,
    int ExecutableCount,
    bool IsApprovedForExternalRun,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPolling,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    IReadOnlyList<string> WhatIsAllowed,
    IReadOnlyList<string> WhatIsNotAllowed,
    bool NoSensitiveContent,
    IReadOnlyList<LmaxReadOnlyMarketHoursNextActionIssue> Issues);

public sealed record LmaxReadOnlyMarketHoursNextActionValidation(
    LmaxReadOnlyMarketHoursNextActionDecision FinalDecision,
    LmaxReadOnlyMarketHoursNextActionSummary Summary,
    IReadOnlyList<LmaxReadOnlyMarketHoursNextActionIssue> Issues);

public static class LmaxReadOnlyMarketHoursNextActionSummaryValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyMarketHoursNextActionValidation FromArtifactFiles(
        string? finalReadinessFile,
        string? marketHoursRetryReadinessFile,
        string? phase6XReviewFile,
        string? documentationPackFile,
        string apiWorkerGatewayMode)
    {
        var issues = new List<LmaxReadOnlyMarketHoursNextActionIssue>();
        using var finalReadiness = ReadJson(finalReadinessFile, "FinalReadinessFile", issues);
        using var retryReadiness = ReadJson(marketHoursRetryReadinessFile, "MarketHoursRetryReadinessFile", issues);
        using var review = ReadJson(phase6XReviewFile, "Phase6XReviewFile", issues);
        using var docPack = ReadJson(documentationPackFile, "DocumentationPackFile", issues);

        if (finalReadiness is null || retryReadiness is null || review is null || docPack is null)
        {
            return MissingSummary(finalReadinessFile, marketHoursRetryReadinessFile, phase6XReviewFile, documentationPackFile, apiWorkerGatewayMode, issues);
        }

        return FromArtifacts(
            finalReadiness.RootElement,
            finalReadinessFile ?? "",
            retryReadiness.RootElement,
            marketHoursRetryReadinessFile ?? "",
            review.RootElement,
            phase6XReviewFile ?? "",
            docPack.RootElement,
            documentationPackFile ?? "",
            apiWorkerGatewayMode,
            issues);
    }

    public static LmaxReadOnlyMarketHoursNextActionValidation FromArtifacts(
        JsonElement finalReadiness,
        string finalReadinessFile,
        JsonElement retryReadiness,
        string marketHoursRetryReadinessFile,
        JsonElement phase6XReview,
        string phase6XReviewFile,
        JsonElement documentationPack,
        string documentationPackFile,
        string apiWorkerGatewayMode,
        IReadOnlyCollection<LmaxReadOnlyMarketHoursNextActionIssue>? initialIssues = null)
    {
        var issues = initialIssues?.ToList() ?? new List<LmaxReadOnlyMarketHoursNextActionIssue>();
        CheckIdentity(finalReadiness, "planningSecurityId", "FinalReadiness", issues);
        CheckIdentity(retryReadiness, "securityId", "RetryReadiness", issues);
        CheckIdentity(phase6XReview, "securityId", "Phase6XReview", issues);

        var finalReadinessDecision = GetString(finalReadiness, "readinessDecision");
        var retryDecision = GetString(retryReadiness, "decision");
        var reviewDecision = GetString(phase6XReview, "finalDecision");
        var docPackDecision = GetString(documentationPack, "finalDecision");

        if (finalReadinessDecision != "PASS") issues.Add(Error("FinalReadinessNotPass", finalReadinessFile, "Final readiness must be PASS."));
        if (retryDecision != "PASS") issues.Add(Error("RetryReadinessNotPass", marketHoursRetryReadinessFile, "Market-hours retry readiness must be PASS."));
        if (reviewDecision != "PASS_WITH_KNOWN_WARNINGS") issues.Add(Error("Phase6XReviewNotExpectedWarning", phase6XReviewFile, "Phase 6X review must be the safe CompletedWithEmptyBook warning state."));
        if (docPackDecision != "PASS") issues.Add(Error("DocumentationPackNotPass", documentationPackFile, "Phase 6Z-D documentation pack must be PASS."));

        if (GetString(phase6XReview, "status") != "CompletedWithEmptyBook")
        {
            issues.Add(Error("PreviousStatusNotCompletedWithEmptyBook", phase6XReviewFile, "Previous GBPUSD result must be CompletedWithEmptyBook."));
        }

        if (!GetBool(retryReadiness, "previousAttemptWasOutsideMarketHours"))
        {
            issues.Add(Error("PreviousAttemptNotOutsideMarketHours", marketHoursRetryReadinessFile, "Previous attempt must be documented as outside market hours."));
        }

        if (!GetBool(retryReadiness, "retryIsManualOnly") || !GetBool(retryReadiness, "retryAllowedOnlyDuringMarketHours") || GetBool(retryReadiness, "canRunAutomatically"))
        {
            issues.Add(Error("RetryNotManualMarketHoursOnly", marketHoursRetryReadinessFile, "Retry must be manual-only, market-hours-only, and not automatic."));
        }

        if (GetInt(documentationPack, "executableCount") != 0)
        {
            issues.Add(Error("ExecutableCountNonZero", documentationPackFile, "Documentation pack executableCount must be 0."));
        }

        CheckFalse(finalReadiness, finalReadinessFile, issues, "isApprovedForExternalRun", "eligibleForManualSnapshotAttempt", "canRunExternalSnapshot", "runtimeShadowReplaySubmit", "orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted", "schedulerStarted");
        CheckFalse(retryReadiness, marketHoursRetryReadinessFile, issues, "canRunAutomatically", "externalConnectionAttempted", "snapshotAttempted", "replayAttempted", "schedulerStarted", "orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted");
        CheckFalse(phase6XReview, phase6XReviewFile, issues, "orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted", "schedulerStarted", "credentialValuesReturned");
        CheckFalse(documentationPack, documentationPackFile, issues, "isApprovedForExternalRun", "canRunExternalSnapshot", "eligibleForManualSnapshotAttempt", "runtimeShadowReplaySubmit", "schedulerOrPolling", "orderSubmission", "gatewayRegistration", "tradingMutation", "externalConnectionAttempted", "snapshotAttempted", "replayAttempted");

        if (!GetBool(finalReadiness, "noSensitiveContent") || !GetBool(retryReadiness, "noSensitiveContent") || !GetBool(phase6XReview, "noSensitiveContent") || !GetBool(documentationPack, "noSensitiveContent"))
        {
            issues.Add(Error("NoSensitiveContentFalse", "", "All source artifacts must report noSensitiveContent=true."));
        }

        if (!string.Equals(apiWorkerGatewayMode, "FakeLmaxGateway", StringComparison.Ordinal))
        {
            issues.Add(Error("ApiWorkerGatewayNotFake", "", "API/Worker gateway mode must remain FakeLmaxGateway."));
        }

        var finalDecision = issues.Any(x => x.Severity == "Error")
            ? LmaxReadOnlyMarketHoursNextActionDecision.FAIL
            : LmaxReadOnlyMarketHoursNextActionDecision.PASS;

        var summary = new LmaxReadOnlyMarketHoursNextActionSummary(
            $"lmax-readonly-market-hours-next-action-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
            DateTimeOffset.UtcNow,
            "OperatorApprovedGbpusdMarketHoursSnapshotAttempt",
            "ReadyForManualMarketHoursAttemptPlanningOnly",
            new("GBPUSD", "GBP/USD", "4002", "8", "SnapshotPlusUpdates", "SecurityIdOnly", 1),
            new(finalReadinessFile, marketHoursRetryReadinessFile, phase6XReviewFile, documentationPackFile),
            new(
                GetString(phase6XReview, "status"),
                OutsideMarketHours: GetBool(retryReadiness, "previousAttemptWasOutsideMarketHours"),
                Safe: reviewDecision == "PASS_WITH_KNOWN_WARNINGS" && GetString(phase6XReview, "status") == "CompletedWithEmptyBook",
                SnapshotReceived: GetBool(phase6XReview, "snapshotReceived"),
                EntryCount: GetInt(phase6XReview, "entryCount"),
                WarningClassification: GetString(phase6XReview, "warningClassification")),
            finalReadinessDecision,
            retryDecision,
            reviewDecision,
            docPackDecision,
            ExecutableCount: 0,
            IsApprovedForExternalRun: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPolling: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            apiWorkerGatewayMode,
            new[] { "Review readiness", "Inspect artifacts", "Wait for market hours" },
            new[] { "Run now from UI", "Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument batch" },
            NoSensitiveContent: issues.All(x => x.Code != "SensitiveContentDetected"),
            issues);

        return new(finalDecision, summary, issues);
    }

    private static JsonDocument? ReadJson(string? path, string label, ICollection<LmaxReadOnlyMarketHoursNextActionIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            issues.Add(new("Warning", $"{label}Missing", path ?? "", $"{label} was not found."));
            return null;
        }

        var raw = File.ReadAllText(path);
        if (SensitivePattern.IsMatch(raw))
        {
            issues.Add(Error("SensitiveContentDetected", path, $"{label} contains credential-shaped content."));
        }

        return JsonDocument.Parse(raw);
    }

    private static LmaxReadOnlyMarketHoursNextActionValidation MissingSummary(
        string? finalReadinessFile,
        string? marketHoursRetryReadinessFile,
        string? phase6XReviewFile,
        string? documentationPackFile,
        string apiWorkerGatewayMode,
        IReadOnlyList<LmaxReadOnlyMarketHoursNextActionIssue> issues)
    {
        var summary = new LmaxReadOnlyMarketHoursNextActionSummary(
            "lmax-readonly-market-hours-next-action-missing",
            DateTimeOffset.UtcNow,
            "OperatorApprovedGbpusdMarketHoursSnapshotAttempt",
            "NotAvailable",
            new("GBPUSD", "GBP/USD", "4002", "8", "SnapshotPlusUpdates", "SecurityIdOnly", 1),
            new(finalReadinessFile ?? "", marketHoursRetryReadinessFile ?? "", phase6XReviewFile ?? "", documentationPackFile ?? ""),
            new("NotAvailable", OutsideMarketHours: true, Safe: false, SnapshotReceived: false, EntryCount: 0, WarningClassification: "NotAvailable"),
            "NotAvailable",
            "NotAvailable",
            "NotAvailable",
            "NotAvailable",
            ExecutableCount: 0,
            IsApprovedForExternalRun: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPolling: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            apiWorkerGatewayMode,
            new[] { "Review readiness", "Inspect artifacts", "Wait for market hours" },
            new[] { "Run now from UI", "Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument batch" },
            NoSensitiveContent: true,
            issues);

        return new(LmaxReadOnlyMarketHoursNextActionDecision.PASS_WITH_KNOWN_WARNINGS, summary, issues);
    }

    private static void CheckIdentity(JsonElement artifact, string securityIdProperty, string label, ICollection<LmaxReadOnlyMarketHoursNextActionIssue> issues)
    {
        if (GetString(artifact, "symbol") != "GBPUSD" ||
            GetString(artifact, "slashSymbol") != "GBP/USD" ||
            GetString(artifact, securityIdProperty) != "4002" ||
            GetString(artifact, "securityIdSource") != "8")
        {
            issues.Add(Error($"{label}IdentityMismatch", "", $"{label} must refer to GBPUSD / GBP/USD / 4002 / SecurityIDSource 8."));
        }
    }

    private static void CheckFalse(JsonElement artifact, string path, ICollection<LmaxReadOnlyMarketHoursNextActionIssue> issues, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (GetBool(artifact, property))
            {
                issues.Add(Error($"{property}True", path, $"{property} must be false."));
            }
        }
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";

    private static bool GetBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static LmaxReadOnlyMarketHoursNextActionIssue Error(string code, string path, string message)
        => new("Error", code, path, message);
}
