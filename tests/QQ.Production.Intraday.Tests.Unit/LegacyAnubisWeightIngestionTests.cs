using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LegacyAnubisWeightIngestionTests
{
    [Fact]
    public async Task ManagerContract_CreatesGenuineReadyBatchForEnabledExecutionSubset()
    {
        using var files = new LineageFiles("EURUSD Curncy;-0.125\r\nNZDUSD Curncy;0.25\r\n", "aggregated-lineage");
        var state = SeedData.Create(Utc(8, 30));
        var service = new LegacyAnubisWeightIngestionService(
            new InMemoryModelWeightBatchRepository(state),
            new InMemoryIntradayRepository(state),
            new FixedClock(Utc(8, 31)));

        var result = await service.IngestAsync(Request(files), CancellationToken.None);

        Assert.False(result.AlreadyExisted);
        Assert.Equal(2, result.SourceRowCount);
        Assert.Equal(1, result.ExecutableRowCount);
        Assert.Equal(ModelWeightSourceSystem.LegacyAnubis, result.Batch.SourceSystem);
        Assert.Equal(ModelWeightBatchStatus.Ready, result.Batch.Status);
        var row = Assert.Single(state.ModelWeightRows);
        Assert.Equal("EURUSD Curncy", row.RawSecurityId);
        Assert.Equal("EURUSD", row.Symbol);
        Assert.Equal(-0.125m, row.Weight);
    }

    [Fact]
    public async Task SameGovernedFiles_AreIdempotent()
    {
        using var files = new LineageFiles("EURUSD Curncy;-0.125\n", "aggregated-lineage");
        var state = SeedData.Create(Utc(8, 30));
        var service = new LegacyAnubisWeightIngestionService(
            new InMemoryModelWeightBatchRepository(state),
            new InMemoryIntradayRepository(state),
            new FixedClock(Utc(8, 31)));

        var first = await service.IngestAsync(Request(files), CancellationToken.None);
        var second = await service.IngestAsync(Request(files), CancellationToken.None);

        Assert.False(first.AlreadyExisted);
        Assert.True(second.AlreadyExisted);
        Assert.Equal(first.Batch.Id, second.Batch.Id);
        Assert.Single(state.ModelWeightBatches);
    }

    [Fact]
    public async Task HashMismatch_FailsBeforePersistence()
    {
        using var files = new LineageFiles("EURUSD Curncy;-0.125\n", "aggregated-lineage");
        var state = SeedData.Create(Utc(8, 30));
        var service = new LegacyAnubisWeightIngestionService(
            new InMemoryModelWeightBatchRepository(state),
            new InMemoryIntradayRepository(state),
            new FixedClock(Utc(8, 31)));
        var request = Request(files) with { ExpectedExecDeskWeightFileSha256 = new string('0', 64) };

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.IngestAsync(request, CancellationToken.None));

        Assert.Contains("does not match governed lineage", ex.Message);
        Assert.Empty(state.ModelWeightBatches);
    }

    private static LegacyAnubisWeightIngestionRequest Request(LineageFiles files) => new(
        "INFX7", files.ExecDeskPath, files.ExecDeskSha256, files.AggregatedPath, files.AggregatedSha256,
        "QQ Intraday Fund", "IntradayFxModel", Utc(8, 30), Utc(8, 45), 15, 1_000_000m,
        TargetQuantityMode.PortfolioBaseCurrencyNotional);

    private static DateTimeOffset Utc(int hour, int minute)
        => new(2026, 9, 2, hour, minute, 0, TimeSpan.Zero);

    private sealed class LineageFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"legacy-anubis-{Guid.NewGuid():N}");
        public string ExecDeskPath { get; }
        public string AggregatedPath { get; }
        public string ExecDeskSha256 { get; }
        public string AggregatedSha256 { get; }

        public LineageFiles(string execDesk, string aggregated)
        {
            Directory.CreateDirectory(root);
            ExecDeskPath = Path.Combine(root, "Weights_INFX7_20260902.txt");
            AggregatedPath = Path.Combine(root, "AggregatedWeights.txt");
            File.WriteAllText(ExecDeskPath, execDesk, new UTF8Encoding(false));
            File.WriteAllText(AggregatedPath, aggregated, new UTF8Encoding(false));
            ExecDeskSha256 = Hash(ExecDeskPath);
            AggregatedSha256 = Hash(AggregatedPath);
        }

        public void Dispose() => Directory.Delete(root, true);
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
