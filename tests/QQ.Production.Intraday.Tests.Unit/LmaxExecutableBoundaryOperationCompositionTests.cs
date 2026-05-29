using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxExecutableBoundaryOperationCompositionTests
{
    [Fact]
    public void R45_blocker_is_cleared_for_explicit_boundary_operation_composition()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR45ExecutableBoundaryOperationComposition);
        Assert.True(result.ExecutableBoundaryOperationCompositionExplicit);
        Assert.True(result.ConcreteBoundedRuntimeCompositionUsed);
        Assert.True(result.CredentialConfigOperationPresent);
        Assert.True(result.TcpSocketOperationPresent);
        Assert.True(result.TlsOperationPresent);
        Assert.True(result.FixLogonSessionOperationPresent);
        Assert.True(result.MarketDataRequestOperationPresent);
        Assert.True(result.MarketDataResponseEntryCapturePresent);
        Assert.True(result.ShutdownRevertOperationPresent);
    }

    [Fact]
    public void Composition_proves_all_required_readonly_boundary_operations_without_executing_them()
    {
        var result = Validate(ValidRequest());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataResponseBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.ShutdownRevertBoundary);
        Assert.False(result.ExternalBoundaryAttempted);
        Assert.Contains("CredentialConfig:True", result.OperationSummary);
        Assert.Contains("MarketDataResponseEntries:True", result.OperationSummary);
        Assert.Contains("ShutdownRevert:True", result.OperationSummary);
    }

    [Fact]
    public void Composition_requires_approved_bounded_executable_readonly_adapter_mode()
    {
        var result = Validate(ValidRequest(adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly));

        Assert.False(result.Passed);
        Assert.True(result.NoApprovedR45ExecutableBoundaryOperationComposition);
        Assert.Contains(result.Issues, x => x.Code == "ApprovedBoundedExecutableReadOnlyModeMissing");
    }

    [Fact]
    public void Composition_requires_bounded_executor_approval()
    {
        var result = Validate(ValidRequest(boundedExecutorApproved: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.False(result.BoundedExecutorApproved);
    }

    [Fact]
    public void Composition_requires_runtime_delegate_binding_approval()
    {
        var result = Validate(ValidRequest(runtimeDelegateBindingApproved: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
        Assert.False(result.RuntimeDelegateBindingApproved);
    }

    [Fact]
    public void Composition_preserves_r44_bounded_runtime_activation_gate()
    {
        var request = ValidRequest() with
        {
            ConcreteBoundedRuntimeCompositionUsed = false
        };

        var result = Validate(request);

        Assert.False(result.Passed);
        Assert.False(result.ConcreteBoundedRuntimeCompositionUsed);
        Assert.Contains(result.Issues, x => x.Code == "ConcreteBoundedRuntimeCompositionMissing");
    }

    [Fact]
    public void Composition_rejects_missing_provider_client_operation_set()
    {
        var request = ValidRequest() with
        {
            ProviderClients = null
        };

        var result = Validate(request);

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "ProviderClientSetMissing");
        Assert.Contains(result.Issues, x => x.Code == "CredentialConfigBoundaryOperationMissing");
        Assert.Contains(result.Issues, x => x.Code == "TcpSocketBoundaryOperationMissing");
        Assert.Contains(result.Issues, x => x.Code == "MarketDataRequestBoundaryOperationMissing");
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

        Assert.DoesNotContain("LmaxExecutableBoundaryOperationComposition", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableBoundaryOperationComposition", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("phase-lmax-r46", appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_does_not_attempt_tcp_tls_fix_or_marketdata_request()
    {
        var counters = new BoundaryCounters();
        var request = ValidRequest(counters: counters);

        var result = Validate(request);

        Assert.True(result.Passed);
        Assert.Equal(0, counters.CredentialAccessCount);
        Assert.Equal(0, counters.SocketConnectCount);
        Assert.Equal(0, counters.TlsHandshakeCount);
        Assert.Equal(0, counters.FixLogonCount);
        Assert.Equal(0, counters.MarketDataReadCount);
        Assert.Equal(0, counters.ShutdownRevertCount);
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
    public void Composition_accepts_next_external_retry_phase()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R47"));

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR45ExecutableBoundaryOperationComposition);
    }

    [Fact]
    public void Composition_accepts_consolidated_next_retry_phase_reservation()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R51"));

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR45ExecutableBoundaryOperationComposition);
    }

    [Fact]
    public void Composition_rejects_arbitrary_retry_phase_reservation()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R999"));

        Assert.False(result.Passed);
        Assert.True(result.NoApprovedR45ExecutableBoundaryOperationComposition);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    private static LmaxExecutableBoundaryOperationCompositionResult Validate(
        LmaxExecutableBoundaryOperationCompositionRequest request)
        => new LmaxExecutableBoundaryOperationComposition().Validate(request);

    private static LmaxExecutableBoundaryOperationCompositionRequest ValidRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R45",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true,
        BoundaryCounters? counters = null)
    {
        var activationRequest = ValidActivationRequest(
            instruments,
            safetyFlags,
            adapterMode,
            requestedNextApprovalPhase,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved);
        var binding = ApprovedBinding(activationRequest.HarnessResult.Scope, counters ?? new BoundaryCounters());
        var operationBinding = binding.CompositionResult is not null
            ? binding
            : ApprovedBinding(ValidActivationRequest().HarnessResult.Scope, counters ?? new BoundaryCounters());
        var bounded = BoundedComposition(activationRequest, binding);

        return new LmaxExecutableBoundaryOperationCompositionRequest(
            activationRequest,
            bounded,
            operationBinding.CompositionResult!.OperationBindings,
            operationBinding.CompositionResult.ProviderClients,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved,
            ConcreteBoundedRuntimeCompositionUsed: true,
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

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R45",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 11, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 11, 05, 00, TimeSpan.Zero),
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
                new DateTimeOffset(2026, 05, 13, 11, 05, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 13, 11, 05, 01, TimeSpan.Zero),
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
