using System.Security.Cryptography;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PmsShadowCaptureClockAuthorityProducerTests : IDisposable
{
    private const string Host = "ARCH7B-PRIMARY";
    private const string Commit = "794e6a486f39ae49556d4fa1625b4cf453e66459";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-clock-producer", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Valid_batch_with_five_independent_samples_creates_qualified_snapshot()
    {
        var snapshot = Create(Batch());

        PmsShadowCaptureClockAuthorityValidator.RequireQualified(
            snapshot, Now, Host, Commit);
        Assert.Equal(5, snapshot.SampleCount);
    }

    [Fact]
    public void Snapshot_sha_is_the_exact_existing_contract_hash()
    {
        var snapshot = Create(Batch());

        Assert.Equal(PmsShadowCaptureClockAuthorityStore.ComputeSha256(snapshot),
            snapshot.SnapshotSha256);
        Assert.Equal(64, snapshot.SnapshotSha256.Length);
    }

    [Fact]
    public async Task Producer_writes_and_reads_the_snapshot_atomically()
    {
        var value = await ProduceAsync(Batch(), "clock_authority_preflight.json");

        Assert.Equal(value.Snapshot,
            PmsShadowCaptureClockAuthorityStore.Read(value.AbsolutePath));
    }

    [Fact]
    public void Store_accepts_byte_identical_replay()
    {
        var path = Path.Combine(root, "clock.json");
        var snapshot = Create(Batch());

        PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, snapshot);
        PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, snapshot);

        Assert.Equal(snapshot, PmsShadowCaptureClockAuthorityStore.Read(path));
    }

    [Fact]
    public void Store_rejects_conflicting_replay()
    {
        var path = Path.Combine(root, "clock.json");
        PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, Create(Batch()));

        Assert.Equal("CLOCK_AUTHORITY_SNAPSHOT_CONFLICT",
            Assert.Throws<InvalidDataException>(() =>
                PmsShadowCaptureClockAuthorityStore.WriteAtomic(path,
                    Create(Batch(offset: 3m)))).Message);
    }

    [Fact]
    public async Task Offset_above_existing_contract_threshold_is_rejected()
    {
        await AssertRejectedAsync(Batch(offset: 101m));
    }

    [Fact]
    public async Task Uncertainty_above_existing_contract_threshold_is_rejected()
    {
        await AssertRejectedAsync(Batch(roundTrip: 202m));
    }

    [Fact]
    public void Fewer_than_three_samples_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Create(Batch(sampleCount: 2)));
    }

    [Fact]
    public async Task Unsynchronized_service_is_rejected()
    {
        await AssertRejectedAsync(Batch(serviceSynchronized: false));
    }

    [Fact]
    public async Task Nonzero_system_leap_indicator_is_rejected()
    {
        await AssertRejectedAsync(Batch(systemLeap: 3));
    }

    [Fact]
    public async Task Non_primary_w32time_source_is_rejected()
    {
        await AssertRejectedAsync(Batch(hostClockSource: "time.windows.com,0x9"));
    }

    [Fact]
    public async Task Nonzero_sample_leap_indicator_is_rejected()
    {
        await AssertRejectedAsync(Batch(sampleLeap: 1));
    }

    [Fact]
    public async Task Unknown_host_clock_source_is_rejected()
    {
        await AssertRejectedAsync(Batch(hostClockSource: "UNKNOWN"));
    }

    [Fact]
    public void Missing_last_successful_sync_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Create(Batch(lastSync: null,
            includeLastSync: false)));
    }

    [Fact]
    public async Task Old_last_successful_sync_is_rejected()
    {
        await AssertRejectedAsync(Batch(lastSync: Now.AddMinutes(-16)));
    }

    [Fact]
    public async Task Stale_measurement_batch_is_rejected()
    {
        await AssertRejectedAsync(Batch(capturedAt: Now.AddSeconds(-61)));
    }

    [Fact]
    public async Task Wrong_host_identity_is_rejected()
    {
        var producer = Producer(Batch());
        await Assert.ThrowsAsync<InvalidDataException>(() => producer.ProduceAsync(
            root, "clock.json", "OTHER-HOST", Commit, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_repository_commit_is_rejected()
    {
        var producer = Producer(Batch());
        await Assert.ThrowsAsync<InvalidDataException>(() => producer.ProduceAsync(
            root, "clock.json", Host, new string('a', 39), CancellationToken.None));
    }

    [Fact]
    public void Capture_and_post_close_must_be_distinct()
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Now.AddMinutes(15));
        var snapshot = Create(Batch(capturedAt: slot.SlotStartUtc));

        Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(snapshot, snapshot), slot, Host, Commit));
    }

    [Fact]
    public void Post_close_before_slot_end_is_rejected()
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Now.AddMinutes(15));
        var capture = Create(Batch(capturedAt: slot.SlotStartUtc));
        var post = Create(Batch(capturedAt: slot.SlotEndUtc.AddMilliseconds(-1), offset: 2m));

        Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(capture, post), slot, Host, Commit));
    }

    [Fact]
    public void Excessive_offset_step_during_slot_is_rejected()
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Now.AddMinutes(15));
        var capture = Create(Batch(capturedAt: slot.SlotStartUtc, offset: -50m));
        var post = Create(Batch(capturedAt: slot.SlotEndUtc, offset: 51m));

        Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(capture, post), slot, Host, Commit));
    }

    [Fact]
    public async Task Produced_file_is_absolute_inside_run_root_and_has_exact_file_sha()
    {
        var value = await ProduceAsync(Batch(), "clock_authority_capture.json");

        Assert.True(Path.IsPathFullyQualified(value.AbsolutePath));
        Assert.Equal(Path.GetFullPath(root),
            Path.GetDirectoryName(value.AbsolutePath));
        Assert.Equal(Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(value.AbsolutePath))), value.FileSha256);
    }

    [Fact]
    public async Task Failed_probe_creates_no_snapshot_or_temporary_file()
    {
        Directory.CreateDirectory(root);
        var producer = new PmsShadowCaptureClockAuthorityProducer(
            new ThrowingProbe(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidDataException>(() => producer.ProduceAsync(
            root, "clock_authority_preflight.json", Host, Commit,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(root));
    }

    [Fact]
    public void Production_probe_and_reference_are_real_non_fixture_authorities()
    {
        Assert.Equal("169.254.169.123",
            PmsShadowCaptureClockAuthorityMeasurementContract.AmazonTimeSyncAddress);
        Assert.Equal(5,
            PmsShadowCaptureClockAuthorityMeasurementContract.ProductionSampleCount);
        Assert.DoesNotContain("Fake",
            typeof(WindowsAmazonTimeSyncClockProbe).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public void Versioned_aggregation_uses_mean_offset_mean_rtt_and_maximum_envelope()
    {
        var batch = Batch() with
        {
            Samples =
            [
                new(Now.AddSeconds(-3), -4m, 4m, 0),
                new(Now.AddSeconds(-2), 2m, 10m, 0),
                new(Now.AddSeconds(-1), 8m, 6m, 0)
            ]
        };

        var snapshot = Create(batch);

        Assert.Equal(2m, snapshot.MeasuredOffsetMilliseconds);
        Assert.Equal(20m / 3m, snapshot.RoundTripMilliseconds);
        Assert.Equal(6m, snapshot.MeasurementUncertaintyMilliseconds);
    }

    private async Task AssertRejectedAsync(PmsShadowCaptureClockMeasurementBatch batch)
    {
        var producer = Producer(batch);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            producer.ProduceAsync(root, "clock.json", Host, Commit,
                CancellationToken.None));
        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    private Task<PmsShadowCaptureClockAuthorityProduction> ProduceAsync(
        PmsShadowCaptureClockMeasurementBatch batch,
        string fileName) => Producer(batch).ProduceAsync(root, fileName, Host,
        Commit, CancellationToken.None);

    private static PmsShadowCaptureClockAuthorityProducer Producer(
        PmsShadowCaptureClockMeasurementBatch batch) =>
        new(new FixedProbe(batch), new FixedTimeProvider(Now));

    private static PmsShadowCaptureClockAuthoritySnapshot Create(
        PmsShadowCaptureClockMeasurementBatch batch) =>
        PmsShadowCaptureClockAuthorityProducer.CreateSnapshot(
            batch, Host, Commit, 1234);

    private static PmsShadowCaptureClockMeasurementBatch Batch(
        int sampleCount = 5,
        DateTimeOffset? capturedAt = null,
        decimal offset = 1m,
        decimal roundTrip = 4m,
        bool serviceSynchronized = true,
        int systemLeap = 0,
        int sampleLeap = 0,
        string hostClockSource = "169.254.169.123,0x9",
        DateTimeOffset? lastSync = null,
        bool includeLastSync = true)
    {
        var captured = capturedAt ?? Now.AddSeconds(-1);
        var samples = Enumerable.Range(0, sampleCount).Select(index =>
            new PmsShadowNtpMeasurement(captured.AddTicks(index),
                offset, roundTrip, sampleLeap)).ToArray();
        return new(
            PmsShadowCaptureClockAuthorityMeasurementContract.Version,
            new(captured, hostClockSource, serviceSynchronized, systemLeap,
                includeLastSync ? lastSync ?? captured.AddMinutes(-1) : null, Host),
            PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource,
            samples);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class FixedProbe(PmsShadowCaptureClockMeasurementBatch batch)
        : IPmsShadowCaptureClockSystemProbe
    {
        public Task<PmsShadowCaptureClockMeasurementBatch> MeasureAsync(
            int sampleCount, CancellationToken cancellationToken)
        {
            Assert.Equal(
                PmsShadowCaptureClockAuthorityMeasurementContract.ProductionSampleCount,
                sampleCount);
            return Task.FromResult(batch);
        }
    }

    private sealed class ThrowingProbe : IPmsShadowCaptureClockSystemProbe
    {
        public Task<PmsShadowCaptureClockMeasurementBatch> MeasureAsync(
            int sampleCount, CancellationToken cancellationToken) =>
            throw new InvalidDataException("MEASUREMENT_FAILED");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
