using System.Globalization;
using System.Net;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public enum PolygonFxEntitlementProbeClassification
{
    EntitledReferenceMetadata,
    NotEntitledReferenceMetadata,
    EntitledHistoricalQuotes,
    NotEntitledHistoricalQuotes,
    AuthFailed,
    RateLimited,
    BadRequest,
    SymbolNotFound,
    NetworkFailed,
    EndpointNotImplementedLocally,
    BlockedMissingCredential,
    BlockedUnsafeClient,
    Unknown
}

public sealed record PolygonFxEntitlementProbePlan(
    IReadOnlyList<string> Symbols,
    DateTimeOffset HistoricalQuotesFromUtc,
    DateTimeOffset HistoricalQuotesToUtc,
    int HistoricalQuotesLimit = 1,
    Uri? BaseUri = null)
{
    public Uri EffectiveBaseUri => BaseUri ?? new Uri("https://api.polygon.io");
}

public sealed record PolygonFxEntitlementProbeEndpointResult(
    string EndpointKind,
    string Symbol,
    int? StatusCode,
    PolygonFxEntitlementProbeClassification Classification,
    int ReturnedRowCount,
    bool BidFieldPresent,
    bool AskFieldPresent,
    bool TimestampFieldPresent,
    bool ExchangeOrSourceFieldPresent,
    bool PaginationTokenPresent,
    bool PaginationFollowed,
    bool RawRowsPersisted,
    string? ErrorCode = null);

public sealed record PolygonFxEntitlementProbeResult(
    bool CredentialPresent,
    string CredentialConvention,
    bool ApiKeyPrinted,
    bool NetworkProbeRun,
    IReadOnlyList<string> SymbolsProbed,
    IReadOnlyList<PolygonFxEntitlementProbeEndpointResult> EndpointResults,
    bool RawRowsPersisted,
    bool PaginationFollowed)
{
    public PolygonFxEntitlementProbeClassification ReferenceMetadataClassification =>
        EndpointResults.FirstOrDefault(x => x.EndpointKind == "ReferenceMetadata")?.Classification
        ?? PolygonFxEntitlementProbeClassification.Unknown;

    public PolygonFxEntitlementProbeClassification HistoricalQuotesClassification =>
        EndpointResults.FirstOrDefault(x => x.EndpointKind == "HistoricalQuotes")?.Classification
        ?? PolygonFxEntitlementProbeClassification.Unknown;
}

public sealed class PolygonFxEntitlementProbeResearch
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PolygonFxEntitlementProbeResult> RunAsync(
        PolygonFxEntitlementProbePlan plan,
        string? apiKey,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(httpClient);

        var symbols = plan.Symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new(
                CredentialPresent: false,
                CredentialConvention: "POLYGON_API_KEY",
                ApiKeyPrinted: false,
                NetworkProbeRun: false,
                SymbolsProbed: symbols,
                EndpointResults: symbols.Select(symbol => Blocked(symbol)).ToArray(),
                RawRowsPersisted: false,
                PaginationFollowed: false);
        }

        if (symbols.Length == 0 ||
            plan.HistoricalQuotesLimit != 1 ||
            plan.HistoricalQuotesFromUtc.Offset != TimeSpan.Zero ||
            plan.HistoricalQuotesToUtc.Offset != TimeSpan.Zero ||
            plan.HistoricalQuotesToUtc <= plan.HistoricalQuotesFromUtc)
        {
            return new(
                CredentialPresent: true,
                CredentialConvention: "POLYGON_API_KEY",
                ApiKeyPrinted: false,
                NetworkProbeRun: false,
                SymbolsProbed: symbols,
                EndpointResults: symbols.Select(symbol => new PolygonFxEntitlementProbeEndpointResult(
                    "PlanValidation",
                    symbol,
                    null,
                    PolygonFxEntitlementProbeClassification.BlockedUnsafeClient,
                    0,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    "UnsafeProbePlan")).ToArray(),
                RawRowsPersisted: false,
                PaginationFollowed: false);
        }

        var results = new List<PolygonFxEntitlementProbeEndpointResult>();

        var first = symbols[0];
        results.Add(await ProbeReferenceMetadataAsync(plan, first, apiKey, httpClient, cancellationToken));
        results.Add(await ProbeHistoricalQuotesAsync(plan, first, apiKey, httpClient, cancellationToken));

        return new(
            CredentialPresent: true,
            CredentialConvention: "POLYGON_API_KEY",
            ApiKeyPrinted: false,
            NetworkProbeRun: true,
            SymbolsProbed: [first],
            EndpointResults: results,
            RawRowsPersisted: false,
            PaginationFollowed: results.Any(x => x.PaginationFollowed));
    }

    private static async Task<PolygonFxEntitlementProbeEndpointResult> ProbeReferenceMetadataAsync(
        PolygonFxEntitlementProbePlan plan,
        string symbol,
        string apiKey,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(
            plan.EffectiveBaseUri,
            "/v3/reference/tickers",
            new Dictionary<string, string>
            {
                ["ticker"] = symbol,
                ["market"] = "fx",
                ["active"] = "true",
                ["limit"] = "1",
                ["apiKey"] = apiKey
            });

        return await SendAndSummarizeAsync("ReferenceMetadata", symbol, uri, httpClient, cancellationToken);
    }

    private static async Task<PolygonFxEntitlementProbeEndpointResult> ProbeHistoricalQuotesAsync(
        PolygonFxEntitlementProbePlan plan,
        string symbol,
        string apiKey,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(
            plan.EffectiveBaseUri,
            $"/v3/quotes/{Uri.EscapeDataString(symbol)}",
            new Dictionary<string, string>
            {
                ["timestamp.gte"] = ToUnixNanos(plan.HistoricalQuotesFromUtc).ToString(CultureInfo.InvariantCulture),
                ["timestamp.lt"] = ToUnixNanos(plan.HistoricalQuotesToUtc).ToString(CultureInfo.InvariantCulture),
                ["order"] = "asc",
                ["sort"] = "timestamp",
                ["limit"] = "1",
                ["apiKey"] = apiKey
            });

        return await SendAndSummarizeAsync("HistoricalQuotes", symbol, uri, httpClient, cancellationToken);
    }

    private static async Task<PolygonFxEntitlementProbeEndpointResult> SendAndSummarizeAsync(
        string endpointKind,
        string symbol,
        Uri uri,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var statusCode = (int)response.StatusCode;
            var classification = ClassifyHttpStatus(endpointKind, response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                return new(endpointKind, symbol, statusCode, classification, 0, false, false, false, false, false, false, false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var rows = TryGetResults(root, out var results)
                ? results.EnumerateArray().Take(1).ToArray()
                : [];
            var first = rows.FirstOrDefault();
            var hasRow = first.ValueKind == JsonValueKind.Object;
            var bidPresent = hasRow && HasAnyProperty(first, "bid_price", "bid", "bp");
            var askPresent = hasRow && HasAnyProperty(first, "ask_price", "ask", "ap");
            var timestampPresent = hasRow && HasAnyProperty(first, "participant_timestamp", "sip_timestamp", "timestamp");
            var exchangePresent = hasRow && HasAnyProperty(first, "bid_exchange", "ask_exchange", "exchange", "source");
            var paginationPresent = HasAnyProperty(root, "next_url", "nextUrl", "cursor");

            if (endpointKind == "HistoricalQuotes" && bidPresent && askPresent && timestampPresent)
            {
                classification = PolygonFxEntitlementProbeClassification.EntitledHistoricalQuotes;
            }
            else if (endpointKind == "ReferenceMetadata" && rows.Length > 0)
            {
                classification = PolygonFxEntitlementProbeClassification.EntitledReferenceMetadata;
            }
            else if (endpointKind == "ReferenceMetadata")
            {
                classification = PolygonFxEntitlementProbeClassification.SymbolNotFound;
            }
            else
            {
                classification = PolygonFxEntitlementProbeClassification.Unknown;
            }

            return new(
                endpointKind,
                symbol,
                statusCode,
                classification,
                rows.Length,
                bidPresent,
                askPresent,
                timestampPresent,
                exchangePresent,
                paginationPresent,
                PaginationFollowed: false,
                RawRowsPersisted: false);
        }
        catch (OperationCanceledException)
        {
            return new(endpointKind, symbol, null, PolygonFxEntitlementProbeClassification.NetworkFailed, 0, false, false, false, false, false, false, false, "CanceledOrTimedOut");
        }
        catch (HttpRequestException)
        {
            return new(endpointKind, symbol, null, PolygonFxEntitlementProbeClassification.NetworkFailed, 0, false, false, false, false, false, false, false, "HttpRequestFailed");
        }
        catch (JsonException)
        {
            return new(endpointKind, symbol, null, PolygonFxEntitlementProbeClassification.Unknown, 0, false, false, false, false, false, false, false, "JsonParseFailed");
        }
    }

    private static PolygonFxEntitlementProbeClassification ClassifyHttpStatus(string endpointKind, HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized => PolygonFxEntitlementProbeClassification.AuthFailed,
            HttpStatusCode.Forbidden => endpointKind == "ReferenceMetadata"
                ? PolygonFxEntitlementProbeClassification.NotEntitledReferenceMetadata
                : PolygonFxEntitlementProbeClassification.NotEntitledHistoricalQuotes,
            HttpStatusCode.TooManyRequests => PolygonFxEntitlementProbeClassification.RateLimited,
            HttpStatusCode.BadRequest => PolygonFxEntitlementProbeClassification.BadRequest,
            HttpStatusCode.NotFound => PolygonFxEntitlementProbeClassification.SymbolNotFound,
            _ => PolygonFxEntitlementProbeClassification.Unknown
        };

    private static PolygonFxEntitlementProbeEndpointResult Blocked(string symbol)
        => new(
            "Blocked",
            symbol,
            null,
            PolygonFxEntitlementProbeClassification.BlockedMissingCredential,
            0,
            false,
            false,
            false,
            false,
            false,
            false,
            false);

    private static bool TryGetResults(JsonElement root, out JsonElement results)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out results) && results.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        results = default;
        return false;
    }

    private static bool HasAnyProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static Uri BuildUri(Uri baseUri, string path, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(new Uri(baseUri, path));
        builder.Query = string.Join(
            "&",
            query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return builder.Uri;
    }

    private static long ToUnixNanos(DateTimeOffset timestampUtc)
        => checked(timestampUtc.ToUnixTimeMilliseconds() * 1_000_000L);
}
