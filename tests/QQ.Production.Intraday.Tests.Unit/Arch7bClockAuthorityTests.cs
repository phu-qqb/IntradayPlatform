using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bClockAuthorityTests
{
    private const string Host = "ARCH7B-TEST-HOST";
    private const string Commit = "e74f984bf3320142617b9016fcb91610d36b5741";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 10, 29, 50, TimeSpan.Zero);

    [Fact]
    public void Fresh_snapshot_with_20ms_offset_and_10ms_uncertainty_qualifies()
    {
        var snapshot = Snapshot();

        PmsShadowCaptureClockAuthorityValidator.RequireQualified(
            snapshot, Now, Host, Commit);

        Assert.Equal(64, snapshot.SnapshotSha256.Length);
        Assert.Equal(snapshot.SnapshotSha256,
            PmsShadowCaptureClockAuthorityStore.ComputeSha256(snapshot));
    }

    [Fact]
    public void Historical_1110ms_skew_is_rejected()
    {
        AssertNotQualified(Snapshot(offset: 1_110m));
    }

    [Fact]
    public void Stale_snapshot_is_rejected()
    {
        AssertNotQualified(Snapshot(capturedAt: Now.AddSeconds(-61)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    [InlineData("Local CMOS Clock")]
    public void Unknown_or_local_only_time_source_is_rejected(string source)
    {
        AssertNotQualified(Snapshot(hostClockSource: source));
    }

    [Fact]
    public void Excessive_uncertainty_is_rejected()
    {
        AssertNotQualified(Snapshot(uncertainty: 101m));
    }

    [Fact]
    public void Host_mismatch_is_rejected()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                Snapshot(), Now, "OTHER-HOST", Commit));

        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    [Fact]
    public void Repository_commit_mismatch_is_rejected()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                Snapshot(), Now, Host, new string('a', 40)));

        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    [Fact]
    public void Unsynchronized_service_invalid_leap_or_old_last_sync_is_rejected()
    {
        AssertNotQualified(Snapshot(serviceSynchronized: false));
        AssertNotQualified(Snapshot(leapIndicator: 3));
        AssertNotQualified(Snapshot(lastSync: Now.AddMinutes(-16)));
    }

    [Fact]
    public void Slot_evidence_requires_independent_same_source_snapshots_without_step()
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            new DateTimeOffset(2026, 7, 24, 10, 45, 0, TimeSpan.Zero));
        var pre = Snapshot(capturedAt: slot.SlotStartUtc.AddSeconds(-1));
        var validPost = Snapshot(capturedAt: slot.SlotEndUtc.AddSeconds(1), offset: 25m);
        PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
            new(pre, validPost), slot, Host, Commit);

        var steppedPost = Snapshot(capturedAt: slot.SlotEndUtc.AddSeconds(1), offset: -90m);
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(pre, steppedPost), slot, Host, Commit));
        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    [Fact]
    public void Tampered_snapshot_hash_is_rejected()
    {
        var snapshot = Snapshot() with { MeasuredOffsetMilliseconds = 21m };

        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                snapshot, Now, Host, Commit));

        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    [Fact]
    public void Late_receipt_and_import_deadline_are_distinct_contracts()
    {
        Assert.Equal(2_000,
            PmsShadowCaptureClockAuthorityContract
                .MaximumLateReceiptAfterSlotCloseMilliseconds);
        Assert.Equal(300,
            PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds);
        Assert.True(
            PmsShadowCaptureClockAuthorityContract
                .MaximumLateReceiptAfterSlotCloseMilliseconds <
            PmsShadowFreshSlotHandoffContract.ReadyMarkerSloSeconds * 1_000);
    }

    [Fact]
    public void Current_host_readonly_measurement_hashes_exactly_and_remains_nonqualifying()
    {
        var snapshot = PmsShadowCaptureClockAuthoritySnapshot.Create(
            new DateTimeOffset(2026, 7, 24, 18, 12, 40, 397,
                TimeSpan.Zero).AddTicks(526),
            "Local CMOS Clock",
            "time.windows.com [51.145.123.29:123]",
            1751.9040m,
            29.1710m,
            28.72435m,
            4,
            "FAIL",
            "LAPTOP-PHU-QQB",
            24216,
            Commit,
            false,
            3,
            null);

        Assert.Equal(
            "b1a1969b6538d5ccae61e16d7a2dc1987680e2e064cb771b0943e89da9d8ebcb",
            snapshot.SnapshotSha256);
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                snapshot,
                new DateTimeOffset(2026, 7, 24, 18, 12, 41, TimeSpan.Zero),
                "LAPTOP-PHU-QQB",
                Commit));
        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker,
            error.Message);
    }

    private static void AssertNotQualified(
        PmsShadowCaptureClockAuthoritySnapshot snapshot)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                snapshot, Now, Host, Commit));
        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Blocker, error.Message);
    }

    private static PmsShadowCaptureClockAuthoritySnapshot Snapshot(
        DateTimeOffset? capturedAt = null,
        decimal offset = 20m,
        decimal uncertainty = 10m,
        string hostClockSource = "Windows Time",
        string referenceClockSource = "time.windows.com",
        bool serviceSynchronized = true,
        int leapIndicator = 0,
        DateTimeOffset? lastSync = null)
    {
        var captured = capturedAt ?? Now.AddSeconds(-1);
        return PmsShadowCaptureClockAuthoritySnapshot.Create(
            captured,
            hostClockSource,
            referenceClockSource,
            offset,
            uncertainty,
            20m,
            5,
            "PASS",
            Host,
            1234,
            Commit,
            serviceSynchronized,
            leapIndicator,
            lastSync ?? captured.AddMinutes(-1));
    }
}
