using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyDemoSnapshotArtifactValidationSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyDemoSnapshotArtifactValidationIssue(
    LmaxReadOnlyDemoSnapshotArtifactValidationSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyDemoSnapshotArtifactValidationResult(
    bool IsValid,
    bool IsSuccessfulSnapshot,
    string Status,
    bool SnapshotReceived,
    bool NoSensitiveContent,
    string RedactionStatus,
    IReadOnlyList<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error).ToList();
}

public static class LmaxReadOnlyDemoSnapshotArtifactValidator
{
    private static readonly Regex[] ForbiddenContentPatterns =
    [
        new("(?i)554\\s*=", RegexOptions.Compiled),
        new("(?i)553\\s*=", RegexOptions.Compiled),
        new("(?i)49\\s*=[^\\s,;]+", RegexOptions.Compiled),
        new("(?i)56\\s*=[^\\s,;]+", RegexOptions.Compiled),
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
        "passwordPresent",
        "usernamePresent"
    ];

    public static LmaxReadOnlyDemoSnapshotArtifactValidationResult ValidateFile(
        string artifactFile,
        string? repositoryRoot = null,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        bool requireSuccessfulSnapshot = true)
    {
        var issues = new List<LmaxReadOnlyDemoSnapshotArtifactValidationIssue>();
        if (string.IsNullOrWhiteSpace(artifactFile) || !File.Exists(artifactFile))
        {
            issues.Add(Error("ArtifactFileMissing", "$", "Artifact file is required and must exist."));
            return Result(issues, status: string.Empty, snapshotReceived: false, noSensitiveContent: false, redactionStatus: string.Empty, isSuccessfulSnapshot: false);
        }

        var fullPath = Path.GetFullPath(artifactFile);
        if (repositoryRoot is not null)
        {
            var repoRoot = Path.GetFullPath(repositoryRoot);
            var expectedRoot = Path.GetFullPath(Path.Combine(repoRoot, "artifacts", "lmax-readonly-runtime-demo-snapshot"));
            if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ArtifactOutsideSnapshotDirectory", "$.artifactFile", "Artifact must live under artifacts/lmax-readonly-runtime-demo-snapshot."));
            }

            var gitignore = Path.Combine(repoRoot, ".gitignore");
            if (!File.Exists(gitignore) || !File.ReadAllText(gitignore).Contains("artifacts/", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("ArtifactDirectoryNotIgnored", "$.artifactFile", "The artifacts directory must be ignored by git."));
            }
        }

        var json = File.ReadAllText(fullPath);
        CheckForbiddenContent(json, forbiddenSensitiveValues, issues);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var status = GetString(root, "status");
        var snapshotReceived = GetBoolean(root, "snapshotReceived");
        var logonSucceeded = GetBoolean(root, "logonSucceeded");
        var logoutSucceeded = GetBoolean(root, "logoutSucceeded");
        var noSensitiveContent = GetBoolean(root, "noSensitiveContent");
        var redactionStatus = GetString(root, "redactionStatus");
        var instrument = GetString(root, "instrument");
        var securityId = GetString(root, "securityId");

        if (requireSuccessfulSnapshot && status is not ("Completed" or "CompletedWithWarnings"))
        {
            issues.Add(Error("StatusNotSuccessful", "$.status", "Successful closure requires Completed or CompletedWithWarnings."));
        }

        Require(root, "snapshotReceived", true, issues);
        Require(root, "logonSucceeded", true, issues);
        Require(root, "logoutSucceeded", true, issues);
        Require(root, "orderSubmissionAttempted", false, issues);
        Require(root, "shadowReplaySubmitAttempted", false, issues);
        Require(root, "tradingMutationAttempted", false, issues);
        Require(root, "schedulerStarted", false, issues);
        Require(root, "credentialValuesReturned", false, issues);
        Require(root, "noSensitiveContent", true, issues);

        if (!string.Equals(redactionStatus, "Redacted", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("RedactionStatusNotRedacted", "$.redactionStatus", "Artifact must report redactionStatus=Redacted."));
        }

        if (!string.Equals(instrument, "EURUSD", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("UnexpectedInstrument", "$.instrument", "Successful closure is limited to EURUSD."));
        }

        if (securityId != "4001")
        {
            issues.Add(Error("UnexpectedSecurityId", "$.securityId", "Successful closure is limited to SecurityID 4001."));
        }

        if (snapshotReceived)
        {
            RequireNumber(root, "bestBid", issues);
            RequireNumber(root, "bestAsk", issues);
            RequireNumber(root, "mid", issues);
            if (GetInt(root, "entryCount") <= 0)
            {
                issues.Add(Error("MissingMarketDataEntries", "$.entryCount", "Successful snapshot artifact must contain at least one entry."));
            }
        }

        var isSuccessfulSnapshot = status is "Completed" or "CompletedWithWarnings"
            && snapshotReceived
            && logonSucceeded
            && logoutSucceeded;

        return Result(issues, status, snapshotReceived, noSensitiveContent, redactionStatus, isSuccessfulSnapshot);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactValidationResult ValidateJson(
        string json,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        bool requireSuccessfulSnapshot = true)
    {
        var temp = Path.Combine(Path.GetTempPath(), "lmax-readonly-demo-snapshot-validation-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(temp, json);
        try
        {
            return ValidateFile(temp, repositoryRoot: null, forbiddenSensitiveValues, requireSuccessfulSnapshot);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static void CheckForbiddenContent(string json, IEnumerable<string>? forbiddenSensitiveValues, List<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> issues)
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
                issues.Add(Error("ForbiddenSensitiveContent", "$", $"Artifact contains forbidden sensitive/order content pattern: {pattern}."));
            }
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && json.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                issues.Add(Error("ForbiddenSensitiveValue", "$", "Artifact contains a configured sensitive sentinel value."));
            }
        }
    }

    private static void Require(JsonElement root, string propertyName, bool expected, List<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> issues)
    {
        if (GetBoolean(root, propertyName) != expected)
        {
            issues.Add(Error("UnexpectedBooleanFlag", "$." + propertyName, $"{propertyName} must be {expected.ToString().ToLowerInvariant()}."));
        }
    }

    private static void RequireNumber(JsonElement root, string propertyName, List<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            issues.Add(Error("MissingMarketDataValue", "$." + propertyName, $"{propertyName} must be present when snapshotReceived=true."));
        }
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static LmaxReadOnlyDemoSnapshotArtifactValidationIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error, code, path, message);

    private static LmaxReadOnlyDemoSnapshotArtifactValidationResult Result(
        IReadOnlyList<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> issues,
        string status,
        bool snapshotReceived,
        bool noSensitiveContent,
        string redactionStatus,
        bool isSuccessfulSnapshot)
        => new(
            !issues.Any(x => x.Severity == LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error),
            isSuccessfulSnapshot,
            status,
            snapshotReceived,
            noSensitiveContent,
            redactionStatus,
            issues);
}
