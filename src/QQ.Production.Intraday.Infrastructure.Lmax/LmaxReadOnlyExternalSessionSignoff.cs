namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionSignoffStatus
{
    Draft,
    Signed,
    Rejected,
    Invalid,
    NotExecutable
}

public enum LmaxReadOnlyExternalSessionSignoffRole
{
    Operator,
    Risk,
    Developer,
    Approver
}

public enum LmaxReadOnlyExternalSessionSignoffDecision
{
    Draft,
    Signed,
    Rejected,
    Invalid,
    NotExecutable
}

public sealed record LmaxReadOnlyExternalSessionSignoffEnvelope(
    Guid SignoffId,
    DateTimeOffset CreatedAtUtc,
    Guid? DryRunReportId,
    Guid IntentId,
    string RequestedByOperatorId,
    string SignedByOperatorId,
    LmaxReadOnlyExternalSessionSignoffRole SignoffRole,
    string Reason,
    bool ConfirmsReadOnlyIntent,
    bool ConfirmsNoOrderSubmission,
    bool ConfirmsNoTradingMutation,
    bool ConfirmsNoScheduler,
    bool ConfirmsNoShadowReplaySubmit,
    bool ConfirmsNoCredentialExposure,
    bool ConfirmsDemoOnly,
    bool ConfirmsDryRunReportReviewed,
    bool DryRunReportCanStartSession,
    IReadOnlyList<string> DryRunReportSafetyMarkers,
    LmaxReadOnlyExternalSessionSignoffDecision RequestedDecision);

public sealed record LmaxReadOnlyExternalSessionSignoffResult(
    Guid SignoffId,
    DateTimeOffset CreatedAtUtc,
    LmaxReadOnlyExternalSessionSignoffStatus Status,
    LmaxReadOnlyExternalSessionSignoffDecision Decision,
    LmaxReadOnlyExternalSessionSignoffRole SignoffRole,
    string RequestedByOperatorId,
    string SignedByOperatorId,
    bool CanAuthorizeExecution,
    bool ExecutionStillBlocked,
    bool SessionStarted,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> ValidationIssues,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates,
    string Message,
    string NextOperatorAction);

public static class LmaxReadOnlyExternalSessionSignoffValidator
{
    private static readonly string[] RequiredDryRunMarkers =
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

    public static LmaxReadOnlyExternalSessionSignoffResult Validate(
        LmaxReadOnlyExternalSessionSignoffEnvelope envelope)
    {
        var issues = new List<LmaxReadOnlyExternalSessionConfigIssue>
        {
            Info("Phase4MSignoffMetadataOnly", "$", "Phase 4M validates manual signoff metadata only. It does not authorize execution.")
        };

        if (envelope.SignoffId == Guid.Empty)
        {
            issues.Add(Error("SignoffIdRequired", "$.signoffId", "SignoffId must be non-empty."));
        }

        if (envelope.CreatedAtUtc == default)
        {
            issues.Add(Error("CreatedAtUtcRequired", "$.createdAtUtc", "CreatedAtUtc must be set."));
        }

        if (envelope.IntentId == Guid.Empty && (!envelope.DryRunReportId.HasValue || envelope.DryRunReportId.Value == Guid.Empty))
        {
            issues.Add(Error("IntentOrDryRunReportRequired", "$.intentId", "A dry-run report id or intent id is required for signoff validation."));
        }

        if (string.IsNullOrWhiteSpace(envelope.Reason))
        {
            issues.Add(Error("ReasonRequired", "$.reason", "A non-empty signoff reason is required."));
        }

        if (string.IsNullOrWhiteSpace(envelope.SignedByOperatorId))
        {
            issues.Add(Error("SignedByOperatorIdRequired", "$.signedByOperatorId", "SignedByOperatorId is required."));
        }

        if (string.IsNullOrWhiteSpace(envelope.RequestedByOperatorId))
        {
            issues.Add(Error("RequestedByOperatorIdRequired", "$.requestedByOperatorId", "RequestedByOperatorId is required."));
        }

        if (envelope.SignoffRole is LmaxReadOnlyExternalSessionSignoffRole.Risk or LmaxReadOnlyExternalSessionSignoffRole.Approver
            && !string.IsNullOrWhiteSpace(envelope.RequestedByOperatorId)
            && string.Equals(envelope.RequestedByOperatorId, envelope.SignedByOperatorId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("MakerCheckerSelfSignoffBlocked", "$.signedByOperatorId", "Risk/approver signoff cannot be performed by the same operator who requested the intent."));
        }

        if (envelope.RequestedDecision == LmaxReadOnlyExternalSessionSignoffDecision.Rejected)
        {
            issues.Add(Warning("SignoffRejected", "$.requestedDecision", "Signoff was rejected by the signer."));
        }

        ValidateAttestation(envelope.ConfirmsReadOnlyIntent, "ConfirmsReadOnlyIntentRequired", "$.confirmsReadOnlyIntent", "Signer must attest that the intent is read-only.", issues);
        ValidateAttestation(envelope.ConfirmsNoOrderSubmission, "ConfirmsNoOrderSubmissionRequired", "$.confirmsNoOrderSubmission", "Signer must attest that order submission remains forbidden.", issues);
        ValidateAttestation(envelope.ConfirmsNoTradingMutation, "ConfirmsNoTradingMutationRequired", "$.confirmsNoTradingMutation", "Signer must attest that trading-state mutation remains forbidden.", issues);
        ValidateAttestation(envelope.ConfirmsNoScheduler, "ConfirmsNoSchedulerRequired", "$.confirmsNoScheduler", "Signer must attest that scheduler activation remains forbidden.", issues);
        ValidateAttestation(envelope.ConfirmsNoShadowReplaySubmit, "ConfirmsNoShadowReplaySubmitRequired", "$.confirmsNoShadowReplaySubmit", "Signer must attest that runtime shadow replay submit remains deferred.", issues);
        ValidateAttestation(envelope.ConfirmsNoCredentialExposure, "ConfirmsNoCredentialExposureRequired", "$.confirmsNoCredentialExposure", "Signer must attest that no credential values are exposed.", issues);
        ValidateAttestation(envelope.ConfirmsDemoOnly, "ConfirmsDemoOnlyRequired", "$.confirmsDemoOnly", "Signer must attest that the future path remains Demo-only.", issues);
        ValidateAttestation(envelope.ConfirmsDryRunReportReviewed, "ConfirmsDryRunReportReviewedRequired", "$.confirmsDryRunReportReviewed", "Signer must attest that the dry-run report was reviewed.", issues);

        if (envelope.DryRunReportCanStartSession)
        {
            issues.Add(Error("DryRunReportMustRemainBlocked", "$.dryRunReportCanStartSession", "Current Phase 4M dry-run report must have canStartSession=false."));
        }

        var markers = new HashSet<string>(envelope.DryRunReportSafetyMarkers ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredDryRunMarkers)
        {
            if (!markers.Contains(required))
            {
                issues.Add(Error(required, "$.dryRunReportSafetyMarkers", $"Dry-run report marker '{required}' must remain present; signoff cannot bypass this blocker."));
            }
        }

        issues.Add(Error("Phase4ExternalRunImplementationNotStarted", "$.signoff", "Signoff cannot authorize execution because external run implementation has not started."));
        issues.Add(Error("CredentialResolverDisabled", "$.signoff", "Signoff cannot override the disabled credential resolver."));
        issues.Add(Error("GuardedTransportImplementationDisabled", "$.signoff", "Signoff cannot override the disabled guarded transport."));

        foreach (var property in typeof(LmaxReadOnlyExternalSessionSignoffEnvelope).GetProperties())
        {
            foreach (var forbidden in SecretOrTransportShapedNames)
            {
                if (property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Contains("Report", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionSignoffEnvelope.RequestedByOperatorId), StringComparison.Ordinal)
                    && !string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionSignoffEnvelope.SignedByOperatorId), StringComparison.Ordinal))
                {
                    issues.Add(Error("ForbiddenSignoffPropertyName", "$." + property.Name, $"Signoff property '{property.Name}' is not allowed because it looks like endpoint, credential, raw FIX, or order-command material."));
                }
            }
        }

        var hasErrors = issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
        var metadataComplete = !hasErrors || issues.All(x => x.Code is "Phase4ExternalRunImplementationNotStarted" or "CredentialResolverDisabled" or "GuardedTransportImplementationDisabled" or "Phase4MSignoffMetadataOnly");
        var status = hasErrors
            ? metadataComplete ? LmaxReadOnlyExternalSessionSignoffStatus.NotExecutable : LmaxReadOnlyExternalSessionSignoffStatus.Invalid
            : envelope.RequestedDecision == LmaxReadOnlyExternalSessionSignoffDecision.Rejected
                ? LmaxReadOnlyExternalSessionSignoffStatus.Rejected
                : LmaxReadOnlyExternalSessionSignoffStatus.Signed;
        var decision = status == LmaxReadOnlyExternalSessionSignoffStatus.Invalid
            ? LmaxReadOnlyExternalSessionSignoffDecision.Invalid
            : status == LmaxReadOnlyExternalSessionSignoffStatus.NotExecutable
                ? LmaxReadOnlyExternalSessionSignoffDecision.Signed
                : envelope.RequestedDecision;

        var gates = issues.Select(issue => new LmaxReadOnlyRuntimeSafetyGateResult(
            issue.Code,
            issue.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error
                ? LmaxReadOnlyRuntimeSafetyGateStatus.Failed
                : LmaxReadOnlyRuntimeSafetyGateStatus.Passed,
            issue.Path,
            "Phase4M signoff remains metadata-only",
            issue.Message)).ToList();

        return new LmaxReadOnlyExternalSessionSignoffResult(
            envelope.SignoffId,
            envelope.CreatedAtUtc,
            status,
            decision,
            envelope.SignoffRole,
            envelope.RequestedByOperatorId,
            envelope.SignedByOperatorId,
            CanAuthorizeExecution: false,
            ExecutionStillBlocked: true,
            SessionStarted: false,
            ExternalConnectionAttempted: false,
            CredentialReadAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            issues,
            gates,
            status == LmaxReadOnlyExternalSessionSignoffStatus.Invalid
                ? "External read-only signoff is invalid and authorizes no execution."
                : "External read-only signoff metadata was evaluated, but Phase 4M cannot authorize execution.",
            "Keep the external runtime disabled. Do not attempt external read-only execution until a separate future implementation gate exists.");
    }

    private static void ValidateAttestation(
        bool value,
        string code,
        string path,
        string message,
        List<LmaxReadOnlyExternalSessionConfigIssue> issues)
    {
        if (!value)
        {
            issues.Add(Error(code, path, message));
        }
    }

    private static LmaxReadOnlyExternalSessionConfigIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Warning, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Info, code, path, message);
}
