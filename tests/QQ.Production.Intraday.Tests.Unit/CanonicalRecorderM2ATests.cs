using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application.CanonicalRecorder;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalRecorderM2ATests
{
    [Fact]
    public async Task T01_envelope_round_trip()
    {
        var result = await RunSampleAsync();
        var events = await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot);
        var text = JsonSerializer.Serialize(events[0], CanonicalRecorderConstants.JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV1>(text, CanonicalRecorderConstants.JsonOptions);
        Assert.Equal(events[0].EventId, roundTrip!.EventId);
    }

    [Fact]
    public async Task T02_unknown_schema_rejected()
    {
        var result = await RunSampleAsync();
        MutateFirstEvent(result.RunRoot, node => node["schema_version"] = "future_schema_v999");
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("unknown_schema", replay.FailureReason);
    }

    [Fact]
    public async Task T03_absent_ids_remain_null()
    {
        var result = await RunSampleAsync();
        var first = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).First();
        Assert.Null(first.ModelRunId);
        Assert.Null(first.TargetPositionId);
        Assert.Null(first.ChildIntentId);
    }

    [Fact]
    public async Task T04_decision_effective_deadline_preserved()
    {
        var result = await RunSampleAsync();
        var target = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).Single(x => x.EventType == "TARGET_OBSERVED");
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 21, 0, 0, TimeSpan.Zero), target.DecisionTime);
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 8, 15, 0, TimeSpan.Zero), target.EffectiveFrom);
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 8, 30, 0, TimeSpan.Zero), target.Deadline);
    }

    [Fact]
    public async Task T05_no_timestamp_invention()
    {
        var result = await RunSampleAsync();
        var stopped = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).Single(x => x.EventType == "RECORDER_RUN_STOPPED");
        Assert.Null(stopped.DecisionTime);
        Assert.Null(stopped.EffectiveFrom);
        Assert.Null(stopped.Deadline);
        Assert.Null(stopped.TargetClose);
    }

    [Fact]
    public async Task T06_compact_jsonl()
    {
        var result = await RunSampleAsync();
        var line = FirstLine(result.RunRoot);
        Assert.DoesNotContain(Environment.NewLine, line);
        Assert.DoesNotContain("  \"", line);
    }

    [Fact]
    public async Task T07_one_event_per_line()
    {
        var result = await RunSampleAsync();
        var lines = AllLines(result.RunRoot);
        Assert.All(lines, line => Assert.StartsWith("{", line));
        Assert.Equal(result.FinalManifest.EventCounts.Values.Sum(), lines.Count);
    }

    [Fact]
    public async Task T08_process_sequence_monotone()
    {
        var result = await RunSampleAsync();
        var sequences = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).Select(x => x.ProcessEventSequence).ToArray();
        Assert.Equal(sequences.OrderBy(x => x), sequences);
    }

    [Fact]
    public async Task T09_chunk_rotation_by_size()
    {
        var root = NewRoot();
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1);
        await using var recorder = await CanonicalRecorder.CreateAsync(Options(root, rotateAfterBytes: 700), clock);
        await recorder.RecordAsync(Event("RECORDER_RUN_STARTED"));
        clock.Advance(TimeSpan.FromMilliseconds(1), 1);
        await recorder.RecordAsync(Event("BBO_UPDATED"));
        clock.Advance(TimeSpan.FromMilliseconds(1), 1);
        await recorder.RecordAsync(Event("RECORDER_RUN_STOPPED"));
        var manifest = await recorder.CompleteAsync();
        Assert.True(manifest.Chunks.Count > 1);
    }

    [Fact]
    public async Task T10_chunk_rotation_by_duration()
    {
        var root = NewRoot();
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1);
        await using var recorder = await CanonicalRecorder.CreateAsync(Options(root, rotateAfter: TimeSpan.FromMilliseconds(1)), clock);
        await recorder.RecordAsync(Event("RECORDER_RUN_STARTED"));
        clock.Advance(TimeSpan.FromMilliseconds(2), 1);
        await recorder.RecordAsync(Event("RECORDER_RUN_STOPPED"));
        var manifest = await recorder.CompleteAsync();
        Assert.Equal(2, manifest.Chunks.Count);
    }

    [Fact]
    public async Task T11_final_manifest_hashes_valid()
    {
        var result = await RunSampleAsync();
        foreach (var chunk in result.FinalManifest.Chunks)
        {
            Assert.Equal(chunk.Sha256, CanonicalRecorder.Sha256File(Path.Combine(result.RunRoot, chunk.File)));
        }
    }

    [Fact]
    public async Task T12_bounded_queue_high_water_measured()
    {
        var result = await RunSampleAsync();
        Assert.True(result.FinalManifest.QueueHighWatermark >= 1);
    }

    [Fact]
    public async Task T13_queue_saturation_fails_closed()
    {
        var root = NewRoot();
        var recorder = await CanonicalRecorder.CreateAsync(Options(root, queueCapacity: 1, startWriter: false), new ManualRecorderClock(DateTimeOffset.UtcNow, 1));
        var accepted = await recorder.RecordAsync(Event("BBO_UPDATED"));
        Assert.False(accepted);
        Assert.True(recorder.Health.Failed);
        await recorder.DisposeAsync();
    }

    [Fact]
    public async Task T14_writer_exception_fails_fast_on_invalid_root()
    {
        var rootFile = Path.Combine(NewRoot(), "not-a-directory");
        Directory.CreateDirectory(Path.GetDirectoryName(rootFile)!);
        File.WriteAllText(rootFile, "occupied");
        await Assert.ThrowsAnyAsync<Exception>(() => CanonicalRecorder.CreateAsync(Options(rootFile), new ManualRecorderClock(DateTimeOffset.UtcNow, 1)));
    }

    [Fact]
    public async Task T15_no_silent_drop()
    {
        var root = NewRoot();
        var recorder = await CanonicalRecorder.CreateAsync(Options(root, startWriter: false), new ManualRecorderClock(DateTimeOffset.UtcNow, 1));
        await recorder.RecordAsync(Event("BBO_UPDATED"));
        Assert.Equal(1, recorder.Health.EventsDropped);
        Assert.Equal(1, recorder.Health.EventsRejected);
        await recorder.DisposeAsync();
    }

    [Fact]
    public async Task T16_partial_final_line_truncated()
    {
        var root = NewRoot();
        var runRoot = RecoveryRunRoot(root);
        Directory.CreateDirectory(Path.Combine(runRoot, "chunks"));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "chunks", "events-000001.jsonl.tmp"), "{\"ok\":true}\n{\"broken\":");
        await using var recorder = await CanonicalRecorder.CreateAsync(Options(root, runId: "m2a-recovery"), new ManualRecorderClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1));
        var report = ReadJson<CanonicalRecorderRecoveryReport>(Path.Combine(recorder.RunRoot, "health", "recovery_report.json"));
        Assert.Equal(1, report.RecoveredTailCount);
    }

    [Fact]
    public async Task T17_complete_final_line_recovered()
    {
        var root = NewRoot();
        var runRoot = RecoveryRunRoot(root);
        Directory.CreateDirectory(Path.Combine(runRoot, "chunks"));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "chunks", "events-000001.jsonl.tmp"), "{\"ok\":true}");
        await using var recorder = await CanonicalRecorder.CreateAsync(Options(root, runId: "m2a-recovery"), new ManualRecorderClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1));
        var report = ReadJson<CanonicalRecorderRecoveryReport>(Path.Combine(recorder.RunRoot, "health", "recovery_report.json"));
        Assert.Equal(1, report.RecoveredLineCount);
    }

    [Fact]
    public async Task T18_valid_chunks_untouched()
    {
        var result = await RunSampleAsync();
        var before = result.FinalManifest.Chunks[0].Sha256;
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Equal("PASS", replay.Status);
        Assert.Equal(before, result.FinalManifest.Chunks[0].Sha256);
    }

    [Fact]
    public async Task T19_recovery_report_emitted()
    {
        var result = await RunSampleAsync();
        Assert.True(File.Exists(Path.Combine(result.RunRoot, "health", "recovery_report.json")));
    }

    [Fact]
    public async Task T20_restart_uses_non_colliding_chunk_policy()
    {
        var root = NewRoot();
        var runRoot = RecoveryRunRoot(root);
        Directory.CreateDirectory(Path.Combine(runRoot, "chunks"));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "chunks", "events-000001.jsonl.tmp"), "{\"ok\":true}\n");
        await using var recorder = await CanonicalRecorder.CreateAsync(Options(root, runId: "m2a-recovery"), new ManualRecorderClock(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), 10));
        await recorder.RecordAsync(Event("RECORDER_RUN_STARTED"));
        var manifest = await recorder.CompleteAsync();
        Assert.Contains(manifest.Chunks, x => x.File.EndsWith("events-000002.jsonl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task T21_deterministic_replay_hash()
    {
        var first = await RunSampleAsync(runId: "same-run");
        var second = await RunSampleAsync(runId: "same-run");
        Assert.Equal(first.ReplayReport.DeterministicReplayHash, second.ReplayReport.DeterministicReplayHash);
    }

    [Fact]
    public async Task T22_missing_chunk_rejected()
    {
        var result = await RunSampleAsync();
        File.Delete(Path.Combine(result.RunRoot, result.FinalManifest.Chunks[0].File));
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("missing_chunk", replay.FailureReason);
    }

    [Fact]
    public async Task T23_tampered_chunk_rejected()
    {
        var result = await RunSampleAsync();
        await File.AppendAllTextAsync(Path.Combine(result.RunRoot, result.FinalManifest.Chunks[0].File), "{}\n");
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("chunk_hash_mismatch", replay.FailureReason);
    }

    [Fact]
    public async Task T24_duplicate_sequence_rejected()
    {
        var result = await RunSampleAsync();
        MutateFirstTwoEvents(result.RunRoot, (first, second) => second["process_event_sequence"] = first["process_event_sequence"]!.GetValue<long>());
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("duplicate_sequence", replay.FailureReason);
    }

    [Fact]
    public async Task T25_decreasing_sequence_rejected()
    {
        var result = await RunSampleAsync();
        MutateFirstTwoEvents(result.RunRoot, (_, second) => second["process_event_sequence"] = 0);
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("decreasing_sequence", replay.FailureReason);
    }

    [Fact]
    public async Task T26_unknown_event_type_rejected_by_replay()
    {
        var result = await RunSampleAsync();
        MutateFirstEvent(result.RunRoot, node => node["event_type"] = "UNKNOWN_NEW_TYPE");
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Contains("unknown_event_type", replay.FailureReason);
    }

    [Fact]
    public void T27_no_gateway_project_reference()
    {
        var source = RecorderSource();
        Assert.DoesNotContain("IVenueExecutionGateway", source);
        Assert.DoesNotContain("LmaxVenueGateway", source);
    }

    [Fact]
    public void T28_no_order_send_symbols()
    {
        var source = RecorderSource();
        Assert.DoesNotContain("SendOrder", source);
        Assert.DoesNotContain("CancelOrder", source);
        Assert.DoesNotContain("ReplaceOrder", source);
    }

    [Fact]
    public void T29_no_fix_logon()
    {
        Assert.DoesNotContain("Logon", RecorderSource(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T30_no_network_apis_in_executed_path()
    {
        var source = RecorderSource();
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("Socket", source);
    }

    [Fact]
    public void T31_no_accountapi()
    {
        Assert.DoesNotContain("AccountAPI", RecorderSource(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T32_no_databento()
    {
        Assert.DoesNotContain("Databento", RecorderSource(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T33_no_r009()
    {
        Assert.DoesNotContain("R009", RecorderSource(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T34_no_db_mutation()
    {
        var source = RecorderSource();
        Assert.DoesNotContain("DbContext", source);
        Assert.DoesNotContain("SqlConnection", source);
        Assert.DoesNotContain("SaveChanges", source);
    }

    [Fact]
    public async Task T35_target_known_day_before_preserved()
    {
        var result = await RunSampleAsync();
        var target = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).Single(x => x.EventType == "TARGET_OBSERVED");
        Assert.True(target.DecisionTime < target.EffectiveFrom);
    }

    [Fact]
    public async Task T36_no_intent_before_effective_time()
    {
        var result = await RunSampleAsync();
        var events = await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot);
        var effective = events.Single(x => x.EventType == "TARGET_OBSERVED").EffectiveFrom!.Value;
        Assert.DoesNotContain(events, x => x.EventType == "SHADOW_CHILD_INTENT" && x.RecordedUtc < effective);
    }

    [Fact]
    public async Task T37_intent_after_activation_recorded()
    {
        var result = await RunSampleAsync();
        var events = await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot);
        var activation = events.Single(x => x.EventType == "TARGET_ACTIVATED").RecordedUtc;
        var intent = events.Single(x => x.EventType == "SHADOW_CHILD_INTENT");
        Assert.True(intent.RecordedUtc >= activation);
    }

    [Fact]
    public async Task T38_deadline_preserved_in_child_intent()
    {
        var result = await RunSampleAsync();
        var intent = (await new CanonicalRecorderReplayer().ReadEventsAsync(result.RunRoot)).Single(x => x.EventType == "SHADOW_CHILD_INTENT");
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 8, 30, 0, TimeSpan.Zero), intent.Deadline);
    }

    [Fact]
    public async Task T39_zero_broker_side_effects()
    {
        var result = await RunSampleAsync();
        Assert.Equal(CanonicalRecorderConstants.CapabilityAbsent, ReadJson<CanonicalRecorderRunManifest>(Path.Combine(result.RunRoot, "run_manifest.json")).OrderEntryCapability);
        Assert.Equal(CanonicalRecorderConstants.CapabilityAbsent, ReadJson<CanonicalRecorderRunManifest>(Path.Combine(result.RunRoot, "run_manifest.json")).NetworkCapability);
    }

    [Fact]
    public async Task T40_replay_equality()
    {
        var result = await RunSampleAsync();
        var replay = await new CanonicalRecorderReplayer().ReplayAsync(result.RunRoot);
        Assert.Equal("PASS", replay.Status);
        Assert.Equal(result.ReplayReport.DeterministicReplayHash, replay.DeterministicReplayHash);
    }

    private static async Task<CanonicalRecorderSyntheticRunResult> RunSampleAsync(long rotateAfterBytes = 4096, string runId = "m2a-synthetic")
    {
        var root = NewRoot();
        return await CanonicalRecorderSyntheticScenario.RunAsync(root, runId, "TEST_COMMIT", "TEST_BASELINE");
    }

    private static CanonicalRecorderOptions Options(
        string root,
        string runId = "m2a-test",
        int queueCapacity = 64,
        bool startWriter = true,
        long rotateAfterBytes = 4096,
        TimeSpan? rotateAfter = null)
        => new(
            root,
            runId,
            "M2A_TEST",
            "TEST_COMMIT",
            "UNIT_TEST",
            "TEST_BASELINE",
            "TEST_CONFIG",
            ["Component"],
            ["EURUSD"],
            queueCapacity,
            rotateAfterBytes,
            rotateAfter ?? TimeSpan.FromHours(1),
            TimeSpan.FromMilliseconds(10),
            startWriter);

    private static CanonicalRecorderEvent Event(string type)
        => new(type, "UnitTest", "FixtureContract", "v1", new { type });

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "qq-intraday-m2a-tests", Guid.NewGuid().ToString("N"));

    private static string RecoveryRunRoot(string root)
        => Path.Combine(root, "environment=M2A_TEST", "date=2026-01-01", "recorder_run=m2a-recovery");

    private static string FirstLine(string runRoot)
        => AllLines(runRoot)[0];

    private static List<string> AllLines(string runRoot)
    {
        var manifest = ReadJson<CanonicalRecorderFinalManifest>(Path.Combine(runRoot, "final_manifest.json"));
        return manifest.Chunks
            .SelectMany(chunk => File.ReadAllLines(Path.Combine(runRoot, chunk.File), Encoding.UTF8))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static T ReadJson<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), CanonicalRecorderConstants.JsonOptions)!;

    private static void MutateFirstEvent(string runRoot, Action<JsonNode> mutate)
        => MutateEvents(runRoot, nodes => mutate(nodes[0]));

    private static void MutateFirstTwoEvents(string runRoot, Action<JsonNode, JsonNode> mutate)
        => MutateEvents(runRoot, nodes => mutate(nodes[0], nodes[1]));

    private static void MutateEvents(string runRoot, Action<List<JsonNode>> mutate)
    {
        var manifestPath = Path.Combine(runRoot, "final_manifest.json");
        var manifestNode = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var chunks = manifestNode["chunks"]!.AsArray();
        var firstChunk = chunks[0]!.AsObject();
        var chunkFile = firstChunk["file"]!.GetValue<string>();
        var chunkPath = Path.Combine(runRoot, chunkFile);
        var nodes = File.ReadAllLines(chunkPath, Encoding.UTF8)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => JsonNode.Parse(x)!)
            .ToList();
        mutate(nodes);
        File.WriteAllLines(chunkPath, nodes.Select(x => x.ToJsonString(CanonicalRecorderConstants.JsonOptions)), new UTF8Encoding(false));
        firstChunk["sha256"] = CanonicalRecorder.Sha256File(chunkPath);
        firstChunk["size_bytes"] = new FileInfo(chunkPath).Length;
        File.WriteAllText(manifestPath, manifestNode.ToJsonString(CanonicalRecorderConstants.JsonOptions), new UTF8Encoding(false));
    }

    private static string RecorderSource()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return File.ReadAllText(Path.Combine(current!.FullName, "src", "QQ.Production.Intraday.Application", "CanonicalRecorder", "CanonicalRecorder.cs"));
    }
}


