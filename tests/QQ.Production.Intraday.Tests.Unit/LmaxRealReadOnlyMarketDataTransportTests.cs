using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyMarketDataTransportTests
{
    [Fact]
    public void Successful_test_double_readonly_flow_returns_sanitized_boundary_evidence()
    {
        var client = TestSessionClient.Success();
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.MarketDataBoundary);
        Assert.True(result.OutputSanitized);
        Assert.False(result.CredentialsLoaded);
        Assert.False(result.CredentialsPrinted);
        Assert.False(result.CredentialsStored);
        Assert.True(result.ShutdownRevertCompleted);
        Assert.Equal(1, client.TcpCalls);
        Assert.Equal(1, client.TlsCalls);
        Assert.Equal(1, client.FixCalls);
        Assert.Equal(1, client.MarketDataCalls);
        Assert.Equal(1, client.ShutdownCalls);
    }

    [Fact]
    public void Concrete_adapter_can_use_real_transport_class_with_session_client_test_double()
    {
        var client = TestSessionClient.Success();
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.True(result.Passed);
        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], client.CapturedSymbols);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotApplicableForInertValidation, x.BoundaryStatus));
    }

    [Fact]
    public void Approved_instruments_only_are_passed_to_session_client_and_usdjpy_caveat_is_preserved()
    {
        var client = TestSessionClient.Success();
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(ValidScope());

        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], client.CapturedSymbols);
        var usdJpy = Assert.Single(result.InstrumentStatuses, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
    }

    [Theory]
    [InlineData("Tcp", "TcpBoundaryFailed", "ReadOnlyTcpBoundaryFailedSanitized")]
    [InlineData("Tls", "TlsBoundaryFailed", "ReadOnlyTlsBoundaryFailedSanitized")]
    [InlineData("Fix", "FixLogonBoundaryFailed", "ReadOnlyFixLogonBoundaryFailedSanitized")]
    [InlineData("MarketData", "MarketDataBoundaryFailed", "ReadOnlyTransportMarketDataFailedSanitized")]
    public void Boundary_failure_returns_sanitized_failure_result(string failedBoundary, string expectedCategory, string expectedStatus)
    {
        var client = TestSessionClient.Success(failedBoundary);
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(ValidScope());

        Assert.Equal(expectedStatus, result.SanitizedStatus);
        Assert.Equal(expectedCategory, result.SanitizedErrorCategory);
        Assert.True(result.OutputSanitized);
        Assert.False(result.CredentialsLoaded);
        Assert.Equal(1, client.ShutdownCalls);
    }

    [Fact]
    public void Instrument_reject_returns_sanitized_instrument_level_status()
    {
        var client = TestSessionClient.Success(instrumentReject: true);
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, result.MarketDataBoundary);
        Assert.Contains(result.InstrumentStatuses, x =>
            x.Symbol == "GBPUSD" &&
            x.MarketDataRequestRejectCount == 1 &&
            x.SanitizedErrorCategory == "MarketDataRequestRejected");
        Assert.DoesNotContain(result.InstrumentStatuses, x => x.SanitizedErrorMessage?.Contains("password", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Shutdown_revert_called_after_partial_start_even_when_tls_fails()
    {
        var client = TestSessionClient.Success("Tls");
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, result.TlsBoundary);
        Assert.True(result.ShutdownRevertCompleted);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.Equal(0, client.FixCalls);
        Assert.Equal(0, client.MarketDataCalls);
    }

    [Theory]
    [InlineData("NonApprovedInstrument")]
    [InlineData("UsdJpyWithoutCaveat")]
    [InlineData("ProductionAccount")]
    [InlineData("OrdersEnabled")]
    [InlineData("LiveTradingEnabled")]
    [InlineData("SchedulerEnabled")]
    [InlineData("PollingEnabled")]
    [InlineData("ReplayEnabled")]
    [InlineData("ShadowReplayEnabled")]
    [InlineData("TradingMutationEnabled")]
    [InlineData("PersistentRuntimeEnablement")]
    [InlineData("DefaultGatewayRegistrationChange")]
    [InlineData("SanitizationDisabled")]
    [InlineData("MissingShutdownRevertPlan")]
    public void Unsafe_request_fails_before_session_client_is_invoked(string condition)
    {
        var client = TestSessionClient.Success();
        var transport = new LmaxRealReadOnlyMarketDataTransport(client);

        var result = transport.RunAsync(InvalidScope(condition));

        Assert.Equal("ReadOnlyTransportBlockedBeforeSessionClientUse", result.SanitizedStatus);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.TotalBoundaryCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.True(result.OutputSanitized);
    }

    [Fact]
    public void Constructor_does_not_load_credentials_or_touch_session_client()
    {
        var client = TestSessionClient.Success();

        _ = new LmaxRealReadOnlyMarketDataTransport(client);

        Assert.Equal(0, client.TotalBoundaryCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.False(client.CredentialsLoaded);
    }

    [Fact]
    public void Readonly_session_client_abstraction_exposes_no_order_trading_replay_or_mutation_methods()
    {
        var methodNames = typeof(ILmaxReadOnlyMarketDataSessionClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();

        Assert.Contains("OpenReadOnlyTcpBoundary", methodNames);
        Assert.Contains("OpenReadOnlyTlsBoundary", methodNames);
        Assert.Contains("OpenReadOnlyFixLogonBoundary", methodNames);
        Assert.Contains("RequestReadOnlyMarketData", methodNames);
        Assert.Contains("ShutdownRevert", methodNames);
        Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Mutation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_api_worker_or_default_config_wiring_was_added()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxRealReadOnlyMarketDataTransport.cs"));

        Assert.DoesNotContain("LmaxRealReadOnlyMarketDataTransport", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyMarketDataTransport", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionPassword", source, StringComparison.Ordinal);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidRequest()
        => LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            ValidHarness(),
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero));

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => ValidHarness().Scope;

    private static LmaxReadOnlyRuntimeActivationGateHarnessResult ValidHarness(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxReadOnlyRuntimeShutdownRevertRecord? shutdownRevert = null)
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 21, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags,
            shutdownRevert));

    private static LmaxTemporaryReadOnlyRuntimeActivationScope InvalidScope(string condition)
        => condition switch
        {
            "NonApprovedInstrument" => ValidHarness(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "prior_workflow_closed", false, null)]).Scope,
            "UsdJpyWithoutCaveat" => ValidHarness(instruments: LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x).ToList()).Scope,
            "ProductionAccount" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true)).Scope,
            "OrdersEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)).Scope,
            "LiveTradingEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowLiveTrading: true)).Scope,
            "SchedulerEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(SchedulerEnabled: true)).Scope,
            "PollingEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PollingEnabled: true)).Scope,
            "ReplayEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ReplayEnabled: true)).Scope,
            "ShadowReplayEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ShadowReplayEnabled: true)).Scope,
            "TradingMutationEnabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(TradingMutationEnabled: true)).Scope,
            "PersistentRuntimeEnablement" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PersistentRuntimeEnablementRequested: true)).Scope,
            "DefaultGatewayRegistrationChange" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(DefaultGatewayRegistrationChangeRequested: true)).Scope,
            "SanitizationDisabled" => ValidHarness(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(OutputSanitizationEnabled: false)).Scope,
            "MissingShutdownRevertPlan" => ValidHarness(shutdownRevert: new LmaxReadOnlyRuntimeShutdownRevertRecord(false, true, true, "artifacts/readiness/lmax-runtime-enablement/missing.json")).Scope,
            _ => throw new InvalidOperationException(condition)
        };

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

    private sealed class TestSessionClient : ILmaxReadOnlyMarketDataSessionClient
    {
        private readonly string? failedBoundary;
        private readonly bool instrumentReject;

        private TestSessionClient(string? failedBoundary = null, bool instrumentReject = false)
        {
            this.failedBoundary = failedBoundary;
            this.instrumentReject = instrumentReject;
        }

        public int TcpCalls { get; private set; }
        public int TlsCalls { get; private set; }
        public int FixCalls { get; private set; }
        public int MarketDataCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool CredentialsLoaded { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];
        public int TotalBoundaryCalls => TcpCalls + TlsCalls + FixCalls + MarketDataCalls;

        public static TestSessionClient Success(string? failedBoundary = null, bool instrumentReject = false)
            => new(failedBoundary, instrumentReject);

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTcpBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TcpCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();
            return Boundary("Tcp");
        }

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTlsBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TlsCalls++;
            return Boundary("Tls");
        }

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyFixLogonBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FixCalls++;
            return Boundary("Fix");
        }

        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarketDataCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            if (failedBoundary == "MarketData")
            {
                return new LmaxReadOnlyMarketDataSessionClientResult(
                    scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "MarketDataBoundaryFailed", "Fake market-data boundary failure.")).ToList(),
                    "ReadOnlyMarketDataFailedSanitized",
                    "MarketDataBoundaryFailed",
                    "Fake market-data boundary failure.");
            }

            if (instrumentReject)
            {
                var statuses = scope.Instruments.Select(x => x.Symbol == "GBPUSD"
                    ? Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "MarketDataRequestRejected", "Sanitized market-data reject.")
                    : Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)).ToList();
                return new LmaxReadOnlyMarketDataSessionClientResult(statuses, "ReadOnlyInstrumentRejectSanitized", "MarketDataBoundaryFailed", "One approved instrument returned a sanitized reject.");
            }

            return new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)).ToList(),
                "ReadOnlyMarketDataSucceededSanitized",
                null,
                null);
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }

        private LmaxReadOnlyBoundaryStepResult Boundary(string boundary)
        {
            var category = boundary == "Fix" ? "FixLogonBoundaryFailed" : $"{boundary}BoundaryFailed";
            return failedBoundary == boundary
                ? new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, $"{boundary}FailedSanitized", category, $"Fake {boundary} boundary failure.")
                : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"{boundary}SucceededSanitized", null, null);
        }

        private static LmaxTemporaryReadOnlyInstrumentMarketDataStatus Status(
            LmaxReadOnlyRuntimeApprovedInstrument instrument,
            LmaxTemporaryReadOnlySessionBoundaryStatus status,
            string? errorCategory = null,
            string? errorMessage = null)
            => new(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                status,
                status == LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded ? 1 : 0,
                errorCategory == "MarketDataRequestRejected" ? 1 : 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                status == LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded ? "ReadOnlyInstrumentStatusSucceededSanitized" : "ReadOnlyInstrumentStatusFailedSanitized",
                errorCategory,
                errorMessage,
                instrument.Caveat);
    }
}
