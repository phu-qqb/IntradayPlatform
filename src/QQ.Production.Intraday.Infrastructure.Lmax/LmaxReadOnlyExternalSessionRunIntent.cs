namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionRunIntentMode
{
    ValidateOnly,
    PreviewOnly,
    FutureExternalReadOnlyManual
}

public sealed record LmaxReadOnlyExternalSessionRunIntent(
    Guid IntentId,
    string Reason,
    string RequestedByOperatorId,
    DateTimeOffset RequestedAtUtc,
    string EnvironmentName,
    string VenueProfileName,
    string CredentialProfileName,
    LmaxReadOnlyExternalSessionRunIntentMode RunMode,
    bool DryRun = true,
    int MaxRuntimeSeconds = 30,
    int MaxEventsPerRun = 100,
    bool RequestedEvidencePreviewOnly = true,
    bool SubmitToShadowReplay = false,
    bool AllowExternalConnections = false,
    bool AllowCredentialUse = false,
    bool AllowOrderSubmission = false,
    bool SchedulerEnabled = false,
    bool PersistToTradingTables = false);

public sealed record LmaxReadOnlyExternalSessionRunIntentSummary(
    Guid IntentId,
    string RequestedByOperatorId,
    DateTimeOffset RequestedAtUtc,
    string EnvironmentName,
    string VenueProfileName,
    string CredentialProfileName,
    LmaxReadOnlyExternalSessionRunIntentMode RunMode,
    bool IsValidationOnly,
    bool IsBlocked,
    bool RequestedEvidencePreviewOnly,
    bool SubmitToShadowReplay,
    string Message);

public sealed record LmaxReadOnlyExternalSessionRunIntentValidationResult(
    LmaxReadOnlyRuntimeRunStatus Status,
    bool IsBlocked,
    LmaxReadOnlyExternalSessionRunIntentSummary Summary,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> Issues)
{
    public bool HasErrors => Issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
    public int ErrorCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
    public int WarningCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Warning);
    public int InfoCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Info);
}

public static class LmaxReadOnlyExternalSessionRunIntentValidator
{
    private static readonly string[] SecretOrTransportShapedNames =
    [
        "host",
        "port",
        "username",
        "password",
        "secret",
        "token",
        "apiKey",
        "privateKey",
        "account",
        "senderComp",
        "targetComp",
        "endpoint",
        "rawFix",
        "newOrder",
        "cancel",
        "replace",
        "submit" + "Order"
    ];

    public static LmaxReadOnlyExternalSessionRunIntentValidationResult Validate(
        LmaxReadOnlyExternalSessionRunIntent intent)
    {
        var issues = new List<LmaxReadOnlyExternalSessionConfigIssue>
        {
            Info("Phase4JIntentOnly", "$", "Phase 4J validates the external read-only run intent envelope only. No external session is started.")
        };

        if (intent.IntentId == Guid.Empty)
        {
            issues.Add(Error("IntentIdRequired", "$.intentId", "IntentId must be non-empty."));
        }

        if (string.IsNullOrWhiteSpace(intent.Reason))
        {
            issues.Add(Error("ReasonRequired", "$.reason", "A non-empty manual reason is required."));
        }

        if (string.IsNullOrWhiteSpace(intent.RequestedByOperatorId))
        {
            issues.Add(Error("RequestedByOperatorIdRequired", "$.requestedByOperatorId", "RequestedByOperatorId is required for auditability."));
        }

        if (intent.RequestedAtUtc == default)
        {
            issues.Add(Error("RequestedAtUtcRequired", "$.requestedAtUtc", "RequestedAtUtc must be set."));
        }

        if (!string.Equals(intent.EnvironmentName, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("EnvironmentMustBeDemo", "$.environmentName", "Future external read-only manual intent is limited to EnvironmentName=Demo for the current Phase 4 path."));
        }

        var venueValidation = new LmaxReadOnlyVenueProfileRegistryDisabled().Validate(intent.VenueProfileName, intent.EnvironmentName);
        issues.AddRange(venueValidation.Issues);

        if (string.IsNullOrWhiteSpace(intent.CredentialProfileName))
        {
            issues.Add(Error("CredentialProfileNameRequired", "$.credentialProfileName", "CredentialProfileName label is required, but it remains label-only."));
        }

        issues.Add(Error("CredentialResolverDisabled", "$.credentialProfileName", "Credential profile resolver remains disabled/no-op in Phase 4J; no credential values are read."));

        if (intent.RunMode == LmaxReadOnlyExternalSessionRunIntentMode.FutureExternalReadOnlyManual)
        {
            issues.Add(Error("Phase4ExternalRunImplementationNotStarted", "$.runMode", "FutureExternalReadOnlyManual remains blocked in Phase 4J because no real external implementation exists."));
        }

        if (!intent.DryRun)
        {
            issues.Add(Error("DryRunRequired", "$.dryRun", "DryRun must remain true."));
        }

        if (!intent.RequestedEvidencePreviewOnly)
        {
            issues.Add(Error("EvidencePreviewOnlyRequired", "$.requestedEvidencePreviewOnly", "Phase 4J intent must remain evidence-preview-only."));
        }

        if (intent.SubmitToShadowReplay)
        {
            issues.Add(Error("ShadowReplaySubmitDeferred", "$.submitToShadowReplay", "SubmitToShadowReplay must remain false in Phase 4J."));
        }

        if (intent.AllowExternalConnections)
        {
            issues.Add(Error("ExternalConnectionBlocked", "$.allowExternalConnections", "AllowExternalConnections must remain false in Phase 4J."));
        }

        if (intent.AllowCredentialUse)
        {
            issues.Add(Error("CredentialUseBlocked", "$.allowCredentialUse", "AllowCredentialUse must remain false in Phase 4J."));
        }

        if (intent.AllowOrderSubmission)
        {
            issues.Add(Error("OrderSubmissionForbidden", "$.allowOrderSubmission", "AllowOrderSubmission must always be false."));
        }

        if (intent.SchedulerEnabled)
        {
            issues.Add(Error("SchedulerForbidden", "$.schedulerEnabled", "SchedulerEnabled must remain false."));
        }

        if (intent.PersistToTradingTables)
        {
            issues.Add(Error("TradingTablePersistenceForbidden", "$.persistToTradingTables", "PersistToTradingTables must always be false."));
        }

        if (intent.MaxRuntimeSeconds <= 0 || intent.MaxRuntimeSeconds > LmaxReadOnlyExternalSessionOptions.SafeMaxRuntimeSeconds)
        {
            issues.Add(Error("MaxRuntimeSecondsOutOfRange", "$.maxRuntimeSeconds", $"MaxRuntimeSeconds must be within 1..{LmaxReadOnlyExternalSessionOptions.SafeMaxRuntimeSeconds}."));
        }

        if (intent.MaxEventsPerRun <= 0 || intent.MaxEventsPerRun > LmaxReadOnlyExternalSessionOptions.SafeMaxEventsPerRun)
        {
            issues.Add(Error("MaxEventsPerRunOutOfRange", "$.maxEventsPerRun", $"MaxEventsPerRun must be within 1..{LmaxReadOnlyExternalSessionOptions.SafeMaxEventsPerRun}."));
        }

        foreach (var property in typeof(LmaxReadOnlyExternalSessionRunIntent).GetProperties())
        {
            foreach (var forbidden in SecretOrTransportShapedNames)
            {
                if (property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionRunIntent.RequestedByOperatorId), StringComparison.Ordinal))
                {
                    issues.Add(Error("ForbiddenRunIntentPropertyName", "$." + property.Name, $"Run intent property '{property.Name}' is not allowed because it looks like endpoint, credential, raw FIX, or order-command material."));
                }
            }
        }

        var blocked = issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
        if (!blocked && intent.RunMode is LmaxReadOnlyExternalSessionRunIntentMode.ValidateOnly or LmaxReadOnlyExternalSessionRunIntentMode.PreviewOnly)
        {
            issues.Add(Info("IntentValidatedOnly", "$.runMode", "Intent is structurally valid for validation/preview, but Phase 4J does not start any session."));
        }

        var summary = new LmaxReadOnlyExternalSessionRunIntentSummary(
            intent.IntentId,
            intent.RequestedByOperatorId,
            intent.RequestedAtUtc,
            intent.EnvironmentName,
            intent.VenueProfileName,
            intent.CredentialProfileName,
            intent.RunMode,
            IsValidationOnly: true,
            IsBlocked: blocked,
            intent.RequestedEvidencePreviewOnly,
            intent.SubmitToShadowReplay,
            blocked
                ? "External read-only run intent is blocked by Phase 4J validation gates. No external session was started."
                : "External read-only run intent is valid for intent validation only. No external session was started.");

        return new LmaxReadOnlyExternalSessionRunIntentValidationResult(
            blocked ? LmaxReadOnlyRuntimeRunStatus.Blocked : LmaxReadOnlyRuntimeRunStatus.DryRun,
            blocked,
            summary,
            issues);
    }

    private static LmaxReadOnlyExternalSessionConfigIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Info, code, path, message);
}
