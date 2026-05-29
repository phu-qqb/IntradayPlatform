using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxRealReadOnlyCredentialConfigBoundaryProviderTests
{
    [Fact]
    public void Provider_can_be_constructed_without_loading_real_credentials()
    {
        var client = new FakeCredentialConfigClient();

        _ = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(ValidOptions(), client);

        Assert.Equal(0, client.AccessCalls);
        Assert.False(client.RealSecretMaterialLoaded);
        Assert.False(client.SensitiveMaterialReturned);
        Assert.False(client.SensitiveMaterialPrinted);
        Assert.False(client.SensitiveMaterialStored);
    }

    [Fact]
    public void Access_without_future_approval_does_not_load_real_secret_material_or_call_client()
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(ValidOptions(), client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy());

        Assert.True(result.AccessAllowed);
        Assert.False(result.RealSecretMaterialLoaded);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.False(result.SensitiveMaterialPrinted);
        Assert.False(result.SensitiveMaterialStored);
        Assert.Equal("CredentialAccessNotExecutedInCurrentPhase", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Fact]
    public void Approved_future_execution_path_can_be_exercised_with_fake_config_provider_only()
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(realSecretMaterialAllowedNow: true));

        Assert.True(result.AccessAllowed);
        Assert.False(result.RealSecretMaterialLoaded);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.False(result.SensitiveMaterialPrinted);
        Assert.False(result.SensitiveMaterialStored);
        Assert.Equal(1, client.AccessCalls);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Live")]
    public void Non_demo_environment_is_rejected_before_client_use(string environment)
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(environmentLabel: environment, externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(realSecretMaterialAllowedNow: true));

        Assert.False(result.AccessAllowed);
        Assert.Equal("NonDemoEnvironment", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Fact]
    public void Missing_readonly_flag_is_rejected_before_client_use()
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(demoReadOnly: false, externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(realSecretMaterialAllowedNow: true));

        Assert.False(result.AccessAllowed);
        Assert.Equal("ReadOnlyFlagMissing", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("vault://demo")]
    [InlineData("user@store")]
    [InlineData("demo-password-source")]
    [InlineData("demo-secret-source")]
    public void Unsafe_config_source_label_is_rejected_before_client_use(string label)
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(configSourceLabel: label, externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(realSecretMaterialAllowedNow: true));

        Assert.False(result.AccessAllowed);
        Assert.Equal("UnsafeConfigSourceLabel", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Fact]
    public void Unsafe_policy_is_rejected_before_client_use()
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(environment: "Production", realSecretMaterialAllowedNow: true));

        Assert.False(result.AccessAllowed);
        Assert.Equal("CredentialPolicyNotSafe", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Fact]
    public void Unsafe_scope_is_rejected_before_client_use()
    {
        var client = new FakeCredentialConfigClient();
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(externalCredentialAccessApproved: true),
            client);
        var scope = ValidScope() with
        {
            SafetyFlags = ValidScope().SafetyFlags with { ProductionAccountRequested = true }
        };

        var result = provider.AccessDemoReadOnlyConfig(scope, ValidPolicy(realSecretMaterialAllowedNow: true));

        Assert.False(result.AccessAllowed);
        Assert.Equal("SafetyConstraintFailed", result.SanitizedErrorCategory);
        Assert.Equal(0, client.AccessCalls);
    }

    [Fact]
    public void Sanitized_evidence_contains_no_secret_material()
    {
        var client = new FakeCredentialConfigClient(includeSensitiveMessage: true);
        var provider = new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
            ValidOptions(externalCredentialAccessApproved: true),
            client);

        var result = provider.AccessDemoReadOnlyConfig(ValidScope(), ValidPolicy(realSecretMaterialAllowedNow: true));

        var text = string.Join(" ", result.SanitizedStatus, result.SanitizedErrorCategory, result.SanitizedErrorMessage);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_public_surface_exposes_no_order_trading_or_replay_methods()
    {
        var methodNames = typeof(LmaxRealReadOnlyCredentialConfigBoundaryProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("TradeCapture", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replay", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlyCredentialConfigOptions ValidOptions(
        string environmentLabel = "Demo/read-only",
        bool demoReadOnly = true,
        string configSourceLabel = "local-approved-demo-readonly-config",
        bool externalCredentialAccessApproved = false)
        => new(environmentLabel, demoReadOnly, configSourceLabel, externalCredentialAccessApproved);

    private static LmaxReadOnlyCredentialAccessPolicy ValidPolicy(
        bool futureApprovedRuntimeAttemptRequired = true,
        bool realSecretMaterialAllowedNow = false,
        bool redactSensitiveFields = true,
        string environment = "Demo/read-only")
        => new(futureApprovedRuntimeAttemptRequired, realSecretMaterialAllowedNow, redactSensitiveFields, environment);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 22, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate))
            .Scope;

    private sealed class FakeCredentialConfigClient : ILmaxReadOnlyCredentialConfigClient
    {
        private readonly bool includeSensitiveMessage;

        public FakeCredentialConfigClient(bool includeSensitiveMessage = false)
        {
            this.includeSensitiveMessage = includeSensitiveMessage;
        }

        public int AccessCalls { get; private set; }
        public bool RealSecretMaterialLoaded { get; private set; }
        public bool SensitiveMaterialReturned { get; private set; }
        public bool SensitiveMaterialPrinted { get; private set; }
        public bool SensitiveMaterialStored { get; private set; }

        public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
            LmaxReadOnlyCredentialConfigOptions options,
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
                "FakeCredentialConfigAccessSanitized",
                null,
                includeSensitiveMessage ? "password=demo secret=demo credential=demo 554=demo sanitized." : null);
        }
    }
}
