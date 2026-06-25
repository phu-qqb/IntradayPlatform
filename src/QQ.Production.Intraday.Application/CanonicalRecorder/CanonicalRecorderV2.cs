using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace QQ.Production.Intraday.Application.CanonicalRecorder;

public static class CanonicalRecorderV2Constants
{
    public const string EnvelopeSchemaVersion = "canonical_recorder_envelope_v2";
    public const string ManifestSchemaVersion = "canonical_recorder_manifest_v2";
    public const string ReplayHashVersion = "canonical_recorder_replay_hash_v2";
    public const string Mode = "SHADOW_OFFLINE";
    public const string CapabilityAbsent = "ABSENT";
    public const string NotEvaluated = "NOT_EVALUATED";

    public static readonly IReadOnlySet<string> SupportedEventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "RECORDER_RUN_STARTED",
        "RECORDER_RUN_STOPPED",
        "MARKET_DATA_RECEIVED",
        "BBO_UPDATED",
        "MARKET_DATA_GAP",
        "MODEL_WEIGHT_BATCH_OBSERVED",
        "MODEL_RUN_OBSERVED",
        "TARGET_WEIGHT_OBSERVED",
        "TARGET_POSITION_OBSERVED",
        "TARGET_OBSERVED",
        "TARGET_ACTIVATED",
        "TARGET_REVISED",
        "DRIFT_SNAPSHOT_OBSERVED",
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

public sealed record CanonicalRecorderV2Options(
    string RootPath,
    string RecorderRunId,
    string Environment,
    string ToolCommit,
    string ToolCommitSource,
    string SourceBaselineCommit,
    string ConfigHash,
    IReadOnlyList<string> ExpectedComponents,
    IReadOnlyList<string> ExpectedInstruments,
    IReadOnlyList<string> ExpectedFunds,
    IReadOnlyList<string> ExpectedStrategies,
    IReadOnlyList<string> ExpectedBooks,
    int QueueCapacity = 4096,
    long RotateAfterBytes = 16 * 1024 * 1024,
    TimeSpan? RotateAfter = null,
    TimeSpan? FlushInterval = null,
    bool StartWriterWorkerForTestsOnly = true,
    long? SimulatedWriterFailureAfterEventsForTestsOnly = null);

public sealed record CanonicalRecorderV2Event(
    string EventType,
    string SourceComponent,
    string SourceContract,
    string SourceContractVersion,
    object Payload,
    string? FundId = null,
    string? PortfolioId = null,
    string? StrategyId = null,
    string? StrategyRunId = null,
    string? StrategyVersion = null,
    string? BookId = null,
    string? CapitalAllocationId = null,
    string? BrokerAccountKey = null,
    string? NavRunId = null,
    string? ExecutionPolicyId = null,
    string? SourceEventId = null,
    long? SourceEventSequence = null,
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

public sealed record CanonicalRecorderEnvelopeV2
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
    public string? FundId { get; init; }
    public string? PortfolioId { get; init; }
    public string? StrategyId { get; init; }
    public string? StrategyRunId { get; init; }
    public string? StrategyVersion { get; init; }
    public string? BookId { get; init; }
    public string? CapitalAllocationId { get; init; }
    public string? BrokerAccountKey { get; init; }
    public string? NavRunId { get; init; }
    public string? ExecutionPolicyId { get; init; }
    public string? SourceEventId { get; init; }
    public long? SourceEventSequence { get; init; }
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

public sealed record CanonicalRecorderV2ChunkManifest(
    string File,
    long SizeBytes,
    string Sha256,
    long FirstSequence,
    long LastSequence,
    int EventCount,
    DateTimeOffset FinalizedUtc);

public sealed record CanonicalRecorderV2RunManifest(
    string RecorderManifestVersion,
    string RecorderRunId,
    string Environment,
    string Mode,
    DateTimeOffset StartUtc,
    IReadOnlyList<string> ExpectedComponents,
    IReadOnlyList<string> ExpectedInstruments,
    IReadOnlyList<string> ExpectedFunds,
    IReadOnlyList<string> ExpectedStrategies,
    IReadOnlyList<string> ExpectedBooks,
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

public sealed record CanonicalRecorderV2FinalManifest(
    string RecorderManifestVersion,
    string RecorderRunId,
    string Environment,
    string Mode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool Finalized,
    IReadOnlyList<CanonicalRecorderV2ChunkManifest> Chunks,
    IReadOnlyDictionary<string, long> EventCounts,
    IReadOnlyDictionary<string, long> EventCountsByStrategy,
    IReadOnlyDictionary<string, long> EventCountsByBook,
    long EventsEnqueued,
    long EventsWritten,
    long EventsRejected,
    long EventsDropped,
    long WriterErrors,
    long FlushCount,
    int QueueCapacity,
    int QueueHighWatermark,
    DateTimeOffset? LastFlushUtc,
    long LastFlushSequence,
    string RunManifestSha256,
    string DataQualityReportSha256,
    string WriterState,
    string? FailureReason);

public sealed record CanonicalRecorderV2DataQualityReport(
    string RunStatus,
    IReadOnlyDictionary<string, long> EventCounts,
    IReadOnlyDictionary<string, long> EventCountsByStrategy,
    IReadOnlyDictionary<string, long> EventCountsByBook,
    DateTimeOffset? FirstRecordedUtc,
    DateTimeOffset? LastRecordedUtc,
    long SequenceOutOfOrderCount,
    long DuplicateSequenceCount,
    long SequenceGapCount,
    long DuplicateEventIdCount,
    long ClockRegressionCount,
    long MarketDataGapCount,
    long InvalidBookCount,
    string StaleQuoteObservationStatus,
    long WriterErrorCount,
    long DroppedEventCount,
    long RecoveredTailCount,
    long UnfinalizedChunkCount,
    long EnqueuedWrittenMismatch,
    long PayloadHashFailureCount,
    string ManifestValidationStatus,
    DateTimeOffset? LastFlushUtc,
    long LastFlushSequence,
    bool ShadowReady,
    string? FailureReason);

public sealed record CanonicalRecorderV2RecoveryReport(
    string Status,
    DateTimeOffset CheckedUtc,
    long ExistingFinalizedChunkCount,
    long UnfinalizedChunkCount,
    long RecoveredLineCount,
    long RecoveredTailCount,
    long RestoredLastSequence,
    IReadOnlyList<string> RecoveredFiles,
    string? Incident);

public sealed record CanonicalRecorderV2ReplayReport(
    string Status,
    string ReplayHashVersion,
    string RecorderRunId,
    int ChunkCount,
    long EventCount,
    string DeterministicReplayHash,
    IReadOnlyDictionary<string, long> EventCounts,
    string? FailureReason);

public sealed record CanonicalRecorderV2HealthSnapshot(
    int QueueCapacity,
    int QueueHighWatermark,
    long EventsEnqueued,
    long EventsWritten,
    long EventsRejected,
    long EventsDropped,
    long WriterErrors,
    long FlushCount,
    DateTimeOffset? LastFlushUtc,
    long LastFlushSequence,
    string WriterState,
    string? FailureReason,
    bool Failed);

public sealed class CanonicalRecorderV2 : IAsyncDisposable
{
    private readonly CanonicalRecorderV2Options options;
    private readonly IRecorderClock clock;
    private readonly string rootPath;
    private readonly string runRoot;
    private readonly string chunksRoot;
    private readonly string healthRoot;
    private readonly Channel<CanonicalRecorderEnvelopeV2> channel;
    private readonly object stateLock = new();
    private readonly string hostId = Environment.MachineName;
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim writerIoLock = new(1, 1);
    private readonly List<CanonicalRecorderV2ChunkManifest> chunks = [];
    private readonly List<long> writtenSequenceOrder = [];
    private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> eventCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> eventCountsByStrategy = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> eventCountsByBook = new(StringComparer.Ordinal);
    private readonly TimeSpan rotateAfter;
    private readonly TimeSpan flushInterval;
    private FileStream? runLock;
    private Task? writerTask;
    private FileStream? stream;
    private StreamWriter? writer;
    private string? currentTmpPath;
    private string? currentFinalPath;
    private DateTimeOffset startUtc;
    private DateTimeOffset currentChunkStartedUtc;
    private long currentChunkFirstSequence;
    private long currentChunkLastSequence;
    private int currentChunkEventCount;
    private int chunkIndex;
    private long sequence;
    private long lastAssignedSequence;
    private DateTimeOffset? lastUtc;
    private long? lastTicks;
    private DateTimeOffset? firstRecordedUtc;
    private DateTimeOffset? lastRecordedUtc;
    private DateTimeOffset? lastFlushUtc;
    private long lastFlushMonotonicTicks;
    private long lastFlushSequence;
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
    private bool writerFailed;
    private string? failureReason;

    private CanonicalRecorderV2(CanonicalRecorderV2Options options, IRecorderClock clock, string rootPath, string runRoot)
    {
        this.options = options;
        this.clock = clock;
        this.rootPath = rootPath;
        this.runRoot = runRoot;
        chunksRoot = Path.Combine(runRoot, "chunks");
        healthRoot = Path.Combine(runRoot, "health");
        rotateAfter = options.RotateAfter ?? TimeSpan.FromMinutes(5);
        flushInterval = options.FlushInterval ?? TimeSpan.FromSeconds(5);
        channel = Channel.CreateBounded<CanonicalRecorderEnvelopeV2>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public string RunRoot => runRoot;

    public CanonicalRecorderV2HealthSnapshot Health => new(
        options.QueueCapacity,
        queueHighWatermark,
        Interlocked.Read(ref eventsEnqueued),
        Interlocked.Read(ref eventsWritten),
        Interlocked.Read(ref eventsRejected),
        Interlocked.Read(ref eventsDropped),
        Interlocked.Read(ref writerErrors),
        Interlocked.Read(ref flushCount),
        lastFlushUtc,
        lastFlushSequence,
        writerFailed ? "FAILED" : "OK",
        failureReason,
        writerFailed || Interlocked.Read(ref eventsDropped) > 0 || Interlocked.Read(ref writerErrors) > 0 || clockRegressionCount > 0);

    public static async Task<CanonicalRecorderV2> CreateAsync(CanonicalRecorderV2Options options, IRecorderClock clock, CancellationToken cancellationToken = default)
    {
        if (options.QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Queue capacity must be positive.");
        }

        var root = Path.GetFullPath(options.RootPath);
        var runRoot = Path.GetFullPath(Path.Combine(root, $"environment={Sanitize(options.Environment)}", $"date={clock.UtcNow.UtcDateTime:yyyy-MM-dd}", $"recorder_run={Sanitize(options.RecorderRunId)}"));
        EnsureWithinRoot(root, runRoot);
        Directory.CreateDirectory(runRoot);

        if (File.Exists(Path.Combine(runRoot, "final_manifest.json")))
        {
            throw new InvalidOperationException("Finalized recorder run cannot be reopened.");
        }

        var recorder = new CanonicalRecorderV2(options, clock, root, runRoot);
        recorder.runLock = new FileStream(Path.Combine(runRoot, "recorder.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Directory.CreateDirectory(recorder.chunksRoot);
        Directory.CreateDirectory(recorder.healthRoot);
        recorder.startUtc = clock.UtcNow;

        var recovery = await recorder.RecoverAndRestoreAsync(cancellationToken).ConfigureAwait(false);
        await recorder.WriteRunManifestIfMissingOrMatchingAsync(cancellationToken).ConfigureAwait(false);

        if (options.StartWriterWorkerForTestsOnly)
        {
            recorder.writerTask = Task.Run(() => recorder.WriterLoopAsync(recorder.cancellation.Token), CancellationToken.None);
            if (recovery.UnfinalizedChunkCount > 0)
            {
                await recorder.RecordAsync(new CanonicalRecorderV2Event(
                    "RECOVERY_EVENT",
                    "CanonicalRecorderV2",
                    "CanonicalRecorderV2RecoveryReport",
                    "v2",
                    recovery), cancellationToken).ConfigureAwait(false);
            }
        }

        return recorder;
    }

    public async Task<bool> RecordAsync(CanonicalRecorderV2Event recorderEvent, CancellationToken cancellationToken = default)
    {
        CanonicalRecorderEnvelopeV2 envelope;
        lock (stateLock)
        {
            if (writerFailed)
            {
                Interlocked.Increment(ref eventsRejected);
                return false;
            }

            if (!CanonicalRecorderV2Constants.SupportedEventTypes.Contains(recorderEvent.EventType))
            {
                Interlocked.Increment(ref eventsRejected);
                return false;
            }

            if (!HasRequiredDimensions(recorderEvent))
            {
                Interlocked.Increment(ref eventsRejected);
                failureReason = "missing_required_fund_strategy_book_dimensions";
                return false;
            }

            var now = clock.UtcNow;
            var ticks = clock.MonotonicTicks;
            if ((lastUtc.HasValue && now < lastUtc.Value) || (lastTicks.HasValue && ticks <= lastTicks.Value))
            {
                clockRegressionCount++;
            }

            lastUtc = now;
            lastTicks = ticks;
            envelope = CreateEnvelope(recorderEvent, ++sequence, now, ticks);
            if (!eventIds.Add(envelope.EventId))
            {
                Interlocked.Increment(ref eventsRejected);
                failureReason = "duplicate_event_id";
                return false;
            }

            if (envelope.ProcessEventSequence <= lastAssignedSequence)
            {
                Interlocked.Increment(ref eventsRejected);
                failureReason = "non_monotone_sequence_assignment";
                return false;
            }

            lastAssignedSequence = envelope.ProcessEventSequence;
            if (!options.StartWriterWorkerForTestsOnly)
            {
                Interlocked.Increment(ref eventsRejected);
                Interlocked.Increment(ref eventsDropped);
                writerFailed = true;
                failureReason = "writer_worker_disabled_test_fail_closed";
                return false;
            }

            if (!channel.Writer.TryWrite(envelope))
            {
                Interlocked.Increment(ref eventsRejected);
                Interlocked.Increment(ref eventsDropped);
                writerFailed = true;
                failureReason = "bounded_queue_saturated";
                return false;
            }

            var queued = Interlocked.Increment(ref queuedApproximation);
            UpdateHighWatermark(queued);
            Interlocked.Increment(ref eventsEnqueued);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return true;
    }

    public async Task FlushCheckpointAsync(CancellationToken cancellationToken = default)
    {
        while (Interlocked.Read(ref eventsWritten) < Interlocked.Read(ref eventsEnqueued) && !writerFailed)
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        }

        await FlushCurrentWriterAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CanonicalRecorderV2FinalManifest> CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (finalized)
        {
            throw new InvalidOperationException("Recorder already finalized.");
        }

        channel.Writer.TryComplete();
        if (writerTask is not null)
        {
            await writerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await FinalizeCurrentChunkAsync(cancellationToken).ConfigureAwait(false);
        finalized = true;
        var quality = await WriteDataQualityReportAsync(cancellationToken).ConfigureAwait(false);
        var manifest = new CanonicalRecorderV2FinalManifest(
            CanonicalRecorderV2Constants.ManifestSchemaVersion,
            options.RecorderRunId,
            options.Environment,
            CanonicalRecorderV2Constants.Mode,
            startUtc,
            clock.UtcNow,
            true,
            chunks.ToArray(),
            eventCounts.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            eventCountsByStrategy.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            eventCountsByBook.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            Interlocked.Read(ref eventsEnqueued),
            Interlocked.Read(ref eventsWritten),
            Interlocked.Read(ref eventsRejected),
            Interlocked.Read(ref eventsDropped),
            Interlocked.Read(ref writerErrors),
            Interlocked.Read(ref flushCount),
            options.QueueCapacity,
            queueHighWatermark,
            lastFlushUtc,
            lastFlushSequence,
            Sha256File(Path.Combine(runRoot, "run_manifest.json")),
            Sha256File(Path.Combine(healthRoot, "data_quality_report.json")),
            writerFailed ? "FAILED" : "OK",
            failureReason);
        await WriteJsonAtomicAsync(Path.Combine(runRoot, "final_manifest.json"), manifest, cancellationToken).ConfigureAwait(false);
        return manifest;
    }

    public async ValueTask DisposeAsync()
    {
        if (!finalized && !writerFailed && options.StartWriterWorkerForTestsOnly)
        {
            await CompleteAsync().ConfigureAwait(false);
        }

        stream?.Dispose();
        writer?.Dispose();
        runLock?.Dispose();
        cancellation.Dispose();
    }

    public static string Sha256Text(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string Sha256File(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(fs, value, CanonicalRecorderV2Constants.JsonOptions, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            fs.Flush(true);
        }

        if (File.Exists(path))
        {
            File.Replace(tmp, path, null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    private static bool HasRequiredDimensions(CanonicalRecorderV2Event e)
    {
        var requires = e.EventType is
            "MODEL_WEIGHT_BATCH_OBSERVED" or
            "MODEL_RUN_OBSERVED" or
            "TARGET_WEIGHT_OBSERVED" or
            "TARGET_POSITION_OBSERVED" or
            "TARGET_OBSERVED" or
            "TARGET_ACTIVATED" or
            "TARGET_REVISED" or
            "DRIFT_SNAPSHOT_OBSERVED" or
            "SHADOW_DECISION" or
            "SHADOW_PARENT_INTENT" or
            "SHADOW_CHILD_INTENT" or
            "RISK_DECISION_OBSERVED" or
            "POSITION_SNAPSHOT_OBSERVED";
        return !requires || (!string.IsNullOrWhiteSpace(e.FundId) && !string.IsNullOrWhiteSpace(e.StrategyId) && !string.IsNullOrWhiteSpace(e.BookId));
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

    private static void EnsureWithinRoot(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Recorder output path escapes configured root.");
        }
    }

    private CanonicalRecorderEnvelopeV2 CreateEnvelope(CanonicalRecorderV2Event e, long eventSequence, DateTimeOffset now, long ticks)
    {
        var payloadJson = JsonSerializer.SerializeToElement(e.Payload, CanonicalRecorderV2Constants.JsonOptions);
        var payloadText = JsonSerializer.Serialize(payloadJson, CanonicalRecorderV2Constants.JsonOptions);
        if (e.EventType == "MARKET_DATA_GAP")
        {
            marketDataGapCount++;
        }

        if (e.BookValid is false)
        {
            invalidBookCount++;
        }

        return new CanonicalRecorderEnvelopeV2
        {
            SchemaVersion = CanonicalRecorderV2Constants.EnvelopeSchemaVersion,
            RecorderRunId = options.RecorderRunId,
            EventId = e.EventId ?? $"evt-{eventSequence:000000000000}",
            ProcessEventSequence = eventSequence,
            EventType = e.EventType,
            Environment = options.Environment,
            SourceComponent = e.SourceComponent,
            SourceContract = e.SourceContract,
            SourceContractVersion = e.SourceContractVersion,
            FundId = e.FundId,
            PortfolioId = e.PortfolioId,
            StrategyId = e.StrategyId,
            StrategyRunId = e.StrategyRunId,
            StrategyVersion = e.StrategyVersion,
            BookId = e.BookId,
            CapitalAllocationId = e.CapitalAllocationId,
            BrokerAccountKey = e.BrokerAccountKey,
            NavRunId = e.NavRunId,
            ExecutionPolicyId = e.ExecutionPolicyId,
            SourceEventId = e.SourceEventId,
            SourceEventSequence = e.SourceEventSequence,
            SourceEntityId = e.SourceEntityId,
            SourceRunId = e.SourceRunId,
            ModelRunId = e.ModelRunId,
            TargetPositionId = e.TargetPositionId,
            ParentIntentId = e.ParentIntentId,
            ChildIntentId = e.ChildIntentId,
            InstrumentId = e.InstrumentId,
            Symbol = e.Symbol,
            Venue = e.Venue,
            SourceTimestampUtc = e.SourceTimestampUtc,
            LocalReceiveUtc = now,
            LocalMonotonicTicks = ticks,
            RecordedUtc = now,
            PayloadSha256 = Sha256Text(payloadText),
            PayloadJson = payloadJson,
            CodeCommit = options.ToolCommit,
            ConfigHash = options.ConfigHash,
            HostId = hostId,
            ProcessId = Environment.ProcessId,
            DecisionTime = e.DecisionTime,
            EffectiveFrom = e.EffectiveFrom,
            Deadline = e.Deadline,
            TargetClose = e.TargetClose,
            SessionId = e.SessionId,
            FixMsgSeqNum = e.FixMsgSeqNum,
            PossDup = e.PossDup,
            SendingTime = e.SendingTime,
            QuoteEventId = e.QuoteEventId,
            Instrument = e.Instrument,
            BidPrice = e.BidPrice,
            BidQuantity = e.BidQuantity,
            AskPrice = e.AskPrice,
            AskQuantity = e.AskQuantity,
            DepthLevel = e.DepthLevel,
            BookValid = e.BookValid,
            SourceReceiveSequence = e.SourceReceiveSequence
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
                    if (options.SimulatedWriterFailureAfterEventsForTestsOnly.HasValue &&
                        Interlocked.Read(ref eventsWritten) >= options.SimulatedWriterFailureAfterEventsForTestsOnly.Value)
                    {
                        throw new IOException("simulated_writer_failure");
                    }

                    await WriteEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref writerErrors);
                    writerFailed = true;
                    failureReason = ex.Message;
                    channel.Writer.TryComplete(ex);
                    break;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            Interlocked.Increment(ref writerErrors);
            writerFailed = true;
            failureReason = ex.Message;
        }
    }

    private async Task WriteEnvelopeAsync(CanonicalRecorderEnvelopeV2 envelope, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(envelope, CanonicalRecorderV2Constants.JsonOptions);
        var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
        if (writer is null || ShouldRotateBeforeWrite(lineBytes, envelope.RecordedUtc))
        {
            await FinalizeCurrentChunkAsync(cancellationToken).ConfigureAwait(false);
            await OpenNextChunkAsync(envelope.ProcessEventSequence, envelope.RecordedUtc).ConfigureAwait(false);
        }

        await writer!.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        currentChunkLastSequence = envelope.ProcessEventSequence;
        currentChunkEventCount++;
        writtenSequenceOrder.Add(envelope.ProcessEventSequence);
        firstRecordedUtc ??= envelope.RecordedUtc;
        lastRecordedUtc = envelope.RecordedUtc;
        eventCounts.AddOrUpdate(envelope.EventType, 1, (_, c) => c + 1);
        if (!string.IsNullOrWhiteSpace(envelope.StrategyId))
        {
            eventCountsByStrategy.AddOrUpdate(envelope.StrategyId, 1, (_, c) => c + 1);
        }

        if (!string.IsNullOrWhiteSpace(envelope.BookId))
        {
            eventCountsByBook.AddOrUpdate(envelope.BookId, 1, (_, c) => c + 1);
        }

        Interlocked.Increment(ref eventsWritten);
        if (clock.MonotonicTicks - lastFlushMonotonicTicks >= flushInterval.Ticks)
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

        return stream.Length + incomingLineBytes > options.RotateAfterBytes || now - currentChunkStartedUtc >= rotateAfter;
    }

    private Task OpenNextChunkAsync(long firstSequence, DateTimeOffset now)
    {
        chunkIndex++;
        currentChunkStartedUtc = now;
        currentChunkFirstSequence = firstSequence;
        currentChunkLastSequence = firstSequence;
        currentChunkEventCount = 0;
        var relative = $"chunks/events-{chunkIndex:000000}.jsonl";
        currentFinalPath = Path.Combine(runRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        currentTmpPath = currentFinalPath + ".tmp";
        stream = new FileStream(currentTmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.WriteThrough);
        writer = new StreamWriter(stream, new UTF8Encoding(false));
        return Task.CompletedTask;
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
        File.Move(currentTmpPath, currentFinalPath);
        var relative = Path.GetRelativePath(runRoot, currentFinalPath).Replace('\\', '/');
        chunks.Add(new CanonicalRecorderV2ChunkManifest(
            relative,
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
        await writerIoLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (writer is null || stream is null)
            {
                return;
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(true);
            lastFlushUtc = clock.UtcNow;
            lastFlushMonotonicTicks = clock.MonotonicTicks;
            lastFlushSequence = Math.Max(lastFlushSequence, currentChunkLastSequence);
            Interlocked.Increment(ref flushCount);
        }
        finally
        {
            writerIoLock.Release();
        }
    }

    private async Task WriteRunManifestIfMissingOrMatchingAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(runRoot, "run_manifest.json");
        if (File.Exists(path))
        {
            await using var existingStream = File.OpenRead(path);
            var existing = await JsonSerializer.DeserializeAsync<CanonicalRecorderV2RunManifest>(existingStream, CanonicalRecorderV2Constants.JsonOptions, cancellationToken).ConfigureAwait(false);
            if (existing?.RecorderRunId != options.RecorderRunId || existing.Environment != options.Environment || existing.ConfigHash != options.ConfigHash)
            {
                throw new InvalidOperationException("Existing run manifest provenance does not match recorder options.");
            }

            return;
        }

        var manifest = new CanonicalRecorderV2RunManifest(
            CanonicalRecorderV2Constants.ManifestSchemaVersion,
            options.RecorderRunId,
            options.Environment,
            CanonicalRecorderV2Constants.Mode,
            startUtc,
            options.ExpectedComponents,
            options.ExpectedInstruments,
            options.ExpectedFunds,
            options.ExpectedStrategies,
            options.ExpectedBooks,
            options.ToolCommit,
            options.ToolCommitSource,
            options.SourceBaselineCommit,
            options.ConfigHash,
            hostId,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Environment.Version.ToString(),
            rootPath,
            new { rotate_after_bytes = options.RotateAfterBytes, rotate_after = rotateAfter.ToString() },
            new { flush_interval = flushInterval.ToString(), flush_to_disk_on_checkpoint = true },
            new { queue_capacity = options.QueueCapacity, full_mode = "FAIL_CLOSED_TRY_WRITE" },
            CanonicalRecorderV2Constants.CapabilityAbsent,
            CanonicalRecorderV2Constants.CapabilityAbsent);
        await WriteJsonAtomicAsync(path, manifest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CanonicalRecorderV2RecoveryReport> RecoverAndRestoreAsync(CancellationToken cancellationToken)
    {
        var recoveredFiles = new List<string>();
        long recoveredLineCount = 0;
        long recoveredTail = 0;
        var finalizedChunks = Directory.Exists(chunksRoot)
            ? Directory.GetFiles(chunksRoot, "events-*.jsonl", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            : [];
        foreach (var finalizedChunk in finalizedChunks)
        {
            RestoreFinalizedChunk(finalizedChunk);
        }

        var tmpFiles = Directory.Exists(chunksRoot)
            ? Directory.GetFiles(chunksRoot, "events-*.jsonl.tmp", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            : [];
        foreach (var tmp in tmpFiles)
        {
            unfinalizedChunkCount++;
            var lines = await File.ReadAllLinesAsync(tmp, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var validLines = new List<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var envelope = JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!;
                    RestoreEnvelopeState(envelope);
                    validLines.Add(line);
                }
                catch (JsonException)
                {
                    recoveredTail++;
                    break;
                }
            }

            if (validLines.Count > 0)
            {
                var final = tmp[..^4];
                await File.WriteAllLinesAsync(final, validLines, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                chunks.Add(BuildChunkManifest(final, validLines.Select(line => JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!).ToArray()));
                recoveredLineCount += validLines.Count;
                recoveredFiles.Add(Path.GetRelativePath(runRoot, final).Replace('\\', '/'));
            }

            File.Move(tmp, tmp + ".recovered", overwrite: true);
        }

        recoveredTailCount = recoveredTail;
        chunkIndex = chunks
            .Select(x => Path.GetFileNameWithoutExtension(x.File))
            .Select(name => name.StartsWith("events-", StringComparison.Ordinal) && int.TryParse(name["events-".Length..], out var index) ? index : 0)
            .DefaultIfEmpty(0)
            .Max();
        sequence = Math.Max(sequence, writtenSequenceOrder.DefaultIfEmpty(0).Max());
        lastAssignedSequence = sequence;
        if (recoveredTail > 0)
        {
            writerFailed = true;
            failureReason = "recovered_corrupt_tail";
        }

        var report = new CanonicalRecorderV2RecoveryReport(
            tmpFiles.Length == 0 ? "NO_RECOVERY_REQUIRED" : recoveredTail > 0 ? "RECOVERED_WITH_TRUNCATED_TAIL" : "RECOVERED",
            clock.UtcNow,
            finalizedChunks.Length,
            tmpFiles.Length,
            recoveredLineCount,
            recoveredTail,
            sequence,
            recoveredFiles,
            recoveredTail > 0 ? "corrupt_tail_truncated" : null);
        await WriteJsonAtomicAsync(Path.Combine(healthRoot, "recovery_report.json"), report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private void RestoreFinalizedChunk(string path)
    {
        var envelopes = File.ReadAllLines(path, Encoding.UTF8)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!)
            .ToArray();
        foreach (var envelope in envelopes)
        {
            RestoreEnvelopeState(envelope);
        }

        chunks.Add(BuildChunkManifest(path, envelopes));
    }

    private CanonicalRecorderV2ChunkManifest BuildChunkManifest(string path, IReadOnlyList<CanonicalRecorderEnvelopeV2> envelopes)
        => new(
            Path.GetRelativePath(runRoot, path).Replace('\\', '/'),
            new FileInfo(path).Length,
            Sha256File(path),
            envelopes.Min(x => x.ProcessEventSequence),
            envelopes.Max(x => x.ProcessEventSequence),
            envelopes.Count,
            clock.UtcNow);

    private void RestoreEnvelopeState(CanonicalRecorderEnvelopeV2 envelope)
    {
        writtenSequenceOrder.Add(envelope.ProcessEventSequence);
        Interlocked.Increment(ref eventsWritten);
        Interlocked.Increment(ref eventsEnqueued);
        eventIds.Add(envelope.EventId);
        eventCounts.AddOrUpdate(envelope.EventType, 1, (_, c) => c + 1);
        if (!string.IsNullOrWhiteSpace(envelope.StrategyId))
        {
            eventCountsByStrategy.AddOrUpdate(envelope.StrategyId, 1, (_, c) => c + 1);
        }

        if (!string.IsNullOrWhiteSpace(envelope.BookId))
        {
            eventCountsByBook.AddOrUpdate(envelope.BookId, 1, (_, c) => c + 1);
        }

        firstRecordedUtc = firstRecordedUtc is null || envelope.RecordedUtc < firstRecordedUtc ? envelope.RecordedUtc : firstRecordedUtc;
        lastRecordedUtc = lastRecordedUtc is null || envelope.RecordedUtc > lastRecordedUtc ? envelope.RecordedUtc : lastRecordedUtc;
    }

    private async Task<CanonicalRecorderV2DataQualityReport> WriteDataQualityReportAsync(CancellationToken cancellationToken)
    {
        var physical = writtenSequenceOrder.ToArray();
        long outOfOrder = 0;
        long duplicateSequence = 0;
        long gap = 0;
        var seen = new HashSet<long>();
        for (var i = 0; i < physical.Length; i++)
        {
            if (!seen.Add(physical[i]))
            {
                duplicateSequence++;
            }

            if (i > 0)
            {
                if (physical[i] <= physical[i - 1])
                {
                    outOfOrder++;
                }
                else if (physical[i] > physical[i - 1] + 1)
                {
                    gap += physical[i] - physical[i - 1] - 1;
                }
            }
        }

        var mismatch = Math.Abs(Interlocked.Read(ref eventsEnqueued) - Interlocked.Read(ref eventsWritten));
        var payloadFailures = ValidatePayloadHashes();
        var writerErrorCount = Interlocked.Read(ref writerErrors);
        var dropped = Interlocked.Read(ref eventsDropped);
        var shadowReady = !writerFailed && dropped == 0 && writerErrorCount == 0 && outOfOrder == 0 && duplicateSequence == 0 &&
                          gap == 0 && clockRegressionCount == 0 && recoveredTailCount == 0 && mismatch == 0 && payloadFailures == 0;
        var report = new CanonicalRecorderV2DataQualityReport(
            shadowReady ? "FINALIZED" : "FAILED",
            eventCounts.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            eventCountsByStrategy.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            eventCountsByBook.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            firstRecordedUtc,
            lastRecordedUtc,
            outOfOrder,
            duplicateSequence,
            gap,
            eventIds.Count == physical.Length ? 0 : physical.Length - eventIds.Count,
            clockRegressionCount,
            marketDataGapCount,
            invalidBookCount,
            CanonicalRecorderV2Constants.NotEvaluated,
            writerErrorCount,
            dropped,
            recoveredTailCount,
            unfinalizedChunkCount,
            mismatch,
            payloadFailures,
            "VALID",
            lastFlushUtc,
            lastFlushSequence,
            shadowReady,
            failureReason);
        await WriteJsonAtomicAsync(Path.Combine(healthRoot, "data_quality_report.json"), report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private long ValidatePayloadHashes()
    {
        long failures = 0;
        foreach (var chunk in chunks)
        {
            var path = Path.Combine(runRoot, chunk.File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures++;
                continue;
            }

            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var envelope = JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!;
                var payload = JsonSerializer.Serialize(envelope.PayloadJson, CanonicalRecorderV2Constants.JsonOptions);
                if (!string.Equals(Sha256Text(payload), envelope.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures++;
                }
            }
        }

        return failures;
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

public sealed class CanonicalRecorderV2Replayer
{
    public async Task<CanonicalRecorderV2ReplayReport> ReplayAsync(string runRoot, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = Path.GetFullPath(runRoot);
            var finalPath = Path.Combine(root, "final_manifest.json");
            if (!File.Exists(finalPath))
            {
                return Failed("UNKNOWN", "final_manifest_missing");
            }

            var finalText = await File.ReadAllTextAsync(finalPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var final = JsonSerializer.Deserialize<CanonicalRecorderV2FinalManifest>(finalText, CanonicalRecorderV2Constants.JsonOptions)!;
            if (!final.Finalized || final.RecorderManifestVersion != CanonicalRecorderV2Constants.ManifestSchemaVersion || final.Mode != CanonicalRecorderV2Constants.Mode)
            {
                return Failed(final.RecorderRunId, "invalid_final_manifest_header");
            }

            if (CanonicalRecorderV2.Sha256File(Path.Combine(root, "run_manifest.json")) != final.RunManifestSha256)
            {
                return Failed(final.RecorderRunId, "run_manifest_hash_mismatch");
            }

            if (CanonicalRecorderV2.Sha256File(Path.Combine(root, "health", "data_quality_report.json")) != final.DataQualityReportSha256)
            {
                return Failed(final.RecorderRunId, "data_quality_hash_mismatch");
            }

            var allEvents = new List<CanonicalRecorderEnvelopeV2>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            long expectedSequence = 1;
            foreach (var chunk in final.Chunks)
            {
                if (!IsSafeRelativePath(chunk.File))
                {
                    return Failed(final.RecorderRunId, $"unsafe_chunk_path:{chunk.File}");
                }

                var path = Path.GetFullPath(Path.Combine(root, chunk.File.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return Failed(final.RecorderRunId, $"chunk_path_escape:{chunk.File}");
                }

                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                {
                    return Failed(final.RecorderRunId, $"chunk_reparse_point:{chunk.File}");
                }

                if (!File.Exists(path))
                {
                    return Failed(final.RecorderRunId, $"missing_chunk:{chunk.File}");
                }

                if (new FileInfo(path).Length != chunk.SizeBytes || CanonicalRecorderV2.Sha256File(path) != chunk.Sha256)
                {
                    return Failed(final.RecorderRunId, $"chunk_metadata_mismatch:{chunk.File}");
                }

                var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                var chunkEvents = new List<CanonicalRecorderEnvelopeV2>();
                foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var envelope = JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!;
                    if (envelope.SchemaVersion != CanonicalRecorderV2Constants.EnvelopeSchemaVersion)
                    {
                        return Failed(final.RecorderRunId, $"unknown_schema:{envelope.SchemaVersion}");
                    }

                    if (!CanonicalRecorderV2Constants.SupportedEventTypes.Contains(envelope.EventType))
                    {
                        return Failed(final.RecorderRunId, $"unknown_event_type:{envelope.EventType}");
                    }

                    if (envelope.RecorderRunId != final.RecorderRunId || envelope.Environment != final.Environment)
                    {
                        return Failed(final.RecorderRunId, "envelope_run_or_environment_mismatch");
                    }

                    if (!eventIds.Add(envelope.EventId))
                    {
                        return Failed(final.RecorderRunId, $"duplicate_event_id:{envelope.EventId}");
                    }

                    if (envelope.ProcessEventSequence != expectedSequence)
                    {
                        return Failed(final.RecorderRunId, $"sequence_gap_or_out_of_order:{envelope.ProcessEventSequence}:expected:{expectedSequence}");
                    }

                    if (RequiresDimensions(envelope) && (string.IsNullOrWhiteSpace(envelope.FundId) || string.IsNullOrWhiteSpace(envelope.StrategyId) || string.IsNullOrWhiteSpace(envelope.BookId)))
                    {
                        return Failed(final.RecorderRunId, $"missing_source_dimensions:{envelope.EventType}");
                    }

                    var payload = JsonSerializer.Serialize(envelope.PayloadJson, CanonicalRecorderV2Constants.JsonOptions);
                    if (CanonicalRecorderV2.Sha256Text(payload) != envelope.PayloadSha256)
                    {
                        return Failed(final.RecorderRunId, $"payload_hash_mismatch:{envelope.EventId}");
                    }

                    expectedSequence++;
                    chunkEvents.Add(envelope);
                    allEvents.Add(envelope);
                }

                if (chunkEvents.Count != chunk.EventCount || chunkEvents.First().ProcessEventSequence != chunk.FirstSequence || chunkEvents.Last().ProcessEventSequence != chunk.LastSequence)
                {
                    return Failed(final.RecorderRunId, $"chunk_sequence_or_count_mismatch:{chunk.File}");
                }
            }

            if (allEvents.Count != final.EventsWritten)
            {
                return Failed(final.RecorderRunId, "events_written_mismatch");
            }

            var eventCounts = allEvents.GroupBy(x => x.EventType, StringComparer.Ordinal).ToDictionary(x => x.Key, x => (long)x.Count(), StringComparer.Ordinal);
            foreach (var (key, value) in final.EventCounts)
            {
                if (!eventCounts.TryGetValue(key, out var actual) || actual != value)
                {
                    return Failed(final.RecorderRunId, $"event_count_mismatch:{key}");
                }
            }

            return new CanonicalRecorderV2ReplayReport("PASS", CanonicalRecorderV2Constants.ReplayHashVersion, final.RecorderRunId, final.Chunks.Count, allEvents.Count, ComputeDeterministicReplayHash(allEvents), eventCounts, null);
        }
        catch (Exception ex)
        {
            return Failed("UNKNOWN", ex.GetType().Name + ":" + ex.Message);
        }
    }

    public async Task<IReadOnlyList<CanonicalRecorderEnvelopeV2>> ReadEventsAsync(string runRoot, CancellationToken cancellationToken = default)
    {
        var report = await ReplayAsync(runRoot, cancellationToken).ConfigureAwait(false);
        if (report.Status != "PASS")
        {
            throw new InvalidOperationException(report.FailureReason);
        }

        var final = JsonSerializer.Deserialize<CanonicalRecorderV2FinalManifest>(await File.ReadAllTextAsync(Path.Combine(runRoot, "final_manifest.json"), Encoding.UTF8, cancellationToken).ConfigureAwait(false), CanonicalRecorderV2Constants.JsonOptions)!;
        return final.Chunks
            .SelectMany(chunk => File.ReadAllLines(Path.Combine(runRoot, chunk.File.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CanonicalRecorderEnvelopeV2>(line, CanonicalRecorderV2Constants.JsonOptions)!)
            .ToArray();
    }

    public static string ComputeDeterministicReplayHash(IEnumerable<CanonicalRecorderEnvelopeV2> events)
    {
        var semantic = events.Select(x => new
        {
            x.ProcessEventSequence,
            x.EventType,
            x.FundId,
            x.PortfolioId,
            x.StrategyId,
            x.StrategyRunId,
            x.StrategyVersion,
            x.BookId,
            x.CapitalAllocationId,
            x.BrokerAccountKey,
            x.NavRunId,
            x.ExecutionPolicyId,
            x.SourceEventId,
            x.SourceEventSequence,
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
            x.SourceTimestampUtc,
            x.DecisionTime,
            x.EffectiveFrom,
            x.Deadline,
            x.TargetClose,
            x.SessionId,
            x.FixMsgSeqNum,
            x.PossDup,
            x.SendingTime,
            x.QuoteEventId,
            x.Instrument,
            x.BidPrice,
            x.BidQuantity,
            x.AskPrice,
            x.AskQuantity,
            x.DepthLevel,
            x.BookValid,
            x.SourceReceiveSequence,
            x.PayloadSha256,
            x.CodeCommit,
            x.ConfigHash
        });
        return CanonicalRecorderV2.Sha256Text(JsonSerializer.Serialize(semantic, CanonicalRecorderV2Constants.JsonOptions));
    }

    private static bool IsSafeRelativePath(string path)
        => path.Contains('/', StringComparison.Ordinal) &&
           !path.Contains('\\', StringComparison.Ordinal) &&
           !Path.IsPathRooted(path) &&
           !path.Split('/').Any(part => part is "" or "." or "..");

    private static bool RequiresDimensions(CanonicalRecorderEnvelopeV2 e)
        => e.EventType is
            "MODEL_WEIGHT_BATCH_OBSERVED" or
            "MODEL_RUN_OBSERVED" or
            "TARGET_WEIGHT_OBSERVED" or
            "TARGET_POSITION_OBSERVED" or
            "TARGET_OBSERVED" or
            "TARGET_ACTIVATED" or
            "TARGET_REVISED" or
            "DRIFT_SNAPSHOT_OBSERVED" or
            "SHADOW_DECISION" or
            "SHADOW_PARENT_INTENT" or
            "SHADOW_CHILD_INTENT" or
            "RISK_DECISION_OBSERVED" or
            "POSITION_SNAPSHOT_OBSERVED";

    private static CanonicalRecorderV2ReplayReport Failed(string runId, string reason)
        => new("FAIL", CanonicalRecorderV2Constants.ReplayHashVersion, runId, 0, 0, string.Empty, new Dictionary<string, long>(), reason);
}


