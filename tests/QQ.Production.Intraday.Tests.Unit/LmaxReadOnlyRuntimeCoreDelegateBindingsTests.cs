using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeCoreDelegateBindingsTests
{
    [Fact]
    public void Final_delegate_binding_set_can_be_built_with_local_delegates_without_external_side_effects()
    {
        var counters = new Counters();
        var bindings = ApprovedBindings(counters);

        var result = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(bindings).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.True(result.Passed);
        Assert.True(result.RuntimeDelegateBindingPassed);
        Assert.True(result.OperationToDelegateMappingExists);
        Assert.True(result.BoundedExecutorCompositionPassed);
        Assert.NotNull(result.CoreBindingSet);
        Assert.NotNull(result.CompositionResult);
        Assert.Equal(0, counters.SocketCalls);
        Assert.Equal(0, counters.TlsCalls);
        Assert.Equal(0, counters.FixCalls);
        Assert.Equal(0, counters.MarketDataCalls);
        Assert.Equal(0, counters.CredentialCalls);
    }

    [Fact]
    public void Bound_delegates_feed_operation_bindings_and_provider_clients_when_tests_call_them()
    {
        var counters = new Counters();
        var result = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(ApprovedBindings(counters)).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var clients = result.CompositionResult!.ProviderClients!;
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
    public void Missing_delegates_are_rejected_individually()
    {
        var counters = new Counters();
        var valid = ApprovedBindings(counters);

        Assert.Contains("SocketConnect:MissingDelegate", Issues(valid with { SocketConnect = null }));
        Assert.Contains("TlsHandshake:MissingDelegate", Issues(valid with { TlsHandshake = null }));
        Assert.Contains("FixSession:MissingDelegate", Issues(valid with { FixSession = null }));
        Assert.Contains("MarketData:MissingDelegate", Issues(valid with { MarketData = null }));
        Assert.Contains("CredentialConfig:MissingDelegate", Issues(valid with { CredentialConfig = null }));
    }

    [Fact]
    public void Fake_test_only_delegate_is_rejected_in_final_production_validation_but_allowed_in_local_fake_mode()
    {
        var counters = new Counters();
        var fake = LmaxReadOnlyRuntimeCoreDelegateBindingSet.CreateLocalFake(
            (_, _, _) => Success("Socket"),
            (_, _, _) => Success("Tls"),
            (_, _, _, _) => Success("Fix"),
            (_, scope, _) => MarketDataSuccess(scope),
            (_, _, _, _) => CredentialSuccess());

        var production = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(fake).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);
        var local = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(fake, LmaxReadOnlyRuntimeCoreDelegateValidationMode.LocalFakeTest).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.False(production.Passed);
        Assert.True(production.ContainsFakeOrTestOnlyDelegate);
        Assert.True(local.Passed);
        Assert.Equal(0, counters.SocketCalls);
    }

    [Theory]
    [InlineData("ApiWorker")]
    [InlineData("LiveLauncher")]
    [InlineData("HostedService")]
    public void Broad_runtime_required_delegate_is_rejected(string rejectedCapability)
    {
        var counters = new Counters();
        var descriptor = ApprovedDescriptor("LmaxApprovedSocketDelegate", "SocketConnect") with
        {
            RequiresApiWorkerStartup = rejectedCapability == "ApiWorker",
            RequiresLiveLauncher = rejectedCapability == "LiveLauncher",
            RequiresHostedService = rejectedCapability == "HostedService"
        };
        var bindings = ApprovedBindings(counters) with { SocketDescriptor = descriptor };

        var result = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(bindings).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        Assert.False(result.Passed);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void Production_account_non_approved_instrument_and_usdjpy_without_caveat_are_rejected()
    {
        var counters = new Counters();

        Assert.False(LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateCompleteness(
            ApprovedBindings(counters),
            ValidScope(new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true))).Passed);
        Assert.False(LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateCompleteness(
            ApprovedBindings(counters),
            ValidScope(instruments: [new("XAUUSD", "9999", "8", "unapproved", false, null)])).Passed);
        Assert.False(LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateCompleteness(
            ApprovedBindings(counters),
            ValidScope(instruments: [new("USDJPY", "4004", "8", "validated_readiness_archive_with_caveat", false, null)])).Passed);
    }

    [Theory]
    [InlineData("NewOrderSingle")]
    [InlineData("OrderCancelRequest")]
    [InlineData("OrderStatusRequest")]
    [InlineData("TradeCaptureReportRequest")]
    [InlineData("Replay")]
    [InlineData("ShadowReplay")]
    public void Unsupported_order_trading_replay_categories_are_rejected_before_delegate_execution(string messageType)
    {
        var counters = new Counters();
        var result = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(ApprovedBindings(counters)).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var fix = result.CompositionResult!.ProviderClients!.FixClient.OpenSessionLogon(
            FixOptions([messageType]),
            ValidScope(),
            ValidAccessRecord());

        Assert.Equal("ForbiddenFixMessageType", fix.SanitizedErrorCategory);
        Assert.Equal(0, counters.FixCalls);
    }

    [Fact]
    public void Raw_fix_and_credentials_are_sanitized_from_bound_delegate_outputs()
    {
        var counters = new Counters();
        var result = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(ApprovedBindings(counters, includeSensitiveStrings: true)).Bind(
            ValidScope(),
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var fix = result.CompositionResult!.ProviderClients!.FixClient.OpenSessionLogon(FixOptions(), ValidScope(), ValidAccessRecord());
        var credential = result.CompositionResult.ProviderClients.CredentialConfigClient.AccessDemoReadOnlyConfig(CredentialOptions(), ValidScope(), ValidPolicy());
        var combined = string.Join(" ", new[] { fix.SanitizedStatus, fix.SanitizedErrorCategory, fix.SanitizedErrorMessage, credential.SanitizedStatus, credential.SanitizedErrorCategory, credential.SanitizedErrorMessage }.Where(x => x is not null));

        Assert.DoesNotContain("password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_delegate_binding_surfaces_expose_no_order_trading_replay_or_launcher_methods()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingSet),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingFactory),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingValidator),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingResult)
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

    private static IReadOnlyList<string> Issues(LmaxReadOnlyRuntimeCoreDelegateBindingSet bindings)
        => LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateCompleteness(bindings, ValidScope()).Issues;

    private static LmaxReadOnlyRuntimeCoreDelegateBindingSet ApprovedBindings(
        Counters counters,
        bool includeSensitiveStrings = false)
        => LmaxReadOnlyRuntimeCoreDelegateBindingSet.CreateApproved(
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
                return MarketDataSuccess(scope);
            },
            (_, _, _, _) =>
            {
                counters.CredentialCalls++;
                return includeSensitiveStrings
                    ? new LmaxRealReadOnlySecretAccessResult(true, false, false, false, false, "password secret 554=value", "secret", "password")
                    : CredentialSuccess();
            });

    private static LmaxReadOnlyRuntimeCoreDelegateDescriptor ApprovedDescriptor(string name, string operation)
        => new(name, operation, true, true, true, true, true, false, false, false, false, false, false);

    private static LmaxRealReadOnlyDependencyResult Success(string name)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"Local{name}Sanitized", null, null);

    private static LmaxReadOnlyMarketDataSessionClientResult MarketDataSuccess(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => new(
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

    private static LmaxRealReadOnlySecretAccessResult CredentialSuccess()
        => new(true, false, false, false, false, "LocalCredentialSanitized", null, null);

    private static LmaxReadOnlySocketConnectionOptions SocketOptions()
        => new("Demo/read-only", "lmax-demo-readonly", 443, TimeSpan.FromSeconds(5), true, true);

    private static LmaxReadOnlyTlsConnectionOptions TlsOptions()
        => new("Demo/read-only", "lmax-demo-readonly", "lmax-demo-readonly-host", TimeSpan.FromSeconds(5), true, "SystemDefaultValidation", true);

    private static LmaxReadOnlyFixSessionOptions FixOptions(IReadOnlyList<string>? allowedMessageTypes = null)
        => new("Demo/read-only", "readonly-sender", "lmax-demo-target", 30, TimeSpan.FromSeconds(5), true, allowedMessageTypes ?? LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes, true);

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
            "LMAX-R40",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            flags ?? new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 19, 00, 00, TimeSpan.Zero),
                "R40 local-only test approval marker",
                "LMAX-R40",
                "Demo/read-only",
                (instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments).Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r40-test"),
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
