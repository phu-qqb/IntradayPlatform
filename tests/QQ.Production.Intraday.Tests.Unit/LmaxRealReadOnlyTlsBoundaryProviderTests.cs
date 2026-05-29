using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyTlsBoundaryProviderTests
{
    [Fact]
    public void Provider_can_be_constructed_without_opening_socket_creating_tls_stream_or_loading_credentials()
    {
        var client = new FakeTlsHandshakeClient();

        _ = new LmaxRealReadOnlyTlsBoundaryProvider(ValidOptions(), client);

        Assert.Equal(0, client.OpenCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsStreamCreated);
        Assert.False(client.RealTlsHandshakeAttempted);
        Assert.False(client.RealCredentialLoaded);
    }

    [Fact]
    public void OpenTls_without_future_execution_approval_does_not_call_client()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(ValidOptions(), client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("TlsExecutionNotApproved", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
        Assert.False(client.RealTlsStreamCreated);
        Assert.False(client.RealTlsHandshakeAttempted);
    }

    [Fact]
    public void Approved_future_execution_path_can_be_exercised_with_fake_client_only()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());
        var shutdown = provider.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal("FakeTlsBoundarySucceededSanitized", result.SanitizedStatus);
        Assert.True(shutdown);
        Assert.Equal(1, client.OpenCalls);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealTlsStreamCreated);
        Assert.False(client.RealTlsHandshakeAttempted);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Live")]
    public void Non_demo_environment_is_rejected_before_client_use(string environment)
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(environmentLabel: environment, externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("NonDemoEnvironment", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Missing_readonly_flag_is_rejected_before_client_use()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(demoReadOnly: false, externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ReadOnlyFlagMissing", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tls://demo.example")]
    [InlineData("user@demo.example")]
    [InlineData("demo-password-host")]
    [InlineData("demo-secret-host")]
    [InlineData("private-key-label")]
    [InlineData("-----BEGIN CERTIFICATE-----")]
    public void Unsafe_tls_config_is_rejected_before_client_use(string label)
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(endpointLabel: label, externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("UnsafeTlsEndpointOrServerName", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Unsafe_target_host_config_is_rejected_before_client_use()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(targetHostLabel: "secret-target-host", externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("UnsafeTlsEndpointOrServerName", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Invalid_timeout_is_rejected_before_client_use(int timeoutSeconds)
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(timeout: TimeSpan.FromSeconds(timeoutSeconds), externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("InvalidTimeout", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Unsafe_certificate_policy_is_rejected_before_client_use()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(certificatePolicy: "TrustAllCertificates", externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("UnsafeCertificateValidationPolicy", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Timeout_and_cancellation_settings_are_accepted()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(timeout: TimeSpan.FromSeconds(5), externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope(), CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(5), client.CapturedOptions!.Timeout);
    }

    [Fact]
    public void Unsafe_scope_is_rejected_before_client_use()
    {
        var client = new FakeTlsHandshakeClient();
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(InvalidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Sanitized_evidence_contains_no_secrets_or_certificate_private_material()
    {
        var client = new FakeTlsHandshakeClient(includeSensitiveMessage: true);
        var provider = new LmaxRealReadOnlyTlsBoundaryProvider(
            ValidOptions(externalTlsHandshakeExecutionApproved: true),
            client);

        var result = provider.OpenTls(ValidScope());

        Assert.DoesNotContain("password", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-----BEGIN", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_public_surface_exposes_no_fix_marketdata_order_or_runtime_wiring_methods()
    {
        var type = typeof(LmaxRealReadOnlyTlsBoundaryProvider);
        var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, x => x.Contains("Fix", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("MarketData", StringComparison.OrdinalIgnoreCase));
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
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxRealReadOnlyTlsBoundaryProvider.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthenticateAsClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyTlsBoundaryProvider", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlyTlsBoundaryProvider", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyTlsConnectionOptions ValidOptions(
        string environmentLabel = "Demo/read-only",
        string endpointLabel = "lmax-demo-readonly",
        string targetHostLabel = "lmax-demo-readonly-host",
        TimeSpan? timeout = null,
        bool demoReadOnly = true,
        string certificatePolicy = "SystemDefaultValidation",
        bool externalTlsHandshakeExecutionApproved = false)
        => new(
            environmentLabel,
            endpointLabel,
            targetHostLabel,
            timeout ?? TimeSpan.FromSeconds(10),
            demoReadOnly,
            certificatePolicy,
            externalTlsHandshakeExecutionApproved);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate))
            .Scope;

    private static LmaxTemporaryReadOnlyRuntimeActivationScope InvalidScope()
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            SafetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)))
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

    private sealed class FakeTlsHandshakeClient : ILmaxReadOnlyTlsHandshakeClient
    {
        private readonly bool includeSensitiveMessage;

        public FakeTlsHandshakeClient(bool includeSensitiveMessage = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int OpenCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealTlsStreamCreated { get; private set; }
        public bool RealTlsHandshakeAttempted { get; private set; }
        public bool RealCredentialLoaded { get; private set; }
        public LmaxReadOnlyTlsConnectionOptions? CapturedOptions { get; private set; }

        public LmaxRealReadOnlyDependencyResult OpenTls(
            LmaxReadOnlyTlsConnectionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            CapturedOptions = options;
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                "FakeTlsBoundarySucceededSanitized",
                null,
                includeSensitiveMessage ? "Fake TLS password=demo secret=demo private key -----BEGIN CERTIFICATE----- 554=demo opened." : null);
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }
    }
}
