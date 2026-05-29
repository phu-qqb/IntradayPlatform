using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyMarketDataOperationalSignoffDecision
{
    Pass,
    PassWithWarnings,
    Fail
}

public enum LmaxReadOnlyMarketDataOperationalSignoffSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyMarketDataOperationalSignoffIssue(
    LmaxReadOnlyMarketDataOperationalSignoffSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyMarketDataOperationalSignoffResult(
    LmaxReadOnlyMarketDataOperationalSignoffDecision Decision,
    int ArtifactCount,
    int EvidencePreviewCount,
    int ManualReplayCount,
    int TotalObservationCount,
    IReadOnlyList<LmaxReadOnlyMarketDataOperationalSignoffIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyMarketDataOperationalSignoffIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyMarketDataOperationalSignoffSeverity.Error).ToList();
}

public static class LmaxReadOnlyMarketDataOperationalSignoffValidator
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

    public static LmaxReadOnlyMarketDataOperationalSignoffResult ValidateFile(
        string signoffFile,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        if (string.IsNullOrWhiteSpace(signoffFile) || !File.Exists(signoffFile))
        {
            return Result([Error("SignoffFileMissing", "$", "Operational signoff file is required and must exist.")], 0, 0, 0, 0);
        }

        return ValidateJson(File.ReadAllText(signoffFile), forbiddenSensitiveValues);
    }

    public static LmaxReadOnlyMarketDataOperationalSignoffResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyMarketDataOperationalSignoffIssue>();
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!string.Equals(GetString(root, "phase"), "5W", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("UnexpectedPhase", "$.phase", "Operational signoff phase must be 5W."));
        }

        if (!string.Equals(GetString(root, "finalDecision"), "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("SignoffDecisionNotPass", "$.finalDecision", "Operational signoff requires finalDecision=PASS."));
        }

        if (!string.Equals(GetString(root, "auditPackFinalDecision"), "PASS", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("AuditPackDecisionNotPass", "$.auditPackFinalDecision", "Referenced audit pack must have finalDecision=PASS."));
        }

        var artifactCount = GetInt(root, "artifactCount");
        var previewCount = GetInt(root, "evidencePreviewCount");
        var replayCount = GetInt(root, "manualReplayCount");
        var totalObservationCount = GetInt(root, "totalObservationCount");

        if (artifactCount <= 0)
        {
            issues.Add(Error("ArtifactCountMissing", "$.artifactCount", "Operational signoff requires at least one validated artifact."));
        }

        if (previewCount != artifactCount)
        {
            issues.Add(Error("PreviewCountMismatch", "$.evidencePreviewCount", "EvidencePreviewCount must equal ArtifactCount."));
        }

        if (replayCount != previewCount)
        {
            issues.Add(Error("ReplayCountMismatch", "$.manualReplayCount", "ManualReplayCount must equal EvidencePreviewCount."));
        }

        if (totalObservationCount != 0)
        {
            issues.Add(Error("ObservationCountNonZero", "$.totalObservationCount", "Operational signoff requires TotalObservationCount=0."));
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
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Operational signoff must report redactionStatus=Redacted."));
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
            issues.Add(Error("SafetyConfirmationsMissing", "$.safetyConfirmations", "Operational signoff must include safety confirmations."));
        }

        if (root.TryGetProperty("authorizedScope", out var authorized) && authorized.ValueKind == JsonValueKind.Array)
        {
            var text = string.Join(" ", authorized.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
            if (!text.Contains("controlled manual Demo read-only MarketData workflow", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("AuthorizedScopeMissing", "$.authorizedScope", "Authorized scope must be limited to recognizing the validated controlled manual Demo read-only MarketData workflow."));
            }
        }
        else
        {
            issues.Add(Error("AuthorizedScopeMissing", "$.authorizedScope", "Operational signoff must include authorizedScope."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataOperationalSignoffSeverity.Error)
            ? LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataOperationalSignoffSeverity.Warning)
                ? LmaxReadOnlyMarketDataOperationalSignoffDecision.PassWithWarnings
                : LmaxReadOnlyMarketDataOperationalSignoffDecision.Pass;

        return new(decision, artifactCount, previewCount, replayCount, totalObservationCount, issues);
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyMarketDataOperationalSignoffIssue> issues)
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
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Operational signoff contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Operational signoff contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyMarketDataOperationalSignoffIssue> issues, string pathPrefix = "$")
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

    private static LmaxReadOnlyMarketDataOperationalSignoffResult Result(
        List<LmaxReadOnlyMarketDataOperationalSignoffIssue> issues,
        int artifactCount,
        int previewCount,
        int replayCount,
        int totalObservationCount)
    {
        var decision = issues.Any(x => x.Severity == LmaxReadOnlyMarketDataOperationalSignoffSeverity.Error)
            ? LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyMarketDataOperationalSignoffSeverity.Warning)
                ? LmaxReadOnlyMarketDataOperationalSignoffDecision.PassWithWarnings
                : LmaxReadOnlyMarketDataOperationalSignoffDecision.Pass;
        return new(decision, artifactCount, previewCount, replayCount, totalObservationCount, issues);
    }

    private static LmaxReadOnlyMarketDataOperationalSignoffIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyMarketDataOperationalSignoffSeverity.Error, code, path, message);
}
