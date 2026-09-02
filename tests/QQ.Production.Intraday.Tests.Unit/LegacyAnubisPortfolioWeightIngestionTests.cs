using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LegacyAnubisPortfolioWeightIngestionTests
{
    [Fact]
    public async Task EuWeightsAreExecutableWhileUsProgrammesAreGenuinelyAbsent()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\nPLNHUF Cunrcy;0.9\n");
        files.Add("INFX10", "EURUSD Curncy;0.2\n");
        var (service, state) = Services();

        var result = await service.IngestAsync(Request([
            Absent("INFX7", "US session has not reached a scheduled observation."),
            Absent("INFX8", "US session has not reached a scheduled observation."),
            Present("INFX9", files),
            Present("INFX10", files)
        ]), CancellationToken.None);

        Assert.Equal(2, result.PresentProgrammeCount);
        Assert.Equal(2, result.AbsentProgrammeCount);
        Assert.Equal(0.3m, Assert.Single(state.ModelWeightRows).Weight);
        Assert.Equal(3, result.SourceRowCount);
        Assert.Contains("ZeroIfAbsent=true", result.Batch.Message);
        Assert.Contains("CarryForward=false", result.Batch.Message);
    }

    [Fact]
    public async Task ManagerCoefficientsAreNotAppliedAgainAndNoNormalizationOccurs()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX7", "EURUSD Curncy;0.1\n");
        files.Add("INFX8", "EURUSD Curncy;0.2\n");
        files.Add("INFX9", "EURUSD Curncy;0.3\n");
        files.Add("INFX10", "EURUSD Curncy;0.4\n");
        var (service, state) = Services();

        await service.IngestAsync(Request(ProgrammeNames.Select(name => Present(name, files)).ToArray()), CancellationToken.None);

        Assert.Equal(1.0m, Assert.Single(state.ModelWeightRows).Weight);
    }

    [Fact]
    public async Task AbsentProgrammeDoesNotCarryItsPreviousWeight()
    {
        using var firstFiles = new ProgrammeFiles();
        firstFiles.Add("INFX7", "EURUSD Curncy;0.5\n");
        firstFiles.Add("INFX9", "EURUSD Curncy;0.1\n");
        var (service, state) = Services();
        await service.IngestAsync(Request([
            Present("INFX7", firstFiles), Absent("INFX8", "No 30-minute decision."),
            Present("INFX9", firstFiles), Absent("INFX10", "No 60-minute decision.")
        ]), CancellationToken.None);

        using var secondFiles = new ProgrammeFiles();
        secondFiles.Add("INFX9", "EURUSD Curncy;0.1\n");
        var secondDecision = Decision.AddMinutes(15);
        var second = Request([
            Absent("INFX7", "No genuine weight at this decision."),
            Absent("INFX8", "No 30-minute decision."),
            Present("INFX9", secondFiles, secondDecision),
            Absent("INFX10", "No 60-minute decision.")
        ], secondDecision);

        var result = await service.IngestAsync(second, CancellationToken.None);

        var secondRow = state.ModelWeightRows.Single(x => x.BatchId == result.Batch.Id);
        Assert.Equal(0.1m, secondRow.Weight);
    }

    [Fact]
    public async Task ProgrammeFailureCanNeverBeReclassifiedAsZero()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\n");
        var (service, state) = Services();
        var request = Request([
            Absent("INFX7", "US session has not opened."),
            Absent("INFX8", "US session has not opened."),
            Present("INFX9", files),
            Failed("INFX10", "Maat exited non-zero at its scheduled timestamp.")
        ]);

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => service.IngestAsync(request, CancellationToken.None));

        Assert.Contains("cannot contribute zero", exception.Message);
        Assert.Empty(state.ModelWeightBatches);
    }

    [Fact]
    public async Task MissingOrDuplicateAuthoritativeProgrammeIsRejected()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\n");
        var (service, state) = Services();
        var request = Request([
            Absent("INFX7", "No decision."), Absent("INFX8", "No decision."),
            Present("INFX9", files), Present("INFX9", files)
        ]);

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => service.IngestAsync(request, CancellationToken.None));

        Assert.Contains("exactly one contribution", exception.Message);
        Assert.Empty(state.ModelWeightBatches);
    }

    [Fact]
    public async Task HashMismatchFailsBeforePersistence()
    {
        using var files = new ProgrammeFiles();
        files.Add("INFX9", "EURUSD Curncy;0.1\n");
        var (service, state) = Services();
        var present = Present("INFX9", files) with { ExpectedExecDeskWeightFileSha256 = new string('0', 64) };
        var request = Request([
            Absent("INFX7", "No decision."), Absent("INFX8", "No decision."), present,
            Absent("INFX10", "No decision.")
        ]);

        var exception = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => service.IngestAsync(request, CancellationToken.None));

        Assert.Contains("does not match governed lineage", exception.Message);
        Assert.Empty(state.ModelWeightBatches);
    }

    private static readonly DateTimeOffset Decision = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ProgrammeNames = ["INFX7", "INFX8", "INFX9", "INFX10"];

    private static (LegacyAnubisPortfolioWeightIngestionService Service, PlatformState State) Services()
    {
        var state = SeedData.Create(Decision);
        return (new(
            new InMemoryModelWeightBatchRepository(state),
            new InMemoryIntradayRepository(state),
            new FixedClock(Decision.AddMinutes(1))), state);
    }

    private static LegacyAnubisPortfolioWeightIngestionRequest Request(
        IReadOnlyList<LegacyAnubisProgrammeContribution> programmes,
        DateTimeOffset? decision = null)
    {
        var at = decision ?? Decision;
        return new(programmes, "QQ Intraday Fund", "IntradayFxPortfolio", at, at.AddMinutes(15),
            1_000_000m, TargetQuantityMode.PortfolioBaseCurrencyNotional);
    }

    private static LegacyAnubisProgrammeContribution Present(
        string name,
        ProgrammeFiles files,
        DateTimeOffset? at = null)
    {
        var contract = Contract(name);
        var lineage = files[name];
        return new(name, contract.Universe, contract.Model, contract.Session, contract.Frequency, contract.Coefficient,
            LegacyAnubisProgrammeContributionState.Present, at ?? Decision, lineage.ExecDeskPath,
            lineage.ExecDeskSha256, lineage.AggregatedPath, lineage.AggregatedSha256);
    }

    private static LegacyAnubisProgrammeContribution Absent(string name, string reason)
    {
        var contract = Contract(name);
        return new(name, contract.Universe, contract.Model, contract.Session, contract.Frequency, contract.Coefficient,
            LegacyAnubisProgrammeContributionState.Absent, Reason: reason);
    }

    private static LegacyAnubisProgrammeContribution Failed(string name, string reason)
    {
        var contract = Contract(name);
        return new(name, contract.Universe, contract.Model, contract.Session, contract.Frequency, contract.Coefficient,
            LegacyAnubisProgrammeContributionState.Failed, Reason: reason);
    }

    private static (int Universe, int Model, string Session, int Frequency, decimal Coefficient) Contract(string name) => name switch
    {
        "INFX7" => (54, 10, "US", 15, 4.5m),
        "INFX8" => (57, 11, "US", 30, 2.1m),
        "INFX9" => (58, 12, "EU", 15, 1.4m),
        "INFX10" => (59, 13, "EU", 60, 0.6m),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private sealed class ProgrammeFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"legacy-anubis-portfolio-{Guid.NewGuid():N}");
        private readonly Dictionary<string, Lineage> files = new(StringComparer.OrdinalIgnoreCase);

        public ProgrammeFiles() => Directory.CreateDirectory(root);
        public Lineage this[string name] => files[name];

        public void Add(string name, string execDesk)
        {
            var programmeRoot = Path.Combine(root, name);
            Directory.CreateDirectory(programmeRoot);
            var execDeskPath = Path.Combine(programmeRoot, $"Weights_{name}.txt");
            var aggregatePath = Path.Combine(programmeRoot, "AggregatedWeights.txt");
            File.WriteAllText(execDeskPath, execDesk, new UTF8Encoding(false));
            File.WriteAllText(aggregatePath, $"{name}-aggregate-lineage", new UTF8Encoding(false));
            files.Add(name, new(execDeskPath, Hash(execDeskPath), aggregatePath, Hash(aggregatePath)));
        }

        public void Dispose() => Directory.Delete(root, true);
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private sealed record Lineage(string ExecDeskPath, string ExecDeskSha256, string AggregatedPath, string AggregatedSha256);
}
