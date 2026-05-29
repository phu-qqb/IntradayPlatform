using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyMarketDataWorkflowDecision
{
    Pass,
    PassWithWarnings,
    Fail
}

public enum LmaxReadOnlyMarketDataWorkflowSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyMarketDataWorkflowIssue(
    LmaxReadOnlyMarketDataWorkflowSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyMarketDataWorkflowValidationResult(
    LmaxReadOnlyMarketDataWorkflowDecision Decision,
    int ArtifactCount,
    int EvidencePreviewCount,
    int ReplayResultCount,
    IReadOnlyList<LmaxReadOnlyMarketDataWorkflowIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyMarketDataWorkflowIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyMarketDataWorkflowSeverity.Error).ToList();
}

public static class LmaxReadOnlyMarketDataWorkflowValidator
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

    public static LmaxReadOnlyMarketDataWorkflowValidationResult ValidateFile(
        string manifestFile,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        if (string.IsNullOrWhiteSpace(manifestFile) || !File.Exists(manifestFile))
        {
            return Result([Error("ManifestFileMissing", "$", "Workflow manifest file is required and must exist.")], 0, 0, 0);
        }

        return ValidateJson(File.ReadAllText(manifestFile), forbiddenSensitiveValues);
    }

    public static LmaxReadOnlyMarketDataWorkflowValidationResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyMarketDataWorkflowIssue>();
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
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Workflow manifest must report redactionStatus=Redacted."));
        }

        var artifactCount = 0;
        if (root.TryGetProperty("snapshotArtifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
        {
            foreach (var artifact in artifacts.EnumerateArray())
            {
                ValidateArtifact(artifact, artifactCount, issues);
                artifactCount++;
            }
        }
        else
        {
            issues.Add(Error("SnapshotArtifactsMissing", "$.snapshotArtifacts", "Workflow manifest must include snapshotArtifacts."));
        }

        var previewCount = 0;
        if (root.TryGetProperty("evidencePreviews", out var previews) && previews.ValueKind == JsonValueKind.Array)
        {
            foreach (var preview in previews.EnumerateArray())
            {
                ValidatePreview(preview, previewCount, issues);
                previewCount++;
            }
        }
        else
        {
            issues.Add(Error("EvidencePreviewsMissing", "$.evidencePreviews", "Workflow manifest must include evidencePreviews."));
        }

        var replayCount = 0;
        if (root.TryGetProperty("manualReplayResults", out var replays) && replays.ValueKind == JsonValueKind.Array)
        {
            foreach (var replay in replays.EnumerateArray())
            {
                ValidateReplay(replay, replayCount, issues);
                replayCount++;
            }
        }

        if (replayCount == 0)
        {
            issues.Add(Warning("ReplayNotRequested", "$.manualReplayResults", "Manual replay results are absent because replay was not requested."));
        }
        else if (replayCount != previewCount)
        {
            issues.Add(Error("ReplayCountDoesNotMatchPreviewCount", "$.manualReplayResults", "When manual replay is performed, replay count must match evidence preview count."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowSeverity.Error)
            ? LmaxReadOnlyMarketDataWorkflowDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowSeverity.Warning)
                ? LmaxReadOnlyMarketDataWorkflowDecision.PassWithWarnings
                : LmaxReadOnlyMarketDataWorkflowDecision.Pass;

        return new(decision, artifactCount, previewCount, replayCount, issues);
    }

    private static void ValidateArtifact(JsonElement artifact, int index, List<LmaxReadOnlyMarketDataWorkflowIssue> issues)
    {
        var path = $"$.snapshotArtifacts[{index}]";
        if (!string.Equals(GetString(artifact, "validationStatus"), "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("ArtifactValidationNotPass", path + ".validationStatus", "Snapshot artifact validation must pass."));
        }

        Require(artifact, "orderSubmissionAttempted", false, issues, path);
        Require(artifact, "shadowReplaySubmitAttempted", false, issues, path);
        Require(artifact, "tradingMutationAttempted", false, issues, path);
        Require(artifact, "schedulerStarted", false, issues, path);
        Require(artifact, "credentialValuesReturned", false, issues, path);
        Require(artifact, "noSensitiveContent", true, issues, path);
    }

    private static void ValidatePreview(JsonElement preview, int index, List<LmaxReadOnlyMarketDataWorkflowIssue> issues)
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
    }

    private static void ValidateReplay(JsonElement replay, int index, List<LmaxReadOnlyMarketDataWorkflowIssue> issues)
    {
        var path = $"$.manualReplayResults[{index}]";
        var status = GetString(replay, "replayStatus");
        if (string.IsNullOrWhiteSpace(status))
        {
            status = GetString(replay, "status");
        }

        if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("ReplayStatusNotCompleted", path + ".replayStatus", "Manual replay status must be Completed when replay results are present."));
        }

        if (GetInt(replay, "observationCount") != 0
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
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyMarketDataWorkflowIssue> issues)
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
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Workflow manifest contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Workflow manifest contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyMarketDataWorkflowIssue> issues, string pathPrefix = "$")
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

    private static LmaxReadOnlyMarketDataWorkflowIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataWorkflowSeverity.Error, code, path, message);

    private static LmaxReadOnlyMarketDataWorkflowIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataWorkflowSeverity.Warning, code, path, message);

    private static LmaxReadOnlyMarketDataWorkflowValidationResult Result(
        IReadOnlyList<LmaxReadOnlyMarketDataWorkflowIssue> issues,
        int artifactCount,
        int previewCount,
        int replayCount)
    {
        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowSeverity.Error)
            ? LmaxReadOnlyMarketDataWorkflowDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataWorkflowSeverity.Warning)
                ? LmaxReadOnlyMarketDataWorkflowDecision.PassWithWarnings
                : LmaxReadOnlyMarketDataWorkflowDecision.Pass;
        return new(decision, artifactCount, previewCount, replayCount, issues);
    }
}
