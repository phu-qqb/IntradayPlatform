using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowCaptureClockAuthorityContract
{
    public const string Version = "pms_shadow_capture_clock_authority_v1";
    public const string QualifiedStatus = "PASS";
    public const string Blocker = "ARCH7B_CAPTURE_HOST_CLOCK_NOT_QUALIFIED";
    public const int MaximumAbsoluteOffsetMilliseconds = 100;
    public const int MaximumUncertaintyMilliseconds = 100;
    public const int MaximumSnapshotAgeSeconds = 60;
    public const int MaximumLastSuccessfulSyncAgeMinutes = 15;
    public const int MaximumOffsetStepDuringSlotMilliseconds = 100;
    public const int SourceTimestampPrecisionMilliseconds = 1;
    public const int MaximumLateReceiptAfterSlotCloseMilliseconds = 2_000;
    public const string CrossClockComparison = "MEASURED_ENVELOPE_V1";
    public const string LmaxSourceTimestampFixTag = "52";

    public static TimeSpan MaximumLateReceiptAfterSlotClose =>
        TimeSpan.FromMilliseconds(MaximumLateReceiptAfterSlotCloseMilliseconds);
}

public sealed record PmsShadowCaptureClockAuthoritySnapshot(
    string ContractVersion,
    DateTimeOffset CapturedAtUtc,
    string HostClockSource,
    string ReferenceClockSource,
    decimal MeasuredOffsetMilliseconds,
    decimal MeasurementUncertaintyMilliseconds,
    decimal RoundTripMilliseconds,
    int SampleCount,
    decimal MaximumAbsoluteOffsetMilliseconds,
    decimal MaximumUncertaintyMilliseconds,
    string Status,
    string HostIdentity,
    int ProcessId,
    string RepositoryCommit,
    bool ServiceSynchronized,
    int LeapIndicator,
    DateTimeOffset? LastSuccessfulSyncUtc,
    string SnapshotSha256)
{
    public static PmsShadowCaptureClockAuthoritySnapshot Create(
        DateTimeOffset capturedAtUtc,
        string hostClockSource,
        string referenceClockSource,
        decimal measuredOffsetMilliseconds,
        decimal measurementUncertaintyMilliseconds,
        decimal roundTripMilliseconds,
        int sampleCount,
        string status,
        string hostIdentity,
        int processId,
        string repositoryCommit,
        bool serviceSynchronized,
        int leapIndicator,
        DateTimeOffset? lastSuccessfulSyncUtc,
        decimal maximumAbsoluteOffsetMilliseconds =
            PmsShadowCaptureClockAuthorityContract.MaximumAbsoluteOffsetMilliseconds,
        decimal maximumUncertaintyMilliseconds =
            PmsShadowCaptureClockAuthorityContract.MaximumUncertaintyMilliseconds)
    {
        var value = new PmsShadowCaptureClockAuthoritySnapshot(
            PmsShadowCaptureClockAuthorityContract.Version,
            capturedAtUtc,
            hostClockSource,
            referenceClockSource,
            measuredOffsetMilliseconds,
            measurementUncertaintyMilliseconds,
            roundTripMilliseconds,
            sampleCount,
            maximumAbsoluteOffsetMilliseconds,
            maximumUncertaintyMilliseconds,
            status,
            hostIdentity,
            processId,
            repositoryCommit,
            serviceSynchronized,
            leapIndicator,
            lastSuccessfulSyncUtc,
            string.Empty);
        return value with
        {
            SnapshotSha256 = PmsShadowCaptureClockAuthorityStore.ComputeSha256(value)
        };
    }
}

public sealed record PmsShadowCaptureClockAuthorityEvidence(
    PmsShadowCaptureClockAuthoritySnapshot PreCapture,
    PmsShadowCaptureClockAuthoritySnapshot PostClose)
{
    public decimal MaximumCrossClockLeadMilliseconds =>
        Math.Max(0m, Math.Max(
            PreCapture.MeasuredOffsetMilliseconds +
            PreCapture.MeasurementUncertaintyMilliseconds,
            PostClose.MeasuredOffsetMilliseconds +
            PostClose.MeasurementUncertaintyMilliseconds)) +
        PmsShadowCaptureClockAuthorityContract.SourceTimestampPrecisionMilliseconds;

    public decimal MaximumClockUncertaintyMilliseconds =>
        Math.Max(PreCapture.MeasurementUncertaintyMilliseconds,
            PostClose.MeasurementUncertaintyMilliseconds);
}

public static class PmsShadowCaptureClockAuthorityValidator
{
    public static void RequireQualified(
        PmsShadowCaptureClockAuthoritySnapshot snapshot,
        DateTimeOffset observedAtUtc,
        string expectedHostIdentity,
        string expectedRepositoryCommit)
    {
        try
        {
            PmsShadowIntradayCadenceContract.RequireUtc(observedAtUtc);
            PmsShadowIntradayCadenceContract.RequireUtc(snapshot.CapturedAtUtc);
            if (snapshot.LastSuccessfulSyncUtc is { } lastSync)
                PmsShadowIntradayCadenceContract.RequireUtc(lastSync);
            Require(snapshot.ContractVersion == PmsShadowCaptureClockAuthorityContract.Version);
            PmsShadowIntradayCadenceContract.RequireSha(
                snapshot.SnapshotSha256, nameof(snapshot.SnapshotSha256));
            Require(snapshot.SnapshotSha256 ==
                PmsShadowCaptureClockAuthorityStore.ComputeSha256(snapshot));
            Require(snapshot.Status == PmsShadowCaptureClockAuthorityContract.QualifiedStatus);
            Require(snapshot.ServiceSynchronized);
            Require(snapshot.LeapIndicator == 0);
            Require(!Unknown(snapshot.HostClockSource));
            Require(!Unknown(snapshot.ReferenceClockSource));
            Require(snapshot.HostIdentity == expectedHostIdentity);
            Require(snapshot.RepositoryCommit == expectedRepositoryCommit);
            Require(snapshot.RepositoryCommit.Length is 40 or 64 &&
                snapshot.RepositoryCommit.All(value =>
                    char.IsAsciiHexDigit(value) && !char.IsUpper(value)));
            Require(snapshot.ProcessId > 0);
            Require(snapshot.SampleCount >= 3);
            Require(snapshot.RoundTripMilliseconds >= 0m);
            Require(snapshot.MeasurementUncertaintyMilliseconds >= 0m);
            Require(snapshot.MaximumAbsoluteOffsetMilliseconds > 0m &&
                snapshot.MaximumAbsoluteOffsetMilliseconds <=
                PmsShadowCaptureClockAuthorityContract.MaximumAbsoluteOffsetMilliseconds);
            Require(snapshot.MaximumUncertaintyMilliseconds > 0m &&
                snapshot.MaximumUncertaintyMilliseconds <=
                PmsShadowCaptureClockAuthorityContract.MaximumUncertaintyMilliseconds);
            Require(Math.Abs(snapshot.MeasuredOffsetMilliseconds) <=
                snapshot.MaximumAbsoluteOffsetMilliseconds);
            Require(snapshot.MeasurementUncertaintyMilliseconds <=
                snapshot.MaximumUncertaintyMilliseconds);
            Require(snapshot.CapturedAtUtc <= observedAtUtc);
            Require(observedAtUtc - snapshot.CapturedAtUtc <=
                TimeSpan.FromSeconds(
                    PmsShadowCaptureClockAuthorityContract.MaximumSnapshotAgeSeconds));
            Require(snapshot.LastSuccessfulSyncUtc is { } successfulSync &&
                successfulSync <= snapshot.CapturedAtUtc &&
                snapshot.CapturedAtUtc - successfulSync <= TimeSpan.FromMinutes(
                    PmsShadowCaptureClockAuthorityContract
                        .MaximumLastSuccessfulSyncAgeMinutes));
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ArgumentException or ArithmeticException)
        {
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker, exception);
        }
    }

    public static void RequireQualifiedForSlot(
        PmsShadowCaptureClockAuthorityEvidence evidence,
        PmsShadowIntradaySlotWindow slot,
        string expectedHostIdentity,
        string expectedRepositoryCommit)
    {
        RequireQualified(evidence.PreCapture, slot.SlotStartUtc,
            expectedHostIdentity, expectedRepositoryCommit);
        RequireQualified(evidence.PostClose, evidence.PostClose.CapturedAtUtc,
            expectedHostIdentity, expectedRepositoryCommit);
        try
        {
            Require(evidence.PostClose.CapturedAtUtc >= slot.SlotEndUtc);
            Require(evidence.PostClose.CapturedAtUtc - slot.SlotEndUtc <=
                TimeSpan.FromSeconds(
                    PmsShadowCaptureClockAuthorityContract.MaximumSnapshotAgeSeconds));
            Require(evidence.PreCapture.SnapshotSha256 !=
                evidence.PostClose.SnapshotSha256);
            Require(evidence.PreCapture.HostClockSource ==
                evidence.PostClose.HostClockSource);
            Require(evidence.PreCapture.ReferenceClockSource ==
                evidence.PostClose.ReferenceClockSource);
            Require(Math.Abs(evidence.PostClose.MeasuredOffsetMilliseconds -
                evidence.PreCapture.MeasuredOffsetMilliseconds) <=
                PmsShadowCaptureClockAuthorityContract
                    .MaximumOffsetStepDuringSlotMilliseconds);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArithmeticException)
        {
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker, exception);
        }
    }

    public static bool IsCrossClockCausalityValid(
        DateTimeOffset sourceTimestampUtc,
        DateTimeOffset recordedUtc,
        PmsShadowCaptureClockAuthorityEvidence evidence) =>
        (sourceTimestampUtc - recordedUtc).TotalMilliseconds <=
        (double)evidence.MaximumCrossClockLeadMilliseconds;

    public static bool IsWithinLateReceiptBound(
        DateTimeOffset recordedUtc,
        DateTimeOffset slotEndUtc,
        PmsShadowCaptureClockAuthorityEvidence evidence)
    {
        var correctedRecordedUtc = recordedUtc.AddMilliseconds(
            (double)evidence.PostClose.MeasuredOffsetMilliseconds);
        return correctedRecordedUtc <= slotEndUtc +
            PmsShadowCaptureClockAuthorityContract.MaximumLateReceiptAfterSlotClose +
            TimeSpan.FromMilliseconds(
                (double)evidence.PostClose.MeasurementUncertaintyMilliseconds);
    }

    public static DateTimeOffset CorrectedRecordedUtcForValidation(
        DateTimeOffset recordedUtc,
        PmsShadowCaptureClockAuthorityEvidence evidence) =>
        recordedUtc.AddMilliseconds(
            (double)evidence.PreCapture.MeasuredOffsetMilliseconds);

    private static bool Unknown(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("UNSPECIFIED", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("LOCAL CMOS CLOCK", StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("CLOCK_AUTHORITY_INVARIANT_FAILED");
    }
}

public static class PmsShadowCaptureClockAuthorityStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    public static PmsShadowCaptureClockAuthoritySnapshot Read(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<PmsShadowCaptureClockAuthoritySnapshot>(
                File.ReadAllBytes(path), JsonOptions)
                ?? throw new InvalidDataException(
                    PmsShadowCaptureClockAuthorityContract.Blocker);
            if (value.SnapshotSha256 != ComputeSha256(value))
                throw new InvalidDataException(
                    PmsShadowCaptureClockAuthorityContract.Blocker);
            return value;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                JsonException or InvalidDataException or ArithmeticException)
        {
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker, exception);
        }
    }

    public static void WriteAtomic(string path,
        PmsShadowCaptureClockAuthoritySnapshot snapshot)
    {
        if (snapshot.SnapshotSha256 != ComputeSha256(snapshot))
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        path = Path.GetFullPath(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes))
                throw new InvalidDataException("CLOCK_AUTHORITY_SNAPSHOT_CONFLICT");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string ComputeSha256(
        PmsShadowCaptureClockAuthoritySnapshot snapshot)
    {
        var canonical = string.Join("\n",
            snapshot.ContractVersion,
            snapshot.CapturedAtUtc.ToUniversalTime().ToString(
                "O", CultureInfo.InvariantCulture),
            snapshot.HostClockSource,
            snapshot.ReferenceClockSource,
            snapshot.MeasuredOffsetMilliseconds.ToString(CultureInfo.InvariantCulture),
            snapshot.MeasurementUncertaintyMilliseconds.ToString(
                CultureInfo.InvariantCulture),
            snapshot.RoundTripMilliseconds.ToString(CultureInfo.InvariantCulture),
            snapshot.SampleCount.ToString(CultureInfo.InvariantCulture),
            snapshot.MaximumAbsoluteOffsetMilliseconds.ToString(
                CultureInfo.InvariantCulture),
            snapshot.MaximumUncertaintyMilliseconds.ToString(
                CultureInfo.InvariantCulture),
            snapshot.Status,
            snapshot.HostIdentity,
            snapshot.ProcessId.ToString(CultureInfo.InvariantCulture),
            snapshot.RepositoryCommit,
            snapshot.ServiceSynchronized ? "true" : "false",
            snapshot.LeapIndicator.ToString(CultureInfo.InvariantCulture),
            snapshot.LastSuccessfulSyncUtc?.ToUniversalTime().ToString(
                "O", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
