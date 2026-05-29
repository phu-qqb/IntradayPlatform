using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionFakeTransportTests
{
    [Fact]
    public async Task Fake_transport_emits_deterministic_read_only_events()
    {
        var script = MixedScript();
        var transport = new LmaxReadOnlyExternalSessionFakeTransport();

        var result = await transport.ReadAsync(script, maxEvents: 10);

        Assert.Equal(script.ScriptId, result.ScriptId);
        Assert.False(result.MaxEventsReached);
        Assert.Equal(script.Messages.Select(x => x.MessageId), result.Events.Select(x => x.EventId));
        Assert.Equal(1, result.Counters.MarketDataSnapshotCount);
        Assert.Equal(1, result.Counters.TradeCaptureReportCount);
        Assert.Equal(1, result.Counters.OrderStatusReportCount);
        Assert.Equal(1, result.Counters.ProtocolRejectCount);
        Assert.Equal(1, result.Counters.SessionWarningCount);
        Assert.Equal(1, result.Counters.SessionErrorCount);
    }

    [Fact]
    public async Task Fake_transport_honors_max_event_cap()
    {
        var transport = new LmaxReadOnlyExternalSessionFakeTransport();

        var result = await transport.ReadAsync(MixedScript(), maxEvents: 2);

        Assert.True(result.MaxEventsReached);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal(2, result.Counters.TotalEventCount);
    }

    [Fact]
    public async Task Fake_transport_can_simulate_protocol_reject_warning_and_error()
    {
        var script = new LmaxReadOnlyExternalSessionFakeTransportScript(
            "reject-warning-error",
            [
                Message("reject", LmaxReadOnlyExternalSessionEventType.ProtocolReject),
                Message("warning", LmaxReadOnlyExternalSessionEventType.SessionWarning),
                Message("error", LmaxReadOnlyExternalSessionEventType.SessionError)
            ]);

        var result = await new LmaxReadOnlyExternalSessionFakeTransport().ReadAsync(script, maxEvents: 10);

        Assert.Equal(1, result.Counters.ProtocolRejectCount);
        Assert.Equal(1, result.Counters.SessionWarningCount);
        Assert.Equal(1, result.Counters.SessionErrorCount);
    }

    [Fact]
    public async Task Fake_session_returns_counters_and_never_creates_evidence_or_shadow_replay()
    {
        var session = new LmaxReadOnlyExternalSessionFake(FakeOptions(), MixedScript());

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("fake transport preview"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Completed, result.Status);
        Assert.True(result.ExternalSessionImplementationAvailable);
        Assert.False(result.SocketOpened);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
        Assert.Equal(6, result.Counters.TotalEventCount);
    }

    [Theory]
    [InlineData("MissingReason")]
    [InlineData("ExternalConnections")]
    [InlineData("CredentialUse")]
    [InlineData("OrderSubmission")]
    [InlineData("TradingPersistence")]
    [InlineData("Scheduler")]
    [InlineData("ShadowReplaySubmit")]
    [InlineData("BeyondAllowedActivation")]
    public async Task Fake_session_blocks_unsafe_conditions(string condition)
    {
        var options = FakeOptions() with
        {
            AllowExternalConnections = condition == "ExternalConnections",
            AllowCredentialUse = condition == "CredentialUse",
            AllowOrderSubmission = condition == "OrderSubmission",
            PersistToTradingTables = condition == "TradingPersistence",
            SchedulerEnabled = condition == "Scheduler",
            SubmitToShadowReplay = condition == "ShadowReplaySubmit",
            MaxAllowedActivationLevel = condition == "BeyondAllowedActivation"
                ? LmaxReadOnlyRuntimeActivationLevel.Level3LabExternalCaptureToFile
                : LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        };
        var reason = condition == "MissingReason" ? "" : "fake transport preview";
        var session = new LmaxReadOnlyExternalSessionFake(options, MixedScript());

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest(reason));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.False(result.SocketOpened);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
    }

    [Fact]
    public async Task Fake_session_blocks_when_max_events_is_invalid()
    {
        var session = new LmaxReadOnlyExternalSessionFake(FakeOptions(), MixedScript());

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("fake transport preview", MaxEvents: 0));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("MaxEventsPerRun", result.Safety.FailedGateNames);
    }

    [Fact]
    public void Fake_transport_dtos_do_not_expose_secret_shaped_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExternalSessionFakeTransportMessage),
            typeof(LmaxReadOnlyExternalSessionFakeTransportScript),
            typeof(LmaxReadOnlyExternalSessionFakeTransportResult)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialValue", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Fake_transport_has_no_order_submission_event_or_method_names()
    {
        var names = typeof(LmaxReadOnlyExternalSessionFakeTransport)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .Concat(Enum.GetNames<LmaxReadOnlyExternalSessionEventType>())
            .ToList();

        Assert.DoesNotContain(names, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fake_transport_does_not_reference_connectivity_lab()
    {
        var references = typeof(LmaxReadOnlyExternalSessionFakeTransport)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(references, x => x.Contains("ConnectivityLab", StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlyExternalSessionFakeTransportScript MixedScript()
        => new(
            "mixed-readonly-fake",
            [
                Message("md-1", LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot),
                Message("tc-1", LmaxReadOnlyExternalSessionEventType.TradeCaptureReport),
                Message("os-1", LmaxReadOnlyExternalSessionEventType.OrderStatusReport),
                Message("reject-1", LmaxReadOnlyExternalSessionEventType.ProtocolReject),
                Message("warn-1", LmaxReadOnlyExternalSessionEventType.SessionWarning),
                Message("err-1", LmaxReadOnlyExternalSessionEventType.SessionError)
            ]);

    private static LmaxReadOnlyExternalSessionFakeTransportMessage Message(string id, LmaxReadOnlyExternalSessionEventType eventType)
        => new(
            id,
            eventType,
            DateTimeOffset.Parse("2026-05-06T17:03:57Z", System.Globalization.CultureInfo.InvariantCulture),
            """{"sanitized":true}""",
            ClientOrderId: "CL-FAKE-1",
            BrokerOrderId: "BO-FAKE-1",
            BrokerExecutionId: eventType == LmaxReadOnlyExternalSessionEventType.TradeCaptureReport ? "EX-FAKE-1" : null,
            InstrumentId: "4001",
            Symbol: "EURUSD");

    private static LmaxReadOnlyRuntimeAdapterOptions FakeOptions()
        => new()
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FakeInMemory,
            AllowExternalConnections = false,
            AllowCredentialUse = false,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = false,
            SchedulerEnabled = false,
            EnvironmentName = "Local",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            LocalOnlyApi = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxEventsPerRun = 100,
            MaxRuntimeSeconds = 30
        };
}
