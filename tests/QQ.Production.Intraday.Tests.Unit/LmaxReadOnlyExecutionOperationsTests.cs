using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExecutionOperationsTests
{
    [Fact]
    public void All_operations_construct_without_external_side_effects()
    {
        var counters = new ExecutionCounters();

        _ = new LmaxReadOnlySocketConnectOperationBinding((_, _, _) =>
        {
            counters.SocketCalls++;
            return Success("socket");
        });
        _ = new LmaxReadOnlyTlsHandshakeOperationBinding((_, _, _) =>
        {
            counters.TlsCalls++;
            return Success("tls");
        });
        _ = new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) =>
        {
            counters.FixCalls++;
            return Success("fix");
        });
        _ = new LmaxReadOnlyMarketDataOperationBinding((_, scope, _) =>
        {
            counters.MarketDataCalls++;
            return MarketDataSuccess(scope);
        });
        _ = new LmaxReadOnlyCredentialConfigOperationBinding((_, _, _, _) =>
        {
            counters.CredentialCalls++;
            return CredentialSuccess();
        });

        Assert.Equal(0, counters.SocketCalls);
        Assert.Equal(0, counters.TlsCalls);
        Assert.Equal(0, counters.FixCalls);
        Assert.Equal(0, counters.MarketDataCalls);
        Assert.Equal(0, counters.CredentialCalls);
    }

    [Fact]
    public void Fake_operations_simulate_success_and_can_feed_provider_clients_without_real_boundaries()
    {
        var scope = ValidScope();
        var accessRecord = ValidAccessRecord();
        var policy = ValidPolicy(realSecretMaterialAllowedNow: true);
        var counters = new ExecutionCounters();

        var bindingSet = new LmaxReadOnlyExecutionOperationBindingSet(
            new LmaxReadOnlySocketConnectOperationBinding((_, _, _) =>
            {
                counters.SocketCalls++;
                return Success("socket");
            }),
            new LmaxReadOnlyTlsHandshakeOperationBinding((_, _, _) =>
            {
                counters.TlsCalls++;
                return Success("tls");
            }),
            new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) =>
            {
                counters.FixCalls++;
                return Success("fix");
            }),
            new LmaxReadOnlyMarketDataOperationBinding((_, requestScope, _) =>
            {
                counters.MarketDataCalls++;
                return MarketDataSuccess(requestScope);
            }),
            new LmaxReadOnlyCredentialConfigOperationBinding((_, _, _, _) =>
            {
                counters.CredentialCalls++;
                return CredentialSuccess();
            }));

        var clients = bindingSet.CreateProviderClientOperationSet();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.SocketClient.OpenTcp(SocketOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.TlsClient.OpenTls(TlsOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, clients.FixClient.OpenSessionLogon(FixOptions(), scope, accessRecord).Status);
        Assert.Equal("FakeMarketDataSanitized", clients.MarketDataClient.RequestReadOnlyStatus(MarketDataOptions(), scope).SanitizedStatus);
        Assert.Equal("Fake[redacted]Sanitized", clients.CredentialConfigClient.AccessDemoReadOnlyConfig(CredentialOptions(), scope, policy).SanitizedStatus);

        Assert.Equal(1, counters.SocketCalls);
        Assert.Equal(1, counters.TlsCalls);
        Assert.Equal(1, counters.FixCalls);
        Assert.Equal(1, counters.MarketDataCalls);
        Assert.Equal(1, counters.CredentialCalls);
    }

    [Fact]
    public void Unsafe_scope_and_production_account_are_rejected_before_operation_cores()
    {
        var unsafeScope = ValidScope(new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true));
        var socket = new LmaxReadOnlySocketConnectOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var tls = new LmaxReadOnlyTlsHandshakeOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var fix = new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) => throw new InvalidOperationException("must not execute"));
        var marketData = new LmaxReadOnlyMarketDataOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var credential = new LmaxReadOnlyCredentialConfigOperationBinding((_, _, _, _) => throw new InvalidOperationException("must not execute"));

        Assert.Equal("SafetyConstraintFailed", socket.Connect(SocketOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", tls.Handshake(TlsOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", fix.Open(FixOptions(), unsafeScope, ValidAccessRecord()).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", marketData.Read(MarketDataOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", credential.Access(CredentialOptions(), unsafeScope, ValidPolicy()).SanitizedErrorCategory);
    }

    [Fact]
    public void Missing_readonly_or_non_demo_options_are_rejected()
    {
        var scope = ValidScope();
        var socket = new LmaxReadOnlySocketConnectOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var tls = new LmaxReadOnlyTlsHandshakeOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var fix = new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) => throw new InvalidOperationException("must not execute"));
        var marketData = new LmaxReadOnlyMarketDataOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));
        var credential = new LmaxReadOnlyCredentialConfigOperationBinding((_, _, _, _) => throw new InvalidOperationException("must not execute"));

        Assert.Equal("NonDemoReadOnlySocketConfig", socket.Connect(SocketOptions(demoReadOnly: false), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyTlsConfig", tls.Handshake(TlsOptions(environment: "Production"), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyFixConfig", fix.Open(FixOptions(demoReadOnly: false), scope, ValidAccessRecord()).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyMarketDataConfig", marketData.Read(MarketDataOptions(environment: "Production"), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyCredentialConfig", credential.Access(CredentialOptions(demoReadOnly: false), scope, ValidPolicy()).SanitizedErrorCategory);
    }

    [Fact]
    public void MarketData_operation_rejects_non_approved_instrument_and_usdjpy_without_caveat()
    {
        var operation = new LmaxReadOnlyMarketDataOperationBinding((_, _, _) => throw new InvalidOperationException("must not execute"));

        var nonApproved = ValidScope(instruments:
        [
            new("XAUUSD", "9999", "8", "unapproved", false, null)
        ]);
        var usdJpyWithoutCaveat = ValidScope(instruments:
        [
            new("USDJPY", "4004", "8", "validated_readiness_archive_with_caveat", false, null)
        ]);

        Assert.Equal("SafetyConstraintFailed", operation.Read(MarketDataOptions(), nonApproved).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", operation.Read(MarketDataOptions(), usdJpyWithoutCaveat).SanitizedErrorCategory);
    }

    [Theory]
    [InlineData("NewOrderSingle")]
    [InlineData("OrderCancelRequest")]
    [InlineData("OrderStatusRequest")]
    [InlineData("TradeCaptureReportRequest")]
    [InlineData("Replay")]
    [InlineData("ShadowReplay")]
    public void Fix_operation_rejects_unsupported_order_trading_replay_categories(string messageType)
    {
        var operation = new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) => throw new InvalidOperationException("must not execute"));

        var result = operation.Open(FixOptions(allowedMessageTypes: [messageType]), ValidScope(), ValidAccessRecord());

        Assert.Equal("ForbiddenFixMessageType", result.SanitizedErrorCategory);
    }

    [Fact]
    public void Sanitized_outputs_do_not_return_raw_fix_or_credentials()
    {
        var scope = ValidScope();
        var fix = new LmaxReadOnlyFixSessionOperationBinding((_, _, _, _) => new LmaxRealReadOnlyDependencyResult(
            LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
            "RawFix 35=D password secret 554=value",
            "RawFix",
            "RawFix 35=AE password secret 554=value"));
        var marketData = new LmaxReadOnlyMarketDataOperationBinding((_, requestScope, _) => new LmaxReadOnlyMarketDataSessionClientResult(
            requestScope.Instruments.Select(i => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                i.Symbol,
                i.SecurityId,
                i.SecurityIdSource,
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                0,
                1,
                0,
                0,
                "RawFix 554=value secret",
                "RawFix",
                "password secret 554=value",
                i.Caveat)).ToList(),
            "RawFix 554=value secret",
            "RawFix",
            "password secret 554=value"));
        var credential = new LmaxReadOnlyCredentialConfigOperationBinding((_, _, _, _) => new LmaxRealReadOnlySecretAccessResult(
            true,
            false,
            false,
            false,
            false,
            "password secret 554=value",
            "password",
            "secret"));

        var fixResult = fix.Open(FixOptions(), scope, ValidAccessRecord());
        var marketDataResult = marketData.Read(MarketDataOptions(), scope);
        var credentialResult = credential.Access(CredentialOptions(), scope, ValidPolicy(realSecretMaterialAllowedNow: true));

        var combined = string.Join(" ", new[]
        {
            fixResult.SanitizedStatus,
            fixResult.SanitizedErrorCategory,
            fixResult.SanitizedErrorMessage,
            marketDataResult.SanitizedStatus,
            marketDataResult.SanitizedErrorCategory,
            marketDataResult.SanitizedErrorMessage,
            credentialResult.SanitizedStatus,
            credentialResult.SanitizedErrorCategory,
            credentialResult.SanitizedErrorMessage
        }.Where(x => x is not null));

        Assert.DoesNotContain("password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=AE", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_operation_surfaces_expose_no_order_trading_replay_or_launcher_methods()
    {
        var operationTypes = new[]
        {
            typeof(LmaxReadOnlySocketConnectOperationBinding),
            typeof(LmaxReadOnlyTlsHandshakeOperationBinding),
            typeof(LmaxReadOnlyFixSessionOperationBinding),
            typeof(LmaxReadOnlyMarketDataOperationBinding),
            typeof(LmaxReadOnlyCredentialConfigOperationBinding)
        };

        foreach (var type in operationTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
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

    private static LmaxRealReadOnlyDependencyResult Success(string name)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"Fake{name}Sanitized", null, null);

    private static LmaxReadOnlyMarketDataSessionClientResult MarketDataSuccess(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
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
                "FakeInstrumentMarketDataSanitized",
                null,
                null,
                instrument.Caveat)).ToList(),
            "FakeMarketDataSanitized",
            null,
            null);

    private static LmaxRealReadOnlySecretAccessResult CredentialSuccess()
        => new(
            AccessAllowed: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "FakeCredentialSanitized",
            null,
            null);

    private static LmaxReadOnlySocketConnectionOptions SocketOptions(
        string environment = "Demo/read-only",
        bool demoReadOnly = true)
        => new(environment, "lmax-demo-readonly", 443, TimeSpan.FromSeconds(5), demoReadOnly, true);

    private static LmaxReadOnlyTlsConnectionOptions TlsOptions(
        string environment = "Demo/read-only",
        bool demoReadOnly = true)
        => new(environment, "lmax-demo-readonly", "lmax-demo-readonly-host", TimeSpan.FromSeconds(5), demoReadOnly, "SystemDefaultValidation", true);

    private static LmaxReadOnlyFixSessionOptions FixOptions(
        bool demoReadOnly = true,
        IReadOnlyList<string>? allowedMessageTypes = null)
        => new("Demo/read-only", "readonly-sender", "lmax-demo-target", 30, TimeSpan.FromSeconds(5), demoReadOnly, allowedMessageTypes ?? LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes, true);

    private static LmaxReadOnlyMarketDataRequestOptions MarketDataOptions(
        string environment = "Demo/read-only",
        bool demoReadOnly = true)
        => new(environment, demoReadOnly, "ReadOnlyMarketDataRequest", "SnapshotOrStatus", TimeSpan.FromSeconds(5), LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes, true);

    private static LmaxReadOnlyCredentialConfigOptions CredentialOptions(
        string environment = "Demo/read-only",
        bool demoReadOnly = true)
        => new(environment, demoReadOnly, "local-approved-demo-readonly-config", true);

    private static LmaxReadOnlyCredentialAccessPolicy ValidPolicy(bool realSecretMaterialAllowedNow = false)
        => new(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: realSecretMaterialAllowedNow,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only");

    private static LmaxReadOnlyCredentialSanitizationRecord ValidAccessRecord()
        => new(
            AccessPolicyAccepted: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            SanitizedStatus: "FakeCredentialAccessSanitized");

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope(
        LmaxReadOnlyRuntimeSafetyFlags? flags = null,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null)
        => new(
            "LMAX-R36",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            flags ?? new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 19, 00, 00, TimeSpan.Zero),
                "R36 local-only test approval marker",
                "LMAX-R36",
                "Demo/read-only",
                (instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments).Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r36-test"),
            MaxRuntimeSeconds: 30,
            "artifacts/readiness/lmax-runtime-enablement");

    private sealed class ExecutionCounters
    {
        public int SocketCalls { get; set; }
        public int TlsCalls { get; set; }
        public int FixCalls { get; set; }
        public int MarketDataCalls { get; set; }
        public int CredentialCalls { get; set; }
    }
}
