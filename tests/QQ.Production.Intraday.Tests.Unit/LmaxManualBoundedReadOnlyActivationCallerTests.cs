using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxManualBoundedReadOnlyActivationCallerTests
{
    private const string R59ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R59 for one temporary Demo read-only runtime market-data activation retry after the R58 approved operational caller for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    [Fact]
    public void R57_operational_caller_blocker_is_cleared()
    {
        var adapter = new FakeActivationAdapter();
        var caller = new LmaxManualBoundedReadOnlyActivationCaller(new LmaxBoundedReadOnlyActivationInvocationPath(adapter));

        var result = caller.Validate(ValidCallerRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR57OperationalCallerForBoundedInvocationPath);
        Assert.True(result.ApprovedOperationalCallerProvable);
        Assert.True(result.ManualOnly);
        Assert.True(result.SingleAttemptOnly);
        Assert.True(result.CallsBoundedInvocationPath);
        Assert.True(result.InvocationPathCallsExecuteOnce);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public void Call_once_calls_invocation_path_which_calls_execute_once()
    {
        var adapter = new FakeActivationAdapter();
        var caller = new LmaxManualBoundedReadOnlyActivationCaller(new LmaxBoundedReadOnlyActivationInvocationPath(adapter));

        var result = caller.CallOnce(ValidCallerRequest());

        Assert.True(result.Validation.Passed);
        Assert.NotNull(result.InvocationResult);
        Assert.True(result.InvocationResult.Validation.Passed);
        Assert.NotNull(result.InvocationResult.ExecutorResult);
        Assert.True(result.InvocationResult.ExecutorResult.ExecutionStarted);
        Assert.Equal(1, result.InvocationResult.ExecutorResult.AttemptsExecuted);
        Assert.Equal(1, adapter.Calls);
        Assert.False(adapter.RealCredentialReadAttempted);
        Assert.False(adapter.RealTcpConnectionAttempted);
        Assert.False(adapter.RealTlsHandshakeAttempted);
        Assert.False(adapter.RealFixLogonAttempted);
        Assert.False(adapter.RealMarketDataRequestSent);
    }

    [Fact]
    public void Caller_permits_exactly_one_call_per_instance()
    {
        var adapter = new FakeActivationAdapter();
        var caller = new LmaxManualBoundedReadOnlyActivationCaller(new LmaxBoundedReadOnlyActivationInvocationPath(adapter));

        var first = caller.CallOnce(ValidCallerRequest());
        var second = caller.CallOnce(ValidCallerRequest());

        Assert.True(first.Validation.Passed);
        Assert.False(second.Validation.Passed);
        Assert.Contains(second.Validation.Issues, x => x.Code == "OperationalCallerAlreadyConsumed");
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public void Caller_is_manual_only()
    {
        var result = Validate(ValidCallerRequest(manualOperatorInvocationRequested: false, manualRunbookReviewed: false));

        Assert.False(result.Passed);
        Assert.False(result.ManualOnly);
        Assert.Contains(result.Issues, x => x.Code == "ManualOperatorInvocationMissing");
    }

    [Fact]
    public void Exact_per_phase_operator_approval_is_required()
    {
        var result = Validate(ValidCallerRequest(operatorApprovalPhrase: "I approve a different phase."));

        Assert.False(result.Passed);
        Assert.False(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.Contains(result.Issues, x => x.Code == "BoundedInvocationPathRegression");
        Assert.Contains(result.Issues, x => x.Code == "ExactPerPhaseOperatorApprovalMissing");
    }

    [Theory]
    [InlineData("LMAX-R58")]
    [InlineData("LMAX-R60")]
    [InlineData("LMAX-R101")]
    [InlineData("LMAX-R059")]
    [InlineData("LMAX-R59-extra")]
    [InlineData("NQ-R59")]
    public void Arbitrary_even_malformed_non_lmax_or_out_of_range_phases_are_rejected(string phase)
    {
        var result = Validate(ValidCallerRequest(phase: phase, operatorApprovalPhrase: ApprovalForPhase(phase)));

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

        var result = Validate(ValidCallerRequest(instruments: instruments));

        Assert.False(result.Passed);
        Assert.False(result.ApprovedInstrumentsExact);
        Assert.Contains(result.Issues, x => x.Code is "ApprovedInstrumentListMismatch" or "InstrumentNotApproved");
    }

    [Fact]
    public void UsdJpy_caveat_is_preserved_and_weakened_caveat_is_rejected()
    {
        var valid = Validate(ValidCallerRequest());
        var weakened = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x)
            .ToList();
        var invalid = Validate(ValidCallerRequest(instruments: weakened));

        Assert.True(valid.UsdJpyCaveatPreserved);
        Assert.False(invalid.UsdJpyCaveatPreserved);
        Assert.Contains(invalid.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    [Fact]
    public void Composition_gates_remain_required()
    {
        var result = Validate(ValidCallerRequest(r44Passed: false));

        Assert.False(result.Passed);
        Assert.False(result.R42ThroughR56GateChainValid);
        Assert.Contains(result.Issues, x => x.Code == "CompositionChainRegression");
    }

    [Fact]
    public void Validation_does_not_attempt_credential_tcp_tls_fix_or_marketdata_boundaries()
    {
        var adapter = new FakeActivationAdapter();
        var caller = new LmaxManualBoundedReadOnlyActivationCaller(new LmaxBoundedReadOnlyActivationInvocationPath(adapter));

        var result = caller.Validate(ValidCallerRequest());

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

        var result = Validate(ValidCallerRequest(safetyFlags: flags));

        Assert.False(result.Passed);
        Assert.True(result.ProductionAccountAllowed);
        Assert.True(result.SchedulerPollingRequired);
        Assert.True(result.OrderTradingPathReachable);
        Assert.Contains(result.Issues, x => x.Code == "ProductionAccountRisk");
    }

    [Fact]
    public void Credential_values_are_not_read_returned_printed_stored_or_serialized()
    {
        var result = Validate(ValidCallerRequest(credentialValuesReturned: true));

        Assert.False(result.Passed);
        Assert.True(result.CredentialValuesReturned);
        Assert.Contains(result.Issues, x => x.Code == "CredentialValuesReturnedOrExposed");
    }

    [Fact]
    public void Caller_is_not_reachable_from_api_worker_default_startup_or_appsettings()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("LmaxManualBoundedReadOnlyActivationCaller", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxManualBoundedReadOnlyActivationCaller", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxManualBoundedReadOnlyActivationCaller", appsettings, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Caller_is_not_a_launcher_hosted_service_scheduler_or_polling_loop()
    {
        var type = typeof(LmaxManualBoundedReadOnlyActivationCaller);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxManualBoundedReadOnlyActivationCaller.cs"));

        Assert.DoesNotContain("Main", methods);
        Assert.DoesNotContain("AddHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain(": BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("while (true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NewOrderSingle", source, StringComparison.Ordinal);
        Assert.Contains("InvokeOnce", source, StringComparison.Ordinal);
    }

    private static LmaxManualBoundedReadOnlyActivationCallerValidationResult Validate(
        LmaxManualBoundedReadOnlyActivationCallerRequest request)
        => new LmaxManualBoundedReadOnlyActivationCaller(new LmaxBoundedReadOnlyActivationInvocationPath(new FakeActivationAdapter())).Validate(request);

    private static LmaxManualBoundedReadOnlyActivationCallerRequest ValidCallerRequest(
        string phase = "LMAX-R59",
        string? operatorApprovalPhrase = null,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        bool manualOperatorInvocationRequested = true,
        bool manualRunbookReviewed = true,
        bool r44Passed = true,
        bool credentialValuesReturned = false)
    {
        operatorApprovalPhrase ??= ApprovalForPhase(phase);
        return new LmaxManualBoundedReadOnlyActivationCallerRequest(
            ValidInvocationRequest(phase, operatorApprovalPhrase, instruments, safetyFlags, r44Passed),
            manualOperatorInvocationRequested,
            manualRunbookReviewed,
            SingleAttemptOnly: true,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            ProductionAccountForbidden: true,
            CredentialValuesReturned: credentialValuesReturned);
    }

    private static LmaxBoundedReadOnlyActivationInvocationPathRequest ValidInvocationRequest(
        string phase,
        string approvalPhrase,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags,
        bool r44Passed)
    {
        var activationRequest = ValidActivationRequest(phase, approvalPhrase, instruments, safetyFlags);
        return new LmaxBoundedReadOnlyActivationInvocationPathRequest(
            activationRequest,
            LmaxTemporaryReadOnlyActivationExecutorOptions.ForApprovedSingleReadOnlyRetry(phase, "operator-approval-redacted"),
            BoundedResult(r44Passed),
            BoundaryOperationResult(),
            ProviderExecutionResult(),
            CredentialBindingResult(),
            approvalPhrase,
            approvalPhrase,
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
            ProductionAccountForbidden: true);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        string phase,
        string approvalPhrase,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags)
    {
        var harness = BuildHarness(phase, approvalPhrase, instruments, safetyFlags);
        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 12, 40, 00, TimeSpan.Zero),
            LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            phase) with
            {
                BoundedExecutorApproved = true,
                RuntimeDelegateBindingApproved = true
            };
    }

    private static LmaxReadOnlyRuntimeActivationGateHarnessResult BuildHarness(
        string phase,
        string approvalPhrase,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 40, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));
        var scopedInstruments = instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;
        var approval = new LmaxReadOnlyRuntimeOperatorApproval(
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 40, 00, TimeSpan.Zero),
            approvalPhrase,
            phase,
            "Demo/read-only",
            scopedInstruments.Select(x => x.Symbol).ToList());
        var scope = harness.Scope with
        {
            Phase = phase,
            Instruments = scopedInstruments,
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
        => phase == "LMAX-R59"
            ? R59ApprovalPhrase
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
                "Fake operational caller adapter accepted without external boundary.",
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
