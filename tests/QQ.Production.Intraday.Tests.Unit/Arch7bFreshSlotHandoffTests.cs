using System.Diagnostics;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed partial class Arch7bFreshSlotHandoffTests : IDisposable
{
    private const string Commit = "7a7b800bef1464a793a4b4bede5b628bc1398650";
    private readonly string root = Path.Combine(Path.GetTempPath(), "qq-arch7b-handoff-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Normal_handoff_is_prearmed_before_close_and_imports_once_after_atomic_marker()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var imports = 0;
        var runner = new PmsShadowFreshSlotHandoffRunner(options, clock);
        var run = runner.RunAsync(_ => Task.CompletedTask,
            (marker, observer, _) =>
            {
                Interlocked.Increment(ref imports);
                observer.Record(PmsShadowFreshSlotHandoffEvents.PostgreSqlConnectionStarted,
                    options.SlotId, marker.ArtifactSha256);
                observer.Record(PmsShadowFreshSlotHandoffEvents.PostgreSqlTransactionStarted,
                    options.SlotId, marker.ArtifactSha256);
                return Task.FromResult("COMPLETED");
            });

        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        var prearmed = Events(options).Single(value =>
            value.EventName == PmsShadowFreshSlotHandoffEvents.ImportWatcherPrearmed);
        Assert.True(prearmed.TimestampUtc < options.SlotCloseUtc);

        clock.UtcNow = options.SlotCloseUtc.AddMilliseconds(250);
        var marker = Marker(options, clock);
        Assert.Equal("READY_MARKER_PUBLISHED",
            PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker,
                new PmsShadowFreshSlotHandoffTimeline(options, clock)));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(1, imports);
        Assert.InRange(result.DetectionLatencyMilliseconds, 0,
            PmsShadowFreshSlotHandoffContract.MarkerDetectionSloSeconds * 1000);
        var names = Events(options).OrderBy(value => value.TimestampUtc)
            .Select(value => value.EventName).ToArray();
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.HandoffPreflightStarted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.HandoffPreflightCompleted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ImportWatcherPrearmed, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.SlotClosed, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ArtifactFinalized, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ReadyMarkerPublished, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ReadyMarkerDetected, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ImportProcessStarted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.PostgreSqlConnectionStarted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.PostgreSqlTransactionStarted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.SlotClassified, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.ImportCompleted, names);
        Assert.Contains(PmsShadowFreshSlotHandoffEvents.CleanupCompleted, names);
    }

    [Fact]
    public async Task Existing_marker_before_prearm_is_rejected_fail_closed()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        Directory.CreateDirectory(options.SlotRoot);
        var marker = Marker(options, clock, createdAt: options.SlotCloseUtc.AddSeconds(1));
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker,
            new PmsShadowFreshSlotHandoffTimeline(options, clock));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
                _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED")));
        Assert.Equal("HANDOFF_STALE_READY_MARKER_PRESENT_BEFORE_PREARM", error.Message);
    }

    [Fact]
    public void Double_identical_marker_is_idempotent_and_conflict_is_rejected()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 12, 0, 1));
        var options = Options(clock);
        var marker = Marker(options, clock);
        var timeline = new PmsShadowFreshSlotHandoffTimeline(options, clock);
        Assert.Equal("READY_MARKER_PUBLISHED",
            PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker, timeline));
        Assert.Equal("READY_MARKER_ALREADY_PUBLISHED_IDENTICAL",
            PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker, timeline));
        var conflict = marker with { CreatorProcessId = marker.CreatorProcessId + 1 };
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, conflict, timeline));
        Assert.Equal("HANDOFF_READY_MARKER_CONFLICT", error.Message);
    }

    [Fact]
    public async Task Two_workers_for_one_slot_have_single_ownership()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        using var cancellation = new CancellationTokenSource();
        var first = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED"),
            cancellation.Token);
        await WaitUntil(() => File.Exists(options.OwnershipPath));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
                _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED")));
        Assert.Equal("HANDOFF_ORCHESTRATOR_OWNERSHIP_CONFLICT", error.Message);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await WaitUntil(() => !File.Exists(options.OwnershipPath));
    }

    [Fact]
    public async Task Polling_fallback_has_no_initial_full_sleep_and_is_bounded()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock) with { FallbackPollInterval = TimeSpan.FromMilliseconds(25) };
        var run = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED"));
        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        clock.UtcNow = options.SlotCloseUtc.AddMilliseconds(1);
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, Marker(options, clock),
            new PmsShadowFreshSlotHandoffTimeline(options, clock));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.InRange(result.DetectionLatencyMilliseconds, 0, 1000);
    }

    [Fact]
    public async Task Slow_noncritical_cleanup_does_not_block_ready_marker_or_import()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var run = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED"));
        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        clock.UtcNow = options.SlotCloseUtc.AddMilliseconds(10);
        var cleanup = Task.Delay(TimeSpan.FromSeconds(2));
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, Marker(options, clock),
            new PmsShadowFreshSlotHandoffTimeline(options, clock));
        var completed = await Task.WhenAny(run, cleanup);
        Assert.Same(run, completed);
        Assert.Equal("COMPLETED", (await run).Status);
    }

    [Fact]
    public async Task PostgreSql_unavailable_in_preflight_prevents_arming_and_capture()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
                _ => throw new InvalidOperationException("POSTGRESQL_UNAVAILABLE"),
                (_, _, _) => Task.FromResult("COMPLETED")));
        Assert.Equal("POSTGRESQL_UNAVAILABLE", error.Message);
        Assert.False(File.Exists(options.ArmedStatePath));
        Assert.DoesNotContain(Events(options),
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.CaptureStarted);
    }

    [Fact]
    public async Task PostgreSql_failure_after_close_is_recorded_once_without_completion()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var attempts = 0;
        var run = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask,
            (_, observer, _) =>
            {
                attempts++;
                observer.Record(PmsShadowFreshSlotHandoffEvents.PostgreSqlConnectionStarted,
                    options.SlotId);
                throw new InvalidOperationException("POSTGRESQL_UNAVAILABLE_AFTER_CLOSE");
            });
        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        clock.UtcNow = options.SlotCloseUtc.AddSeconds(1);
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, Marker(options, clock),
            new PmsShadowFreshSlotHandoffTimeline(options, clock));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("POSTGRESQL_UNAVAILABLE_AFTER_CLOSE", error.Message);
        Assert.Equal(1, attempts);
        Assert.False(File.Exists(options.CompletionPath));
        Assert.Contains(Events(options),
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.ImportFailed);
    }

    [Fact]
    public void Absolute_300_second_boundary_is_unchanged()
    {
        var close = Utc(2026, 7, 24, 12, 0, 0);
        Assert.True(PmsShadowFreshSlotHandoffContract.IsWithinAbsoluteStartDeadline(
            close, close.AddSeconds(299.999)));
        Assert.True(PmsShadowFreshSlotHandoffContract.IsWithinAbsoluteStartDeadline(
            close, close.AddSeconds(300)));
        Assert.False(PmsShadowFreshSlotHandoffContract.IsWithinAbsoluteStartDeadline(
            close, close.AddSeconds(300.001)));
        Assert.Equal(300, PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds);
    }

    [Fact]
    public async Task Successful_replay_is_idempotent_and_does_not_start_second_worker()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var imports = 0;
        Task<string> Import(PmsShadowFreshSlotReadyMarker _, IPmsShadowIntradayImportObserver __,
            CancellationToken ___)
        {
            imports++;
            return Task.FromResult("COMPLETED");
        }
        var run = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask, Import);
        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        clock.UtcNow = options.SlotCloseUtc.AddSeconds(1);
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, Marker(options, clock),
            new PmsShadowFreshSlotHandoffTimeline(options, clock));
        Assert.Equal("COMPLETED", (await run).Status);
        var replay = await new PmsShadowFreshSlotHandoffRunner(options, clock)
            .RunAsync(_ => Task.CompletedTask, Import);
        Assert.Equal("ALREADY_COMPLETED_IDENTICAL", replay.Status);
        Assert.Equal(1, imports);
    }

    [Fact]
    public async Task Cleanup_releases_armed_state_owner_and_preserves_append_only_evidence()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var run = new PmsShadowFreshSlotHandoffRunner(options, clock).RunAsync(
            _ => Task.CompletedTask, (_, _, _) => Task.FromResult("COMPLETED"));
        await WaitUntil(() => File.Exists(options.ArmedStatePath));
        clock.UtcNow = options.SlotCloseUtc.AddSeconds(1);
        PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, Marker(options, clock),
            new PmsShadowFreshSlotHandoffTimeline(options, clock));
        await run;
        Assert.False(File.Exists(options.ArmedStatePath));
        Assert.False(File.Exists(options.OwnershipPath));
        Assert.True(File.Exists(options.ReadyMarkerPath));
        Assert.True(File.Exists(options.CompletionPath));
        Assert.Contains(Events(options),
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.CleanupCompleted);
    }

    [Fact]
    public void Marker_rejects_preclose_creation_and_noncritical_work_is_not_in_contract()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 11, 59, 59));
        var options = Options(clock);
        var marker = Marker(options, clock, createdAt: options.SlotCloseUtc.AddTicks(-1));
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowFreshSlotReadyMarkerStore.Validate(options, marker, true));
        Assert.Equal("HANDOFF_READY_MARKER_CREATED_BEFORE_SLOT_CLOSE", error.Message);
        var eventNames = typeof(PmsShadowFreshSlotHandoffEvents).GetFields()
            .Select(field => (string)field.GetRawConstantValue()!).ToArray();
        Assert.DoesNotContain(eventNames, value => value.Contains("ZIP", StringComparison.Ordinal));
        Assert.DoesNotContain(eventNames, value => value.Contains("REPORT", StringComparison.Ordinal));
        Assert.DoesNotContain(eventNames, value => value.Contains("TEST", StringComparison.Ordinal));
        Assert.DoesNotContain(eventNames, value => value.Contains("HISTORY", StringComparison.Ordinal));
    }

    [Fact]
    public void Operational_cli_uses_real_utc_clock_and_has_no_migration_or_retry_path()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch6fEconomicReplay",
            "Arch7bPrearmedFreshSlotHandoffCli.cs"));
        Assert.Contains("DateTimeOffset.UtcNow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Migrate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Retry", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OrderEntry", source, StringComparison.OrdinalIgnoreCase);
    }

    private PmsShadowFreshSlotHandoffOptions Options(MutableTimeProvider clock)
    {
        var close = Utc(2026, 7, 24, 12, 0, 0);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(close);
        return PmsShadowFreshSlotHandoffOptions.Create(root, slot,
            "arch6b-daily-tier1-20260721T130346Z-422530a8", "arch7b-handoff-test", Commit,
            "ARCH7B_UNIT_TEST", new string('a', 64));
    }

    private static PmsShadowFreshSlotReadyMarker Marker(
        PmsShadowFreshSlotHandoffOptions options, MutableTimeProvider clock,
        DateTimeOffset? createdAt = null)
    {
        Directory.CreateDirectory(options.SlotRoot);
        var artifact = Path.Combine(options.SlotRoot, "slot.jsonl");
        var manifest = Path.Combine(options.SlotRoot, "slot_manifest.json");
        File.WriteAllText(artifact, "{\"symbol\":\"GBPUSD\",\"no_order\":true}\n");
        File.WriteAllText(manifest, "{\"complete\":true,\"no_order\":true}\n");
        var marker = PmsShadowFreshSlotReadyMarkerStore.Build(options, artifact, manifest, clock);
        return createdAt is null ? marker : marker with { CreatedAtUtc = createdAt.Value };
    }

    private static IReadOnlyList<PmsShadowFreshSlotHandoffTimelineEvent> Events(
        PmsShadowFreshSlotHandoffOptions options)
    {
        if (!Directory.Exists(options.TimelineRoot)) return [];
        return Directory.GetFiles(options.TimelineRoot, "*.json")
            .Select(path => JsonSerializer.Deserialize<PmsShadowFreshSlotHandoffTimelineEvent>(
                File.ReadAllBytes(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })!)
            .ToArray();
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
