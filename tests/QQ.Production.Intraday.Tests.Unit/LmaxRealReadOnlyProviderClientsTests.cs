using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyProviderClientsTests
{
    [Fact]
    public void All_clients_construct_without_external_side_effects()
    {
        var counters = new ExecutionCounters();

        _ = new LmaxRealReadOnlySocketConnectionClient((_, _, _) =>
        {
            counters.SocketCalls++;
            return Success("socket");
        });
        _ = new LmaxRealReadOnlyTlsHandshakeClient((_, _, _) =>
        {
            counters.TlsCalls++;
            return Success("tls");
        });
        _ = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) =>
        {
            counters.FixCalls++;
            return Success("fix");
        });
        _ = new LmaxRealReadOnlyMarketDataFrameClient((_, scope, _) =>
        {
            counters.MarketDataCalls++;
            return MarketDataSuccess(scope);
        });
        _ = new LmaxRealReadOnlyCredentialConfigClient((_, _, _, _) =>
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
    public void Fake_execution_paths_simulate_success_without_real_boundaries()
    {
        var scope = ValidScope();
        var accessRecord = ValidAccessRecord();
        var policy = ValidPolicy(realSecretMaterialAllowedNow: true);
        var counters = new ExecutionCounters();

        var socket = new LmaxRealReadOnlySocketConnectionClient((_, _, _) =>
        {
            counters.SocketCalls++;
            return Success("socket");
        });
        var tls = new LmaxRealReadOnlyTlsHandshakeClient((_, _, _) =>
        {
            counters.TlsCalls++;
            return Success("tls");
        });
        var fix = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) =>
        {
            counters.FixCalls++;
            return Success("fix");
        });
        var marketData = new LmaxRealReadOnlyMarketDataFrameClient((_, requestScope, _) =>
        {
            counters.MarketDataCalls++;
            return MarketDataSuccess(requestScope);
        });
        var credential = new LmaxRealReadOnlyCredentialConfigClient((_, _, _, _) =>
        {
            counters.CredentialCalls++;
            return CredentialSuccess();
        });

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, socket.OpenTcp(SocketOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, tls.OpenTls(TlsOptions(), scope).Status);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, fix.OpenSessionLogon(FixOptions(), scope, accessRecord).Status);
        Assert.Equal("FakeMarketDataSanitized", marketData.RequestReadOnlyStatus(MarketDataOptions(), scope).SanitizedStatus);
        Assert.Equal("Fake[redacted]Sanitized", credential.AccessDemoReadOnlyConfig(CredentialOptions(), scope, policy).SanitizedStatus);

        Assert.Equal(1, counters.SocketCalls);
        Assert.Equal(1, counters.TlsCalls);
        Assert.Equal(1, counters.FixCalls);
        Assert.Equal(1, counters.MarketDataCalls);
        Assert.Equal(1, counters.CredentialCalls);
    }

    [Fact]
    public void Unsafe_scope_and_production_account_are_rejected_before_client_operations()
    {
        var unsafeScope = ValidScope(new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true));
        var socket = new LmaxRealReadOnlySocketConnectionClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var tls = new LmaxRealReadOnlyTlsHandshakeClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var fix = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) => throw new InvalidOperationException("must not execute"));
        var marketData = new LmaxRealReadOnlyMarketDataFrameClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var credential = new LmaxRealReadOnlyCredentialConfigClient((_, _, _, _) => throw new InvalidOperationException("must not execute"));

        Assert.Equal("SafetyConstraintFailed", socket.OpenTcp(SocketOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", tls.OpenTls(TlsOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", fix.OpenSessionLogon(FixOptions(), unsafeScope, ValidAccessRecord()).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", marketData.RequestReadOnlyStatus(MarketDataOptions(), unsafeScope).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", credential.AccessDemoReadOnlyConfig(CredentialOptions(), unsafeScope, ValidPolicy()).SanitizedErrorCategory);
    }

    [Fact]
    public void Missing_readonly_or_non_demo_options_are_rejected()
    {
        var scope = ValidScope();
        var socket = new LmaxRealReadOnlySocketConnectionClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var tls = new LmaxRealReadOnlyTlsHandshakeClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var fix = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) => throw new InvalidOperationException("must not execute"));
        var marketData = new LmaxRealReadOnlyMarketDataFrameClient((_, _, _) => throw new InvalidOperationException("must not execute"));
        var credential = new LmaxRealReadOnlyCredentialConfigClient((_, _, _, _) => throw new InvalidOperationException("must not execute"));

        Assert.Equal("NonDemoReadOnlySocketConfig", socket.OpenTcp(SocketOptions(demoReadOnly: false), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyTlsConfig", tls.OpenTls(TlsOptions(environment: "Production"), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyFixConfig", fix.OpenSessionLogon(FixOptions(demoReadOnly: false), scope, ValidAccessRecord()).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyMarketDataConfig", marketData.RequestReadOnlyStatus(MarketDataOptions(environment: "Production"), scope).SanitizedErrorCategory);
        Assert.Equal("NonDemoReadOnlyCredentialConfig", credential.AccessDemoReadOnlyConfig(CredentialOptions(demoReadOnly: false), scope, ValidPolicy()).SanitizedErrorCategory);
    }

    [Fact]
    public void MarketData_client_rejects_non_approved_instrument_and_usdjpy_without_caveat()
    {
        var client = new LmaxRealReadOnlyMarketDataFrameClient((_, _, _) => throw new InvalidOperationException("must not execute"));

        var nonApproved = ValidScope(instruments:
        [
            new("XAUUSD", "9999", "8", "unapproved", false, null)
        ]);
        var usdJpyWithoutCaveat = ValidScope(instruments:
        [
            new("USDJPY", "4004", "8", "validated_readiness_archive_with_caveat", false, null)
        ]);

        Assert.Equal("SafetyConstraintFailed", client.RequestReadOnlyStatus(MarketDataOptions(), nonApproved).SanitizedErrorCategory);
        Assert.Equal("SafetyConstraintFailed", client.RequestReadOnlyStatus(MarketDataOptions(), usdJpyWithoutCaveat).SanitizedErrorCategory);
    }

    [Fact]
    public void MarketData_client_sanitizer_preserves_request_write_read_and_classification_state_fields()
    {
        var scope = ValidScope();
        var client = new LmaxRealReadOnlyMarketDataFrameClient((_, requestScope, _) => MarketDataSuccess(requestScope) with
        {
            MarketDataRequestWriteAttempted = true,
            MarketDataRequestWriteSucceeded = true,
            MarketDataRequestResponseReadAttempted = true,
            MarketDataRequestReachedBoundedResponseClassification = true
        });

        var result = client.RequestReadOnlyStatus(MarketDataOptions(), scope);

        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
    }

    [Theory]
    [InlineData("NewOrderSingle")]
    [InlineData("OrderCancelRequest")]
    [InlineData("OrderStatusRequest")]
    [InlineData("TradeCaptureReportRequest")]
    [InlineData("Replay")]
    [InlineData("ShadowReplay")]
    public void Fix_client_rejects_unsupported_order_trading_replay_categories(string messageType)
    {
        var client = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) => throw new InvalidOperationException("must not execute"));
        var options = FixOptions(allowedMessageTypes: [messageType]);

        var result = client.OpenSessionLogon(options, ValidScope(), ValidAccessRecord());

        Assert.Equal("ForbiddenFixMessageType", result.SanitizedErrorCategory);
    }

    [Fact]
    public void Sanitized_outputs_do_not_return_raw_fix_or_credentials()
    {
        var scope = ValidScope();
        var fix = new LmaxRealReadOnlyFixFrameClient((_, _, _, _) => new LmaxRealReadOnlyDependencyResult(
            LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
            "RawFix 35=D password secret 554=value",
            "RawFix",
            "RawFix 35=AE password secret 554=value"));
        var marketData = new LmaxRealReadOnlyMarketDataFrameClient((_, requestScope, _) => new LmaxReadOnlyMarketDataSessionClientResult(
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
        var credential = new LmaxRealReadOnlyCredentialConfigClient((_, _, _, _) => new LmaxRealReadOnlySecretAccessResult(
            true,
            false,
            false,
            false,
            false,
            "password secret 554=value",
            "password",
            "secret"));

        var fixResult = fix.OpenSessionLogon(FixOptions(), scope, ValidAccessRecord());
        var marketDataResult = marketData.RequestReadOnlyStatus(MarketDataOptions(), scope);
        var credentialResult = credential.AccessDemoReadOnlyConfig(CredentialOptions(), scope, ValidPolicy(realSecretMaterialAllowedNow: true));

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
    public void Public_client_surfaces_expose_no_order_trading_replay_or_launcher_methods()
    {
        var clientTypes = new[]
        {
            typeof(LmaxRealReadOnlySocketConnectionClient),
            typeof(LmaxRealReadOnlyTlsHandshakeClient),
            typeof(LmaxRealReadOnlyFixFrameClient),
            typeof(LmaxRealReadOnlyMarketDataFrameClient),
            typeof(LmaxRealReadOnlyCredentialConfigClient)
        };

        foreach (var type in clientTypes)
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
            "LMAX-R34",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            flags ?? new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 18, 30, 00, TimeSpan.Zero),
                "R34 local-only test approval marker",
                "LMAX-R34",
                "Demo/read-only",
                (instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments).Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r34-test"),
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
