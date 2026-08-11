using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

internal sealed class Arch7bTestTimeProvider(DateTimeOffset current) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => current;

    public void AdvanceTo(DateTimeOffset value)
    {
        if (value < current) throw new InvalidOperationException("TEST_CLOCK_CANNOT_REWIND");
        current = value;
    }
}

internal sealed class Arch7bTestClockAuthorityProducer(
    Arch7bTestTimeProvider timeProvider) : IPmsShadowCaptureClockAuthorityProducer
{
    private int sequence;

    public Task<PmsShadowCaptureClockAuthorityProduction> ProduceAsync(
        string runRoot,
        string fileName,
        string expectedHostIdentity,
        string expectedRepositoryCommit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturedAt = timeProvider.GetUtcNow();
        if (fileName is "clock_authority_capture.json" or
            "clock_authority_post_close.json")
        {
            using var slot = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(runRoot, "selected-slot.json")));
            capturedAt = fileName == "clock_authority_capture.json"
                ? slot.RootElement.GetProperty("slot_start_utc")
                    .GetDateTimeOffset().AddSeconds(-1)
                : slot.RootElement.GetProperty("slot_end_utc")
                    .GetDateTimeOffset().AddMilliseconds(1);
            timeProvider.AdvanceTo(capturedAt);
        }
        else
        {
            capturedAt = capturedAt.AddMilliseconds(++sequence);
            timeProvider.AdvanceTo(capturedAt);
        }

        var snapshot = PmsShadowCaptureClockAuthoritySnapshot.Create(
            capturedAt,
            "169.254.169.123,0x9",
            PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource,
            ++sequence,
            2m,
            3m,
            5,
            PmsShadowCaptureClockAuthorityContract.QualifiedStatus,
            expectedHostIdentity,
            1234,
            expectedRepositoryCommit,
            true,
            0,
            capturedAt.AddMinutes(-1));
        var path = Path.GetFullPath(Path.Combine(runRoot, fileName));
        PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, snapshot);
        var fileSha = Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(path)));
        return Task.FromResult(new PmsShadowCaptureClockAuthorityProduction(
            path, fileSha, snapshot));
    }
}

internal sealed class Arch7bTestStageWindowWaiter(
    Arch7bTestTimeProvider timeProvider) : IArch7bStageWindowWaiter
{
    private readonly List<(string StageId, DateTimeOffset TargetUtc,
        bool EnforceMaximumWakeLateness)> waits = [];

    public IReadOnlyList<(string StageId, DateTimeOffset TargetUtc,
        bool EnforceMaximumWakeLateness)> Waits => waits;

    public Task WaitUntilAsync(string stageId, DateTimeOffset targetUtc,
        TimeProvider ignored, bool enforceMaximumWakeLateness,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        timeProvider.AdvanceTo(targetUtc);
        waits.Add((stageId, targetUtc, enforceMaximumWakeLateness));
        return Task.CompletedTask;
    }
}
