using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyDemoSnapshotStabilitySummarySeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyDemoSnapshotStabilitySummaryIssue(
    LmaxReadOnlyDemoSnapshotStabilitySummarySeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyDemoSnapshotStabilitySummaryValidationResult(
    bool IsValid,
    int AttemptCountRequested,
    int AttemptCountCompleted,
    int SuccessCount,
    int FailedSafeCount,
    int SnapshotReceivedCount,
    bool NoSensitiveContent,
    IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilitySummarySeverity.Error).ToList();
}

public static class LmaxReadOnlyDemoSnapshotStabilitySummaryValidator
{
    public const int MaxAttemptCount = 5;
    public const int MaxDelaySeconds = 10;

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
        "LMAX_DEMO_FIX_USERNAME",
        "LMAX_DEMO_FIX_PASSWORD",
        "LMAX_DEMO_SENDER_COMP_ID",
        "LMAX_DEMO_TARGET_COMP_ID",
        "credentialValuesReturned"
    ];

    public static LmaxReadOnlyDemoSnapshotStabilitySummaryValidationResult ValidateFile(
        string summaryFile,
        string? repositoryRoot = null,
        IEnumerable<string>? forbiddenSensitiveValues = null)
    {
        var issues = new List<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue>();
        if (string.IsNullOrWhiteSpace(summaryFile) || !File.Exists(summaryFile))
        {
            issues.Add(Error("SummaryFileMissing", "$", "Stability summary file is required and must exist."));
            return Result(issues, 0, 0, 0, 0, 0, false);
        }

        var fullPath = Path.GetFullPath(summaryFile);
        if (repositoryRoot is not null)
        {
            var repoRoot = Path.GetFullPath(repositoryRoot);
            var expectedRoot = Path.GetFullPath(Path.Combine(repoRoot, "artifacts", "lmax-readonly-runtime-demo-snapshot", "stability"));
            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("SummaryOutsideStabilityDirectory", "$.summaryFile", "Stability summary must live under artifacts/lmax-readonly-runtime-demo-snapshot/stability."));
            }
        }

        return ValidateJson(File.ReadAllText(fullPath), forbiddenSensitiveValues, issues);
    }

    public static LmaxReadOnlyDemoSnapshotStabilitySummaryValidationResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null)
        => ValidateJson(json, forbiddenSensitiveValues, []);

    private static LmaxReadOnlyDemoSnapshotStabilitySummaryValidationResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues,
        List<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> issues)
    {
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var attemptCountRequested = GetInt(root, "attemptCountRequested");
        var attemptCountCompleted = GetInt(root, "attemptCountCompleted");
        var successCount = GetInt(root, "successCount");
        var failedSafeCount = GetInt(root, "failedSafeCount");
        var snapshotReceivedCount = GetInt(root, "snapshotReceivedCount");
        var noSensitiveContent = GetBoolean(root, "noSensitiveContent");

        if (attemptCountRequested is < 1 or > MaxAttemptCount)
        {
            issues.Add(Error("AttemptCountOutOfRange", "$.attemptCountRequested", $"AttemptCount must be within 1..{MaxAttemptCount}."));
        }

        if (attemptCountCompleted < 0 || attemptCountCompleted > attemptCountRequested)
        {
            issues.Add(Error("AttemptCountCompletedInvalid", "$.attemptCountCompleted", "Completed attempts must be between 0 and requested attempts."));
        }

        if (successCount + failedSafeCount != attemptCountCompleted)
        {
            issues.Add(Error("AttemptTotalsMismatch", "$", "successCount + failedSafeCount must equal attemptCountCompleted."));
        }

        Require(root, "credentialValuesReturned", false, issues);
        Require(root, "orderSubmissionAttempted", false, issues);
        Require(root, "shadowReplaySubmitAttempted", false, issues);
        Require(root, "tradingMutationAttempted", false, issues);
        Require(root, "schedulerStarted", false, issues);
        Require(root, "noSensitiveContent", true, issues);

        if (!string.Equals(GetString(root, "redactionStatus"), "Redacted", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Stability summary must report redactionStatus=Redacted."));
        }

        if (root.TryGetProperty("attempts", out var attempts) && attempts.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var attempt in attempts.EnumerateArray())
            {
                ValidateAttempt(attempt, index, issues);
                index++;
            }

            if (index != attemptCountCompleted)
            {
                issues.Add(Error("AttemptArrayCountMismatch", "$.attempts", "attempts array count must equal attemptCountCompleted."));
            }
        }
        else
        {
            issues.Add(Error("AttemptsArrayMissing", "$.attempts", "Stability summary must include attempts array."));
        }

        return Result(issues, attemptCountRequested, attemptCountCompleted, successCount, failedSafeCount, snapshotReceivedCount, noSensitiveContent);
    }

    private static void ValidateAttempt(JsonElement attempt, int index, List<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> issues)
    {
        var path = $"$.attempts[{index}]";
        Require(attempt, "credentialValuesReturned", false, issues, path);
        Require(attempt, "orderSubmissionAttempted", false, issues, path);
        Require(attempt, "shadowReplaySubmitAttempted", false, issues, path);
        Require(attempt, "tradingMutationAttempted", false, issues, path);
        Require(attempt, "schedulerStarted", false, issues, path);
        Require(attempt, "noSensitiveContent", true, issues, path);
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> issues)
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

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> issues, string pathPrefix = "$")
    {
        if (GetBoolean(root, propertyName) != expected)
        {
            issues.Add(Error("UnexpectedBooleanFlag", pathPrefix + "." + propertyName, $"{propertyName} must be {expected.ToString().ToLowerInvariant()}."));
        }
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static LmaxReadOnlyDemoSnapshotStabilitySummaryIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyDemoSnapshotStabilitySummarySeverity.Error, code, path, message);

    private static LmaxReadOnlyDemoSnapshotStabilitySummaryValidationResult Result(
        IReadOnlyList<LmaxReadOnlyDemoSnapshotStabilitySummaryIssue> issues,
        int attemptCountRequested,
        int attemptCountCompleted,
        int successCount,
        int failedSafeCount,
        int snapshotReceivedCount,
        bool noSensitiveContent)
        => new(
            !issues.Any(x => x.Severity == LmaxReadOnlyDemoSnapshotStabilitySummarySeverity.Error),
            attemptCountRequested,
            attemptCountCompleted,
            successCount,
            failedSafeCount,
            snapshotReceivedCount,
            noSensitiveContent,
            issues);
}
