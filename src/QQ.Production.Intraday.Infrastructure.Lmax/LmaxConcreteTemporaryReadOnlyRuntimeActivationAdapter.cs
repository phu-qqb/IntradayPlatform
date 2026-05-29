namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxTemporaryReadOnlySessionBoundaryStatus
{
    NotAttempted,
    Succeeded,
    Failed,
    FakeSucceeded,
    FakeFailed
}

public sealed record LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
    string Symbol,
    string? SecurityId,
    string? SecurityIdSource,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataBoundary,
    int MarketDataSnapshotCount,
    int MarketDataRequestRejectCount,
    int BusinessMessageRejectCount,
    int SessionRejectCount,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage,
    string? Caveat);

public sealed record LmaxTemporaryReadOnlyTransportResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataBoundary,
    IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> InstrumentStatuses,
    bool OutputSanitized,
    bool CredentialsLoaded,
    bool CredentialsPrinted,
    bool CredentialsStored,
    bool ShutdownRevertCompleted,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage)
{
    public string? TcpBoundarySanitizedStatus { get; init; }

    public string? TcpBoundarySanitizedErrorCategory { get; init; }

    public string? TlsBoundarySanitizedStatus { get; init; }

    public string? TlsBoundarySanitizedErrorCategory { get; init; }

    public string? FixBoundarySanitizedStatus { get; init; }

    public string? FixBoundarySanitizedErrorCategory { get; init; }

    public string? MarketDataBoundarySanitizedStatus { get; init; }

    public string? MarketDataBoundarySanitizedErrorCategory { get; init; }

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

    public LmaxSanitizedTlsBoundaryEvidence TlsEvidence =>
        LmaxSanitizedTlsBoundaryClassifier.Classify(new LmaxReadOnlyBoundaryStepResult(
            TlsBoundary,
            TlsBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded ||
            TlsBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded
                ? "TlsBoundarySucceededSanitized"
                : SanitizedStatus,
            TlsBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded ||
            TlsBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded
                ? null
                : SanitizedErrorCategory,
            SanitizedErrorMessage));
}

public interface ILmaxTemporaryReadOnlyMarketDataTransport
{
    LmaxTemporaryReadOnlyTransportResult RunAsync(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    void ShutdownRevert();
}

public sealed class LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
{
    private readonly ILmaxTemporaryReadOnlyMarketDataTransport transport;

    public LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(ILmaxTemporaryReadOnlyMarketDataTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = ValidateBeforeTransport(request);
        if (issues.Count > 0)
        {
            return Blocked(request, issues);
        }

        LmaxTemporaryReadOnlyTransportResult transportResult;
        try
        {
            transportResult = transport.RunAsync(request.HarnessResult.Scope, cancellationToken);
        }
        finally
        {
            transport.ShutdownRevert();
        }

        var transportIssues = ValidateTransportResult(transportResult);
        var statuses = ToSanitizedStatuses(request, transportResult, transportIssues.Count == 0);
        var outcome = transportIssues.Count == 0
            ? request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly
                ? LmaxTemporaryReadOnlyRuntimeActivationOutcome.BoundedExecutableReadOnlyAccepted
                : LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted
            : LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed;
        var dryRunOnly = request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly;
        var futureR10ApprovalRequired = dryRunOnly;
        var safetySnapshot = BuildSafetySnapshot(request.AdapterMode, transportResult);

        return new LmaxTemporaryReadOnlyRuntimeActivationResult(
            request.Phase,
            request.CreatedAtUtc,
            request.AdapterMode,
            outcome,
            HarnessOutputConsumed: true,
            HarnessPreflightPassed: request.HarnessResult.PreflightGate.Passed,
            ApprovedInstrumentsOnly: ApprovedInstrumentsOnly(request.HarnessResult.Scope),
            UsdJpyCaveatPreserved: UsdJpyCaveatPreserved(request.HarnessResult.Scope),
            DryRunOnly: dryRunOnly,
            FutureR10ApprovalRequired: futureR10ApprovalRequired,
            transportIssues.Count == 0
                ? dryRunOnly
                    ? "Concrete adapter completed dry-run read-only activation simulation with sanitized local boundary evidence."
                    : "Concrete adapter accepted the approved bounded executable read-only path with sanitized bounded evidence."
                : "Concrete adapter blocked transport result because sanitized safety validation failed.",
            transportIssues,
            statuses,
            safetySnapshot) with
            {
                TcpBoundary = transportResult.TcpBoundary,
                TlsBoundary = transportResult.TlsBoundary,
                FixLogonBoundary = transportResult.FixLogonBoundary,
                MarketDataBoundary = transportResult.MarketDataBoundary,
                TcpBoundarySanitizedStatus = transportResult.TcpBoundarySanitizedStatus,
                TcpBoundarySanitizedErrorCategory = transportResult.TcpBoundarySanitizedErrorCategory,
                TlsBoundarySanitizedStatus = transportResult.TlsBoundarySanitizedStatus,
                TlsBoundarySanitizedErrorCategory = transportResult.TlsBoundarySanitizedErrorCategory,
                FixBoundarySanitizedStatus = transportResult.FixBoundarySanitizedStatus,
                FixBoundarySanitizedErrorCategory = transportResult.FixBoundarySanitizedErrorCategory,
                MarketDataBoundarySanitizedStatus = transportResult.MarketDataBoundarySanitizedStatus,
                MarketDataBoundarySanitizedErrorCategory = transportResult.MarketDataBoundarySanitizedErrorCategory,
                MarketDataBoundarySanitizedErrorMessage =
                    transportResult.MarketDataBoundary is LmaxTemporaryReadOnlySessionBoundaryStatus.Failed or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed
                        ? transportResult.SanitizedErrorMessage
                        : null,
                MarketDataRequestWriteAttempted = transportResult.MarketDataRequestWriteAttempted,
                MarketDataRequestWriteSucceeded = transportResult.MarketDataRequestWriteSucceeded,
                MarketDataRequestResponseReadAttempted = transportResult.MarketDataRequestResponseReadAttempted,
                MarketDataRequestReachedBoundedResponseClassification = transportResult.MarketDataRequestReachedBoundedResponseClassification,
                MarketDataRequestSentLegacyFlag = safetySnapshot.MarketDataRequestSent,
                MarketDataRejectSanitizedSubcategory = transportResult.MarketDataRejectSanitizedSubcategory,
                SessionRejectSanitizedSubcategory = transportResult.SessionRejectSanitizedSubcategory,
                RejectReasonExtractionSource = transportResult.RejectReasonExtractionSource,
                SessionRejectRefTagIdSanitizedCategory = transportResult.SessionRejectRefTagIdSanitizedCategory,
                SessionRejectReasonSanitizedCategory = transportResult.SessionRejectReasonSanitizedCategory,
                SessionRejectRefMsgTypeSanitizedCategory = transportResult.SessionRejectRefMsgTypeSanitizedCategory,
                MarketDataEntriesObserved = transportResult.MarketDataEntriesObserved,
                MarketDataSanitizedEntryCount = transportResult.MarketDataSanitizedEntryCount,
                MarketDataEntriesEvidenceCategory = transportResult.MarketDataEntriesEvidenceCategory,
                MarketDataEntriesReportingSource = transportResult.MarketDataEntriesReportingSource,
                MarketDataEntriesNotAvailableReason = transportResult.MarketDataEntriesNotAvailableReason,
                LogoutObserved = transportResult.LogoutObserved,
                LogoutSourceCategory = transportResult.LogoutSourceCategory,
                LogoutReasonSanitizedCategory = transportResult.LogoutReasonSanitizedCategory,
                LogoutTextPresentSanitized = transportResult.LogoutTextPresentSanitized,
                LogoutAfterInstrument = transportResult.LogoutAfterInstrument,
                LogoutAfterSecurityIdSanitized = transportResult.LogoutAfterSecurityIdSanitized,
                LogoutTimingCategory = transportResult.LogoutTimingCategory,
                LogoutReasonExtractionSource = transportResult.LogoutReasonExtractionSource,
                TransportSanitizedStatus = transportResult.SanitizedStatus,
                TransportSanitizedErrorCategory = transportResult.SanitizedErrorCategory,
                TransportSanitizedErrorMessage = transportResult.SanitizedErrorMessage,
                TlsEvidence = transportResult.TlsEvidence
            };
    }

    private static List<LmaxReadOnlyRuntimePreflightIssue> ValidateBeforeTransport(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var harness = request.HarnessResult;
        var scope = harness.Scope;

        if (!harness.Passed || !harness.PreflightGate.Passed)
        {
            Add(issues, "HarnessPreflightNotPassed", "$.harnessResult.preflightGate", "R7/R9 harness preflight must pass before transport use.");
        }

        if (scope.OperatorApproval is null)
        {
            Add(issues, "OperatorApprovalMissing", "$.scope.operatorApproval", "Operator approval is required before transport use.");
        }

        if (request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.RealActivationSkeleton)
        {
            Add(issues, "RealActivationSkeletonNotExecutable", "$.adapterMode", "The R9 real activation skeleton remains non-executable.");
        }

        if (request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            if (!request.BoundedExecutorApproved)
            {
                Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "Approved executable read-only adapter mode requires the bounded executor path.");
            }

            if (!request.RuntimeDelegateBindingApproved)
            {
                Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "Approved executable read-only adapter mode requires runtime delegate binding approval.");
            }

            if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(request.RequestedNextApprovalPhase))
            {
                Add(issues, "UnexpectedExecutableApprovalPhase", "$.requestedNextApprovalPhase", "Approved executable read-only adapter mode is reserved for an operator-approved activation retry phase.");
            }
        }
        else if (request.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly)
        {
            Add(issues, "AdapterModeNotSupported", "$.adapterMode", "Concrete adapter supports only dry-run validation or explicitly approved bounded executable read-only mode.");
        }

        if (harness.ExternalRunExecuted || harness.RuntimeActivationExecuted || harness.CredentialLoadingAdded || harness.LiveConnectionScriptCreated)
        {
            Add(issues, "HarnessIndicatesRuntimePower", "$.harnessResult", "Harness output must remain local-only and non-authorizing.");
        }

        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        issues.AddRange(gate.Issues);

        if (!ApprovedInstrumentsOnly(scope))
        {
            Add(issues, "NonApprovedInstrument", "$.scope.instruments", "Only approved read-only instruments are allowed.");
        }

        if (!UsdJpyCaveatPreserved(scope))
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        return issues;
    }

    private static List<LmaxReadOnlyRuntimePreflightIssue> ValidateTransportResult(
        LmaxTemporaryReadOnlyTransportResult result)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();

        if (!result.OutputSanitized)
        {
            Add(issues, "TransportOutputNotSanitized", "$.transportResult.outputSanitized", "Transport output must be sanitized.");
        }

        if (result.CredentialsPrinted || result.CredentialsStored)
        {
            Add(issues, "TransportCredentialExposure", "$.transportResult.credentials", "Transport must not print or store credentials.");
        }

        if (!result.ShutdownRevertCompleted)
        {
            Add(issues, "ShutdownRevertNotCompleted", "$.transportResult.shutdownRevertCompleted", "Shutdown/revert must complete for every fake transport run.");
        }

        foreach (var status in result.InstrumentStatuses)
        {
            if (LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(status.Symbol) is null)
            {
                Add(issues, "TransportReturnedNonApprovedInstrument", "$.transportResult.instrumentStatuses", $"Transport returned non-approved instrument '{status.Symbol}'.");
            }

            if (string.Equals(status.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "TransportUsdJpyCaveatMissing", "$.transportResult.instrumentStatuses[USDJPY].caveat", "Transport result must preserve USDJPY caveat.");
            }
        }

        return issues;
    }

    private static IReadOnlyList<LmaxReadOnlyRuntimeSanitizedInstrumentStatus> ToSanitizedStatuses(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        LmaxTemporaryReadOnlyTransportResult result,
        bool passed)
        => result.InstrumentStatuses.Select(x => new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
            x.Symbol,
            x.SecurityId,
            x.SecurityIdSource,
            "Demo/read-only",
            ToRuntimeBoundaryStatus(x.MarketDataBoundary),
            passed ? x.SanitizedStatus : "FakeTransportBlockedNoNetwork",
            x.SanitizedErrorCategory,
            request.CreatedAtUtc,
            x.Caveat)
        {
            MarketDataSnapshotCount = Math.Max(0, x.MarketDataSnapshotCount),
            MarketDataRequestRejectCount = Math.Max(0, x.MarketDataRequestRejectCount),
            BusinessMessageRejectCount = Math.Max(0, x.BusinessMessageRejectCount),
            SessionRejectCount = Math.Max(0, x.SessionRejectCount),
            SanitizedReasonCategory = x.SanitizedErrorMessage
        }).ToList();

    private static LmaxTemporaryReadOnlyRuntimeBoundaryStatus ToRuntimeBoundaryStatus(
        LmaxTemporaryReadOnlySessionBoundaryStatus status)
        => status switch
        {
            LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded => LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotApplicableForInertValidation,
            LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed => LmaxTemporaryReadOnlyRuntimeBoundaryStatus.BlockedByPreflight,
            LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded => LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotApplicableForInertValidation,
            LmaxTemporaryReadOnlySessionBoundaryStatus.Failed => LmaxTemporaryReadOnlyRuntimeBoundaryStatus.BlockedByPreflight,
            _ => LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted
        };

    private static LmaxTemporaryReadOnlyRuntimeActivationResult Blocked(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> issues)
        => new(
            request.Phase,
            request.CreatedAtUtc,
            request.AdapterMode,
            LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed,
            HarnessOutputConsumed: true,
            HarnessPreflightPassed: request.HarnessResult.PreflightGate.Passed,
            ApprovedInstrumentsOnly: ApprovedInstrumentsOnly(request.HarnessResult.Scope),
            UsdJpyCaveatPreserved: UsdJpyCaveatPreserved(request.HarnessResult.Scope),
            DryRunOnly: request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
            FutureR10ApprovalRequired: request.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
            "Concrete adapter blocked the request before transport use.",
            issues,
            [],
            LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork);

    private static LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot BuildSafetySnapshot(
        LmaxTemporaryReadOnlyRuntimeAdapterMode mode,
        LmaxTemporaryReadOnlyTransportResult transportResult)
    {
        var executableMode = mode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly;
        var tcpAttempted = executableMode && IsRealBoundaryAttempt(transportResult.TcpBoundary);
        var tlsAttempted = executableMode && IsRealBoundaryAttempt(transportResult.TlsBoundary);
        var fixAttempted = executableMode && IsRealBoundaryAttempt(transportResult.FixLogonBoundary);
        var marketDataAttempted = executableMode && IsRealBoundaryAttempt(transportResult.MarketDataBoundary);
        var runtimePowered = tcpAttempted || tlsAttempted || fixAttempted || marketDataAttempted;

        var snapshot = new LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot(
            ExternalRunExecuted: runtimePowered,
            SnapshotExecuted: executableMode && transportResult.InstrumentStatuses.Any(x => x.MarketDataSnapshotCount > 0 && IsRealBoundaryAttempt(x.MarketDataBoundary)),
            ReplayExecuted: false,
            PostEndpointInvoked: false,
            RealSocketOpened: executableMode && transportResult.TcpBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
            TcpConnectionAttempted: tcpAttempted,
            TlsHandshakeAttempted: tlsAttempted,
            FixLogonAttempted: fixAttempted,
            MarketDataRequestSent: marketDataAttempted,
            OrderSubmissionExecuted: false,
            TradingStateMutated: false,
            SchedulerStarted: false,
            PollingStarted: false,
            ShadowReplaySubmitted: false,
            ApiWorkerStarted: false,
            RuntimePoweredUp: runtimePowered,
            RuntimeEnablementExecuted: runtimePowered,
            RuntimeEnablementPersisted: false,
            DefaultGatewayRegistrationChanged: false,
            CredentialsLoaded: transportResult.CredentialsLoaded,
            CredentialsPrinted: transportResult.CredentialsPrinted,
            CredentialsStored: transportResult.CredentialsStored,
            OutputSanitized: transportResult.OutputSanitized);

        return snapshot with
        {
            MarketDataRequestWriteAttempted = transportResult.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = transportResult.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = transportResult.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = transportResult.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRequestSentLegacyFlag = marketDataAttempted
        };
    }

    private static bool IsRealBoundaryAttempt(LmaxTemporaryReadOnlySessionBoundaryStatus status)
        => status is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
            or LmaxTemporaryReadOnlySessionBoundaryStatus.Failed;

    private static bool ApprovedInstrumentsOnly(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments.All(x => LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(x.Symbol) is not null);

    private static bool UsdJpyCaveatPreserved(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
