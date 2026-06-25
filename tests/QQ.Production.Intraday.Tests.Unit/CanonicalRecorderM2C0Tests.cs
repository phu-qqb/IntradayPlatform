using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalRecorderM2C0Tests
{
    [Fact]
    public void T01_read_only_market_data_source_interface_has_no_order_or_account_capability()
    {
        var names = typeof(IReadOnlyMarketDataSource).GetMethods().Select(x => x.Name).ToArray();
        Assert.Contains("StartAsync", names);
        Assert.Contains("SubscribeAsync", names);
        Assert.Contains("ReadMarketDataAsync", names);
        Assert.Contains("StopAsync", names);
        Assert.DoesNotContain(names, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Account", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T02_m2c1_config_template_contract_has_no_credentials_or_order_fields()
    {
        var fields = typeof(M2C1ReadOnlyCaptureConfig).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name).ToArray();
        Assert.Contains("MarketDataEndpointAlias", fields);
        Assert.Contains("MarketDataSessionAlias", fields);
        Assert.DoesNotContain(fields, x => x.Contains("Credential", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, x => x.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, x => x.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, x => x.Contains("Account", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T03_feed_state_machine_is_fail_closed_unless_synchronized_valid_fresh_and_recorder_ready()
    {
        var machine = new ReadOnlyMarketDataFeedStateMachine();
        var quote = ReadOnlyMarketDataFixtures.EurUsdAudUsdPlayback().First();
        Assert.False(machine.CanProduceShadowIntent(quote, quote.SourceTimestampUtc, TimeSpan.FromSeconds(1), recorderReady: true));
        machine.OnStart();
        machine.OnConnected();
        machine.OnSubscribing();
        machine.OnSynchronized();
        Assert.True(machine.CanProduceShadowIntent(quote, quote.SourceTimestampUtc.AddMilliseconds(250), TimeSpan.FromSeconds(1), recorderReady: true));
        Assert.False(machine.CanProduceShadowIntent(quote with { BookValid = false }, quote.SourceTimestampUtc.AddMilliseconds(250), TimeSpan.FromSeconds(1), recorderReady: true));
        Assert.False(machine.CanProduceShadowIntent(quote, quote.SourceTimestampUtc.AddSeconds(5), TimeSpan.FromSeconds(1), recorderReady: true));
        Assert.False(machine.CanProduceShadowIntent(quote, quote.SourceTimestampUtc.AddMilliseconds(250), TimeSpan.FromSeconds(1), recorderReady: false));
    }

    [Fact]
    public async Task T04_playback_covers_multi_instrument_gap_possdup_stale_invalid_and_recovery_cases()
    {
        var playback = new PlaybackReadOnlyMarketDataSource(ReadOnlyMarketDataFixtures.EurUsdAudUsdPlayback());
        await playback.StartAsync();
        await playback.SubscribeAsync([
            new ReadOnlyMarketDataSubscription("4001", "EURUSD"),
            new ReadOnlyMarketDataSubscription("4007", "AUDUSD")]);
        var rows = await CollectAsync(playback.ReadMarketDataAsync());
        Assert.Contains(rows, x => x.Symbol == "EURUSD");
        Assert.Contains(rows, x => x.Symbol == "AUDUSD");
        Assert.Contains(rows, x => x.PossDup);
        Assert.Contains(rows, x => x.GapStatus == "GAP");
        Assert.Contains(rows, x => x.GapStatus == "STALE");
        Assert.Contains(rows, x => !x.BookValid);
        Assert.Contains(rows, x => x.GapStatus == "RECOVERED");
        Assert.Equal(ReadOnlyMarketDataFeedState.Failed, playback.Health.State);
    }

    [Fact]
    public void T05_activation_policy_blocks_before_effective_and_after_deadline()
    {
        var f = CanonicalShadowOfflineFixtures.IntradayFixture();
        var policy = ShadowTargetActivationPolicy.Default;
        Assert.False(policy.Evaluate(f.EffectiveFromUtc.AddMilliseconds(-1), f.EffectiveFromUtc, f.DeadlineUtc).MayActivate);
        Assert.True(policy.Evaluate(f.EffectiveFromUtc, f.EffectiveFromUtc, f.DeadlineUtc).MayActivate);
        Assert.True(policy.Evaluate(f.DeadlineUtc.AddMilliseconds(1), f.EffectiveFromUtc, f.DeadlineUtc).IsExpired);
    }

    [Fact]
    public void T06_before_effective_from_observes_targets_without_decisions_or_child_intents()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var f = contracts.SourceFixture;
        var events = CanonicalDomainEventMapper.MapIntraday(contracts, nowUtc: f.EffectiveFromUtc.AddMilliseconds(-1)).ToArray();
        Assert.Contains(events, x => x.EventType == "TARGET_OBSERVED");
        Assert.DoesNotContain(events, x => x.EventType == "TARGET_ACTIVATED");
        Assert.DoesNotContain(events, x => x.EventType == "SHADOW_DECISION");
        Assert.DoesNotContain(events, x => x.EventType == "SHADOW_CHILD_INTENT");
    }

    [Fact]
    public void T07_after_deadline_expires_targets_without_new_child_intents()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var f = contracts.SourceFixture;
        var events = CanonicalDomainEventMapper.MapIntraday(contracts, nowUtc: f.DeadlineUtc.AddMilliseconds(1)).ToArray();
        Assert.Contains(events, x => x.EventType == "TARGET_EXPIRED");
        Assert.DoesNotContain(events, x => x.EventType == "SHADOW_CHILD_INTENT");
    }

    [Fact]
    public void T08_revision_is_explicit_versioned_and_absent_from_nominal_mapping()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.DoesNotContain(CanonicalDomainEventMapper.MapIntraday(contracts), x => x.EventType == "TARGET_REVISED");
        var revision = CanonicalDomainEventMapper.MapRevision(contracts, "EURUSD").Single();
        Assert.Equal("TARGET_REVISED", revision.EventType);
        Assert.Equal(2, revision.TargetVersion);
        Assert.Equal(1, revision.SupersedesTargetVersion);
        Assert.NotNull(revision.DerivedEventId);
    }

    [Fact]
    public void T09_shadow_risk_is_not_authoritative_and_not_risk_approved()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.All(contracts.TradeIntents, x => Assert.Equal(TradeIntentStatus.Created, x.Status));
        Assert.All(contracts.RiskDecisions, x => Assert.NotEqual(RiskDecisionStatus.Approved, x.Status));
        var riskEval = CanonicalDomainEventMapper.MapIntraday(contracts).First(x => x.EventType == "SHADOW_RISK_EVALUATION_OBSERVED");
        var json = JsonSerializer.Serialize(riskEval.Payload, CanonicalRecorderV2Constants.JsonOptions);
        Assert.Contains("\"authoritative\":false", json);
    }

    [Fact]
    public void T10_position_fixture_is_nonzero_and_drift_uses_it()
    {
        var contracts = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        Assert.Contains(contracts.SourceFixture.Weights, x => x.CurrentVenueQuantity != 0m);
        var eur = contracts.SourceFixture.Weights.ToList().FindIndex(x => x.Symbol == "EURUSD");
        Assert.Equal(contracts.TargetPositions[eur].TargetVenueQuantity - contracts.SourceFixture.Weights[eur].CurrentVenueQuantity, contracts.DriftSnapshots[eur].DriftVenueQuantity);
    }

    [Fact]
    public async Task T11_offline_host_records_sizing_and_execution_bbo_for_each_target_instrument()
    {
        var result = await RunHostAsync();
        Assert.Equal("PASS", result.Status);
        Assert.Contains(result.Events, x => x.EventType == "SIZING_MARKET_SNAPSHOT_OBSERVED" && x.Symbol == "EURUSD" && x.BidQuantity == 1_000_000m && x.AskQuantity == 1_000_000m);
        Assert.Contains(result.Events, x => x.EventType == "SIZING_MARKET_SNAPSHOT_OBSERVED" && x.Symbol == "AUDUSD");
        Assert.Contains(result.Events, x => x.EventType == "EXECUTION_BBO_OBSERVED" && x.Symbol == "EURUSD" && x.ExecutionBboEventId is not null);
        Assert.Contains(result.Events, x => x.EventType == "SHADOW_CHILD_INTENT" && x.SizingMarketSnapshotId is not null && x.ExecutionBboEventId is not null);
    }

    [Fact]
    public async Task T12_parity_is_independent_and_fails_when_source_contract_changes()
    {
        var result = await RunHostAsync();
        var fixture = CanonicalShadowOfflineFixtures.IntradayFixture();
        var weights = fixture.Weights.ToArray();
        weights[0] = weights[0] with { Weight = weights[0].Weight + 0.0010m };
        var tamperedSource = CanonicalIntradayManagerOutputMapper.Map(fixture with { Weights = weights });
        var parity = CanonicalShadowOfflineHost.BuildParity(tamperedSource, CanonicalShadowOfflineFixtures.DailyFixture(), result.Events);
        Assert.Equal("FAIL", parity.Status);
        Assert.True(parity.MismatchCount > 0);
    }

    [Fact]
    public async Task T13_replay_snapshot_returns_validated_events_and_input_hashes()
    {
        var result = await RunHostAsync();
        var snapshot = await new CanonicalRecorderV2Replayer().ReplaySnapshotAsync(result.RunRoot);
        Assert.Equal("PASS", snapshot.ReplayReport.Status);
        Assert.NotEmpty(snapshot.Events);
        Assert.Contains("final_manifest.json", snapshot.InputFileHashes.Keys);
        Assert.Contains("run_manifest.json", snapshot.InputFileHashes.Keys);
    }

    [Fact]
    public async Task T14_run_integrity_and_shadow_readiness_status_are_calculated()
    {
        var result = await RunHostAsync();
        Assert.Equal("VALID", result.DataQualityReport.ManifestValidationStatus);
        Assert.Equal("PASS", result.DataQualityReport.RunIntegrityStatus);
        Assert.Equal("READY", result.DataQualityReport.RecorderHealthStatus);
        Assert.Equal("READY", result.DataQualityReport.ShadowReadinessStatus);
    }

    [Fact]
    public void T15_replay_hash_changes_with_environment_event_id_and_derived_identity()
    {
        var payload = JsonSerializer.SerializeToElement(new { x = 1 }, CanonicalRecorderV2Constants.JsonOptions);
        var baseEvent = Envelope("ENV-A", "evt-a", "derived-a", payload);
        var envChanged = baseEvent with { Environment = "ENV-B" };
        var idChanged = baseEvent with { EventId = "evt-b" };
        var derivedChanged = baseEvent with { DerivedEventId = "derived-b" };
        var h = CanonicalRecorderV2Replayer.ComputeDeterministicReplayHash([baseEvent]);
        Assert.NotEqual(h, CanonicalRecorderV2Replayer.ComputeDeterministicReplayHash([envChanged]));
        Assert.NotEqual(h, CanonicalRecorderV2Replayer.ComputeDeterministicReplayHash([idChanged]));
        Assert.NotEqual(h, CanonicalRecorderV2Replayer.ComputeDeterministicReplayHash([derivedChanged]));
    }

    [Fact]
    public void T16_safety_tokens_absent_from_new_read_only_market_data_host_runtime_contracts()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "QQ.Production.Intraday.Application", "CanonicalRecorder", "CanonicalReadOnlyMarketDataHost.cs"));
        Assert.DoesNotContain("IVenueExecutionGateway", source);
        Assert.DoesNotContain("SendOrder", source);
        Assert.DoesNotContain("CancelOrder", source);
        Assert.DoesNotContain("ReplaceOrder", source);
        Assert.DoesNotContain("AccountAPI", source);
        Assert.DoesNotContain("Databento", source);
        Assert.DoesNotContain("R009", source);
        Assert.DoesNotContain("R018", source);
        Assert.DoesNotContain("R216", source);
    }

    [Fact]
    public async Task T17_playback_to_recorder_to_replay_passes_without_network_or_order_entry()
    {
        var root = NewRoot();
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), 1_000_000);
        await using var recorder = await CanonicalRecorderV2.CreateAsync(new CanonicalRecorderV2Options(root, "m2c0-playback", "LOCAL_SHADOW_OFFLINE", "test", "git", "m2c0", "cfg", ["M2C0Playback"], ["EURUSD", "AUDUSD"], [], [], [], FlushInterval: TimeSpan.FromMilliseconds(1)), clock);
        var source = new PlaybackReadOnlyMarketDataSource(ReadOnlyMarketDataFixtures.EurUsdAudUsdPlayback().Where(x => x.BookValid && x.GapStatus == "OK").ToArray());
        await source.StartAsync();
        await source.SubscribeAsync([new ReadOnlyMarketDataSubscription("4001", "EURUSD"), new ReadOnlyMarketDataSubscription("4007", "AUDUSD")]);
        await foreach (var observation in source.ReadMarketDataAsync())
        {
            clock.Advance(TimeSpan.FromMilliseconds(1), 1);
            Assert.True(await recorder.RecordAsync(new CanonicalRecorderV2Event(
                "BBO_UPDATED",
                "M2C0Playback",
                "ReadOnlyMarketDataObservationV1",
                "v1",
                observation,
                SourceEventId: observation.QuoteEventId,
                SourceEventSequence: observation.FixMsgSeqNum,
                SourceEntityId: observation.QuoteEventId,
                InstrumentId: observation.InstrumentId,
                Symbol: observation.Symbol,
                Venue: observation.Venue,
                SourceTimestampUtc: observation.SourceTimestampUtc,
                SessionId: observation.SessionId,
                FixMsgSeqNum: observation.FixMsgSeqNum,
                PossDup: observation.PossDup,
                QuoteEventId: observation.QuoteEventId,
                BidPrice: observation.BidPrice,
                BidQuantity: observation.BidQuantity,
                AskPrice: observation.AskPrice,
                AskQuantity: observation.AskQuantity,
                BookValid: observation.BookValid,
                SourceReceiveSequence: observation.FixMsgSeqNum)));
        }

        await recorder.CompleteAsync();
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Equal("PASS", replay.Status);
    }

    private static async Task<List<ReadOnlyMarketDataObservationV1>> CollectAsync(IAsyncEnumerable<ReadOnlyMarketDataObservationV1> rows)
    {
        var list = new List<ReadOnlyMarketDataObservationV1>();
        await foreach (var row in rows)
        {
            list.Add(row);
        }

        return list;
    }

    private static async Task<CanonicalShadowOfflineRunResult> RunHostAsync()
        => await new CanonicalShadowOfflineHost().RunAsync(NewRoot(), "m2c0-shadow-fixture", "test-commit", "551dd0bae4ff1133f51eb8580ed9062797791c03");

    private static CanonicalRecorderEnvelopeV2 Envelope(string environment, string eventId, string derivedEventId, JsonElement payload)
        => new()
        {
            SchemaVersion = CanonicalRecorderV2Constants.EnvelopeSchemaVersion,
            RecorderRunId = "hash-test",
            EventId = eventId,
            ProcessEventSequence = 1,
            EventType = "TARGET_OBSERVED",
            Environment = environment,
            SourceComponent = "UnitTest",
            SourceContract = "TargetPosition",
            SourceContractVersion = "v1",
            FundId = "FUND",
            PortfolioId = "PORT",
            StrategyId = "INTRADAY",
            BookId = "INTRADAY",
            SourceEventId = "source-target",
            SourceEventSequence = 1,
            DerivedEventId = derivedEventId,
            DerivedFromSourceEventId = "source-target",
            DerivedEventSequence = 1,
            SourceEntityId = "target",
            LocalReceiveUtc = DateTimeOffset.UnixEpoch,
            LocalMonotonicTicks = 1,
            RecordedUtc = DateTimeOffset.UnixEpoch,
            PayloadSha256 = CanonicalRecorderV2.Sha256Text(JsonSerializer.Serialize(payload, CanonicalRecorderV2Constants.JsonOptions)),
            PayloadJson = payload,
            CodeCommit = "test",
            ConfigHash = "cfg",
            HostId = "host",
            ProcessId = 1
        };

    private static string NewRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "anubis-m2c0-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "QQ.Production.Intraday.Application", "CanonicalRecorder", "CanonicalReadOnlyMarketDataHost.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo_root_not_found");
    }
}
