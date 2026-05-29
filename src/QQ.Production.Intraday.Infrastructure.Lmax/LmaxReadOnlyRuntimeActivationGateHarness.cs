namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyRuntimeActivationGateHarnessRequest(
    string Phase,
    string RequestedByOperatorId,
    DateTimeOffset RequestedAtUtc,
    string FutureApprovalPhraseTemplate,
    IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? Instruments = null,
    LmaxReadOnlyRuntimeSafetyFlags? SafetyFlags = null,
    LmaxReadOnlyRuntimeShutdownRevertRecord? ShutdownRevert = null,
    int MaxRuntimeSeconds = 30,
    string OutputRoot = "artifacts/readiness/lmax-runtime-enablement");

public sealed record LmaxReadOnlyRuntimeApprovalTemplateValidation(
    bool TemplatePresent,
    bool TemplateMatchesExpected,
    bool ActiveAuthorization,
    string ExpectedTemplate,
    string ObservedTemplate,
    string Message);

public sealed record LmaxReadOnlyRuntimeActivationGateHarnessResult(
    string Phase,
    DateTimeOffset CreatedAtUtc,
    LmaxTemporaryReadOnlyRuntimeActivationScope Scope,
    LmaxReadOnlyRuntimeApprovalTemplateValidation ApprovalTemplateValidation,
    LmaxReadOnlyRuntimePreflightGate PreflightGate,
    LmaxReadOnlyRuntimeForbiddenActionValidation ForbiddenActionValidation,
    LmaxReadOnlyRuntimeNonMutationValidation NonMutationValidation,
    LmaxReadOnlyRuntimeRailIsolationValidation RailIsolationValidation,
    bool R8Authorized,
    bool ExternalRunExecuted,
    bool RuntimeActivationExecuted,
    bool CredentialLoadingAdded,
    bool LiveConnectionScriptCreated,
    string FinalDecision)
{
    public bool Passed =>
        ApprovalTemplateValidation.TemplateMatchesExpected &&
        !ApprovalTemplateValidation.ActiveAuthorization &&
        PreflightGate.Passed &&
        ForbiddenActionValidation.Passed &&
        NonMutationValidation.Passed &&
        RailIsolationValidation.Passed &&
        !R8Authorized &&
        !ExternalRunExecuted &&
        !RuntimeActivationExecuted &&
        !CredentialLoadingAdded &&
        !LiveConnectionScriptCreated;
}

public static class LmaxReadOnlyRuntimeActivationGateHarness
{
    public const string ExpectedR8ApprovalPhraseTemplate =
        "I, Philippe, explicitly approve Phase LMAX-R8 for one temporary Demo read-only runtime market-data activation attempt using the local-only R7 gate harness output for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    public static LmaxReadOnlyRuntimeActivationGateHarnessResult BuildDryRunPreflight(
        LmaxReadOnlyRuntimeActivationGateHarnessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instruments = request.Instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;
        var safetyFlags = request.SafetyFlags ?? new LmaxReadOnlyRuntimeSafetyFlags();
        var approvalTemplate = ValidateApprovalTemplate(request.FutureApprovalPhraseTemplate);
        var approval = new LmaxReadOnlyRuntimeOperatorApproval(
            request.RequestedByOperatorId,
            request.RequestedAtUtc,
            request.FutureApprovalPhraseTemplate,
            request.Phase,
            "Demo/read-only",
            instruments.Select(x => x.Symbol).ToList());
        var shutdownRevert = request.ShutdownRevert ?? new LmaxReadOnlyRuntimeShutdownRevertRecord(
            PlanPresent: true,
            ShutdownRequiredAfterAttempt: true,
            RevertRequiredAfterAttempt: true,
            "artifacts/readiness/lmax-runtime-enablement/phase-lmax-r7-shutdown-revert-schema-validation.json");

        var scope = new LmaxTemporaryReadOnlyRuntimeActivationScope(
            request.Phase,
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            instruments,
            safetyFlags,
            approval,
            shutdownRevert,
            request.MaxRuntimeSeconds,
            request.OutputRoot);

        var preflight = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        var forbidden = new LmaxReadOnlyRuntimeForbiddenActionValidation(
            OrdersSubmitted: false,
            OrderPathEnabled: safetyFlags.AllowOrderSubmission || safetyFlags.OrderGatewayRegistered,
            SchedulerStarted: safetyFlags.SchedulerEnabled,
            PollingStarted: safetyFlags.PollingEnabled,
            ReplayExecuted: safetyFlags.ReplayEnabled,
            ShadowReplaySubmitted: safetyFlags.ShadowReplayEnabled,
            TradingStateMutated: safetyFlags.TradingMutationEnabled,
            ProductionAccountUsed: safetyFlags.ProductionAccountRequested);
        var nonMutation = new LmaxReadOnlyRuntimeNonMutationValidation(
            TradingStateMutated: safetyFlags.TradingMutationEnabled,
            PostEndpointInvoked: false,
            RuntimePoweredUp: false,
            CredentialsLoaded: false,
            CredentialsPrinted: false,
            CredentialsStored: false);
        var railIsolation = new LmaxReadOnlyRuntimeRailIsolationValidation(
            ValidatedRailsModified: false,
            Phase7ArchiveModified: false,
            UsdJpyT1T7ArtifactsModified: false,
            NonApprovedInstrumentTouched: instruments.Any(x => LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(x.Symbol) is null));

        return new LmaxReadOnlyRuntimeActivationGateHarnessResult(
            request.Phase,
            request.RequestedAtUtc,
            scope,
            approvalTemplate,
            preflight,
            forbidden,
            nonMutation,
            railIsolation,
            R8Authorized: false,
            ExternalRunExecuted: false,
            RuntimeActivationExecuted: false,
            CredentialLoadingAdded: false,
            LiveConnectionScriptCreated: false,
            "LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION");
    }

    public static LmaxReadOnlyRuntimeApprovalTemplateValidation ValidateApprovalTemplate(string? phrase)
    {
        var observed = phrase ?? string.Empty;
        var present = !string.IsNullOrWhiteSpace(observed);
        var matches = string.Equals(observed, ExpectedR8ApprovalPhraseTemplate, StringComparison.Ordinal);

        return new LmaxReadOnlyRuntimeApprovalTemplateValidation(
            present,
            matches,
            ActiveAuthorization: false,
            ExpectedR8ApprovalPhraseTemplate,
            observed,
            matches
                ? "Future R8 approval phrase template matches exactly; R8 remains not authorized by R7."
                : "Future R8 approval phrase template does not match exactly; R8 remains not authorized.");
    }
}
