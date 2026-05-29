using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergencePreregisteredEvalR015Tests
{
    private static readonly DateTimeOffset StartUtc = new(2025, 07, 07, 00, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EndUtc = new(2025, 07, 09, 23, 59, 59, TimeSpan.Zero);
    private static readonly string[] Symbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];

    [Fact]
    public async Task Optional_live_r015_preregistered_backfill_uses_fixed_plan_and_writes_unapproved_manifest()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_R015_PREREGISTERED_BACKFILL"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var root = FindRepoRoot();
        var artifactsPath = Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-preregistered-eval-r015");
        Directory.CreateDirectory(artifactsPath);
        var planPath = Path.Combine(artifactsPath, "PREREGISTERED_EVAL_PLAN_R015.md");
        Assert.True(File.Exists(planPath), "PREREGISTERED_EVAL_PLAN_R015.md must be written before network calls or row loads.");

        var apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        var timestamp = Environment.GetEnvironmentVariable("R015_BACKFILL_TIMESTAMP_UTC");
        Assert.False(string.IsNullOrWhiteSpace(apiKey));
        Assert.False(string.IsNullOrWhiteSpace(timestamp));

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QQ.Production.Intraday",
            "ResearchData",
            "PolygonFxTicks",
            "polygon-fx-quotes-r015",
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
                OutputFileTag: "r015"),
            apiKey,
            httpClient,
            CancellationToken.None);

        var manifest = new PolygonFxTickBackfillResearch().BuildManifestProposal(result, TimeSpan.FromSeconds(5));
        var manifestPath = Path.Combine(artifactsPath, "POLYGON_FX_TICK_PREREGISTERED_R015_MANIFEST_PROPOSAL.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, JsonOptions()),
            CancellationToken.None);

        await File.WriteAllTextAsync(
            Path.Combine(artifactsPath, "r015-backfill-result.json"),
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
    public void R015_plan_is_fixed_and_out_of_window()
    {
        Assert.Equal(new DateTimeOffset(2025, 07, 07, 00, 00, 00, TimeSpan.Zero), StartUtc);
        Assert.Equal(new DateTimeOffset(2025, 07, 09, 23, 59, 59, TimeSpan.Zero), EndUtc);
        Assert.Equal(["C:EURUSD", "C:GBPUSD", "C:AUDUSD"], Symbols);
    }

    [Fact]
    public void R015_approval_template_defaults_to_false_when_present()
    {
        var root = FindRepoRoot();
        var templatePath = Path.Combine(
            root,
            "artifacts",
            "readiness",
            "intraday-fx-residual-divergence-preregistered-eval-r015",
            "OPERATOR_APPROVAL_FX_RESIDUAL_DIVERGENCE_R015.TEMPLATE.json");

        if (!File.Exists(templatePath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        Assert.False(document.RootElement.GetProperty("ApproveForResearchPreregisteredEvaluation").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesResearchOnly").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoProductionUse").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoExecutionOrOrders").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoParameterTuning").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNextStepIsDiagnosticEvaluationOnly").GetBoolean());
    }

    [Fact]
    public void R015_code_does_not_bind_execution_sizing_or_alpha_evaluation_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015Tests.cs"));
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
