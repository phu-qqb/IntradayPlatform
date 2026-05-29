namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxExternalBoundaryProviderExecutionCompositionRequest(
    LmaxTemporaryReadOnlyRuntimeActivationRequest ActivationRequest,
    LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedRuntimeComposition,
    LmaxExecutableBoundaryOperationCompositionResult BoundaryOperationComposition,
    LmaxReadOnlyProviderClientOperationSet? ProviderClients,
    LmaxRealReadOnlyDependencyProviderSet? DependencyProviders,
    LmaxReadOnlySocketConnectionOptions SocketOptions,
    LmaxReadOnlyTlsConnectionOptions TlsOptions,
    LmaxReadOnlyFixSessionOptions FixOptions,
    LmaxReadOnlyMarketDataRequestOptions MarketDataOptions,
    LmaxReadOnlyCredentialConfigOptions CredentialConfigOptions,
    LmaxReadOnlyCredentialAccessPolicy CredentialAccessPolicy,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool ConcreteBoundedRuntimeCompositionUsed,
    bool ExecutableBoundaryOperationCompositionUsed,
    bool ProviderExecutionCompositionApproved,
    bool CredentialConfigValidationOnly,
    bool RealCredentialValuesRead,
    bool CredentialValuesReturned,
    bool NoApiWorkerStartupPath,
    bool NoLiveLauncher,
    bool NoHostedBackgroundService,
    bool NoSchedulerPolling,
    bool NoOrderTradingPath,
    bool ProductionAccountForbidden,
    bool ExternalBoundaryAttempted = false);

public sealed record LmaxExternalBoundaryProviderExecutionCompositionResult(
    bool Passed,
    string Status,
    bool NoApprovedR47ExternalBoundaryProviderExecutionComposition,
    bool ExternalBoundaryProviderExecutionCompositionExplicit,
    bool ProviderExecutionCompositionApproved,
    bool ConcreteBoundedRuntimeCompositionUsed,
    bool ExecutableBoundaryOperationCompositionUsed,
    bool AdapterModeApprovedBoundedExecutableReadOnly,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool CredentialConfigProviderClientPresent,
    bool TcpSocketProviderClientPresent,
    bool TlsProviderClientPresent,
    bool FixFrameSessionProviderClientPresent,
    bool MarketDataFrameRequestProviderClientPresent,
    bool MarketDataResponseEntryCaptureProviderClientPresent,
    bool ShutdownRevertProviderClientPresent,
    bool CredentialConfigSanitizedValidationOnly,
    bool RealCredentialValuesRead,
    bool CredentialValuesReturned,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool ExternalBoundaryAttempted,
    LmaxTemporaryReadOnlySessionBoundaryStatus CredentialConfigBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataRequestBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataResponseBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus ShutdownRevertBoundary,
    IReadOnlyList<string> ProviderExecutionSummary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed class LmaxExternalBoundaryProviderExecutionComposition
{
    private static readonly HashSet<string> AllowedFixMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A",
        "0",
        "1",
        "5",
        "W",
        "Y",
        "j",
        "3",
        "Logon",
        "Logout",
        "Heartbeat",
        "TestRequest",
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject"
    };

    private static readonly HashSet<string> AllowedMarketDataMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "W",
        "Y",
        "j",
        "3",
        "5",
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject",
        "Logout"
    };

    private static readonly string[] UnsafeLabelTokens =
    [
        "://",
        "@",
        "password",
        "secret",
        "credential",
        "554=",
        "35=D",
        "35=F",
        "35=H",
        "35=AE",
        "35=8",
        "-----BEGIN",
        "private"
    ];

    public LmaxExternalBoundaryProviderExecutionCompositionResult Validate(
        LmaxExternalBoundaryProviderExecutionCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var activationRequest = request.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        if (!request.ConcreteBoundedRuntimeCompositionUsed || !request.BoundedRuntimeComposition.Passed)
        {
            Add(issues, "ConcreteBoundedRuntimeCompositionMissing", "$.boundedRuntimeComposition", "External provider execution composition must use the R44 concrete bounded runtime activation composition.");
        }

        if (request.BoundedRuntimeComposition.NoApprovedR43BoundedExecutableRuntimeActivationComposition)
        {
            Add(issues, "R43BoundedRuntimeCompositionBlockerPresent", "$.boundedRuntimeComposition.noApprovedR43BoundedExecutableRuntimeActivationComposition", "R43 bounded runtime composition blocker must stay cleared.");
        }

        if (!request.ExecutableBoundaryOperationCompositionUsed || !request.BoundaryOperationComposition.Passed)
        {
            Add(issues, "ExecutableBoundaryOperationCompositionMissing", "$.boundaryOperationComposition", "External provider execution composition must use the R46 executable boundary operation composition.");
        }

        if (request.BoundaryOperationComposition.NoApprovedR45ExecutableBoundaryOperationComposition)
        {
            Add(issues, "R45ExecutableBoundaryOperationCompositionBlockerPresent", "$.boundaryOperationComposition.noApprovedR45ExecutableBoundaryOperationComposition", "R45 executable boundary operation composition blocker must stay cleared.");
        }

        if (activationRequest.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            Add(issues, "ApprovedBoundedExecutableReadOnlyModeMissing", "$.activationRequest.adapterMode", "External provider execution composition requires ApprovedBoundedExecutableReadOnly adapter mode.");
        }

        if (!request.BoundedExecutorApproved || !activationRequest.BoundedExecutorApproved)
        {
            Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "External provider execution composition requires bounded executor approval.");
        }

        if (!request.RuntimeDelegateBindingApproved || !activationRequest.RuntimeDelegateBindingApproved)
        {
            Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "External provider execution composition requires runtime delegate binding approval.");
        }

        if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase))
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.activationRequest.requestedNextApprovalPhase", "External provider execution composition is reserved for the next approved retry phase.");
        }

        if (!request.ProviderExecutionCompositionApproved)
        {
            Add(issues, "ProviderExecutionCompositionApprovalMissing", "$.providerExecutionCompositionApproved", "External provider execution composition requires explicit provider execution approval proof.");
        }

        var providerClients = request.ProviderClients;
        var dependencyProviders = request.DependencyProviders;
        if (providerClients is null)
        {
            Add(issues, "ProviderClientSetMissing", "$.providerClients", "Provider client operation set must be supplied by the approved runtime composition.");
        }

        if (dependencyProviders is null)
        {
            Add(issues, "DependencyProviderSetMissing", "$.dependencyProviders", "Concrete real dependency provider set must be supplied for provider execution proof.");
        }

        var credentialProviderClientPresent = providerClients?.CredentialConfigClient.GetType() == typeof(LmaxRealReadOnlyCredentialConfigClient) &&
            dependencyProviders?.CredentialConfigProvider.GetType() == typeof(LmaxRealCredentialConfigProvider);
        var socketProviderClientPresent = providerClients?.SocketClient.GetType() == typeof(LmaxRealReadOnlySocketConnectionClient) &&
            dependencyProviders?.SocketProvider.GetType() == typeof(LmaxRealSocketProvider);
        var tlsProviderClientPresent = providerClients?.TlsClient.GetType() == typeof(LmaxRealReadOnlyTlsHandshakeClient) &&
            dependencyProviders?.TlsProvider.GetType() == typeof(LmaxRealTlsStreamProvider);
        var fixProviderClientPresent = providerClients?.FixClient.GetType() == typeof(LmaxRealReadOnlyFixFrameClient) &&
            dependencyProviders?.FixProvider.GetType() == typeof(LmaxRealFixFrameProvider);
        var marketDataProviderClientPresent = providerClients?.MarketDataClient.GetType() == typeof(LmaxRealReadOnlyMarketDataFrameClient) &&
            dependencyProviders?.MarketDataProvider.GetType() == typeof(LmaxRealMarketDataFrameProvider);
        var marketDataResponseCapturePresent =
            request.BoundaryOperationComposition.MarketDataResponseEntryCapturePresent;
        var shutdownRevertPresent = providerClients is not null &&
            HasShutdownRevert(providerClients.SocketClient) &&
            HasShutdownRevert(providerClients.TlsClient) &&
            HasShutdownRevert(providerClients.FixClient) &&
            HasShutdownRevert(providerClients.MarketDataClient);

        if (!credentialProviderClientPresent)
        {
            Add(issues, "CredentialConfigProviderClientMissing", "$.providers.credentialConfig", "Credential/config provider and client must be concrete and approved.");
        }

        if (!socketProviderClientPresent)
        {
            Add(issues, "TcpSocketProviderClientMissing", "$.providers.socket", "TCP/socket provider and client must be concrete and approved.");
        }

        if (!tlsProviderClientPresent)
        {
            Add(issues, "TlsProviderClientMissing", "$.providers.tls", "TLS provider and client must be concrete and approved.");
        }

        if (!fixProviderClientPresent)
        {
            Add(issues, "FixFrameProviderClientMissing", "$.providers.fix", "FIX frame/session provider and client must be concrete and approved.");
        }

        if (!marketDataProviderClientPresent)
        {
            Add(issues, "MarketDataProviderClientMissing", "$.providers.marketData", "MarketData frame/request provider and client must be concrete and approved.");
        }

        if (!marketDataResponseCapturePresent)
        {
            Add(issues, "MarketDataResponseEntryCaptureMissing", "$.marketDataResponse", "MarketDataResponse/entry capture provider proof must be present.");
        }

        if (!shutdownRevertPresent)
        {
            Add(issues, "ShutdownRevertProviderClientMissing", "$.shutdownRevert", "Provider/client shutdown-revert proof must be present.");
        }

        ValidateCredentialOptions(request.CredentialConfigOptions, request.CredentialAccessPolicy, request, issues);
        ValidateSocketOptions(request.SocketOptions, issues);
        ValidateTlsOptions(request.TlsOptions, issues);
        ValidateFixOptions(request.FixOptions, issues);
        ValidateMarketDataOptions(request.MarketDataOptions, issues);
        ValidateCommonSafety(request, scope, issues);

        var approvedInstrumentsExact = ApprovedInstrumentsExact(scope);
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "External provider execution composition requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = UsdJpyCaveatPreserved(scope);
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        issues.AddRange(LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);

        var passed = issues.Count == 0;
        return new LmaxExternalBoundaryProviderExecutionCompositionResult(
            Passed: passed,
            Status: passed
                ? "ExternalBoundaryProviderExecutionCompositionReadyNoExternalActivation"
                : "ExternalBoundaryProviderExecutionCompositionRejected",
            NoApprovedR47ExternalBoundaryProviderExecutionComposition: !passed,
            ExternalBoundaryProviderExecutionCompositionExplicit: passed,
            ProviderExecutionCompositionApproved: request.ProviderExecutionCompositionApproved,
            ConcreteBoundedRuntimeCompositionUsed: request.ConcreteBoundedRuntimeCompositionUsed && request.BoundedRuntimeComposition.Passed,
            ExecutableBoundaryOperationCompositionUsed: request.ExecutableBoundaryOperationCompositionUsed && request.BoundaryOperationComposition.Passed,
            AdapterModeApprovedBoundedExecutableReadOnly: activationRequest.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            BoundedExecutorApproved: request.BoundedExecutorApproved && activationRequest.BoundedExecutorApproved,
            RuntimeDelegateBindingApproved: request.RuntimeDelegateBindingApproved && activationRequest.RuntimeDelegateBindingApproved,
            CredentialConfigProviderClientPresent: credentialProviderClientPresent,
            TcpSocketProviderClientPresent: socketProviderClientPresent,
            TlsProviderClientPresent: tlsProviderClientPresent,
            FixFrameSessionProviderClientPresent: fixProviderClientPresent,
            MarketDataFrameRequestProviderClientPresent: marketDataProviderClientPresent,
            MarketDataResponseEntryCaptureProviderClientPresent: marketDataResponseCapturePresent,
            ShutdownRevertProviderClientPresent: shutdownRevertPresent,
            CredentialConfigSanitizedValidationOnly: request.CredentialConfigValidationOnly &&
                !request.CredentialAccessPolicy.RealSecretMaterialAllowedNow,
            RealCredentialValuesRead: request.RealCredentialValuesRead,
            CredentialValuesReturned: request.CredentialValuesReturned,
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath,
            LiveLauncherRequired: !request.NoLiveLauncher,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            ExternalBoundaryAttempted: request.ExternalBoundaryAttempted,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TcpBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TlsBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            FixLogonBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataRequestBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataResponseBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ShutdownRevertBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ProviderExecutionSummary: BuildSummary(
                credentialProviderClientPresent,
                socketProviderClientPresent,
                tlsProviderClientPresent,
                fixProviderClientPresent,
                marketDataProviderClientPresent,
                marketDataResponseCapturePresent,
                shutdownRevertPresent),
            Issues: issues);
    }

    private static void ValidateCredentialOptions(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxReadOnlyCredentialAccessPolicy policy,
        LmaxExternalBoundaryProviderExecutionCompositionRequest request,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "CredentialConfigProviderConfigRejected", "$.credentialConfigOptions", "Credential/config provider proof requires Demo/read-only options and policy.");
        }

        if (UnsafeLabel(options.SanitizedConfigSourceLabel))
        {
            Add(issues, "UnsafeCredentialConfigSourceLabel", "$.credentialConfigOptions.sanitizedConfigSourceLabel", "Credential/config source label must be sanitized.");
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired || !policy.RedactSensitiveFields)
        {
            Add(issues, "CredentialPolicyNotSafe", "$.credentialAccessPolicy", "Credential/config policy must require future approval and redaction.");
        }

        if (!request.CredentialConfigValidationOnly || policy.RealSecretMaterialAllowedNow)
        {
            Add(issues, "CredentialConfigValidationOnlyMissing", "$.credentialAccessPolicy.realSecretMaterialAllowedNow", "R48 credential/config proof must be validation-only and must not allow real secret material.");
        }

        if (request.RealCredentialValuesRead || request.CredentialValuesReturned)
        {
            Add(issues, "CredentialValuesReturnedOrRead", "$.credentialEvidence", "R48 must not read or return real credential values.");
        }
    }

    private static void ValidateSocketOptions(
        LmaxReadOnlySocketConnectionOptions options,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !options.ExternalConnectionExecutionApproved)
        {
            Add(issues, "SocketProviderExecutionApprovalMissing", "$.socketOptions", "TCP/socket provider proof requires Demo/read-only options with execution approval prepared.");
        }

        if (UnsafeLabel(options.SanitizedEndpointLabel) ||
            options.Port is <= 0 or > 65535 ||
            options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromSeconds(60))
        {
            Add(issues, "SocketProviderOptionsUnsafe", "$.socketOptions", "TCP/socket provider options must be sanitized and bounded.");
        }
    }

    private static void ValidateTlsOptions(
        LmaxReadOnlyTlsConnectionOptions options,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !options.ExternalTlsHandshakeExecutionApproved)
        {
            Add(issues, "TlsProviderExecutionApprovalMissing", "$.tlsOptions", "TLS provider proof requires Demo/read-only options with execution approval prepared.");
        }

        if (UnsafeLabel(options.SanitizedEndpointLabel) ||
            UnsafeLabel(options.SanitizedTargetHostLabel) ||
            options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromSeconds(60) ||
            (!string.Equals(options.CertificateValidationPolicyLabel, "SystemDefaultValidation", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(options.CertificateValidationPolicyLabel, "PinnedPublicCertificateMetadataOnly", StringComparison.OrdinalIgnoreCase)))
        {
            Add(issues, "TlsProviderOptionsUnsafe", "$.tlsOptions", "TLS provider options must be sanitized and bounded.");
        }
    }

    private static void ValidateFixOptions(
        LmaxReadOnlyFixSessionOptions options,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !options.ExternalFixExecutionApproved)
        {
            Add(issues, "FixProviderExecutionApprovalMissing", "$.fixOptions", "FIX provider proof requires Demo/read-only options with execution approval prepared.");
        }

        if (UnsafeLabel(options.SenderCompIdLabel) ||
            UnsafeLabel(options.TargetCompIdLabel) ||
            options.HeartbeatIntervalSeconds is <= 0 or > 60 ||
            options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromSeconds(60) ||
            options.AllowedMessageTypes.Count == 0 ||
            options.AllowedMessageTypes.Any(x => !AllowedFixMessageTypes.Contains(x)))
        {
            Add(issues, "FixProviderOptionsUnsafe", "$.fixOptions", "FIX provider options must be sanitized, read-only, and bounded.");
        }
    }

    private static void ValidateMarketDataOptions(
        LmaxReadOnlyMarketDataRequestOptions options,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !options.ExternalMarketDataRequestExecutionApproved)
        {
            Add(issues, "MarketDataProviderExecutionApprovalMissing", "$.marketDataOptions", "MarketData provider proof requires Demo/read-only options with execution approval prepared.");
        }

        if (UnsafeLabel(options.RequestTypeLabel) ||
            UnsafeLabel(options.SnapshotModeLabel) ||
            options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromSeconds(60) ||
            options.AllowedMessageTypes.Count == 0 ||
            options.AllowedMessageTypes.Any(x => !AllowedMarketDataMessageTypes.Contains(x)))
        {
            Add(issues, "MarketDataProviderOptionsUnsafe", "$.marketDataOptions", "MarketData provider options must be sanitized, read-only, and bounded.");
        }
    }

    private static void ValidateCommonSafety(
        LmaxExternalBoundaryProviderExecutionCompositionRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "External provider execution composition must not require API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "External provider execution composition must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "External provider execution composition must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "External provider execution composition must not require scheduler or polling.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "External provider execution composition must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden)
        {
            Add(issues, "ProductionAccountNotForbidden", "$.productionAccountForbidden", "Production account use must remain forbidden.");
        }

        if (request.ExternalBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R48 provider composition validation must not attempt external boundaries.");
        }

        if (scope.SafetyFlags.ProductionAccountRequested)
        {
            Add(issues, "ProductionAccountRequested", "$.scope.safetyFlags.productionAccountRequested", "Production account is forbidden.");
        }
    }

    private static bool ApprovedInstrumentsExact(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var expected = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actual = scope.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return actual.Length == expected.Length &&
               actual.Zip(expected).All(pair =>
                   string.Equals(pair.First.Symbol, pair.Second.Symbol, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(pair.First.SecurityId, pair.Second.SecurityId, StringComparison.Ordinal) &&
                   string.Equals(pair.First.SecurityIdSource, pair.Second.SecurityIdSource, StringComparison.Ordinal));
    }

    private static bool UsdJpyCaveatPreserved(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));

    private static bool HasShutdownRevert(object client)
        => client.GetType().GetMethod("ShutdownRevert", Type.EmptyTypes) is not null;

    private static bool UnsafeLabel(string value)
        => string.IsNullOrWhiteSpace(value) ||
           UnsafeLabelTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> BuildSummary(
        bool credential,
        bool socket,
        bool tls,
        bool fix,
        bool marketData,
        bool marketDataResponse,
        bool shutdown)
        =>
        [
            $"CredentialConfigProviderClient:{credential}",
            $"TcpSocketProviderClient:{socket}",
            $"TlsProviderClient:{tls}",
            $"FixFrameSessionProviderClient:{fix}",
            $"MarketDataFrameRequestProviderClient:{marketData}",
            $"MarketDataResponseEntries:{marketDataResponse}",
            $"ShutdownRevertProviderClient:{shutdown}"
        ];

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
