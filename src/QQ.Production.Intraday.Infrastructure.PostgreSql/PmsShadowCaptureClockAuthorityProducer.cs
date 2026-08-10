using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowCaptureClockAuthorityMeasurementContract
{
    public const string Version = "pms_shadow_capture_clock_authority_measurement_v1";
    public const string AmazonTimeSyncAddress = "169.254.169.123";
    public const string HostClockSource = "169.254.169.123,0x9";
    public const string ReferenceClockSource =
        "Amazon Time Sync Service 169.254.169.123:123";
    public const int ProductionSampleCount = 5;
    public const int MinimumSampleCount = 3;
    public const int NtpPort = 123;
    public const int NtpTimeoutMilliseconds = 2_000;
}

public sealed record PmsShadowClockSystemStatus(
    DateTimeOffset ObservedAtUtc,
    string HostClockSource,
    bool ServiceSynchronized,
    int LeapIndicator,
    DateTimeOffset? LastSuccessfulSyncUtc,
    string HostIdentity);

public sealed record PmsShadowNtpMeasurement(
    DateTimeOffset CapturedAtUtc,
    decimal OffsetMilliseconds,
    decimal RoundTripMilliseconds,
    int LeapIndicator);

public sealed record PmsShadowCaptureClockMeasurementBatch(
    string MeasurementVersion,
    PmsShadowClockSystemStatus SystemStatus,
    string ReferenceClockSource,
    IReadOnlyList<PmsShadowNtpMeasurement> Samples);

public sealed record PmsShadowCaptureClockAuthorityProduction(
    string AbsolutePath,
    string FileSha256,
    PmsShadowCaptureClockAuthoritySnapshot Snapshot);

public interface IPmsShadowCaptureClockSystemProbe
{
    Task<PmsShadowCaptureClockMeasurementBatch> MeasureAsync(
        int sampleCount,
        CancellationToken cancellationToken);
}

public interface IPmsShadowCaptureClockAuthorityProducer
{
    Task<PmsShadowCaptureClockAuthorityProduction> ProduceAsync(
        string runRoot,
        string fileName,
        string expectedHostIdentity,
        string expectedRepositoryCommit,
        CancellationToken cancellationToken);
}

public sealed class PmsShadowCaptureClockAuthorityProducer(
    IPmsShadowCaptureClockSystemProbe probe,
    TimeProvider timeProvider) : IPmsShadowCaptureClockAuthorityProducer
{
    public PmsShadowCaptureClockAuthorityProducer()
        : this(new WindowsAmazonTimeSyncClockProbe(), TimeProvider.System)
    {
    }

    public async Task<PmsShadowCaptureClockAuthorityProduction> ProduceAsync(
        string runRoot,
        string fileName,
        string expectedHostIdentity,
        string expectedRepositoryCommit,
        CancellationToken cancellationToken)
    {
        try
        {
            runRoot = Path.GetFullPath(runRoot);
            if (!Path.IsPathFullyQualified(runRoot) ||
                string.IsNullOrWhiteSpace(fileName) ||
                fileName != Path.GetFileName(fileName))
                throw new InvalidDataException("CLOCK_AUTHORITY_OUTPUT_PATH_INVALID");
            var path = Path.GetFullPath(Path.Combine(runRoot, fileName));
            if (!Path.GetRelativePath(runRoot, path).Equals(fileName,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("CLOCK_AUTHORITY_OUTPUT_PATH_INVALID");

            var batch = await probe.MeasureAsync(
                PmsShadowCaptureClockAuthorityMeasurementContract.ProductionSampleCount,
                cancellationToken).ConfigureAwait(false);
            var snapshot = CreateSnapshot(batch, expectedHostIdentity,
                expectedRepositoryCommit, Environment.ProcessId);
            var observedAtUtc = timeProvider.GetUtcNow();
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                snapshot, observedAtUtc, expectedHostIdentity, expectedRepositoryCommit);
            PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, snapshot);
            var readback = PmsShadowCaptureClockAuthorityStore.Read(path);
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                readback, timeProvider.GetUtcNow(), expectedHostIdentity,
                expectedRepositoryCommit);
            var fileSha256 = Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(path)));
            return new(path, fileSha256, readback);
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is IOException or UnauthorizedAccessException or InvalidDataException or
                ArgumentException or SocketException or FormatException or OverflowException or
                Win32Exception)
        {
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker, exception);
        }
    }

    public static PmsShadowCaptureClockAuthoritySnapshot CreateSnapshot(
        PmsShadowCaptureClockMeasurementBatch batch,
        string expectedHostIdentity,
        string expectedRepositoryCommit,
        int processId)
    {
        if (batch.MeasurementVersion !=
                PmsShadowCaptureClockAuthorityMeasurementContract.Version ||
            batch.Samples.Count <
                PmsShadowCaptureClockAuthorityMeasurementContract.MinimumSampleCount ||
            batch.Samples.Any(value => value.RoundTripMilliseconds < 0m ||
                value.LeapIndicator != 0) ||
            batch.SystemStatus.HostIdentity != expectedHostIdentity ||
            batch.SystemStatus.LeapIndicator != 0 ||
            !batch.SystemStatus.ServiceSynchronized ||
            batch.SystemStatus.HostClockSource !=
                PmsShadowCaptureClockAuthorityMeasurementContract.HostClockSource ||
            batch.SystemStatus.LastSuccessfulSyncUtc is null ||
            batch.ReferenceClockSource !=
                PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource)
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);

        var capturedAtUtc = batch.Samples.Max(value => value.CapturedAtUtc);
        var meanOffset = batch.Samples.Average(value => value.OffsetMilliseconds);
        var meanRoundTrip = batch.Samples.Average(value => value.RoundTripMilliseconds);
        var halfMaximumRoundTrip =
            batch.Samples.Max(value => value.RoundTripMilliseconds) / 2m;
        var halfOffsetRange =
            (batch.Samples.Max(value => value.OffsetMilliseconds) -
             batch.Samples.Min(value => value.OffsetMilliseconds)) / 2m;
        var uncertainty = Math.Max(halfMaximumRoundTrip, halfOffsetRange);

        return PmsShadowCaptureClockAuthoritySnapshot.Create(
            capturedAtUtc,
            batch.SystemStatus.HostClockSource,
            batch.ReferenceClockSource,
            meanOffset,
            uncertainty,
            meanRoundTrip,
            batch.Samples.Count,
            PmsShadowCaptureClockAuthorityContract.QualifiedStatus,
            batch.SystemStatus.HostIdentity,
            processId,
            expectedRepositoryCommit,
            batch.SystemStatus.ServiceSynchronized,
            batch.SystemStatus.LeapIndicator,
            batch.SystemStatus.LastSuccessfulSyncUtc);
    }
}

public sealed partial class WindowsAmazonTimeSyncClockProbe(
    TimeProvider timeProvider) : IPmsShadowCaptureClockSystemProbe
{
    private const ulong NtpEpochOffsetSeconds = 2_208_988_800UL;

    [GeneratedRegex(@"(?im)^\s*Source\s*:\s*(?<value>.+?)\s*$")]
    private static partial Regex SourcePattern();

    [GeneratedRegex(@"(?im)^\s*Leap Indicator\s*:\s*(?<value>\d+)")]
    private static partial Regex LeapPattern();

    [GeneratedRegex(@"(?im)^\s*Last Successful Sync Time\s*:\s*(?<value>.+?)\s*$")]
    private static partial Regex LastSyncPattern();

    public WindowsAmazonTimeSyncClockProbe() : this(TimeProvider.System)
    {
    }

    public async Task<PmsShadowCaptureClockMeasurementBatch> MeasureAsync(
        int sampleCount,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || sampleCount <
            PmsShadowCaptureClockAuthorityMeasurementContract.MinimumSampleCount)
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);

        var service = Run("sc.exe", "query W32Time");
        var status = Run("w32tm.exe", "/query /status /verbose");
        var source = Required(SourcePattern(), status);
        var leap = int.Parse(Required(LeapPattern(), status),
            CultureInfo.InvariantCulture);
        var lastSyncText = Required(LastSyncPattern(), status);
        if (!DateTimeOffset.TryParse(lastSyncText, CultureInfo.CurrentCulture,
                DateTimeStyles.AssumeLocal, out var lastSync) &&
            !DateTimeOffset.TryParse(lastSyncText, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out lastSync))
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);

        var measurements = new List<PmsShadowNtpMeasurement>(sampleCount);
        for (var index = 0; index < sampleCount; index++)
        {
            measurements.Add(await MeasureNtpAsync(cancellationToken)
                .ConfigureAwait(false));
            if (index + 1 < sampleCount)
                await Task.Delay(TimeSpan.FromMilliseconds(100), timeProvider,
                    cancellationToken).ConfigureAwait(false);
        }

        return new(
            PmsShadowCaptureClockAuthorityMeasurementContract.Version,
            new(timeProvider.GetUtcNow(), source,
                service.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) &&
                leap == 0, leap, lastSync.ToUniversalTime(), Environment.MachineName),
            PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource,
            measurements);
    }

    private async Task<PmsShadowNtpMeasurement> MeasureNtpAsync(
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Connect(IPAddress.Parse(
            PmsShadowCaptureClockAuthorityMeasurementContract.AmazonTimeSyncAddress),
            PmsShadowCaptureClockAuthorityMeasurementContract.NtpPort);
        var request = new byte[48];
        request[0] = 0x23;
        var t1 = timeProvider.GetUtcNow();
        WriteNtpTimestamp(request.AsSpan(40, 8), t1);
        await udp.SendAsync(request, cancellationToken).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(
            PmsShadowCaptureClockAuthorityMeasurementContract.NtpTimeoutMilliseconds);
        UdpReceiveResult response;
        try
        {
            response = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        }
        var t4 = timeProvider.GetUtcNow();
        if (response.Buffer.Length < 48 ||
            (response.Buffer[0] & 0x07) != 4 ||
            response.Buffer[1] is 0 or > 15 ||
            !response.Buffer.AsSpan(24, 8).SequenceEqual(request.AsSpan(40, 8)))
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        var leap = (response.Buffer[0] >> 6) & 0x03;
        var t2 = ReadNtpTimestamp(response.Buffer.AsSpan(32, 8));
        var t3 = ReadNtpTimestamp(response.Buffer.AsSpan(40, 8));
        var offset = ((t2 - t1).TotalMilliseconds +
                      (t3 - t4).TotalMilliseconds) / 2d;
        var roundTrip = Math.Max(0d,
            (t4 - t1).TotalMilliseconds - (t3 - t2).TotalMilliseconds);
        return new(t4, (decimal)offset, (decimal)roundTrip, leap);
    }

    private static string Run(string executable, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidDataException(
            PmsShadowCaptureClockAuthorityContract.Blocker);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(5_000))
        {
            try
            {
                process.Kill(true);
                process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
                // The process exited between the timeout and the cleanup attempt.
            }
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        }
        if (process.ExitCode != 0)
            throw new InvalidDataException(string.Join('\n',
                PmsShadowCaptureClockAuthorityContract.Blocker, error));
        return output;
    }

    private static string Required(Regex pattern, string value)
    {
        var match = pattern.Match(value);
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? match.Groups["value"].Value.Trim()
            : throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
    }

    private static void WriteNtpTimestamp(Span<byte> target, DateTimeOffset value)
    {
        var secondsSinceUnixEpoch = value.ToUnixTimeMilliseconds() / 1_000d;
        var ntp = secondsSinceUnixEpoch + NtpEpochOffsetSeconds;
        var seconds = (uint)Math.Floor(ntp);
        var fraction = (uint)Math.Min(uint.MaxValue,
            Math.Floor((ntp - seconds) * ((double)uint.MaxValue + 1d)));
        BinaryPrimitives.WriteUInt32BigEndian(target[..4], seconds);
        BinaryPrimitives.WriteUInt32BigEndian(target[4..], fraction);
    }

    private static DateTimeOffset ReadNtpTimestamp(ReadOnlySpan<byte> source)
    {
        var seconds = BinaryPrimitives.ReadUInt32BigEndian(source[..4]);
        var fraction = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        var unixSeconds = seconds - NtpEpochOffsetSeconds +
                          fraction / ((double)uint.MaxValue + 1d);
        return DateTimeOffset.UnixEpoch.AddSeconds(unixSeconds);
    }
}
