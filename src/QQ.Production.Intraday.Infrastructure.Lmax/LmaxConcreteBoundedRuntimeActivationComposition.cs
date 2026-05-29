namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxConcreteBoundedRuntimeActivationCompositionRequest(
    LmaxTemporaryReadOnlyRuntimeActivationRequest ActivationRequest,
    LmaxTemporaryReadOnlyActivationExecutorOptions ExecutorOptions,
    LmaxReadOnlyRuntimeCoreDelegateBindingResult RuntimeDelegateBinding,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool NoApiWorkerStartupPath,
    bool NoLiveLauncher,
    bool NoHostedBackgroundService,
    bool NoSchedulerPolling,
    bool NoOrderTradingPath,
    bool ProductionAccountForbidden,
    bool ExternalBoundaryAttempted = false);

public sealed record LmaxConcreteBoundedRuntimeActivationCompositionResult(
    bool Passed,
    string Status,
    bool NoApprovedR43BoundedExecutableRuntimeActivationComposition,
    bool BoundedExecutableRuntimeActivationCompositionExplicit,
    bool ConcreteAdapterPresent,
    bool BoundedExecutorPresent,
    bool RuntimeDelegateBindingPresent,
    bool OperationBindingSetPresent,
    bool ProviderClientSetPresent,
    bool AdapterModeApprovedBoundedExecutableReadOnly,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool PhaseReservedForApprovedRetry,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool ExternalBoundaryAttempted,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataRequestBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataResponseBoundary,
    IReadOnlyList<string> MappingSummary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed class LmaxConcreteBoundedRuntimeActivationComposition
{
    private readonly ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter;

    public LmaxConcreteBoundedRuntimeActivationComposition(
        ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter)
    {
        this.activationAdapter = activationAdapter ?? throw new ArgumentNullException(nameof(activationAdapter));
    }

    public LmaxConcreteBoundedRuntimeActivationCompositionResult Validate(
        LmaxConcreteBoundedRuntimeActivationCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var activationRequest = request.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        var concreteAdapterPresent = activationAdapter.GetType() == typeof(LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter);
        if (!concreteAdapterPresent)
        {
            Add(issues, "ConcreteAdapterMissing", "$.activationAdapter", "The approved bounded composition requires the concrete temporary read-only activation adapter.");
        }

        if (activationRequest.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            Add(issues, "ApprovedBoundedExecutableReadOnlyModeMissing", "$.activationRequest.adapterMode", "The bounded executable composition requires ApprovedBoundedExecutableReadOnly adapter mode.");
        }

        if (!request.BoundedExecutorApproved || !activationRequest.BoundedExecutorApproved)
        {
            Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "The bounded executable composition requires bounded executor approval.");
        }

        if (!request.RuntimeDelegateBindingApproved || !activationRequest.RuntimeDelegateBindingApproved)
        {
            Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "The bounded executable composition requires runtime delegate binding approval.");
        }

        if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase))
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.activationRequest.requestedNextApprovalPhase", "The bounded executable composition is reserved for an approved retry phase.");
        }

        if (!request.RuntimeDelegateBinding.Passed ||
            request.RuntimeDelegateBinding.CoreBindingSet is null ||
            request.RuntimeDelegateBinding.CompositionResult is null)
        {
            Add(issues, "RuntimeDelegateBindingRegression", "$.runtimeDelegateBinding", "Runtime delegate binding must be complete before bounded composition is provable.");
        }

        var composition = request.RuntimeDelegateBinding.CompositionResult;
        if (composition?.OperationBindings is null)
        {
            Add(issues, "OperationBindingSetMissing", "$.runtimeDelegateBinding.composition.operationBindings", "Operation bindings must be present in the bounded composition.");
        }

        if (composition?.ProviderClients is null)
        {
            Add(issues, "ProviderClientSetMissing", "$.runtimeDelegateBinding.composition.providerClients", "Provider clients must be present in the bounded composition.");
        }

        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "The bounded composition must not require API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "The bounded composition must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "The bounded composition must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "The bounded composition must not require scheduler or polling.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "The bounded composition must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden)
        {
            Add(issues, "ProductionAccountNotForbidden", "$.productionAccountForbidden", "Production account use must remain forbidden.");
        }

        if (request.ExternalBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R44 composition validation must not attempt external boundaries.");
        }

        if (request.ExecutorOptions.MaxAttemptCount != 1)
        {
            Add(issues, "InvalidMaxAttemptCount", "$.executorOptions.maxAttemptCount", "The bounded composition permits exactly one planned attempt.");
        }

        if (request.ExecutorOptions.RetryCount != 0)
        {
            Add(issues, "RetryNotAllowed", "$.executorOptions.retryCount", "The bounded composition must not add retry logic.");
        }

        if (request.ExecutorOptions.BatchMode)
        {
            Add(issues, "BatchModeNotAllowed", "$.executorOptions.batchMode", "Batch mode is forbidden.");
        }

        if (request.ExecutorOptions.LoopMode)
        {
            Add(issues, "LoopModeNotAllowed", "$.executorOptions.loopMode", "Loop mode is forbidden.");
        }

        if (!request.ExecutorOptions.FutureExternalExecutionApproved)
        {
            Add(issues, "ApprovedRetryExecutorModeMissing", "$.executorOptions.futureExternalExecutionApproved", "The bounded composition must be structurally ready for the next explicitly approved retry.");
        }

        var approvedInstrumentsExact = ApprovedInstrumentsExact(scope, request.ExecutorOptions.ApprovedInstruments);
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "The bounded composition requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = UsdJpyCaveatPreserved(scope);
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        issues.AddRange(gate.Issues);

        _ = new LmaxTemporaryReadOnlyActivationExecutor(request.ExecutorOptions, activationAdapter);

        var passed = issues.Count == 0;
        return new LmaxConcreteBoundedRuntimeActivationCompositionResult(
            Passed: passed,
            Status: passed
                ? "ConcreteBoundedRuntimeActivationCompositionReadyNoExternalActivation"
                : "ConcreteBoundedRuntimeActivationCompositionRejected",
            NoApprovedR43BoundedExecutableRuntimeActivationComposition: !passed,
            BoundedExecutableRuntimeActivationCompositionExplicit: passed,
            ConcreteAdapterPresent: concreteAdapterPresent,
            BoundedExecutorPresent: true,
            RuntimeDelegateBindingPresent: request.RuntimeDelegateBinding.Passed,
            OperationBindingSetPresent: composition?.OperationBindings is not null,
            ProviderClientSetPresent: composition?.ProviderClients is not null,
            AdapterModeApprovedBoundedExecutableReadOnly: activationRequest.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            BoundedExecutorApproved: request.BoundedExecutorApproved && activationRequest.BoundedExecutorApproved,
            RuntimeDelegateBindingApproved: request.RuntimeDelegateBindingApproved && activationRequest.RuntimeDelegateBindingApproved,
            PhaseReservedForApprovedRetry: LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase),
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath || request.RuntimeDelegateBinding.ApiWorkerStartupRequired,
            LiveLauncherRequired: !request.NoLiveLauncher || request.RuntimeDelegateBinding.LiveLauncherRequired,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService || request.RuntimeDelegateBinding.HostedBackgroundServiceRequired,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            ExternalBoundaryAttempted: request.ExternalBoundaryAttempted,
            TcpBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TlsBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            FixLogonBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataRequestBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataResponseBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MappingSummary: request.RuntimeDelegateBinding.MappingSummary,
            Issues: issues);
    }

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

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
