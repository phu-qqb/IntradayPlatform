using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxExternalBoundaryProviderExecutionCompositionTests
{
    [Fact]
    public void R47_blocker_is_cleared_for_explicit_external_provider_execution_composition()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR47ExternalBoundaryProviderExecutionComposition);
        Assert.True(result.ExternalBoundaryProviderExecutionCompositionExplicit);
        Assert.True(result.ProviderExecutionCompositionApproved);
        Assert.True(result.CredentialConfigProviderClientPresent);
        Assert.True(result.TcpSocketProviderClientPresent);
        Assert.True(result.TlsProviderClientPresent);
        Assert.True(result.FixFrameSessionProviderClientPresent);
        Assert.True(result.MarketDataFrameRequestProviderClientPresent);
        Assert.True(result.MarketDataResponseEntryCaptureProviderClientPresent);
        Assert.True(result.ShutdownRevertProviderClientPresent);
    }

    [Fact]
    public void Composition_proves_provider_execution_path_without_attempting_boundaries_or_reading_secrets()
    {
        var counters = new BoundaryCounters();
        var result = Validate(ValidRequest(counters: counters));

        Assert.True(result.Passed);
        Assert.True(result.CredentialConfigSanitizedValidationOnly);
        Assert.False(result.RealCredentialValuesRead);
        Assert.False(result.CredentialValuesReturned);
        Assert.False(result.ExternalBoundaryAttempted);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.CredentialConfigBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataResponseBoundary);
        Assert.Equal(0, counters.CredentialAccessCount);
        Assert.Equal(0, counters.SocketConnectCount);
        Assert.Equal(0, counters.TlsHandshakeCount);
        Assert.Equal(0, counters.FixLogonCount);
        Assert.Equal(0, counters.MarketDataReadCount);
        Assert.Equal(0, counters.ShutdownRevertCount);
    }

    [Fact]
    public void Composition_requires_approved_bounded_executable_readonly_adapter_mode()
    {
        var result = Validate(ValidRequest(adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly));

        Assert.False(result.Passed);
        Assert.True(result.NoApprovedR47ExternalBoundaryProviderExecutionComposition);
        Assert.Contains(result.Issues, x => x.Code == "ApprovedBoundedExecutableReadOnlyModeMissing");
    }

    [Fact]
    public void Composition_requires_bounded_executor_approval()
    {
        var result = Validate(ValidRequest(boundedExecutorApproved: false));

        Assert.False(result.Passed);
        Assert.False(result.BoundedExecutorApproved);
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
    }

    [Fact]
    public void Composition_requires_runtime_delegate_binding_approval()
    {
        var result = Validate(ValidRequest(runtimeDelegateBindingApproved: false));

        Assert.False(result.Passed);
        Assert.False(result.RuntimeDelegateBindingApproved);
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
    }

    [Fact]
    public void Composition_requires_explicit_provider_execution_approval()
    {
        var result = Validate(ValidRequest(providerExecutionCompositionApproved: false));

        Assert.False(result.Passed);
        Assert.False(result.ProviderExecutionCompositionApproved);
        Assert.Contains(result.Issues, x => x.Code == "ProviderExecutionCompositionApprovalMissing");
    }

    [Fact]
    public void Composition_preserves_r44_bounded_runtime_activation_gate()
    {
        var result = Validate(ValidRequest(concreteBoundedRuntimeCompositionUsed: false));

        Assert.False(result.Passed);
        Assert.False(result.ConcreteBoundedRuntimeCompositionUsed);
        Assert.Contains(result.Issues, x => x.Code == "ConcreteBoundedRuntimeCompositionMissing");
    }

    [Fact]
    public void Composition_preserves_r46_executable_boundary_operation_gate()
    {
        var result = Validate(ValidRequest(executableBoundaryOperationCompositionUsed: false));

        Assert.False(result.Passed);
        Assert.False(result.ExecutableBoundaryOperationCompositionUsed);
        Assert.Contains(result.Issues, x => x.Code == "ExecutableBoundaryOperationCompositionMissing");
    }

    [Fact]
    public void Composition_rejects_missing_concrete_dependency_provider_set()
    {
        var result = Validate(ValidRequest(includeDependencyProviders: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "DependencyProviderSetMissing");
        Assert.Contains(result.Issues, x => x.Code == "TcpSocketProviderClientMissing");
        Assert.Contains(result.Issues, x => x.Code == "MarketDataProviderClientMissing");
    }

    [Fact]
    public void Composition_requires_execution_approval_flags_for_external_noncredential_providers()
    {
        var result = Validate(ValidRequest(socketOptions: ApprovedSocketOptions() with
        {
            ExternalConnectionExecutionApproved = false
        }));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "SocketProviderExecutionApprovalMissing");
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
    }

    [Fact]
    public void Composition_rejects_real_credential_value_access_in_r48()
    {
        var result = Validate(ValidRequest(
            credentialAccessPolicy: CredentialPolicy() with { RealSecretMaterialAllowedNow = true },
            realCredentialValuesRead: true,
            credentialValuesReturned: true));

        Assert.False(result.Passed);
        Assert.True(result.RealCredentialValuesRead);
        Assert.True(result.CredentialValuesReturned);
        Assert.Contains(result.Issues, x => x.Code == "CredentialConfigValidationOnlyMissing");
        Assert.Contains(result.Issues, x => x.Code == "CredentialValuesReturnedOrRead");
    }

    [Fact]
    public void Composition_preserves_inert_dry_run_path()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);
        var dryRun = adapter.ValidateAsync(ValidActivationRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
            requestedNextApprovalPhase: "LMAX-R10",
            boundedExecutorApproved: false,
            runtimeDelegateBindingApproved: false));

        Assert.True(dryRun.Passed);
        Assert.True(dryRun.DryRunOnly);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted, dryRun.Outcome);
        Assert.False(dryRun.SafetySnapshot.ExternalRunExecuted);
        Assert.Equal(1, transport.RunCount);
    }

    [Fact]
    public void Composition_is_not_reachable_from_api_worker_or_default_startup()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));
        var workerPath = Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerPath) ? File.ReadAllText(workerPath) : string.Empty;

        Assert.DoesNotContain("LmaxExternalBoundaryProviderExecutionComposition", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExternalBoundaryProviderExecutionComposition", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("phase-lmax-r48", appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_blocks_forbidden_order_or_trading_scope()
    {
        var result = Validate(ValidRequest(
            safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)));

        Assert.False(result.Passed);
        Assert.True(result.OrderTradingPathReachable);
        Assert.Contains(result.Issues, x => x.Code == "AllowOrderSubmission");
    }

    [Fact]
    public void Composition_preserves_usdjpy_caveat()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.UsdJpyCaveatPreserved);
        Assert.True(result.ApprovedInstrumentsExact);
    }

    [Fact]
    public void Composition_rejects_weakened_usdjpy_caveat()
    {
        var instruments = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x)
            .ToList();

        var result = Validate(ValidRequest(instruments: instruments));

        Assert.False(result.Passed);
        Assert.False(result.UsdJpyCaveatPreserved);
        Assert.Contains(result.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    [Fact]
    public void Composition_accepts_consolidated_next_retry_phase_reservation()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R51"));

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR47ExternalBoundaryProviderExecutionComposition);
        Assert.False(result.ExternalBoundaryAttempted);
    }

    [Fact]
    public void Composition_rejects_arbitrary_retry_phase_reservation()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R999"));

        Assert.False(result.Passed);
        Assert.True(result.NoApprovedR47ExternalBoundaryProviderExecutionComposition);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    private static LmaxExternalBoundaryProviderExecutionCompositionResult Validate(
        LmaxExternalBoundaryProviderExecutionCompositionRequest request)
        => new LmaxExternalBoundaryProviderExecutionComposition().Validate(request);

    private static LmaxExternalBoundaryProviderExecutionCompositionRequest ValidRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R49",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true,
        bool providerExecutionCompositionApproved = true,
        bool concreteBoundedRuntimeCompositionUsed = true,
        bool executableBoundaryOperationCompositionUsed = true,
        bool includeDependencyProviders = true,
        LmaxReadOnlySocketConnectionOptions? socketOptions = null,
        LmaxReadOnlyCredentialAccessPolicy? credentialAccessPolicy = null,
        bool realCredentialValuesRead = false,
        bool credentialValuesReturned = false,
        BoundaryCounters? counters = null)
    {
        counters ??= new BoundaryCounters();
        var activationRequest = ValidActivationRequest(
            instruments,
            safetyFlags,
            adapterMode,
            requestedNextApprovalPhase,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved);
        var binding = ApprovedBinding(activationRequest.HarnessResult.Scope, counters);
        var bindingForProviderProof = binding.CompositionResult is not null
            ? binding
            : ApprovedBinding(ValidActivationRequest(requestedNextApprovalPhase: requestedNextApprovalPhase).HarnessResult.Scope, counters);
        var bounded = BoundedComposition(activationRequest, binding);
        var operationComposition = OperationComposition(activationRequest, bounded, bindingForProviderProof);
        var providerClients = bindingForProviderProof.CompositionResult!.ProviderClients!;

        return new LmaxExternalBoundaryProviderExecutionCompositionRequest(
            activationRequest,
            bounded,
            operationComposition,
            providerClients,
            includeDependencyProviders
                ? DependencyProviders(providerClients, socketOptions ?? ApprovedSocketOptions())
                : null,
            socketOptions ?? ApprovedSocketOptions(),
            ApprovedTlsOptions(),
            ApprovedFixOptions(),
            ApprovedMarketDataOptions(),
            CredentialOptions(),
            credentialAccessPolicy ?? CredentialPolicy(),
            boundedExecutorApproved,
            runtimeDelegateBindingApproved,
            concreteBoundedRuntimeCompositionUsed,
            executableBoundaryOperationCompositionUsed,
            providerExecutionCompositionApproved,
            CredentialConfigValidationOnly: true,
            realCredentialValuesRead,
            credentialValuesReturned,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            ProductionAccountForbidden: true);
    }

    private static LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedComposition(
        LmaxTemporaryReadOnlyRuntimeActivationRequest activationRequest,
        LmaxReadOnlyRuntimeCoreDelegateBindingResult binding)
        => new LmaxConcreteBoundedRuntimeActivationComposition(
            new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(FakeTransport.Success())).Validate(
                new LmaxConcreteBoundedRuntimeActivationCompositionRequest(
                    activationRequest,
                    LmaxTemporaryReadOnlyActivationExecutorOptions.ForApprovedSingleReadOnlyRetry(
                        activationRequest.RequestedNextApprovalPhase,
                        "operator-approval-redacted"),
                    binding,
                    activationRequest.BoundedExecutorApproved,
                    activationRequest.RuntimeDelegateBindingApproved,
                    NoApiWorkerStartupPath: true,
                    NoLiveLauncher: true,
                    NoHostedBackgroundService: true,
                    NoSchedulerPolling: true,
                    NoOrderTradingPath: true,
                    ProductionAccountForbidden: true));

    private static LmaxExecutableBoundaryOperationCompositionResult OperationComposition(
        LmaxTemporaryReadOnlyRuntimeActivationRequest activationRequest,
        LmaxConcreteBoundedRuntimeActivationCompositionResult bounded,
        LmaxReadOnlyRuntimeCoreDelegateBindingResult binding)
        => new LmaxExecutableBoundaryOperationComposition().Validate(
            new LmaxExecutableBoundaryOperationCompositionRequest(
                activationRequest,
                bounded,
                binding.CompositionResult!.OperationBindings,
                binding.CompositionResult.ProviderClients,
                activationRequest.BoundedExecutorApproved,
                activationRequest.RuntimeDelegateBindingApproved,
                ConcreteBoundedRuntimeCompositionUsed: true,
                NoApiWorkerStartupPath: true,
                NoLiveLauncher: true,
                NoHostedBackgroundService: true,
                NoSchedulerPolling: true,
                NoOrderTradingPath: true,
                ProductionAccountForbidden: true));

    private static LmaxRealReadOnlyDependencyProviderSet DependencyProviders(
        LmaxReadOnlyProviderClientOperationSet providerClients,
        LmaxReadOnlySocketConnectionOptions socketOptions)
        => new LmaxRealReadOnlyDependencyProviderFactory(
            new LmaxRealReadOnlySocketBoundaryProvider(socketOptions, providerClients.SocketClient),
            new LmaxRealReadOnlyTlsBoundaryProvider(ApprovedTlsOptions(), providerClients.TlsClient),
            new LmaxRealReadOnlyFixFrameBoundaryProvider(ApprovedFixOptions(), providerClients.FixClient),
            new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(ApprovedMarketDataOptions(), providerClients.MarketDataClient),
            new LmaxRealReadOnlyCredentialConfigBoundaryProvider(CredentialOptions(), providerClients.CredentialConfigClient)).Create();

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R49",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 12, 05, 00, TimeSpan.Zero),
            adapterMode,
            requestedNextApprovalPhase) with
            {
                BoundedExecutorApproved = boundedExecutorApproved,
                RuntimeDelegateBindingApproved = runtimeDelegateBindingApproved
            };
    }

    private static LmaxReadOnlyRuntimeCoreDelegateBindingResult ApprovedBinding(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        BoundaryCounters counters)
    {
        var bindings = LmaxReadOnlyRuntimeCoreDelegateBindingSet.CreateApproved(
            (_, _, _) =>
            {
                counters.SocketConnectCount++;
                return NotAttempted("SocketCoreComposedNotExecuted");
            },
            (_, _, _) =>
            {
                counters.TlsHandshakeCount++;
                return NotAttempted("TlsCoreComposedNotExecuted");
            },
            (_, _, _, _) =>
            {
                counters.FixLogonCount++;
                return NotAttempted("FixCoreComposedNotExecuted");
            },
            (_, composedScope, _) =>
            {
                counters.MarketDataReadCount++;
                return new LmaxReadOnlyMarketDataSessionClientResult(
                    composedScope.Instruments.Select(x => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                        x.Symbol,
                        x.SecurityId,
                        x.SecurityIdSource,
                        LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                        0,
                        0,
                        0,
                        0,
                        "MarketDataCoreComposedNotExecuted",
                        "NoExternalBoundaryAttempted",
                        null,
                        x.Caveat)).ToList(),
                    "MarketDataCoreComposedNotExecuted",
                    "NoExternalBoundaryAttempted",
                    null);
            },
            (_, _, _, _) =>
            {
                counters.CredentialAccessCount++;
                return new LmaxRealReadOnlySecretAccessResult(
                    AccessAllowed: true,
                    RealSecretMaterialLoaded: false,
                    SensitiveMaterialReturned: false,
                    SensitiveMaterialPrinted: false,
                    SensitiveMaterialStored: false,
                    "CredentialCoreComposedNotExecuted",
                    "NoSecretMaterialReturned",
                    null);
            });

        return new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(bindings).Bind(
            scope,
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);
    }

    private static LmaxRealReadOnlyDependencyResult NotAttempted(string status)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            "NoExternalBoundaryAttempted",
            null);

    private static LmaxReadOnlySocketConnectionOptions ApprovedSocketOptions()
        => new(
            "Demo/read-only",
            "DemoReadOnlyEndpoint",
            443,
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            ExternalConnectionExecutionApproved: true);

    private static LmaxReadOnlyTlsConnectionOptions ApprovedTlsOptions()
        => new(
            "Demo/read-only",
            "DemoReadOnlyEndpoint",
            "DemoReadOnlyTargetHost",
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            "SystemDefaultValidation",
            ExternalTlsHandshakeExecutionApproved: true);

    private static LmaxReadOnlyFixSessionOptions ApprovedFixOptions()
        => new(
            "Demo/read-only",
            "DemoSenderCompId",
            "DemoTargetCompId",
            30,
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes,
            ExternalFixExecutionApproved: true);

    private static LmaxReadOnlyMarketDataRequestOptions ApprovedMarketDataOptions()
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            "ReadOnlyMarketDataRequest",
            "SnapshotOrStatus",
            TimeSpan.FromSeconds(15),
            LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
            ExternalMarketDataRequestExecutionApproved: true);

    private static LmaxReadOnlyCredentialConfigOptions CredentialOptions()
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            "DemoReadOnlyConfigSource",
            ExternalCredentialAccessApproved: false);

    private static LmaxReadOnlyCredentialAccessPolicy CredentialPolicy()
        => new(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: false,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only");

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class BoundaryCounters
    {
        public int CredentialAccessCount { get; set; }
        public int SocketConnectCount { get; set; }
        public int TlsHandshakeCount { get; set; }
        public int FixLogonCount { get; set; }
        public int MarketDataReadCount { get; set; }
        public int ShutdownRevertCount { get; set; }
    }

    private sealed class FakeTransport : ILmaxTemporaryReadOnlyMarketDataTransport
    {
        public int RunCount { get; private set; }

        public static FakeTransport Success() => new();

        public LmaxTemporaryReadOnlyTransportResult RunAsync(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            var instruments = scope.Instruments.Select(x => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                x.Symbol,
                x.SecurityId,
                x.SecurityIdSource,
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                1,
                0,
                0,
                0,
                "FakeMarketDataStatusSucceededNoNetwork",
                null,
                null,
                x.Caveat)).ToList();

            return new LmaxTemporaryReadOnlyTransportResult(
                new DateTimeOffset(2026, 05, 13, 12, 05, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 13, 12, 05, 01, TimeSpan.Zero),
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                instruments,
                OutputSanitized: true,
                CredentialsLoaded: false,
                CredentialsPrinted: false,
                CredentialsStored: false,
                ShutdownRevertCompleted: true,
                "FakeTransportCompletedNoNetwork",
                null,
                null);
        }

        public void ShutdownRevert()
        {
        }
    }
}
