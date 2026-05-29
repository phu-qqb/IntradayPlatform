using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public enum PolygonFxTickBackfillStatus
{
    Succeeded,
    BlockedMissingCredential,
    BlockedUnsafeStorageRoot,
    AuthFailed,
    NotEntitled,
    RateLimited,
    BadRequest,
    SymbolNotFound,
    NetworkFailed,
    Unknown
}

public enum PolygonFxTickBackfillRowRejectReason
{
    MissingTimestamp,
    MissingBid,
    MissingAsk,
    TimestampParseFailed,
    NonPositiveBid,
    NonPositiveAsk,
    CrossedQuote,
    AmbiguousDuplicateTimestamp
}

public sealed record PolygonFxTickBackfillParameters(
    IReadOnlyList<string> Symbols,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string DataDirectory,
    string? ReadinessArtifactsDirectory = null,
    int MaxPagesPerSymbol = 3,
    int MaxRowsPerSymbol = 100000,
    int MaxTotalRows = 300000,
    int PageLimit = 50000,
    string OutputFileTag = "r009",
    string? WindowId = null,
    Uri? BaseUri = null)
{
    public Uri EffectiveBaseUri => BaseUri ?? new Uri("https://api.polygon.io");
}

public sealed record PolygonFxTickBackfillNormalizedRow(
    string Symbol,
    DateTimeOffset QuoteTimestampUtc,
    decimal Bid,
    decimal Ask,
    string? SequenceId,
    string? WindowId,
    string Provider,
    DateTimeOffset DownloadObservedAtUtc,
    string? BidExchange,
    string? AskExchange);

public sealed record PolygonFxTickBackfillSymbolResult(
    string Symbol,
    PolygonFxTickBackfillStatus Status,
    int PagesRequested,
    int RowsReceived,
    int RowsWritten,
    IReadOnlyDictionary<PolygonFxTickBackfillRowRejectReason, int> InvalidRowsByReason,
    bool NextUrlSeen,
    int NextUrlFollowedCount,
    bool MaxPageCapReached,
    bool MaxRowCapReached,
    string? OutputFilePath,
    long FileSizeBytes,
    string? Sha256,
    DateTimeOffset? MinQuoteTimestampUtc,
    DateTimeOffset? MaxQuoteTimestampUtc,
    bool BidFieldPresent,
    bool AskFieldPresent,
    bool TimestampFieldPresent,
    int? LastStatusCode);

public sealed record PolygonFxTickBackfillResult(
    bool CredentialPresent,
    bool ApiKeyPrinted,
    IReadOnlyList<string> SymbolsRequested,
    IReadOnlyList<PolygonFxTickBackfillSymbolResult> Symbols,
    bool RawRowsWrittenToReadinessArtifacts,
    bool LocalEvaluationRun)
{
    public int RowsWrittenTotal => Symbols.Sum(x => x.RowsWritten);
    public int PagesRequestedTotal => Symbols.Sum(x => x.PagesRequested);
    public bool PaginationFollowedWithinCap => Symbols.Any(x => x.NextUrlFollowedCount > 0);
    public bool MaxPageCapReached => Symbols.Any(x => x.MaxPageCapReached);
    public bool MaxRowCapReached => Symbols.Any(x => x.MaxRowCapReached);
}

public sealed class PolygonFxTickBackfillResearch
{
    public async Task<PolygonFxTickBackfillResult> RunAsync(
        PolygonFxTickBackfillParameters parameters,
        string? apiKey,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(httpClient);

        var symbols = parameters.Symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new(
                CredentialPresent: false,
                ApiKeyPrinted: false,
                SymbolsRequested: symbols,
                Symbols: symbols.Select(x => EmptySymbol(x, PolygonFxTickBackfillStatus.BlockedMissingCredential)).ToArray(),
                RawRowsWrittenToReadinessArtifacts: false,
                LocalEvaluationRun: false);
        }

        if (!IsSafeStorage(parameters))
        {
            return new(
                CredentialPresent: true,
                ApiKeyPrinted: false,
                SymbolsRequested: symbols,
                Symbols: symbols.Select(x => EmptySymbol(x, PolygonFxTickBackfillStatus.BlockedUnsafeStorageRoot)).ToArray(),
                RawRowsWrittenToReadinessArtifacts: false,
                LocalEvaluationRun: false);
        }

        Directory.CreateDirectory(parameters.DataDirectory);
        var results = new List<PolygonFxTickBackfillSymbolResult>();
        var totalRows = 0;
        foreach (var symbol in symbols)
        {
            if (totalRows >= parameters.MaxTotalRows)
            {
                results.Add(EmptySymbol(symbol, PolygonFxTickBackfillStatus.Succeeded));
                continue;
            }

            var result = await BackfillSymbolAsync(parameters, symbol, apiKey, httpClient, parameters.MaxTotalRows - totalRows, cancellationToken);
            totalRows += result.RowsWritten;
            results.Add(result);
            if (result.Status is PolygonFxTickBackfillStatus.AuthFailed or PolygonFxTickBackfillStatus.NotEntitled)
            {
                break;
            }
        }

        return new(
            CredentialPresent: true,
            ApiKeyPrinted: false,
            SymbolsRequested: symbols,
            Symbols: results,
            RawRowsWrittenToReadinessArtifacts: false,
            LocalEvaluationRun: false);
    }

    public FxBboResearchDataAuthorizationManifest BuildManifestProposal(
        PolygonFxTickBackfillResult result,
        TimeSpan assumedAvailabilityDelay)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (assumedAvailabilityDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(assumedAvailabilityDelay), "Availability delay must be positive.");
        }

        return new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Polygon FX BBO Backfill Proposal",
            DatasetVendor: "Polygon",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: false,
            AuthorizedBy: null,
            AuthorizationTimestampUtc: null,
            AuthorizationExpiresUtc: null,
            Files: result.Symbols
                .Where(x => x.RowsWritten > 0 && !string.IsNullOrWhiteSpace(x.OutputFilePath) && !string.IsNullOrWhiteSpace(x.Sha256))
                .Select(x => new FxBboResearchAuthorizedFileEntry(
                    Path: x.OutputFilePath!,
                    Sha256: x.Sha256,
                    Symbol: null,
                    Format: FxBboResearchFileFormat.Csv,
                    TimestampColumn: "quote_timestamp_utc",
                    BidColumn: "bid",
                    AskColumn: "ask",
                    SymbolColumn: "symbol",
                    AvailableAtColumn: null,
                    ReceivedAtColumn: null,
                    SequenceIdColumn: "sequence_id",
                    TimeZone: "UTC",
                    TimestampSemantics: "Provider quote/event timestamp normalized to UTC.",
                    AvailabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
                    AssumedAvailabilityDelay: assumedAvailabilityDelay,
                    MaxAllowedReadRows: Math.Max(1, x.RowsWritten),
                    Approved: false)).ToArray());
    }

    private static async Task<PolygonFxTickBackfillSymbolResult> BackfillSymbolAsync(
        PolygonFxTickBackfillParameters parameters,
        string symbol,
        string apiKey,
        HttpClient httpClient,
        int remainingTotalRows,
        CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(
            parameters.DataDirectory,
            $"{CleanSymbol(symbol).ToLowerInvariant()}-{parameters.StartUtc:yyyyMMdd}-{parameters.EndUtc:yyyyMMdd}-bbo-{CleanFileTag(parameters.OutputFileTag)}.csv");
        var invalid = new Dictionary<PolygonFxTickBackfillRowRejectReason, int>();
        var seenTimestampValues = new Dictionary<long, (decimal Bid, decimal Ask)>();
        var rows = new List<PolygonFxTickBackfillNormalizedRow>();
        var pages = 0;
        var rowsReceived = 0;
        var nextUrlSeen = false;
        var nextUrlFollowed = 0;
        var bidPresent = false;
        var askPresent = false;
        var timestampPresent = false;
        int? lastStatus = null;
        string? nextUrl = BuildQuoteUrl(parameters, symbol, apiKey).ToString();

        while (!string.IsNullOrWhiteSpace(nextUrl) &&
               pages < parameters.MaxPagesPerSymbol &&
               rows.Count < parameters.MaxRowsPerSymbol &&
               rows.Count < remainingTotalRows)
        {
            pages++;
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(nextUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return FinalizeSymbol(symbol, PolygonFxTickBackfillStatus.NetworkFailed, pages, rowsReceived, rows, invalid, nextUrlSeen, nextUrlFollowed, outputFile, bidPresent, askPresent, timestampPresent, lastStatus, parameters);
            }
            catch (HttpRequestException)
            {
                return FinalizeSymbol(symbol, PolygonFxTickBackfillStatus.NetworkFailed, pages, rowsReceived, rows, invalid, nextUrlSeen, nextUrlFollowed, outputFile, bidPresent, askPresent, timestampPresent, lastStatus, parameters);
            }

            using (response)
            {
                lastStatus = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    return FinalizeSymbol(symbol, ClassifyStatus(response.StatusCode), pages, rowsReceived, rows, invalid, nextUrlSeen, nextUrlFollowed, outputFile, bidPresent, askPresent, timestampPresent, lastStatus, parameters);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("next_url", out var nextElement) &&
                    nextElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(nextElement.GetString()))
                {
                    nextUrlSeen = true;
                    nextUrl = AddApiKeyToNextUrl(nextElement.GetString()!, apiKey);
                }
                else
                {
                    nextUrl = null;
                }

                if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var row in results.EnumerateArray())
                {
                    rowsReceived++;
                    var parsed = TryParseRow(symbol, row, DateTimeOffset.UtcNow, parameters.WindowId, invalid, seenTimestampValues, out var normalized);
                    bidPresent |= HasAnyProperty(row, "bid_price", "bid", "bp");
                    askPresent |= HasAnyProperty(row, "ask_price", "ask", "ap");
                    timestampPresent |= HasAnyProperty(row, "participant_timestamp", "sip_timestamp", "timestamp");
                    if (parsed)
                    {
                        rows.Add(normalized!);
                    }

                    if (rows.Count >= parameters.MaxRowsPerSymbol || rows.Count >= remainingTotalRows)
                    {
                        nextUrl = null;
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(nextUrl) && pages < parameters.MaxPagesPerSymbol)
            {
                nextUrlFollowed++;
            }
        }

        return FinalizeSymbol(symbol, PolygonFxTickBackfillStatus.Succeeded, pages, rowsReceived, rows, invalid, nextUrlSeen, nextUrlFollowed, outputFile, bidPresent, askPresent, timestampPresent, lastStatus, parameters);
    }

    private static bool TryParseRow(
        string symbol,
        JsonElement row,
        DateTimeOffset downloadObservedAtUtc,
        string? windowId,
        IDictionary<PolygonFxTickBackfillRowRejectReason, int> invalid,
        IDictionary<long, (decimal Bid, decimal Ask)> seenTimestampValues,
        out PolygonFxTickBackfillNormalizedRow? normalized)
    {
        normalized = null;
        if (!TryGetInt64(row, out var timestampNanos, "participant_timestamp", "sip_timestamp", "timestamp"))
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.MissingTimestamp);
            return false;
        }

        if (!TryGetDecimal(row, out var bid, "bid_price", "bid", "bp"))
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.MissingBid);
            return false;
        }

        if (!TryGetDecimal(row, out var ask, "ask_price", "ask", "ap"))
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.MissingAsk);
            return false;
        }

        if (bid <= 0m)
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.NonPositiveBid);
            return false;
        }

        if (ask <= 0m)
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.NonPositiveAsk);
            return false;
        }

        if (ask < bid)
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.CrossedQuote);
            return false;
        }

        if (seenTimestampValues.TryGetValue(timestampNanos, out var prior) && (prior.Bid != bid || prior.Ask != ask))
        {
            AddInvalid(invalid, PolygonFxTickBackfillRowRejectReason.AmbiguousDuplicateTimestamp);
            return false;
        }

        seenTimestampValues[timestampNanos] = (bid, ask);
        var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampNanos / 1_000_000L);
        var sequenceId = TryReadString(row, "sequence_number", "sequence", "id");
        normalized = new(
            symbol,
            timestampUtc,
            bid,
            ask,
            sequenceId,
            windowId,
            "Polygon",
            downloadObservedAtUtc,
            TryReadString(row, "bid_exchange"),
            TryReadString(row, "ask_exchange"));
        return true;
    }

    private static PolygonFxTickBackfillSymbolResult FinalizeSymbol(
        string symbol,
        PolygonFxTickBackfillStatus status,
        int pages,
        int rowsReceived,
        IReadOnlyList<PolygonFxTickBackfillNormalizedRow> rows,
        IReadOnlyDictionary<PolygonFxTickBackfillRowRejectReason, int> invalid,
        bool nextUrlSeen,
        int nextUrlFollowed,
        string outputFile,
        bool bidPresent,
        bool askPresent,
        bool timestampPresent,
        int? lastStatus,
        PolygonFxTickBackfillParameters parameters)
    {
        if (rows.Count > 0)
        {
            WriteCsv(outputFile, rows);
        }

        var file = File.Exists(outputFile) ? new FileInfo(outputFile) : null;
        return new(
            symbol,
            status,
            pages,
            rowsReceived,
            rows.Count,
            invalid,
            nextUrlSeen,
            nextUrlFollowed,
            nextUrlSeen && pages >= parameters.MaxPagesPerSymbol,
            rows.Count >= parameters.MaxRowsPerSymbol,
            file?.FullName,
            file?.Length ?? 0L,
            file is null ? null : ComputeSha256(file.FullName),
            rows.Count == 0 ? null : rows.Min(x => x.QuoteTimestampUtc),
            rows.Count == 0 ? null : rows.Max(x => x.QuoteTimestampUtc),
            bidPresent,
            askPresent,
            timestampPresent,
            lastStatus);
    }

    private static void WriteCsv(string outputFile, IReadOnlyList<PolygonFxTickBackfillNormalizedRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        var builder = new StringBuilder();
        builder.AppendLine("symbol,quote_timestamp_utc,bid,ask,sequence_id,window_id,provider,download_observed_at_utc,bid_exchange,ask_exchange");
        foreach (var row in rows.OrderBy(x => x.QuoteTimestampUtc).ThenBy(x => x.SequenceId, StringComparer.Ordinal))
        {
            builder.Append(row.Symbol).Append(',')
                .Append(row.QuoteTimestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Bid.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Ask.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(row.SequenceId)).Append(',')
                .Append(Escape(row.WindowId)).Append(',')
                .Append(row.Provider).Append(',')
                .Append(row.DownloadObservedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(row.BidExchange)).Append(',')
                .Append(Escape(row.AskExchange)).AppendLine();
        }

        File.WriteAllText(outputFile, builder.ToString(), Encoding.UTF8);
    }

    private static Uri BuildQuoteUrl(PolygonFxTickBackfillParameters parameters, string symbol, string apiKey)
    {
        var builder = new UriBuilder(new Uri(parameters.EffectiveBaseUri, $"/v3/quotes/{Uri.EscapeDataString(symbol)}"));
        var query = new Dictionary<string, string>
        {
            ["timestamp.gte"] = ToUnixNanos(parameters.StartUtc).ToString(CultureInfo.InvariantCulture),
            ["timestamp.lt"] = ToUnixNanos(parameters.EndUtc).ToString(CultureInfo.InvariantCulture),
            ["order"] = "asc",
            ["sort"] = "timestamp",
            ["limit"] = parameters.PageLimit.ToString(CultureInfo.InvariantCulture),
            ["apiKey"] = apiKey
        };
        builder.Query = string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return builder.Uri;
    }

    private static bool IsSafeStorage(PolygonFxTickBackfillParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.DataDirectory))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(parameters.ReadinessArtifactsDirectory))
        {
            var data = Path.GetFullPath(parameters.DataDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var artifacts = Path.GetFullPath(parameters.ReadinessArtifactsDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (data.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return parameters.StartUtc.Offset == TimeSpan.Zero &&
               parameters.EndUtc.Offset == TimeSpan.Zero &&
               parameters.EndUtc > parameters.StartUtc &&
               parameters.MaxPagesPerSymbol is > 0 and <= 30 &&
               parameters.MaxRowsPerSymbol is > 0 and <= 750000 &&
               parameters.MaxTotalRows is > 0 and <= 2250000 &&
               parameters.PageLimit is > 0 and <= 50000;
    }

    private static PolygonFxTickBackfillStatus ClassifyStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized => PolygonFxTickBackfillStatus.AuthFailed,
            HttpStatusCode.Forbidden => PolygonFxTickBackfillStatus.NotEntitled,
            HttpStatusCode.TooManyRequests => PolygonFxTickBackfillStatus.RateLimited,
            HttpStatusCode.BadRequest => PolygonFxTickBackfillStatus.BadRequest,
            HttpStatusCode.NotFound => PolygonFxTickBackfillStatus.SymbolNotFound,
            _ => PolygonFxTickBackfillStatus.Unknown
        };

    private static PolygonFxTickBackfillSymbolResult EmptySymbol(string symbol, PolygonFxTickBackfillStatus status)
        => new(symbol, status, 0, 0, 0, new Dictionary<PolygonFxTickBackfillRowRejectReason, int>(), false, 0, false, false, null, 0, null, null, null, false, false, false, null);

    private static string AddApiKeyToNextUrl(string nextUrl, string apiKey)
        => nextUrl.Contains("apiKey=", StringComparison.Ordinal)
            ? nextUrl
            : nextUrl.Contains('?', StringComparison.Ordinal) ? $"{nextUrl}&apiKey={Uri.EscapeDataString(apiKey)}" : $"{nextUrl}?apiKey={Uri.EscapeDataString(apiKey)}";

    private static bool TryGetDecimal(JsonElement row, out decimal value, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var property) &&
                (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value) ||
                 property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
            {
                return true;
            }
        }

        value = 0m;
        return false;
    }

    private static bool TryGetInt64(JsonElement row, out long value, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var property) &&
                (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value) ||
                 property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)))
            {
                return true;
            }
        }

        value = 0L;
        return false;
    }

    private static string? TryReadString(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var property))
            {
                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
            }
        }

        return null;
    }

    private static bool HasAnyProperty(JsonElement element, params string[] names)
        => element.ValueKind == JsonValueKind.Object && names.Any(name => element.TryGetProperty(name, out _));

    private static void AddInvalid(IDictionary<PolygonFxTickBackfillRowRejectReason, int> invalid, PolygonFxTickBackfillRowRejectReason reason)
        => invalid[reason] = invalid.TryGetValue(reason, out var count) ? count + 1 : 1;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string CleanSymbol(string symbol)
        => symbol.Replace("C:", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static string CleanFileTag(string tag)
        => string.Concat((string.IsNullOrWhiteSpace(tag) ? "research" : tag.Trim().ToLowerInvariant())
            .Select(x => char.IsLetterOrDigit(x) || x == '-' ? x : '-'));

    private static long ToUnixNanos(DateTimeOffset timestampUtc)
        => checked(timestampUtc.ToUnixTimeMilliseconds() * 1_000_000L);
}
