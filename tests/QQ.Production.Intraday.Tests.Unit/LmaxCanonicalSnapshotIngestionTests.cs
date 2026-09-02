using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxCanonicalSnapshotIngestionTests
{
    private static readonly DateTimeOffset Decision = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FinalizedCanonicalBboIsPersistedOnceWithManifestLineage()
    {
        await using var capture = await Capture.CreateAsync(Decision.AddSeconds(-10));
        var state = SeedData.Create(Decision);
        var service = Service(state);
        var request = capture.Request(Decision, TimeSpan.FromSeconds(30));

        var first = await service.IngestAsync(request, CancellationToken.None);
        var second = await service.IngestAsync(request, CancellationToken.None);

        var snapshot = state.MarketData.Single(x => x.Source.StartsWith("LMAX_CANONICAL:", StringComparison.Ordinal));
        Assert.Equal(1, first.ImportedSnapshotCount);
        Assert.Equal(0, first.AlreadyPersistedSnapshotCount);
        Assert.Equal(0, second.ImportedSnapshotCount);
        Assert.Equal(1, second.AlreadyPersistedSnapshotCount);
        Assert.Equal("EURUSD", Assert.Single(first.Symbols));
        Assert.False(snapshot.IsSynthetic);
        Assert.Equal(capture.Timestamp, snapshot.SourceTimestampUtc);
        Assert.Equal(capture.FinalManifestSha256, snapshot.Source["LMAX_CANONICAL:".Length..]);
    }

    [Fact]
    public async Task StaleCanonicalBboFailsClosedBeforePersistence()
    {
        await using var capture = await Capture.CreateAsync(Decision.AddMinutes(-2));
        var state = SeedData.Create(Decision);

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => Service(state).IngestAsync(capture.Request(Decision, TimeSpan.FromSeconds(30)), CancellationToken.None));

        Assert.Contains("no valid fresh BBO", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(state.MarketData, x => x.Source.StartsWith("LMAX_CANONICAL:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ManifestHashMismatchFailsClosedBeforeReplayOrPersistence()
    {
        await using var capture = await Capture.CreateAsync(Decision.AddSeconds(-10));
        var state = SeedData.Create(Decision);
        var request = capture.Request(Decision, TimeSpan.FromSeconds(30)) with
        {
            ExpectedFinalManifestSha256 = new string('0', 64)
        };

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => Service(state).IngestAsync(request, CancellationToken.None));

        Assert.Contains("hash does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(state.MarketData, x => x.Source.StartsWith("LMAX_CANONICAL:", StringComparison.Ordinal));
    }

    private static LmaxCanonicalSnapshotIngestionService Service(PlatformState state)
        => new(new InMemoryIntradayRepository(state), new InMemoryMarketDataSnapshotRepository(state), new FixedClock(Decision));

    private sealed class Capture : IAsyncDisposable
    {
        private readonly string root;
        public DateTimeOffset Timestamp { get; }
        public string FinalManifestSha256 { get; }

        private Capture(string root, DateTimeOffset timestamp, string finalManifestSha256)
        {
            this.root = root;
            Timestamp = timestamp;
            FinalManifestSha256 = finalManifestSha256;
        }

        public static async Task<Capture> CreateAsync(DateTimeOffset timestamp)
        {
            var root = Path.Combine(Path.GetTempPath(), $"lmax-canonical-import-{Guid.NewGuid():N}");
            var recorder = await CanonicalRecorderV2.CreateAsync(new CanonicalRecorderV2Options(
                root,
                "M2C1B_TEST_CAPTURE",
                "DEMO",
                "test-commit",
                "test",
                "test-baseline",
                "test-config",
                ["LMAX_MARKET_DATA_CAPTURE_ONLY"],
                ["EURUSD"],
                [],
                [],
                []),
                new ManualRecorderClock(timestamp));
            var runRoot = recorder.RunRoot;
            await using (recorder)
            {
                await recorder.RecordAsync(new CanonicalRecorderV2Event(
                    "BBO_UPDATED",
                    "LMAX_MARKET_DATA_CAPTURE_ONLY",
                    "ReadOnlyMarketDataObservationV2",
                    "v2",
                    new { source = "test" },
                    InstrumentId: "4001",
                    Symbol: "EURUSD",
                    Venue: "LMAX_DEMO_READ_ONLY",
                    SourceTimestampUtc: timestamp,
                    FixMsgSeqNum: 42,
                    QuoteEventId: "quote-42",
                    BidPrice: 1.1000m,
                    BidQuantity: 1_000_000m,
                    AskPrice: 1.1002m,
                    AskQuantity: 1_000_000m,
                    BookValid: true,
                    SourceReceiveSequence: 42));
                await recorder.CompleteAsync();
            }

            var finalManifest = Path.Combine(runRoot, "final_manifest.json");
            return new Capture(runRoot, timestamp, CanonicalRecorderV2.Sha256File(finalManifest));
        }

        public LmaxCanonicalSnapshotIngestionRequest Request(DateTimeOffset decision, TimeSpan maxAge)
            => new(root, FinalManifestSha256, decision, maxAge);

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
