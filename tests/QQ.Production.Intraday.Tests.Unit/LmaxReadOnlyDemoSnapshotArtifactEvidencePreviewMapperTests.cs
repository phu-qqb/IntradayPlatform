using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapperTests
{
    [Fact]
    public void Successful_artifact_maps_to_market_data_only_evidence_preview()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(SuccessArtifactJson());
        var root = JsonNode.Parse(result.NormalizedEvidenceJson)!.AsObject();

        Assert.True(result.IsValid);
        Assert.Equal("lmax-fix-lifecycle-evidence-v1", result.SchemaVersion);
        Assert.Equal("MarketDataOnly", result.EvidenceMode);
        Assert.Equal("RuntimeDemoReadOnlySnapshotArtifact", root["source"]!.GetValue<string>());
        Assert.Equal("RuntimeDemoReadOnlySnapshotPreview", root["captureMode"]!.GetValue<string>());
        Assert.True(root["noSensitiveContent"]!.GetValue<bool>());
        Assert.Equal("Redacted", root["redactionStatus"]!.GetValue<string>());
        Assert.Equal("Demo", root["environment"]!.GetValue<string>());
        Assert.Equal("EURUSD", root["instrumentSymbol"]!.GetValue<string>());
        Assert.Equal("4001", root["securityId"]!.GetValue<string>());
        Assert.False(root["shadowReplaySubmitAttempted"]!.GetValue<bool>());
        Assert.False(root["tradingMutationAttempted"]!.GetValue<bool>());
        Assert.False(root["orderSubmissionAttempted"]!.GetValue<bool>());
    }

    [Fact]
    public void Mapped_preview_contains_market_data_values_and_empty_replay_arrays()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(SuccessArtifactJson());
        var root = JsonNode.Parse(result.NormalizedEvidenceJson)!.AsObject();
        var marketData = root["marketData"]!.AsObject();

        Assert.True(marketData["snapshotReceived"]!.GetValue<bool>());
        Assert.Equal(1.17662m, marketData["bestBid"]!.GetValue<decimal>());
        Assert.Equal(1.17667m, marketData["bestAsk"]!.GetValue<decimal>());
        Assert.Equal(1.176645m, marketData["mid"]!.GetValue<decimal>());
        Assert.Equal(2, marketData["entryCount"]!.GetValue<int>());
        Assert.Equal(2, marketData["entries"]!.AsArray().Count);
        Assert.Empty(root["executionReports"]!.AsArray());
        Assert.Empty(root["orderStatuses"]!.AsArray());
        Assert.Empty(root["tradeCaptureReports"]!.AsArray());
        Assert.Empty(root["protocolRejects"]!.AsArray());
    }

    [Fact]
    public void Existing_fixture_validator_accepts_mapped_preview()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(SuccessArtifactJson());
        var path = Path.Combine(Path.GetTempPath(), "lmax-demo-snapshot-preview-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, result.NormalizedEvidenceJson);

            var preview = LmaxReadOnlyRuntimeAdapterFakeInMemory.PreviewFixtureEvidence(path);

            Assert.Equal(0, preview.ErrorCount);
            Assert.Equal("MarketDataOnly", preview.Batch.EvidenceMode);
            Assert.Equal(1, preview.Batch.MarketDataSnapshotCount);
            Assert.Equal(0, preview.Batch.ExecutionReportCount);
            Assert.Equal(0, preview.Batch.OrderStatusCount);
            Assert.Equal(0, preview.Batch.TradeCaptureReportCount);
            Assert.Equal(0, preview.Batch.ProtocolRejectCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Unsafe_credential_values_returned_artifact_fails_before_mapping()
    {
        var unsafeJson = Mutate(SuccessArtifactJson(), root => root["credentialValuesReturned"] = true);

        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(unsafeJson);

        Assert.False(result.IsValid);
        Assert.Empty(result.NormalizedEvidenceJson);
        Assert.Contains(result.ArtifactIssues, x => x.Code == "UnexpectedBooleanFlag" && x.Path == "$.credentialValuesReturned");
    }

    [Fact]
    public void Unsafe_order_submission_artifact_fails_before_mapping()
    {
        var unsafeJson = Mutate(SuccessArtifactJson(), root => root["orderSubmissionAttempted"] = true);

        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(unsafeJson);

        Assert.False(result.IsValid);
        Assert.Empty(result.NormalizedEvidenceJson);
        Assert.Contains(result.ArtifactIssues, x => x.Code == "UnexpectedBooleanFlag" && x.Path == "$.orderSubmissionAttempted");
    }

    [Fact]
    public void Snapshot_not_received_does_not_map_as_successful_market_data_preview()
    {
        var unsafeJson = Mutate(SuccessArtifactJson(), root =>
        {
            root["status"] = "FailedSafeSnapshotTimeout";
            root["snapshotReceived"] = false;
            root["marketDataSnapshotReceived"] = false;
            root.Remove("bestBid");
            root.Remove("bestAsk");
            root.Remove("mid");
        });

        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(unsafeJson);

        Assert.False(result.IsValid);
        Assert.Empty(result.NormalizedEvidenceJson);
        Assert.Contains(result.ArtifactIssues, x => x.Code == "StatusNotSuccessful");
    }

    [Fact]
    public void Mapped_preview_contains_no_sentinel_or_credential_values()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(
            SuccessArtifactJson(),
            forbiddenSensitiveValues: ["SENTINEL_USER", "SENTINEL_PASSWORD", "SENTINEL_SENDER", "SENTINEL_TARGET"]);

        Assert.True(result.IsValid);
        Assert.DoesNotContain("SENTINEL_USER", result.NormalizedEvidenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_PASSWORD", result.NormalizedEvidenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_SENDER", result.NormalizedEvidenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_TARGET", result.NormalizedEvidenceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("554=", result.NormalizedEvidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rawFix", result.NormalizedEvidenceJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_book_gbpusd_artifact_maps_to_market_data_only_preview_with_warning()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapEmptyBookJson(EmptyBookArtifactJson());
        var root = JsonNode.Parse(result.NormalizedEvidenceJson)!.AsObject();
        var marketData = root["marketData"]!.AsObject();

        Assert.True(result.IsValid);
        Assert.Equal("MarketDataOnly", result.EvidenceMode);
        Assert.Equal("GBPUSD", root["instrumentSymbol"]!.GetValue<string>());
        Assert.Equal("GBP/USD", root["slashSymbol"]!.GetValue<string>());
        Assert.Equal("4002", root["securityId"]!.GetValue<string>());
        Assert.Equal("EmptyBook", marketData["status"]!.GetValue<string>());
        Assert.True(marketData["snapshotReceived"]!.GetValue<bool>());
        Assert.Equal(0, marketData["entryCount"]!.GetValue<int>());
        Assert.Empty(marketData["entries"]!.AsArray());
        Assert.Empty(root["executionReports"]!.AsArray());
        Assert.Empty(root["orderStatuses"]!.AsArray());
        Assert.Empty(root["tradeCaptureReports"]!.AsArray());
        Assert.Empty(root["protocolRejects"]!.AsArray());
        Assert.Contains("no entries", root["warnings"]!.AsArray()[0]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.False(root["shadowReplaySubmitAttempted"]!.GetValue<bool>());
        Assert.False(root["tradingMutationAttempted"]!.GetValue<bool>());
        Assert.False(root["orderSubmissionAttempted"]!.GetValue<bool>());
    }

    [Fact]
    public void Phase5m_mapper_does_not_submit_replay_or_mutate_trading_state()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(SuccessArtifactJson());
        var root = JsonNode.Parse(result.NormalizedEvidenceJson)!.AsObject();

        Assert.True(result.IsValid);
        Assert.False(root["shadowReplaySubmitAttempted"]!.GetValue<bool>());
        Assert.False(root["tradingMutationAttempted"]!.GetValue<bool>());
        Assert.False(root["orderSubmissionAttempted"]!.GetValue<bool>());
        Assert.Equal(0, result.Batch.ExecutionReportCount);
        Assert.Equal(0, result.Batch.OrderStatusCount);
        Assert.Equal(0, result.Batch.TradeCaptureReportCount);
        Assert.Equal(0, result.Batch.ProtocolRejectCount);
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }

    private static string SuccessArtifactJson()
        => """
           {
             "runId": "phase5m-test-run",
             "startedAtUtc": "2026-05-08T11:26:46.4132214+00:00",
             "completedAtUtc": "2026-05-08T11:26:46.7030664+00:00",
             "status": "Completed",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "credentialProfileName": "LmaxDemoReadOnlyProfile",
             "reason": "Phase 5M unit test sanitized snapshot",
             "operatorId": "local-operator",
             "externalConnectionAttempted": true,
             "credentialReadAttempted": true,
             "credentialValuesReturned": false,
             "logonAttempted": true,
             "logonSucceeded": true,
             "snapshotRequestAttempted": true,
             "snapshotReceived": true,
             "logoutAttempted": true,
             "logoutSucceeded": true,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "entryCount": 2,
             "marketDataSnapshotReceived": true,
             "instrument": "EURUSD",
             "securityId": "4001",
             "bestBid": 1.17662,
             "bestAsk": 1.17667,
             "mid": 1.176645,
             "snapshotReceivedAtUtc": "2026-05-08T11:26:46.7000639+00:00",
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "warnings": [],
             "errors": [],
             "retryEnabled": false,
             "retryAllowed": false
           }
           """;

    private static string EmptyBookArtifactJson()
        => """
           {
             "runId": "phase6x-empty-book-test-run",
             "startedAtUtc": "2026-05-09T17:12:32.5767668+00:00",
             "completedAtUtc": "2026-05-09T17:12:34.3306726+00:00",
             "status": "CompletedWithEmptyBook",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "credentialProfileName": "LmaxDemoReadOnlyProfile",
             "reason": "Phase 6X unit test sanitized empty-book snapshot",
             "operatorId": "local-operator",
             "externalConnectionAttempted": true,
             "credentialReadAttempted": true,
             "credentialValuesReturned": false,
             "logonAttempted": true,
             "logonSucceeded": true,
             "snapshotRequestAttempted": true,
             "snapshotReceived": true,
             "logoutAttempted": true,
             "logoutSucceeded": true,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "entryCount": 0,
             "marketDataSnapshotReceived": true,
             "instrument": "GBPUSD",
             "symbol": "GBPUSD",
             "slashSymbol": "GBP/USD",
             "securityId": "4002",
             "securityIdSource": "8",
             "requestMode": "SnapshotPlusUpdates",
             "symbolEncodingMode": "SecurityIdOnly",
             "marketDepth": 1,
             "bestBid": null,
             "bestAsk": null,
             "mid": null,
             "snapshotReceivedAtUtc": "2026-05-09T17:12:34.3266407+00:00",
             "sourceFinalReadinessFile": "final-readiness.json",
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "diagnostics": {
               "request": { "waitDurationMs": 102 },
               "messageCounters": {
                 "marketDataSnapshot": 1,
                 "marketDataRequestReject": 0,
                 "businessMessageReject": 0,
                 "reject": 0
               },
               "responseClassification": "CompletedWithEmptyBook",
               "sessionWarnings": [ "Market data snapshot was received with no entries." ],
               "sessionErrors": []
             },
             "warnings": [ "Market data snapshot was received with no entries." ],
             "errors": [],
             "retryEnabled": false,
             "retryAllowed": false
           }
           """;
}
