namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionDryRunOutcome
{
    Blocked,
    ValidateOnly
}

public enum LmaxReadOnlyExternalSessionDryRunOperatorAction
{
    ReviewBlockedGates,
    KeepRuntimeDisabled
}

public sealed record LmaxReadOnlyExternalSessionDryRunSection(
    string Name,
    string Status,
    string Message,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> Issues,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates);

public sealed record LmaxReadOnlyExternalSessionVenueProfileSummary(
    string VenueProfileName,
    string EnvironmentName,
    bool IsActive,
    bool IsExternalConnectionAllowed,
    bool IsCredentialUseAllowed,
    string SafetyStatus,
    string RedactionStatus);

public sealed record LmaxReadOnlyExternalSessionCredentialProfileSummary(
    string CredentialProfileName,
    string EnvironmentName,
    string VenueProfileName,
    bool IsConfigured,
    string SourceKind,
    string RedactionStatus,
    string ResolverMode,
    bool CredentialReadImplemented,
    bool CredentialUseImplemented,
    bool SensitiveMaterialReturned);

public sealed record LmaxReadOnlyExternalSessionGuardedTransportSummary(
    string Status,
    bool NetworkTransportImplemented,
    bool SocketActivation,
    bool FixLogonImplemented,
    bool CredentialUseImplemented,
    bool OrderSubmissionImplemented,
    bool ReadOnlyOnly,
    bool ShadowReplaySubmitImplemented,
    bool TradingMutationImplemented,
    bool SchedulerImplemented);

public sealed record LmaxReadOnlyExternalSessionSkeletonSummary(
    string ExternalSessionImplementationMode,
    bool SocketActivation,
    bool FixLogonImplemented,
    bool CredentialUseImplemented,
    bool OrderSubmissionImplemented,
    bool ShadowReplaySubmitImplemented,
    bool TradingMutationImplemented,
    bool SchedulerImplemented,
    bool RuntimeGatewayRegistrationImplemented);

public sealed record LmaxReadOnlyExternalSessionDryRunReport(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    LmaxReadOnlyExternalSessionRunIntentMode RunMode,
    string EnvironmentName,
    string VenueProfileName,
    string CredentialProfileName,
    LmaxReadOnlyExternalSessionRunIntentValidationResult IntentValidation,
    LmaxReadOnlyExternalSessionOptionsValidationResult OptionsValidation,
    LmaxReadOnlyExternalSessionVenueProfileSummary VenueProfile,
    LmaxReadOnlyExternalSessionCredentialProfileSummary CredentialProfile,
    LmaxReadOnlyExternalSessionGuardedTransportSummary GuardedTransport,
    LmaxReadOnlyExternalSessionSkeletonSummary ExternalSessionSkeleton,
    IReadOnlyList<LmaxReadOnlyExternalSessionDryRunSection> Sections,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates,
    bool CanStartSession,
    bool SessionStarted,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    LmaxReadOnlyExternalSessionDryRunOutcome ExpectedOutcome,
    string BlockedReason,
    LmaxReadOnlyExternalSessionDryRunOperatorAction NextOperatorAction,
    bool NoSensitiveContent);

public sealed class LmaxReadOnlyExternalSessionDryRunReportGenerator(
    LmaxReadOnlyRuntimeAdapterOptions? runtimeOptions = null,
    ILmaxReadOnlyVenueProfileRegistry? venueRegistry = null,
    ILmaxReadOnlyCredentialProfileResolver? credentialResolver = null,
    ILmaxReadOnlyGuardedTransport? guardedTransport = null,
    ILmaxReadOnlyExternalSession? externalSession = null)
{
    private readonly LmaxReadOnlyRuntimeAdapterOptions _runtimeOptions = runtimeOptions ?? new LmaxReadOnlyRuntimeAdapterOptions();
    private readonly ILmaxReadOnlyVenueProfileRegistry _venueRegistry = venueRegistry ?? new LmaxReadOnlyVenueProfileRegistryDisabled();
    private readonly ILmaxReadOnlyCredentialProfileResolver _credentialResolver = credentialResolver ?? new LmaxReadOnlyCredentialProfileResolverDisabled();
    private readonly ILmaxReadOnlyGuardedTransport _guardedTransport = guardedTransport ?? new LmaxReadOnlyGuardedTransportDisabled(runtimeOptions);
    private readonly ILmaxReadOnlyExternalSession _externalSession = externalSession ?? new LmaxReadOnlyExternalSessionSkeleton(runtimeOptions);

    public async Task<LmaxReadOnlyExternalSessionDryRunReport> GenerateAsync(
        LmaxReadOnlyExternalSessionRunIntent intent,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var intentValidation = LmaxReadOnlyExternalSessionRunIntentValidator.Validate(intent);
        var optionsValidation = LmaxReadOnlyExternalSessionOptionsValidator.Validate(ToExternalSessionOptions(intent), intent.Reason);
        var venueValidation = _venueRegistry.Validate(intent.VenueProfileName, intent.EnvironmentName);
        var credentialStatus = await _credentialResolver.GetStatusAsync(new LmaxReadOnlyCredentialProfileRequest(
            intent.CredentialProfileName,
            intent.EnvironmentName,
            intent.VenueProfileName,
            intent.Reason), cancellationToken);
        var guardedTransportStatus = await _guardedTransport.GetStatusAsync(cancellationToken);
        var skeletonStatus = await _externalSession.GetStatusAsync(cancellationToken);
        var skeletonReport = _externalSession is LmaxReadOnlyExternalSessionSkeleton skeleton
            ? skeleton.GetSkeletonSafetyReport(new LmaxReadOnlyExternalSessionRequest(intent.Reason, intent.MaxEventsPerRun, intent.MaxRuntimeSeconds))
            : new LmaxReadOnlyExternalSessionSkeletonSafetyReport(
                "UnknownDisabledBoundary",
                SocketActivation: false,
                FixLogonImplemented: false,
                CredentialUseImplemented: false,
                OrderSubmissionImplemented: false,
                ShadowReplaySubmitImplemented: false,
                TradingMutationImplemented: false,
                SchedulerImplemented: false,
                RuntimeGatewayRegistrationImplemented: false,
                skeletonStatus.SafetyGates);

        var sections = new List<LmaxReadOnlyExternalSessionDryRunSection>
        {
            new(
                "IntentValidation",
                intentValidation.Status.ToString(),
                intentValidation.Summary.Message,
                intentValidation.Issues,
                IssuesToGates(intentValidation.Issues)),
            new(
                "OptionsValidation",
                optionsValidation.HasErrors ? LmaxReadOnlyRuntimeRunStatus.Blocked.ToString() : LmaxReadOnlyRuntimeRunStatus.DryRun.ToString(),
                optionsValidation.IsSafeDisabled ? "External read-only session options remain safe-disabled." : "External read-only session options are blocked by validation gates.",
                optionsValidation.Issues,
                IssuesToGates(optionsValidation.Issues)),
            new(
                "VenueProfile",
                venueValidation.IsAllowedForPhase4 ? "LabelOnlyInactive" : LmaxReadOnlyRuntimeRunStatus.Blocked.ToString(),
                "Venue profile registry returned labels only; no address, account, session, or credential values are available.",
                venueValidation.Issues,
                IssuesToGates(venueValidation.Issues)),
            new(
                "CredentialResolver",
                credentialStatus.Status.ToString(),
                "Credential resolver is disabled/no-op and returned no sensitive material.",
                [],
                credentialStatus.Safety.Gates),
            new(
                "GuardedTransport",
                guardedTransportStatus.Status.ToString(),
                "Guarded transport is disabled/no-network and returned no events.",
                [],
                guardedTransportStatus.SafetyGates),
            new(
                "ExternalSessionSkeleton",
                skeletonStatus.Status.ToString(),
                "External session skeleton is disabled/not implemented and cannot start a session.",
                [],
                skeletonStatus.SafetyGates)
        };

        var gates = sections.SelectMany(x => x.Gates).ToList();
        var blockedCodes = gates.Where(x => x.BlocksRun).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var blockedReason = blockedCodes.Count == 0
            ? "Phase 4L is validate-only/no-network; no session can start even when dry-run report inputs validate."
            : "Blocked by Phase 4L dry-run report gates: " + string.Join(", ", blockedCodes);
        return new LmaxReadOnlyExternalSessionDryRunReport(
            Guid.NewGuid(),
            createdAtUtc,
            intent.RequestedByOperatorId,
            intent.Reason,
            intent.RunMode,
            intent.EnvironmentName,
            intent.VenueProfileName,
            intent.CredentialProfileName,
            intentValidation,
            optionsValidation,
            ToVenueSummary(venueValidation, intent),
            ToCredentialSummary(credentialStatus),
            ToGuardedTransportSummary(guardedTransportStatus),
            ToSkeletonSummary(skeletonReport),
            sections,
            gates,
            CanStartSession: false,
            SessionStarted: false,
            ExternalConnectionAttempted: false,
            CredentialReadAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            intentValidation.IsBlocked || optionsValidation.HasErrors || blockedCodes.Count > 0
                ? LmaxReadOnlyExternalSessionDryRunOutcome.Blocked
                : LmaxReadOnlyExternalSessionDryRunOutcome.ValidateOnly,
            blockedReason,
            blockedCodes.Count > 0
                ? LmaxReadOnlyExternalSessionDryRunOperatorAction.ReviewBlockedGates
                : LmaxReadOnlyExternalSessionDryRunOperatorAction.KeepRuntimeDisabled,
            NoSensitiveContent: true);
    }

    private static LmaxReadOnlyExternalSessionOptions ToExternalSessionOptions(LmaxReadOnlyExternalSessionRunIntent intent)
        => new()
        {
            Enabled = false,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DesignOnly,
            ActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton,
            EnvironmentName = intent.EnvironmentName,
            VenueProfileName = intent.VenueProfileName,
            CredentialProfileName = intent.CredentialProfileName,
            AllowExternalConnections = intent.AllowExternalConnections,
            AllowCredentialUse = intent.AllowCredentialUse,
            AllowOrderSubmission = intent.AllowOrderSubmission,
            PersistRawFixMessages = false,
            PersistToTradingTables = intent.PersistToTradingTables,
            SchedulerEnabled = intent.SchedulerEnabled,
            SubmitToShadowReplay = intent.SubmitToShadowReplay,
            DryRun = intent.DryRun,
            MaxRuntimeSeconds = intent.MaxRuntimeSeconds,
            MaxEventsPerRun = intent.MaxEventsPerRun
        };

    private static IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> IssuesToGates(IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> issues)
        => issues.Select(issue => new LmaxReadOnlyRuntimeSafetyGateResult(
            issue.Code,
            issue.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error
                ? LmaxReadOnlyRuntimeSafetyGateStatus.Failed
                : LmaxReadOnlyRuntimeSafetyGateStatus.Passed,
            issue.Path,
            "Phase4L no-network dry-run report boundary",
            issue.Message)).ToList();

    private static LmaxReadOnlyExternalSessionVenueProfileSummary ToVenueSummary(
        LmaxReadOnlyVenueProfileValidationResult validation,
        LmaxReadOnlyExternalSessionRunIntent intent)
    {
        var descriptor = validation.Descriptor;
        return new LmaxReadOnlyExternalSessionVenueProfileSummary(
            descriptor?.VenueProfileName ?? intent.VenueProfileName,
            descriptor?.EnvironmentName ?? intent.EnvironmentName,
            descriptor?.IsActive ?? false,
            descriptor?.IsExternalConnectionAllowed ?? false,
            descriptor?.IsCredentialUseAllowed ?? false,
            (descriptor?.SafetyStatus ?? LmaxReadOnlyVenueProfileSafetyStatus.Blocked).ToString(),
            (descriptor?.RedactionStatus ?? LmaxReadOnlyVenueProfileRedactionStatus.LabelOnly).ToString());
    }

    private static LmaxReadOnlyExternalSessionCredentialProfileSummary ToCredentialSummary(
        LmaxReadOnlyCredentialProfileStatus status)
        => new(
            status.Descriptor.CredentialProfileName,
            status.Descriptor.EnvironmentName,
            status.Descriptor.VenueProfileName,
            status.Descriptor.IsConfigured,
            status.Descriptor.SourceKind.ToString(),
            status.Descriptor.RedactionStatus.ToString(),
            status.Safety.ResolverMode.ToString(),
            status.Safety.CredentialReadImplemented,
            status.Safety.CredentialUseImplemented,
            status.Safety.SensitiveMaterialReturned);

    private static LmaxReadOnlyExternalSessionGuardedTransportSummary ToGuardedTransportSummary(
        LmaxReadOnlyGuardedTransportStatus status)
        => new(
            status.Status.ToString(),
            status.Capabilities.NetworkTransportImplemented,
            status.Capabilities.SocketActivation,
            status.Capabilities.FixLogonImplemented,
            status.Capabilities.CredentialUseImplemented,
            status.Capabilities.OrderSubmissionImplemented,
            status.Capabilities.ReadOnlyOnly,
            status.Capabilities.ShadowReplaySubmitImplemented,
            status.Capabilities.TradingMutationImplemented,
            status.Capabilities.SchedulerImplemented);

    private static LmaxReadOnlyExternalSessionSkeletonSummary ToSkeletonSummary(
        LmaxReadOnlyExternalSessionSkeletonSafetyReport report)
        => new(
            report.ExternalSessionImplementationMode,
            report.SocketActivation,
            report.FixLogonImplemented,
            report.CredentialUseImplemented,
            report.OrderSubmissionImplemented,
            report.ShadowReplaySubmitImplemented,
            report.TradingMutationImplemented,
            report.SchedulerImplemented,
            report.RuntimeGatewayRegistrationImplemented);
}
