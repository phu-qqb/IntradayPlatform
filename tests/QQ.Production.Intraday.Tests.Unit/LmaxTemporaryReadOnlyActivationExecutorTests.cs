using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxTemporaryReadOnlyActivationExecutorTests
{
    [Fact]
    public void Executor_can_be_constructed_without_socket_tcp_tls_fix_marketdata_credentials_or_worker_startup()
    {
        var adapter = new FakeActivationAdapter();

        _ = new LmaxTemporaryReadOnlyActivationExecutor(ValidOptions(), adapter);

        Assert.Equal(0, adapter.Calls);
        Assert.False(adapter.RealSocketOpened);
        Assert.False(adapter.RealTcpConnectionAttempted);
        Assert.False(adapter.RealTlsHandshakeAttempted);
        Assert.False(adapter.RealFixLogonAttempted);
        Assert.False(adapter.RealFixMessageSent);
        Assert.False(adapter.RealMarketDataRequestSent);
        Assert.False(adapter.RealCredentialLoadingExecuted);
        Assert.False(adapter.ApiWorkerStarted);
    }

    [Fact]
    public void Valid_future_execution_request_passes_local_validation_with_fake_stack()
    {
        var adapter = new FakeActivationAdapter();
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(
            ValidOptions(futureExternalExecutionApproved: true),
            adapter);

        var result = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());

        Assert.True(result.ValidationPassed);
        Assert.True(result.ExecutionStarted);
        Assert.Equal(1, result.AttemptsExecuted);
        Assert.Equal("BoundedExecutorCompletedSingleAttemptWithSanitizedEvidence", result.SanitizedStatus);
        Assert.Equal("RequiresLiveLauncherCreationFixedByBoundedExecutor", result.ConcreteBlockerFixed);
        Assert.True(result.NoLiveLauncherCreated);
        Assert.True(result.NoHostedServiceCreated);
        Assert.True(result.NoApiWorkerStarted);
        Assert.True(result.NoDefaultConfigChanged);
        Assert.Equal(1, adapter.Calls);
        Assert.False(adapter.RealSocketOpened);
        Assert.False(adapter.RealTcpConnectionAttempted);
        Assert.False(adapter.RealTlsHandshakeAttempted);
        Assert.False(adapter.RealFixLogonAttempted);
        Assert.False(adapter.RealFixMessageSent);
        Assert.False(adapter.RealMarketDataRequestSent);
        Assert.False(adapter.RealCredentialLoadingExecuted);
    }

    [Fact]
    public void Future_execution_without_explicit_approval_validates_but_does_not_call_adapter()
    {
        var adapter = new FakeActivationAdapter();
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(ValidOptions(), adapter);

        var result = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());

        Assert.True(result.ValidationPassed);
        Assert.False(result.ExecutionStarted);
        Assert.Equal("FutureApprovalRequired", result.SanitizedErrorCategory);
        Assert.Equal(0, result.AttemptsExecuted);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public void Executor_permits_exactly_one_attempt()
    {
        var adapter = new FakeActivationAdapter();
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(
            ValidOptions(futureExternalExecutionApproved: true),
            adapter);

        var first = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());
        var second = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());

        Assert.True(first.ValidationPassed);
        Assert.Equal("SafetyConstraintFailed", second.SanitizedErrorCategory);
        Assert.Contains(second.Issues, x => x.Code == "AttemptAlreadyConsumed");
        Assert.Equal(1, adapter.Calls);
    }

    [Theory]
    [InlineData(2, 0, false, false, "InvalidMaxAttemptCount")]
    [InlineData(1, 1, false, false, "RetryNotAllowed")]
    [InlineData(1, 0, true, false, "BatchModeNotAllowed")]
    [InlineData(1, 0, false, true, "LoopModeNotAllowed")]
    public void Invalid_attempt_retry_batch_or_loop_options_are_rejected_before_adapter_use(
        int maxAttemptCount,
        int retryCount,
        bool batchMode,
        bool loopMode,
        string expectedIssue)
    {
        var adapter = new FakeActivationAdapter();
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(
            ValidOptions(
                maxAttemptCount: maxAttemptCount,
                retryCount: retryCount,
                batchMode: batchMode,
                loopMode: loopMode,
                futureExternalExecutionApproved: true),
            adapter);

        var result = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());

        Assert.False(result.ExecutionStarted);
        Assert.Contains(result.Issues, x => x.Code == expectedIssue);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public void Production_account_is_rejected_before_adapter_use()
    {
        var result = ExecuteWithScope(ValidScope() with
        {
            SafetyFlags = ValidScope().SafetyFlags with { ProductionAccountRequested = true }
        });

        Assert.Contains(result.Issues, x => x.Code == "ProductionAccountRequested");
    }

    [Fact]
    public void Non_approved_instrument_is_rejected_before_adapter_use()
    {
        var result = ExecuteWithScope(ValidScope() with
        {
            Instruments =
            [
                new LmaxReadOnlyRuntimeApprovedInstrument("XAUUSD", "9999", "8", "not_approved", false, null)
            ]
        });

        Assert.Contains(result.Issues, x => x.Code is "ApprovedInstrumentListMismatch" or "NonApprovedInstrument");
    }

    [Fact]
    public void UsdJpy_without_caveat_is_rejected_before_adapter_use()
    {
        var result = ExecuteWithScope(ValidScope() with
        {
            Instruments = ValidScope().Instruments.Select(x =>
                string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase)
                    ? x with { Caveat = null }
                    : x).ToList()
        });

        Assert.Contains(result.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    [Theory]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.AllowOrderSubmission), "AllowOrderSubmission")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.AllowLiveTrading), "AllowLiveTrading")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.IsTradingEnabled), "IsTradingEnabled")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.SchedulerEnabled), "SchedulerEnabled")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.PollingEnabled), "PollingEnabled")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.ReplayEnabled), "ReplayEnabled")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.ShadowReplayEnabled), "ShadowReplayEnabled")]
    [InlineData(nameof(LmaxReadOnlyRuntimeSafetyFlags.TradingMutationEnabled), "TradingMutationEnabled")]
    public void Orders_trading_scheduler_polling_replay_shadow_replay_or_mutation_are_rejected(
        string flagName,
        string expectedIssue)
    {
        var flags = SetSafetyFlag(ValidScope().SafetyFlags, flagName);
        var result = ExecuteWithScope(ValidScope() with { SafetyFlags = flags });

        Assert.Contains(result.Issues, x => x.Code == expectedIssue);
    }

    [Fact]
    public void Missing_shutdown_revert_plan_is_rejected_before_adapter_use()
    {
        var result = ExecuteWithScope(ValidScope() with { ShutdownRevert = null });

        Assert.Contains(result.Issues, x => x.Code == "ShutdownRevertPlanMissing");
    }

    [Fact]
    public void Missing_sanitization_is_rejected_before_adapter_use()
    {
        var result = ExecuteWithScope(ValidScope() with
        {
            SafetyFlags = ValidScope().SafetyFlags with { OutputSanitizationEnabled = false }
        });

        Assert.Contains(result.Issues, x => x.Code == "OutputSanitizationRequired");
    }

    [Fact]
    public void Output_contains_no_secrets()
    {
        var adapter = new FakeActivationAdapter(includeSensitiveMessage: true);
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(
            ValidOptions(futureExternalExecutionApproved: true),
            adapter);

        var result = executor.ExecuteOnce(ValidRequest(), SanitizedApprovalMarker());
        var text = string.Join(" ", result.SanitizedStatus, result.SanitizedErrorCategory, result.SanitizedErrorMessage);

        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Executor_is_not_a_live_launcher_and_has_no_main_or_hosted_service_behavior()
    {
        var type = typeof(LmaxTemporaryReadOnlyActivationExecutor);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("Main", methods);
        Assert.DoesNotContain(methods, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, x => x.Contains("Scheduler", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, x => x.Contains("Polling", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ExecuteOnce", methods);
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxTemporaryReadOnlyActivationExecutor.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("static async Task Main", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static void Main", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("while (true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxTemporaryReadOnlyActivationExecutor", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxTemporaryReadOnlyActivationExecutor", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxTemporaryReadOnlyActivationExecutor", appsettings, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxTemporaryReadOnlyActivationExecutorResult ExecuteWithScope(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var adapter = new FakeActivationAdapter();
        var executor = new LmaxTemporaryReadOnlyActivationExecutor(
            ValidOptions(futureExternalExecutionApproved: true),
            adapter);

        var request = ValidRequest(scope);
        var result = executor.ExecuteOnce(request, SanitizedApprovalMarker());
        Assert.False(result.ExecutionStarted);
        Assert.Equal(0, adapter.Calls);
        return result;
    }

    private static LmaxTemporaryReadOnlyActivationExecutorOptions ValidOptions(
        string phaseLabel = "LMAX-R33",
        string approvalPhraseMarker = "R33-approval-redacted",
        string environmentLabel = "Demo/read-only",
        IReadOnlyList<string>? approvedInstruments = null,
        int maxAttemptCount = 1,
        int retryCount = 0,
        TimeSpan? timeout = null,
        bool shutdownRevertRequired = true,
        bool sanitizationRequired = true,
        bool noPersistence = true,
        bool batchMode = false,
        bool loopMode = false,
        bool futureExternalExecutionApproved = false)
        => new(
            phaseLabel,
            approvalPhraseMarker,
            environmentLabel,
            approvedInstruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList(),
            maxAttemptCount,
            retryCount,
            timeout ?? TimeSpan.FromSeconds(10),
            shutdownRevertRequired,
            sanitizationRequired,
            noPersistence,
            batchMode,
            loopMode,
            futureExternalExecutionApproved);

    private static string SanitizedApprovalMarker() => "R33 operator approval marker redacted";

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidRequest()
        => ValidRequest(ValidScope());

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidRequest(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate));
        var scopedHarness = harness with
        {
            Scope = scope,
            PreflightGate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope),
            ForbiddenActionValidation = new LmaxReadOnlyRuntimeForbiddenActionValidation(
                OrdersSubmitted: false,
                OrderPathEnabled: scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.OrderGatewayRegistered,
                SchedulerStarted: scope.SafetyFlags.SchedulerEnabled,
                PollingStarted: scope.SafetyFlags.PollingEnabled,
                ReplayExecuted: scope.SafetyFlags.ReplayEnabled,
                ShadowReplaySubmitted: scope.SafetyFlags.ShadowReplayEnabled,
                TradingStateMutated: scope.SafetyFlags.TradingMutationEnabled,
                ProductionAccountUsed: scope.SafetyFlags.ProductionAccountRequested),
            NonMutationValidation = new LmaxReadOnlyRuntimeNonMutationValidation(
                TradingStateMutated: scope.SafetyFlags.TradingMutationEnabled,
                PostEndpointInvoked: false,
                RuntimePoweredUp: false,
                CredentialsLoaded: false,
                CredentialsPrinted: false,
                CredentialsStored: false),
            RailIsolationValidation = new LmaxReadOnlyRuntimeRailIsolationValidation(
                ValidatedRailsModified: false,
                Phase7ArchiveModified: false,
                UsdJpyT1T7ArtifactsModified: false,
                NonApprovedInstrumentTouched: scope.Instruments.Any(x => LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(x.Symbol) is null))
        };

        return new LmaxTemporaryReadOnlyRuntimeActivationRequest(
            "LMAX-R33",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            scopedHarness,
            LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
            "LMAX-R33",
            "artifacts/readiness/lmax-runtime-enablement");
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate))
            .Scope;

    private static LmaxReadOnlyRuntimeSafetyFlags SetSafetyFlag(
        LmaxReadOnlyRuntimeSafetyFlags flags,
        string flagName)
    {
        var property = typeof(LmaxReadOnlyRuntimeSafetyFlags).GetProperty(flagName)
            ?? throw new InvalidOperationException($"Unknown safety flag {flagName}.");
        var values = typeof(LmaxReadOnlyRuntimeSafetyFlags)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => (bool)x.GetValue(flags)!);
        values[flagName] = true;

        return new LmaxReadOnlyRuntimeSafetyFlags(
            ProductionAccountRequested: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.ProductionAccountRequested)],
            AllowOrderSubmission: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.AllowOrderSubmission)],
            AllowLiveTrading: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.AllowLiveTrading)],
            IsTradingEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.IsTradingEnabled)],
            SchedulerEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.SchedulerEnabled)],
            PollingEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.PollingEnabled)],
            ReplayEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.ReplayEnabled)],
            ShadowReplayEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.ShadowReplayEnabled)],
            TradingMutationEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.TradingMutationEnabled)],
            OrderGatewayRegistered: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.OrderGatewayRegistered)],
            TradingGatewayRegistered: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.TradingGatewayRegistered)],
            PersistentRuntimeEnablementRequested: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.PersistentRuntimeEnablementRequested)],
            DefaultGatewayRegistrationChangeRequested: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.DefaultGatewayRegistrationChangeRequested)],
            OutputSanitizationEnabled: values[nameof(LmaxReadOnlyRuntimeSafetyFlags.OutputSanitizationEnabled)]);
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

    private sealed class FakeActivationAdapter : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
    {
        private readonly bool includeSensitiveMessage;

        public FakeActivationAdapter(bool includeSensitiveMessage = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int Calls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }
        public bool RealFixMessageSent { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }
        public bool RealCredentialLoadingExecuted { get; private set; }
        public bool ApiWorkerStarted { get; private set; }

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
                LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted,
                HarnessOutputConsumed: true,
                HarnessPreflightPassed: true,
                ApprovedInstrumentsOnly: true,
                UsdJpyCaveatPreserved: true,
                DryRunOnly: false,
                FutureR10ApprovalRequired: false,
                includeSensitiveMessage ? "fake password=demo secret=demo credential=demo 554=demo sanitized" : "Fake bounded executor adapter accepted sanitized single attempt.",
                [],
                request.HarnessResult.Scope.Instruments.Select(x => new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
                    x.Symbol,
                    x.SecurityId,
                    x.SecurityIdSource,
                    "Demo/read-only",
                    LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotApplicableForInertValidation,
                    "FakeBoundedExecutorInstrumentStatusSanitized",
                    null,
                    request.CreatedAtUtc,
                    x.Caveat)).ToList(),
                new LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot(
                    ExternalRunExecuted: false,
                    SnapshotExecuted: false,
                    ReplayExecuted: false,
                    PostEndpointInvoked: false,
                    RealSocketOpened: false,
                    TcpConnectionAttempted: false,
                    TlsHandshakeAttempted: false,
                    FixLogonAttempted: false,
                    MarketDataRequestSent: false,
                    OrderSubmissionExecuted: false,
                    TradingStateMutated: false,
                    SchedulerStarted: false,
                    PollingStarted: false,
                    ShadowReplaySubmitted: false,
                    ApiWorkerStarted: false,
                    RuntimePoweredUp: false,
                    RuntimeEnablementExecuted: false,
                    RuntimeEnablementPersisted: false,
                    DefaultGatewayRegistrationChanged: false,
                    CredentialsLoaded: false,
                    CredentialsPrinted: false,
                    CredentialsStored: false,
                    OutputSanitized: true));
        }
    }
}
