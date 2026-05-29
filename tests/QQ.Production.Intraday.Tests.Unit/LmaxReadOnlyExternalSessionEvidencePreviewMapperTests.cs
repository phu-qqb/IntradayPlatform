using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionEvidencePreviewMapperTests
{
    [Theory]
    [InlineData("empty", "EmptyReadOnly", 0, 0, 0, 0, 0)]
    [InlineData("market", "MarketDataOnly", 1, 0, 0, 0, 0)]
    [InlineData("trade", "TradeCaptureOnly", 0, 1, 0, 0, 0)]
    [InlineData("status", "OrderStatusOnly", 0, 0, 1, 0, 0)]
    [InlineData("reject", "ProtocolRejectOnly", 0, 0, 0, 1, 0)]
    [InlineData("mixed", "MixedReadOnly", 1, 1, 1, 1, 0)]
    public void Fake_transport_events_map_to_valid_evidence_modes(string scenario, string expectedMode, int marketData, int tradeCapture, int orderStatus, int protocolReject, int executionReports)
    {
        var result = new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(Scenario(scenario));

        Assert.Equal(expectedMode, result.EvidenceMode);
        Assert.Equal(0, result.ValidationErrorCount);
        Assert.True(result.NoSensitiveContent);
        Assert.Equal(marketData, result.Batch.MarketDataSnapshotCount);
        Assert.Equal(tradeCapture, result.Batch.TradeCaptureReportCount);
        Assert.Equal(orderStatus, result.Batch.OrderStatusCount);
        Assert.Equal(protocolReject, result.Batch.ProtocolRejectCount);
        Assert.Equal(executionReports, result.Batch.ExecutionReportCount);
        AssertFixtureValidatorAccepts(result.NormalizedEvidenceJson, expectedMode);
    }

    [Fact]
    public void Session_warning_and_error_become_warnings_not_replay_events()
    {
        var result = new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(
            [
                Event("warn-1", LmaxReadOnlyExternalSessionEventType.SessionWarning, """{"message":"heartbeat late"}"""),
                Event("error-1", LmaxReadOnlyExternalSessionEventType.SessionError, """{"message":"fake disconnect"}""")
            ]);
        var root = JsonNode.Parse(result.NormalizedEvidenceJson)!.AsObject();

        Assert.Equal("EmptyReadOnly", result.EvidenceMode);
        Assert.Empty(root["orderStatuses"]!.AsArray());
        Assert.Empty(root["tradeCaptureReports"]!.AsArray());
        Assert.Empty(root["protocolRejects"]!.AsArray());
        Assert.Equal(2, root["warnings"]!.AsArray().Count);
        Assert.Contains(result.Issues, x => x.Code == "SessionErrorCapturedAsWarning");
    }

    [Fact]
    public void Trade_capture_preview_normalizes_trade_date_side_and_explicit_null_trade_uti()
    {
        var result = new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(Scenario("trade"));
        var trade = JsonNode.Parse(result.NormalizedEvidenceJson)!["tradeCaptureReports"]![0]!.AsObject();

        Assert.Equal("2026-05-06", trade["tradeDate"]!.GetValue<string>());
        Assert.Equal("Buy", trade["side"]!.GetValue<string>());
        Assert.True(trade.ContainsKey("tradeUti"));
        Assert.Null(trade["tradeUti"]);
    }

    [Fact]
    public void Single_item_arrays_remain_arrays()
    {
        var root = JsonNode.Parse(new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(Scenario("trade")).NormalizedEvidenceJson)!.AsObject();

        Assert.IsType<JsonArray>(root["executionReports"]);
        Assert.IsType<JsonArray>(root["orderStatuses"]);
        Assert.IsType<JsonArray>(root["tradeCaptureReports"]);
        Assert.IsType<JsonArray>(root["protocolRejects"]);
        Assert.Single(root["tradeCaptureReports"]!.AsArray());
    }

    [Fact]
    public async Task Fake_session_can_return_local_evidence_preview_without_shadow_replay_submit()
    {
        var session = new LmaxReadOnlyExternalSessionFake(FakeOptions(), new LmaxReadOnlyExternalSessionFakeTransportScript("mixed", Scenario("mixed").Select(ToMessage).ToList()));

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("preview evidence", PreviewEvidence: true));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Completed, result.Status);
        Assert.True(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
        Assert.NotNull(result.EvidencePreview);
        Assert.Equal("MixedReadOnly", result.EvidencePreview.EvidenceMode);
        AssertFixtureValidatorAccepts(result.EvidencePreview.NormalizedEvidenceJson, "MixedReadOnly");
    }

    [Fact]
    public async Task Max_events_cap_is_applied_before_mapping()
    {
        var session = new LmaxReadOnlyExternalSessionFake(FakeOptions(), new LmaxReadOnlyExternalSessionFakeTransportScript("mixed", Scenario("mixed").Select(ToMessage).ToList()));

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("preview capped evidence", MaxEvents: 1, PreviewEvidence: true));

        Assert.Equal(1, result.Counters.TotalEventCount);
        Assert.NotNull(result.EvidencePreview);
        Assert.Equal(1, result.EvidencePreview.InputEventCount);
        Assert.Equal("MarketDataOnly", result.EvidencePreview.EvidenceMode);
    }

    [Fact]
    public void Preview_json_contains_no_sensitive_or_order_command_content()
    {
        var json = new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(Scenario("mixed")).NormalizedEvidenceJson;

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NewOrderSingle", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OrderCancel", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CancelReplace", json, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<LmaxReadOnlyExternalSessionEvent> Scenario(string scenario)
        => scenario switch
        {
            "empty" => [],
            "market" => [Market()],
            "trade" => [Trade()],
            "status" => [Status()],
            "reject" => [Reject()],
            "mixed" => [Market(), Trade(), Status(), Reject()],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

    private static LmaxReadOnlyExternalSessionEvent Market()
        => Event("md-1", LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot, """{"bestBid":1.17370,"bestAsk":1.17376,"bidSize":75,"askSize":125}""");

    private static LmaxReadOnlyExternalSessionEvent Trade()
        => Event("tc-1", LmaxReadOnlyExternalSessionEventType.TradeCaptureReport, """{"side":"1","lastQty":0.1,"lastPx":1.17372,"tradeDate":"20260506","transactTimeUtc":"2026-05-06T17:05:00Z","securityIdSource":"8","tradeReportId":"TR-FAKE-1"}""", brokerExecutionId: "EX-FAKE-1");

    private static LmaxReadOnlyExternalSessionEvent Status()
        => Event("os-1", LmaxReadOnlyExternalSessionEventType.OrderStatusReport, """{"orderStatus":"Filled","cumQty":0.1,"leavesQty":0,"execId":"status-fake-1","executionType":"OrderStatus","securityIdSource":"8"}""");

    private static LmaxReadOnlyExternalSessionEvent Reject()
        => Event("reject-1", LmaxReadOnlyExternalSessionEventType.ProtocolReject, """{"refMsgType":"AD","refSeqNum":"4","rejectContext":"ReadOnlyRecoveryRequest","message":"Synthetic read-only reject"}""");

    private static LmaxReadOnlyExternalSessionEvent Event(string id, LmaxReadOnlyExternalSessionEventType eventType, string payload, string? brokerExecutionId = null)
        => new(
            id,
            eventType,
            DateTimeOffset.Parse("2026-05-06T17:05:00Z", System.Globalization.CultureInfo.InvariantCulture),
            payload,
            ClientOrderId: "CO-FAKE-1",
            BrokerOrderId: "BO-FAKE-1",
            BrokerExecutionId: brokerExecutionId,
            InstrumentId: "4001",
            Symbol: "EURUSD");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage ToMessage(LmaxReadOnlyExternalSessionEvent item)
        => new(
            item.EventId,
            item.EventType,
            item.ObservedAtUtc,
            item.SanitizedPayloadJson,
            item.ClientOrderId,
            item.BrokerOrderId,
            item.BrokerExecutionId,
            item.InstrumentId,
            item.Symbol);

    private static void AssertFixtureValidatorAccepts(string json, string expectedMode)
    {
        var path = Path.Combine(Path.GetTempPath(), "lmax-preview-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, json);
            var preview = LmaxReadOnlyRuntimeAdapterFakeInMemory.PreviewFixtureEvidence(path);

            Assert.Equal(0, preview.ErrorCount);
            Assert.Equal(expectedMode, preview.Batch.EvidenceMode);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

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
