namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxManualBoundedReadOnlyActivationCallerRequest(
    LmaxBoundedReadOnlyActivationInvocationPathRequest InvocationRequest,
    bool ManualOperatorInvocationRequested,
    bool ManualRunbookReviewed,
    bool SingleAttemptOnly,
    bool NoApiWorkerStartupPath,
    bool NoLiveLauncher,
    bool NoHostedBackgroundService,
    bool NoSchedulerPolling,
    bool NoOrderTradingPath,
    bool ProductionAccountForbidden,
    bool CredentialValuesRead = false,
    bool CredentialValuesReturned = false,
    bool CredentialValuesPrinted = false,
    bool CredentialValuesStored = false,
    bool CredentialValuesSerialized = false,
    bool ExternalBoundaryAttempted = false);

public sealed record LmaxManualBoundedReadOnlyActivationCallerValidationResult(
    bool Passed,
    string Status,
    bool NoApprovedR57OperationalCallerForBoundedInvocationPath,
    bool ApprovedOperationalCallerProvable,
    bool ManualOnly,
    bool SingleAttemptOnly,
    bool CallsBoundedInvocationPath,
    bool InvocationPathCallsExecuteOnce,
    bool ExactPerPhaseOperatorApprovalRequired,
    bool ExactPerPhaseOperatorApprovalPresent,
    bool RetryPhaseReserved,
    bool R42ThroughR56GateChainValid,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool ExternalBoundaryAttempted,
    bool CredentialValuesRead,
    bool CredentialValuesReturned,
    bool CredentialValuesPrinted,
    bool CredentialValuesStored,
    bool CredentialValuesSerialized,
    LmaxTemporaryReadOnlySessionBoundaryStatus CredentialConfigBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataRequestBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataResponseBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus ShutdownRevertBoundary,
    IReadOnlyList<string> OperationalCallerSummary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed record LmaxManualBoundedReadOnlyActivationCallerExecutionResult(
    LmaxManualBoundedReadOnlyActivationCallerValidationResult Validation,
    LmaxBoundedReadOnlyActivationInvocationPathExecutionResult? InvocationResult);

public sealed class LmaxManualBoundedReadOnlyActivationCaller
{
    private readonly LmaxBoundedReadOnlyActivationInvocationPath invocationPath;
    private bool callConsumed;

    public LmaxManualBoundedReadOnlyActivationCaller(
        LmaxBoundedReadOnlyActivationInvocationPath invocationPath)
    {
        this.invocationPath = invocationPath ?? throw new ArgumentNullException(nameof(invocationPath));
    }

    public LmaxManualBoundedReadOnlyActivationCallerValidationResult Validate(
        LmaxManualBoundedReadOnlyActivationCallerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var invocationValidation = invocationPath.Validate(request.InvocationRequest);
        var activationRequest = request.InvocationRequest.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        if (!invocationValidation.Passed)
        {
            Add(issues, "BoundedInvocationPathRegression", "$.invocationPath", "The operational caller requires the R56 bounded invocation path to validate.");
            issues.AddRange(invocationValidation.Issues);
        }

        if (!request.ManualOperatorInvocationRequested || !request.ManualRunbookReviewed)
        {
            Add(issues, "ManualOperatorInvocationMissing", "$.manual", "The operational caller is manual-only and requires explicit operator/manual runbook confirmation.");
        }

        if (!request.SingleAttemptOnly || !invocationValidation.SingleAttemptOnly)
        {
            Add(issues, "SingleAttemptProofMissing", "$.singleAttemptOnly", "The operational caller permits one bounded attempt only.");
        }

        ValidateCommonSafety(request, scope, issues);

        var approvedInstrumentsExact = invocationValidation.ApprovedInstrumentsExact;
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "The operational caller requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = invocationValidation.UsdJpyCaveatPreserved;
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        var r42ThroughR56GateChainValid =
            invocationValidation.R42ConcreteAdapterGateValid &&
            invocationValidation.R44BoundedRuntimeCompositionValid &&
            invocationValidation.R46BoundaryOperationCompositionValid &&
            invocationValidation.R48ProviderExecutionCompositionValid &&
            invocationValidation.R50ConsolidationGateValid &&
            invocationValidation.R52CredentialConfigSourceBindingValid &&
            invocationValidation.R54RetryPhaseReservationRuleValid &&
            invocationValidation.ApprovedBoundedInvocationPathProvable;
        if (!r42ThroughR56GateChainValid)
        {
            Add(issues, "CompositionChainRegression", "$.gateChain", "The operational caller requires the complete R42/R44/R46/R48/R50/R52/R54/R56 gate chain.");
        }

        var passed = issues.Count == 0;
        return new LmaxManualBoundedReadOnlyActivationCallerValidationResult(
            Passed: passed,
            Status: passed
                ? "ApprovedManualOperationalCallerReadyNoExternalActivation"
                : "ApprovedManualOperationalCallerRejected",
            NoApprovedR57OperationalCallerForBoundedInvocationPath: !passed,
            ApprovedOperationalCallerProvable: passed,
            ManualOnly: request.ManualOperatorInvocationRequested && request.ManualRunbookReviewed,
            SingleAttemptOnly: request.SingleAttemptOnly && invocationValidation.SingleAttemptOnly,
            CallsBoundedInvocationPath: true,
            InvocationPathCallsExecuteOnce: invocationValidation.ExistingBoundedExecutorExecuteOncePathUsed,
            ExactPerPhaseOperatorApprovalRequired: invocationValidation.ExactPerPhaseOperatorApprovalRequired,
            ExactPerPhaseOperatorApprovalPresent: invocationValidation.ExactPerPhaseOperatorApprovalPresent,
            RetryPhaseReserved: invocationValidation.RetryPhaseReserved,
            R42ThroughR56GateChainValid: r42ThroughR56GateChainValid,
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath,
            LiveLauncherRequired: !request.NoLiveLauncher,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            ExternalBoundaryAttempted: request.ExternalBoundaryAttempted,
            CredentialValuesRead: request.CredentialValuesRead,
            CredentialValuesReturned: request.CredentialValuesReturned,
            CredentialValuesPrinted: request.CredentialValuesPrinted,
            CredentialValuesStored: request.CredentialValuesStored,
            CredentialValuesSerialized: request.CredentialValuesSerialized,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TcpBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TlsBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            FixLogonBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataRequestBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataResponseBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ShutdownRevertBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            OperationalCallerSummary:
            [
                "Manual-only library operational caller; no Main, API endpoint, hosted service, scheduler, polling loop, or appsettings enablement.",
                "Requires ApprovedBoundedExecutableReadOnly mode through the bounded invocation path validation.",
                "Calls LmaxBoundedReadOnlyActivationInvocationPath.InvokeOnce only through CallOnce after validation passes.",
                "The invocation path then calls LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce."
            ],
            Issues: issues);
    }

    public LmaxManualBoundedReadOnlyActivationCallerExecutionResult CallOnce(
        LmaxManualBoundedReadOnlyActivationCallerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = Validate(request);
        if (!validation.Passed)
        {
            return new LmaxManualBoundedReadOnlyActivationCallerExecutionResult(validation, null);
        }

        if (callConsumed)
        {
            var rejected = validation with
            {
                Passed = false,
                Status = "ApprovedManualOperationalCallerRejected",
                NoApprovedR57OperationalCallerForBoundedInvocationPath = true,
                ApprovedOperationalCallerProvable = false,
                Issues =
                [
                    new LmaxReadOnlyRuntimePreflightIssue(
                        "OperationalCallerAlreadyConsumed",
                        "$.operationalCaller",
                        "The approved manual operational caller permits exactly one call per caller instance.")
                ]
            };
            return new LmaxManualBoundedReadOnlyActivationCallerExecutionResult(rejected, null);
        }

        callConsumed = true;
        var invocationResult = invocationPath.InvokeOnce(request.InvocationRequest, cancellationToken);
        return new LmaxManualBoundedReadOnlyActivationCallerExecutionResult(validation, invocationResult);
    }

    private static void ValidateCommonSafety(
        LmaxManualBoundedReadOnlyActivationCallerRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "The operational caller must not be reachable from API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "The operational caller must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "The operational caller must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "The operational caller must not add scheduler or polling behavior.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "The operational caller must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested)
        {
            Add(issues, "ProductionAccountRisk", "$.productionAccountForbidden", "Production account/config must remain forbidden.");
        }

        if (request.ExternalBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R58 operational caller validation must not attempt credential/config or external boundaries.");
        }

        if (request.CredentialValuesRead ||
            request.CredentialValuesReturned ||
            request.CredentialValuesPrinted ||
            request.CredentialValuesStored ||
            request.CredentialValuesSerialized)
        {
            Add(issues, "CredentialValuesReturnedOrExposed", "$.credentialEvidence", "R58 must not read, return, print, store, or serialize credential values.");
        }
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
