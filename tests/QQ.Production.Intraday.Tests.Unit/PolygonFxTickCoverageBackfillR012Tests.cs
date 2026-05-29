using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PolygonFxTickCoverageBackfillR012Tests
{
    private static readonly DateTimeOffset StartUtc = new(2025, 07, 01, 00, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EndUtc = new(2025, 07, 03, 23, 59, 59, TimeSpan.Zero);
    private static readonly string[] Symbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];

    [Fact]
    public async Task Optional_live_r012_coverage_backfill_uses_fixed_plan_and_writes_unapproved_manifest()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_R012_COVERAGE_BACKFILL"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var root = FindRepoRoot();
        var artifactsPath = Path.Combine(root, "artifacts", "readiness", "intraday-polygon-fx-tick-coverage-backfill-r012");
        Directory.CreateDirectory(artifactsPath);
        var planPath = Path.Combine(artifactsPath, "COVERAGE_BACKFILL_PLAN_R012.md");
        Assert.True(File.Exists(planPath), "COVERAGE_BACKFILL_PLAN_R012.md must be written before network calls.");

        var apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        var timestamp = Environment.GetEnvironmentVariable("R012_BACKFILL_TIMESTAMP_UTC");
        Assert.False(string.IsNullOrWhiteSpace(apiKey));
        Assert.False(string.IsNullOrWhiteSpace(timestamp));

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QQ.Production.Intraday",
            "ResearchData",
            "PolygonFxTicks",
            "polygon-fx-quotes-r012",
            timestamp);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var result = await new PolygonFxTickBackfillResearch().RunAsync(
            new PolygonFxTickBackfillParameters(
                Symbols: Symbols,
                StartUtc: StartUtc,
                EndUtc: EndUtc,
                DataDirectory: dataDirectory,
                ReadinessArtifactsDirectory: artifactsPath,
                MaxPagesPerSymbol: 30,
                MaxRowsPerSymbol: 750000,
                MaxTotalRows: 2250000,
                PageLimit: 50000,
                OutputFileTag: "r012"),
            apiKey,
            httpClient,
            CancellationToken.None);

        var manifest = new PolygonFxTickBackfillResearch().BuildManifestProposal(result, TimeSpan.FromSeconds(5));
        var manifestPath = Path.Combine(artifactsPath, "POLYGON_FX_TICK_COVERAGE_BACKFILL_R012_MANIFEST_PROPOSAL.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, JsonOptions()),
            CancellationToken.None);

        await File.WriteAllTextAsync(
            Path.Combine(artifactsPath, "r012-backfill-result.json"),
            JsonSerializer.Serialize(result, JsonOptions()),
            CancellationToken.None);

        Assert.True(result.CredentialPresent);
        Assert.False(result.ApiKeyPrinted);
        Assert.False(result.RawRowsWrittenToReadinessArtifacts);
        Assert.False(result.LocalEvaluationRun);
        Assert.Equal(Symbols, result.SymbolsRequested);
        Assert.False(manifest.AuthorizedForResearch);
        Assert.All(manifest.Files, file =>
        {
            Assert.False(file.Approved);
            Assert.False(string.IsNullOrWhiteSpace(file.Sha256));
            Assert.Equal(FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay, file.AvailabilityMode);
            Assert.Equal(TimeSpan.FromSeconds(5), file.AssumedAvailabilityDelay);
        });
    }

    [Fact]
    public void R012_backfill_parameters_allow_coverage_caps_but_remain_bounded()
    {
        var parameters = new PolygonFxTickBackfillParameters(
            Symbols,
            StartUtc,
            EndUtc,
            DataDirectory: Path.Combine(Path.GetTempPath(), "polygon-fx-r012-tests"),
            MaxPagesPerSymbol: 30,
            MaxRowsPerSymbol: 750000,
            MaxTotalRows: 2250000,
            OutputFileTag: "r012");

        Assert.Equal(30, parameters.MaxPagesPerSymbol);
        Assert.Equal(750000, parameters.MaxRowsPerSymbol);
        Assert.Equal(2250000, parameters.MaxTotalRows);
        Assert.Equal(StartUtc, parameters.StartUtc);
        Assert.Equal(EndUtc, parameters.EndUtc);
        Assert.Equal(Symbols, parameters.Symbols);
    }

    [Fact]
    public void R012_code_does_not_bind_execution_sizing_or_alpha_evaluation_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxTickCoverageBackfillR012Tests.cs"));
        var forbidden = new[]
        {
            string.Concat("Market", "Data", "Snapshot"),
            string.Concat("Target", "Notional"),
            string.Concat("Quantity", "Policy"),
            string.Concat("Target", "Weight"),
            string.Concat("Core", "Execution"),
            string.Concat("Core", "Netting"),
            string.Concat("Order", "Request"),
            string.Concat("Fill", "Report"),
            string.Concat("Ledger", "Entry"),
            string.Concat("Residual", "Divergence", "Research", "Strategy")
        };

        foreach (var token in forbidden)
        {
            Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
