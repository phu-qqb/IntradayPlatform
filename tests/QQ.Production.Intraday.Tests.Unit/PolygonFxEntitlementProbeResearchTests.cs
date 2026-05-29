using System.Net;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PolygonFxEntitlementProbeResearchTests
{
    private static readonly PolygonFxEntitlementProbePlan Plan = new(
        Symbols: ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"],
        HistoricalQuotesFromUtc: new DateTimeOffset(2026, 05, 26, 00, 00, 00, TimeSpan.Zero),
        HistoricalQuotesToUtc: new DateTimeOffset(2026, 05, 26, 00, 01, 00, TimeSpan.Zero));

    [Fact]
    public async Task Missing_credential_blocks_network_calls()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var result = await RunAsync(handler, apiKey: null);

        Assert.False(result.CredentialPresent);
        Assert.False(result.NetworkProbeRun);
        Assert.Empty(handler.Requests);
        Assert.All(result.EndpointResults, x => Assert.Equal(PolygonFxEntitlementProbeClassification.BlockedMissingCredential, x.Classification));
    }

    [Fact]
    public async Task Api_key_is_never_written_to_probe_result_text()
    {
        const string secret = "super-secret-polygon-key";
        var result = await RunAsync(SuccessHandler(), secret);
        var serialized = JsonSerializer.Serialize(result);

        Assert.False(result.ApiKeyPrinted);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probe_does_not_follow_next_url_pagination()
    {
        var handler = SuccessHandler(withNextUrl: true);
        var result = await RunAsync(handler, "key");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(result.EndpointResults, x => x.PaginationTokenPresent);
        Assert.False(result.PaginationFollowed);
        Assert.All(result.EndpointResults, x => Assert.False(x.PaginationFollowed));
    }

    [Fact]
    public async Task Probe_caps_returned_rows_to_tiny_limit()
    {
        var handler = SuccessHandler(extraRows: true);
        var result = await RunAsync(handler, "key");

        var quote = Assert.Single(result.EndpointResults, x => x.EndpointKind == "HistoricalQuotes");
        Assert.Equal(1, quote.ReturnedRowCount);
    }

    [Fact]
    public async Task Historical_quote_response_with_bid_ask_and_timestamp_is_entitled()
    {
        var result = await RunAsync(SuccessHandler(), "key");

        var quote = Assert.Single(result.EndpointResults, x => x.EndpointKind == "HistoricalQuotes");
        Assert.Equal(PolygonFxEntitlementProbeClassification.EntitledHistoricalQuotes, quote.Classification);
        Assert.True(quote.BidFieldPresent);
        Assert.True(quote.AskFieldPresent);
        Assert.True(quote.TimestampFieldPresent);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PolygonFxEntitlementProbeClassification.AuthFailed)]
    [InlineData(HttpStatusCode.Forbidden, PolygonFxEntitlementProbeClassification.NotEntitledHistoricalQuotes)]
    public async Task Auth_and_entitlement_failures_are_classified(HttpStatusCode statusCode, PolygonFxEntitlementProbeClassification expectedHistorical)
    {
        var handler = new FakeHandler(request =>
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = JsonContent("{}")
            };
        });

        var result = await RunAsync(handler, "key");

        Assert.Contains(result.EndpointResults, x => x.EndpointKind == "HistoricalQuotes" && x.Classification == expectedHistorical);
    }

    [Fact]
    public async Task Rate_limit_is_classified()
    {
        var result = await RunAsync(new FakeHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = JsonContent("{}")
        }), "key");

        Assert.All(result.EndpointResults, x => Assert.Equal(PolygonFxEntitlementProbeClassification.RateLimited, x.Classification));
    }

    [Fact]
    public async Task Network_timeout_is_classified_as_network_failed()
    {
        var handler = new FakeHandler(_ => throw new TaskCanceledException("timeout"));
        var result = await RunAsync(handler, "key");

        Assert.All(result.EndpointResults, x => Assert.Equal(PolygonFxEntitlementProbeClassification.NetworkFailed, x.Classification));
    }

    [Fact]
    public async Task Probe_result_contains_no_raw_rows()
    {
        var result = await RunAsync(SuccessHandler(), "key");
        var serialized = JsonSerializer.Serialize(result);

        Assert.False(result.RawRowsPersisted);
        Assert.DoesNotContain("1.1001", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-05-26T00:00:00Z", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Probe_code_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "PolygonFxEntitlementProbeResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxEntitlementProbeResearchTests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("PolygonFxEntitlementProbeResearch", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static Task<PolygonFxEntitlementProbeResult> RunAsync(FakeHandler handler, string? apiKey)
        => new PolygonFxEntitlementProbeResearch().RunAsync(
            Plan,
            apiKey,
            new HttpClient(handler) { BaseAddress = new Uri("https://api.polygon.io") });

    private static FakeHandler SuccessHandler(bool withNextUrl = false, bool extraRows = false)
        => new(request =>
        {
            var json = request.RequestUri!.AbsolutePath.Contains("/quotes/", StringComparison.Ordinal)
                ? QuoteJson(withNextUrl, extraRows)
                : ReferenceJson(withNextUrl);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(json)
            };
        });

    private static string ReferenceJson(bool withNextUrl)
        => withNextUrl
            ? """{"results":[{"ticker":"C:EURUSD","market":"fx"}],"next_url":"https://api.polygon.io/next"}"""
            : """{"results":[{"ticker":"C:EURUSD","market":"fx"}]}""";

    private static string QuoteJson(bool withNextUrl, bool extraRows)
    {
        var rows = extraRows
            ? """[{"bid_price":1.1001,"ask_price":1.1002,"participant_timestamp":1770000000000000000,"bid_exchange":1},{"bid_price":1.2001,"ask_price":1.2002,"participant_timestamp":1770000001000000000}]"""
            : """[{"bid_price":1.1001,"ask_price":1.1002,"participant_timestamp":1770000000000000000,"bid_exchange":1}]""";
        return withNextUrl
            ? $$"""{"results":{{rows}},"next_url":"https://api.polygon.io/next"}"""
            : $$"""{"results":{{rows}}}""";
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

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
