using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyDemoSnapshotStabilityClosureDecision
{
    Pass,
    PassWithWarnings,
    Fail
}

public enum LmaxReadOnlyDemoSnapshotStabilityClosureSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyDemoSnapshotStabilityClosureIssue(
    LmaxReadOnlyDemoSnapshotStabilityClosureSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyDemoSnapshotStabilityClosureResult(
    LmaxReadOnlyDemoSnapshotStabilityClosureDecision Decision,
    int AttemptCountRequested,
    int AttemptCountCompleted,
    int SuccessCount,
    int FailedSafeCount,
    int SnapshotReceivedCount,
    string ReadinessRecommendation,
    IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> Issues)
{
    public bool IsPass => Decision is LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Pass;

    public IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Error).ToList();

    public IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Warning).ToList();
}

public static class LmaxReadOnlyDemoSnapshotStabilityClosureValidator
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
        new("(?i)NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest", RegexOptions.Compiled)
    ];

    private static readonly string[] AllowedCredentialLabelTokens =
    [
        "credentialValuesReturned",
        "LMAX_DEMO_FIX_USERNAME",
        "LMAX_DEMO_FIX_PASSWORD",
        "LMAX_DEMO_SENDER_COMP_ID",
        "LMAX_DEMO_TARGET_COMP_ID"
    ];

    public static LmaxReadOnlyDemoSnapshotStabilityClosureResult ValidateFile(
        string summaryFile,
        string? repositoryRoot = null,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue>();
        if (string.IsNullOrWhiteSpace(summaryFile) || !File.Exists(summaryFile))
        {
            issues.Add(Error("SummaryFileMissing", "$", "Stability summary file is required and must exist."));
            return Result(issues, 0, 0, 0, 0, 0);
        }

        var fullPath = Path.GetFullPath(summaryFile);
        var repoRoot = repositoryRoot is null ? null : Path.GetFullPath(repositoryRoot);
        if (repoRoot is not null)
        {
            var expectedRoot = Path.GetFullPath(Path.Combine(repoRoot, "artifacts", "lmax-readonly-runtime-demo-snapshot", "stability"));
            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("SummaryOutsideStabilityDirectory", "$.summaryFile", "Stability summary must live under artifacts/lmax-readonly-runtime-demo-snapshot/stability."));
            }
        }

        var json = File.ReadAllText(fullPath);
        return ValidateJson(json, repoRoot, forbiddenSensitiveValues, issues);
    }

    public static LmaxReadOnlyDemoSnapshotStabilityClosureResult ValidateJson(
        string json,
        string? repositoryRoot = null,
        IEnumerable<string>? forbiddenSensitiveValues = null)
        => ValidateJson(json, repositoryRoot, forbiddenSensitiveValues, []);

    private static LmaxReadOnlyDemoSnapshotStabilityClosureResult ValidateJson(
        string json,
        string? repositoryRoot,
        IEnumerable<string>? forbiddenSensitiveValues,
        List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues)
    {
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);
        var summary = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json, forbiddenSensitiveValues);
        foreach (var issue in summary.Issues)
        {
            issues.Add(new(
                issue.Severity == LmaxReadOnlyDemoSnapshotStabilitySummarySeverity.Error
                    ? LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Error
                    : LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Warning,
                issue.Code,
                issue.Path,
                issue.Message));
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var attemptCountRequested = GetInt(root, "attemptCountRequested");
        var attemptCountCompleted = GetInt(root, "attemptCountCompleted");
        var successCount = GetInt(root, "successCount");
        var failedSafeCount = GetInt(root, "failedSafeCount");
        var snapshotReceivedCount = GetInt(root, "snapshotReceivedCount");

        if (attemptCountRequested is < 1 or > LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.MaxAttemptCount)
        {
            issues.Add(Error("AttemptCountOutOfRange", "$.attemptCountRequested", "Successful stability closure requires AttemptCountRequested within 1..5."));
        }

        if (attemptCountCompleted != attemptCountRequested)
        {
            issues.Add(Error("AttemptCountNotCompleted", "$.attemptCountCompleted", "Successful stability closure requires AttemptCountCompleted == AttemptCountRequested."));
        }

        if (successCount != attemptCountRequested)
        {
            issues.Add(Error("SuccessCountMismatch", "$.successCount", "Successful stability closure requires SuccessCount == AttemptCountRequested."));
        }

        if (failedSafeCount != 0)
        {
            issues.Add(Error("FailedSafeCountNonZero", "$.failedSafeCount", "Successful stability closure requires FailedSafeCount=0."));
        }

        if (snapshotReceivedCount != attemptCountRequested)
        {
            issues.Add(Error("SnapshotReceivedCountMismatch", "$.snapshotReceivedCount", "Successful stability closure requires SnapshotReceivedCount == AttemptCountRequested."));
        }

        if (root.TryGetProperty("attempts", out var attempts) && attempts.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var attempt in attempts.EnumerateArray())
            {
                ValidateAttemptReference(attempt, index, repositoryRoot, forbiddenSensitiveValues, issues);
                index++;
            }
        }

        return Result(issues, attemptCountRequested, attemptCountCompleted, successCount, failedSafeCount, snapshotReceivedCount);
    }

    private static void ValidateAttemptReference(
        JsonElement attempt,
        int index,
        string? repositoryRoot,
        IEnumerable<string>? forbiddenSensitiveValues,
        List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues)
    {
        var path = $"$.attempts[{index}]";
        if (!string.Equals(GetString(attempt, "status"), "Completed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(GetString(attempt, "status"), "CompletedWithWarnings", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("AttemptStatusNotSuccessful", path + ".status", "Successful closure requires every attempt to be Completed or CompletedWithWarnings."));
        }

        ValidateArtifactReference(GetString(attempt, "artifactPath"), path + ".artifactPath", repositoryRoot, forbiddenSensitiveValues, issues);
        ValidatePreviewReference(GetString(attempt, "evidencePreviewPath"), path + ".evidencePreviewPath", repositoryRoot, issues);
    }

    private static void ValidateArtifactReference(
        string artifactPath,
        string path,
        string? repositoryRoot,
        IEnumerable<string>? forbiddenSensitiveValues,
        List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            issues.Add(Warning("ArtifactPathMissing", path, "Attempt did not include an artifact path; referenced artifact validation was skipped."));
            return;
        }

        var resolved = ResolvePath(artifactPath, repositoryRoot);
        if (repositoryRoot is not null && !IsUnderSnapshotArtifacts(resolved, repositoryRoot))
        {
            issues.Add(Error("ArtifactOutsideSnapshotDirectory", path, "Referenced artifact must live under artifacts/lmax-readonly-runtime-demo-snapshot."));
        }

        if (!File.Exists(resolved))
        {
            issues.Add(Warning("ReferencedArtifactMissing", path, "Referenced artifact file is not present locally; artifact validation was skipped."));
            return;
        }

        var validation = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateFile(resolved, repositoryRoot, forbiddenSensitiveValues, requireSuccessfulSnapshot: true);
        foreach (var issue in validation.Issues)
        {
            issues.Add(new(
                issue.Severity == LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error
                    ? LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Error
                    : LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Warning,
                "Artifact" + issue.Code,
                path + issue.Path.TrimStart('$'),
                issue.Message));
        }
    }

    private static void ValidatePreviewReference(
        string previewPath,
        string path,
        string? repositoryRoot,
        List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(previewPath))
        {
            issues.Add(Warning("EvidencePreviewPathMissing", path, "Attempt did not include an evidence preview path; preview validation was skipped."));
            return;
        }

        var resolved = ResolvePath(previewPath, repositoryRoot);
        if (repositoryRoot is not null && !IsUnderSnapshotArtifacts(resolved, repositoryRoot))
        {
            issues.Add(Error("EvidencePreviewOutsideSnapshotDirectory", path, "Referenced evidence preview must live under artifacts/lmax-readonly-runtime-demo-snapshot."));
        }

        if (!File.Exists(resolved))
        {
            issues.Add(Warning("ReferencedEvidencePreviewMissing", path, "Referenced evidence preview file is not present locally; preview validation was skipped."));
            return;
        }

        var preview = LmaxReadOnlyRuntimeAdapterFakeInMemory.PreviewFixtureEvidence(resolved);
        if (preview.ErrorCount != 0)
        {
            issues.Add(Error("EvidencePreviewValidationFailed", path, "Referenced evidence preview did not validate cleanly."));
        }

        if (!string.Equals(preview.Batch.EvidenceMode, "MarketDataOnly", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("EvidencePreviewModeNotMarketDataOnly", path, "Referenced evidence preview must be MarketDataOnly."));
        }

        if (preview.Batch.ExecutionReportCount != 0
            || preview.Batch.OrderStatusCount != 0
            || preview.Batch.TradeCaptureReportCount != 0
            || preview.Batch.ProtocolRejectCount != 0)
        {
            issues.Add(Error("EvidencePreviewContainsNonMarketDataEvents", path, "Referenced evidence preview must have zero execution/order/trade/reject events."));
        }

        if (preview.Batch.MarketDataSnapshotCount <= 0)
        {
            issues.Add(Error("EvidencePreviewMissingMarketData", path, "Referenced evidence preview must include market data snapshot content."));
        }

        if (!preview.Batch.Sanitized)
        {
            issues.Add(Error("EvidencePreviewNotSanitized", path, "Referenced evidence preview must be sanitized."));
        }
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues)
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
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Summary contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Summary contains a configured sensitive sentinel value."));
            }
        }
    }

    private static bool IsUnderSnapshotArtifacts(string path, string repositoryRoot)
    {
        var expectedRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "artifacts", "lmax-readonly-runtime-demo-snapshot"));
        return Path.GetFullPath(path).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePath(string path, string? repositoryRoot)
        => Path.GetFullPath(Path.IsPathRooted(path) || repositoryRoot is null ? path : Path.Combine(repositoryRoot, path));

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static LmaxReadOnlyDemoSnapshotStabilityClosureIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Error, code, path, message);

    private static LmaxReadOnlyDemoSnapshotStabilityClosureIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Warning, code, path, message);

    private static LmaxReadOnlyDemoSnapshotStabilityClosureResult Result(
        IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilityClosureIssue> issues,
        int attemptCountRequested,
        int attemptCountCompleted,
        int successCount,
        int failedSafeCount,
        int snapshotReceivedCount)
    {
        var decision = issues.Any(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Error)
            ? LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail
            : issues.Any(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilityClosureSeverity.Warning)
                ? LmaxReadOnlyDemoSnapshotStabilityClosureDecision.PassWithWarnings
                : LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Pass;
        var recommendation = decision == LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail
            ? "Do not proceed. Resolve stability closure issues and rerun the Phase 5P review."
            : "Ready to consider Phase 5Q controlled manual MarketData evidence workflow hardening. This does not authorize scheduler, polling, order submission, runtime shadow replay submit, gateway registration, or production use.";

        return new(decision, attemptCountRequested, attemptCountCompleted, successCount, failedSafeCount, snapshotReceivedCount, recommendation, issues);
    }
}
