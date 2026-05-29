using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionDryRunReportTests
{
    [Fact]
    public async Task Dry_run_report_for_future_manual_intent_is_blocked_and_no_network()
    {
        var generator = new LmaxReadOnlyExternalSessionDryRunReportGenerator();

        var report = await generator.GenerateAsync(ValidIntent() with
        {
            RunMode = LmaxReadOnlyExternalSessionRunIntentMode.FutureExternalReadOnlyManual
        }, DateTimeOffset.UtcNow);

        Assert.Equal(LmaxReadOnlyExternalSessionDryRunOutcome.Blocked, report.ExpectedOutcome);
        Assert.False(report.CanStartSession);
        Assert.False(report.SessionStarted);
        Assert.False(report.ExternalConnectionAttempted);
        Assert.False(report.CredentialReadAttempted);
        Assert.False(report.ShadowReplaySubmitAttempted);
        Assert.False(report.TradingMutationAttempted);
        Assert.True(report.NoSensitiveContent);
        Assert.Contains(report.SafetyGates, x => x.Name == "Phase4ExternalRunImplementationNotStarted" && x.BlocksRun);
        Assert.Contains(report.SafetyGates, x => x.Name == "CredentialResolverDisabled" && x.BlocksRun);
        Assert.Contains(report.SafetyGates, x => x.Name == "GuardedTransportImplementationDisabled" && x.BlocksRun);
        Assert.Contains(report.SafetyGates, x => x.Name == "ExternalSessionImplementationStarted" && x.BlocksRun);
        Assert.False(report.VenueProfile.IsActive);
        Assert.False(report.VenueProfile.IsExternalConnectionAllowed);
        Assert.False(report.CredentialProfile.CredentialReadImplemented);
        Assert.False(report.CredentialProfile.SensitiveMaterialReturned);
        Assert.False(report.GuardedTransport.NetworkTransportImplemented);
        Assert.False(report.GuardedTransport.SocketActivation);
        Assert.False(report.ExternalSessionSkeleton.SocketActivation);
    }

    [Theory]
    [InlineData("AllowExternalConnections", "ExternalConnectionBlocked")]
    [InlineData("AllowCredentialUse", "CredentialUseBlocked")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    public async Task Dry_run_report_surfaces_unsafe_flags(string condition, string expectedCode)
    {
        var generator = new LmaxReadOnlyExternalSessionDryRunReportGenerator();
        var intent = ValidIntent() with
        {
            AllowExternalConnections = condition == "AllowExternalConnections",
            AllowCredentialUse = condition == "AllowCredentialUse",
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            SchedulerEnabled = condition == "SchedulerEnabled",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay"
        };

        var report = await generator.GenerateAsync(intent, DateTimeOffset.UtcNow);

        Assert.Equal(LmaxReadOnlyExternalSessionDryRunOutcome.Blocked, report.ExpectedOutcome);
        Assert.Contains(report.SafetyGates, x => x.Name == expectedCode && x.BlocksRun);
    }

    [Fact]
    public void Dry_run_report_types_do_not_expose_forbidden_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExternalSessionDryRunReport),
            typeof(LmaxReadOnlyExternalSessionDryRunSection),
            typeof(LmaxReadOnlyExternalSessionVenueProfileSummary),
            typeof(LmaxReadOnlyExternalSessionCredentialProfileSummary),
            typeof(LmaxReadOnlyExternalSessionGuardedTransportSummary),
            typeof(LmaxReadOnlyExternalSessionSkeletonSummary)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            if (property.Name is "RequestedByOperatorId" or "CredentialProfileName" or "CredentialReadAttempted" or "CredentialReadImplemented" or "CredentialUseImplemented")
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
        }
    }

    [Fact]
    public async Task Dry_run_report_json_contains_no_sensitive_material_or_network_activation()
    {
        var report = await new LmaxReadOnlyExternalSessionDryRunReportGenerator().GenerateAsync(ValidIntent(), DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(report);

        foreach (var forbidden in new[] { "password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "554=", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SocketOpened\":true", "CredentialsUsed\":true" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyExternalSessionRunIntent ValidIntent()
        => new(
            Guid.NewGuid(),
            "manual dry-run report test",
            "local-admin",
            DateTimeOffset.UtcNow,
            "Demo",
            "DemoLondon",
            "LmaxDemoReadOnlyProfile",
            LmaxReadOnlyExternalSessionRunIntentMode.PreviewOnly);
}
