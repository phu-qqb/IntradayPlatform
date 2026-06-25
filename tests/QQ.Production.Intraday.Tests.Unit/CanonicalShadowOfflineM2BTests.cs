using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalShadowOfflineM2BTests
{
    [Fact]
    public void T01_manager_fixture_maps_to_existing_model_weight_batch_and_model_run()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.Equal("M2B-INTRADAY-BATCH-20260625-0800", contracts.Batch.ExternalBatchId);
        Assert.Equal(ModelWeightBatchStatus.Accepted, contracts.Batch.Status);
        Assert.Equal(ModelRunStatus.Processed, contracts.ModelRun.Status);
        Assert.Equal(TargetQuantityMode.PortfolioBaseCurrencyNotional, contracts.ModelRun.TargetQuantityMode);
    }

    [Fact]
    public void T02_weights_are_existing_target_weight_contracts_not_new_truth()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.All(contracts.TargetWeights, x => Assert.IsType<TargetWeight>(x));
        Assert.Equal(contracts.SourceFixture.Weights.Select(x => x.Weight), contracts.TargetWeights.Select(x => x.Weight));
    }

    [Fact]
    public void T03_aum_price_contract_size_and_rounding_create_tradeable_target_positions()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.All(contracts.TargetPositions, x => Assert.NotEqual(0m, x.TargetVenueQuantity));
        Assert.All(contracts.TargetPositions, x => Assert.Equal(0m, Math.Abs(x.TargetVenueQuantity * 10m) % 1m));
    }

    [Fact]
    public void T04_trade_intents_are_deltas_from_existing_position_context()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.Equal(contracts.TargetPositions.Count, contracts.TradeIntents.Count);
        Assert.Contains(contracts.TradeIntents, x => x.Side == TradeSide.Buy);
        Assert.Contains(contracts.TradeIntents, x => x.Side == TradeSide.Sell);
    }

    [Fact]
    public void T05_missing_fund_blocks_before_recording()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture() with { FundId = "" };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture));
    }

    [Fact]
    public void T06_missing_strategy_blocks_before_recording()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture() with { StrategyId = "" };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture));
    }

    [Fact]
    public void T07_missing_book_blocks_before_recording()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture() with { BookId = "" };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture));
    }

    [Fact]
    public void T08_missing_nav_blocks_before_recording()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture() with { NavUsd = 0m };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture));
    }

    [Fact]
    public void T09_invalid_target_time_contract_blocks()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture();
        fixture = fixture with { EffectiveFromUtc = fixture.DecisionTimeUtc.AddMinutes(-1) };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture));
    }

    [Fact]
    public void T10_source_lineage_ids_sequences_and_payload_hashes_are_present()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.Contains("batch", contracts.SourceEventIds.Keys);
        Assert.Contains("target-weight:EURUSD", contracts.SourceEventIds.Keys);
        Assert.True(contracts.SourceEventSequences.Values.Distinct().Count() == contracts.SourceEventSequences.Count);
        Assert.All(contracts.SourcePayloadHashes.Values, x => Assert.Equal(64, x.Length));
    }

    [Fact]
    public void T11_daily_and_intraday_same_symbol_remain_distinct_strategy_book_scope()
    {
        var intraday = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var daily = CanonicalShadowOfflineFixtures.DailyFixture();
        var intradayEvent = CanonicalDomainEventMapper.MapIntraday(intraday).First(x => x.EventType == "TARGET_WEIGHT_OBSERVED" && x.Symbol == "EURUSD");
        var dailyEvent = CanonicalDomainEventMapper.MapDaily(daily);
        Assert.Equal("INTRADAY", intradayEvent.StrategyId);
        Assert.Equal("DAILY", dailyEvent.StrategyId);
        Assert.Equal("EURUSD", intradayEvent.Symbol);
        Assert.Equal("EURUSD", dailyEvent.Symbol);
        Assert.NotEqual(intradayEvent.BookId, dailyEvent.BookId);
    }

    [Fact]
    public void T12_intraday_mapping_emits_parent_and_child_shadow_intents_without_gateway_order()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var events = CanonicalDomainEventMapper.MapIntraday(contracts).ToArray();
        Assert.Contains(events, x => x.EventType == "SHADOW_PARENT_INTENT");
        Assert.Contains(events, x => x.EventType == "SHADOW_CHILD_INTENT");
        Assert.All(events.Where(x => x.EventType.Contains("INTENT", StringComparison.Ordinal)), x => Assert.DoesNotContain("Broker", JsonSerializer.Serialize(x.Payload)));
    }

    [Fact]
    public void T13_market_data_event_can_be_shared_without_fund_strategy_book_scope()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var evt = CanonicalDomainEventMapper.MapMarketData(contracts, 0);
        Assert.Equal("BBO_UPDATED", evt.EventType);
        Assert.Null(evt.StrategyId);
        Assert.True(evt.BookValid);
    }

    [Fact]
    public void T14_risk_decision_is_observed_not_executed()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var evt = CanonicalDomainEventMapper.MapIntraday(contracts).Single(x => x.EventType == "RISK_DECISION_OBSERVED" && x.Symbol == "EURUSD");
        Assert.Equal("RiskDecision", evt.SourceContract);
        Assert.Contains("offline shadow", JsonSerializer.Serialize(evt.Payload));
    }

    [Fact]
    public async Task T15_offline_host_records_full_vertical_slice_and_replay_passes()
    {
        var result = await RunHostAsync();
        Assert.Equal("PASS", result.Status);
        Assert.Equal("PASS", result.ReplayReport.Status);
        Assert.True(result.DataQualityReport.ShadowReady);
        Assert.Equal("PASS", result.ParityReport.Status);
    }

    [Fact]
    public async Task T16_sequence_contains_expected_intraday_lifecycle_events()
    {
        var result = await RunHostAsync();
        var eventTypes = result.Events.Select(x => x.EventType).ToArray();
        Assert.Contains("RECORDER_RUN_STARTED", eventTypes);
        Assert.Contains("BBO_UPDATED", eventTypes);
        Assert.Contains("MODEL_WEIGHT_BATCH_OBSERVED", eventTypes);
        Assert.Contains("MODEL_RUN_OBSERVED", eventTypes);
        Assert.Contains("TARGET_WEIGHT_OBSERVED", eventTypes);
        Assert.Contains("TARGET_POSITION_OBSERVED", eventTypes);
        Assert.Contains("TARGET_OBSERVED", eventTypes);
        Assert.Contains("TARGET_ACTIVATED", eventTypes);
        Assert.DoesNotContain("TARGET_REVISED", eventTypes);
        Assert.Contains("SIZING_MARKET_SNAPSHOT_OBSERVED", eventTypes);
        Assert.Contains("EXECUTION_BBO_OBSERVED", eventTypes);
        Assert.Contains("DRIFT_SNAPSHOT_OBSERVED", eventTypes);
        Assert.Contains("SHADOW_DECISION", eventTypes);
        Assert.Contains("SHADOW_PARENT_INTENT", eventTypes);
        Assert.Contains("SHADOW_CHILD_INTENT", eventTypes);
        Assert.Contains("RISK_DECISION_OBSERVED", eventTypes);
        Assert.Contains("POSITION_SNAPSHOT_OBSERVED", eventTypes);
        Assert.Contains("RECORDER_RUN_STOPPED", eventTypes);
    }

    [Fact]
    public async Task T17_event_counts_by_strategy_and_book_are_separate()
    {
        var result = await RunHostAsync();
        Assert.True(result.FinalManifest.EventCountsByStrategy["INTRADAY"] > result.FinalManifest.EventCountsByStrategy["DAILY"]);
        Assert.True(result.FinalManifest.EventCountsByBook["INTRADAY"] > result.FinalManifest.EventCountsByBook["DAILY"]);
    }

    [Fact]
    public async Task T18_no_implicit_netting_between_daily_and_intraday_books()
    {
        var result = await RunHostAsync();
        var daily = result.Events.Single(x => x.StrategyId == "DAILY");
        var intraday = result.Events.First(x => x.StrategyId == "INTRADAY" && x.Symbol == "EURUSD" && x.EventType == "TARGET_WEIGHT_OBSERVED");
        Assert.NotEqual(daily.BookId, intraday.BookId);
        Assert.NotEqual(daily.StrategyRunId, intraday.StrategyRunId);
    }

    [Fact]
    public async Task T19_replay_hash_is_deterministic_for_same_fixture()
    {
        var first = await RunHostAsync();
        var second = await RunHostAsync();
        Assert.Equal(first.ReplayReport.DeterministicReplayHash, second.ReplayReport.DeterministicReplayHash);
    }

    [Fact]
    public async Task T20_parity_report_matches_source_payload_hashes()
    {
        var result = await RunHostAsync();
        Assert.All(result.ParityReport.Rows, row => Assert.True(row.Match));
        Assert.True(result.ParityReport.RowCount >= 10);
    }

    [Fact]
    public async Task T21_final_manifest_paths_are_relative_forward_slash_only()
    {
        var result = await RunHostAsync();
        Assert.All(result.FinalManifest.Chunks, x =>
        {
            Assert.DoesNotContain("\\", x.File);
            Assert.DoesNotContain("..", x.File);
        });
    }

    [Fact]
    public void T22_forbidden_runtime_tokens_are_not_present_in_offline_runtime_source()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "QQ.Production.Intraday.Application", "CanonicalRecorder", "CanonicalShadowOffline.cs"));
        var runtimeSource = source.Split("public static readonly string[] ForbiddenRuntimeTokens", StringSplitOptions.None)[0];
        var hits = CanonicalShadowOfflineSafety.FindForbiddenTokensInText(runtimeSource);
        Assert.Empty(hits);
    }

    [Fact]
    public async Task T23_no_gateway_or_network_capabilities_in_run_manifest()
    {
        var result = await RunHostAsync();
        var runManifestPath = Path.Combine(result.RunRoot, "run_manifest.json");
        var manifestText = await File.ReadAllTextAsync(runManifestPath);
        Assert.Contains("\"order_entry_capability\":\"ABSENT\"", manifestText);
        Assert.Contains("\"network_capability\":\"ABSENT\"", manifestText);
    }

    [Fact]
    public void T24_fixture_rejects_invalid_market_data_before_recording()
    {
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture();
        var bad = fixture.Weights.ToArray();
        bad[0] = bad[0] with { Ask = 0m };
        Assert.Throws<InvalidOperationException>(() => CanonicalIntradayManagerOutputMapper.Map(fixture with { Weights = bad }));
    }

    [Fact]
    public async Task T25_position_snapshot_carries_target_current_and_drift()
    {
        var result = await RunHostAsync();
        var position = result.Events.First(x => x.EventType == "POSITION_SNAPSHOT_OBSERVED");
        using var doc = JsonDocument.Parse(position.PayloadJson.GetRawText());
        Assert.NotEqual(0m, doc.RootElement.GetProperty("target_venue_quantity").GetDecimal());
        Assert.NotEqual(0m, doc.RootElement.GetProperty("current_venue_quantity").GetDecimal());
        Assert.NotEqual(0m, doc.RootElement.GetProperty("drift_venue_quantity").GetDecimal());
        Assert.False(doc.RootElement.GetProperty("current_position").GetProperty("authoritative").GetBoolean());
    }

    [Fact]
    public async Task T26_failure_injection_no_go_when_recorder_rejects_event()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(
            new CanonicalRecorderV2Options(NewRoot(), "fail-shadow", "LOCAL_SHADOW_OFFLINE", "test", "git", "m2a", "cfg", ["CanonicalShadowOffline"], ["EURUSD"], ["FUND-DEMO-001"], ["INTRADAY"], ["INTRADAY"], QueueCapacity: 1, StartWriterWorkerForTestsOnly: false),
            new ManualRecorderClock(new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero)));
        var sink = new CanonicalRecorderSink(recorder);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.RecordAsync(new CanonicalRecorderV2Event("TARGET_WEIGHT_OBSERVED", "test", "TargetWeight", "v1", new { x = 1 }, FundId: "FUND-DEMO-001", StrategyId: "INTRADAY", BookId: "INTRADAY")));
        Assert.True(recorder.Health.Failed);
    }

    private static async Task<CanonicalShadowOfflineRunResult> RunHostAsync()
        => await new CanonicalShadowOfflineHost().RunAsync(NewRoot(), "m2b-shadow-fixture", "test-commit", "0c2366b1b167401cbd7f1b3441004f1ab03a2955");

    private static string NewRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "anubis-m2b-shadow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "QQ.Production.Intraday.Application", "CanonicalRecorder", "CanonicalShadowOffline.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo_root_not_found");
    }
}
