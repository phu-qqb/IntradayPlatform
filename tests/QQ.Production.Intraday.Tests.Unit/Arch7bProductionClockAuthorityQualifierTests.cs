using System.Security.Cryptography;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bProductionClockAuthorityQualifierTests
{
    [Fact]
    public async Task Produces_three_real_shape_sequences_and_validates_every_pair()
    {
        using var temp = new TempRoot();
        var start = new DateTimeOffset(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);
        var timeProvider = new Arch7bTestTimeProvider(start);
        var pacer = new CountingPacer();
        var qualifier = new Arch7bProductionClockAuthorityQualifier(
            new SequenceClockProducer(timeProvider), timeProvider, pacer);

        var result = await qualifier.RunAsync(temp.Path,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal("PASS", result.Status);
        Assert.True(result.QualificationOnly);
        Assert.Equal(3, result.RunCount);
        Assert.Equal(3, result.PreflightValidationCount);
        Assert.Equal(3, result.CaptureValidationCount);
        Assert.Equal(3, result.PostCloseValidationCount);
        Assert.Equal(3, result.PairValidationCount);
        Assert.Equal(2_000, result.InterBatchDelayMilliseconds);
        Assert.Equal(8, result.AppliedDelayCount);
        Assert.Equal(8, pacer.DelayCount);
        Assert.Equal(9, Directory.GetFiles(temp.Path, "*.json",
            SearchOption.AllDirectories).Length);
        Assert.Equal(9, result.Runs.SelectMany(value => new[]
        {
            value.PreflightSnapshotSha256,
            value.CaptureSnapshotSha256,
            value.PostCloseSnapshotSha256
        }).Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Runs, value =>
        {
            Assert.Equal(3, value.IndividualValidationCount);
            Assert.Equal(1, value.PairValidationCount);
            Assert.True(value.SlotEndUtc >= value.SlotStartUtc);
        });
    }

    [Fact]
    public async Task Rejects_non_empty_output_root_before_producing_a_snapshot()
    {
        using var temp = new TempRoot();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "existing.txt"), "occupied");
        var start = new DateTimeOffset(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);
        var timeProvider = new Arch7bTestTimeProvider(start);
        var pacer = new CountingPacer();
        var qualifier = new Arch7bProductionClockAuthorityQualifier(
            new SequenceClockProducer(timeProvider), timeProvider, pacer);

        var exception = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            qualifier.RunAsync(temp.Path,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

        Assert.Equal(Arch7bV2Blockers.AuthorityBindingMismatch,
            exception.BlockerCode);
    }

    private sealed class CountingPacer : IArch7bClockQualificationPacer
    {
        public int DelayCount { get; private set; }

        public Task DelayAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DelayCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TempRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "arch7b-clock-qualification-tests",
            Guid.NewGuid().ToString("N"));

        public TempRoot() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }

    private sealed class SequenceClockProducer(Arch7bTestTimeProvider timeProvider)
        : IPmsShadowCaptureClockAuthorityProducer
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
            var current = timeProvider.GetUtcNow().AddMilliseconds(10);
            timeProvider.AdvanceTo(current);
            var snapshot = PmsShadowCaptureClockAuthoritySnapshot.Create(
                current,
                PmsShadowCaptureClockAuthorityMeasurementContract.HostClockSource,
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
                current.AddMinutes(-1));
            var path = Path.GetFullPath(Path.Combine(runRoot, fileName));
            PmsShadowCaptureClockAuthorityStore.WriteAtomic(path, snapshot);
            return Task.FromResult(new PmsShadowCaptureClockAuthorityProduction(
                path,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                snapshot));
        }
    }
}
