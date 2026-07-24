using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowFreshSlotHandoffContract
{
    public const string Version = "pms_shadow_fresh_slot_handoff_v1";
    public const string Environment = "TEST";
    public const int AbsoluteStartDeadlineSeconds = 300;
    public const int ReadyMarkerSloSeconds = 10;
    public const int MarkerDetectionSloSeconds = 2;
    public const int PostgreSqlConnectionSloSeconds = 30;
    public const int IndispensableHashingSloSeconds = 5;
    public static readonly TimeSpan DefaultFallbackPollInterval = TimeSpan.FromMilliseconds(100);

    public static bool IsWithinAbsoluteStartDeadline(DateTimeOffset closeUtc, DateTimeOffset observedUtc)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(closeUtc);
        PmsShadowIntradayCadenceContract.RequireUtc(observedUtc);
        return observedUtc - closeUtc <= TimeSpan.FromSeconds(AbsoluteStartDeadlineSeconds);
    }
}

public static class PmsShadowFreshSlotHandoffEvents
{
    public const string HandoffPreflightStarted = "HANDOFF_PREFLIGHT_STARTED";
    public const string HandoffPreflightCompleted = "HANDOFF_PREFLIGHT_COMPLETED";
    public const string ImportWatcherPrearmed = "IMPORT_WATCHER_PREARMED";
    public const string CaptureStarted = "CAPTURE_STARTED";
    public const string SlotClosed = "SLOT_CLOSED";
    public const string ArtifactFinalized = "ARTIFACT_FINALIZED";
    public const string IndispensableHashingStarted = "INDISPENSABLE_HASHING_STARTED";
    public const string IndispensableHashingCompleted = "INDISPENSABLE_HASHING_COMPLETED";
    public const string IndispensableHashingSlow = "INDISPENSABLE_HASHING_SLOW";
    public const string ReadyMarkerPublished = "READY_MARKER_PUBLISHED";
    public const string ReadyMarkerDetected = "READY_MARKER_DETECTED";
    public const string ImportProcessStarted = "IMPORT_PROCESS_STARTED";
    public const string PostgreSqlConnectionStarted = "POSTGRESQL_CONNECTION_STARTED";
    public const string PostgreSqlTransactionStarted = "POSTGRESQL_TRANSACTION_STARTED";
    public const string SlotClassified = "SLOT_CLASSIFIED";
    public const string ImportCompleted = "IMPORT_COMPLETED";
    public const string ImportFailed = "IMPORT_FAILED";
    public const string CleanupCompleted = "CLEANUP_COMPLETED";
}

public sealed record PmsShadowFreshSlotReadyMarker(
    string ContractVersion,
    string SlotId,
    DateTimeOffset SlotCloseUtc,
    string SourceSessionId,
    string LogicalArtifactPath,
    string ArtifactSha256,
    string ManifestSha256,
    DateTimeOffset CreatedAtUtc,
    int CreatorProcessId,
    string RepositoryCommit,
    string Environment,
    bool NoOrder);

public sealed record PmsShadowFreshSlotHandoffOptions(
    string HandoffRoot,
    string SlotId,
    DateTimeOffset SlotCloseUtc,
    string SourceSessionId,
    string RunId,
    string RepositoryCommit,
    TimeSpan FallbackPollInterval)
{
    public string SlotRoot => Path.Combine(Path.GetFullPath(HandoffRoot), SlotId);
    public string ReadyMarkerPath => Path.Combine(SlotRoot, "capture.ready.json");
    public string ArmedStatePath => Path.Combine(SlotRoot, "importer.armed.json");
    public string OwnershipPath => Path.Combine(SlotRoot, "orchestrator.owner.lock");
    public string CompletionPath => Path.Combine(SlotRoot, "import.completed.json");
    public string TimelineRoot => Path.Combine(SlotRoot, "timeline-events");

    public static PmsShadowFreshSlotHandoffOptions Create(string root,
        PmsShadowIntradaySlotWindow slot, string sourceSessionId, string runId, string commit,
        TimeSpan? pollInterval = null) =>
        new(root, slot.SlotId, slot.SlotEndUtc, sourceSessionId, runId, commit,
            pollInterval ?? PmsShadowFreshSlotHandoffContract.DefaultFallbackPollInterval);
}

public sealed record PmsShadowFreshSlotHandoffTimelineEvent(
    string ContractVersion,
    string EventName,
    DateTimeOffset TimestampUtc,
    double ProcessMonotonicElapsedMilliseconds,
    int ProcessId,
    int ManagedThreadId,
    string SlotId,
    string RunId,
    string? ArtifactSha256,
    string? Detail);

public sealed class PmsShadowFreshSlotHandoffTimeline
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly PmsShadowFreshSlotHandoffOptions options;
    private readonly TimeProvider clock;
    private readonly long started;
    private long sequence;

    public PmsShadowFreshSlotHandoffTimeline(PmsShadowFreshSlotHandoffOptions options,
        TimeProvider? clock = null)
    {
        this.options = options;
        this.clock = clock ?? TimeProvider.System;
        started = this.clock.GetTimestamp();
    }

    public void Record(string name, string? artifactSha = null, string? detail = null)
    {
        if (artifactSha is not null)
            PmsShadowIntradayCadenceContract.RequireSha(artifactSha, nameof(artifactSha));
        var now = clock.GetUtcNow();
        var value = new PmsShadowFreshSlotHandoffTimelineEvent(
            PmsShadowFreshSlotHandoffContract.Version, name, now,
            clock.GetElapsedTime(started).TotalMilliseconds, Environment.ProcessId,
            Environment.CurrentManagedThreadId, options.SlotId, options.RunId, artifactSha, detail);
        Directory.CreateDirectory(options.TimelineRoot);
        var ordinal = Interlocked.Increment(ref sequence);
        WriteCreateNew(Path.Combine(options.TimelineRoot,
            $"{now.UtcTicks:D19}-{Environment.ProcessId:D8}-{ordinal:D6}-{Guid.NewGuid():N}.json"),
            JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
    }

    internal static void WriteCreateNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }
}

public interface IPmsShadowIntradayImportObserver
{
    void Record(string eventName, string slotId, string? artifactSha256 = null, string? detail = null);
}

public sealed class NullPmsShadowIntradayImportObserver : IPmsShadowIntradayImportObserver
{
    public static NullPmsShadowIntradayImportObserver Instance { get; } = new();
    private NullPmsShadowIntradayImportObserver() { }
    public void Record(string eventName, string slotId, string? artifactSha256 = null, string? detail = null) { }
}

public sealed class PmsShadowFreshSlotTimelineImportObserver(
    string expectedSlotId, PmsShadowFreshSlotHandoffTimeline timeline)
    : IPmsShadowIntradayImportObserver
{
    public void Record(string eventName, string slotId, string? artifactSha256 = null, string? detail = null)
    {
        if (slotId != expectedSlotId) throw new InvalidOperationException("HANDOFF_TIMELINE_SLOT_MISMATCH");
        timeline.Record(eventName, artifactSha256, detail);
    }
}

public static class PmsShadowFreshSlotReadyMarkerStore
{
    public static PmsShadowFreshSlotReadyMarker Build(PmsShadowFreshSlotHandoffOptions options,
        string artifactPath, string manifestPath, TimeProvider? clock = null,
        PmsShadowFreshSlotHandoffTimeline? timeline = null,
        Func<string, string>? hashFile = null)
    {
        clock ??= TimeProvider.System;
        hashFile ??= Sha256;
        artifactPath = Path.GetFullPath(artifactPath);
        manifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(artifactPath)) throw new InvalidDataException("HANDOFF_ARTIFACT_MISSING");
        if (!File.Exists(manifestPath)) throw new InvalidDataException("HANDOFF_MANIFEST_MISSING");
        if (Path.GetFileName(manifestPath) != "slot_manifest.json")
            throw new InvalidDataException("HANDOFF_MANIFEST_NAME_INVALID");
        if (!string.Equals(Path.GetDirectoryName(artifactPath), Path.GetDirectoryName(manifestPath),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("HANDOFF_ARTIFACT_MANIFEST_ROOT_MISMATCH");
        timeline?.Record(PmsShadowFreshSlotHandoffEvents.IndispensableHashingStarted);
        var started = clock.GetUtcNow();
        var artifactSha = hashFile(artifactPath);
        var manifestSha = hashFile(manifestPath);
        var completed = clock.GetUtcNow();
        var elapsed = completed - started;
        timeline?.Record(PmsShadowFreshSlotHandoffEvents.IndispensableHashingCompleted,
            artifactSha, $"elapsed_ms={elapsed.TotalMilliseconds:F3};files=2");
        if (elapsed > TimeSpan.FromSeconds(PmsShadowFreshSlotHandoffContract.IndispensableHashingSloSeconds))
            timeline?.Record(PmsShadowFreshSlotHandoffEvents.IndispensableHashingSlow,
                artifactSha, $"phase=ARTIFACT_AND_MANIFEST_SHA256;elapsed_ms={elapsed.TotalMilliseconds:F3}");
        return new(PmsShadowFreshSlotHandoffContract.Version, options.SlotId, options.SlotCloseUtc,
            options.SourceSessionId, artifactPath, artifactSha, manifestSha,
            completed, Environment.ProcessId,
            options.RepositoryCommit, PmsShadowFreshSlotHandoffContract.Environment, true);
    }

    public static string PublishAtomic(PmsShadowFreshSlotHandoffOptions options,
        PmsShadowFreshSlotReadyMarker marker, PmsShadowFreshSlotHandoffTimeline timeline)
    {
        Validate(options, marker, true);
        Directory.CreateDirectory(options.SlotRoot);
        if (File.Exists(options.ReadyMarkerPath))
        {
            if (Read(options) != marker) throw new InvalidDataException("HANDOFF_READY_MARKER_CONFLICT");
            return "READY_MARKER_ALREADY_PUBLISHED_IDENTICAL";
        }

        var temporary = options.ReadyMarkerPath + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            PmsShadowFreshSlotHandoffTimeline.WriteCreateNew(temporary,
                JsonSerializer.SerializeToUtf8Bytes(marker, PmsShadowFreshSlotHandoffTimeline.JsonOptions));
            File.Move(temporary, options.ReadyMarkerPath);
        }
        catch (IOException) when (File.Exists(options.ReadyMarkerPath))
        {
            if (Read(options) != marker) throw new InvalidDataException("HANDOFF_READY_MARKER_CONFLICT");
            return "READY_MARKER_ALREADY_PUBLISHED_IDENTICAL";
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        timeline.Record(PmsShadowFreshSlotHandoffEvents.ArtifactFinalized, marker.ArtifactSha256,
            $"manifest_sha256={marker.ManifestSha256}");
        timeline.Record(PmsShadowFreshSlotHandoffEvents.ReadyMarkerPublished, marker.ArtifactSha256);
        return "READY_MARKER_PUBLISHED";
    }

    public static PmsShadowFreshSlotReadyMarker Read(PmsShadowFreshSlotHandoffOptions options)
    {
        var marker = JsonSerializer.Deserialize<PmsShadowFreshSlotReadyMarker>(
            File.ReadAllBytes(options.ReadyMarkerPath), PmsShadowFreshSlotHandoffTimeline.JsonOptions)
            ?? throw new InvalidDataException("HANDOFF_READY_MARKER_INVALID");
        Validate(options, marker, true);
        return marker;
    }

    public static void Validate(PmsShadowFreshSlotHandoffOptions options,
        PmsShadowFreshSlotReadyMarker marker, bool verifyFiles)
    {
        if (marker.ContractVersion != PmsShadowFreshSlotHandoffContract.Version)
            throw new InvalidDataException("HANDOFF_READY_MARKER_VERSION_MISMATCH");
        if (marker.SlotId != options.SlotId || marker.SlotCloseUtc != options.SlotCloseUtc)
            throw new InvalidDataException("HANDOFF_READY_MARKER_SLOT_MISMATCH");
        if (marker.SourceSessionId != options.SourceSessionId)
            throw new InvalidDataException("HANDOFF_READY_MARKER_SOURCE_SESSION_MISMATCH");
        if (marker.RepositoryCommit != options.RepositoryCommit)
            throw new InvalidDataException("HANDOFF_READY_MARKER_REPOSITORY_COMMIT_MISMATCH");
        if (marker.Environment != PmsShadowFreshSlotHandoffContract.Environment || !marker.NoOrder)
            throw new InvalidDataException("HANDOFF_READY_MARKER_SAFETY_MISMATCH");
        PmsShadowIntradayCadenceContract.RequireUtc(marker.CreatedAtUtc);
        PmsShadowIntradayCadenceContract.RequireSha(marker.ArtifactSha256, nameof(marker.ArtifactSha256));
        PmsShadowIntradayCadenceContract.RequireSha(marker.ManifestSha256, nameof(marker.ManifestSha256));
        if (marker.CreatedAtUtc < options.SlotCloseUtc)
            throw new InvalidDataException("HANDOFF_READY_MARKER_CREATED_BEFORE_SLOT_CLOSE");
        if (!verifyFiles) return;
        if (!File.Exists(marker.LogicalArtifactPath) ||
            Sha256(marker.LogicalArtifactPath) != marker.ArtifactSha256)
            throw new InvalidDataException("HANDOFF_READY_MARKER_ARTIFACT_INVALID");
        var manifest = Path.Combine(Path.GetDirectoryName(marker.LogicalArtifactPath)!, "slot_manifest.json");
        if (!File.Exists(manifest) || Sha256(manifest) != marker.ManifestSha256)
            throw new InvalidDataException("HANDOFF_READY_MARKER_MANIFEST_INVALID");
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

public sealed record PmsShadowFreshSlotHandoffResult(
    string Status,
    PmsShadowFreshSlotReadyMarker Marker,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset ImportStartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double DetectionLatencyMilliseconds,
    bool WithinAbsoluteStartDeadline,
    bool NoOrder);

public sealed class PmsShadowFreshSlotHandoffRunner(
    PmsShadowFreshSlotHandoffOptions options, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly PmsShadowFreshSlotHandoffTimeline timeline = new(options, timeProvider);

    public async Task<PmsShadowFreshSlotHandoffResult> RunAsync(
        Func<CancellationToken, Task> connectivityPreflight,
        Func<PmsShadowFreshSlotReadyMarker, IPmsShadowIntradayImportObserver,
            CancellationToken, Task<string>> import,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions();
        if (File.Exists(options.CompletionPath)) return ReadCompleted();
        Directory.CreateDirectory(options.SlotRoot);
        await using var ownership = AcquireOwnership();
        if (File.Exists(options.ReadyMarkerPath))
        {
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CleanupCompleted,
                detail: "phase=STALE_MARKER_PREFLIGHT");
            throw new InvalidDataException("HANDOFF_STALE_READY_MARKER_PRESENT_BEFORE_PREARM");
        }

        timeline.Record(PmsShadowFreshSlotHandoffEvents.HandoffPreflightStarted);
        try
        {
            await connectivityPreflight(cancellationToken);
        }
        catch (Exception exception)
        {
            timeline.Record(PmsShadowFreshSlotHandoffEvents.ImportFailed,
                detail: "phase=HANDOFF_PREFLIGHT;" + exception.GetType().Name + ":" + exception.Message);
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CleanupCompleted,
                detail: "phase=HANDOFF_PREFLIGHT");
            throw;
        }
        timeline.Record(PmsShadowFreshSlotHandoffEvents.HandoffPreflightCompleted);
        if (clock.GetUtcNow() >= options.SlotCloseUtc)
        {
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CleanupCompleted,
                detail: "phase=PREARM_DEADLINE");
            throw new InvalidOperationException("HANDOFF_NOT_PREARMED_BEFORE_SLOT_CLOSE");
        }
        WriteArmedState();
        timeline.Record(PmsShadowFreshSlotHandoffEvents.ImportWatcherPrearmed);

        using var watcher = new FileSystemWatcher(options.SlotRoot, Path.GetFileName(options.ReadyMarkerPath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        var signal = NewSignal();
        FileSystemEventHandler notify = (_, _) => signal.TrySetResult();
        RenamedEventHandler renamed = (_, _) => signal.TrySetResult();
        watcher.Created += notify;
        watcher.Renamed += renamed;
        try
        {
            while (!File.Exists(options.ReadyMarkerPath))
            {
                var now = clock.GetUtcNow();
                if (!PmsShadowFreshSlotHandoffContract.IsWithinAbsoluteStartDeadline(options.SlotCloseUtc, now))
                    throw new TimeoutException("HANDOFF_READY_MARKER_ABSOLUTE_DEADLINE_EXCEEDED");
                await Task.WhenAny(signal.Task,
                    Task.Delay(options.FallbackPollInterval, clock, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                signal = NewSignal();
            }

            var detected = clock.GetUtcNow();
            if (detected >= options.SlotCloseUtc)
                timeline.Record(PmsShadowFreshSlotHandoffEvents.SlotClosed);
            var marker = PmsShadowFreshSlotReadyMarkerStore.Read(options);
            timeline.Record(PmsShadowFreshSlotHandoffEvents.ReadyMarkerDetected, marker.ArtifactSha256);
            if (!PmsShadowFreshSlotHandoffContract.IsWithinAbsoluteStartDeadline(options.SlotCloseUtc, detected))
                throw new TimeoutException("HANDOFF_IMPORT_ABSOLUTE_DEADLINE_EXCEEDED");
            var importStarted = clock.GetUtcNow();
            timeline.Record(PmsShadowFreshSlotHandoffEvents.ImportProcessStarted, marker.ArtifactSha256);
            var observer = new PmsShadowFreshSlotTimelineImportObserver(options.SlotId, timeline);
            try
            {
                var classification = await import(marker, observer, cancellationToken);
                observer.Record(PmsShadowFreshSlotHandoffEvents.SlotClassified, options.SlotId,
                    marker.ArtifactSha256, classification);
                var result = new PmsShadowFreshSlotHandoffResult("COMPLETED", marker, detected,
                    importStarted, clock.GetUtcNow(), (detected - marker.CreatedAtUtc).TotalMilliseconds,
                    true, true);
                timeline.Record(PmsShadowFreshSlotHandoffEvents.ImportCompleted,
                    marker.ArtifactSha256, classification);
                PmsShadowFreshSlotHandoffTimeline.WriteCreateNew(options.CompletionPath,
                    JsonSerializer.SerializeToUtf8Bytes(result,
                        PmsShadowFreshSlotHandoffTimeline.JsonOptions));
                return result;
            }
            catch (Exception exception)
            {
                timeline.Record(PmsShadowFreshSlotHandoffEvents.ImportFailed, marker.ArtifactSha256,
                    exception.GetType().Name + ":" + exception.Message);
                throw;
            }
        }
        finally
        {
            watcher.Created -= notify;
            watcher.Renamed -= renamed;
            if (File.Exists(options.ArmedStatePath)) File.Delete(options.ArmedStatePath);
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CleanupCompleted);
        }
    }

    private void ValidateOptions()
    {
        PmsShadowIntradayCadenceContract.RequireUtc(options.SlotCloseUtc);
        if (options.FallbackPollInterval <= TimeSpan.Zero ||
            options.FallbackPollInterval > TimeSpan.FromSeconds(1))
            throw new InvalidDataException("HANDOFF_FALLBACK_POLL_INTERVAL_INVALID");
        if (options.RepositoryCommit.Length is not (40 or 64) ||
            options.RepositoryCommit.Any(x => !char.IsAsciiHexDigit(x) || char.IsUpper(x)))
            throw new InvalidDataException("HANDOFF_REPOSITORY_COMMIT_INVALID");
        if (PmsShadowIntradayCadenceContract.WindowEnding(options.SlotCloseUtc).SlotId != options.SlotId)
            throw new InvalidDataException("HANDOFF_SLOT_ID_MISMATCH");
    }

    private FileStream AcquireOwnership()
    {
        try
        {
            var stream = new FileStream(options.OwnershipPath, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.Read, 4096, FileOptions.WriteThrough | FileOptions.DeleteOnClose);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                contract_version = PmsShadowFreshSlotHandoffContract.Version,
                options.SlotId,
                options.RunId,
                owner_process_id = Environment.ProcessId,
                acquired_at_utc = clock.GetUtcNow(),
                no_order = true
            }, PmsShadowFreshSlotHandoffTimeline.JsonOptions);
            stream.Write(bytes);
            stream.Flush(true);
            return stream;
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("HANDOFF_ORCHESTRATOR_OWNERSHIP_CONFLICT", exception);
        }
    }

    private void WriteArmedState() =>
        PmsShadowFreshSlotHandoffTimeline.WriteCreateNew(options.ArmedStatePath,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                contract_version = PmsShadowFreshSlotHandoffContract.Version,
                options.SlotId,
                options.RunId,
                options.SlotCloseUtc,
                deadline_utc = options.SlotCloseUtc.AddSeconds(
                    PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds),
                watcher = "FILESYSTEM_WATCHER_WITH_SHORT_POLL_FALLBACK",
                fallback_poll_interval_ms = options.FallbackPollInterval.TotalMilliseconds,
                repository_commit = options.RepositoryCommit,
                environment = PmsShadowFreshSlotHandoffContract.Environment,
                prearmed_at_utc = clock.GetUtcNow(),
                no_order = true
            }, PmsShadowFreshSlotHandoffTimeline.JsonOptions));

    private PmsShadowFreshSlotHandoffResult ReadCompleted()
    {
        var result = JsonSerializer.Deserialize<PmsShadowFreshSlotHandoffResult>(
            File.ReadAllBytes(options.CompletionPath), PmsShadowFreshSlotHandoffTimeline.JsonOptions)
            ?? throw new InvalidDataException("HANDOFF_COMPLETION_INVALID");
        PmsShadowFreshSlotReadyMarkerStore.Validate(options, result.Marker, true);
        return result with { Status = "ALREADY_COMPLETED_IDENTICAL" };
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
