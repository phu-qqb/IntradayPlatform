using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace QQ.Production.Intraday.Application.CanonicalRecorder;

public static class CanonicalRecorderConstants
{
    public const string SchemaVersion = "canonical_recorder_envelope_v1";
    public const string Mode = "SHADOW_OFFLINE";
    public const string CapabilityAbsent = "ABSENT";

    public static readonly IReadOnlySet<string> SupportedEventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "RECORDER_RUN_STARTED",
        "RECORDER_RUN_STOPPED",
        "MARKET_DATA_RECEIVED",
        "BBO_UPDATED",
        "MARKET_DATA_GAP",
        "TARGET_OBSERVED",
        "TARGET_ACTIVATED",
        "TARGET_REVISED",
        "SHADOW_DECISION",
        "SHADOW_PARENT_INTENT",
        "SHADOW_CHILD_INTENT",
        "RISK_DECISION_OBSERVED",
        "POSITION_SNAPSHOT_OBSERVED",
        "HEALTH_EVENT",
        "CLOCK_EVENT",
        "WRITER_EVENT",
        "RECOVERY_EVENT"
    };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
}

public interface IRecorderClock
{
    DateTimeOffset UtcNow { get; }
    long MonotonicTicks { get; }
    long MonotonicFrequency => TimeSpan.TicksPerSecond;
    TimeSpan MonotonicElapsedSince(long startTicks)
        => TimeSpan.FromSeconds((double)(MonotonicTicks - startTicks) / MonotonicFrequency);
}

public sealed class SystemRecorderClock : IRecorderClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long MonotonicTicks => Stopwatch.GetTimestamp();
    public long MonotonicFrequency => Stopwatch.Frequency;
    public TimeSpan MonotonicElapsedSince(long startTicks)
        => Stopwatch.GetElapsedTime(startTicks, Stopwatch.GetTimestamp());
}

public sealed class ManualRecorderClock(DateTimeOffset utcNow, long monotonicTicks = 0) : IRecorderClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
    public long MonotonicTicks { get; private set; } = monotonicTicks;

    public void Advance(TimeSpan utcDelta, long ticksDelta = 1)
    {
        UtcNow = UtcNow.Add(utcDelta);
        MonotonicTicks += ticksDelta;
    }

    public void Set(DateTimeOffset utcNow, long monotonicTicks)
    {
        UtcNow = utcNow;
        MonotonicTicks = monotonicTicks;
    }
}

public sealed record CanonicalRecorderOptions(
    string RootPath,
    string RecorderRunId,
    string Environment,
    string ToolCommit,
    string ToolCommitSource,
    string SourceBaselineCommit,
    string ConfigHash,
    IReadOnlyList<string> ExpectedComponents,
    IReadOnlyList<string> ExpectedInstruments,
    int QueueCapacity = 4096,
    long RotateAfterBytes = 16 * 1024 * 1024,
    TimeSpan? RotateAfter = null,
    TimeSpan? FlushInterval = null,
    bool StartWriterWorker = true);

public sealed record CanonicalRecorderEvent(
    string EventType,
    string SourceComponent,
    string SourceContract,
    string SourceContractVersion,
    object Payload,
    string? SourceEntityId = null,
    string? SourceRunId = null,
    string? ModelRunId = null,
    string? TargetPositionId = null,
    string? ParentIntentId = null,
    string? ChildIntentId = null,
    string? InstrumentId = null,
    string? Symbol = null,
    string? Venue = null,
    DateTimeOffset? SourceTimestampUtc = null,
    DateTimeOffset? DecisionTime = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? Deadline = null,
    DateTimeOffset? TargetClose = null,
    string? SessionId = null,
    long? FixMsgSeqNum = null,
    bool? PossDup = null,
    DateTimeOffset? SendingTime = null,
    string? QuoteEventId = null,
    string? Instrument = null,
    decimal? BidPrice = null,
    decimal? BidQuantity = null,
    decimal? AskPrice = null,
    decimal? AskQuantity = null,
    int? DepthLevel = null,
    bool? BookValid = null,
    long? SourceReceiveSequence = null,
    string? EventId = null);

public sealed record CanonicalRecorderEnvelopeV1
{
    public required string SchemaVersion { get; init; }
    public required string RecorderRunId { get; init; }
    public required string EventId { get; init; }
    public required long ProcessEventSequence { get; init; }
    public required string EventType { get; init; }
    public required string Environment { get; init; }
    public required string SourceComponent { get; init; }
    public required string SourceContract { get; init; }
    public required string SourceContractVersion { get; init; }
    public string? SourceEntityId { get; init; }
    public string? SourceRunId { get; init; }
    public string? ModelRunId { get; init; }
    public string? TargetPositionId { get; init; }
    public string? ParentIntentId { get; init; }
    public string? ChildIntentId { get; init; }
    public string? InstrumentId { get; init; }
    public string? Symbol { get; init; }
    public string? Venue { get; init; }
    public DateTimeOffset? SourceTimestampUtc { get; init; }
    public required DateTimeOffset LocalReceiveUtc { get; init; }
    public required long LocalMonotonicTicks { get; init; }
    public required DateTimeOffset RecordedUtc { get; init; }
    public required string PayloadSha256 { get; init; }
    public required JsonElement PayloadJson { get; init; }
    public required string CodeCommit { get; init; }
    public required string ConfigHash { get; init; }
    public required string HostId { get; init; }
    public required int ProcessId { get; init; }
    public DateTimeOffset? DecisionTime { get; init; }
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public DateTimeOffset? TargetClose { get; init; }
    public string? SessionId { get; init; }
    public long? FixMsgSeqNum { get; init; }
    public bool? PossDup { get; init; }
    public DateTimeOffset? SendingTime { get; init; }
    public string? QuoteEventId { get; init; }
    public string? Instrument { get; init; }
    public decimal? BidPrice { get; init; }
    public decimal? BidQuantity { get; init; }
    public decimal? AskPrice { get; init; }
    public decimal? AskQuantity { get; init; }
    public int? DepthLevel { get; init; }
    public bool? BookValid { get; init; }
    public long? SourceReceiveSequence { get; init; }
}

public sealed record CanonicalRecorderChunkManifest(
    string File,
    long SizeBytes,
    string Sha256,
    long FirstSequence,
    long LastSequence,
    int EventCount,
    DateTimeOffset FinalizedUtc);

public sealed record CanonicalRecorderRunManifest(
    string RecorderSchemaVersion,
    string RecorderRunId,
    string Environment,
    string Mode,
    DateTimeOffset StartUtc,
    IReadOnlyList<string> ExpectedComponents,
    IReadOnlyList<string> ExpectedInstruments,
    string ToolCommit,
    string ToolCommitSource,
    string SourceBaselineCommit,
    string ConfigHash,
    string Host,
    string Os,
    string DotnetVersion,
    string RootPath,
    object RotationPolicy,
    object FlushPolicy,
    object QueuePolicy,
    string OrderEntryCapability,
    string NetworkCapability);

public sealed record CanonicalRecorderFinalManifest(
    string RecorderSchemaVersion,
    string RecorderRunId,
    string Environment,
    string Mode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool Finalized,
    IReadOnlyList<CanonicalRecorderChunkManifest> Chunks,
    IReadOnlyDictionary<string, long> EventCounts,
    long EventsEnqueued,
    long EventsWritten,
    long EventsRejected,
    long EventsDropped,
    long WriterErrors,
    long FlushCount,
    int QueueCapacity,
    int QueueHighWatermark,
    string RunManifestSha256,
    string DataQualityReportSha256);

public sealed record CanonicalRecorderDataQualityReport(
    string RunStatus,
    IReadOnlyDictionary<string, long> EventCounts,
    DateTimeOffset? FirstRecordedUtc,
    DateTimeOffset? LastRecordedUtc,
    long SequenceGapCount,
    long DuplicateEventCount,
    long ClockRegressionCount,
    long MarketDataGapCount,
    long InvalidBookCount,
    long StaleQuoteObservationCount,
    long WriterErrorCount,
    long DroppedEventCount,
    long RecoveredTailCount,
    long UnfinalizedChunkCount,
    string ManifestHashStatus,
    bool ShadowReady);

public sealed record CanonicalRecorderRecoveryReport(
    string Status,
    DateTimeOffset CheckedUtc,
    long UnfinalizedChunkCount,
    long RecoveredLineCount,
    long RecoveredTailCount,
    IReadOnlyList<string> RecoveredFiles);

public sealed record CanonicalRecorderHealthSnapshot(
    int QueueCapacity,
    int QueueHighWatermark,
    long EventsEnqueued,
    long EventsWritten,
    long EventsRejected,
    long EventsDropped,
    long WriterErrors,
    long FlushCount,
    DateTimeOffset? LastSuccessfulWriteUtc,
    bool Failed);

public sealed record CanonicalRecorderReplayReport(
    string Status,
    string RecorderRunId,
    int ChunkCount,
    long EventCount,
    string DeterministicReplayHash,
    IReadOnlyDictionary<string, long> EventCounts,
    string? FailureReason);

public sealed record CanonicalRecorderSyntheticRunResult(
    string RunRoot,
    CanonicalRecorderFinalManifest FinalManifest,
    CanonicalRecorderDataQualityReport DataQualityReport,
    CanonicalRecorderReplayReport ReplayReport);

public sealed class CanonicalRecorder : IAsyncDisposable
{
    private readonly CanonicalRecorderOptions options;
    private readonly IRecorderClock clock;
    private readonly string rootPath;
    private readonly string runRoot;
    private readonly string chunksRoot;
    private readonly string healthRoot;
    private readonly Channel<CanonicalRecorderEnvelopeV1> channel;
    private readonly ConcurrentDictionary<string, long> eventCounts = new(StringComparer.Ordinal);
    private readonly List<CanonicalRecorderChunkManifest> chunks = [];
    private readonly List<long> writtenSequences = [];
    private readonly object stateLock = new();
    private readonly string hostId = Environment.MachineName;
    private readonly CancellationTokenSource cancellation = new();
    private readonly TimeSpan rotateAfter;
    private readonly TimeSpan flushInterval;
    private Task? writerTask;
    private FileStream? stream;
    private StreamWriter? writer;
    private string? currentTmpPath;
    private string? currentFinalPath;
    private DateTimeOffset currentChunkStartedUtc;
    private long currentChunkFirstSequence;
    private long currentChunkLastSequence;
    private int currentChunkEventCount;
    private int chunkIndex;
    private long sequence;
    private DateTimeOffset? lastUtc;
    private long? lastTicks;
    private DateTimeOffset startUtc;
    private DateTimeOffset? firstRecordedUtc;
    private DateTimeOffset? lastRecordedUtc;
    private DateTimeOffset? lastSuccessfulWriteUtc;
    private int queuedApproximation;
    private int queueHighWatermark;
    private long eventsEnqueued;
    private long eventsWritten;
    private long eventsRejected;
    private long eventsDropped;
    private long writerErrors;
    private long flushCount;
    private long clockRegressionCount;
    private long marketDataGapCount;
    private long invalidBookCount;
    private long recoveredTailCount;
    private long unfinalizedChunkCount;
    private bool finalized;

    private CanonicalRecorder(CanonicalRecorderOptions options, IRecorderClock clock, string rootPath, string runRoot)
    {
        this.options = options;
        this.clock = clock;
        this.rootPath = rootPath;
        this.runRoot = runRoot;
        chunksRoot = Path.Combine(runRoot, "chunks");
        healthRoot = Path.Combine(runRoot, "health");
        rotateAfter = options.RotateAfter ?? TimeSpan.FromMinutes(5);
        flushInterval = options.FlushInterval ?? TimeSpan.FromSeconds(5);
        channel = Channel.CreateBounded<CanonicalRecorderEnvelopeV1>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public string RunRoot => runRoot;

    public CanonicalRecorderHealthSnapshot Health => new(
        options.QueueCapacity,
        queueHighWatermark,
        Interlocked.Read(ref eventsEnqueued),
        Interlocked.Read(ref eventsWritten),
        Interlocked.Read(ref eventsRejected),
        Interlocked.Read(ref eventsDropped),
        Interlocked.Read(ref writerErrors),
        Interlocked.Read(ref flushCount),
        lastSuccessfulWriteUtc,
        Interlocked.Read(ref eventsDropped) > 0 || Interlocked.Read(ref writerErrors) > 0 || clockRegressionCount > 0);

    public static async Task<CanonicalRecorder> CreateAsync(CanonicalRecorderOptions options, IRecorderClock clock, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new ArgumentException("Recorder root path is required.", nameof(options));
        }

        if (options.QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Queue capacity must be positive.");
        }

        var rootPath = Path.GetFullPath(options.RootPath);
        var safeEnvironment = SanitizePathSegment(options.Environment);
        var safeRunId = SanitizePathSegment(options.RecorderRunId);
        var date = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd");
        var runRoot = Path.GetFullPath(Path.Combine(rootPath, $"environment={safeEnvironment}", $"date={date}", $"recorder_run={safeRunId}"));
        EnsureWithinRoot(rootPath, runRoot);

        var recorder = new CanonicalRecorder(options, clock, rootPath, runRoot);
        Directory.CreateDirectory(recorder.chunksRoot);
        Directory.CreateDirectory(recorder.healthRoot);
        recorder.startUtc = clock.UtcNow;

        var recovery = await recorder.RecoverUnfinalizedChunksAsync(cancellationToken).ConfigureAwait(false);
        recorder.chunkIndex = recorder.FindMaxExistingChunkIndex();
        await recorder.WriteRunManifestAsync(cancellationToken).ConfigureAwait(false);

        if (options.StartWriterWorker)
        {
            recorder.writerTask = Task.Run(() => recorder.WriterLoopAsync(recorder.cancellation.Token), CancellationToken.None);
            if (recovery.RecoveredLineCount > 0 || recovery.RecoveredTailCount > 0 || recovery.UnfinalizedChunkCount > 0)
            {
                await recorder.RecordAsync(new CanonicalRecorderEvent(
                    "RECOVERY_EVENT",
                    "CanonicalRecorder",
                    "CanonicalRecorderRecoveryReport",
                    "v1",
                    recovery), cancellationToken).ConfigureAwait(false);
            }
        }

        return recorder;
    }

    public async Task<bool> RecordAsync(CanonicalRecorderEvent recorderEvent, CancellationToken cancellationToken = default)
    {
        if (!CanonicalRecorderConstants.SupportedEventTypes.Contains(recorderEvent.EventType))
        {
            Interlocked.Increment(ref eventsRejected);
            return false;
        }

        var now = clock.UtcNow;
        var ticks = clock.MonotonicTicks;
        List<CanonicalRecorderEnvelopeV1> envelopes = [];

        lock (stateLock)
        {
            if ((lastUtc.HasValue && now < lastUtc.Value) || (lastTicks.HasValue && ticks <= lastTicks.Value))
            {
                clockRegressionCount++;
                var clockSequence = ++sequence;
                envelopes.Add(CreateEnvelope(
                    new CanonicalRecorderEvent(
                        "CLOCK_EVENT",
                        "CanonicalRecorder",
                        "IRecorderClock",
                        "v1",
                        new
                        {
                            reason = "CLOCK_REGRESSION_OR_NON_MONOTONIC_TICKS",
                            previous_utc = lastUtc,
                            current_utc = now,
                            previous_monotonic_ticks = lastTicks,
                            current_monotonic_ticks = ticks
                        }),
                    clockSequence,
                    now,
                    ticks));
            }

            lastUtc = now;
            lastTicks = ticks;
            var eventSequence = ++sequence;
            envelopes.Add(CreateEnvelope(recorderEvent, eventSequence, now, ticks));
        }

        var accepted = true;
        foreach (var envelope in envelopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!options.StartWriterWorker)
            {
                Interlocked.Increment(ref eventsRejected);
                Interlocked.Increment(ref eventsDropped);
                accepted = false;
                continue;
            }

            if (!channel.Writer.TryWrite(envelope))
            {
                Interlocked.Increment(ref eventsRejected);
                Interlocked.Increment(ref eventsDropped);
                accepted = false;
                continue;
            }

            var queued = Interlocked.Increment(ref queuedApproximation);
            UpdateHighWatermark(queued);
            Interlocked.Increment(ref eventsEnqueued);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return accepted;
    }

    public async Task<CanonicalRecorderFinalManifest> CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (finalized)
        {
            throw new InvalidOperationException("Recorder already finalized.");
        }

        channel.Writer.TryComplete();
        if (writerTask is not null)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await writerTask.WaitAsync(linked.Token).ConfigureAwait(false);
        }

        await FinalizeCurrentChunkAsync(cancellationToken).ConfigureAwait(false);
        finalized = true;
        var quality = await WriteDataQualityReportAsync(finalized: true, cancellationToken).ConfigureAwait(false);
        var finalManifest = new CanonicalRecorderFinalManifest(
            CanonicalRecorderConstants.SchemaVersion,
            options.RecorderRunId,
            options.Environment,
            CanonicalRecorderConstants.Mode,
            startUtc,
            clock.UtcNow,
            true,
            chunks.ToArray(),
            eventCounts.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            Interlocked.Read(ref eventsEnqueued),
            Interlocked.Read(ref eventsWritten),
            Interlocked.Read(ref eventsRejected),
            Interlocked.Read(ref eventsDropped),
            Interlocked.Read(ref writerErrors),
            Interlocked.Read(ref flushCount),
            options.QueueCapacity,
            queueHighWatermark,
            Sha256File(Path.Combine(runRoot, "run_manifest.json")),
            Sha256File(Path.Combine(healthRoot, "data_quality_report.json")));

        await WriteJsonAtomicAsync(Path.Combine(runRoot, "final_manifest.json"), finalManifest, cancellationToken).ConfigureAwait(false);
        return finalManifest;
    }

    public async ValueTask DisposeAsync()
    {
        if (!finalized && options.StartWriterWorker)
        {
            await CompleteAsync().ConfigureAwait(false);
        }

        cancellation.Dispose();
    }

    internal static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(fs, value, CanonicalRecorderConstants.JsonOptions, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            fs.Flush(true);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path);
    }

    public static string Sha256Text(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string Sha256File(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void EnsureWithinRoot(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Recorder output path escapes configured root: {candidate}");
        }
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException("Path segment is required.", nameof(segment));
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private CanonicalRecorderEnvelopeV1 CreateEnvelope(CanonicalRecorderEvent recorderEvent, long eventSequence, DateTimeOffset now, long ticks)
    {
        var payloadJson = JsonSerializer.SerializeToElement(recorderEvent.Payload, CanonicalRecorderConstants.JsonOptions);
        var payloadText = JsonSerializer.Serialize(payloadJson, CanonicalRecorderConstants.JsonOptions);
        var sourceTimestamp = recorderEvent.SourceTimestampUtc;
        if (recorderEvent.EventType is "MARKET_DATA_GAP")
        {
            marketDataGapCount++;
        }

        if (recorderEvent.BookValid is false)
        {
            invalidBookCount++;
        }

        return new CanonicalRecorderEnvelopeV1
        {
            SchemaVersion = CanonicalRecorderConstants.SchemaVersion,
            RecorderRunId = options.RecorderRunId,
            EventId = recorderEvent.EventId ?? $"evt-{eventSequence:000000000000}",
            ProcessEventSequence = eventSequence,
            EventType = recorderEvent.EventType,
            Environment = options.Environment,
            SourceComponent = recorderEvent.SourceComponent,
            SourceContract = recorderEvent.SourceContract,
            SourceContractVersion = recorderEvent.SourceContractVersion,
            SourceEntityId = recorderEvent.SourceEntityId,
            SourceRunId = recorderEvent.SourceRunId,
            ModelRunId = recorderEvent.ModelRunId,
            TargetPositionId = recorderEvent.TargetPositionId,
            ParentIntentId = recorderEvent.ParentIntentId,
            ChildIntentId = recorderEvent.ChildIntentId,
            InstrumentId = recorderEvent.InstrumentId,
            Symbol = recorderEvent.Symbol,
            Venue = recorderEvent.Venue,
            SourceTimestampUtc = sourceTimestamp,
            LocalReceiveUtc = now,
            LocalMonotonicTicks = ticks,
            RecordedUtc = now,
            PayloadSha256 = Sha256Text(payloadText),
            PayloadJson = payloadJson,
            CodeCommit = options.ToolCommit,
            ConfigHash = options.ConfigHash,
            HostId = hostId,
            ProcessId = Environment.ProcessId,
            DecisionTime = recorderEvent.DecisionTime,
            EffectiveFrom = recorderEvent.EffectiveFrom,
            Deadline = recorderEvent.Deadline,
            TargetClose = recorderEvent.TargetClose,
            SessionId = recorderEvent.SessionId,
            FixMsgSeqNum = recorderEvent.FixMsgSeqNum,
            PossDup = recorderEvent.PossDup,
            SendingTime = recorderEvent.SendingTime,
            QuoteEventId = recorderEvent.QuoteEventId,
            Instrument = recorderEvent.Instrument,
            BidPrice = recorderEvent.BidPrice,
            BidQuantity = recorderEvent.BidQuantity,
            AskPrice = recorderEvent.AskPrice,
            AskQuantity = recorderEvent.AskQuantity,
            DepthLevel = recorderEvent.DepthLevel,
            BookValid = recorderEvent.BookValid,
            SourceReceiveSequence = recorderEvent.SourceReceiveSequence
        };
    }

    private async Task WriterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    Interlocked.Decrement(ref queuedApproximation);
                    await WriteEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Increment(ref writerErrors);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref writerErrors);
        }
    }

    private async Task WriteEnvelopeAsync(CanonicalRecorderEnvelopeV1 envelope, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(envelope, CanonicalRecorderConstants.JsonOptions);
        var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
        if (writer is null || ShouldRotateBeforeWrite(lineBytes, envelope.RecordedUtc))
        {
            await FinalizeCurrentChunkAsync(cancellationToken).ConfigureAwait(false);
            await OpenNextChunkAsync(envelope.ProcessEventSequence, envelope.RecordedUtc, cancellationToken).ConfigureAwait(false);
        }

        await writer!.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        currentChunkLastSequence = envelope.ProcessEventSequence;
        currentChunkEventCount++;
        lock (stateLock)
        {
            writtenSequences.Add(envelope.ProcessEventSequence);
            firstRecordedUtc ??= envelope.RecordedUtc;
            lastRecordedUtc = envelope.RecordedUtc;
        }

        eventCounts.AddOrUpdate(envelope.EventType, 1, (_, count) => count + 1);
        Interlocked.Increment(ref eventsWritten);
        lastSuccessfulWriteUtc = envelope.RecordedUtc;

        if (DateTimeOffset.UtcNow - (lastSuccessfulWriteUtc ?? DateTimeOffset.MinValue) >= flushInterval)
        {
            await FlushCurrentWriterAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldRotateBeforeWrite(int incomingLineBytes, DateTimeOffset now)
    {
        if (stream is null || currentChunkEventCount == 0)
        {
            return false;
        }

        if (stream.Length + incomingLineBytes > options.RotateAfterBytes)
        {
            return true;
        }

        return now - currentChunkStartedUtc >= rotateAfter;
    }

    private async Task OpenNextChunkAsync(long firstSequence, DateTimeOffset now, CancellationToken cancellationToken)
    {
        chunkIndex++;
        currentChunkStartedUtc = now;
        currentChunkFirstSequence = firstSequence;
        currentChunkLastSequence = firstSequence;
        currentChunkEventCount = 0;
        currentFinalPath = Path.Combine(chunksRoot, $"events-{chunkIndex:000000}.jsonl");
        currentTmpPath = currentFinalPath + ".tmp";
        stream = new FileStream(currentTmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.WriteThrough);
        writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task FinalizeCurrentChunkAsync(CancellationToken cancellationToken)
    {
        if (writer is null || stream is null || currentTmpPath is null || currentFinalPath is null)
        {
            return;
        }

        await FlushCurrentWriterAsync(cancellationToken).ConfigureAwait(false);
        await writer.DisposeAsync().ConfigureAwait(false);
        await stream.DisposeAsync().ConfigureAwait(false);
        writer = null;
        stream = null;

        if (File.Exists(currentFinalPath))
        {
            throw new InvalidOperationException($"Final chunk already exists: {currentFinalPath}");
        }

        File.Move(currentTmpPath, currentFinalPath);
        chunks.Add(new CanonicalRecorderChunkManifest(
            Path.GetRelativePath(runRoot, currentFinalPath),
            new FileInfo(currentFinalPath).Length,
            Sha256File(currentFinalPath),
            currentChunkFirstSequence,
            currentChunkLastSequence,
            currentChunkEventCount,
            clock.UtcNow));
        currentTmpPath = null;
        currentFinalPath = null;
    }

    private async Task FlushCurrentWriterAsync(CancellationToken cancellationToken)
    {
        if (writer is null || stream is null)
        {
            return;
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
        Interlocked.Increment(ref flushCount);
    }

    private async Task WriteRunManifestAsync(CancellationToken cancellationToken)
    {
        var manifest = new CanonicalRecorderRunManifest(
            CanonicalRecorderConstants.SchemaVersion,
            options.RecorderRunId,
            options.Environment,
            CanonicalRecorderConstants.Mode,
            startUtc,
            options.ExpectedComponents,
            options.ExpectedInstruments,
            options.ToolCommit,
            options.ToolCommitSource,
            options.SourceBaselineCommit,
            options.ConfigHash,
            hostId,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Environment.Version.ToString(),
            rootPath,
            new { rotate_after_bytes = options.RotateAfterBytes, rotate_after = rotateAfter.ToString() },
            new { flush_interval = flushInterval.ToString(), flush_to_disk_on_finalize = true },
            new { queue_capacity = options.QueueCapacity, full_mode = "FAIL_CLOSED_TRY_WRITE" },
            CanonicalRecorderConstants.CapabilityAbsent,
            CanonicalRecorderConstants.CapabilityAbsent);

        await WriteJsonAtomicAsync(Path.Combine(runRoot, "run_manifest.json"), manifest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CanonicalRecorderDataQualityReport> WriteDataQualityReportAsync(bool finalized, CancellationToken cancellationToken)
    {
        var sequences = writtenSequences.OrderBy(x => x).ToArray();
        long gapCount = 0;
        long duplicateCount = 0;
        for (var i = 1; i < sequences.Length; i++)
        {
            if (sequences[i] == sequences[i - 1])
            {
                duplicateCount++;
            }
            else if (sequences[i] > sequences[i - 1] + 1)
            {
                gapCount += sequences[i] - sequences[i - 1] - 1;
            }
        }

        var writerErrorCount = Interlocked.Read(ref writerErrors);
        var droppedEventCount = Interlocked.Read(ref eventsDropped);
        var report = new CanonicalRecorderDataQualityReport(
            finalized && droppedEventCount == 0 && writerErrorCount == 0 && gapCount == 0 && duplicateCount == 0 && clockRegressionCount == 0 ? "FINALIZED" : "FAILED",
            eventCounts.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            firstRecordedUtc,
            lastRecordedUtc,
            gapCount,
            duplicateCount,
            clockRegressionCount,
            marketDataGapCount,
            invalidBookCount,
            0,
            writerErrorCount,
            droppedEventCount,
            recoveredTailCount,
            unfinalizedChunkCount,
            "VALID",
            finalized && droppedEventCount == 0 && writerErrorCount == 0 && gapCount == 0 && duplicateCount == 0 && clockRegressionCount == 0);

        await WriteJsonAtomicAsync(Path.Combine(healthRoot, "data_quality_report.json"), report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private async Task<CanonicalRecorderRecoveryReport> RecoverUnfinalizedChunksAsync(CancellationToken cancellationToken)
    {
        var recoveredFiles = new List<string>();
        long recoveredLines = 0;
        long recoveredTails = 0;
        var tmpFiles = Directory.Exists(chunksRoot)
            ? Directory.GetFiles(chunksRoot, "*.tmp", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            : [];

        foreach (var tmpFile in tmpFiles)
        {
            unfinalizedChunkCount++;
            var finalPath = tmpFile[..^4];
            var validLines = new List<string>();
            var lines = await File.ReadAllLinesAsync(tmpFile, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var _ = JsonDocument.Parse(line);
                    validLines.Add(line);
                }
                catch (JsonException)
                {
                    recoveredTails++;
                    break;
                }
            }

            if (validLines.Count > 0 && !File.Exists(finalPath))
            {
                await File.WriteAllLinesAsync(finalPath, validLines, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                recoveredLines += validLines.Count;
                recoveredFiles.Add(Path.GetRelativePath(runRoot, finalPath));
            }

            var recoveredSource = tmpFile + ".recovered";
            if (File.Exists(recoveredSource))
            {
                File.Delete(recoveredSource);
            }

            File.Move(tmpFile, recoveredSource);
        }

        recoveredTailCount = recoveredTails;
        var report = new CanonicalRecorderRecoveryReport(
            tmpFiles.Length == 0 ? "NO_RECOVERY_REQUIRED" : "RECOVERED",
            clock.UtcNow,
            tmpFiles.Length,
            recoveredLines,
            recoveredTails,
            recoveredFiles);
        await WriteJsonAtomicAsync(Path.Combine(healthRoot, "recovery_report.json"), report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private int FindMaxExistingChunkIndex()
    {
        if (!Directory.Exists(chunksRoot))
        {
            return 0;
        }

        return Directory.GetFiles(chunksRoot, "events-*.jsonl", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name is not null && name.StartsWith("events-", StringComparison.Ordinal) && int.TryParse(name["events-".Length..], out var index) ? index : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private void UpdateHighWatermark(int queued)
    {
        while (true)
        {
            var current = queueHighWatermark;
            if (queued <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref queueHighWatermark, queued, current) == current)
            {
                return;
            }
        }
    }
}

public sealed class CanonicalRecorderReplayer
{
    public async Task<CanonicalRecorderReplayReport> ReplayAsync(string runRoot, CancellationToken cancellationToken = default)
    {
        try
        {
            var manifestPath = Path.Combine(runRoot, "final_manifest.json");
            if (!File.Exists(manifestPath))
            {
                return Failed("UNKNOWN", "final_manifest_missing");
            }

            await using var manifestStream = File.OpenRead(manifestPath);
            var manifest = (await JsonSerializer.DeserializeAsync<CanonicalRecorderFinalManifest>(
                manifestStream,
                CanonicalRecorderConstants.JsonOptions,
                cancellationToken).ConfigureAwait(false))!;

            var events = new List<CanonicalRecorderEnvelopeV1>();
            var seenSequences = new HashSet<long>();
            long previous = 0;
            foreach (var chunk in manifest.Chunks)
            {
                var path = Path.Combine(runRoot, chunk.File);
                if (!File.Exists(path))
                {
                    return Failed(manifest.RecorderRunId, $"missing_chunk:{chunk.File}");
                }

                var actualHash = CanonicalRecorder.Sha256File(path);
                if (!string.Equals(actualHash, chunk.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Failed(manifest.RecorderRunId, $"chunk_hash_mismatch:{chunk.File}");
                }

                var lineNumber = 0;
                foreach (var line in await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
                {
                    lineNumber++;
                    CanonicalRecorderEnvelopeV1 envelope;
                    try
                    {
                        envelope = JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV1>(line, CanonicalRecorderConstants.JsonOptions)
                            ?? throw new JsonException("null envelope");
                    }
                    catch (JsonException)
                    {
                        return Failed(manifest.RecorderRunId, $"invalid_json:{chunk.File}:{lineNumber}");
                    }

                    if (!string.Equals(envelope.SchemaVersion, CanonicalRecorderConstants.SchemaVersion, StringComparison.Ordinal))
                    {
                        return Failed(manifest.RecorderRunId, $"unknown_schema:{envelope.SchemaVersion}");
                    }

                    if (!CanonicalRecorderConstants.SupportedEventTypes.Contains(envelope.EventType))
                    {
                        return Failed(manifest.RecorderRunId, $"unknown_event_type:{envelope.EventType}");
                    }

                    if (!seenSequences.Add(envelope.ProcessEventSequence))
                    {
                        return Failed(manifest.RecorderRunId, $"duplicate_sequence:{envelope.ProcessEventSequence}");
                    }

                    if (previous != 0 && envelope.ProcessEventSequence <= previous)
                    {
                        return Failed(manifest.RecorderRunId, $"decreasing_sequence:{envelope.ProcessEventSequence}");
                    }

                    previous = envelope.ProcessEventSequence;
                    events.Add(envelope);
                }
            }

            var eventCounts = events
                .GroupBy(x => x.EventType, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => (long)x.Count(), StringComparer.Ordinal);
            var replayHash = ComputeDeterministicReplayHash(events);
            return new CanonicalRecorderReplayReport("PASS", manifest.RecorderRunId, manifest.Chunks.Count, events.Count, replayHash, eventCounts, null);
        }
        catch (Exception ex)
        {
            return Failed("UNKNOWN", ex.GetType().Name + ":" + ex.Message);
        }
    }

    public async Task<IReadOnlyList<CanonicalRecorderEnvelopeV1>> ReadEventsAsync(string runRoot, CancellationToken cancellationToken = default)
    {
        var report = await ReplayAsync(runRoot, cancellationToken).ConfigureAwait(false);
        if (report.Status != "PASS")
        {
            throw new InvalidOperationException(report.FailureReason);
        }

        await using var manifestStream = File.OpenRead(Path.Combine(runRoot, "final_manifest.json"));
        var manifest = (await JsonSerializer.DeserializeAsync<CanonicalRecorderFinalManifest>(
            manifestStream,
            CanonicalRecorderConstants.JsonOptions,
            cancellationToken).ConfigureAwait(false))!;

        var events = new List<CanonicalRecorderEnvelopeV1>();
        foreach (var chunk in manifest.Chunks)
        {
            foreach (var line in await File.ReadAllLinesAsync(Path.Combine(runRoot, chunk.File), Encoding.UTF8, cancellationToken).ConfigureAwait(false))
            {
                events.Add(JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV1>(line, CanonicalRecorderConstants.JsonOptions)!);
            }
        }

        return events;
    }

    public static string ComputeDeterministicReplayHash(IEnumerable<CanonicalRecorderEnvelopeV1> events)
    {
        var canonical = events
            .OrderBy(x => x.ProcessEventSequence)
            .Select(x => new
            {
                x.ProcessEventSequence,
                x.EventType,
                x.SourceComponent,
                x.SourceContract,
                x.SourceContractVersion,
                x.SourceEntityId,
                x.SourceRunId,
                x.ModelRunId,
                x.TargetPositionId,
                x.ParentIntentId,
                x.ChildIntentId,
                x.InstrumentId,
                x.Symbol,
                x.Venue,
                x.PayloadSha256,
                x.DecisionTime,
                x.EffectiveFrom,
                x.Deadline,
                x.TargetClose,
                x.QuoteEventId,
                x.BidPrice,
                x.AskPrice,
                x.BookValid
            });
        return CanonicalRecorder.Sha256Text(JsonSerializer.Serialize(canonical, CanonicalRecorderConstants.JsonOptions));
    }

    private static CanonicalRecorderReplayReport Failed(string runId, string reason)
        => new("FAIL", runId, 0, 0, string.Empty, new Dictionary<string, long>(), reason);
}

public static class CanonicalRecorderSyntheticScenario
{
    public static async Task<CanonicalRecorderSyntheticRunResult> RunAsync(
        string rootPath,
        string recorderRunId,
        string toolCommit,
        string sourceBaselineCommit,
        CancellationToken cancellationToken = default)
    {
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 6, 24, 8, 0, 0, TimeSpan.Zero), 1_000);
        var options = new CanonicalRecorderOptions(
            rootPath,
            recorderRunId,
            "M2A_SYNTHETIC_NO_LIVE",
            toolCommit,
            "UNVERIFIED_USER_SUPPLIED",
            sourceBaselineCommit,
            "m2a-synthetic-config-v1",
            ["MarketDataFixture", "AnubisTargetFixture", "ShadowDecisionFixture", "RiskFixture", "PositionFixture"],
            ["EURUSD"],
            QueueCapacity: 128,
            RotateAfterBytes: 4096,
            RotateAfter: TimeSpan.FromMinutes(1),
            FlushInterval: TimeSpan.FromMilliseconds(50));

        await using var recorder = await CanonicalRecorder.CreateAsync(options, clock, cancellationToken).ConfigureAwait(false);
        await recorder.RecordAsync(Event("RECORDER_RUN_STARTED", "CanonicalRecorder", "RecorderRun", new { recorder_run_id = recorderRunId }), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(10), 1);
        await recorder.RecordAsync(Event("BBO_UPDATED", "MarketDataFixture", "MarketDataSnapshot", new { bid = 1.1000m, ask = 1.1002m, mid = 1.1001m },
            symbol: "EURUSD", venue: "LMAX_DEMO", quoteEventId: "q-001", bid: 1.1000m, ask: 1.1002m, bookValid: true), cancellationToken).ConfigureAwait(false);

        var decision = new DateTimeOffset(2026, 6, 23, 21, 0, 0, TimeSpan.Zero);
        var effective = new DateTimeOffset(2026, 6, 24, 8, 15, 0, TimeSpan.Zero);
        var deadline = new DateTimeOffset(2026, 6, 24, 8, 30, 0, TimeSpan.Zero);
        var targetId = "target-eurusd-20260624-0830";
        clock.Advance(TimeSpan.FromMilliseconds(10), 1);
        await recorder.RecordAsync(Event("TARGET_OBSERVED", "AnubisTargetFixture", "TargetPosition", new { target_position_id = targetId, weight = 0.12m, target_qty = 1000000 },
            symbol: "EURUSD", targetPositionId: targetId, decision: decision, effective: effective, deadline: deadline, targetClose: deadline), cancellationToken).ConfigureAwait(false);

        clock.Set(effective, 1_100);
        await recorder.RecordAsync(Event("TARGET_ACTIVATED", "AnubisTargetFixture", "TargetPosition", new { target_position_id = targetId, activated = true },
            symbol: "EURUSD", targetPositionId: targetId, decision: decision, effective: effective, deadline: deadline, targetClose: deadline), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("SHADOW_DECISION", "ShadowExecutionFixture", "ShadowDecision", new { action = "PLAN_PARENT", reason = "effective_from_reached" },
            symbol: "EURUSD", targetPositionId: targetId, decision: decision, effective: effective, deadline: deadline, targetClose: deadline), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("SHADOW_PARENT_INTENT", "ShadowExecutionFixture", "ParentIntent", new { parent_intent_id = "parent-eurusd-001", side = "BUY", qty = 1000000 },
            symbol: "EURUSD", targetPositionId: targetId, parentIntentId: "parent-eurusd-001", decision: decision, effective: effective, deadline: deadline, targetClose: deadline), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("SHADOW_CHILD_INTENT", "ShadowExecutionFixture", "ChildIntent", new { child_intent_id = "child-eurusd-001", side = "BUY", qty = 250000, order_entry_capability = "ABSENT" },
            symbol: "EURUSD", targetPositionId: targetId, parentIntentId: "parent-eurusd-001", childIntentId: "child-eurusd-001", decision: decision, effective: effective, deadline: deadline, targetClose: deadline), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("BBO_UPDATED", "MarketDataFixture", "MarketDataSnapshot", new { bid = 1.1001m, ask = 1.1003m, mid = 1.1002m },
            symbol: "EURUSD", venue: "LMAX_DEMO", quoteEventId: "q-002", bid: 1.1001m, ask: 1.1003m, bookValid: true), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("RISK_DECISION_OBSERVED", "RiskFixture", "RiskDecision", new { status = "APPROVED_SHADOW_ONLY", no_order_entry = true },
            symbol: "EURUSD", targetPositionId: targetId, parentIntentId: "parent-eurusd-001", childIntentId: "child-eurusd-001"), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("POSITION_SNAPSHOT_OBSERVED", "PositionFixture", "PositionSnapshot", new { current_qty = 0, source = "synthetic_no_live" },
            symbol: "EURUSD", targetPositionId: targetId), cancellationToken).ConfigureAwait(false);
        clock.Advance(TimeSpan.FromMilliseconds(5), 1);
        await recorder.RecordAsync(Event("RECORDER_RUN_STOPPED", "CanonicalRecorder", "RecorderRun", new { recorder_run_id = recorderRunId, status = "STOPPED_CLEAN" }), cancellationToken).ConfigureAwait(false);

        var manifest = await recorder.CompleteAsync(cancellationToken).ConfigureAwait(false);
        await using var qualityStream = File.OpenRead(Path.Combine(recorder.RunRoot, "health", "data_quality_report.json"));
        var quality = (await JsonSerializer.DeserializeAsync<CanonicalRecorderDataQualityReport>(
            qualityStream,
            CanonicalRecorderConstants.JsonOptions,
            cancellationToken).ConfigureAwait(false))!;

        var replayer = new CanonicalRecorderReplayer();
        var replay = await replayer.ReplayAsync(recorder.RunRoot, cancellationToken).ConfigureAwait(false);
        await CanonicalRecorder.WriteJsonAtomicAsync(Path.Combine(recorder.RunRoot, "replay_report.json"), replay, cancellationToken).ConfigureAwait(false);
        await CanonicalRecorder.WriteJsonAtomicAsync(Path.Combine(recorder.RunRoot, "replay_events_hash.json"), new { replay.DeterministicReplayHash }, cancellationToken).ConfigureAwait(false);
        return new CanonicalRecorderSyntheticRunResult(recorder.RunRoot, manifest, quality, replay);
    }

    private static CanonicalRecorderEvent Event(
        string eventType,
        string sourceComponent,
        string sourceContract,
        object payload,
        string? symbol = null,
        string? venue = null,
        string? targetPositionId = null,
        string? parentIntentId = null,
        string? childIntentId = null,
        string? quoteEventId = null,
        decimal? bid = null,
        decimal? ask = null,
        bool? bookValid = null,
        DateTimeOffset? decision = null,
        DateTimeOffset? effective = null,
        DateTimeOffset? deadline = null,
        DateTimeOffset? targetClose = null)
        => new(
            eventType,
            sourceComponent,
            sourceContract,
            "v1",
            payload,
            SourceEntityId: targetPositionId ?? childIntentId ?? quoteEventId,
            TargetPositionId: targetPositionId,
            ParentIntentId: parentIntentId,
            ChildIntentId: childIntentId,
            Symbol: symbol,
            Venue: venue,
            DecisionTime: decision,
            EffectiveFrom: effective,
            Deadline: deadline,
            TargetClose: targetClose,
            QuoteEventId: quoteEventId,
            Instrument: symbol,
            BidPrice: bid,
            AskPrice: ask,
            BookValid: bookValid,
            SourceReceiveSequence: quoteEventId is null ? null : long.Parse(quoteEventId.Split('-').Last()));
}
