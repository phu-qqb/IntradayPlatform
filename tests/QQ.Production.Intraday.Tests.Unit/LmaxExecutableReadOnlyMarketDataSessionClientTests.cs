using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxExecutableReadOnlyMarketDataSessionClientTests
{
    [Fact]
    public void Executable_session_client_can_be_constructed_with_fake_low_level_dependencies()
    {
        var socket = FakeSocket.Success();
        var fix = FakeFix.Success();
        var codec = FakeCodec.Success();

        _ = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, fix, codec);

        Assert.Equal(0, socket.TotalCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, codec.RequestCalls);
    }

    [Fact]
    public void Constructor_does_not_load_credentials_or_make_network_calls()
    {
        var socket = FakeSocket.Success();
        var fix = FakeFix.Success();
        var codec = FakeCodec.Success();

        _ = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, fix, codec);

        Assert.False(socket.RealNetworkOpened);
        Assert.False(fix.RealFixLogonAttempted);
        Assert.False(codec.RealMarketDataRequestSent);
        Assert.False(socket.RealCredentialLoadingExecuted);
    }

    [Fact]
    public void Successful_fake_tcp_tls_fix_marketdata_flow_returns_sanitized_statuses()
    {
        var socket = FakeSocket.Success();
        var fix = FakeFix.Success();
        var codec = FakeCodec.Success();
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, fix, codec);
        var scope = ValidScope();

        var tcp = client.OpenReadOnlyTcpBoundary(scope);
        var tls = client.OpenReadOnlyTlsBoundary(scope);
        var fixResult = client.OpenReadOnlyFixLogonBoundary(scope);
        var marketData = client.RequestReadOnlyMarketData(scope);
        var shutdown = client.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, tcp.Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, tls.Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, fixResult.Status);
        Assert.Equal("ReadOnlyMarketDataSucceededSanitized", marketData.SanitizedStatus);
        Assert.True(marketData.MarketDataRequestWriteAttempted);
        Assert.True(marketData.MarketDataRequestWriteSucceeded);
        Assert.True(marketData.MarketDataRequestResponseReadAttempted);
        Assert.True(marketData.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], codec.CapturedSymbols);
        Assert.All(marketData.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, x.MarketDataBoundary));
        Assert.True(shutdown);
        Assert.Equal(1, socket.ShutdownCalls);
    }

    [Theory]
    [InlineData("Tcp", "TcpBoundaryFailed")]
    [InlineData("Tls", "TlsBoundaryFailed")]
    [InlineData("Fix", "FixLogonBoundaryFailed")]
    public void Boundary_failure_returns_sanitized_boundary_failure(string failedBoundary, string expectedCategory)
    {
        var socket = FakeSocket.Success(failedBoundary);
        var fix = FakeFix.Success(failedBoundary);
        var codec = FakeCodec.Success();
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, fix, codec);
        var scope = ValidScope();

        var tcp = client.OpenReadOnlyTcpBoundary(scope);
        var tls = tcp.Succeeded ? client.OpenReadOnlyTlsBoundary(scope) : null;
        var fixResult = tls?.Succeeded == true ? client.OpenReadOnlyFixLogonBoundary(scope) : null;

        var observed = failedBoundary switch
        {
            "Tcp" => tcp,
            "Tls" => tls!,
            "Fix" => fixResult!,
            _ => throw new InvalidOperationException(failedBoundary)
        };
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, observed.Status);
        Assert.Equal(expectedCategory, observed.SanitizedErrorCategory);
        Assert.DoesNotContain("password", observed.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marketdata_failure_returns_sanitized_marketdata_failure()
    {
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(
            FakeSocket.Success(),
            FakeFix.Success(),
            FakeCodec.Success(failed: true));
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        client.OpenReadOnlyTlsBoundary(scope);
        client.OpenReadOnlyFixLogonBoundary(scope);
        var marketData = client.RequestReadOnlyMarketData(scope);

        Assert.Equal("ReadOnlyMarketDataFailedSanitized", marketData.SanitizedStatus);
        Assert.Equal("MarketDataBoundaryFailed", marketData.SanitizedErrorCategory);
        Assert.True(marketData.MarketDataRequestWriteAttempted);
        Assert.True(marketData.MarketDataRequestWriteSucceeded);
        Assert.True(marketData.MarketDataRequestResponseReadAttempted);
        Assert.True(marketData.MarketDataRequestReachedBoundedResponseClassification);
        Assert.All(marketData.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, x.MarketDataBoundary));
    }

    [Fact]
    public void Request_before_fix_logon_keeps_state_fields_false_and_does_not_invoke_codec()
    {
        var codec = FakeCodec.Success();
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(
            FakeSocket.Success(),
            FakeFix.Success(),
            codec);
        var scope = ValidScope();

        var marketData = client.RequestReadOnlyMarketData(scope);

        Assert.Equal("ReadOnlyMarketDataNotAttempted", marketData.SanitizedStatus);
        Assert.Equal("FixLogonBoundaryNotOpened", marketData.SanitizedErrorCategory);
        Assert.False(marketData.MarketDataRequestWriteAttempted);
        Assert.False(marketData.MarketDataRequestWriteSucceeded);
        Assert.False(marketData.MarketDataRequestResponseReadAttempted);
        Assert.False(marketData.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal(0, codec.RequestCalls);
    }

    [Fact]
    public void Instrument_reject_returns_sanitized_instrument_level_status()
    {
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(
            FakeSocket.Success(),
            FakeFix.Success(),
            FakeCodec.Success(instrumentReject: true));
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        client.OpenReadOnlyTlsBoundary(scope);
        client.OpenReadOnlyFixLogonBoundary(scope);
        var marketData = client.RequestReadOnlyMarketData(scope);

        Assert.Contains(marketData.InstrumentStatuses, x =>
            x.Symbol == "GBPUSD" &&
            x.MarketDataRequestRejectCount == 1 &&
            x.SanitizedErrorCategory == "MarketDataRequestRejected");
        Assert.DoesNotContain(marketData.InstrumentStatuses, x => x.SanitizedErrorMessage?.Contains("password", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Shutdown_called_exactly_once_after_partial_start()
    {
        var socket = FakeSocket.Success("Tls");
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, FakeFix.Success(), FakeCodec.Success());
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        var tls = client.OpenReadOnlyTlsBoundary(scope);
        var shutdown = client.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, tls.Status);
        Assert.True(shutdown);
        Assert.Equal(1, socket.ShutdownCalls);
    }

    [Fact]
    public void Approved_instruments_only_are_passed_to_fake_session_and_codec_and_usdjpy_caveat_preserved()
    {
        var codec = FakeCodec.Success();
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(FakeSocket.Success(), FakeFix.Success(), codec);
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        client.OpenReadOnlyTlsBoundary(scope);
        client.OpenReadOnlyFixLogonBoundary(scope);
        var marketData = client.RequestReadOnlyMarketData(scope);

        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], codec.CapturedSymbols);
        var usdJpy = Assert.Single(marketData.InstrumentStatuses, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
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
    public void Unsafe_request_fails_before_low_level_session_is_invoked(string condition)
    {
        var socket = FakeSocket.Success();
        var fix = FakeFix.Success();
        var codec = FakeCodec.Success();
        var client = new LmaxExecutableReadOnlyMarketDataSessionClient(socket, fix, codec);

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope(condition));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, socket.TotalCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, codec.RequestCalls);
    }

    [Fact]
    public void Public_readonly_interfaces_expose_no_order_trading_replay_or_mutation_methods()
    {
        foreach (var type in new[]
        {
            typeof(ILmaxReadOnlyMarketDataSessionClient),
            typeof(ILmaxReadOnlySocketSessionBoundary),
            typeof(ILmaxReadOnlyFixSessionBoundary),
            typeof(ILmaxReadOnlyMarketDataRequestCodec)
        })
        {
            var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();

            Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Mutation", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxExecutableReadOnlyMarketDataSessionClient.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlyMarketDataSessionClient", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlyMarketDataSessionClient", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => ValidHarness().Scope;

    private static LmaxReadOnlyRuntimeActivationGateHarnessResult ValidHarness(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxReadOnlyRuntimeShutdownRevertRecord? shutdownRevert = null)
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
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

    private sealed class FakeSocket : ILmaxReadOnlySocketSessionBoundary
    {
        private readonly string? failedBoundary;

        private FakeSocket(string? failedBoundary) => this.failedBoundary = failedBoundary;

        public int TcpCalls { get; private set; }
        public int TlsCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealNetworkOpened { get; private set; }
        public bool RealCredentialLoadingExecuted { get; private set; }
        public int TotalCalls => TcpCalls + TlsCalls;

        public static FakeSocket Success(string? failedBoundary = null) => new(failedBoundary);

        public LmaxReadOnlyBoundaryStepResult OpenTcpBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TcpCalls++;
            return Boundary("Tcp", "TcpBoundaryFailed");
        }

        public LmaxReadOnlyBoundaryStepResult OpenTlsBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TlsCalls++;
            return Boundary("Tls", "TlsBoundaryFailed");
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }

        private LmaxReadOnlyBoundaryStepResult Boundary(string boundary, string errorCategory)
            => failedBoundary == boundary
                ? new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, $"{boundary}FailedSanitized", errorCategory, $"Fake {boundary} boundary failure.")
                : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"{boundary}SucceededSanitized", null, null);
    }

    private sealed class FakeFix : ILmaxReadOnlyFixSessionBoundary
    {
        private readonly string? failedBoundary;

        private FakeFix(string? failedBoundary) => this.failedBoundary = failedBoundary;

        public int LogonCalls { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }

        public static FakeFix Success(string? failedBoundary = null) => new(failedBoundary);

        public LmaxReadOnlyBoundaryStepResult OpenFixLogonBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogonCalls++;
            return failedBoundary == "Fix"
                ? new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "FixFailedSanitized", "FixLogonBoundaryFailed", "Fake FIX boundary failure.")
                : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FixSucceededSanitized", null, null);
        }
    }

    private sealed class FakeCodec : ILmaxReadOnlyMarketDataRequestCodec
    {
        private readonly bool failed;
        private readonly bool instrumentReject;

        private FakeCodec(bool failed, bool instrumentReject)
        {
            this.failed = failed;
            this.instrumentReject = instrumentReject;
        }

        public int RequestCalls { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public static FakeCodec Success(bool failed = false, bool instrumentReject = false)
            => new(failed, instrumentReject);

        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            if (failed)
            {
                return WithAttemptedRequestState(new LmaxReadOnlyMarketDataSessionClientResult(
                    scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "MarketDataBoundaryFailed", "Fake market-data boundary failure.")).ToList(),
                    "ReadOnlyMarketDataFailedSanitized",
                    "MarketDataBoundaryFailed",
                    "Fake market-data boundary failure."));
            }

            if (instrumentReject)
            {
                var statuses = scope.Instruments.Select(x => x.Symbol == "GBPUSD"
                    ? Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "MarketDataRequestRejected", "Sanitized market-data reject.")
                    : Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)).ToList();

                return WithAttemptedRequestState(new LmaxReadOnlyMarketDataSessionClientResult(
                    statuses,
                    "ReadOnlyInstrumentRejectSanitized",
                    "MarketDataBoundaryFailed",
                    "One approved instrument returned a sanitized reject."));
            }

            return WithAttemptedRequestState(new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)).ToList(),
                "ReadOnlyMarketDataSucceededSanitized",
                null,
                null));
        }

        private static LmaxReadOnlyMarketDataSessionClientResult WithAttemptedRequestState(
            LmaxReadOnlyMarketDataSessionClientResult result)
            => result with
            {
                MarketDataRequestWriteAttempted = true,
                MarketDataRequestWriteSucceeded = true,
                MarketDataRequestResponseReadAttempted = true,
                MarketDataRequestReachedBoundedResponseClassification = true
            };

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
