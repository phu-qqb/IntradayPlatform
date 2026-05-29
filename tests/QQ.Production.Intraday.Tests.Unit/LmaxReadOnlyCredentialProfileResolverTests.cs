using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyCredentialProfileResolverTests
{
    [Fact]
    public async Task Disabled_resolver_returns_blocked_status_and_no_credential_material()
    {
        var resolver = new LmaxReadOnlyCredentialProfileResolverDisabled();
        var request = new LmaxReadOnlyCredentialProfileRequest(
            "LmaxDemoReadOnlyProfile",
            "Demo",
            "LmaxDemoReadOnly",
            "phase 4h disabled resolver test");

        var status = await resolver.GetStatusAsync(request);
        var result = await resolver.ResolveAsync(request);

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, status.Status);
        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Equal("LmaxDemoReadOnlyProfile", result.Descriptor.CredentialProfileName);
        Assert.Equal("Demo", result.Descriptor.EnvironmentName);
        Assert.Equal("LmaxDemoReadOnly", result.Descriptor.VenueProfileName);
        Assert.False(result.Descriptor.IsConfigured);
        Assert.Equal(LmaxReadOnlyCredentialProfileSourceKind.None, result.Descriptor.SourceKind);
        Assert.Equal(LmaxReadOnlyCredentialProfileResolverMode.Disabled, result.Safety.ResolverMode);
        Assert.True(result.Safety.CredentialProfileBoundaryPresent);
        Assert.True(result.Safety.CredentialResolverDisabled);
        Assert.False(result.CredentialReadImplemented);
        Assert.False(result.CredentialUseImplemented);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.True(result.RedactionRequired);
        Assert.False(result.Safety.CredentialReadImplemented);
        Assert.False(result.Safety.CredentialUseImplemented);
        Assert.False(result.Safety.SensitiveMaterialReturned);
        Assert.True(result.Safety.RedactionRequired);
        Assert.Contains("CredentialResolverDisabled", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Disabled_resolver_does_not_return_environment_values()
    {
        const string variableName = "QQ_PHASE4H_TEST_PROFILE_VALUE";
        const string sensitiveSentinel = "phase4h-sensitive-value-never-return";
        Environment.SetEnvironmentVariable(variableName, sensitiveSentinel);
        try
        {
            var resolver = new LmaxReadOnlyCredentialProfileResolverDisabled();

            var result = await resolver.ResolveAsync(new LmaxReadOnlyCredentialProfileRequest(
                "LmaxDemoReadOnlyProfile",
                "Demo",
                "LmaxDemoReadOnly",
                "phase 4h environment non-read test"));

            var json = JsonSerializer.Serialize(result);
            Assert.DoesNotContain(sensitiveSentinel, json, StringComparison.Ordinal);
            Assert.DoesNotContain(variableName, json, StringComparison.Ordinal);
            Assert.False(result.CredentialReadImplemented);
            Assert.False(result.CredentialUseImplemented);
            Assert.False(result.SensitiveMaterialReturned);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public void Disabled_resolver_source_contains_no_secret_or_environment_read_implementation()
    {
        var source = File.ReadAllText(FindRepoFile("src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyCredentialProfile.cs"));

        foreach (var forbidden in new[]
        {
            "ConfigurationBuilder",
            "IConfiguration",
            "AddUserSecrets",
            "UserSecretsId",
            "KeyVault",
            "VaultClient"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Credential_profile_boundary_types_do_not_expose_credential_value_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyCredentialProfileRequest),
            typeof(LmaxReadOnlyCredentialProfileResult),
            typeof(LmaxReadOnlyCredentialProfileStatus),
            typeof(LmaxReadOnlyCredentialProfileDescriptor),
            typeof(LmaxReadOnlyCredentialProfileSafetyReport)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            if (string.Equals(property.Name, nameof(LmaxReadOnlyCredentialProfileRequest.CredentialProfileName), StringComparison.Ordinal)
                || string.Equals(property.Name, nameof(LmaxReadOnlyCredentialProfileDescriptor.CredentialProfileName), StringComparison.Ordinal))
            {
                continue;
            }

            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialValue", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Credential_profile_name_is_a_label_only_and_still_blocks_future_activation()
    {
        var options = new LmaxReadOnlyExternalSessionOptions
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            ActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            EnvironmentName = "Demo",
            VenueProfileName = "LmaxDemoReadOnly",
            CredentialProfileName = "LmaxDemoReadOnlyProfile",
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SchedulerEnabled = false,
            SubmitToShadowReplay = false,
            DryRun = true,
            MaxRuntimeSeconds = 30,
            MaxEventsPerRun = 100
        };

        var validation = LmaxReadOnlyExternalSessionOptionsValidator.Validate(options, "phase 4h future activation test");

        Assert.True(validation.HasErrors);
        Assert.Contains(validation.Issues, x => x.Code == "CredentialUseBlocked");
        Assert.Contains(validation.Issues, x => x.Code == "CredentialResolverDisabled");
        Assert.DoesNotContain(validation.Issues, x => x.Code == "CredentialProfileNameRequired");
    }

    [Fact]
    public void Missing_credential_profile_name_blocks_future_activation()
    {
        var validation = LmaxReadOnlyExternalSessionOptionsValidator.Validate(
            new LmaxReadOnlyExternalSessionOptions
            {
                Enabled = true,
                CredentialProfileName = "",
                AllowCredentialUse = true
            },
            "phase 4h missing profile label test");

        Assert.Contains(validation.Issues, x => x.Code == "CredentialProfileNameRequired");
        Assert.Contains(validation.Issues, x => x.Code == "CredentialResolverDisabled");
    }

    [Fact]
    public void Credential_profile_boundary_adds_no_network_or_order_submission_surface()
    {
        var source = File.ReadAllText(FindRepoFile("src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyCredentialProfile.cs"));

        foreach (var forbidden in new[]
        {
            "TcpClient",
            "Socket(",
            "SslStream",
            "QuickFIX",
            "ClientWebSocket",
            "HttpClient",
            "NetworkStream",
            "NewOrderSingle",
            "OrderCancelRequest",
            "OrderCancelReplaceRequest",
            "SubmitOrder",
            "CancelOrder",
            "ReplaceOrder",
            "Lmax.ConnectivityLab"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Environment_availability_resolver_reports_missing_labels_without_values()
    {
        var resolver = new LmaxReadOnlyCredentialProfileResolverEnvironment(_ => null);

        var result = resolver.CheckAvailability(new LmaxReadOnlyCredentialProfileRequest(
            "LmaxDemoReadOnlyProfile",
            "Demo",
            "DemoLondon",
            "phase 5c missing availability"));

        Assert.True(result.CredentialReadAttempted);
        Assert.False(result.IsConfigured);
        Assert.False(result.CredentialValuesReturned);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.Equal(LmaxReadOnlyCredentialProfileSourceKind.Environment, result.SourceKind);
        Assert.Equal(LmaxReadOnlyCredentialProfileRedactionStatus.Redacted, result.RedactionStatus);
        Assert.Equal(LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels.Count, result.MissingKeyLabels.Count);
        Assert.All(result.KeyStatuses, x => Assert.False(x.IsPresent));
    }

    [Fact]
    public void Environment_availability_resolver_reports_present_labels_without_returning_sentinel_values()
    {
        var values = LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels
            .ToDictionary(x => x, x => "phase5c-sentinel-" + x);
        var resolver = new LmaxReadOnlyCredentialProfileResolverEnvironment(label => values.TryGetValue(label, out var value) ? value : null);

        var result = resolver.CheckAvailability(new LmaxReadOnlyCredentialProfileRequest(
            "LmaxDemoReadOnlyProfile",
            "Demo",
            "DemoLondon",
            "phase 5c present availability"));
        var json = JsonSerializer.Serialize(result);

        Assert.True(result.CredentialReadAttempted);
        Assert.True(result.IsConfigured);
        Assert.Empty(result.MissingKeyLabels);
        Assert.False(result.CredentialValuesReturned);
        Assert.False(result.SensitiveMaterialReturned);
        Assert.All(result.KeyStatuses, x => Assert.True(x.IsPresent));
        foreach (var sentinel in values.Values)
        {
            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Redaction_policy_removes_secret_like_values_from_strings()
    {
        Environment.SetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD", "phase5c-env-password");
        try
        {
            var raw = "{\"password\":\"phase5c-json-password\",\"token\":\"phase5c-token\",\"note\":\"phase5c-env-password\"}";

            var redacted = LmaxReadOnlyCredentialRedactionPolicy.Redact(raw);

            Assert.DoesNotContain("phase5c-json-password", redacted, StringComparison.Ordinal);
            Assert.DoesNotContain("phase5c-token", redacted, StringComparison.Ordinal);
            Assert.DoesNotContain("phase5c-env-password", redacted, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD", null);
        }
    }

    private static string FindRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate repo file.", Path.Combine(segments));
    }
}
