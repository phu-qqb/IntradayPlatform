using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application.CanonicalRecorder;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalRecorderM2BCloseoutTests
{
    [Fact]
    public async Task T01_concurrent_producers_preserve_physical_order_without_duplicates_gaps()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot(), queueCapacity: 8192), new ConcurrentRecorderClock());
        var tasks = Enumerable.Range(0, 8)
            .Select(producer => Task.Run(async () =>
            {
                for (var i = 0; i < 300; i++)
                {
                    var accepted = await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", $"producer-{producer}-event-{i}", sourceEventSequence: producer * 1000 + i));
                    Assert.True(accepted);
                }
            }))
            .ToArray();
        await Task.WhenAll(tasks);
        var manifest = await recorder.CompleteAsync();
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Equal("PASS", replay.Status);
        Assert.Equal(2400, manifest.EventsWritten);
        Assert.Equal(2400, replay.EventCount);
    }

    [Fact]
    public async Task T02_payload_source_ids_preserved_under_concurrency()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot(), queueCapacity: 128), new ConcurrentRecorderClock());
        await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "source-id-a", sourceEventSequence: 42));
        await recorder.RecordAsync(IntradayEvent("TARGET_POSITION_OBSERVED", "source-id-b", sourceEventSequence: 43));
        await recorder.CompleteAsync();
        var events = await new CanonicalRecorderV2Replayer().ReadEventsAsync(recorder.RunRoot);
        Assert.Contains(events, x => x.SourceEventId == "source-id-a" && x.SourceEventSequence == 42);
        Assert.Contains(events, x => x.SourceEventId == "source-id-b" && x.SourceEventSequence == 43);
    }

    [Fact]
    public async Task T03_saturation_fails_closed()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot(), queueCapacity: 1, startWriter: false), new ConcurrentRecorderClock());
        var accepted = await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "one"));
        Assert.False(accepted);
        Assert.True(recorder.Health.Failed);
        Assert.Equal("FAILED", recorder.Health.WriterState);
    }

    [Fact]
    public async Task T04_second_writer_same_run_rejected()
    {
        var root = NewRoot();
        var clock = new ConcurrentRecorderClock();
        await using var first = await CanonicalRecorderV2.CreateAsync(Options(root, runId: "same-run"), clock);
        await Assert.ThrowsAsync<IOException>(() => CanonicalRecorderV2.CreateAsync(Options(root, runId: "same-run"), clock));
    }

    [Fact]
    public async Task T05_lock_released_after_dispose_but_finalized_run_cannot_reopen()
    {
        var root = NewRoot();
        var clock = new ConcurrentRecorderClock();
        await using (var recorder = await CanonicalRecorderV2.CreateAsync(Options(root, runId: "finalized-run"), clock))
        {
            await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "source"));
            await recorder.CompleteAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => CanonicalRecorderV2.CreateAsync(Options(root, runId: "finalized-run"), clock));
    }

    [Fact]
    public async Task T06_recovered_complete_line_included_in_replay_and_sequence_continues()
    {
        var root = NewRoot();
        var runRoot = PrecreateRunRoot(root, "recover-complete");
        Directory.CreateDirectory(Path.Combine(runRoot, "chunks"));
        var envelope = Envelope(1, "RECORDER_RUN_STARTED", sourceEventId: "recovered-line");
        await File.WriteAllTextAsync(Path.Combine(runRoot, "chunks", "events-000001.jsonl.tmp"), JsonSerializer.Serialize(envelope, CanonicalRecorderV2Constants.JsonOptions) + "\n");
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(root, runId: "recover-complete"), new ConcurrentRecorderClock());
        await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "after-recovery"));
        await recorder.CompleteAsync();
        var events = await new CanonicalRecorderV2Replayer().ReadEventsAsync(recorder.RunRoot);
        Assert.Contains(events, x => x.SourceEventId == "recovered-line");
        Assert.Contains(events, x => x.ProcessEventSequence == 3 && x.SourceEventId == "after-recovery");
    }

    [Fact]
    public async Task T07_recovered_corrupt_tail_makes_readiness_false()
    {
        var root = NewRoot();
        var runRoot = PrecreateRunRoot(root, "recover-tail");
        Directory.CreateDirectory(Path.Combine(runRoot, "chunks"));
        var envelope = JsonSerializer.Serialize(Envelope(1, "RECORDER_RUN_STARTED", sourceEventId: "ok"), CanonicalRecorderV2Constants.JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(runRoot, "chunks", "events-000001.jsonl.tmp"), envelope + "\n{\"broken\":");
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(root, runId: "recover-tail"), new ConcurrentRecorderClock());
        var manifest = await recorder.CompleteAsync();
        var dq = ReadJson<CanonicalRecorderV2DataQualityReport>(Path.Combine(recorder.RunRoot, "health", "data_quality_report.json"));
        Assert.False(dq.ShadowReady);
        Assert.Equal("FAILED", dq.RunStatus);
        Assert.Equal("recovered_corrupt_tail", manifest.FailureReason);
    }

    [Fact]
    public async Task T08_flush_checkpoint_makes_event_readable_before_complete()
    {
        var root = NewRoot();
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(root), new ConcurrentRecorderClock());
        await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "flush-me"));
        await recorder.FlushCheckpointAsync();
        var tmp = Directory.GetFiles(Path.Combine(recorder.RunRoot, "chunks"), "*.tmp").Single();
        await using var fs = new FileStream(tmp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        Assert.Contains("flush-me", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task T09_writer_failure_stops_acceptance_and_manifest_failed()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot(), failAfter: 0), new ConcurrentRecorderClock());
        Assert.True(await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "will-fail")));
        await SpinUntilAsync(() => recorder.Health.Failed);
        Assert.False(await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "after-fail")));
        var manifest = await recorder.CompleteAsync();
        Assert.Equal("FAILED", manifest.WriterState);
        var dq = ReadJson<CanonicalRecorderV2DataQualityReport>(Path.Combine(recorder.RunRoot, "health", "data_quality_report.json"));
        Assert.False(dq.ShadowReady);
    }

    [Fact]
    public async Task T10_clean_run_enqueued_written_invariant()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot()), new ConcurrentRecorderClock());
        await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "one"));
        var manifest = await recorder.CompleteAsync();
        Assert.Equal(manifest.EventsEnqueued, manifest.EventsWritten);
    }

    [Fact]
    public async Task T11_duplicate_event_id_detected()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot()), new ConcurrentRecorderClock());
        Assert.True(await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "a", eventId: "dup")));
        Assert.False(await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "b", eventId: "dup")));
    }

    [Fact]
    public async Task T12_payload_hash_tamper_rejected()
    {
        var recorder = await CleanRunAsync();
        MutateFirstEvent(recorder.RunRoot, node => node["payload_json"]!["tampered"] = true);
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Contains("payload_hash_mismatch", replay.FailureReason);
    }

    [Fact]
    public async Task T13_chunk_metadata_mismatch_rejected()
    {
        var recorder = await CleanRunAsync();
        var finalPath = Path.Combine(recorder.RunRoot, "final_manifest.json");
        var node = JsonNode.Parse(await File.ReadAllTextAsync(finalPath))!.AsObject();
        node["chunks"]![0]!["event_count"] = 999;
        await File.WriteAllTextAsync(finalPath, node.ToJsonString(CanonicalRecorderV2Constants.JsonOptions));
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Contains("chunk_sequence_or_count_mismatch", replay.FailureReason);
    }

    [Fact]
    public async Task T14_manifest_hash_mismatch_rejected()
    {
        var recorder = await CleanRunAsync();
        await File.AppendAllTextAsync(Path.Combine(recorder.RunRoot, "run_manifest.json"), "\n");
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Contains("run_manifest_hash_mismatch", replay.FailureReason);
    }

    [Fact]
    public async Task T15_sequence_gap_rejected()
    {
        var recorder = await CleanRunAsync(twoEvents: true);
        MutateFirstTwoEvents(recorder.RunRoot, (_, second) => second["process_event_sequence"] = 3);
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Contains("sequence_gap_or_out_of_order", replay.FailureReason);
    }

    [Fact]
    public async Task T16_chunk_path_traversal_rejected()
    {
        var recorder = await CleanRunAsync();
        var finalPath = Path.Combine(recorder.RunRoot, "final_manifest.json");
        var node = JsonNode.Parse(await File.ReadAllTextAsync(finalPath))!.AsObject();
        node["chunks"]![0]!["file"] = "../escape.jsonl";
        await File.WriteAllTextAsync(finalPath, node.ToJsonString(CanonicalRecorderV2Constants.JsonOptions));
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.Contains("unsafe_chunk_path", replay.FailureReason);
    }

    [Fact]
    public async Task T17_manifest_uses_cross_platform_slash_paths()
    {
        var recorder = await CleanRunAsync();
        var manifest = ReadJson<CanonicalRecorderV2FinalManifest>(Path.Combine(recorder.RunRoot, "final_manifest.json"));
        Assert.All(manifest.Chunks, chunk => Assert.Contains('/', chunk.File));
        Assert.All(manifest.Chunks, chunk => Assert.DoesNotContain('\\', chunk.File));
    }

    [Fact]
    public async Task T18_deterministic_hash_changes_for_semantic_field_family()
    {
        var recorder = await CleanRunAsync();
        var replay1 = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        MutateFirstEvent(recorder.RunRoot, node =>
        {
            node["strategy_id"] = "INTRADAY_CHANGED";
            node["payload_sha256"] = CanonicalRecorderV2.Sha256Text(node["payload_json"]!.ToJsonString(CanonicalRecorderV2Constants.JsonOptions));
        });
        var replay2 = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot);
        Assert.NotEqual(replay1.DeterministicReplayHash, replay2.DeterministicReplayHash);
    }

    [Fact]
    public async Task T19_required_strategy_book_enforced()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot()), new ConcurrentRecorderClock());
        var accepted = await recorder.RecordAsync(new CanonicalRecorderV2Event("TARGET_WEIGHT_OBSERVED", "test", "TargetWeight", "v1", new { x = 1 }, FundId: "FUND"));
        Assert.False(accepted);
    }

    [Fact]
    public async Task T20_shared_market_data_may_omit_strategy()
    {
        await using var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot()), new ConcurrentRecorderClock());
        var accepted = await recorder.RecordAsync(new CanonicalRecorderV2Event("BBO_UPDATED", "test", "MarketDataSnapshot", "v1", new { bid = 1.1, ask = 1.2 }, Symbol: "EURUSD", BookValid: true));
        Assert.True(accepted);
    }

    private static async Task<CanonicalRecorderV2> CleanRunAsync(bool twoEvents = false)
    {
        var recorder = await CanonicalRecorderV2.CreateAsync(Options(NewRoot()), new ConcurrentRecorderClock());
        await recorder.RecordAsync(IntradayEvent("TARGET_WEIGHT_OBSERVED", "one"));
        if (twoEvents)
        {
            await recorder.RecordAsync(IntradayEvent("TARGET_POSITION_OBSERVED", "two"));
        }

        await recorder.CompleteAsync();
        return recorder;
    }

    private static CanonicalRecorderV2Options Options(string root, string runId = "m2b-closeout", int queueCapacity = 128, bool startWriter = true, long? failAfter = null)
        => new(
            root,
            runId,
            "M2B_TEST",
            "TEST_COMMIT",
            "UNIT_TEST",
            "TEST_BASELINE",
            "TEST_CONFIG",
            ["test"],
            ["EURUSD"],
            ["FUND"],
            ["INTRADAY", "DAILY"],
            ["INTRADAY", "DAILY"],
            queueCapacity,
            4096,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMilliseconds(1),
            startWriter,
            failAfter);

    private static CanonicalRecorderV2Event IntradayEvent(string type, string sourceEventId, long? sourceEventSequence = null, string? eventId = null)
        => new(
            type,
            "UnitTest",
            type.Replace("_OBSERVED", string.Empty),
            "v1",
            new { source_event_id = sourceEventId, value = 123m },
            FundId: "FUND",
            PortfolioId: "PORT",
            StrategyId: "INTRADAY",
            StrategyRunId: "STRAT-RUN",
            StrategyVersion: "v1",
            BookId: "INTRADAY",
            SourceEventId: sourceEventId,
            SourceEventSequence: sourceEventSequence,
            SourceEntityId: sourceEventId,
            Symbol: "EURUSD",
            EventId: eventId);

    private static CanonicalRecorderEnvelopeV2 Envelope(long sequence, string type, string sourceEventId)
    {
        var payload = JsonSerializer.SerializeToElement(new { source_event_id = sourceEventId }, CanonicalRecorderV2Constants.JsonOptions);
        return new CanonicalRecorderEnvelopeV2
        {
            SchemaVersion = CanonicalRecorderV2Constants.EnvelopeSchemaVersion,
            RecorderRunId = "recover-complete",
            EventId = $"evt-{sequence:000000000000}",
            ProcessEventSequence = sequence,
            EventType = type,
            Environment = "M2B_TEST",
            SourceComponent = "RecoveryFixture",
            SourceContract = "Recovery",
            SourceContractVersion = "v2",
            FundId = null,
            StrategyId = null,
            BookId = null,
            SourceEventId = sourceEventId,
            SourceEntityId = sourceEventId,
            LocalReceiveUtc = DateTimeOffset.UtcNow,
            LocalMonotonicTicks = sequence,
            RecordedUtc = DateTimeOffset.UtcNow,
            PayloadSha256 = CanonicalRecorderV2.Sha256Text(JsonSerializer.Serialize(payload, CanonicalRecorderV2Constants.JsonOptions)),
            PayloadJson = payload,
            CodeCommit = "TEST_COMMIT",
            ConfigHash = "TEST_CONFIG",
            HostId = "TEST_HOST",
            ProcessId = 1
        };
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "qq-intraday-m2b-tests", Guid.NewGuid().ToString("N"));

    private static string PrecreateRunRoot(string root, string runId)
        => Path.Combine(root, "environment=M2B_TEST", $"date={DateTimeOffset.UtcNow:yyyy-MM-dd}", $"recorder_run={runId}");

    private static T ReadJson<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), CanonicalRecorderV2Constants.JsonOptions)!;

    private static async Task SpinUntilAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(predicate());
    }

    private static void MutateFirstEvent(string runRoot, Action<JsonNode> mutate)
        => MutateEvents(runRoot, nodes => mutate(nodes[0]));

    private static void MutateFirstTwoEvents(string runRoot, Action<JsonNode, JsonNode> mutate)
        => MutateEvents(runRoot, nodes => mutate(nodes[0], nodes[1]));

    private static void MutateEvents(string runRoot, Action<List<JsonNode>> mutate)
    {
        var finalPath = Path.Combine(runRoot, "final_manifest.json");
        var final = JsonNode.Parse(File.ReadAllText(finalPath))!.AsObject();
        var firstChunk = final["chunks"]![0]!.AsObject();
        var chunkPath = Path.Combine(runRoot, firstChunk["file"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
        var nodes = File.ReadAllLines(chunkPath, Encoding.UTF8)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => JsonNode.Parse(x)!)
            .ToList();
        mutate(nodes);
        File.WriteAllLines(chunkPath, nodes.Select(x => x.ToJsonString(CanonicalRecorderV2Constants.JsonOptions)), new UTF8Encoding(false));
        firstChunk["sha256"] = CanonicalRecorderV2.Sha256File(chunkPath);
        firstChunk["size_bytes"] = new FileInfo(chunkPath).Length;
        File.WriteAllText(finalPath, final.ToJsonString(CanonicalRecorderV2Constants.JsonOptions), new UTF8Encoding(false));
    }

    private sealed class ConcurrentRecorderClock : IRecorderClock
    {
        private long ticks;
        private readonly DateTimeOffset start = new(2026, 6, 25, 8, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => start.AddMilliseconds(Interlocked.Increment(ref ticks));
        public long MonotonicTicks => ticks + 1_000_000;
    }
}


