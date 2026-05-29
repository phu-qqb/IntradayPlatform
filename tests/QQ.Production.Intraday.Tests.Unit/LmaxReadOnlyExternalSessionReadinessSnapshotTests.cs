using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionReadinessSnapshotTests
{
    [Fact]
    public async Task Snapshot_for_valid_looking_external_intent_is_not_executable()
    {
        var generator = new LmaxReadOnlyExternalSessionReadinessSnapshotGenerator();
        var snapshot = await generator.GenerateAsync(Intent(), DateTimeOffset.UtcNow);

        Assert.Equal(LmaxReadOnlyExternalSessionReadinessSnapshotStatus.NotExecutable, snapshot.Status);
        Assert.Equal(LmaxReadOnlyExternalSessionReadinessSnapshotDecision.NotExecutable, snapshot.FinalDecision);
        Assert.False(snapshot.CanStartSession);
        Assert.False(snapshot.SessionStarted);
        Assert.False(snapshot.ExternalConnectionAttempted);
        Assert.False(snapshot.CredentialReadAttempted);
        Assert.False(snapshot.ShadowReplaySubmitAttempted);
        Assert.False(snapshot.TradingMutationAttempted);
        Assert.True(snapshot.NoSensitiveContent);
        Assert.Contains(snapshot.StableBlockers, x => x == "Phase4ExternalRunImplementationNotStarted");
        Assert.Contains(snapshot.StableBlockers, x => x == "CredentialResolverDisabled");
        Assert.Contains(snapshot.StableBlockers, x => x == "GuardedTransportImplementationDisabled");
        Assert.False(snapshot.DryRunReport.VenueProfile.IsActive);
        Assert.False(snapshot.DryRunReport.CredentialProfile.CredentialReadImplemented);
        Assert.False(snapshot.DryRunReport.GuardedTransport.NetworkTransportImplemented);
        Assert.False(snapshot.DryRunReport.ExternalSessionSkeleton.SocketActivation);
    }

    [Theory]
    [InlineData("external", "ExternalConnectionBlocked")]
    [InlineData("credentials", "CredentialUseBlocked")]
    [InlineData("orders", "OrderSubmissionForbidden")]
    [InlineData("scheduler", "SchedulerForbidden")]
    [InlineData("persistence", "TradingTablePersistenceForbidden")]
    [InlineData("replay", "ShadowReplaySubmitDeferred")]
    public async Task Snapshot_surfaces_unsafe_flags(string condition, string expectedCode)
    {
        var generator = new LmaxReadOnlyExternalSessionReadinessSnapshotGenerator();
        var snapshot = await generator.GenerateAsync(Intent(
            allowExternalConnections: condition == "external",
            allowCredentialUse: condition == "credentials",
            allowOrderSubmission: condition == "orders",
            schedulerEnabled: condition == "scheduler",
            persistToTradingTables: condition == "persistence",
            submitToShadowReplay: condition == "replay"), DateTimeOffset.UtcNow);

        Assert.False(snapshot.CanStartSession);
        Assert.Contains(snapshot.SafetyGates, x => x.Name == expectedCode && x.BlocksRun);
    }

    [Fact]
    public void Snapshot_types_do_not_expose_forbidden_fields_or_sensitive_json()
    {
        var names = typeof(LmaxReadOnlyExternalSessionReadinessSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();

        foreach (var forbidden in new[] { "Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix", "NewOrder", "Cancel", "Replace", "SubmitOrder" })
        {
            Assert.DoesNotContain(names, x => x.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                                               && !x.Contains("Report", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Snapshot_json_contains_no_sensitive_material()
    {
        var generator = new LmaxReadOnlyExternalSessionReadinessSnapshotGenerator();
        var json = JsonSerializer.Serialize(await generator.GenerateAsync(Intent(), DateTimeOffset.UtcNow));

        foreach (var forbidden in new[] { "password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "authorization", "554=", "endpointUrl", "rawFixText", "Connected=True", "OrderSent=True" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyExternalSessionRunIntent Intent(
        bool allowExternalConnections = false,
        bool allowCredentialUse = false,
        bool allowOrderSubmission = false,
        bool schedulerEnabled = false,
        bool persistToTradingTables = false,
        bool submitToShadowReplay = false)
        => new(
            Guid.NewGuid(),
            "Readiness snapshot validation only",
            "requesting-operator",
            DateTimeOffset.UtcNow,
            "Demo",
            "DemoLondon",
            "LmaxDemoReadOnlyProfile",
            LmaxReadOnlyExternalSessionRunIntentMode.FutureExternalReadOnlyManual,
            DryRun: true,
            MaxRuntimeSeconds: 30,
            MaxEventsPerRun: 100,
            RequestedEvidencePreviewOnly: true,
            submitToShadowReplay,
            allowExternalConnections,
            allowCredentialUse,
            allowOrderSubmission,
            schedulerEnabled,
            persistToTradingTables);
}
