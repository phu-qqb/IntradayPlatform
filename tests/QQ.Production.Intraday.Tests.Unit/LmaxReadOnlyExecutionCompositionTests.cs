using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExecutionCompositionTests
{
    [Fact]
    public void Final_composition_can_be_built_with_local_delegates_without_external_side_effects()
    {
        var counters = new Counters();
        var cores = ApprovedCores(counters);

        var result = new LmaxReadOnlyExecutionCompositionRoot(cores).Compose(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.True(result.Passed);
        Assert.True(result.OperationToCoreMappingExists);
        Assert.True(result.BoundedExecutorCompositionPassed);
        Assert.False(result.ContainsFakeOrTestOnlyCore);
        Assert.NotNull(result.OperationBindings);
        Assert.NotNull(result.ProviderClients);
        Assert.Equal(0, counters.SocketCalls);
        Assert.Equal(0, counters.TlsCalls);
        Assert.Equal(0, counters.FixCalls);
        Assert.Equal(0, counters.MarketDataCalls);
        Assert.Equal(0, counters.CredentialCalls);
    }

    [Fact]
    public void Composed_clients_execute_only_supplied_local_delegates_when_called_by_tests()
    {
        var counters = new Counters();
        var result = new LmaxReadOnlyExecutionCompositionRoot(ApprovedCores(counters)).Compose(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var clients = result.ProviderClients!;
        var scope = ValidScope();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.SocketClient.OpenTcp(SocketOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.TlsClient.OpenTls(TlsOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.FixClient.OpenSessionLogon(FixOptions(), scope, ValidAccessRecord()).Status);
        Assert.Equal("LocalMarketDataSanitized", clients.MarketDataClient.RequestReadOnlyStatus(MarketDataOptions(), scope).SanitizedStatus);
        Assert.Equal("Local[redacted]Sanitized", clients.CredentialConfigClient.AccessDemoReadOnlyConfig(CredentialOptions(), scope, ValidPolicy()).SanitizedStatus);

        Assert.Equal(1, counters.SocketCalls);
        Assert.Equal(1, counters.TlsCalls);
        Assert.Equal(1, counters.FixCalls);
        Assert.Equal(1, counters.MarketDataCalls);
        Assert.Equal(1, counters.CredentialCalls);
    }

    [Fact]
    public void Fake_test_only_core_is_rejected_in_final_composition_validation()
    {
        var counters = new Counters();
        var cores = ApprovedCores(counters) with
        {
            SocketDescriptor = new(
                "FakeSocketCore",
                "TcpBoundary",
                ApprovedForDemoReadOnly: true,
                TestOnly: true,
                ProductionAccountCapable: false,
                OrderTradingReplayCapable: false,
                RequiresApiWorkerStartup: false,
                RequiresLiveLauncher: false,
                RequiresHostedService: false,
                SanitizedEvidenceSupported: true)
        };

        var result = new LmaxReadOnlyExecutionCompositionRoot(cores).Compose(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.False(result.Passed);
        Assert.True(result.ContainsFakeOrTestOnlyCore);
        Assert.Contains(result.Issues, x => x.Contains("FakeTestOnlyCoreRejected", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ApiWorker")]
    [InlineData("LiveLauncher")]
    [InlineData("HostedService")]
    public void Broad_runtime_required_cores_are_rejected(string rejectedCapability)
    {
        var counters = new Counters();
        var descriptor = new LmaxReadOnlyExecutionCoreDescriptor(
            $"LmaxRealSocketCore{rejectedCapability}",
            "TcpBoundary",
            ApprovedForDemoReadOnly: true,
            TestOnly: false,
            ProductionAccountCapable: false,
            OrderTradingReplayCapable: false,
            RequiresApiWorkerStartup: rejectedCapability == "ApiWorker",
            RequiresLiveLauncher: rejectedCapability == "LiveLauncher",
            RequiresHostedService: rejectedCapability == "HostedService",
            SanitizedEvidenceSupported: true);
        var cores = ApprovedCores(counters) with { SocketDescriptor = descriptor };

        var result = new LmaxReadOnlyExecutionCompositionRoot(cores).Compose(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.False(result.Passed);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void Production_account_missing_readonly_non_approved_instrument_and_missing_usdjpy_caveat_are_rejected()
    {
        var counters = new Counters();

        Assert.False(LmaxReadOnlyExecutionCompositionValidator.Validate(
            ApprovedCores(counters),
            ValidScope(new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true))).Passed);
        Assert.False(LmaxReadOnlyExecutionCompositionValidator.Validate(
            ApprovedCores(counters),
            ValidScope(instruments: [new("XAUUSD", "9999", "8", "unapproved", false, null)])).Passed);
        Assert.False(LmaxReadOnlyExecutionCompositionValidator.Validate(
            ApprovedCores(counters),
            ValidScope(instruments: [new("USDJPY", "4004", "8", "validated_readiness_archive_with_caveat", false, null)])).Passed);
    }

    [Fact]
    public void Public_composition_surfaces_expose_no_order_trading_replay_or_launcher_methods()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExecutionCompositionRoot),
            typeof(LmaxReadOnlyExecutionCoreBindingSet),
            typeof(LmaxReadOnlyExecutionCompositionResult),
            typeof(LmaxReadOnlyExecutionCompositionValidator)
        };

        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(x => x.Name)
                .ToList();

            Assert.DoesNotContain(methods, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("ShadowReplay", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(methods, x => x.Contains("BackgroundWorker", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Sanitized_outputs_do_not_store_raw_fix_or_credentials()
    {
        var counters = new Counters();
        var cores = ApprovedCores(counters, includeSensitiveStrings: true);
        var result = new LmaxReadOnlyExecutionCompositionRoot(cores).Compose(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var fix = result.ProviderClients!.FixClient.OpenSessionLogon(FixOptions(), ValidScope(), ValidAccessRecord());
        var credential = result.ProviderClients.CredentialConfigClient.AccessDemoReadOnlyConfig(CredentialOptions(), ValidScope(), ValidPolicy());
        var combined = string.Join(" ", new[] { fix.SanitizedStatus, fix.SanitizedErrorCategory, fix.SanitizedErrorMessage, credential.SanitizedStatus, credential.SanitizedErrorCategory, credential.SanitizedErrorMessage }.Where(x => x is not null));

        Assert.DoesNotContain("password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static LmaxReadOnlyExecutionCoreBindingSet ApprovedCores(
        Counters counters,
        bool includeSensitiveStrings = false)
        => LmaxReadOnlyExecutionCoreBindingSet.CreateApproved(
            (_, _, _) =>
            {
                counters.SocketCalls++;
                return Success("Socket");
            },
            (_, _, _) =>
            {
                counters.TlsCalls++;
                return Success("Tls");
            },
            (_, _, _, _) =>
            {
                counters.FixCalls++;
                return includeSensitiveStrings
                    ? new LmaxRealReadOnlyDependencyResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "RawFix 35=D password secret 554=value", "RawFix", "secret 554=value")
                    : Success("Fix");
            },
            (_, scope, _) =>
            {
                counters.MarketDataCalls++;
                return new(
                    scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                        instrument.Symbol,
                        instrument.SecurityId,
                        instrument.SecurityIdSource,
                        LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                        1,
                        0,
                        0,
                        0,
                        "LocalInstrumentMarketDataSanitized",
                        null,
                        null,
                        instrument.Caveat)).ToList(),
                    "LocalMarketDataSanitized",
                    null,
                    null);
            },
            (_, _, _, _) =>
            {
                counters.CredentialCalls++;
                return new(
                    AccessAllowed: true,
                    RealSecretMaterialLoaded: false,
                    SensitiveMaterialReturned: false,
                    SensitiveMaterialPrinted: false,
                    SensitiveMaterialStored: false,
                    includeSensitiveStrings ? "password secret 554=value" : "LocalCredentialSanitized",
                    includeSensitiveStrings ? "secret" : null,
                    includeSensitiveStrings ? "password" : null);
            });

    private static LmaxRealReadOnlyDependencyResult Success(string name)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"Local{name}Sanitized", null, null);

    private static LmaxReadOnlySocketConnectionOptions SocketOptions()
        => new("Demo/read-only", "lmax-demo-readonly", 443, TimeSpan.FromSeconds(5), true, true);

    private static LmaxReadOnlyTlsConnectionOptions TlsOptions()
        => new("Demo/read-only", "lmax-demo-readonly", "lmax-demo-readonly-host", TimeSpan.FromSeconds(5), true, "SystemDefaultValidation", true);

    private static LmaxReadOnlyFixSessionOptions FixOptions()
        => new("Demo/read-only", "readonly-sender", "lmax-demo-target", 30, TimeSpan.FromSeconds(5), true, LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes, true);

    private static LmaxReadOnlyMarketDataRequestOptions MarketDataOptions()
        => new("Demo/read-only", true, "ReadOnlyMarketDataRequest", "SnapshotOrStatus", TimeSpan.FromSeconds(5), LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes, true);

    private static LmaxReadOnlyCredentialConfigOptions CredentialOptions()
        => new("Demo/read-only", true, "local-approved-demo-readonly-config", true);

    private static LmaxReadOnlyCredentialAccessPolicy ValidPolicy()
        => new(FutureApprovedRuntimeAttemptRequired: true, RealSecretMaterialAllowedNow: true, RedactSensitiveFields: true, Environment: "Demo/read-only");

    private static LmaxReadOnlyCredentialSanitizationRecord ValidAccessRecord()
        => new(true, false, false, false, false, "LocalCredentialAccessSanitized");

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope(
        LmaxReadOnlyRuntimeSafetyFlags? flags = null,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null)
        => new(
            "LMAX-R38",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            flags ?? new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 19, 00, 00, TimeSpan.Zero),
                "R38 local-only test approval marker",
                "LMAX-R38",
                "Demo/read-only",
                (instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments).Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r38-test"),
            MaxRuntimeSeconds: 30,
            "artifacts/readiness/lmax-runtime-enablement");

    private sealed class Counters
    {
        public int SocketCalls { get; set; }
        public int TlsCalls { get; set; }
        public int FixCalls { get; set; }
        public int MarketDataCalls { get; set; }
        public int CredentialCalls { get; set; }
    }
}
