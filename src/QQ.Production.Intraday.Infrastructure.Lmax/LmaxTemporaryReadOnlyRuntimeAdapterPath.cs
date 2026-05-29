namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxTemporaryReadOnlyRuntimeAdapterMode
{
    DryRunOnly,
    RealActivationSkeleton,
    ApprovedBoundedExecutableReadOnly
}

public enum LmaxTemporaryReadOnlyRuntimeActivationOutcome
{
    PreflightAborted,
    DryRunAccepted,
    BoundedExecutableReadOnlyAccepted,
    RealActivationNotImplemented,
    RealActivationNotAuthorized,
    SafetyConstraintFailed,
    FutureR10ApprovalRequired
}

public sealed record LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot(
    bool ExternalRunExecuted,
    bool SnapshotExecuted,
    bool ReplayExecuted,
    bool PostEndpointInvoked,
    bool RealSocketOpened,
    bool TcpConnectionAttempted,
    bool TlsHandshakeAttempted,
    bool FixLogonAttempted,
    bool MarketDataRequestSent,
    bool OrderSubmissionExecuted,
    bool TradingStateMutated,
    bool SchedulerStarted,
    bool PollingStarted,
    bool ShadowReplaySubmitted,
    bool ApiWorkerStarted,
    bool RuntimePoweredUp,
    bool RuntimeEnablementExecuted,
    bool RuntimeEnablementPersisted,
    bool DefaultGatewayRegistrationChanged,
    bool CredentialsLoaded,
    bool CredentialsPrinted,
    bool CredentialsStored,
    bool OutputSanitized)
{
    public bool MarketDataRequestWriteAttempted { get; init; }

    public bool MarketDataRequestWriteSucceeded { get; init; }

    public bool MarketDataRequestResponseReadAttempted { get; init; }

    public bool MarketDataRequestReachedBoundedResponseClassification { get; init; }

    public bool MarketDataRequestSentLegacyFlag { get; init; }

    public static LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot DryRunNoNetwork { get; } = new(
        ExternalRunExecuted: false,
        SnapshotExecuted: false,
        ReplayExecuted: false,
        PostEndpointInvoked: false,
        RealSocketOpened: false,
        TcpConnectionAttempted: false,
        TlsHandshakeAttempted: false,
        FixLogonAttempted: false,
        MarketDataRequestSent: false,
        OrderSubmissionExecuted: false,
        TradingStateMutated: false,
        SchedulerStarted: false,
        PollingStarted: false,
        ShadowReplaySubmitted: false,
        ApiWorkerStarted: false,
        RuntimePoweredUp: false,
        RuntimeEnablementExecuted: false,
        RuntimeEnablementPersisted: false,
        DefaultGatewayRegistrationChanged: false,
        CredentialsLoaded: false,
        CredentialsPrinted: false,
        CredentialsStored: false,
        OutputSanitized: true);
}

public sealed record LmaxTemporaryReadOnlyRuntimeActivationRequest(
    string Phase,
    DateTimeOffset CreatedAtUtc,
    LmaxReadOnlyRuntimeActivationGateHarnessResult HarnessResult,
    LmaxTemporaryReadOnlyRuntimeAdapterMode AdapterMode,
    string RequestedNextApprovalPhase,
    string OutputRoot,
    bool BoundedExecutorApproved = false,
    bool RuntimeDelegateBindingApproved = false)
{
    public static LmaxTemporaryReadOnlyRuntimeActivationRequest FromHarnessResult(
        LmaxReadOnlyRuntimeActivationGateHarnessResult harnessResult,
        DateTimeOffset createdAtUtc,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
        string requestedNextApprovalPhase = "LMAX-R10",
        string outputRoot = "artifacts/readiness/lmax-runtime-enablement")
    {
        ArgumentNullException.ThrowIfNull(harnessResult);

        return new LmaxTemporaryReadOnlyRuntimeActivationRequest(
            "LMAX-R9",
            createdAtUtc,
            harnessResult,
            adapterMode,
            requestedNextApprovalPhase,
            outputRoot);
    }
}

public sealed record LmaxTemporaryReadOnlyRuntimeActivationResult(
    string Phase,
    DateTimeOffset CreatedAtUtc,
    LmaxTemporaryReadOnlyRuntimeAdapterMode AdapterMode,
    LmaxTemporaryReadOnlyRuntimeActivationOutcome Outcome,
    bool HarnessOutputConsumed,
    bool HarnessPreflightPassed,
    bool ApprovedInstrumentsOnly,
    bool UsdJpyCaveatPreserved,
    bool DryRunOnly,
    bool FutureR10ApprovalRequired,
    string SanitizedStatus,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues,
    IReadOnlyList<LmaxReadOnlyRuntimeSanitizedInstrumentStatus> InstrumentStatuses,
    LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot SafetySnapshot)
{
    public LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary { get; init; } =
        LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted;

    public LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary { get; init; } =
        LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted;

    public LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary { get; init; } =
        LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted;

    public LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataBoundary { get; init; } =
        LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted;

    public string? TcpBoundarySanitizedStatus { get; init; }

    public string? TcpBoundarySanitizedErrorCategory { get; init; }

    public string? TlsBoundarySanitizedStatus { get; init; }

    public string? TlsBoundarySanitizedErrorCategory { get; init; }

    public string? FixBoundarySanitizedStatus { get; init; }

    public string? FixBoundarySanitizedErrorCategory { get; init; }

    public string? MarketDataBoundarySanitizedStatus { get; init; }

    public string? MarketDataBoundarySanitizedErrorCategory { get; init; }

    public string? MarketDataBoundarySanitizedErrorMessage { get; init; }

    public bool MarketDataRequestWriteAttempted { get; init; }

    public bool MarketDataRequestWriteSucceeded { get; init; }

    public bool MarketDataRequestResponseReadAttempted { get; init; }

    public bool MarketDataRequestReachedBoundedResponseClassification { get; init; }

    public bool MarketDataRequestSentLegacyFlag { get; init; }

    public string? MarketDataRejectSanitizedSubcategory { get; init; }

    public string? SessionRejectSanitizedSubcategory { get; init; }

    public string? RejectReasonExtractionSource { get; init; }

    public string? SessionRejectRefTagIdSanitizedCategory { get; init; }

    public string? SessionRejectReasonSanitizedCategory { get; init; }

    public string? SessionRejectRefMsgTypeSanitizedCategory { get; init; }

    public bool? MarketDataEntriesObserved { get; init; }

    public int? MarketDataSanitizedEntryCount { get; init; }

    public string? MarketDataEntriesEvidenceCategory { get; init; }

    public string? MarketDataEntriesReportingSource { get; init; }

    public string? MarketDataEntriesNotAvailableReason { get; init; }

    public bool LogoutObserved { get; init; }

    public string? LogoutSourceCategory { get; init; }

    public string? LogoutReasonSanitizedCategory { get; init; }

    public bool? LogoutTextPresentSanitized { get; init; }

    public string? LogoutAfterInstrument { get; init; }

    public string? LogoutAfterSecurityIdSanitized { get; init; }

    public string? LogoutTimingCategory { get; init; }

    public string? LogoutReasonExtractionSource { get; init; }

    public string? TransportSanitizedStatus { get; init; }

    public string? TransportSanitizedErrorCategory { get; init; }

    public string? TransportSanitizedErrorMessage { get; init; }

    public LmaxSanitizedTlsBoundaryEvidence TlsEvidence { get; init; } =
        LmaxSanitizedTlsBoundaryEvidence.NotAttempted;

    public bool Passed =>
        Outcome == LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted &&
        HarnessOutputConsumed &&
        HarnessPreflightPassed &&
        ApprovedInstrumentsOnly &&
        UsdJpyCaveatPreserved &&
        DryRunOnly &&
        FutureR10ApprovalRequired &&
        Issues.Count == 0 &&
        SafetySnapshot.OutputSanitized &&
        !SafetySnapshot.ExternalRunExecuted &&
        !SafetySnapshot.RealSocketOpened &&
        !SafetySnapshot.CredentialsLoaded;
}

public interface ILmaxTemporaryReadOnlyRuntimeActivationAdapter
{
    LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
{
    public LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var harness = request.HarnessResult;
        var scope = harness.Scope;

        if (request.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly)
        {
            Add(issues, "AdapterModeNotDryRunOnly", "$.adapterMode", "R9 executable adapter path is dry-run only.");
        }

        if (!string.Equals(request.RequestedNextApprovalPhase, "LMAX-R10", StringComparison.Ordinal))
        {
            Add(issues, "UnexpectedNextApprovalPhase", "$.requestedNextApprovalPhase", "Future activation must be reserved for LMAX-R10.");
        }

        if (!request.OutputRoot.Replace('\\', '/').StartsWith("artifacts/readiness/lmax-runtime-enablement", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "InvalidOutputRoot", "$.outputRoot", "R9 output root must stay under artifacts/readiness/lmax-runtime-enablement.");
        }

        if (!harness.Passed || !harness.PreflightGate.Passed)
        {
            Add(issues, "HarnessPreflightNotPassed", "$.harnessResult.preflightGate", "R7 harness preflight must pass before a temporary adapter request can be considered.");
        }

        if (harness.R8Authorized || harness.ExternalRunExecuted || harness.RuntimeActivationExecuted || harness.CredentialLoadingAdded || harness.LiveConnectionScriptCreated)
        {
            Add(issues, "HarnessIndicatesRuntimePower", "$.harnessResult", "R7 harness output must remain dry-run and non-authorizing.");
        }

        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        issues.AddRange(gate.Issues);

        var approvedInstrumentsOnly = scope.Instruments.All(x => LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(x.Symbol) is not null);
        if (!approvedInstrumentsOnly)
        {
            Add(issues, "NonApprovedInstrument", "$.scope.instruments", "All instruments must be in the approved read-only runtime allowlist.");
        }

        var usdJpyCaveatPreserved = scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        var statuses = scope.Instruments.Select(x => new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
            x.Symbol,
            x.SecurityId,
            x.SecurityIdSource,
            "Demo/read-only",
            LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted,
            issues.Count == 0 ? "DryRunAdapterAcceptedNoNetwork" : "DryRunAdapterBlockedNoNetwork",
            issues.Count == 0 ? null : "SafetyConstraintFailed",
            request.CreatedAtUtc,
            x.Caveat)).ToList();

        var outcome = issues.Count == 0
            ? LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted
            : LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed;

        return new LmaxTemporaryReadOnlyRuntimeActivationResult(
            request.Phase,
            request.CreatedAtUtc,
            request.AdapterMode,
            outcome,
            HarnessOutputConsumed: true,
            HarnessPreflightPassed: harness.PreflightGate.Passed,
            ApprovedInstrumentsOnly: approvedInstrumentsOnly,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            DryRunOnly: true,
            FutureR10ApprovalRequired: true,
            outcome == LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted
                ? "R9 dry-run adapter path accepted the R7 harness output without network, credential, API/Worker, or runtime activation."
                : "R9 dry-run adapter path blocked the request before any network, credential, API/Worker, or runtime activation.",
            issues,
            statuses,
            LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork);
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}

public sealed class LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
{
    public LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return new LmaxTemporaryReadOnlyRuntimeActivationResult(
            request.Phase,
            request.CreatedAtUtc,
            LmaxTemporaryReadOnlyRuntimeAdapterMode.RealActivationSkeleton,
            LmaxTemporaryReadOnlyRuntimeActivationOutcome.RealActivationNotAuthorized,
            HarnessOutputConsumed: true,
            HarnessPreflightPassed: request.HarnessResult.PreflightGate.Passed,
            ApprovedInstrumentsOnly: request.HarnessResult.Scope.Instruments.All(x => LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(x.Symbol) is not null),
            UsdJpyCaveatPreserved: request.HarnessResult.Scope.Instruments
                .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
                .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal)),
            DryRunOnly: false,
            FutureR10ApprovalRequired: true,
            "Real temporary read-only runtime activation is not implemented or authorized in R9.",
            [new LmaxReadOnlyRuntimePreflightIssue("RealActivationNotAuthorized", "$.adapterMode", "R9 provides a non-executing skeleton only; future R10 approval is required.")],
            [],
            LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork);
    }
}
