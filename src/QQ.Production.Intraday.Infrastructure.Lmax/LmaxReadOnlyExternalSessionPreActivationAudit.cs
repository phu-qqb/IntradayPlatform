namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionPreActivationAuditStatus
{
    PreviewOnly,
    Blocked,
    Invalid,
    NotExecutable
}

public enum LmaxReadOnlyExternalSessionPreActivationAuditOutcome
{
    NotExecutable,
    PreviewOnly,
    Blocked
}

public sealed record LmaxReadOnlyExternalSessionPreActivationAuditEnvelope(
    Guid AuditEnvelopeId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string? ReviewedByOperatorId,
    string? SignedByOperatorId,
    string Reason,
    Guid IntentId,
    Guid DryRunReportId,
    Guid SignoffId,
    bool DryRunReportCanStartSession,
    bool SignoffCanAuthorizeExecution,
    bool SignoffExecutionStillBlocked,
    bool SessionStarted,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    IReadOnlyList<string> StableBlockers,
    bool DryRunReportReviewed,
    bool SignoffReviewed);

public sealed record LmaxReadOnlyExternalSessionPreActivationAuditResult(
    Guid AuditEnvelopeId,
    DateTimeOffset CreatedAtUtc,
    LmaxReadOnlyExternalSessionPreActivationAuditStatus Status,
    LmaxReadOnlyExternalSessionPreActivationAuditOutcome FinalOutcome,
    string RequestedByOperatorId,
    string? ReviewedByOperatorId,
    string? SignedByOperatorId,
    bool CanAuthorizeExecution,
    bool ExecutionStillBlocked,
    bool SessionStarted,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool NoSensitiveContent,
    IReadOnlyList<string> StableBlockers,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> ValidationIssues,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates,
    string Message,
    string NextOperatorAction);

public static class LmaxReadOnlyExternalSessionPreActivationAuditValidator
{
    private static readonly string[] RequiredStableBlockers =
    [
        "Phase4ExternalRunImplementationNotStarted",
        "CredentialResolverDisabled",
        "GuardedTransportImplementationDisabled"
    ];

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

    public static LmaxReadOnlyExternalSessionPreActivationAuditResult Validate(
        LmaxReadOnlyExternalSessionPreActivationAuditEnvelope envelope)
    {
        var issues = new List<LmaxReadOnlyExternalSessionConfigIssue>
        {
            Info("Phase4NPreActivationAuditMetadataOnly", "$", "Phase 4N validates a pre-activation audit envelope only. It does not authorize execution.")
        };

        if (envelope.AuditEnvelopeId == Guid.Empty)
        {
            issues.Add(Error("AuditEnvelopeIdRequired", "$.auditEnvelopeId", "AuditEnvelopeId must be non-empty."));
        }

        if (envelope.CreatedAtUtc == default)
        {
            issues.Add(Error("CreatedAtUtcRequired", "$.createdAtUtc", "CreatedAtUtc must be set."));
        }

        if (string.IsNullOrWhiteSpace(envelope.Reason))
        {
            issues.Add(Error("ReasonRequired", "$.reason", "A non-empty pre-activation audit reason is required."));
        }

        if (string.IsNullOrWhiteSpace(envelope.RequestedByOperatorId))
        {
            issues.Add(Error("RequestedByOperatorIdRequired", "$.requestedByOperatorId", "RequestedByOperatorId is required."));
        }

        if (envelope.IntentId == Guid.Empty)
        {
            issues.Add(Error("IntentSummaryRequired", "$.intentId", "Intent summary is required for the pre-activation audit envelope."));
        }

        if (envelope.DryRunReportId == Guid.Empty)
        {
            issues.Add(Error("DryRunReportSummaryRequired", "$.dryRunReportId", "Dry-run report summary is required for the pre-activation audit envelope."));
        }

        if (envelope.SignoffId == Guid.Empty)
        {
            issues.Add(Error("SignoffSummaryRequired", "$.signoffId", "Signoff summary is required for the pre-activation audit envelope."));
        }

        if (!envelope.DryRunReportReviewed)
        {
            issues.Add(Error("DryRunReportReviewedRequired", "$.dryRunReportReviewed", "The dry-run report must be reviewed before building the audit envelope."));
        }

        if (!envelope.SignoffReviewed)
        {
            issues.Add(Error("SignoffReviewedRequired", "$.signoffReviewed", "The signoff metadata must be reviewed before building the audit envelope."));
        }

        if (envelope.DryRunReportCanStartSession)
        {
            issues.Add(Error("DryRunReportMustRemainBlocked", "$.dryRunReportCanStartSession", "Dry-run report must keep canStartSession=false."));
        }

        if (envelope.SignoffCanAuthorizeExecution)
        {
            issues.Add(Error("SignoffCannotAuthorizeExecution", "$.signoffCanAuthorizeExecution", "Signoff must keep canAuthorizeExecution=false."));
        }

        if (!envelope.SignoffExecutionStillBlocked)
        {
            issues.Add(Error("SignoffExecutionStillBlockedRequired", "$.signoffExecutionStillBlocked", "Signoff must keep executionStillBlocked=true."));
        }

        ValidateNoAttempt(envelope.SessionStarted, "SessionStartedMustRemainFalse", "$.sessionStarted", "Pre-activation audit must not report a started session.", issues);
        ValidateNoAttempt(envelope.ExternalConnectionAttempted, "ExternalConnectionAttemptedMustRemainFalse", "$.externalConnectionAttempted", "Pre-activation audit must not report an external connection attempt.", issues);
        ValidateNoAttempt(envelope.CredentialReadAttempted, "CredentialReadAttemptedMustRemainFalse", "$.credentialReadAttempted", "Pre-activation audit must not report a credential read attempt.", issues);
        ValidateNoAttempt(envelope.ShadowReplaySubmitAttempted, "ShadowReplaySubmitAttemptedMustRemainFalse", "$.shadowReplaySubmitAttempted", "Pre-activation audit must not report a shadow replay submit attempt.", issues);
        ValidateNoAttempt(envelope.TradingMutationAttempted, "TradingMutationAttemptedMustRemainFalse", "$.tradingMutationAttempted", "Pre-activation audit must not report a trading mutation attempt.", issues);

        var blockers = new HashSet<string>(envelope.StableBlockers ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredStableBlockers)
        {
            if (!blockers.Contains(required))
            {
                issues.Add(Error(required, "$.stableBlockers", $"Stable blocker '{required}' must remain present; audit cannot bypass this blocker."));
            }
        }

        issues.Add(Error("Phase4ExternalRunImplementationNotStarted", "$.auditEnvelope", "Pre-activation audit cannot authorize execution because external run implementation has not started."));
        issues.Add(Error("CredentialResolverDisabled", "$.auditEnvelope", "Pre-activation audit cannot override the disabled credential resolver."));
        issues.Add(Error("GuardedTransportImplementationDisabled", "$.auditEnvelope", "Pre-activation audit cannot override the disabled guarded transport."));

        foreach (var property in typeof(LmaxReadOnlyExternalSessionPreActivationAuditEnvelope).GetProperties())
        {
            foreach (var forbidden in SecretOrTransportShapedNames)
            {
                if (property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains("Report", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionPreActivationAuditEnvelope.RequestedByOperatorId), StringComparison.Ordinal)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionPreActivationAuditEnvelope.ReviewedByOperatorId), StringComparison.Ordinal)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionPreActivationAuditEnvelope.SignedByOperatorId), StringComparison.Ordinal))
                {
                    issues.Add(Error("ForbiddenPreActivationAuditPropertyName", "$." + property.Name, $"Pre-activation audit property '{property.Name}' is not allowed because it looks like endpoint, credential, raw FIX, or order-command material."));
                }
            }
        }

        var missingRequiredStableBlocker = RequiredStableBlockers.Any(x => !blockers.Contains(x));
        var hasErrors = issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
        var onlyExpectedBlockers = issues.All(x => x.Code is "Phase4NPreActivationAuditMetadataOnly" or "Phase4ExternalRunImplementationNotStarted" or "CredentialResolverDisabled" or "GuardedTransportImplementationDisabled");
        var status = hasErrors
            ? onlyExpectedBlockers && !missingRequiredStableBlocker ? LmaxReadOnlyExternalSessionPreActivationAuditStatus.NotExecutable : LmaxReadOnlyExternalSessionPreActivationAuditStatus.Invalid
            : LmaxReadOnlyExternalSessionPreActivationAuditStatus.PreviewOnly;
        var outcome = status == LmaxReadOnlyExternalSessionPreActivationAuditStatus.Invalid
            ? LmaxReadOnlyExternalSessionPreActivationAuditOutcome.Blocked
            : status == LmaxReadOnlyExternalSessionPreActivationAuditStatus.PreviewOnly
                ? LmaxReadOnlyExternalSessionPreActivationAuditOutcome.PreviewOnly
                : LmaxReadOnlyExternalSessionPreActivationAuditOutcome.NotExecutable;

        var stableBlockers = (envelope.StableBlockers ?? [])
            .Concat(RequiredStableBlockers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var gates = issues.Select(issue => new LmaxReadOnlyRuntimeSafetyGateResult(
            issue.Code,
            issue.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error
                ? LmaxReadOnlyRuntimeSafetyGateStatus.Failed
                : LmaxReadOnlyRuntimeSafetyGateStatus.Passed,
            issue.Path,
            "Phase4N pre-activation audit remains metadata-only",
            issue.Message)).ToList();

        return new LmaxReadOnlyExternalSessionPreActivationAuditResult(
            envelope.AuditEnvelopeId,
            envelope.CreatedAtUtc,
            status,
            outcome,
            envelope.RequestedByOperatorId,
            envelope.ReviewedByOperatorId,
            envelope.SignedByOperatorId,
            CanAuthorizeExecution: false,
            ExecutionStillBlocked: true,
            SessionStarted: false,
            ExternalConnectionAttempted: false,
            CredentialReadAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            NoSensitiveContent: true,
            stableBlockers,
            issues,
            gates,
            status == LmaxReadOnlyExternalSessionPreActivationAuditStatus.Invalid
                ? "External read-only pre-activation audit envelope is invalid and authorizes no execution."
                : "External read-only pre-activation audit envelope was evaluated, but Phase 4N cannot authorize execution.",
            "Keep the external runtime disabled. Use this envelope only as pre-activation audit metadata for a separate future implementation gate.");
    }

    private static void ValidateNoAttempt(
        bool value,
        string code,
        string path,
        string message,
        List<LmaxReadOnlyExternalSessionConfigIssue> issues)
    {
        if (value)
        {
            issues.Add(Error(code, path, message));
        }
    }

    private static LmaxReadOnlyExternalSessionConfigIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Info, code, path, message);
}
