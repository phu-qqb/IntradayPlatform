using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionOptionsTests
{
    [Fact]
    public void Default_config_validates_as_safe_disabled()
    {
        var result = LmaxReadOnlyExternalSessionOptionsValidator.Validate(new LmaxReadOnlyExternalSessionOptions());

        Assert.True(result.IsSafeDisabled);
        Assert.False(result.HasErrors);
        Assert.Equal(0, result.ErrorCount);
        Assert.Contains(result.Issues, x => x.Code == "SafeDisabled" && x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Info);
    }

    [Fact]
    public void Future_looking_enabled_config_remains_blocked_in_phase4g()
    {
        var result = LmaxReadOnlyExternalSessionOptionsValidator.Validate(FutureLookingOptions(), "manual phase 4g attempt");

        Assert.False(result.IsSafeDisabled);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, x => x.Code == "ExternalSessionImplementationNotStarted");
        Assert.Contains(result.Issues, x => x.Code == "ImplementationModeBlocked");
        Assert.Contains(result.Issues, x => x.Code == "ActivationLevelBlocked");
        Assert.Contains(result.Issues, x => x.Code == "ExternalConnectionBlocked");
        Assert.Contains(result.Issues, x => x.Code == "CredentialUseBlocked");
        Assert.Contains(result.Issues, x => x.Code == "CredentialResolverDisabled");
        Assert.Contains(result.Issues, x => x.Code == "CredentialProfileBoundaryPresent" && x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Info);
    }

    [Theory]
    [InlineData("NonDemoEnvironment", "EnvironmentMustBeDemo")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    [InlineData("PersistRawFixMessages", "RawFixPersistenceForbidden")]
    [InlineData("InvalidRuntime", "MaxRuntimeSecondsOutOfRange")]
    [InlineData("TooLargeRuntime", "MaxRuntimeSecondsOutOfRange")]
    [InlineData("InvalidEvents", "MaxEventsPerRunOutOfRange")]
    [InlineData("TooLargeEvents", "MaxEventsPerRunOutOfRange")]
    [InlineData("MissingReason", "ReasonRequired")]
    public void Validator_blocks_unsafe_options(string condition, string expectedCode)
    {
        var options = FutureLookingOptions() with
        {
            EnvironmentName = condition == "NonDemoEnvironment" ? "UAT" : "Demo",
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SchedulerEnabled = condition == "SchedulerEnabled",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay",
            PersistRawFixMessages = condition == "PersistRawFixMessages",
            MaxRuntimeSeconds = condition == "InvalidRuntime"
                ? 0
                : condition == "TooLargeRuntime"
                    ? LmaxReadOnlyExternalSessionOptions.SafeMaxRuntimeSeconds + 1
                    : 30,
            MaxEventsPerRun = condition == "InvalidEvents"
                ? 0
                : condition == "TooLargeEvents"
                    ? LmaxReadOnlyExternalSessionOptions.SafeMaxEventsPerRun + 1
                    : 100
        };
        var reason = condition == "MissingReason" ? "" : "manual phase 4g attempt";

        var result = LmaxReadOnlyExternalSessionOptionsValidator.Validate(options, reason);

        Assert.Contains(result.Issues, x => x.Code == expectedCode && x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
    }

    [Fact]
    public void External_session_options_do_not_expose_secret_shaped_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExternalSessionOptions),
            typeof(LmaxReadOnlyExternalSessionEnvironmentOptions),
            typeof(LmaxReadOnlyExternalSessionLimitsOptions),
            typeof(LmaxReadOnlyExternalSessionCredentialProfileOptions),
            typeof(LmaxReadOnlyExternalSessionConfigIssue),
            typeof(LmaxReadOnlyExternalSessionOptionsValidationResult)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
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
    public void Sample_config_contains_no_sensitive_placeholders_and_stays_disabled()
    {
        var repoRoot = FindRepoRoot();
        var samplePath = Path.Combine(repoRoot, "docs", "examples", "lmax-readonly-external-session-options.sample.json");
        var json = File.ReadAllText(samplePath);

        foreach (var forbidden in new[] { "password", "secret", "token", "apiKey", "privateKey", "authorization", "554=", "username", "host" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.GetProperty("LmaxReadOnlyExternalSession");
        Assert.False(root.GetProperty("Enabled").GetBoolean());
        Assert.Equal("DesignOnly", root.GetProperty("ImplementationMode").GetString());
        Assert.False(root.GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(root.GetProperty("AllowCredentialUse").GetBoolean());
        Assert.False(root.GetProperty("AllowOrderSubmission").GetBoolean());
        Assert.False(root.GetProperty("SubmitToShadowReplay").GetBoolean());
    }

    [Fact]
    public void Default_appsettings_remain_disabled_design_only()
    {
        var repoRoot = FindRepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "appsettings.json")));
        var root = document.RootElement.GetProperty("LmaxReadOnlyRuntime");

        Assert.False(root.GetProperty("Enabled").GetBoolean());
        Assert.Equal("DesignOnly", root.GetProperty("ImplementationMode").GetString());
        Assert.False(root.GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(root.GetProperty("AllowCredentialUse").GetBoolean());
        Assert.False(root.GetProperty("AllowOrderSubmission").GetBoolean());
        Assert.False(root.GetProperty("SchedulerEnabled").GetBoolean());
        Assert.False(root.GetProperty("SubmitToShadowReplay").GetBoolean());
    }

    private static LmaxReadOnlyExternalSessionOptions FutureLookingOptions()
        => new()
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
            RequireReason = true,
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            MaxRuntimeSeconds = 30,
            MaxEventsPerRun = 100
        };

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
}
