using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoCycleCoordinatorTests
{
    private static readonly DateTimeOffset Decision = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessesOnlyTheExactPromotedModelRunAndDuplicateInvocationCannotSendAgain()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\n");
        var services = CreateServices();
        var oldRun = Assert.Single(services.State.ModelRuns);
        var request = Request(files);

        var first = await services.Coordinator.RunAsync(request, CancellationToken.None);
        var second = await services.Coordinator.RunAsync(request, CancellationToken.None);

        Assert.True(first.Validation.Succeeded);
        Assert.True(first.Promotion!.Succeeded);
        Assert.NotNull(first.Promotion.ModelRunId);
        Assert.Equal(ProcessModelRunStatus.Processed, first.Processing!.Status);
        Assert.Equal(ProcessModelRunStatus.AlreadyProcessed, second.Processing!.Status);
        Assert.Equal(ModelRunStatus.Received, services.State.ModelRuns.Single(x => x.Id == oldRun.Id).Status);
        Assert.DoesNotContain(services.State.TradeIntents, x => x.ModelRunId == oldRun.Id);
        Assert.Single(services.State.ParentOrders, x => x.TradeIntentId ==
            services.State.TradeIntents.Single(x => x.ModelRunId == first.Promotion.ModelRunId).Id);
        Assert.Equal(2, services.CanonicalSnapshotIngestion.InvocationCount);
    }

    [Fact]
    public async Task RejectsMismatchedCaptureAndPortfolioDecisionBeforeAnyStageRuns()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\n");
        var services = CreateServices();
        var request = Request(files) with
        {
            PortfolioWeights = Request(files).PortfolioWeights with { DecisionAtUtc = Decision.AddMinutes(15) }
        };

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => services.Coordinator.RunAsync(request, CancellationToken.None));

        Assert.Contains("decision timestamps", exception.Message);
        Assert.Equal(0, services.CanonicalSnapshotIngestion.InvocationCount);
        Assert.Empty(services.State.ModelWeightBatches);
    }

    private static LmaxDemoCycleCoordinatorRequest Request(ProgrammeFiles files)
        => new(
            "20260915T080000Z",
            new LmaxCanonicalSnapshotIngestionRequest("C:/private/cycle", new string('A', 64), Decision, TimeSpan.FromSeconds(60)),
            new LegacyAnubisPortfolioWeightIngestionRequest(
                [
                    Absent("INFX7", "No US decision at this EU timestamp."),
                    Absent("INFX8", "No US decision at this EU timestamp."),
                    Present("INFX9", files),
                    Absent("INFX10", "No 60-minute decision at this timestamp.")
                ],
                "QQ Intraday Fund",
                "IntradayFxPortfolio",
                Decision,
                Decision.AddMinutes(15),
                1_000_000m,
                TargetQuantityMode.PortfolioBaseCurrencyNotional));

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(Decision);
        var clock = new FixedClock(Decision);
        var intraday = new InMemoryIntradayRepository(state);
        var batches = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intraday, clock);
        var canonical = new RecordingCanonicalSnapshotIngestion();
        var portfolio = new LegacyAnubisPortfolioWeightIngestionService(batches, intraday, clock);
        var promotion = new ModelWeightPromotionService(batches, intraday, integrity, clock);
        var processing = new ProcessModelRunService(
            intraday,
            new FakeLmaxGateway(new FakeLmaxOptions { Behavior = FakeLmaxBehavior.FullFill }, clock),
            new FakeBrokerPositionProvider(state, clock),
            clock,
            integrity);
        return new TestServices(state, canonical, new LmaxDemoCycleCoordinator(canonical, portfolio, promotion, processing));
    }

    private static LegacyAnubisProgrammeContribution Present(string name, ProgrammeFiles files)
    {
        var (universe, model, session, frequency, coefficient) = Contract(name);
        var lineage = files[name];
        return new LegacyAnubisProgrammeContribution(name, universe, model, session, frequency, coefficient,
            LegacyAnubisProgrammeContributionState.Present, Decision, lineage.ExecDeskPath, lineage.ExecDeskSha256,
            lineage.AggregatedPath, lineage.AggregatedSha256);
    }

    private static LegacyAnubisProgrammeContribution Absent(string name, string reason)
    {
        var (universe, model, session, frequency, coefficient) = Contract(name);
        return new LegacyAnubisProgrammeContribution(name, universe, model, session, frequency, coefficient,
            LegacyAnubisProgrammeContributionState.Absent, Reason: reason);
    }

    private static (int Universe, int Model, string Session, int Frequency, decimal Coefficient) Contract(string name) => name switch
    {
        "INFX7" => (54, 10, "US", 15, 4.5m),
        "INFX8" => (57, 11, "US", 30, 2.1m),
        "INFX9" => (58, 12, "EU", 15, 1.4m),
        "INFX10" => (59, 13, "EU", 60, 0.6m),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private sealed class RecordingCanonicalSnapshotIngestion : ILmaxCanonicalSnapshotIngestionService
    {
        public int InvocationCount { get; private set; }

        public Task<LmaxCanonicalSnapshotIngestionResult> IngestAsync(
            LmaxCanonicalSnapshotIngestionRequest request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(new LmaxCanonicalSnapshotIngestionResult(
                "capture-20260915T080000Z",
                request.ExpectedFinalManifestSha256,
                1,
                0,
                ["EURUSD"]));
        }
    }

    private sealed class ProgrammeFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"lmax-demo-cycle-{Guid.NewGuid():N}");
        private readonly Dictionary<string, Lineage> files = new(StringComparer.OrdinalIgnoreCase);

        public ProgrammeFiles() => Directory.CreateDirectory(root);
        public Lineage this[string name] => files[name];

        public void Add(string name, string weights)
        {
            var directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            var execDeskPath = Path.Combine(directory, "Weights.txt");
            var aggregatedPath = Path.Combine(directory, "AggregatedWeights.txt");
            File.WriteAllText(execDeskPath, weights, new UTF8Encoding(false));
            File.WriteAllText(aggregatedPath, name + " aggregate lineage", new UTF8Encoding(false));
            files[name] = new Lineage(execDeskPath, Hash(execDeskPath), aggregatedPath, Hash(aggregatedPath));
        }

        public void Dispose() => Directory.Delete(root, true);
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private sealed record Lineage(string ExecDeskPath, string ExecDeskSha256, string AggregatedPath, string AggregatedSha256);
    private sealed record TestServices(PlatformState State, RecordingCanonicalSnapshotIngestion CanonicalSnapshotIngestion, LmaxDemoCycleCoordinator Coordinator);
}
