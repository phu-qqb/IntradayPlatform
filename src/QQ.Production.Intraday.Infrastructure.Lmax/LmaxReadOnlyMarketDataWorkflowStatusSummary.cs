using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyMarketDataWorkflowOperationalStatus
{
    NotAvailable,
    FrozenManualReadOnly,
    Pass,
    PassWithWarnings,
    Fail
}

public enum LmaxReadOnlyMarketDataWorkflowStatusSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyMarketDataWorkflowStatusIssue(
    LmaxReadOnlyMarketDataWorkflowStatusSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyMarketDataWorkflowStatusSummary(
    string SummaryId,
    DateTimeOffset CreatedAtUtc,
    string SignoffDecision,
    string AuditPackDecision,
    string GateDecision,
    int ArtifactCount,
    int EvidencePreviewCount,
    int ManualReplayCount,
    int TotalObservationCount,
    bool RuntimeShadowReplaySubmit,
    bool ExternalConnectionAttempted,
    bool CredentialValuesReturned,
    bool OrderSubmissionAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    string ApiWorkerGatewayMode,
    bool WorkflowFrozen,
    LmaxReadOnlyMarketDataWorkflowOperationalStatus OperationalStatus,
    IReadOnlyList<string> WhatIsAllowed,
    IReadOnlyList<string> WhatIsNotAllowed,
    bool NoSensitiveContent,
    IReadOnlyList<LmaxReadOnlyMarketDataWorkflowStatusIssue> Issues);

public sealed record LmaxReadOnlyMarketDataWorkflowStatusSummaryResult(
    LmaxReadOnlyMarketDataWorkflowOperationalStatus OperationalStatus,
    LmaxReadOnlyMarketDataWorkflowStatusSummary Summary,
    IReadOnlyList<LmaxReadOnlyMarketDataWorkflowStatusIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyMarketDataWorkflowStatusIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyMarketDataWorkflowStatusSeverity.Error).ToList();
}

public static class LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator
{
    private static readonly Regex[] ForbiddenPatterns =
    [
        new("(?i)554\\s*=", RegexOptions.Compiled),
        new("(?i)553\\s*=", RegexOptions.Compiled),
        new("(?i)password\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)secret\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)token\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)apiKey\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)privateKey\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)rawFix", RegexOptions.Compiled),
        new("(?i)NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest|TradeCapture", RegexOptions.Compiled)
    ];

    public static LmaxReadOnlyMarketDataWorkflowStatusSummaryResult FromSignoffFile(
        string? signoffFile,
        string apiWorkerGatewayMode = "FakeLmaxGateway",
        string? gateDecision = null,
        DateTimeOffset? createdAtUtc = null,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(signoffFile) || !File.Exists(signoffFile))
        {
            var issue = Warning("SignoffNotAvailable", "$.signoffFile", "Phase 5W signoff file was not found.");
            var unavailableSummary = CreateDefaultSummary(now, "NotAvailable", "NotAvailable", gateDecision ?? "NotAvailable", 0, 0, 0, 0, apiWorkerGatewayMode, false, LmaxReadOnlyMarketDataWorkflowOperationalStatus.NotAvailable, [issue]);
            return new(LmaxReadOnlyMarketDataWorkflowOperationalStatus.NotAvailable, unavailableSummary, unavailableSummary.Issues);
        }

        var json = File.ReadAllText(signoffFile);
        var issues = new List<LmaxReadOnlyMarketDataWorkflowStatusIssue>();
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var signoffDecision = GetString(root, "finalDecision");
        var auditPackDecision = GetString(root, "auditPackFinalDecision");
        var artifactCount = GetInt(root, "artifactCount");
        var evidencePreviewCount = GetInt(root, "evidencePreviewCount");
        var manualReplayCount = GetInt(root, "manualReplayCount");
        var totalObservationCount = GetInt(root, "totalObservationCount");
        var runtimeShadowReplaySubmit = GetBoolean(root, "runtimeShadowReplaySubmit");
        var externalConnectionAttempted = GetBoolean(root, "externalConnectionAttempted");
        var credentialValuesReturned = GetBoolean(root, "credentialValuesReturned");
        var orderSubmissionAttempted = GetBoolean(root, "orderSubmissionAttempted");
        var tradingMutationAttempted = GetBoolean(root, "tradingMutationAttempted");
        var schedulerStarted = GetBoolean(root, "schedulerStarted");
        var noSensitiveContent = GetBoolean(root, "noSensitiveContent");

        if (!string.Equals(signoffDecision, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("SignoffDecisionNotPass", "$.finalDecision", "Frozen workflow status requires Phase 5W signoff finalDecision=PASS."));
        }

        if (!string.Equals(auditPackDecision, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("AuditPackDecisionNotPass", "$.auditPackFinalDecision", "Frozen workflow status requires Phase 5V audit pack decision=PASS."));
        }

        if (artifactCount <= 0)
        {
            issues.Add(Error("ArtifactCountMissing", "$.artifactCount", "ArtifactCount must be greater than zero."));
        }

        if (evidencePreviewCount != artifactCount)
        {
            issues.Add(Error("PreviewCountMismatch", "$.evidencePreviewCount", "EvidencePreviewCount must equal ArtifactCount."));
        }

        if (manualReplayCount != evidencePreviewCount)
        {
            issues.Add(Error("ReplayCountMismatch", "$.manualReplayCount", "ManualReplayCount must equal EvidencePreviewCount."));
        }

        if (totalObservationCount != 0)
        {
            issues.Add(Error("ObservationCountNonZero", "$.totalObservationCount", "TotalObservationCount must be zero."));
        }

        RequireFalse(runtimeShadowReplaySubmit, "$.runtimeShadowReplaySubmit", "RuntimeShadowReplaySubmit must remain false.", issues);
        RequireFalse(externalConnectionAttempted, "$.externalConnectionAttempted", "ExternalConnectionAttempted must remain false for the workflow status review.", issues);
        RequireFalse(credentialValuesReturned, "$.credentialValuesReturned", "CredentialValuesReturned must remain false.", issues);
        RequireFalse(orderSubmissionAttempted, "$.orderSubmissionAttempted", "OrderSubmissionAttempted must remain false.", issues);
        RequireFalse(tradingMutationAttempted, "$.tradingMutationAttempted", "TradingMutationAttempted must remain false.", issues);
        RequireFalse(schedulerStarted, "$.schedulerStarted", "SchedulerStarted must remain false.", issues);
        if (!noSensitiveContent)
        {
            issues.Add(Error("SensitiveContentFlagFalse", "$.noSensitiveContent", "noSensitiveContent must be true."));
        }

        var status = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowStatusSeverity.Error)
            ? LmaxReadOnlyMarketDataWorkflowOperationalStatus.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowStatusSeverity.Warning)
                ? LmaxReadOnlyMarketDataWorkflowOperationalStatus.PassWithWarnings
                : LmaxReadOnlyMarketDataWorkflowOperationalStatus.FrozenManualReadOnly;

        var summary = CreateDefaultSummary(
            now,
            signoffDecision,
            auditPackDecision,
            gateDecision ?? "PASS",
            artifactCount,
            evidencePreviewCount,
            manualReplayCount,
            totalObservationCount,
            apiWorkerGatewayMode,
            true,
            status,
            issues,
            runtimeShadowReplaySubmit,
            externalConnectionAttempted,
            credentialValuesReturned,
            orderSubmissionAttempted,
            tradingMutationAttempted,
            schedulerStarted,
            noSensitiveContent);

        return new(status, summary, issues);
    }

    private static LmaxReadOnlyMarketDataWorkflowStatusSummary CreateDefaultSummary(
        DateTimeOffset createdAtUtc,
        string signoffDecision,
        string auditPackDecision,
        string gateDecision,
        int artifactCount,
        int evidencePreviewCount,
        int manualReplayCount,
        int totalObservationCount,
        string apiWorkerGatewayMode,
        bool workflowFrozen,
        LmaxReadOnlyMarketDataWorkflowOperationalStatus operationalStatus,
        IReadOnlyList<LmaxReadOnlyMarketDataWorkflowStatusIssue> issues,
        bool runtimeShadowReplaySubmit = false,
        bool externalConnectionAttempted = false,
        bool credentialValuesReturned = false,
        bool orderSubmissionAttempted = false,
        bool tradingMutationAttempted = false,
        bool schedulerStarted = false,
        bool noSensitiveContent = true)
        => new(
            Guid.NewGuid().ToString("D"),
            createdAtUtc,
            signoffDecision,
            auditPackDecision,
            gateDecision,
            artifactCount,
            evidencePreviewCount,
            manualReplayCount,
            totalObservationCount,
            runtimeShadowReplaySubmit,
            externalConnectionAttempted,
            credentialValuesReturned,
            orderSubmissionAttempted,
            tradingMutationAttempted,
            schedulerStarted,
            apiWorkerGatewayMode,
            workflowFrozen,
            operationalStatus,
            ["Manual Demo MarketData workflow review", "Artifact, evidence preview, and replay result inspection"],
            ["Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument expansion"],
            noSensitiveContent,
            issues);

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyMarketDataWorkflowStatusIssue> issues)
    {
        var scanText = json
            .Replace("credentialValuesReturned", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("shadowReplaySubmitAttempted", string.Empty, StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in ForbiddenPatterns)
        {
            if (pattern.IsMatch(scanText))
            {
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Status summary source contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var value in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value) && json.Contains(value, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Status summary source contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void RequireFalse(bool value, string path, string message, List<LmaxReadOnlyMarketDataWorkflowStatusIssue> issues)
    {
        if (value)
        {
            issues.Add(Error("UnsafeBooleanFlag", path, message));
        }
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static LmaxReadOnlyMarketDataWorkflowStatusIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataWorkflowStatusSeverity.Error, code, path, message);

    private static LmaxReadOnlyMarketDataWorkflowStatusIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataWorkflowStatusSeverity.Warning, code, path, message);
}
