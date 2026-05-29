using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public static class LmaxReadOnlyActivationManualExecutionSurfaceFactory
{
    public const string NoExternalBoundaryMode = "no-external-boundary";
    public const string RealBoundedExecutableReadOnlyMode = "real-bounded-executable-readonly";

    public static LmaxReadOnlyActivationManualExecutionSurface CreateForManualTool()
        => CreateForManualTool(NoExternalBoundaryMode);

    public static LmaxReadOnlyActivationManualExecutionSurface CreateForManualTool(string adapterMode)
        => new(CreateRequest, () => CreateCaller(adapterMode));

    public static bool IsApprovedAdapterMode(string adapterMode)
        => string.Equals(adapterMode, NoExternalBoundaryMode, StringComparison.Ordinal) ||
           string.Equals(adapterMode, RealBoundedExecutableReadOnlyMode, StringComparison.Ordinal);

    public static LmaxManualBoundedReadOnlyActivationCaller CreateCaller(string adapterMode)
        => new(new LmaxBoundedReadOnlyActivationInvocationPath(CreateAdapter(adapterMode)));

    public static ILmaxTemporaryReadOnlyRuntimeActivationAdapter CreateAdapter(string adapterMode)
    {
        if (string.Equals(adapterMode, NoExternalBoundaryMode, StringComparison.Ordinal))
        {
            return new LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter();
        }

        if (string.Equals(adapterMode, RealBoundedExecutableReadOnlyMode, StringComparison.Ordinal))
        {
            return CreateRealBoundedExecutableReadOnlyAdapter();
        }

        throw new ArgumentException("Manual LMAX read-only activation adapter mode is not approved.", nameof(adapterMode));
    }

    public static LmaxReadOnlyActivationManualFixCredentialMaterialBindingValidation ValidateFixCredentialMaterialBinding(
        string phase,
        string expectedOperatorApprovalPhrase,
        string operatorApprovalPhrase)
    {
        var command = new LmaxReadOnlyActivationManualExecutionSurfaceCommand(
            phase,
            expectedOperatorApprovalPhrase,
            operatorApprovalPhrase,
            ExecuteOnceRequested: true,
            ManualOperatorConfirmation: true,
            SingleAttemptOnly: true,
            NoApiWorkerStartup: true,
            NoServiceSchedulerPolling: true,
            NoOrderTradingPath: true,
            NoCredentialOutput: true);
        var surfaceValidation = CreateForManualTool(RealBoundedExecutableReadOnlyMode).Validate(command);
        var binding = CredentialBindingResult();
        var policy = new LmaxReadOnlyCredentialAccessPolicy(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: surfaceValidation.Passed,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only");
        var activationRequest = BuildActivationRequest(phase, operatorApprovalPhrase);
        var access = LmaxCredentialConfigSourceBinding.CreateApprovedOperation(binding)(
            new LmaxReadOnlyCredentialConfigOptions(
                "Demo/read-only",
                DemoReadOnly: true,
                "DemoReadOnlyConfigSource",
                ExternalCredentialAccessApproved: true),
            activationRequest.HarnessResult.Scope,
            policy,
            CancellationToken.None);

        return new LmaxReadOnlyActivationManualFixCredentialMaterialBindingValidation(
            BindingName: "LmaxReadOnlyActivationManualFixCredentialMaterialBinding",
            AdapterMode: RealBoundedExecutableReadOnlyMode,
            ExactPerPhaseOperatorApprovalPresent: surfaceValidation.ExactPerPhaseOperatorApprovalPresent,
            RetryPhaseReserved: surfaceValidation.RetryPhaseReserved,
            ManualCliRequired: true,
            ManualRealBoundedPathOnly: true,
            ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady: surfaceValidation.Passed &&
                access.AccessAllowed &&
                access.RealSecretMaterialLoaded,
            RealSecretMaterialAllowedForApprovedManualRetry: policy.RealSecretMaterialAllowedNow,
            RealSecretMaterialLoadedInMemoryForFutureAttempt: access.RealSecretMaterialLoaded,
            CredentialValuesReturned: false,
            SensitiveMaterialReturned: access.SensitiveMaterialReturned,
            SensitiveMaterialPrinted: access.SensitiveMaterialPrinted,
            SensitiveMaterialStored: access.SensitiveMaterialStored,
            SensitiveMaterialSerialized: false,
            ProductionAccountConfigExcluded: true,
            ApiWorkerReachable: false,
            MarketDataRequestBlockedUntilFixSuccess: true,
            ExternalBoundaryAttemptedDuringValidation: false,
            Issues: surfaceValidation.Issues.Select(x => x.Code).ToList());
    }

    private static ILmaxTemporaryReadOnlyRuntimeActivationAdapter CreateRealBoundedExecutableReadOnlyAdapter()
    {
        var endpointBinding = LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData();
        var socketConnector = new LmaxReadOnlyActivationManualTcpSocketConnector(endpointBinding);
        var socketClient = new LmaxRealReadOnlySocketConnectionClient(
            socketConnector.Connect,
            socketConnector.ShutdownRevert);
        var tlsClient = new LmaxRealReadOnlyTlsHandshakeClient(
            socketConnector.AuthenticateTls,
            socketConnector.ShutdownRevert);
        var fixClient = new LmaxRealReadOnlyFixFrameClient(
            socketConnector.OpenFixSession,
            socketConnector.ShutdownRevert);
        var marketDataClient = new LmaxRealReadOnlyMarketDataFrameClient(
            socketConnector.RequestMarketData,
            socketConnector.ShutdownRevert);
        var credentialOperation = LmaxCredentialConfigSourceBinding.CreateApprovedOperation(CredentialBindingResult());
        var credentialClient = new LmaxRealReadOnlyCredentialConfigClient(
            (options, scope, policy, cancellationToken) => credentialOperation(options, scope, policy, cancellationToken));

        var dependencyProviders = new LmaxRealReadOnlyDependencyProviderFactory(
            new LmaxRealReadOnlySocketBoundaryProvider(
                new LmaxReadOnlySocketConnectionOptions(
                    "Demo/read-only",
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedEndpointLabel,
                    endpointBinding.RuntimePort,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    ExternalConnectionExecutionApproved: true),
                socketClient),
            new LmaxRealReadOnlyTlsBoundaryProvider(
                new LmaxReadOnlyTlsConnectionOptions(
                    "Demo/read-only",
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedEndpointLabel,
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedTargetHostLabel,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    "SystemDefaultValidation",
                    ExternalTlsHandshakeExecutionApproved: true),
                tlsClient),
            new LmaxRealReadOnlyFixFrameBoundaryProvider(
                new LmaxReadOnlyFixSessionOptions(
                    "Demo/read-only",
                    "DemoReadOnlySenderCompId",
                    "DemoReadOnlyTargetCompId",
                    30,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes,
                    ExternalFixExecutionApproved: true),
                fixClient),
            new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
                new LmaxReadOnlyMarketDataRequestOptions(
                    "Demo/read-only",
                    DemoReadOnly: true,
                    "ReadOnlyMarketDataRequest",
                    LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
                    TimeSpan.FromSeconds(15),
                    LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
                    ExternalMarketDataRequestExecutionApproved: true),
                marketDataClient),
            new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
                new LmaxReadOnlyCredentialConfigOptions(
                    "Demo/read-only",
                    DemoReadOnly: true,
                    "DemoReadOnlyConfigSource",
                    ExternalCredentialAccessApproved: true),
                credentialClient)).Create();

        var lowLevelDependencies = dependencyProviders.CreateLowLevelDependencySet();
        var sessionClient = lowLevelDependencies.CreateSessionClient(new LmaxReadOnlyCredentialAccessPolicy(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: true,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only"));
        return new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(new LmaxRealReadOnlyMarketDataTransport(sessionClient));
    }

    private static LmaxManualBoundedReadOnlyActivationCallerRequest CreateRequest(
        LmaxReadOnlyActivationManualExecutionSurfaceCommand command)
    {
        var activationRequest = BuildActivationRequest(command.Phase, command.OperatorApprovalPhrase);
        return new LmaxManualBoundedReadOnlyActivationCallerRequest(
            new LmaxBoundedReadOnlyActivationInvocationPathRequest(
                activationRequest,
                LmaxTemporaryReadOnlyActivationExecutorOptions.ForApprovedSingleReadOnlyRetry(command.Phase, "operator-approval-redacted"),
                BoundedResult(),
                BoundaryOperationResult(),
                ProviderExecutionResult(),
                CredentialBindingResult(),
                command.ExpectedOperatorApprovalPhrase,
                command.OperatorApprovalPhrase,
                BoundedExecutorApproved: true,
                RuntimeDelegateBindingApproved: true,
                R42ConcreteAdapterGateValid: true,
                R50ConsolidationGateValid: true,
                R54RetryPhaseReservationRuleValid: true,
                NoApiWorkerStartupPath: true,
                NoLiveLauncher: true,
                NoHostedBackgroundService: true,
                NoSchedulerPolling: true,
                NoOrderTradingPath: true,
                ProductionAccountForbidden: true),
            ManualOperatorInvocationRequested: true,
            ManualRunbookReviewed: true,
            SingleAttemptOnly: true,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            ProductionAccountForbidden: true);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest BuildActivationRequest(string phase, string approvalPhrase)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 13, 40, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate));
        var approval = new LmaxReadOnlyRuntimeOperatorApproval(
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 13, 40, 00, TimeSpan.Zero),
            approvalPhrase,
            phase,
            "Demo/read-only",
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList());
        var scope = harness.Scope with
        {
            Phase = phase,
            Instruments = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            OperatorApproval = approval
        };
        var scopedHarness = harness with
        {
            Scope = scope,
            PreflightGate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope)
        };

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            scopedHarness,
            new DateTimeOffset(2026, 05, 13, 13, 40, 00, TimeSpan.Zero),
            LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            phase) with
            {
                BoundedExecutorApproved = true,
                RuntimeDelegateBindingApproved = true
            };
    }

    private static LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedResult()
        => new(
            Passed: true,
            "ConcreteBoundedRuntimeActivationCompositionReadyNoExternalActivation",
            NoApprovedR43BoundedExecutableRuntimeActivationComposition: false,
            BoundedExecutableRuntimeActivationCompositionExplicit: true,
            ConcreteAdapterPresent: true,
            BoundedExecutorPresent: true,
            RuntimeDelegateBindingPresent: true,
            OperationBindingSetPresent: true,
            ProviderClientSetPresent: true,
            AdapterModeApprovedBoundedExecutableReadOnly: true,
            BoundedExecutorApproved: true,
            RuntimeDelegateBindingApproved: true,
            PhaseReservedForApprovedRetry: true,
            ApprovedInstrumentsExact: true,
            UsdJpyCaveatPreserved: true,
            ProductionAccountAllowed: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            SchedulerPollingRequired: false,
            OrderTradingPathReachable: false,
            ExternalBoundaryAttempted: false,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ["R44 bounded composition ready"],
            []);

    private static LmaxExecutableBoundaryOperationCompositionResult BoundaryOperationResult()
        => new(
            Passed: true,
            "ExecutableBoundaryOperationCompositionReadyNoExternalActivation",
            NoApprovedR45ExecutableBoundaryOperationComposition: false,
            ExecutableBoundaryOperationCompositionExplicit: true,
            ConcreteBoundedRuntimeCompositionUsed: true,
            AdapterModeApprovedBoundedExecutableReadOnly: true,
            BoundedExecutorApproved: true,
            RuntimeDelegateBindingApproved: true,
            CredentialConfigOperationPresent: true,
            TcpSocketOperationPresent: true,
            TlsOperationPresent: true,
            FixLogonSessionOperationPresent: true,
            MarketDataRequestOperationPresent: true,
            MarketDataResponseEntryCapturePresent: true,
            ShutdownRevertOperationPresent: true,
            ApprovedInstrumentsExact: true,
            UsdJpyCaveatPreserved: true,
            ProductionAccountAllowed: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            SchedulerPollingRequired: false,
            OrderTradingPathReachable: false,
            ExternalBoundaryAttempted: false,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ["R46 boundary operations ready"],
            []);

    private static LmaxExternalBoundaryProviderExecutionCompositionResult ProviderExecutionResult()
        => new(
            Passed: true,
            "ExternalBoundaryProviderExecutionCompositionReadyNoExternalActivation",
            NoApprovedR47ExternalBoundaryProviderExecutionComposition: false,
            ExternalBoundaryProviderExecutionCompositionExplicit: true,
            ProviderExecutionCompositionApproved: true,
            ConcreteBoundedRuntimeCompositionUsed: true,
            ExecutableBoundaryOperationCompositionUsed: true,
            AdapterModeApprovedBoundedExecutableReadOnly: true,
            BoundedExecutorApproved: true,
            RuntimeDelegateBindingApproved: true,
            CredentialConfigProviderClientPresent: true,
            TcpSocketProviderClientPresent: true,
            TlsProviderClientPresent: true,
            FixFrameSessionProviderClientPresent: true,
            MarketDataFrameRequestProviderClientPresent: true,
            MarketDataResponseEntryCaptureProviderClientPresent: true,
            ShutdownRevertProviderClientPresent: true,
            CredentialConfigSanitizedValidationOnly: true,
            RealCredentialValuesRead: false,
            CredentialValuesReturned: false,
            ApprovedInstrumentsExact: true,
            UsdJpyCaveatPreserved: true,
            ProductionAccountAllowed: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            SchedulerPollingRequired: false,
            OrderTradingPathReachable: false,
            ExternalBoundaryAttempted: false,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ["R48 provider execution ready"],
            []);

    private static LmaxCredentialConfigSourceBindingResult CredentialBindingResult()
        => new(
            Passed: true,
            "CredentialConfigSourceBindingReadyNoExternalActivation",
            NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad: false,
            ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable: true,
            SourcePresent: true,
            SourceExplicitlyApprovedForBoundedReadOnlyActivation: true,
            SourceReachableOnlyThroughBoundedPath: true,
            SourceStructurallyLoadable: true,
            AdapterModeApprovedBoundedExecutableReadOnly: true,
            BoundedExecutorApproved: true,
            RuntimeDelegateBindingApproved: true,
            ApprovedInstrumentsExact: true,
            UsdJpyCaveatPreserved: true,
            ProductionAccountAllowedOrUsed: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            SchedulerPollingRequired: false,
            OrderTradingPathReachable: false,
            CredentialValuesRead: false,
            CredentialValuesReturned: false,
            CredentialValuesPrinted: false,
            CredentialValuesStored: false,
            CredentialValuesSerialized: false,
            ExternalBoundaryAttempted: false,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            [new LmaxCredentialConfigRequiredFieldPresence("DemoReadOnlyProfilePresent", true)],
            []);
}

public sealed record LmaxReadOnlyActivationManualFixCredentialMaterialBindingValidation(
    string BindingName,
    string AdapterMode,
    bool ExactPerPhaseOperatorApprovalPresent,
    bool RetryPhaseReserved,
    bool ManualCliRequired,
    bool ManualRealBoundedPathOnly,
    bool ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady,
    bool RealSecretMaterialAllowedForApprovedManualRetry,
    bool RealSecretMaterialLoadedInMemoryForFutureAttempt,
    bool CredentialValuesReturned,
    bool SensitiveMaterialReturned,
    bool SensitiveMaterialPrinted,
    bool SensitiveMaterialStored,
    bool SensitiveMaterialSerialized,
    bool ProductionAccountConfigExcluded,
    bool ApiWorkerReachable,
    bool MarketDataRequestBlockedUntilFixSuccess,
    bool ExternalBoundaryAttemptedDuringValidation,
    IReadOnlyList<string> Issues);
