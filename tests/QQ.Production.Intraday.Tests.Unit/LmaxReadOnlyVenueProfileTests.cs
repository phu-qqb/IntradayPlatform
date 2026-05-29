using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyVenueProfileTests
{
    [Fact]
    public void Registry_returns_only_inactive_non_secret_descriptors()
    {
        var registry = new LmaxReadOnlyVenueProfileRegistryDisabled();

        var profiles = registry.ListProfiles();

        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, x => x.VenueProfileName == "DemoLondon");
        Assert.All(profiles, descriptor =>
        {
            Assert.False(descriptor.IsActive);
            Assert.False(descriptor.IsExternalConnectionAllowed);
            Assert.False(descriptor.IsCredentialUseAllowed);
            Assert.NotEqual(LmaxReadOnlyVenueProfileSafetyStatus.Disabled, descriptor.SafetyStatus);
            var json = JsonSerializer.Serialize(descriptor);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialValue", json, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Venue_profile_types_do_not_expose_endpoint_or_credential_value_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyVenueProfileName),
            typeof(LmaxReadOnlyVenueProfileDescriptor),
            typeof(LmaxReadOnlyVenueProfileValidationResult)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.DoesNotContain("host", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.Equals(property.Name, "Port", StringComparison.OrdinalIgnoreCase));
            Assert.False(property.Name.Contains("EndpointPort", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("user", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("account", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("senderComp", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("targetComp", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Demo_london_is_recognized_but_inactive()
    {
        var registry = new LmaxReadOnlyVenueProfileRegistryDisabled();

        var validation = registry.Validate("DemoLondon", "Demo");

        Assert.True(validation.IsKnown);
        Assert.True(validation.IsAllowedForPhase4);
        Assert.False(validation.HasErrors);
        Assert.NotNull(validation.Descriptor);
        Assert.False(validation.Descriptor.IsActive);
        Assert.False(validation.Descriptor.IsExternalConnectionAllowed);
        Assert.False(validation.Descriptor.IsCredentialUseAllowed);
        Assert.Equal(LmaxReadOnlyVenueProfileSafetyStatus.FuturePrototypeLabelOnly, validation.Descriptor.SafetyStatus);
    }

    [Theory]
    [InlineData("Production", "Production", "VenueProfileProductionBlocked")]
    [InlineData("Uat", "UAT", "VenueProfileUatBlocked")]
    [InlineData("UnknownFutureVenue", "Demo", "VenueProfileUnknown")]
    [InlineData("DemoLondon", "UAT", "VenueProfileEnvironmentMismatch")]
    public void Registry_blocks_unsafe_or_unknown_profiles(string venueProfileName, string environmentName, string expectedIssueCode)
    {
        var registry = new LmaxReadOnlyVenueProfileRegistryDisabled();

        var validation = registry.Validate(venueProfileName, environmentName);

        Assert.True(validation.HasErrors);
        Assert.False(validation.IsAllowedForPhase4);
        Assert.Contains(validation.Issues, x => x.Code == expectedIssueCode);
    }

    [Theory]
    [InlineData("UnknownFutureVenue", "Demo", "VenueProfileUnknown")]
    [InlineData("Production", "Production", "VenueProfileProductionBlocked")]
    [InlineData("Uat", "UAT", "VenueProfileUatBlocked")]
    [InlineData("DemoLondon", "UAT", "VenueProfileEnvironmentMismatch")]
    public void Options_validator_blocks_bad_venue_profiles(string venueProfileName, string environmentName, string expectedIssueCode)
    {
        var validation = LmaxReadOnlyExternalSessionOptionsValidator.Validate(new LmaxReadOnlyExternalSessionOptions
        {
            EnvironmentName = environmentName,
            VenueProfileName = venueProfileName
        });

        Assert.Contains(validation.Issues, x => x.Code == expectedIssueCode);
    }

    [Fact]
    public void Options_validator_accepts_demo_london_label_as_safe_disabled_metadata()
    {
        var validation = LmaxReadOnlyExternalSessionOptionsValidator.Validate(new LmaxReadOnlyExternalSessionOptions
        {
            EnvironmentName = "Demo",
            VenueProfileName = "DemoLondon"
        });

        Assert.False(validation.HasErrors);
        Assert.True(validation.IsSafeDisabled);
        Assert.Contains(validation.Issues, x => x.Code == "VenueProfileBoundaryPresent");
    }

    [Fact]
    public void Venue_profile_sample_config_contains_labels_only()
    {
        var samplePath = FindRepoFile("docs", "examples", "lmax-readonly-external-session-options.sample.json");
        var json = File.ReadAllText(samplePath);

        foreach (var forbidden in new[] { "host", "port", "username", "password", "secret", "token", "apiKey", "privateKey", "accountId", "senderComp", "targetComp" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(json);
        var section = document.RootElement.GetProperty("LmaxReadOnlyExternalSession");
        Assert.Equal("Demo", section.GetProperty("EnvironmentName").GetString());
        Assert.Equal("DemoLondon", section.GetProperty("VenueProfileName").GetString());
        Assert.Equal("LmaxDemoReadOnlyProfile", section.GetProperty("CredentialProfileName").GetString());
        Assert.False(section.GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(section.GetProperty("AllowCredentialUse").GetBoolean());
    }

    [Fact]
    public void Venue_profile_boundary_adds_no_network_order_or_gateway_surface()
    {
        var source = File.ReadAllText(FindRepoFile("src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyVenueProfile.cs"));

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
