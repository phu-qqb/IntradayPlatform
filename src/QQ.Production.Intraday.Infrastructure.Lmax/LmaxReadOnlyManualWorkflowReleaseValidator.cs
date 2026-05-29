using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyManualWorkflowReleaseDecision
{
    Pass,
    PassWithWarnings,
    Fail
}

public enum LmaxReadOnlyManualWorkflowReleaseSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyManualWorkflowReleaseIssue(
    LmaxReadOnlyManualWorkflowReleaseSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyManualWorkflowReleaseValidationResult(
    LmaxReadOnlyManualWorkflowReleaseDecision Decision,
    int ArtifactCount,
    int EvidencePreviewCount,
    int ManualReplayCount,
    IReadOnlyList<LmaxReadOnlyManualWorkflowReleaseIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyManualWorkflowReleaseIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyManualWorkflowReleaseSeverity.Error).ToList();
}

public static class LmaxReadOnlyManualWorkflowReleaseValidator
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

    public static LmaxReadOnlyManualWorkflowReleaseValidationResult ValidateFile(
        string manifestFile,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        if (string.IsNullOrWhiteSpace(manifestFile) || !File.Exists(manifestFile))
        {
            return Result([Error("ReleaseManifestMissing", "$", "Release manifest file is required and must exist.")], 0, 0, 0);
        }

        return ValidateJson(File.ReadAllText(manifestFile), forbiddenSensitiveValues);
    }

    public static LmaxReadOnlyManualWorkflowReleaseValidationResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyManualWorkflowReleaseIssue>();
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

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
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Release manifest must report redactionStatus=Redacted."));
        }

        var artifactCount = ValidateArtifacts(root, issues);
        var previewCount = ValidatePreviews(root, issues);
        var replayCount = ValidateReplays(root, previewCount, issues);

        if (artifactCount == 0)
        {
            issues.Add(Error("SnapshotArtifactsMissing", "$.snapshotArtifacts", "Release manifest must include at least one snapshot artifact."));
        }

        if (previewCount == 0)
        {
            issues.Add(Error("EvidencePreviewsMissing", "$.evidencePreviews", "Release manifest must include at least one evidence preview."));
        }

        var replayRequested = GetBoolean(root, "replayRequested");
        if (!replayRequested && replayCount == 0)
        {
            issues.Add(Warning("ReplayNotRequested", "$.manualReplayResults", "Manual replay was skipped; release remains pass-with-warnings."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyManualWorkflowReleaseSeverity.Error)
            ? LmaxReadOnlyManualWorkflowReleaseDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyManualWorkflowReleaseSeverity.Warning)
                ? LmaxReadOnlyManualWorkflowReleaseDecision.PassWithWarnings
                : LmaxReadOnlyManualWorkflowReleaseDecision.Pass;

        return new(decision, artifactCount, previewCount, replayCount, issues);
    }

    private static int ValidateArtifacts(JsonElement root, List<LmaxReadOnlyManualWorkflowReleaseIssue> issues)
    {
        if (!root.TryGetProperty("snapshotArtifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var artifact in artifacts.EnumerateArray())
        {
            var path = $"$.snapshotArtifacts[{count}]";
            if (!string.Equals(GetString(artifact, "validationStatus"), "PASS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ArtifactValidationNotPass", path + ".validationStatus", "Snapshot artifact validation must pass."));
            }

            if (GetString(artifact, "path") is not { Length: > 0 })
            {
                issues.Add(Error("ArtifactPathMissing", path + ".path", "Snapshot artifact path is required."));
            }

            Require(artifact, "snapshotReceived", true, issues, path);
            Require(artifact, "orderSubmissionAttempted", false, issues, path);
            Require(artifact, "shadowReplaySubmitAttempted", false, issues, path);
            Require(artifact, "tradingMutationAttempted", false, issues, path);
            Require(artifact, "schedulerStarted", false, issues, path);
            Require(artifact, "credentialValuesReturned", false, issues, path);
            Require(artifact, "noSensitiveContent", true, issues, path);
            count++;
        }

        return count;
    }

    private static int ValidatePreviews(JsonElement root, List<LmaxReadOnlyManualWorkflowReleaseIssue> issues)
    {
        if (!root.TryGetProperty("evidencePreviews", out var previews) || previews.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var preview in previews.EnumerateArray())
        {
            var path = $"$.evidencePreviews[{count}]";
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

            if (GetString(preview, "path") is not { Length: > 0 })
            {
                issues.Add(Error("EvidencePreviewPathMissing", path + ".path", "Evidence preview path is required."));
            }

            Require(preview, "noSensitiveContent", true, issues, path);
            count++;
        }

        return count;
    }

    private static int ValidateReplays(JsonElement root, int previewCount, List<LmaxReadOnlyManualWorkflowReleaseIssue> issues)
    {
        if (!root.TryGetProperty("manualReplayResults", out var replays) || replays.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var replay in replays.EnumerateArray())
        {
            var path = $"$.manualReplayResults[{count}]";
            if (!string.Equals(GetString(replay, "replayStatus"), "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(GetString(replay, "status"), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ReplayStatusNotCompleted", path + ".replayStatus", "Manual replay status must be Completed."));
            }

            if (GetInt(replay, "observationCount") != 0
                || GetInt(replay, "blockingObservationCount") != 0
                || GetInt(replay, "warningObservationCount") != 0)
            {
                issues.Add(Error("ReplayObservationsPresent", path, "Manual replay must produce zero observations."));
            }

            if (!string.Equals(GetString(replay, "mutationGuard"), "Unchanged", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ReplayMutationGuardChanged", path + ".mutationGuard", "Manual replay mutation guard must remain unchanged."));
            }

            Require(replay, "noSensitiveContent", true, issues, path);
            count++;
        }

        if (count > 0 && count != previewCount)
        {
            issues.Add(Error("ReplayCountDoesNotMatchPreviewCount", "$.manualReplayResults", "Manual replay count must match evidence preview count."));
        }

        return count;
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyManualWorkflowReleaseIssue> issues)
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
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Release manifest contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Release manifest contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyManualWorkflowReleaseIssue> issues, string pathPrefix = "$")
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

    private static LmaxReadOnlyManualWorkflowReleaseIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyManualWorkflowReleaseSeverity.Error, code, path, message);

    private static LmaxReadOnlyManualWorkflowReleaseIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyManualWorkflowReleaseSeverity.Warning, code, path, message);

    private static LmaxReadOnlyManualWorkflowReleaseValidationResult Result(
        IReadOnlyList<LmaxReadOnlyManualWorkflowReleaseIssue> issues,
        int artifactCount,
        int previewCount,
        int replayCount)
    {
        var decision = issues.Any(x => x.Severity == LmaxReadOnlyManualWorkflowReleaseSeverity.Error)
            ? LmaxReadOnlyManualWorkflowReleaseDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyManualWorkflowReleaseSeverity.Warning)
                ? LmaxReadOnlyManualWorkflowReleaseDecision.PassWithWarnings
                : LmaxReadOnlyManualWorkflowReleaseDecision.Pass;
        return new(decision, artifactCount, previewCount, replayCount, issues);
    }
}
