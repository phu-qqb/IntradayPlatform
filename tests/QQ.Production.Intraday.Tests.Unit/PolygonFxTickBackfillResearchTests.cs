using System.Net;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PolygonFxTickBackfillResearchTests
{
    private static readonly DateTimeOffset Start = new(2025, 07, 01, 00, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2025, 07, 01, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task Missing_credential_blocks_network_calls()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var result = await RunAsync(handler, apiKey: null);

        Assert.False(result.CredentialPresent);
        Assert.Empty(handler.Requests);
        Assert.All(result.Symbols, x => Assert.Equal(PolygonFxTickBackfillStatus.BlockedMissingCredential, x.Status));
    }

    [Fact]
    public async Task Api_key_is_not_present_in_result_strings()
    {
        const string secret = "polygon-secret-value";
        var result = await RunAsync(SinglePageHandler(), secret);
        var serialized = JsonSerializer.Serialize(result);

        Assert.False(result.ApiKeyPrinted);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backfill_writes_no_data_under_readiness_artifacts()
    {
        var directory = CreateTempDirectory();
        var artifactDirectory = Path.Combine(directory, "artifacts");
        var dataDirectory = Path.Combine(artifactDirectory, "bad");

        var result = await new PolygonFxTickBackfillResearch().RunAsync(
            Parameters(dataDirectory, artifactDirectory),
            "key",
            new HttpClient(SinglePageHandler()));

        Assert.All(result.Symbols, x => Assert.Equal(PolygonFxTickBackfillStatus.BlockedUnsafeStorageRoot, x.Status));
        Assert.False(result.RawRowsWrittenToReadinessArtifacts);
    }

    [Fact]
    public async Task Backfill_enforces_max_pages_per_symbol_and_does_not_follow_beyond_cap()
    {
        var handler = PagedHandler(pageCount: 5);
        var result = await RunAsync(handler, "key", maxPages: 3, maxRows: 1000);

        var symbol = Assert.Single(result.Symbols);
        Assert.Equal(3, symbol.PagesRequested);
        Assert.Equal(2, symbol.NextUrlFollowedCount);
        Assert.True(symbol.MaxPageCapReached);
        Assert.Equal(3, symbol.RowsWritten);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Backfill_enforces_max_rows_per_symbol()
    {
        var result = await RunAsync(PagedHandler(pageCount: 5), "key", maxPages: 3, maxRows: 2);

        var symbol = Assert.Single(result.Symbols);
        Assert.True(symbol.MaxRowCapReached);
        Assert.Equal(2, symbol.RowsWritten);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PolygonFxTickBackfillStatus.AuthFailed)]
    [InlineData(HttpStatusCode.Forbidden, PolygonFxTickBackfillStatus.NotEntitled)]
    public async Task Auth_and_entitlement_failures_stop_and_classify(HttpStatusCode status, PolygonFxTickBackfillStatus expected)
    {
        var result = await RunAsync(new FakeHandler(_ => new HttpResponseMessage(status)
        {
            Content = JsonContent("{}")
        }), "key");

        var symbol = Assert.Single(result.Symbols);
        Assert.Equal(expected, symbol.Status);
        Assert.Equal(0, symbol.RowsWritten);
    }

    [Fact]
    public async Task Rate_limit_classifies_and_stops()
    {
        var result = await RunAsync(new FakeHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = JsonContent("{}")
        }), "key");

        Assert.Equal(PolygonFxTickBackfillStatus.RateLimited, Assert.Single(result.Symbols).Status);
    }

    [Fact]
    public async Task Valid_quote_response_writes_normalized_rows_to_research_data_root()
    {
        var directory = CreateTempDirectory();
        var result = await RunAsync(SinglePageHandler(), "key", dataDirectory: directory);
        var symbol = Assert.Single(result.Symbols);

        Assert.Equal(PolygonFxTickBackfillStatus.Succeeded, symbol.Status);
        Assert.Equal(1, symbol.RowsWritten);
        Assert.NotNull(symbol.OutputFilePath);
        Assert.StartsWith(directory, symbol.OutputFilePath!, StringComparison.OrdinalIgnoreCase);
        var file = File.ReadAllText(symbol.OutputFilePath!);
        Assert.Contains("symbol,quote_timestamp_utc,bid,ask,sequence_id,window_id,provider,download_observed_at_utc,bid_exchange,ask_exchange", file, StringComparison.Ordinal);
        Assert.Contains("C:EURUSD", file, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"results":[{"participant_timestamp":1751328000000000000,"bid_price":0,"ask_price":1.2}]}""", PolygonFxTickBackfillRowRejectReason.NonPositiveBid)]
    [InlineData("""{"results":[{"participant_timestamp":1751328000000000000,"bid_price":1.1,"ask_price":0}]}""", PolygonFxTickBackfillRowRejectReason.NonPositiveAsk)]
    [InlineData("""{"results":[{"participant_timestamp":1751328000000000000,"bid_price":1.2,"ask_price":1.1}]}""", PolygonFxTickBackfillRowRejectReason.CrossedQuote)]
    [InlineData("""{"results":[{"bid_price":1.1,"ask_price":1.2}]}""", PolygonFxTickBackfillRowRejectReason.MissingTimestamp)]
    public async Task Invalid_rows_are_counted_and_not_written(string json, PolygonFxTickBackfillRowRejectReason reason)
    {
        var result = await RunAsync(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(json)
        }), "key");

        var symbol = Assert.Single(result.Symbols);
        Assert.Equal(0, symbol.RowsWritten);
        Assert.True(symbol.InvalidRowsByReason.TryGetValue(reason, out var count));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Ambiguous_duplicate_timestamp_is_counted_without_selecting_best_spread()
    {
        const string json = """{"results":[{"participant_timestamp":1751328000000000000,"bid_price":1.1,"ask_price":1.2},{"participant_timestamp":1751328000000000000,"bid_price":1.1001,"ask_price":1.1002}]}""";

        var result = await RunAsync(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(json)
        }), "key");

        var symbol = Assert.Single(result.Symbols);
        Assert.Equal(1, symbol.RowsWritten);
        Assert.True(symbol.InvalidRowsByReason.ContainsKey(PolygonFxTickBackfillRowRejectReason.AmbiguousDuplicateTimestamp));
    }

    [Fact]
    public async Task Manifest_proposal_is_unapproved_and_hash_pinned()
    {
        var result = await RunAsync(SinglePageHandler(), "key");
        var manifest = new PolygonFxTickBackfillResearch().BuildManifestProposal(result, TimeSpan.FromSeconds(5));

        Assert.False(manifest.AuthorizedForResearch);
        var file = Assert.Single(manifest.Files);
        Assert.False(file.Approved);
        Assert.NotNull(file.Sha256);
        Assert.Equal(FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay, file.AvailabilityMode);
        Assert.Equal(TimeSpan.FromSeconds(5), file.AssumedAvailabilityDelay);
    }

    [Fact]
    public async Task Manifest_proposal_requires_positive_delay_and_never_uses_event_timestamp_proxy()
    {
        var result = await RunAsync(SinglePageHandler(), "key");

        Assert.Throws<ArgumentOutOfRangeException>(() => new PolygonFxTickBackfillResearch().BuildManifestProposal(result, TimeSpan.Zero));
        var manifest = new PolygonFxTickBackfillResearch().BuildManifestProposal(result, TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(manifest.Files, x => x.AvailabilityMode == FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy);
    }

    [Fact]
    public async Task No_alpha_evaluation_or_execution_outputs_are_produced()
    {
        var result = await RunAsync(SinglePageHandler(), "key");
        var serialized = JsonSerializer.Serialize(result);

        Assert.False(result.LocalEvaluationRun);
        Assert.DoesNotContain("TargetNotional", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataSnapshot", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("FillId", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Backfill_code_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "PolygonFxTickBackfillResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxTickBackfillResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxTickCoverageBackfillR012Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedPreregisteredR016Tests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("PolygonFxTickBackfillResearch", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static Task<PolygonFxTickBackfillResult> RunAsync(
        FakeHandler handler,
        string? apiKey,
        int maxPages = 3,
        int maxRows = 100000,
        string? dataDirectory = null)
        => new PolygonFxTickBackfillResearch().RunAsync(
            Parameters(dataDirectory ?? CreateTempDirectory(), maxPages: maxPages, maxRows: maxRows),
            apiKey,
            new HttpClient(handler));

    private static PolygonFxTickBackfillParameters Parameters(
        string dataDirectory,
        string? artifactsDirectory = null,
        int maxPages = 3,
        int maxRows = 100000)
        => new(
            Symbols: ["C:EURUSD"],
            StartUtc: Start,
            EndUtc: End,
            DataDirectory: dataDirectory,
            ReadinessArtifactsDirectory: artifactsDirectory,
            MaxPagesPerSymbol: maxPages,
            MaxRowsPerSymbol: maxRows,
            MaxTotalRows: maxRows,
            PageLimit: 50000);

    private static FakeHandler SinglePageHandler()
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"results":[{"participant_timestamp":1751328000000000000,"bid_price":1.1001,"ask_price":1.1002,"sequence_number":17,"bid_exchange":1,"ask_exchange":2}]}""")
        });

    private static FakeHandler PagedHandler(int pageCount)
    {
        var page = 0;
        return new(_ =>
        {
            page++;
            var next = page < pageCount ? ",\"next_url\":\"https://api.polygon.io/next\"" : string.Empty;
            var timestamp = 1751328000000000000L + page;
            var json = $$"""{"results":[{"participant_timestamp":{{timestamp}},"bid_price":1.1001,"ask_price":1.1002,"sequence_number":{{page}}}]{{next}}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(json)
            };
        });
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "polygon-fx-backfill-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
