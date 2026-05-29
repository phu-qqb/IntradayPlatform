using System.Reflection;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxBoundedReadOnlyActivationInvocationPathRequest(
    LmaxTemporaryReadOnlyRuntimeActivationRequest ActivationRequest,
    LmaxTemporaryReadOnlyActivationExecutorOptions ExecutorOptions,
    LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedRuntimeComposition,
    LmaxExecutableBoundaryOperationCompositionResult BoundaryOperationComposition,
    LmaxExternalBoundaryProviderExecutionCompositionResult ProviderExecutionComposition,
    LmaxCredentialConfigSourceBindingResult CredentialConfigSourceBinding,
    string ExpectedOperatorApprovalPhrase,
    string OperatorApprovalPhrase,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool R42ConcreteAdapterGateValid,
    bool R50ConsolidationGateValid,
    bool R54RetryPhaseReservationRuleValid,
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
    bool ExternalBoundaryAttempted = false,
    bool CredentialConfigBoundaryAttempted = false);

public sealed record LmaxBoundedReadOnlyActivationInvocationPathValidationResult(
    bool Passed,
    string Status,
    bool NoApprovedR55BoundedRuntimeActivationInvocationPath,
    bool ApprovedBoundedInvocationPathProvable,
    bool ExistingBoundedExecutorExecuteOncePathUsed,
    bool AdapterModeApprovedBoundedExecutableReadOnly,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool ExactPerPhaseOperatorApprovalRequired,
    bool ExactPerPhaseOperatorApprovalPresent,
    bool RetryPhaseReserved,
    bool R42ConcreteAdapterGateValid,
    bool R44BoundedRuntimeCompositionValid,
    bool R46BoundaryOperationCompositionValid,
    bool R48ProviderExecutionCompositionValid,
    bool R50ConsolidationGateValid,
    bool R52CredentialConfigSourceBindingValid,
    bool R54RetryPhaseReservationRuleValid,
    bool SingleAttemptOnly,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool ExternalBoundaryAttempted,
    bool CredentialConfigBoundaryAttempted,
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
    IReadOnlyList<string> InvocationSummary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed record LmaxBoundedReadOnlyActivationInvocationPathExecutionResult(
    LmaxBoundedReadOnlyActivationInvocationPathValidationResult Validation,
    LmaxTemporaryReadOnlyActivationExecutorResult? ExecutorResult);

public sealed class LmaxBoundedReadOnlyActivationInvocationPath
{
    private readonly ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter;
    private bool invocationConsumed;

    public LmaxBoundedReadOnlyActivationInvocationPath(
        ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter)
    {
        this.activationAdapter = activationAdapter ?? throw new ArgumentNullException(nameof(activationAdapter));
    }

    public LmaxBoundedReadOnlyActivationInvocationPathValidationResult Validate(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var activationRequest = request.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        if (activationRequest.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            Add(issues, "ApprovedBoundedExecutableReadOnlyModeMissing", "$.activationRequest.adapterMode", "The bounded invocation path requires ApprovedBoundedExecutableReadOnly adapter mode.");
        }

        if (!request.BoundedExecutorApproved || !activationRequest.BoundedExecutorApproved)
        {
            Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "The bounded invocation path requires bounded executor approval.");
        }

        if (!request.RuntimeDelegateBindingApproved || !activationRequest.RuntimeDelegateBindingApproved)
        {
            Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "The bounded invocation path requires runtime delegate binding approval.");
        }

        var retryPhaseReserved = LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase);
        if (!retryPhaseReserved)
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.activationRequest.requestedNextApprovalPhase", "The bounded invocation path is reserved for an approved odd-numbered LMAX retry phase.");
        }

        ValidateOperatorApproval(request, scope, issues);
        ValidateGateChain(request, issues);
        ValidateExecutorOptions(request.ExecutorOptions, activationRequest, issues);
        ValidateCommonSafety(request, scope, issues);

        var executeOnceMethodPresent = typeof(LmaxTemporaryReadOnlyActivationExecutor).GetMethod(
            nameof(LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce),
            BindingFlags.Public | BindingFlags.Instance) is not null;
        if (!executeOnceMethodPresent)
        {
            Add(issues, "ExecuteOnceNotAvailable", "$.executor", "The approved invocation path must use LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce.");
        }

        var approvedInstrumentsExact = ApprovedInstrumentsExact(scope, request.ExecutorOptions.ApprovedInstruments);
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "The invocation path requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = UsdJpyCaveatPreserved(scope);
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        issues.AddRange(LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);

        var passed = issues.Count == 0;
        return new LmaxBoundedReadOnlyActivationInvocationPathValidationResult(
            Passed: passed,
            Status: passed
                ? "ApprovedBoundedRuntimeActivationInvocationPathReadyNoExternalActivation"
                : "ApprovedBoundedRuntimeActivationInvocationPathRejected",
            NoApprovedR55BoundedRuntimeActivationInvocationPath: !passed,
            ApprovedBoundedInvocationPathProvable: passed,
            ExistingBoundedExecutorExecuteOncePathUsed: executeOnceMethodPresent,
            AdapterModeApprovedBoundedExecutableReadOnly: activationRequest.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            BoundedExecutorApproved: request.BoundedExecutorApproved && activationRequest.BoundedExecutorApproved,
            RuntimeDelegateBindingApproved: request.RuntimeDelegateBindingApproved && activationRequest.RuntimeDelegateBindingApproved,
            ExactPerPhaseOperatorApprovalRequired: true,
            ExactPerPhaseOperatorApprovalPresent: ExactOperatorApprovalPresent(request, scope),
            RetryPhaseReserved: retryPhaseReserved,
            R42ConcreteAdapterGateValid: request.R42ConcreteAdapterGateValid,
            R44BoundedRuntimeCompositionValid: request.BoundedRuntimeComposition.Passed && !request.BoundedRuntimeComposition.NoApprovedR43BoundedExecutableRuntimeActivationComposition,
            R46BoundaryOperationCompositionValid: request.BoundaryOperationComposition.Passed && !request.BoundaryOperationComposition.NoApprovedR45ExecutableBoundaryOperationComposition,
            R48ProviderExecutionCompositionValid: request.ProviderExecutionComposition.Passed && !request.ProviderExecutionComposition.NoApprovedR47ExternalBoundaryProviderExecutionComposition,
            R50ConsolidationGateValid: request.R50ConsolidationGateValid,
            R52CredentialConfigSourceBindingValid: request.CredentialConfigSourceBinding.Passed && !request.CredentialConfigSourceBinding.NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad,
            R54RetryPhaseReservationRuleValid: request.R54RetryPhaseReservationRuleValid,
            SingleAttemptOnly: SingleAttemptOnly(request.ExecutorOptions),
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath,
            LiveLauncherRequired: !request.NoLiveLauncher,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            ExternalBoundaryAttempted: request.ExternalBoundaryAttempted,
            CredentialConfigBoundaryAttempted: request.CredentialConfigBoundaryAttempted,
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
            InvocationSummary:
            [
                "Manual library composition only; no Main, API endpoint, hosted service, scheduler, or polling loop.",
                "Requires ApprovedBoundedExecutableReadOnly mode, bounded executor approval, runtime delegate binding approval, and exact per-phase operator approval.",
                "Invokes LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce only through InvokeOnce after validation passes."
            ],
            Issues: issues);
    }

    public LmaxBoundedReadOnlyActivationInvocationPathExecutionResult InvokeOnce(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = Validate(request);
        if (!validation.Passed)
        {
            return new LmaxBoundedReadOnlyActivationInvocationPathExecutionResult(validation, null);
        }

        if (invocationConsumed)
        {
            var rejected = validation with
            {
                Passed = false,
                Status = "ApprovedBoundedRuntimeActivationInvocationPathRejected",
                NoApprovedR55BoundedRuntimeActivationInvocationPath = true,
                ApprovedBoundedInvocationPathProvable = false,
                Issues =
                [
                    new LmaxReadOnlyRuntimePreflightIssue(
                        "InvocationAlreadyConsumed",
                        "$.invocationPath",
                        "The approved bounded invocation path permits exactly one invocation per path instance.")
                ]
            };

            return new LmaxBoundedReadOnlyActivationInvocationPathExecutionResult(rejected, null);
        }

        invocationConsumed = true;
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(request.ExecutorOptions, activationAdapter);
        var result = executor.ExecuteOnce(request.ActivationRequest, request.OperatorApprovalPhrase, cancellationToken);
        return new LmaxBoundedReadOnlyActivationInvocationPathExecutionResult(validation, result);
    }

    private static void ValidateOperatorApproval(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!ExactOperatorApprovalPresent(request, scope))
        {
            Add(issues, "ExactPerPhaseOperatorApprovalMissing", "$.operatorApproval", "The invocation path requires exact per-phase operator approval text matching the current retry phase.");
        }
    }

    private static bool ExactOperatorApprovalPresent(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var approval = scope.OperatorApproval;
        var phase = request.ActivationRequest.RequestedNextApprovalPhase;
        return approval is not null &&
               string.Equals(approval.ApprovedPhase, phase, StringComparison.Ordinal) &&
               string.Equals(request.ExecutorOptions.PhaseLabel, phase, StringComparison.Ordinal) &&
               string.Equals(request.ExpectedOperatorApprovalPhrase, request.OperatorApprovalPhrase, StringComparison.Ordinal) &&
               string.Equals(approval.ApprovalPhrase, request.OperatorApprovalPhrase, StringComparison.Ordinal) &&
               request.OperatorApprovalPhrase.Contains($"Phase {phase}", StringComparison.Ordinal) &&
               !UnsafeApprovalPhrase(request.OperatorApprovalPhrase);
    }

    private static void ValidateGateChain(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!request.R42ConcreteAdapterGateValid)
        {
            Add(issues, "R42ConcreteAdapterGateRegression", "$.r42ConcreteAdapterGateValid", "The R42 concrete adapter executable path gate must remain valid.");
        }

        if (!request.BoundedRuntimeComposition.Passed || request.BoundedRuntimeComposition.NoApprovedR43BoundedExecutableRuntimeActivationComposition)
        {
            Add(issues, "R44BoundedRuntimeCompositionRegression", "$.boundedRuntimeComposition", "The R44 bounded runtime activation composition gate must remain valid.");
        }

        if (!request.BoundaryOperationComposition.Passed || request.BoundaryOperationComposition.NoApprovedR45ExecutableBoundaryOperationComposition)
        {
            Add(issues, "R46BoundaryOperationCompositionRegression", "$.boundaryOperationComposition", "The R46 executable boundary operation composition gate must remain valid.");
        }

        if (!request.ProviderExecutionComposition.Passed || request.ProviderExecutionComposition.NoApprovedR47ExternalBoundaryProviderExecutionComposition)
        {
            Add(issues, "R48ProviderExecutionCompositionRegression", "$.providerExecutionComposition", "The R48 external boundary provider execution composition gate must remain valid.");
        }

        if (!request.R50ConsolidationGateValid)
        {
            Add(issues, "R50ConsolidationGateRegression", "$.r50ConsolidationGateValid", "The R50 pre-external consolidation gate must remain valid.");
        }

        if (!request.CredentialConfigSourceBinding.Passed || request.CredentialConfigSourceBinding.NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad)
        {
            Add(issues, "R52CredentialConfigBindingRegression", "$.credentialConfigSourceBinding", "The R52 credential/config source binding gate must remain valid.");
        }

        if (!request.R54RetryPhaseReservationRuleValid)
        {
            Add(issues, "R54RetryPhaseReservationRuleRegression", "$.r54RetryPhaseReservationRuleValid", "The R54 retry phase reservation rule must remain valid.");
        }
    }

    private static void ValidateExecutorOptions(
        LmaxTemporaryReadOnlyActivationExecutorOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationRequest activationRequest,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!SingleAttemptOnly(options))
        {
            Add(issues, "SingleAttemptOptionsInvalid", "$.executorOptions", "The bounded invocation path requires maxAttemptCount=1, retryCount=0, batchMode=false, loopMode=false, no persistence, and future external approval.");
        }

        if (!string.Equals(options.PhaseLabel, activationRequest.RequestedNextApprovalPhase, StringComparison.Ordinal))
        {
            Add(issues, "ExecutorPhaseMismatch", "$.executorOptions.phaseLabel", "Executor phase label must match the requested approved retry phase.");
        }
    }

    private static void ValidateCommonSafety(
        LmaxBoundedReadOnlyActivationInvocationPathRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "The invocation path must not be reachable from API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "The invocation path must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "The invocation path must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "The invocation path must not add scheduler or polling behavior.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "The invocation path must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested)
        {
            Add(issues, "ProductionAccountRisk", "$.productionAccountForbidden", "Production account/config must remain forbidden.");
        }

        if (request.ExternalBoundaryAttempted || request.CredentialConfigBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R56 invocation path validation must not attempt credential/config or external boundaries.");
        }

        if (request.CredentialValuesRead ||
            request.CredentialValuesReturned ||
            request.CredentialValuesPrinted ||
            request.CredentialValuesStored ||
            request.CredentialValuesSerialized)
        {
            Add(issues, "CredentialValuesReturnedOrExposed", "$.credentialEvidence", "R56 must not read, return, print, store, or serialize credential values.");
        }
    }

    private static bool SingleAttemptOnly(LmaxTemporaryReadOnlyActivationExecutorOptions options)
        => options.MaxAttemptCount == 1 &&
           options.RetryCount == 0 &&
           !options.BatchMode &&
           !options.LoopMode &&
           options.NoPersistence &&
           options.ShutdownRevertRequired &&
           options.SanitizationRequired &&
           options.FutureExternalExecutionApproved;

    private static bool ApprovedInstrumentsExact(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<string> executorApprovedInstruments)
    {
        var expected = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;
        var actual = scope.Instruments.OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToArray();
        var approved = executorApprovedInstruments.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var expectedSorted = expected.OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToArray();

        return actual.Length == expectedSorted.Length &&
               approved.SequenceEqual(expectedSorted.Select(x => x.Symbol), StringComparer.OrdinalIgnoreCase) &&
               actual.Zip(expectedSorted).All(pair =>
                   string.Equals(pair.First.Symbol, pair.Second.Symbol, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(pair.First.SecurityId, pair.Second.SecurityId, StringComparison.Ordinal) &&
                   string.Equals(pair.First.SecurityIdSource, pair.Second.SecurityIdSource, StringComparison.Ordinal));
    }

    private static bool UsdJpyCaveatPreserved(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));

    private static bool UnsafeApprovalPhrase(string phrase)
        => string.IsNullOrWhiteSpace(phrase) ||
           ContainsRawPasswordMarker(phrase) ||
           phrase.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("token", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("554=", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsRawPasswordMarker(string phrase)
        => phrase.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("password:", StringComparison.OrdinalIgnoreCase);

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
