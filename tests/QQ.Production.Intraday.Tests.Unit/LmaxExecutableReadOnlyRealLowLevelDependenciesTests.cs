using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxExecutableReadOnlyRealLowLevelDependenciesTests
{
    [Fact]
    public void Real_dependency_set_can_be_constructed_without_opening_sockets_or_loading_real_secrets()
    {
        var tcp = FakeTcpConnector.Success();
        var tls = FakeTlsAuthenticator.Success();
        var fix = FakeFixDriver.Success();
        var marketData = FakeMarketDataDriver.Success();
        var secrets = FakeSecretProvider.Success();

        _ = new LmaxRealReadOnlyLowLevelDependencyFactory(tcp, tls, fix, marketData, secrets).Create();

        Assert.Equal(0, tcp.ConnectCalls);
        Assert.Equal(0, tls.AuthenticateCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, marketData.ReadCalls);
        Assert.Equal(0, secrets.AccessCalls);
        Assert.False(tcp.RealSocketOpened);
        Assert.False(tcp.RealTcpConnectionAttempted);
        Assert.False(tls.RealTlsHandshakeAttempted);
        Assert.False(fix.RealFixLogonAttempted);
        Assert.False(marketData.RealMarketDataRequestSent);
        Assert.False(secrets.RealSecretLoaded);
    }

    [Fact]
    public void Fake_success_path_exercises_real_dependency_set_without_real_external_action()
    {
        var tcp = FakeTcpConnector.Success();
        var tls = FakeTlsAuthenticator.Success();
        var fix = FakeFixDriver.Success();
        var marketData = FakeMarketDataDriver.Success();
        var secrets = FakeSecretProvider.Success();
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(tcp, tls, fix, marketData, secrets).Create();
        var client = set.CreateSessionClient();
        var scope = ValidScope();

        var tcpResult = client.OpenReadOnlyTcpBoundary(scope);
        var tlsResult = client.OpenReadOnlyTlsBoundary(scope);
        var fixResult = client.OpenReadOnlyFixLogonBoundary(scope);
        var marketDataResult = client.RequestReadOnlyMarketData(scope);
        var shutdown = client.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, tcpResult.Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, tlsResult.Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, fixResult.Status);
        Assert.Equal("ReadOnlyMarketDataSucceededSanitized", marketDataResult.SanitizedStatus);
        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], marketData.CapturedSymbols);
        Assert.All(marketDataResult.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, x.MarketDataBoundary));
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, Assert.Single(marketDataResult.InstrumentStatuses, x => x.Symbol == "USDJPY").Caveat);
        Assert.True(shutdown);
        Assert.Equal(1, tcp.ShutdownCalls);
        Assert.Equal(0, secrets.AccessCalls);
    }

    [Theory]
    [InlineData("Tcp", "TcpBoundaryFailed")]
    [InlineData("Tls", "TlsBoundaryFailed")]
    [InlineData("Fix", "FixLogonBoundaryFailed")]
    public void Fake_boundary_failure_returns_sanitized_failure(string failedBoundary, string expectedCategory)
    {
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(
            FakeTcpConnector.Success(failedBoundary),
            FakeTlsAuthenticator.Success(failedBoundary),
            FakeFixDriver.Success(failedBoundary),
            FakeMarketDataDriver.Success(),
            FakeSecretProvider.Success()).Create();
        var client = set.CreateSessionClient();
        var scope = ValidScope();

        var tcp = client.OpenReadOnlyTcpBoundary(scope);
        var tls = tcp.Succeeded ? client.OpenReadOnlyTlsBoundary(scope) : null;
        var fix = tls?.Succeeded == true ? client.OpenReadOnlyFixLogonBoundary(scope) : null;
        var observed = failedBoundary switch
        {
            "Tcp" => tcp,
            "Tls" => tls!,
            "Fix" => fix!,
            _ => throw new InvalidOperationException(failedBoundary)
        };

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, observed.Status);
        Assert.Equal(expectedCategory, observed.SanitizedErrorCategory);
        Assert.DoesNotContain("password", observed.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", observed.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fake_marketdata_failure_returns_sanitized_failure()
    {
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(
            FakeTcpConnector.Success(),
            FakeTlsAuthenticator.Success(),
            FakeFixDriver.Success(),
            FakeMarketDataDriver.Success(failed: true),
            FakeSecretProvider.Success()).Create();
        var client = set.CreateSessionClient();
        var scope = ValidScope();

        client.OpenReadOnlyTcpBoundary(scope);
        client.OpenReadOnlyTlsBoundary(scope);
        client.OpenReadOnlyFixLogonBoundary(scope);
        var result = client.RequestReadOnlyMarketData(scope);

        Assert.Equal("ReadOnlyMarketDataFailedSanitized", result.SanitizedStatus);
        Assert.Equal("MarketDataBoundaryFailed", result.SanitizedErrorCategory);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, x.MarketDataBoundary));
        Assert.DoesNotContain("secret", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_approved_instrument_is_rejected_before_request_construction()
    {
        var tcp = FakeTcpConnector.Success();
        var tls = FakeTlsAuthenticator.Success();
        var fix = FakeFixDriver.Success();
        var marketData = FakeMarketDataDriver.Success();
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(tcp, tls, fix, marketData, FakeSecretProvider.Success()).Create();
        var client = set.CreateSessionClient();

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope("NonApprovedInstrument"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, tcp.ConnectCalls);
        Assert.Equal(0, tls.AuthenticateCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, marketData.ReadCalls);
    }

    [Fact]
    public void UsdJpy_caveat_is_preserved_and_missing_caveat_is_rejected_before_low_level_use()
    {
        var tcp = FakeTcpConnector.Success();
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(
            tcp,
            FakeTlsAuthenticator.Success(),
            FakeFixDriver.Success(),
            FakeMarketDataDriver.Success(),
            FakeSecretProvider.Success()).Create();
        var client = set.CreateSessionClient();

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope("UsdJpyWithoutCaveat"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, tcp.ConnectCalls);
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
    public void Unsafe_scope_fails_before_real_low_level_execution(string condition)
    {
        var tcp = FakeTcpConnector.Success();
        var tls = FakeTlsAuthenticator.Success();
        var fix = FakeFixDriver.Success();
        var marketData = FakeMarketDataDriver.Success();
        var secrets = FakeSecretProvider.Success();
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(tcp, tls, fix, marketData, secrets).Create();
        var client = set.CreateSessionClient();

        var result = client.OpenReadOnlyTcpBoundary(InvalidScope(condition));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, tcp.ConnectCalls);
        Assert.Equal(0, tls.AuthenticateCalls);
        Assert.Equal(0, fix.LogonCalls);
        Assert.Equal(0, marketData.ReadCalls);
        Assert.Equal(0, secrets.AccessCalls);
    }

    [Fact]
    public void Secret_provider_can_be_fake_driven_for_future_approval_policy_without_returning_sensitive_material()
    {
        var secrets = FakeSecretProvider.Success();
        var dependency = new LmaxRealReadOnlyCredentialDependency(secrets);
        var policy = new LmaxReadOnlyCredentialAccessPolicy(RealSecretMaterialAllowedNow: true);

        var result = dependency.ValidatePolicy(ValidScope(), policy);

        Assert.True(result.AccessPolicyAccepted);
        Assert.False(result.RealSecretMaterialLoaded);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.False(result.SensitiveMaterialPrinted);
        Assert.False(result.SensitiveMaterialStored);
        Assert.Equal(1, secrets.AccessCalls);
        Assert.False(secrets.RealSecretLoaded);
    }

    [Fact]
    public void Sensitive_fields_are_redacted_from_results()
    {
        var set = new LmaxRealReadOnlyLowLevelDependencyFactory(
            FakeTcpConnector.Success("Tcp", includeSensitiveMessage: true),
            FakeTlsAuthenticator.Success(),
            FakeFixDriver.Success(),
            FakeMarketDataDriver.Success(),
            FakeSecretProvider.Success()).Create();
        var client = set.CreateSessionClient();

        var result = client.OpenReadOnlyTcpBoundary(ValidScope());

        Assert.DoesNotContain("password", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_real_dependency_interfaces_expose_no_order_trading_replay_or_mutation_methods()
    {
        foreach (var type in new[]
        {
            typeof(ILmaxRealReadOnlyTcpConnector),
            typeof(ILmaxRealReadOnlyTlsAuthenticator),
            typeof(ILmaxRealReadOnlyFixSessionDriver),
            typeof(ILmaxRealReadOnlyMarketDataDriver),
            typeof(ILmaxRealReadOnlySecretProvider)
        })
        {
            var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();

            Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("OrderStatus", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("ExecutionReport", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("ShadowReplay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradingMutation", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxExecutableReadOnlyRealLowLevelDependencies.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyLowLevelDependencyFactory", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyLowLevelDependencyFactory", workerProgram, StringComparison.Ordinal);
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

    private sealed class FakeTcpConnector : ILmaxRealReadOnlyTcpConnector
    {
        private readonly string? failedBoundary;
        private readonly bool includeSensitiveMessage;

        private FakeTcpConnector(string? failedBoundary, bool includeSensitiveMessage)
        {
            this.failedBoundary = failedBoundary;
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int ConnectCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }

        public static FakeTcpConnector Success(string? failedBoundary = null, bool includeSensitiveMessage = false)
            => new(failedBoundary, includeSensitiveMessage);

        public LmaxRealReadOnlyDependencyResult Connect(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectCalls++;
            return failedBoundary == "Tcp"
                ? new LmaxRealReadOnlyDependencyResult(
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    "TcpFailedSanitized",
                    "TcpBoundaryFailed",
                    includeSensitiveMessage ? "Fake TCP password=demo 554=demo-secret failure." : "Fake TCP failure.")
                : new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "TcpSucceededSanitized");
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }
    }

    private sealed class FakeTlsAuthenticator : ILmaxRealReadOnlyTlsAuthenticator
    {
        private readonly string? failedBoundary;

        private FakeTlsAuthenticator(string? failedBoundary) => this.failedBoundary = failedBoundary;

        public int AuthenticateCalls { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }

        public static FakeTlsAuthenticator Success(string? failedBoundary = null) => new(failedBoundary);

        public LmaxRealReadOnlyDependencyResult Authenticate(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuthenticateCalls++;
            return failedBoundary == "Tls"
                ? new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "TlsFailedSanitized", "TlsBoundaryFailed", "Fake TLS failure.")
                : new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "TlsSucceededSanitized");
        }
    }

    private sealed class FakeFixDriver : ILmaxRealReadOnlyFixSessionDriver
    {
        private readonly string? failedBoundary;

        private FakeFixDriver(string? failedBoundary) => this.failedBoundary = failedBoundary;

        public int LogonCalls { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }

        public static FakeFixDriver Success(string? failedBoundary = null) => new(failedBoundary);

        public LmaxRealReadOnlyDependencyResult OpenLogon(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialSanitizationRecord secretRecord,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogonCalls++;
            Assert.True(secretRecord.AccessPolicyAccepted);
            Assert.False(secretRecord.SensitiveMaterialReturned);
            return failedBoundary == "Fix"
                ? new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "FixFailedSanitized", "FixLogonBoundaryFailed", "Fake FIX password=demo 554=demo-secret failure.")
                : new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FixSucceededSanitized");
        }
    }

    private sealed class FakeMarketDataDriver : ILmaxRealReadOnlyMarketDataDriver
    {
        private readonly bool failed;

        private FakeMarketDataDriver(bool failed) => this.failed = failed;

        public int ReadCalls { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public static FakeMarketDataDriver Success(bool failed = false) => new(failed);

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

    private sealed class FakeSecretProvider : ILmaxRealReadOnlySecretProvider
    {
        public int AccessCalls { get; private set; }
        public bool RealSecretLoaded { get; private set; }

        public static FakeSecretProvider Success() => new();

        public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnly(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialAccessPolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessCalls++;
            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "FakeSecretProviderAcceptedNoSensitiveMaterial");
        }
    }
}
