using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxExecutableReadOnlyLowLevelSessionStackTests
{
    [Fact]
    public void Low_level_stack_can_be_constructed_without_real_credentials_or_network_calls()
    {
        var socket = FakeSocketTransport.Success();
        var fix = FakeFixBoundary.Success();
        var codec = FakeMarketDataCodec.Success();
        var credentials = new LmaxExecutableReadOnlyCredentialBoundary();

        _ = new LmaxExecutableReadOnlySessionStackFactory(socket, fix, codec, credentials).CreateSessionClient();

        Assert.Equal(0, socket.TotalCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, codec.ReadCalls);
        Assert.False(socket.RealSocketOpened);
        Assert.False(socket.RealTcpConnectionAttempted);
        Assert.False(socket.RealTlsHandshakeAttempted);
        Assert.False(fix.RealFixLogonAttempted);
        Assert.False(codec.RealMarketDataRequestSent);
    }

    [Fact]
    public void Successful_fake_tcp_tls_fix_marketdata_flow_preserves_approved_instruments_and_caveat()
    {
        var socket = FakeSocketTransport.Success();
        var fix = FakeFixBoundary.Success();
        var codec = FakeMarketDataCodec.Success();
        var client = Client(socket, fix, codec);
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
        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], codec.CapturedSymbols);
        Assert.All(marketData.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, x.MarketDataBoundary));
        var usdJpy = Assert.Single(marketData.InstrumentStatuses, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
        Assert.True(shutdown);
        Assert.Equal(1, socket.ShutdownCalls);
    }

    [Theory]
    [InlineData("Tcp", "TcpBoundaryFailed")]
    [InlineData("Tls", "TlsBoundaryFailed")]
    [InlineData("Fix", "FixLogonBoundaryFailed")]
    public void Fake_boundary_failure_returns_sanitized_boundary_status(string failedBoundary, string expectedCategory)
    {
        var socket = FakeSocketTransport.Success(failedBoundary);
        var fix = FakeFixBoundary.Success(failedBoundary);
        var client = Client(socket, fix, FakeMarketDataCodec.Success());
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
        Assert.DoesNotContain("554=", observed.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fake_marketdata_failure_returns_sanitized_marketdata_failure()
    {
        var client = Client(FakeSocketTransport.Success(), FakeFixBoundary.Success(), FakeMarketDataCodec.Success(failed: true));
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        client.OpenReadOnlyTlsBoundary(scope);
        client.OpenReadOnlyFixLogonBoundary(scope);
        var marketData = client.RequestReadOnlyMarketData(scope);

        Assert.Equal("ReadOnlyMarketDataFailedSanitized", marketData.SanitizedStatus);
        Assert.Equal("MarketDataBoundaryFailed", marketData.SanitizedErrorCategory);
        Assert.All(marketData.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, x.MarketDataBoundary));
        Assert.DoesNotContain("secret", marketData.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_approved_instrument_is_rejected_before_request_construction()
    {
        var socket = FakeSocketTransport.Success();
        var fix = FakeFixBoundary.Success();
        var codec = FakeMarketDataCodec.Success();
        var client = Client(socket, fix, codec);

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope("NonApprovedInstrument"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, socket.TotalCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, codec.ReadCalls);
    }

    [Fact]
    public void UsdJpy_without_caveat_is_rejected_before_low_level_use()
    {
        var socket = FakeSocketTransport.Success();
        var client = Client(socket, FakeFixBoundary.Success(), FakeMarketDataCodec.Success());

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope("UsdJpyWithoutCaveat"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, socket.TotalCalls);
    }

    [Theory]
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
    public void Unsafe_scope_fails_before_real_low_level_use(string condition)
    {
        var socket = FakeSocketTransport.Success();
        var fix = FakeFixBoundary.Success();
        var codec = FakeMarketDataCodec.Success();
        var client = Client(socket, fix, codec);

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope(condition));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, socket.TotalCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, codec.ReadCalls);
    }

    [Fact]
    public void Sensitive_fields_are_redacted_from_boundary_and_marketdata_results()
    {
        var client = Client(
            FakeSocketTransport.Success("Tcp", includeSensitiveMessage: true),
            FakeFixBoundary.Success(),
            FakeMarketDataCodec.Success());

        var tcp = client.OpenReadOnlyTcpBoundary(ValidScope());

        Assert.DoesNotContain("password", tcp.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", tcp.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", tcp.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_low_level_interfaces_expose_no_order_trading_replay_or_mutation_methods()
    {
        foreach (var type in new[]
        {
            typeof(ILmaxReadOnlyCredentialBoundary),
            typeof(ILmaxReadOnlySocketBoundaryTransport),
            typeof(ILmaxReadOnlyFixFrameBoundary),
            typeof(ILmaxReadOnlyMarketDataFrameCodec),
            typeof(ILmaxReadOnlySocketSessionBoundary),
            typeof(ILmaxReadOnlyFixSessionBoundary),
            typeof(ILmaxReadOnlyMarketDataRequestCodec)
        })
        {
            var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();

            Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Execution", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("ShadowReplay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradingMutation", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxExecutableReadOnlyLowLevelSessionStack.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlyLowLevelSessionStack", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlySessionStackFactory", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlyLowLevelSessionStack", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxExecutableReadOnlySessionStackFactory", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxExecutableReadOnlyMarketDataSessionClient Client(
        FakeSocketTransport socket,
        FakeFixBoundary fix,
        FakeMarketDataCodec codec)
        => new LmaxExecutableReadOnlySessionStackFactory(
            socket,
            fix,
            codec,
            new LmaxExecutableReadOnlyCredentialBoundary()).CreateSessionClient();

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
            "NonApprovedInstrument" => ValidHarness(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "not_approved", false, null)]).Scope,
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

    private sealed class FakeSocketTransport : ILmaxReadOnlySocketBoundaryTransport
    {
        private readonly string? failedBoundary;
        private readonly bool includeSensitiveMessage;

        private FakeSocketTransport(string? failedBoundary, bool includeSensitiveMessage)
        {
            this.failedBoundary = failedBoundary;
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int TcpCalls { get; private set; }
        public int TlsCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public int TotalCalls => TcpCalls + TlsCalls;

        public static FakeSocketTransport Success(string? failedBoundary = null, bool includeSensitiveMessage = false)
            => new(failedBoundary, includeSensitiveMessage);

        public LmaxReadOnlyBoundaryStepResult OpenTcp(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TcpCalls++;
            return Boundary("Tcp", "TcpBoundaryFailed");
        }

        public LmaxReadOnlyBoundaryStepResult OpenTls(
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
                ? new LmaxReadOnlyBoundaryStepResult(
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    $"{boundary}FailedSanitized",
                    errorCategory,
                    includeSensitiveMessage ? $"Fake {boundary} failure password=demo 554=demo-secret." : $"Fake {boundary} boundary failure.")
                : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"{boundary}SucceededSanitized", null, null);
    }

    private sealed class FakeFixBoundary : ILmaxReadOnlyFixFrameBoundary
    {
        private readonly string? failedBoundary;

        private FakeFixBoundary(string? failedBoundary) => this.failedBoundary = failedBoundary;

        public int LogonCalls { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }

        public static FakeFixBoundary Success(string? failedBoundary = null) => new(failedBoundary);

        public LmaxReadOnlyBoundaryStepResult OpenLogon(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialSanitizationRecord credentialRecord,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogonCalls++;
            Assert.True(credentialRecord.AccessPolicyAccepted);
            Assert.False(credentialRecord.RealSecretMaterialLoaded);
            return failedBoundary == "Fix"
                ? new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "FixFailedSanitized", "FixLogonBoundaryFailed", "Fake FIX logon password=demo 554=demo-secret.")
                : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FixSucceededSanitized", null, null);
        }
    }

    private sealed class FakeMarketDataCodec : ILmaxReadOnlyMarketDataFrameCodec
    {
        private readonly bool failed;

        private FakeMarketDataCodec(bool failed) => this.failed = failed;

        public int ReadCalls { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public static FakeMarketDataCodec Success(bool failed = false) => new(failed);

        public LmaxReadOnlyMarketDataSessionClientResult ReadMarketData(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            if (failed)
            {
                return new LmaxReadOnlyMarketDataSessionClientResult(
                    scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "MarketDataBoundaryFailed", "Fake market-data secret=demo failure.")).ToList(),
                    "ReadOnlyMarketDataFailedSanitized",
                    "MarketDataBoundaryFailed",
                    "Fake market-data secret=demo failure.");
            }

            return new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(x => Status(x, LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)).ToList(),
                "ReadOnlyMarketDataSucceededSanitized",
                null,
                null);
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
                errorCategory == "MarketDataBoundaryFailed" ? 1 : 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                status == LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded ? "ReadOnlyInstrumentStatusSucceededSanitized" : "ReadOnlyInstrumentStatusFailedSanitized",
                errorCategory,
                errorMessage,
                instrument.Caveat);
    }
}
