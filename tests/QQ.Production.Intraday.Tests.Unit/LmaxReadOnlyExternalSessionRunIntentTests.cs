using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionRunIntentTests
{
    [Fact]
    public void Valid_looking_future_external_manual_intent_is_still_blocked()
    {
        var result = LmaxReadOnlyExternalSessionRunIntentValidator.Validate(ValidIntent() with
        {
            RunMode = LmaxReadOnlyExternalSessionRunIntentMode.FutureExternalReadOnlyManual
        });

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.True(result.IsBlocked);
        Assert.True(result.Summary.IsValidationOnly);
        Assert.Contains(result.Issues, x => x.Code == "Phase4ExternalRunImplementationNotStarted");
        Assert.Contains(result.Issues, x => x.Code == "CredentialResolverDisabled");
        Assert.False(result.Summary.SubmitToShadowReplay);
    }

    [Fact]
    public void Validate_only_intent_can_validate_without_starting_session()
    {
        var result = LmaxReadOnlyExternalSessionRunIntentValidator.Validate(ValidIntent() with
        {
            RunMode = LmaxReadOnlyExternalSessionRunIntentMode.ValidateOnly
        });

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.True(result.IsBlocked);
        Assert.True(result.Summary.IsValidationOnly);
        Assert.Contains(result.Issues, x => x.Code == "CredentialResolverDisabled");
        Assert.DoesNotContain(result.Issues, x => x.Code == "Phase4ExternalRunImplementationNotStarted");
    }

    [Theory]
    [InlineData("MissingReason", "ReasonRequired")]
    [InlineData("MissingOperator", "RequestedByOperatorIdRequired")]
    [InlineData("UnknownVenue", "VenueProfileUnknown")]
    [InlineData("VenueMismatch", "VenueProfileEnvironmentMismatch")]
    [InlineData("Uat", "VenueProfileUatBlocked")]
    [InlineData("Production", "VenueProfileProductionBlocked")]
    [InlineData("AllowExternalConnections", "ExternalConnectionBlocked")]
    [InlineData("AllowCredentialUse", "CredentialUseBlocked")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    [InlineData("InvalidRuntime", "MaxRuntimeSecondsOutOfRange")]
    [InlineData("TooLargeRuntime", "MaxRuntimeSecondsOutOfRange")]
    [InlineData("InvalidEvents", "MaxEventsPerRunOutOfRange")]
    [InlineData("TooLargeEvents", "MaxEventsPerRunOutOfRange")]
    public void Validator_blocks_unsafe_intent_conditions(string condition, string expectedCode)
    {
        var intent = ValidIntent() with
        {
            Reason = condition == "MissingReason" ? "" : "manual validation test",
            RequestedByOperatorId = condition == "MissingOperator" ? "" : "local-admin",
            EnvironmentName = condition is "VenueMismatch" or "Uat" ? "UAT" : condition == "Production" ? "Production" : "Demo",
            VenueProfileName = condition switch
            {
                "UnknownVenue" => "UnknownFutureVenue",
                "VenueMismatch" => "DemoLondon",
                "Uat" => "Uat",
                "Production" => "Production",
                _ => "DemoLondon"
            },
            AllowExternalConnections = condition == "AllowExternalConnections",
            AllowCredentialUse = condition == "AllowCredentialUse",
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            SchedulerEnabled = condition == "SchedulerEnabled",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay",
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

        var result = LmaxReadOnlyExternalSessionRunIntentValidator.Validate(intent);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Issues, x => x.Code == expectedCode);
    }

    [Fact]
    public void Credential_profile_name_is_label_only_and_does_not_resolve_credentials()
    {
        var result = LmaxReadOnlyExternalSessionRunIntentValidator.Validate(ValidIntent());

        Assert.Equal("LmaxDemoReadOnlyProfile", result.Summary.CredentialProfileName);
        Assert.Contains(result.Issues, x => x.Code == "CredentialResolverDisabled");
        Assert.DoesNotContain(result.Issues, x => x.Message.Contains("returned credential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Run_intent_types_do_not_expose_forbidden_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExternalSessionRunIntent),
            typeof(LmaxReadOnlyExternalSessionRunIntentSummary),
            typeof(LmaxReadOnlyExternalSessionRunIntentValidationResult)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            if (string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionRunIntent.RequestedByOperatorId), StringComparison.Ordinal)
                || string.Equals(property.Name, nameof(LmaxReadOnlyExternalSessionRunIntentSummary.RequestedByOperatorId), StringComparison.Ordinal))
            {
                continue;
            }

            Assert.DoesNotContain("host", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.Equals(property.Name, "Port", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("username", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("account", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("senderComp", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("targetComp", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("endpoint", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rawFix", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("newOrder", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cancel", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("replace", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Run_intent_boundary_adds_no_network_order_gateway_or_shadow_submit_implementation()
    {
        var source = File.ReadAllText(FindRepoFile("src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyExternalSessionRunIntent.cs"));

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
            "SubmitOrderAsync",
            "CancelOrderAsync",
            "ReplaceOrderAsync",
            "Lmax.ConnectivityLab"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyExternalSessionRunIntent ValidIntent()
        => new(
            Guid.NewGuid(),
            "manual validation test",
            "local-admin",
            DateTimeOffset.UtcNow,
            "Demo",
            "DemoLondon",
            "LmaxDemoReadOnlyProfile",
            LmaxReadOnlyExternalSessionRunIntentMode.PreviewOnly);

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
