using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyMarketDataFrameBoundaryProviderTests
{
    [Fact]
    public void Provider_can_be_constructed_without_opening_socket_tls_fix_marketdata_or_loading_credentials()
    {
        var client = new FakeMarketDataFrameClient();

        _ = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(ValidOptions(), client);

        Assert.Equal(0, client.RequestCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsHandshakeAttempted);
        Assert.False(client.RealFixLogonAttempted);
        Assert.False(client.RealFixMessageSent);
        Assert.False(client.RealMarketDataRequestSent);
        Assert.False(client.RealCredentialLoaded);
    }

    [Fact]
    public void RequestReadOnlyStatus_without_future_execution_approval_does_not_call_client()
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(ValidOptions(), client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("MarketDataExecutionNotApproved", result.SanitizedErrorCategory);
        Assert.Equal(4, result.InstrumentStatuses.Count);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, x.MarketDataBoundary));
        Assert.Equal(0, client.RequestCalls);
        Assert.False(client.RealMarketDataRequestSent);
    }

    [Fact]
    public void Approved_future_execution_path_can_be_exercised_with_fake_frames_only()
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());
        var shutdown = provider.ShutdownRevert();

        Assert.Equal("FakeMarketDataStatusSucceededSanitized", result.SanitizedStatus);
        Assert.True(shutdown);
        Assert.Equal(1, client.RequestCalls);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.Equal(new[] { "AUDUSD", "EURGBP", "GBPUSD", "USDJPY" }, client.CapturedSymbols.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, x.MarketDataBoundary));
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsHandshakeAttempted);
        Assert.False(client.RealFixLogonAttempted);
        Assert.False(client.RealFixMessageSent);
        Assert.False(client.RealMarketDataRequestSent);
    }

    [Fact]
    public void Sanitized_provider_result_preserves_marketdata_request_write_read_and_classification_state_fields()
    {
        var client = new FakeMarketDataFrameClient(propagateStateFields: true);
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal(1, client.RequestCalls);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("NewOrderSingle")]
    [InlineData("OrderCancelRequest")]
    [InlineData("OrderStatusRequest")]
    [InlineData("ExecutionReport")]
    [InlineData("TradeCaptureReportRequest")]
    [InlineData("Replay")]
    [InlineData("ShadowReplay")]
    public void Unsupported_order_trading_or_replay_message_type_is_rejected(string messageType)
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(allowedMessageTypes: [messageType], externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("ForbiddenMarketDataMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Theory]
    [InlineData("QuoteRequest")]
    [InlineData("SecurityDefinitionRequest")]
    public void Unsupported_readonly_unknown_message_type_is_rejected(string messageType)
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(allowedMessageTypes: [messageType], externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("UnsupportedMarketDataMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Live")]
    public void Non_demo_environment_is_rejected_before_client_use(string environment)
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(environmentLabel: environment, externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("NonDemoEnvironment", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Fact]
    public void Missing_readonly_flag_is_rejected_before_client_use()
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(demoReadOnly: false, externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("ReadOnlyFlagMissing", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("md://demo")]
    [InlineData("user@demo")]
    [InlineData("demo-password-label")]
    [InlineData("demo-secret-label")]
    [InlineData("35=D")]
    public void Unsafe_marketdata_label_is_rejected_before_client_use(string label)
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(requestTypeLabel: label, externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        Assert.Equal("UnsafeMarketDataLabel", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Fact]
    public void Non_approved_instrument_is_rejected_before_client_use()
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(externalMarketDataRequestExecutionApproved: true),
            client);
        var scope = ValidScope() with
        {
            Instruments =
            [
                new LmaxReadOnlyRuntimeApprovedInstrument(
                    "XAUUSD",
                    "9999",
                    "8",
                    "not_approved",
                    false,
                    null)
            ]
        };

        var result = provider.RequestReadOnlyStatus(scope);

        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Fact]
    public void UsdJpy_without_caveat_is_rejected_before_client_use()
    {
        var client = new FakeMarketDataFrameClient();
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(externalMarketDataRequestExecutionApproved: true),
            client);
        var scope = ValidScope() with
        {
            Instruments = ValidScope().Instruments.Select(x =>
                string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase)
                    ? x with { Caveat = null }
                    : x).ToList()
        };

        var result = provider.RequestReadOnlyStatus(scope);

        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.RequestCalls);
    }

    [Fact]
    public void Sanitized_snapshot_and_reject_evidence_contains_no_raw_fix_or_secrets()
    {
        var client = new FakeMarketDataFrameClient(includeSensitiveMessage: true);
        var provider = new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
            ValidOptions(externalMarketDataRequestExecutionApproved: true),
            client);

        var result = provider.RequestReadOnlyStatus(ValidScope());

        var text = string.Join(" ", result.SanitizedStatus, result.SanitizedErrorMessage, string.Join(" ", result.InstrumentStatuses.Select(x => $"{x.SanitizedStatus} {x.SanitizedErrorMessage}")));
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawFix", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.RawFixStored);
    }

    [Fact]
    public void Provider_public_surface_exposes_no_order_trading_or_replay_methods()
    {
        var methodNames = typeof(LmaxRealReadOnlyMarketDataFrameBoundaryProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxRealReadOnlyMarketDataFrameBoundaryProvider.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthenticateAsClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyMarketDataFrameBoundaryProvider", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyMarketDataFrameBoundaryProvider", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyMarketDataRequestOptions ValidOptions(
        string environmentLabel = "Demo/read-only",
        bool demoReadOnly = true,
        string requestTypeLabel = "ReadOnlyMarketDataRequest",
        string snapshotModeLabel = "SnapshotOrStatus",
        TimeSpan? timeout = null,
        IReadOnlyList<string>? allowedMessageTypes = null,
        bool externalMarketDataRequestExecutionApproved = false)
        => new(
            environmentLabel,
            demoReadOnly,
            requestTypeLabel,
            snapshotModeLabel,
            timeout ?? TimeSpan.FromSeconds(10),
            allowedMessageTypes ?? LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
            externalMarketDataRequestExecutionApproved);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate))
            .Scope;

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

    private sealed class FakeMarketDataFrameClient : ILmaxReadOnlyMarketDataFrameClient
    {
        private readonly bool includeSensitiveMessage;
        private readonly bool propagateStateFields;

        public FakeMarketDataFrameClient(
            bool includeSensitiveMessage = false,
            bool propagateStateFields = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
            this.propagateStateFields = propagateStateFields;
        }

        public int RequestCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }
        public bool RealFixMessageSent { get; private set; }
        public bool RealMarketDataRequestSent { get; private set; }
        public bool RealCredentialLoaded { get; private set; }
        public bool RawFixStored { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
            LmaxReadOnlyMarketDataRequestOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            return new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(x => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    x.Symbol,
                    x.SecurityId,
                    x.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                    MarketDataSnapshotCount: 1,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 0,
                    "FakeMarketDataSnapshotFullRefreshSanitized",
                    null,
                    includeSensitiveMessage ? "RawFix 35=W 554=demo password=demo secret=demo credential=demo bid/ask accepted." : null,
                    x.Caveat)).ToList(),
                "FakeMarketDataStatusSucceededSanitized",
                null,
                includeSensitiveMessage ? "RawFix 35=Y 554=demo password=demo secret=demo credential=demo rejected then sanitized." : null)
            {
                MarketDataRequestWriteAttempted = propagateStateFields,
                MarketDataRequestWriteSucceeded = propagateStateFields,
                MarketDataRequestResponseReadAttempted = propagateStateFields,
                MarketDataRequestReachedBoundedResponseClassification = propagateStateFields
            };
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }
    }
}
