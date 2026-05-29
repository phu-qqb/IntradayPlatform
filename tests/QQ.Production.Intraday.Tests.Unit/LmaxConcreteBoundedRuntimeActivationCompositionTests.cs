using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxConcreteBoundedRuntimeActivationCompositionTests
{
    [Fact]
    public void R43_blocker_is_cleared_for_explicit_bounded_runtime_composition()
    {
        var transport = new ThrowIfRunTransport();
        var composition = new LmaxConcreteBoundedRuntimeActivationComposition(
            new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport));

        var result = composition.Validate(ValidCompositionRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR43BoundedExecutableRuntimeActivationComposition);
        Assert.True(result.BoundedExecutableRuntimeActivationCompositionExplicit);
        Assert.True(result.ConcreteAdapterPresent);
        Assert.True(result.BoundedExecutorPresent);
        Assert.True(result.RuntimeDelegateBindingPresent);
        Assert.True(result.OperationBindingSetPresent);
        Assert.True(result.ProviderClientSetPresent);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
        Assert.Equal(0, transport.RunCount);
    }

    [Fact]
    public void Composition_requires_approved_bounded_executable_readonly_adapter_mode()
    {
        var result = Validate(ValidCompositionRequest(adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly));

        Assert.False(result.Passed);
        Assert.True(result.NoApprovedR43BoundedExecutableRuntimeActivationComposition);
        Assert.Contains(result.Issues, x => x.Code == "ApprovedBoundedExecutableReadOnlyModeMissing");
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
    }

    [Fact]
    public void Composition_requires_bounded_executor_approval()
    {
        var result = Validate(ValidCompositionRequest(boundedExecutorApproved: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.False(result.BoundedExecutorApproved);
        Assert.False(result.ExternalBoundaryAttempted);
    }

    [Fact]
    public void Composition_requires_runtime_delegate_binding_approval()
    {
        var result = Validate(ValidCompositionRequest(runtimeDelegateBindingApproved: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
        Assert.False(result.RuntimeDelegateBindingApproved);
        Assert.False(result.ExternalBoundaryAttempted);
    }

    [Fact]
    public void Composition_rejects_missing_runtime_delegate_binding_result()
    {
        var result = Validate(ValidCompositionRequest(
            runtimeDelegateBinding: LmaxReadOnlyRuntimeCoreDelegateBindingResult.Rejected(["RuntimeDelegateBindingRegression"])));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingRegression");
        Assert.False(result.RuntimeDelegateBindingPresent);
    }

    [Fact]
    public void Composition_preserves_inert_dry_run_adapter_path()
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

        Assert.DoesNotContain("LmaxConcreteBoundedRuntimeActivationComposition", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxConcreteBoundedRuntimeActivationComposition", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("phase-lmax-r44", appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_validation_does_not_attempt_external_boundaries()
    {
        var transport = new ThrowIfRunTransport();
        var composition = new LmaxConcreteBoundedRuntimeActivationComposition(
            new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport));

        var result = composition.Validate(ValidCompositionRequest());

        Assert.True(result.Passed);
        Assert.False(result.ExternalBoundaryAttempted);
        Assert.Equal(0, transport.RunCount);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
    }

    [Fact]
    public void Composition_blocks_forbidden_order_or_trading_scope()
    {
        var result = Validate(ValidCompositionRequest(
            safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)));

        Assert.False(result.Passed);
        Assert.True(result.OrderTradingPathReachable);
        Assert.Contains(result.Issues, x => x.Code == "AllowOrderSubmission");
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
    }

    [Fact]
    public void Composition_preserves_usdjpy_caveat()
    {
        var result = Validate(ValidCompositionRequest());

        Assert.True(result.UsdJpyCaveatPreserved);
        Assert.True(result.ApprovedInstrumentsExact);
    }

    [Fact]
    public void Composition_rejects_weakened_usdjpy_caveat()
    {
        var instruments = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x)
            .ToList();

        var result = Validate(ValidCompositionRequest(instruments: instruments));

        Assert.False(result.Passed);
        Assert.False(result.UsdJpyCaveatPreserved);
        Assert.Contains(result.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    [Fact]
    public void Composition_accepts_next_external_retry_phase_without_weakening_r43_gate()
    {
        var result = Validate(ValidCompositionRequest(requestedNextApprovalPhase: "LMAX-R45"));

        Assert.True(result.Passed);
        Assert.True(result.PhaseReservedForApprovedRetry);
        Assert.False(result.NoApprovedR43BoundedExecutableRuntimeActivationComposition);
    }

    [Fact]
    public void Composition_accepts_consolidated_next_retry_phase_reservation()
    {
        var result = Validate(ValidCompositionRequest(requestedNextApprovalPhase: "LMAX-R53"));

        Assert.True(result.Passed);
        Assert.True(result.PhaseReservedForApprovedRetry);
        Assert.False(result.NoApprovedR43BoundedExecutableRuntimeActivationComposition);
    }

    [Fact]
    public void Composition_accepts_future_odd_retry_phase_without_new_code_edit()
    {
        var result = Validate(ValidCompositionRequest(requestedNextApprovalPhase: "LMAX-R55"));

        Assert.True(result.Passed);
        Assert.True(result.PhaseReservedForApprovedRetry);
        Assert.False(result.NoApprovedR43BoundedExecutableRuntimeActivationComposition);
    }

    [Fact]
    public void Composition_rejects_arbitrary_retry_phase_reservation()
    {
        var result = Validate(ValidCompositionRequest(requestedNextApprovalPhase: "LMAX-R999"));

        Assert.False(result.Passed);
        Assert.False(result.PhaseReservedForApprovedRetry);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    [Theory]
    [InlineData("LMAX-R54")]
    [InlineData("LMAX-R56")]
    [InlineData("LMAX-R42")]
    [InlineData("LMAX-R101")]
    [InlineData("LMAX-R055")]
    [InlineData("LMAX-R55-extra")]
    [InlineData("NQ-R55")]
    public void Composition_rejects_even_malformed_non_lmax_and_out_of_range_retry_phase_reservations(string phase)
    {
        var result = Validate(ValidCompositionRequest(requestedNextApprovalPhase: phase));

        Assert.False(result.Passed);
        Assert.False(result.PhaseReservedForApprovedRetry);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    private static LmaxConcreteBoundedRuntimeActivationCompositionResult Validate(
        LmaxConcreteBoundedRuntimeActivationCompositionRequest request)
    {
        var transport = new ThrowIfRunTransport();
        var composition = new LmaxConcreteBoundedRuntimeActivationComposition(
            new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport));

        var result = composition.Validate(request);

        Assert.Equal(0, transport.RunCount);
        return result;
    }

    private static LmaxConcreteBoundedRuntimeActivationCompositionRequest ValidCompositionRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R43",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true,
        LmaxReadOnlyRuntimeCoreDelegateBindingResult? runtimeDelegateBinding = null)
    {
        var activationRequest = ValidActivationRequest(
            instruments,
            safetyFlags,
            adapterMode,
            requestedNextApprovalPhase,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved);
        var binding = runtimeDelegateBinding ?? ApprovedBinding(activationRequest.HarnessResult.Scope);

        return new LmaxConcreteBoundedRuntimeActivationCompositionRequest(
            activationRequest,
            LmaxTemporaryReadOnlyActivationExecutorOptions.ForApprovedSingleReadOnlyRetry(
                requestedNextApprovalPhase,
                "operator-approval-redacted"),
            binding,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            ProductionAccountForbidden: true);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R43",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 10, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 10, 05, 00, TimeSpan.Zero),
            adapterMode,
            requestedNextApprovalPhase) with
            {
                BoundedExecutorApproved = boundedExecutorApproved,
                RuntimeDelegateBindingApproved = runtimeDelegateBindingApproved
            };
    }

    private static LmaxReadOnlyRuntimeCoreDelegateBindingResult ApprovedBinding(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var bindings = LmaxReadOnlyRuntimeCoreDelegateBindingSet.CreateApproved(
            (_, _, _) => new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "SocketCoreComposedNotExecuted",
                "NoExternalBoundaryAttempted",
                null),
            (_, _, _) => new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "TlsCoreComposedNotExecuted",
                "NoExternalBoundaryAttempted",
                null),
            (_, _, _, _) => new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "FixCoreComposedNotExecuted",
                "NoExternalBoundaryAttempted",
                null),
            (_, composedScope, _) => new LmaxReadOnlyMarketDataSessionClientResult(
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
                null),
            (_, _, _, _) => new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialCoreComposedNotExecuted",
                "NoSecretMaterialReturned",
                null));

        return new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(bindings).Bind(
            scope,
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);
    }

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

    private sealed class ThrowIfRunTransport : ILmaxTemporaryReadOnlyMarketDataTransport
    {
        public int RunCount { get; private set; }

        public LmaxTemporaryReadOnlyTransportResult RunAsync(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            throw new InvalidOperationException("R44 composition validation must not execute transport.");
        }

        public void ShutdownRevert()
        {
        }
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
                new DateTimeOffset(2026, 05, 13, 10, 05, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 13, 10, 05, 01, TimeSpan.Zero),
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
