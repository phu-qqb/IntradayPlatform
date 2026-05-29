using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyMarketDataWorkflowAuditPackDecision
{
    Pass,
    PassWithKnownWarnings,
    Fail
}

public enum LmaxReadOnlyMarketDataWorkflowAuditPackSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyMarketDataWorkflowAuditPackIssue(
    LmaxReadOnlyMarketDataWorkflowAuditPackSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyMarketDataWorkflowAuditPackResult(
    LmaxReadOnlyMarketDataWorkflowAuditPackDecision Decision,
    int ArtifactCount,
    int EvidencePreviewCount,
    int ManualReplayCount,
    int TotalObservationCount,
    IReadOnlyList<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Error).ToList();

    public IReadOnlyList<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Warning).ToList();
}

public static class LmaxReadOnlyMarketDataWorkflowAuditPackValidator
{
    private static readonly Regex[] ForbiddenContentPatterns =
    [
        new("(?i)554\\s*=", RegexOptions.Compiled),
        new("(?i)553\\s*=", RegexOptions.Compiled),
        new("(?i)password\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)secret\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)token\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)apiKey\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)privateKey\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)bearer\\s+(?!\\[REDACTED\\])[^\\s,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)authorization\\s*[:=]\\s*(?!\\[REDACTED\\])[^\\r\\n,;\\\"}]+", RegexOptions.Compiled),
        new("(?i)rawFix", RegexOptions.Compiled),
        new("(?i)NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest", RegexOptions.Compiled)
    ];

    private static readonly string[] AllowedCredentialLabelTokens =
    [
        "credentialValuesReturned",
        "LMAX_DEMO_FIX_USERNAME",
        "LMAX_DEMO_FIX_PASSWORD",
        "LMAX_DEMO_SENDER_COMP_ID",
        "LMAX_DEMO_TARGET_COMP_ID"
    ];

    public static LmaxReadOnlyMarketDataWorkflowAuditPackResult ValidateFile(
        string auditPackFile,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        if (string.IsNullOrWhiteSpace(auditPackFile) || !File.Exists(auditPackFile))
        {
            return Result([Error("AuditPackFileMissing", "$", "Audit pack file is required and must exist.")], 0, 0, 0, 0);
        }

        return ValidateJson(File.ReadAllText(auditPackFile), forbiddenSensitiveValues);
    }

    public static LmaxReadOnlyMarketDataWorkflowAuditPackResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue>();
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!string.Equals(GetString(root, "phase"), "5V", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("UnexpectedPhase", "$.phase", "Audit pack phase must be 5V."));
        }

        if (!string.Equals(GetString(root, "finalDecision"), "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("FinalDecisionNotPass", "$.finalDecision", "Final audit pack requires finalDecision=PASS."));
        }

        Require(root, "runtimeShadowReplaySubmit", false, issues);
        Require(root, "externalConnectionAttempted", false, issues);
        Require(root, "orderSubmissionAttempted", false, issues);
        Require(root, "shadowReplaySubmitAttempted", false, issues);
        Require(root, "tradingMutationAttempted", false, issues);
        Require(root, "schedulerStarted", false, issues);
        Require(root, "credentialValuesReturned", false, issues);
        Require(root, "noSensitiveContent", true, issues);

        if (!string.Equals(GetString(root, "redactionStatus"), "Redacted", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Audit pack must report redactionStatus=Redacted."));
        }

        var stabilityDecision = GetString(root, "stabilityDecision");
        if (!string.Equals(stabilityDecision, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("StabilityDecisionNotPass", "$.stabilityDecision", "Stability summary must validate as PASS."));
        }

        var workflowDecision = GetString(root, "workflowFinalDecision");
        if (!string.Equals(workflowDecision, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("WorkflowDecisionNotPass", "$.workflowFinalDecision", "Replay-enabled workflow manifest must have finalDecision=PASS."));
        }

        var artifactCount = GetInt(root, "artifactCount");
        var previewCount = GetInt(root, "evidencePreviewCount");
        var replayCount = GetInt(root, "manualReplayCount");
        if (artifactCount <= 0)
        {
            issues.Add(Error("ArtifactCountMissing", "$.artifactCount", "Audit pack must include at least one artifact."));
        }

        if (previewCount != artifactCount)
        {
            issues.Add(Error("PreviewCountMismatch", "$.evidencePreviewCount", "EvidencePreviewCount must equal ArtifactCount."));
        }

        if (replayCount != previewCount)
        {
            issues.Add(Error("ReplayCountMismatch", "$.manualReplayCount", "ManualReplayCount must equal EvidencePreviewCount."));
        }

        ValidateArtifacts(root, issues);
        ValidatePreviews(root, issues);
        var totalObservationCount = ValidateReplays(root, issues);

        if (totalObservationCount != 0)
        {
            issues.Add(Error("ReplayObservationsPresent", "$.manualReplayResults", "Final audit pack requires zero replay observations."));
        }

        if (root.TryGetProperty("safetyConfirmations", out var safety) && safety.ValueKind == JsonValueKind.Object)
        {
            Require(safety, "apiWorkerFakeLmaxGatewayOnly", true, issues, "$.safetyConfirmations");
            Require(safety, "noSchedulerOrPolling", true, issues, "$.safetyConfirmations");
            Require(safety, "noRuntimeShadowReplaySubmit", true, issues, "$.safetyConfirmations");
            Require(safety, "noOrderSurface", true, issues, "$.safetyConfirmations");
            Require(safety, "noGatewayRegistration", true, issues, "$.safetyConfirmations");
            Require(safety, "noTradingMutation", true, issues, "$.safetyConfirmations");
            Require(safety, "noCredentialExposure", true, issues, "$.safetyConfirmations");
        }
        else
        {
            issues.Add(Error("SafetyConfirmationsMissing", "$.safetyConfirmations", "Audit pack must include safety confirmations."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Error)
            ? LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Warning)
                ? LmaxReadOnlyMarketDataWorkflowAuditPackDecision.PassWithKnownWarnings
                : LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Pass;

        return new(decision, artifactCount, previewCount, replayCount, totalObservationCount, issues);
    }

    private static void ValidateArtifacts(JsonElement root, List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues)
    {
        if (!root.TryGetProperty("snapshotArtifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("SnapshotArtifactsMissing", "$.snapshotArtifacts", "Audit pack must include snapshotArtifacts."));
            return;
        }

        var index = 0;
        foreach (var artifact in artifacts.EnumerateArray())
        {
            var path = $"$.snapshotArtifacts[{index}]";
            if (!string.Equals(GetString(artifact, "validationStatus"), "PASS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ArtifactValidationNotPass", path + ".validationStatus", "Snapshot artifact validation must pass."));
            }

            if (!GetBoolean(artifact, "snapshotReceived"))
            {
                issues.Add(Error("ArtifactSnapshotNotReceived", path + ".snapshotReceived", "Snapshot artifact must have snapshotReceived=true."));
            }

            Require(artifact, "orderSubmissionAttempted", false, issues, path);
            Require(artifact, "shadowReplaySubmitAttempted", false, issues, path);
            Require(artifact, "tradingMutationAttempted", false, issues, path);
            Require(artifact, "schedulerStarted", false, issues, path);
            Require(artifact, "credentialValuesReturned", false, issues, path);
            Require(artifact, "noSensitiveContent", true, issues, path);
            index++;
        }
    }

    private static void ValidatePreviews(JsonElement root, List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues)
    {
        if (!root.TryGetProperty("evidencePreviews", out var previews) || previews.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("EvidencePreviewsMissing", "$.evidencePreviews", "Audit pack must include evidencePreviews."));
            return;
        }

        var index = 0;
        foreach (var preview in previews.EnumerateArray())
        {
            var path = $"$.evidencePreviews[{index}]";
            if (!string.Equals(GetString(preview, "validationStatus"), "PASS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("EvidencePreviewValidationNotPass", path + ".validationStatus", "Evidence preview validation must pass."));
            }

            if (!string.Equals(GetString(preview, "evidenceMode"), "MarketDataOnly", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("EvidencePreviewNotMarketDataOnly", path + ".evidenceMode", "Evidence preview must be MarketDataOnly."));
            }

            if (GetInt(preview, "executionReportCount") != 0
                || GetInt(preview, "orderStatusCount") != 0
                || GetInt(preview, "tradeCaptureReportCount") != 0
                || GetInt(preview, "protocolRejectCount") != 0)
            {
                issues.Add(Error("EvidencePreviewContainsNonMarketDataEvents", path, "Evidence preview must have zero execution/order/trade/reject events."));
            }

            Require(preview, "noSensitiveContent", true, issues, path);
            index++;
        }
    }

    private static int ValidateReplays(JsonElement root, List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues)
    {
        if (!root.TryGetProperty("manualReplayResults", out var replays) || replays.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("ManualReplayResultsMissing", "$.manualReplayResults", "Final audit pack requires manual replay results."));
            return 0;
        }

        var totalObservationCount = 0;
        var index = 0;
        foreach (var replay in replays.EnumerateArray())
        {
            var path = $"$.manualReplayResults[{index}]";
            var status = GetString(replay, "replayStatus");
            if (string.IsNullOrWhiteSpace(status))
            {
                status = GetString(replay, "status");
            }

            if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ReplayStatusNotCompleted", path + ".replayStatus", "Manual replay status must be Completed."));
            }

            var observationCount = GetInt(replay, "observationCount");
            totalObservationCount += observationCount;
            if (observationCount != 0
                || GetInt(replay, "blockingObservationCount") != 0
                || GetInt(replay, "warningObservationCount") != 0)
            {
                issues.Add(Error("ReplayObservationsPresent", path, "MarketDataOnly manual replay must produce zero observations."));
            }

            if (!string.Equals(GetString(replay, "mutationGuard"), "Unchanged", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ReplayMutationGuardChanged", path + ".mutationGuard", "Manual replay mutation guard must remain unchanged."));
            }

            Require(replay, "noSensitiveContent", true, issues, path);
            index++;
        }

        return totalObservationCount;
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues)
    {
        var scanText = json;
        foreach (var allowed in AllowedCredentialLabelTokens)
        {
            scanText = scanText.Replace(allowed, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var pattern in ForbiddenContentPatterns)
        {
            if (pattern.IsMatch(scanText))
            {
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Audit pack contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Audit pack contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues, string pathPrefix = "$")
    {
        if (GetBoolean(root, propertyName) != expected)
        {
            issues.Add(Error("UnexpectedBooleanFlag", pathPrefix + "." + propertyName, $"{propertyName} must be {expected.ToString().ToLowerInvariant()}."));
        }
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static LmaxReadOnlyMarketDataWorkflowAuditPackResult Result(
        List<LmaxReadOnlyMarketDataWorkflowAuditPackIssue> issues,
        int artifactCount,
        int previewCount,
        int replayCount,
        int totalObservationCount)
    {
        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Error)
            ? LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Warning)
                ? LmaxReadOnlyMarketDataWorkflowAuditPackDecision.PassWithKnownWarnings
                : LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Pass;
        return new(decision, artifactCount, previewCount, replayCount, totalObservationCount, issues);
    }

    private static LmaxReadOnlyMarketDataWorkflowAuditPackIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataWorkflowAuditPackSeverity.Error, code, path, message);
}
