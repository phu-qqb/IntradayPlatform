namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionReadinessSnapshotStatus
{
    NotReady,
    Blocked,
    ValidateOnly,
    NotExecutable
}

public enum LmaxReadOnlyExternalSessionReadinessSnapshotDecision
{
    NotReady,
    Blocked,
    ValidateOnly,
    NotExecutable
}

public sealed record LmaxReadOnlyExternalSessionReadinessSnapshot(
    Guid SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    LmaxReadOnlyExternalSessionRunIntentValidationResult IntentValidation,
    LmaxReadOnlyExternalSessionDryRunReport DryRunReport,
    LmaxReadOnlyExternalSessionSignoffResult Signoff,
    LmaxReadOnlyExternalSessionPreActivationAuditResult PreActivationAudit,
    LmaxReadOnlyExternalSessionReadinessSnapshotStatus Status,
    LmaxReadOnlyExternalSessionReadinessSnapshotDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> ValidationIssues,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates,
    IReadOnlyList<string> StableBlockers,
    bool CanStartSession,
    bool SessionStarted,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool NoSensitiveContent,
    string Message,
    string NextOperatorAction);

public sealed class LmaxReadOnlyExternalSessionReadinessSnapshotGenerator(
    LmaxReadOnlyRuntimeAdapterOptions? runtimeOptions = null)
{
    private static readonly string[] RequiredStableBlockers =
    [
        "Phase4ExternalRunImplementationNotStarted",
        "CredentialResolverDisabled",
        "GuardedTransportImplementationDisabled"
    ];

    private readonly LmaxReadOnlyExternalSessionDryRunReportGenerator _dryRunReportGenerator = new(runtimeOptions);

    public async Task<LmaxReadOnlyExternalSessionReadinessSnapshot> GenerateAsync(
        LmaxReadOnlyExternalSessionRunIntent intent,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var dryRunReport = await _dryRunReportGenerator.GenerateAsync(intent, createdAtUtc, cancellationToken);
        var dryRunGateNames = dryRunReport.SafetyGates.Select(x => x.Name).ToList();
        var signoff = LmaxReadOnlyExternalSessionSignoffValidator.Validate(new LmaxReadOnlyExternalSessionSignoffEnvelope(
            Guid.NewGuid(),
            createdAtUtc,
            dryRunReport.ReportId,
            dryRunReport.IntentValidation.Summary.IntentId,
            intent.RequestedByOperatorId,
            "readiness-snapshot-signoff-preview",
            LmaxReadOnlyExternalSessionSignoffRole.Approver,
            intent.Reason,
            ConfirmsReadOnlyIntent: true,
            ConfirmsNoOrderSubmission: true,
            ConfirmsNoTradingMutation: true,
            ConfirmsNoScheduler: true,
            ConfirmsNoShadowReplaySubmit: true,
            ConfirmsNoCredentialExposure: true,
            ConfirmsDemoOnly: string.Equals(intent.EnvironmentName, "Demo", StringComparison.OrdinalIgnoreCase),
            ConfirmsDryRunReportReviewed: true,
            DryRunReportCanStartSession: dryRunReport.CanStartSession,
            dryRunGateNames,
            LmaxReadOnlyExternalSessionSignoffDecision.Signed));

        var stableBlockers = dryRunReport.SafetyGates
            .Select(x => x.Name)
            .Concat(signoff.SafetyGates.Select(x => x.Name))
            .Concat(RequiredStableBlockers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var audit = LmaxReadOnlyExternalSessionPreActivationAuditValidator.Validate(new LmaxReadOnlyExternalSessionPreActivationAuditEnvelope(
            Guid.NewGuid(),
            createdAtUtc,
            intent.RequestedByOperatorId,
            "readiness-snapshot-reviewer",
            signoff.SignedByOperatorId,
            intent.Reason,
            dryRunReport.IntentValidation.Summary.IntentId,
            dryRunReport.ReportId,
            signoff.SignoffId,
            dryRunReport.CanStartSession,
            signoff.CanAuthorizeExecution,
            signoff.ExecutionStillBlocked,
            SessionStarted: false,
            ExternalConnectionAttempted: false,
            CredentialReadAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            stableBlockers,
            DryRunReportReviewed: true,
            SignoffReviewed: true));

        var issues = dryRunReport.IntentValidation.Issues
            .Concat(dryRunReport.OptionsValidation.Issues)
            .Concat(signoff.ValidationIssues)
            .Concat(audit.ValidationIssues)
            .Append(new LmaxReadOnlyExternalSessionConfigIssue(
                LmaxReadOnlyExternalSessionConfigIssueSeverity.Info,
                "Phase4OReadinessSnapshotMetadataOnly",
                "$",
                "Phase 4O readiness snapshot is metadata-only and cannot authorize execution."))
            .ToList();
        var gates = dryRunReport.SafetyGates
            .Concat(signoff.SafetyGates)
            .Concat(audit.SafetyGates)
            .Append(new LmaxReadOnlyRuntimeSafetyGateResult(
                "Phase4OReadinessSnapshotMetadataOnly",
                LmaxReadOnlyRuntimeSafetyGateStatus.Passed,
                "No execution approval",
                "Snapshot remains no-network/no-socket/no-credential/no-replay/no-mutation",
                "Readiness snapshot was generated as metadata only."))
            .ToList();

        return new LmaxReadOnlyExternalSessionReadinessSnapshot(
            Guid.NewGuid(),
            createdAtUtc,
            intent.RequestedByOperatorId,
            intent.Reason,
            dryRunReport.IntentValidation,
            dryRunReport,
            signoff,
            audit,
            LmaxReadOnlyExternalSessionReadinessSnapshotStatus.NotExecutable,
            LmaxReadOnlyExternalSessionReadinessSnapshotDecision.NotExecutable,
            issues,
            gates,
            stableBlockers,
            CanStartSession: false,
            SessionStarted: false,
            ExternalConnectionAttempted: false,
            CredentialReadAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            NoSensitiveContent: true,
            "External read-only readiness snapshot was generated, but Phase 4O cannot approve execution.",
            "Keep the external runtime disabled. Review blockers before any separate future implementation gate.");
    }
}
