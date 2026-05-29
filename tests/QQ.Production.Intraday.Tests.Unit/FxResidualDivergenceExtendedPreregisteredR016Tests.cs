using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceExtendedPreregisteredR016Tests
{
    private static readonly string[] Symbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    private static readonly R016Window[] Windows =
    [
        new("window_20250714_20250716", new DateTimeOffset(2025, 07, 14, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 16, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250721_20250723", new DateTimeOffset(2025, 07, 21, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 23, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250728_20250730", new DateTimeOffset(2025, 07, 28, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 30, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250804_20250806", new DateTimeOffset(2025, 08, 04, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 08, 06, 23, 59, 59, TimeSpan.Zero))
    ];

    [Fact]
    public async Task Optional_live_r016_extended_backfill_uses_fixed_windows_and_writes_unapproved_manifest()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_R016_EXTENDED_BACKFILL"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var root = FindRepoRoot();
        var artifactsPath = Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-extended-preregistered-backfill-r016");
        Directory.CreateDirectory(artifactsPath);
        var planPath = Path.Combine(artifactsPath, "EXTENDED_PREREGISTERED_PLAN_R016.md");
        Assert.True(File.Exists(planPath), "EXTENDED_PREREGISTERED_PLAN_R016.md must be written before network calls or row loads.");

        var apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        var timestamp = Environment.GetEnvironmentVariable("R016_BACKFILL_TIMESTAMP_UTC");
        Assert.False(string.IsNullOrWhiteSpace(apiKey));
        Assert.False(string.IsNullOrWhiteSpace(timestamp));

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QQ.Production.Intraday",
            "ResearchData",
            "PolygonFxTicks",
            "polygon-fx-quotes-r016",
            timestamp);

        var results = new List<WindowBackfillResult>();
        foreach (var window in Windows)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var result = await new PolygonFxTickBackfillResearch().RunAsync(
                new PolygonFxTickBackfillParameters(
                    Symbols: Symbols,
                    StartUtc: window.StartUtc,
                    EndUtc: window.EndUtc,
                    DataDirectory: dataDirectory,
                    ReadinessArtifactsDirectory: artifactsPath,
                    MaxPagesPerSymbol: 30,
                    MaxRowsPerSymbol: 750000,
                    MaxTotalRows: 2250000,
                    PageLimit: 50000,
                    OutputFileTag: $"r016-{window.Id}",
                    WindowId: window.Id),
                apiKey,
                httpClient,
                CancellationToken.None);

            results.Add(new(window, result));
            Assert.True(result.CredentialPresent);
            Assert.False(result.ApiKeyPrinted);
            Assert.False(result.RawRowsWrittenToReadinessArtifacts);
            Assert.False(result.LocalEvaluationRun);
            Assert.Equal(Symbols, result.SymbolsRequested);
        }

        var manifest = BuildManifestProposal(results);
        await File.WriteAllTextAsync(
            Path.Combine(artifactsPath, "POLYGON_FX_TICK_EXTENDED_R016_MANIFEST_PROPOSAL.json"),
            JsonSerializer.Serialize(manifest, JsonOptions()),
            CancellationToken.None);

        await File.WriteAllTextAsync(
            Path.Combine(artifactsPath, "r016-backfill-result.json"),
            JsonSerializer.Serialize(new
            {
                Windows = results.Select(x => new
                {
                    x.Window.Id,
                    x.Window.StartUtc,
                    x.Window.EndUtc,
                    x.Result.CredentialPresent,
                    x.Result.ApiKeyPrinted,
                    x.Result.SymbolsRequested,
                    x.Result.Symbols,
                    x.Result.RawRowsWrittenToReadinessArtifacts,
                    x.Result.LocalEvaluationRun,
                    x.Result.RowsWrittenTotal,
                    x.Result.PagesRequestedTotal,
                    x.Result.PaginationFollowedWithinCap,
                    x.Result.MaxPageCapReached,
                    x.Result.MaxRowCapReached
                }),
                RowsWrittenTotal = results.Sum(x => x.Result.RowsWrittenTotal),
                PagesRequestedTotal = results.Sum(x => x.Result.PagesRequestedTotal),
                MaxPageCapReached = results.Any(x => x.Result.MaxPageCapReached),
                MaxRowCapReached = results.Any(x => x.Result.MaxRowCapReached),
                RawRowsWrittenToReadinessArtifacts = false,
                LocalEvaluationRun = false,
                ResearchDataRoot = dataDirectory
            }, JsonOptions()),
            CancellationToken.None);

        Assert.False(manifest.AuthorizedForResearch);
        Assert.All(manifest.Files, file =>
        {
            Assert.False(file.Approved);
            Assert.False(string.IsNullOrWhiteSpace(file.Sha256));
            Assert.Equal("EventTimestampPlusConfiguredDelay", file.AvailabilityMode);
            Assert.Equal("00:00:05", file.AssumedAvailabilityDelay);
        });
    }

    [Fact]
    public void R016_plan_uses_fixed_symbols_and_windows()
    {
        Assert.Equal(["C:EURUSD", "C:GBPUSD", "C:AUDUSD"], Symbols);
        Assert.Equal(4, Windows.Length);
        Assert.Equal(new DateTimeOffset(2025, 07, 14, 00, 00, 00, TimeSpan.Zero), Windows[0].StartUtc);
        Assert.Equal(new DateTimeOffset(2025, 08, 06, 23, 59, 59, TimeSpan.Zero), Windows[^1].EndUtc);
    }

    [Fact]
    public void R016_approval_template_defaults_to_false_when_present()
    {
        var root = FindRepoRoot();
        var templatePath = Path.Combine(
            root,
            "artifacts",
            "readiness",
            "intraday-fx-residual-divergence-extended-preregistered-backfill-r016",
            "OPERATOR_APPROVAL_FX_RESIDUAL_DIVERGENCE_R016.TEMPLATE.json");

        if (!File.Exists(templatePath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        Assert.False(document.RootElement.GetProperty("ApproveForResearchExtendedPreregisteredEvaluation").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesResearchOnly").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoProductionUse").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoExecutionOrOrders").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNoParameterTuning").GetBoolean());
        Assert.False(document.RootElement.GetProperty("AcknowledgesNextStepIsDiagnosticEvaluationOnly").GetBoolean());
    }

    [Fact]
    public void R016_code_does_not_bind_execution_sizing_or_alpha_evaluation_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedPreregisteredR016Tests.cs"));
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

    private static R016ManifestProposal BuildManifestProposal(IReadOnlyList<WindowBackfillResult> results)
        => new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Polygon FX BBO Extended R016 Backfill Proposal",
            DatasetVendor: "Polygon",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: false,
            AuthorizedBy: null,
            AuthorizationTimestampUtc: null,
            AuthorizationExpiresUtc: null,
            Windows: Windows.Select(x => new R016WindowEntry(x.Id, x.StartUtc, x.EndUtc)).ToArray(),
            Files: results
                .SelectMany(x => x.Result.Symbols.Select(symbol => new { x.Window, SymbolResult = symbol }))
                .Where(x => x.SymbolResult.RowsWritten > 0 && !string.IsNullOrWhiteSpace(x.SymbolResult.OutputFilePath) && !string.IsNullOrWhiteSpace(x.SymbolResult.Sha256))
                .Select(x => new R016FileEntry(
                    Path: x.SymbolResult.OutputFilePath!,
                    Sha256: x.SymbolResult.Sha256!,
                    Symbol: null,
                    Format: "Csv",
                    TimestampColumn: "quote_timestamp_utc",
                    BidColumn: "bid",
                    AskColumn: "ask",
                    SymbolColumn: "symbol",
                    AvailableAtColumn: null,
                    ReceivedAtColumn: null,
                    SequenceIdColumn: "sequence_id",
                    TimeZone: "UTC",
                    TimestampSemantics: "Provider quote/event timestamp normalized to UTC.",
                    AvailabilityMode: "EventTimestampPlusConfiguredDelay",
                    AssumedAvailabilityDelay: "00:00:05",
                    MaxAllowedReadRows: Math.Max(1, x.SymbolResult.RowsWritten),
                    Approved: false,
                    WindowId: x.Window.Id,
                    WindowStartUtc: x.Window.StartUtc,
                    WindowEndUtc: x.Window.EndUtc,
                    ResearchOnlyWarning: "Dataset requires separate operator approval before evaluation."))
                .ToArray());

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
        => new() { WriteIndented = true };

    private sealed record R016Window(string Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
    private sealed record WindowBackfillResult(R016Window Window, PolygonFxTickBackfillResult Result);
    private sealed record R016WindowEntry(string Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    private sealed record R016ManifestProposal(
        string ManifestVersion,
        string DatasetName,
        string DatasetVendor,
        string DatasetKind,
        bool AuthorizedForResearch,
        string? AuthorizedBy,
        DateTimeOffset? AuthorizationTimestampUtc,
        DateTimeOffset? AuthorizationExpiresUtc,
        IReadOnlyList<R016WindowEntry> Windows,
        IReadOnlyList<R016FileEntry> Files);

    private sealed record R016FileEntry(
        string Path,
        string Sha256,
        string? Symbol,
        string Format,
        string TimestampColumn,
        string BidColumn,
        string AskColumn,
        string? SymbolColumn,
        string? AvailableAtColumn,
        string? ReceivedAtColumn,
        string? SequenceIdColumn,
        string TimeZone,
        string TimestampSemantics,
        string AvailabilityMode,
        string AssumedAvailabilityDelay,
        int MaxAllowedReadRows,
        bool Approved,
        string WindowId,
        DateTimeOffset WindowStartUtc,
        DateTimeOffset WindowEndUtc,
        string ResearchOnlyWarning);
}
