using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyFinalProviderCompletenessTests
{
    [Fact]
    public void All_required_provider_interfaces_have_non_test_implementations()
    {
        Assert.True(typeof(ILmaxRealReadOnlySocketBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlySocketBoundaryProvider)));
        Assert.True(typeof(ILmaxRealReadOnlyTlsBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyTlsBoundaryProvider)));
        Assert.True(typeof(ILmaxRealReadOnlyFixFrameBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyFixFrameBoundaryProvider)));
        Assert.True(typeof(ILmaxRealReadOnlyMarketDataFrameBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyMarketDataFrameBoundaryProvider)));
        Assert.True(typeof(ILmaxRealReadOnlyCredentialConfigBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyCredentialConfigBoundaryProvider)));
    }

    [Fact]
    public void Provider_factory_can_assemble_complete_provider_set_with_fake_clients_without_external_execution()
    {
        var factory = new LmaxRealReadOnlyDependencyProviderFactory(
            new LmaxRealReadOnlySocketBoundaryProvider(
                LmaxReadOnlySocketConnectionOptions.DemoReadOnlyDisabled("lmax-demo-readonly", 443),
                new FakeSocketClient()),
            new LmaxRealReadOnlyTlsBoundaryProvider(
                LmaxReadOnlyTlsConnectionOptions.DemoReadOnlyDisabled("lmax-demo-readonly", "lmax-demo-readonly-host"),
                new FakeTlsClient()),
            new LmaxRealReadOnlyFixFrameBoundaryProvider(
                LmaxReadOnlyFixSessionOptions.DemoReadOnlyDisabled("readonly-sender", "lmax-demo-target"),
                new FakeFixClient()),
            new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
                LmaxReadOnlyMarketDataRequestOptions.DemoReadOnlyDisabled(),
                new FakeMarketDataClient()),
            new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
                new LmaxReadOnlyCredentialConfigOptions("Demo/read-only", DemoReadOnly: true, "local-approved-demo-readonly-config"),
                new FakeCredentialClient()));

        var providerSet = factory.Create();
        var dependencySet = providerSet.CreateLowLevelDependencySet();

        Assert.NotNull(providerSet.SocketProvider);
        Assert.NotNull(providerSet.TlsProvider);
        Assert.NotNull(providerSet.FixProvider);
        Assert.NotNull(providerSet.MarketDataProvider);
        Assert.NotNull(providerSet.CredentialConfigProvider);
        Assert.NotNull(dependencySet.SocketDependency);
        Assert.NotNull(dependencySet.FixSessionDependency);
        Assert.NotNull(dependencySet.MarketDataDependency);
        Assert.NotNull(dependencySet.SecretDependency);
    }

    [Fact]
    public void Provider_public_surfaces_expose_no_order_trading_replay_or_hosted_service_methods()
    {
        var providerTypes = new[]
        {
            typeof(LmaxRealReadOnlySocketBoundaryProvider),
            typeof(LmaxRealReadOnlyTlsBoundaryProvider),
            typeof(LmaxRealReadOnlyFixFrameBoundaryProvider),
            typeof(LmaxRealReadOnlyMarketDataFrameBoundaryProvider),
            typeof(LmaxRealReadOnlyCredentialConfigBoundaryProvider)
        };

        foreach (var providerType in providerTypes)
        {
            var methodNames = providerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(x => x.Name)
                .ToList();

            Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("TradingMutation", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methodNames, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Complete_provider_sweep_adds_no_api_worker_default_config_launcher_or_hosted_service_wiring()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
        foreach (var token in new[]
                 {
                     "LmaxRealReadOnlySocketBoundaryProvider",
                     "LmaxRealReadOnlyTlsBoundaryProvider",
                     "LmaxRealReadOnlyFixFrameBoundaryProvider",
                     "LmaxRealReadOnlyMarketDataFrameBoundaryProvider",
                     "LmaxRealReadOnlyCredentialConfigBoundaryProvider",
                     "LmaxRealReadOnlyDependencyProviderFactory",
                     "phase-lmax-r30"
                 })
        {
            Assert.DoesNotContain(token, apiProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(token, workerProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(token, appsettings, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
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

    private sealed class FakeSocketClient : ILmaxReadOnlySocketConnectionClient
    {
        public LmaxRealReadOnlyDependencyResult OpenTcp(
            LmaxReadOnlySocketConnectionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FakeSocketSanitized", null, null);

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeTlsClient : ILmaxReadOnlyTlsHandshakeClient
    {
        public LmaxRealReadOnlyDependencyResult OpenTls(
            LmaxReadOnlyTlsConnectionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FakeTlsSanitized", null, null);

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeFixClient : ILmaxReadOnlyFixFrameClient
    {
        public LmaxRealReadOnlyDependencyResult OpenSessionLogon(
            LmaxReadOnlyFixSessionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialSanitizationRecord accessRecord,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "FakeFixSanitized", null, null);

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeMarketDataClient : ILmaxReadOnlyMarketDataFrameClient
    {
        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
            LmaxReadOnlyMarketDataRequestOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new([], "FakeMarketDataSanitized", null, null);

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeCredentialClient : ILmaxReadOnlyCredentialConfigClient
    {
        public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
            LmaxReadOnlyCredentialConfigOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialAccessPolicy policy,
            CancellationToken cancellationToken = default)
            => new(
                AccessAllowed: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "FakeConfigSanitized",
                null,
                null);
    }
}
