using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyFixFrameBoundaryProviderTests
{
    [Fact]
    public void Provider_can_be_constructed_without_opening_socket_tls_fix_or_loading_credentials()
    {
        var client = new FakeFixFrameClient();

        _ = new LmaxRealReadOnlyFixFrameBoundaryProvider(ValidOptions(), client);

        Assert.Equal(0, client.OpenCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsHandshakeAttempted);
        Assert.False(client.RealFixLogonAttempted);
        Assert.False(client.RealFixMessageSent);
        Assert.False(client.RealCredentialLoaded);
    }

    [Fact]
    public void OpenSessionLogon_without_future_execution_approval_does_not_call_client()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(ValidOptions(), client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("FixExecutionNotApproved", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
        Assert.False(client.RealFixLogonAttempted);
        Assert.False(client.RealFixMessageSent);
    }

    [Fact]
    public void Approved_future_execution_path_can_be_exercised_with_fake_frames_only()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());
        var shutdown = provider.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal("FakeFixLogonSucceededSanitized", result.SanitizedStatus);
        Assert.True(shutdown);
        Assert.Equal(1, client.OpenCalls);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsHandshakeAttempted);
        Assert.False(client.RealFixLogonAttempted);
        Assert.False(client.RealFixMessageSent);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("NewOrderSingle")]
    [InlineData("OrderCancelRequest")]
    [InlineData("OrderStatusRequest")]
    [InlineData("ExecutionReport")]
    public void Unsupported_order_or_trading_fix_message_type_is_rejected(string messageType)
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(allowedMessageTypes: [messageType], externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ForbiddenFixMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData("TradeCaptureReportRequest")]
    [InlineData("Replay")]
    [InlineData("ShadowReplay")]
    public void Trade_capture_order_status_or_replay_message_type_is_rejected(string messageType)
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(allowedMessageTypes: [messageType], externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ForbiddenFixMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Unsupported_readonly_unknown_message_type_is_rejected()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(allowedMessageTypes: ["QuoteRequest"], externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("UnsupportedFixMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Live")]
    public void Non_demo_environment_is_rejected_before_client_use(string environment)
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(environmentLabel: environment, externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("NonDemoEnvironment", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Missing_readonly_flag_is_rejected_before_client_use()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(demoReadOnly: false, externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ReadOnlyFlagMissing", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Non_approved_instrument_is_rejected_before_client_use()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
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

        var result = provider.OpenSessionLogon(scope, ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void UsdJpy_without_caveat_is_rejected_before_client_use()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
            client);
        var scope = ValidScope() with
        {
            Instruments = ValidScope().Instruments.Select(x =>
                string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase)
                    ? x with { Caveat = null }
                    : x).ToList()
        };

        var result = provider.OpenSessionLogon(scope, ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void MarketDataRequest_intent_is_limited_to_approved_instrument_scope()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal(new[] { "AUDUSD", "EURGBP", "GBPUSD", "USDJPY" }, client.CapturedSymbols.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Unsanitized_credential_evidence_is_rejected_before_client_use()
    {
        var client = new FakeFixFrameClient();
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(
            ValidScope(),
            ValidCredentialRecord() with { SensitiveMaterialReturned = true });

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("CredentialPolicyNotSafe", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Logon_session_evidence_is_sanitized_and_raw_fix_is_not_stored()
    {
        var client = new FakeFixFrameClient(includeSensitiveMessage: true);
        var provider = new LmaxRealReadOnlyFixFrameBoundaryProvider(
            ValidOptions(externalFixExecutionApproved: true),
            client);

        var result = provider.OpenSessionLogon(ValidScope(), ValidCredentialRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.DoesNotContain("password", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawFix", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.RawFixStored);
    }

    [Fact]
    public void Provider_public_surface_exposes_no_order_trading_or_replay_methods()
    {
        var type = typeof(LmaxRealReadOnlyFixFrameBoundaryProvider);
        var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("MarketData", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_api_worker_default_config_launcher_or_hosted_service_wiring_was_added()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxRealReadOnlyFixFrameBoundaryProvider.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthenticateAsClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyFixFrameBoundaryProvider", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyFixFrameBoundaryProvider", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyFixSessionOptions ValidOptions(
        string environmentLabel = "Demo/read-only",
        string senderCompIdLabel = "readonly-sender",
        string targetCompIdLabel = "lmax-demo-target",
        int heartbeatIntervalSeconds = 30,
        TimeSpan? timeout = null,
        bool demoReadOnly = true,
        IReadOnlyList<string>? allowedMessageTypes = null,
        bool externalFixExecutionApproved = false)
        => new(
            environmentLabel,
            senderCompIdLabel,
            targetCompIdLabel,
            heartbeatIntervalSeconds,
            timeout ?? TimeSpan.FromSeconds(10),
            demoReadOnly,
            allowedMessageTypes ?? LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes,
            externalFixExecutionApproved);

    private static LmaxReadOnlyCredentialSanitizationRecord ValidCredentialRecord()
        => new(
            AccessPolicyAccepted: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialEvidenceSanitized");

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

    private sealed class FakeFixFrameClient : ILmaxReadOnlyFixFrameClient
    {
        private readonly bool includeSensitiveMessage;

        public FakeFixFrameClient(bool includeSensitiveMessage = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int OpenCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public bool RealFixLogonAttempted { get; private set; }
        public bool RealFixMessageSent { get; private set; }
        public bool RealCredentialLoaded { get; private set; }
        public bool RawFixStored { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public LmaxRealReadOnlyDependencyResult OpenSessionLogon(
            LmaxReadOnlyFixSessionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialSanitizationRecord accessRecord,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                "FakeFixLogonSucceededSanitized",
                null,
                includeSensitiveMessage ? "RawFix 35=A 554=demo password=demo secret=demo credential=demo accepted." : null);
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }
    }
}
