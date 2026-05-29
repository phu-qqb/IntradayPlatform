using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxBoundedReadOnlyActivationInvocationPathTests
{
    private const string R55ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R55 for one temporary Demo read-only runtime market-data activation retry after the R54 retry phase reservation rule fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    [Fact]
    public void R55_invocation_path_blocker_is_cleared_for_approved_bounded_path()
    {
        var adapter = new FakeActivationAdapter();
        var path = new LmaxBoundedReadOnlyActivationInvocationPath(adapter);

        var result = path.Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR55BoundedRuntimeActivationInvocationPath);
        Assert.True(result.ApprovedBoundedInvocationPathProvable);
        Assert.True(result.ExistingBoundedExecutorExecuteOncePathUsed);
        Assert.True(result.SingleAttemptOnly);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public void Invoke_once_uses_existing_bounded_executor_execute_once_path()
    {
        var adapter = new FakeActivationAdapter();
        var path = new LmaxBoundedReadOnlyActivationInvocationPath(adapter);

        var result = path.InvokeOnce(ValidRequest());

        Assert.True(result.Validation.Passed);
        Assert.NotNull(result.ExecutorResult);
        Assert.True(result.ExecutorResult.ValidationPassed);
        Assert.True(result.ExecutorResult.ExecutionStarted);
        Assert.Equal(1, result.ExecutorResult.AttemptsExecuted);
        Assert.Equal(1, adapter.Calls);
        Assert.False(adapter.RealCredentialReadAttempted);
        Assert.False(adapter.RealTcpConnectionAttempted);
        Assert.False(adapter.RealTlsHandshakeAttempted);
        Assert.False(adapter.RealFixLogonAttempted);
        Assert.False(adapter.RealMarketDataRequestSent);
    }

    [Fact]
    public void Invocation_path_permits_exactly_one_invoke_per_instance()
    {
        var adapter = new FakeActivationAdapter();
        var path = new LmaxBoundedReadOnlyActivationInvocationPath(adapter);

        var first = path.InvokeOnce(ValidRequest());
        var second = path.InvokeOnce(ValidRequest());

        Assert.True(first.Validation.Passed);
        Assert.False(second.Validation.Passed);
        Assert.Contains(second.Validation.Issues, x => x.Code == "InvocationAlreadyConsumed");
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public void Exact_per_phase_operator_approval_is_required()
    {
        var request = ValidRequest(operatorApprovalPhrase: "I approve something else.");

        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter()).Validate(request);

        Assert.False(result.Passed);
        Assert.False(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.Contains(result.Issues, x => x.Code == "ExactPerPhaseOperatorApprovalMissing");
    }

    [Theory]
    [InlineData("LMAX-R54")]
    [InlineData("LMAX-R56")]
    [InlineData("LMAX-R101")]
    [InlineData("LMAX-R055")]
    [InlineData("LMAX-R55-extra")]
    [InlineData("NQ-R55")]
    public void Arbitrary_even_malformed_non_lmax_or_out_of_range_phases_are_rejected(string phase)
    {
        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(phase: phase, operatorApprovalPhrase: ApprovalForPhase(phase)));

        Assert.False(result.Passed);
        Assert.False(result.RetryPhaseReserved);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    [Fact]
    public void Unapproved_instruments_are_rejected()
    {
        var instruments = new[]
        {
            new LmaxReadOnlyRuntimeApprovedInstrument("XAUUSD", "9999", "8", "not_approved", false, null)
        };

        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(instruments: instruments));

        Assert.False(result.Passed);
        Assert.False(result.ApprovedInstrumentsExact);
        Assert.Contains(result.Issues, x => x.Code is "ApprovedInstrumentListMismatch" or "InstrumentNotApproved");
    }

    [Fact]
    public void UsdJpy_caveat_is_preserved_and_weakened_caveat_is_rejected()
    {
        var valid = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter()).Validate(ValidRequest());
        var weakened = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x)
            .ToList();
        var invalid = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(instruments: weakened));

        Assert.True(valid.UsdJpyCaveatPreserved);
        Assert.False(invalid.UsdJpyCaveatPreserved);
        Assert.Contains(invalid.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    [Fact]
    public void Existing_inert_dry_run_path_remains_available()
    {
        var harness = BuildHarness(R55ApprovalPhrase);
        var dryRun = new LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter().ValidateAsync(
            LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
                harness,
                new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
                LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
                "LMAX-R10"));

        Assert.True(dryRun.Passed);
        Assert.True(dryRun.DryRunOnly);
        Assert.False(dryRun.SafetySnapshot.ExternalRunExecuted);
    }

    [Fact]
    public void Composition_gates_remain_required()
    {
        var request = ValidRequest(r44Passed: false);

        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter()).Validate(request);

        Assert.False(result.Passed);
        Assert.False(result.R44BoundedRuntimeCompositionValid);
        Assert.Contains(result.Issues, x => x.Code == "R44BoundedRuntimeCompositionRegression");
    }

    [Fact]
    public void Bounded_executor_and_runtime_delegate_approvals_remain_required()
    {
        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(boundedExecutorApproved: false, runtimeDelegateBindingApproved: false));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
    }

    [Fact]
    public void Validation_does_not_attempt_credential_tcp_tls_fix_or_marketdata_boundaries()
    {
        var adapter = new FakeActivationAdapter();

        var result = new LmaxBoundedReadOnlyActivationInvocationPath(adapter).Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.False(result.ExternalBoundaryAttempted);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.CredentialConfigBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public void Forbidden_order_trading_scheduler_polling_and_production_flags_are_blocked()
    {
        var flags = new LmaxReadOnlyRuntimeSafetyFlags(
            ProductionAccountRequested: true,
            AllowOrderSubmission: true,
            SchedulerEnabled: true,
            PollingEnabled: true,
            TradingMutationEnabled: true);

        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(safetyFlags: flags));

        Assert.False(result.Passed);
        Assert.True(result.ProductionAccountAllowed);
        Assert.True(result.SchedulerPollingRequired);
        Assert.True(result.OrderTradingPathReachable);
        Assert.Contains(result.Issues, x => x.Code == "ProductionAccountRisk");
    }

    [Fact]
    public void Credential_values_are_not_read_returned_printed_stored_or_serialized()
    {
        var result = new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())
            .Validate(ValidRequest(credentialValuesReturned: true));

        Assert.False(result.Passed);
        Assert.True(result.CredentialValuesReturned);
        Assert.Contains(result.Issues, x => x.Code == "CredentialValuesReturnedOrExposed");
    }

    [Fact]
    public void Invocation_path_is_not_reachable_from_api_worker_default_startup_or_appsettings()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("LmaxBoundedReadOnlyActivationInvocationPath", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxBoundedReadOnlyActivationInvocationPath", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxBoundedReadOnlyActivationInvocationPath", appsettings, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Invocation_path_is_not_a_launcher_hosted_service_scheduler_or_polling_loop()
    {
        var type = typeof(LmaxBoundedReadOnlyActivationInvocationPath);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxBoundedReadOnlyActivationInvocationPath.cs"));

        Assert.DoesNotContain("Main", methods);
        Assert.DoesNotContain("AddHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain(": BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("while (true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NewOrderSingle", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteOnce", source, StringComparison.Ordinal);
    }

    private static LmaxBoundedReadOnlyActivationInvocationPathRequest ValidRequest(
        string phase = "LMAX-R55",
        string? operatorApprovalPhrase = null,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true,
        bool r44Passed = true,
        bool credentialValuesReturned = false)
    {
        operatorApprovalPhrase ??= ApprovalForPhase(phase);
        var activationRequest = ValidActivationRequest(phase, operatorApprovalPhrase, instruments, safetyFlags, boundedExecutorApproved, runtimeDelegateBindingApproved);

        return new LmaxBoundedReadOnlyActivationInvocationPathRequest(
            activationRequest,
            LmaxTemporaryReadOnlyActivationExecutorOptions.ForApprovedSingleReadOnlyRetry(phase, "operator-approval-redacted"),
            BoundedResult(r44Passed),
            BoundaryOperationResult(),
            ProviderExecutionResult(),
            CredentialBindingResult(),
            operatorApprovalPhrase,
            operatorApprovalPhrase,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved,
            R42ConcreteAdapterGateValid: true,
            R50ConsolidationGateValid: true,
            R54RetryPhaseReservationRuleValid: true,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            ProductionAccountForbidden: true,
            CredentialValuesReturned: credentialValuesReturned);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        string phase,
        string approvalPhrase,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags,
        bool boundedExecutorApproved,
        bool runtimeDelegateBindingApproved)
    {
        var harness = BuildHarness(approvalPhrase, phase, instruments, safetyFlags);
        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
            LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            phase) with
            {
                BoundedExecutorApproved = boundedExecutorApproved,
                RuntimeDelegateBindingApproved = runtimeDelegateBindingApproved
            };
    }

    private static LmaxReadOnlyRuntimeActivationGateHarnessResult BuildHarness(
        string approvalPhrase,
        string phase = "LMAX-R55",
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));
        var approval = new LmaxReadOnlyRuntimeOperatorApproval(
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
            approvalPhrase,
            phase,
            "Demo/read-only",
            (instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments).Select(x => x.Symbol).ToList());
        var scope = harness.Scope with
        {
            Phase = phase,
            Instruments = instruments ?? harness.Scope.Instruments,
            SafetyFlags = safetyFlags ?? harness.Scope.SafetyFlags,
            OperatorApproval = approval
        };

        return harness with
        {
            Scope = scope,
            PreflightGate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope)
        };
    }

    private static string ApprovalForPhase(string phase)
        => phase == "LMAX-R55"
            ? R55ApprovalPhrase
            : $"I, Philippe, explicitly approve Phase {phase} for one temporary Demo read-only runtime market-data activation retry with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    private static LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedResult(bool passed)
        => new(
            passed,
            passed ? "ConcreteBoundedRuntimeActivationCompositionReadyNoExternalActivation" : "ConcreteBoundedRuntimeActivationCompositionRejected",
            NoApprovedR43BoundedExecutableRuntimeActivationComposition: !passed,
            BoundedExecutableRuntimeActivationCompositionExplicit: passed,
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

    private sealed class FakeActivationAdapter : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
    {
        public int Calls { get; private set; }
        public bool RealCredentialReadAttempted { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }

        public LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
            LmaxTemporaryReadOnlyRuntimeActivationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;

            return new LmaxTemporaryReadOnlyRuntimeActivationResult(
                request.Phase,
                request.CreatedAtUtc,
                request.AdapterMode,
                LmaxTemporaryReadOnlyRuntimeActivationOutcome.BoundedExecutableReadOnlyAccepted,
                HarnessOutputConsumed: true,
                HarnessPreflightPassed: true,
                ApprovedInstrumentsOnly: true,
                UsdJpyCaveatPreserved: true,
                DryRunOnly: false,
                FutureR10ApprovalRequired: false,
                "Fake invocation adapter accepted ExecuteOnce without external boundary.",
                [],
                request.HarnessResult.Scope.Instruments.Select(x => new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
                    x.Symbol,
                    x.SecurityId,
                    x.SecurityIdSource,
                    "Demo/read-only",
                    LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted,
                    "NotAttempted",
                    null,
                    request.CreatedAtUtc,
                    x.Caveat)).ToList(),
                LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork);
        }
    }
}
