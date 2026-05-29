using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlySocketBoundaryProviderTests
{
    [Fact]
    public void Provider_can_be_constructed_without_opening_socket_or_loading_credentials()
    {
        var client = new FakeSocketConnectionClient();

        _ = new LmaxRealReadOnlySocketBoundaryProvider(ValidOptions(), client);

        Assert.Equal(0, client.OpenCalls);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
        Assert.False(client.RealCredentialLoaded);
    }

    [Fact]
    public void OpenTcp_without_future_execution_approval_does_not_call_client()
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(ValidOptions(), client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SocketExecutionNotApproved", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
    }

    [Fact]
    public void Approved_future_execution_path_can_be_exercised_with_fake_client_only()
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());
        var shutdown = provider.ShutdownRevert();

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal("FakeSocketBoundarySucceededSanitized", result.SanitizedStatus);
        Assert.True(shutdown);
        Assert.Equal(1, client.OpenCalls);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.False(client.RealSocketOpened);
        Assert.False(client.RealTcpConnectionAttempted);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Live")]
    public void Non_demo_environment_is_rejected_before_client_use(string environment)
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(environmentLabel: environment, externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("NonDemoEnvironment", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Missing_readonly_flag_is_rejected_before_client_use()
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(demoReadOnly: false, externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ReadOnlyFlagMissing", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tcp://demo.example")]
    [InlineData("user@demo.example")]
    [InlineData("demo-password-host")]
    [InlineData("demo-secret-host")]
    public void Unsafe_endpoint_config_is_rejected_before_client_use(string endpointLabel)
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(endpointLabel: endpointLabel, externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("UnsafeEndpointLabel", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Invalid_port_is_rejected_before_client_use(int port)
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(port: port, externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("InvalidPort", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Invalid_timeout_is_rejected_before_client_use(int timeoutSeconds)
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(timeout: TimeSpan.FromSeconds(timeoutSeconds), externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("InvalidTimeout", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Timeout_and_cancellation_settings_are_accepted()
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(timeout: TimeSpan.FromSeconds(5), externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope(), CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(5), client.CapturedOptions!.Timeout);
    }

    [Fact]
    public void Unsafe_scope_is_rejected_before_client_use()
    {
        var client = new FakeSocketConnectionClient();
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(InvalidScope());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.OpenCalls);
    }

    [Fact]
    public void Sanitized_evidence_contains_no_secrets()
    {
        var client = new FakeSocketConnectionClient(includeSensitiveMessage: true);
        var provider = new LmaxRealReadOnlySocketBoundaryProvider(
            ValidOptions(externalConnectionExecutionApproved: true),
            client);

        var result = provider.OpenTcp(ValidScope());

        Assert.DoesNotContain("password", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result.SanitizedErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_public_surface_exposes_no_tls_fix_marketdata_order_or_runtime_wiring_methods()
    {
        var type = typeof(LmaxRealReadOnlySocketBoundaryProvider);
        var methodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, x => x.Contains("Tls", StringComparison.OrdinalIgnoreCase));
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
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxRealReadOnlySocketBoundaryProvider.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlySocketBoundaryProvider", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxRealReadOnlySocketBoundaryProvider", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxReadOnlySocketConnectionOptions ValidOptions(
        string environmentLabel = "Demo/read-only",
        string endpointLabel = "lmax-demo-readonly",
        int port = 443,
        TimeSpan? timeout = null,
        bool demoReadOnly = true,
        bool externalConnectionExecutionApproved = false)
        => new(
            environmentLabel,
            endpointLabel,
            port,
            timeout ?? TimeSpan.FromSeconds(10),
            demoReadOnly,
            externalConnectionExecutionApproved);

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

    private sealed class FakeSocketConnectionClient : ILmaxReadOnlySocketConnectionClient
    {
        private readonly bool includeSensitiveMessage;

        public FakeSocketConnectionClient(bool includeSensitiveMessage = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int OpenCalls { get; private set; }
        public int ShutdownCalls { get; private set; }
        public bool RealSocketOpened { get; private set; }
        public bool RealTcpConnectionAttempted { get; private set; }
        public bool RealCredentialLoaded { get; private set; }
        public LmaxReadOnlySocketConnectionOptions? CapturedOptions { get; private set; }

        public LmaxRealReadOnlyDependencyResult OpenTcp(
            LmaxReadOnlySocketConnectionOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            CapturedOptions = options;
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                "FakeSocketBoundarySucceededSanitized",
                null,
                includeSensitiveMessage ? "Fake socket password=demo secret=demo 554=demo opened." : null);
        }

        public bool ShutdownRevert()
        {
            ShutdownCalls++;
            return true;
        }
    }
}
