namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Globalization;
using System.Text;

public enum LmaxFixMarketDataMessageType
{
    SnapshotFullRefresh,
    IncrementalRefresh,
    RequestReject,
    Other
}

public enum LmaxFixMarketDataRequestMode
{
    Auto,
    SnapshotOnly,
    SnapshotPlusUpdates
}

public enum LmaxFixMarketDataSymbolEncodingMode
{
    Auto,
    SecurityId,
    SecurityIdNoIdSource,
    SecurityIdAndSymbolWithIdSource,
    SecurityIdAndSymbolNoIdSource,
    SlashSymbol,
    InternalSymbol,
    SecurityIdAndSymbol
}

public sealed record LmaxFixMarketDataRequestOptions(
    string InstrumentSymbol,
    string LmaxInstrumentId,
    string LmaxSlashSymbol,
    int MarketDepth,
    LmaxFixMarketDataRequestMode RequestMode,
    int MaxWaitSeconds,
    int MaxMessages,
    LmaxFixMarketDataSymbolEncodingMode SymbolEncodingMode,
    string? SecurityIdSource,
    bool ShowFixMessages)
{
    public static LmaxFixMarketDataRequestOptions FromLabOptions(LmaxConnectivityLabOptions options)
        => new(
            options.InstrumentSymbol,
            options.LmaxInstrumentId,
            options.LmaxSlashSymbol,
            Math.Max(1, options.MarketDepth),
            options.MarketDataRequestMode,
            Math.Max(1, options.MarketDataMaxWaitSeconds),
            Math.Max(1, options.MarketDataMaxMessages),
            options.MarketDataSymbolEncodingMode,
            string.IsNullOrWhiteSpace(options.FixSecurityIdSource) ? null : options.FixSecurityIdSource,
            options.ShowFixMessages);

    public LmaxFixMarketDataRequestOptions WithEncoding(LmaxFixMarketDataSymbolEncodingMode encodingMode)
        => this with { SymbolEncodingMode = encodingMode };

    public LmaxFixMarketDataRequestOptions WithRequestMode(LmaxFixMarketDataRequestMode requestMode)
        => this with { RequestMode = requestMode };
}

public sealed record LmaxFixMarketDataEntry(
    string? MdReqId,
    LmaxFixMarketDataMessageType MessageType,
    string? Symbol,
    string? SecurityId,
    string? EntryType,
    decimal? Price,
    decimal? Size,
    string? EntryDate,
    string? EntryTime,
    string? UpdateAction);

public sealed record LmaxFixMarketDataSmokeResult(
    string Command,
    string Status,
    bool Connected,
    bool LoggedOn,
    bool RequestSent,
    bool RequestRejected,
    bool TcpConnected,
    bool TlsHandshakeCompleted,
    bool FixLogonSent,
    bool FixLoggedOn,
    bool MarketDataRequestSent,
    bool MarketDataSnapshotReceived,
    bool MarketDataRejectReceived,
    bool LogoutSent,
    string? RejectReason,
    string? RejectText,
    string? LastReceivedMsgType,
    int MessageCount,
    IReadOnlyList<LmaxFixMarketDataEntry> Entries,
    decimal? BestBid,
    decimal? BestAsk,
    decimal? Mid,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Attempts)
{
    public static LmaxFixMarketDataSmokeResult Skipped(string message, IReadOnlyList<string> safetyDecisions)
    {
        var now = DateTimeOffset.UtcNow;
        return new("fix-marketdata-snapshot-smoke", "Skipped", false, false, false, false, false, false, false, false, false, false, false, false, null, null, null, 0, [], null, null, null, now, now, message, safetyDecisions, [], []);
    }

    public static LmaxFixMarketDataSmokeResult Create(
        string status,
        string message,
        DateTimeOffset startedAtUtc,
        bool tcpConnected,
        bool tlsHandshakeCompleted,
        bool fixLogonSent,
        bool fixLoggedOn,
        bool marketDataRequestSent,
        bool marketDataSnapshotReceived,
        bool marketDataRejectReceived,
        bool logoutSent,
        string? rejectReason,
        string? rejectText,
        string? lastReceivedMsgType,
        IReadOnlyList<string> safetyDecisions,
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<string> attempts,
        IReadOnlyList<LmaxFixMarketDataEntry>? entries = null,
        decimal? bestBid = null,
        decimal? bestAsk = null,
        decimal? mid = null,
        int messageCount = 0)
        => new(
            "fix-marketdata-snapshot-smoke",
            status,
            tcpConnected,
            fixLoggedOn,
            marketDataRequestSent,
            marketDataRejectReceived,
            tcpConnected,
            tlsHandshakeCompleted,
            fixLogonSent,
            fixLoggedOn,
            marketDataRequestSent,
            marketDataSnapshotReceived,
            marketDataRejectReceived,
            logoutSent,
            rejectReason,
            rejectText,
            lastReceivedMsgType,
            messageCount,
            entries ?? [],
            bestBid,
            bestAsk,
            mid,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            message,
            safetyDecisions,
            diagnostics,
            attempts);
}

public static class LmaxFixMarketDataCodec
{
    public const char Soh = '\x01';

    public static string BuildMarketDataRequest(
        string senderCompId,
        string targetCompId,
        int sequenceNumber,
        string mdReqId,
        LmaxFixMarketDataRequestOptions options,
        bool unsubscribe = false)
    {
        var fields = new List<(string Tag, string Value)>
        {
            ("262", mdReqId),
            ("263", unsubscribe ? "2" : options.RequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates ? "1" : "0")
        };

        if (!unsubscribe)
        {
            fields.Add(("264", options.MarketDepth.ToString(CultureInfo.InvariantCulture)));
            fields.Add(("267", "2"));
            fields.Add(("269", "0"));
            fields.Add(("269", "1"));
            fields.Add(("146", "1"));
            fields.AddRange(BuildInstrumentFields(options));
        }

        return BuildMessage("V", sequenceNumber, senderCompId, targetCompId, fields);
    }

    public static IReadOnlyList<string> DescribeMarketDataRequest(string message)
    {
        var fields = ParseFields(message);
        var entryTypes = fields.Where(x => x.Tag == "269").Select(x => x.Value).ToArray();
        var instrumentFields = fields.Where(x => x.Tag is "48" or "55" or "22")
            .Select(x => $"{x.Tag}={x.Value}")
            .ToArray();
        return
        [
            $"MDReqID={GetSingle(fields, "262") ?? "(missing)"}",
            $"SubscriptionRequestType={GetSingle(fields, "263") ?? "(missing)"}",
            $"MarketDepth={GetSingle(fields, "264") ?? "(missing)"}",
            $"MDUpdateType={GetSingle(fields, "265") ?? "(not sent)"}",
            $"MdEntryTypeCount={GetSingle(fields, "267") ?? "(missing)"}",
            $"MdEntryTypesSent={string.Join(",", entryTypes)}",
            $"RelatedSymCount={GetSingle(fields, "146") ?? "(missing)"}",
            $"InstrumentFieldsSent={string.Join(",", instrumentFields)}"
        ];
    }

    public static IReadOnlyList<LmaxFixMarketDataEntry> ParseMarketDataEntries(string message)
    {
        var fields = ParseFields(message);
        var type = GetSingle(fields, "35") switch
        {
            "W" => LmaxFixMarketDataMessageType.SnapshotFullRefresh,
            "X" => LmaxFixMarketDataMessageType.IncrementalRefresh,
            "Y" => LmaxFixMarketDataMessageType.RequestReject,
            _ => LmaxFixMarketDataMessageType.Other
        };

        if (type is LmaxFixMarketDataMessageType.RequestReject)
        {
            return [new LmaxFixMarketDataEntry(GetSingle(fields, "262"), type, GetSingle(fields, "55"), GetSingle(fields, "48"), null, null, null, null, null, null)];
        }

        var mdReqId = GetSingle(fields, "262");
        var symbol = GetSingle(fields, "55");
        var securityId = GetSingle(fields, "48");
        var entries = new List<LmaxFixMarketDataEntry>();
        LmaxFixMarketDataEntry? current = null;

        foreach (var (tag, value) in fields)
        {
            if (tag is "269")
            {
                if (current is not null)
                {
                    entries.Add(current);
                }

                current = new LmaxFixMarketDataEntry(mdReqId, type, symbol, securityId, value, null, null, null, null, null);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            current = tag switch
            {
                "270" => current with { Price = ParseDecimal(value) },
                "271" => current with { Size = ParseDecimal(value) },
                "272" => current with { EntryDate = value },
                "273" => current with { EntryTime = value },
                "279" => current with { UpdateAction = value },
                _ => current
            };
        }

        if (current is not null)
        {
            entries.Add(current);
        }

        return entries;
    }

    public static (bool IsReject, string? MdReqId, string? Reason, string? Text) ParseReject(string message)
    {
        var fields = ParseFields(message);
        return (GetSingle(fields, "35") == "Y", GetSingle(fields, "262"), GetSingle(fields, "281"), GetSingle(fields, "58"));
    }

    public static string? GetTag(string message, string tag)
        => GetSingle(ParseFields(message), tag);

    public static string? GetMsgType(string message)
        => GetSingle(ParseFields(message), "35");

    public static (decimal? BestBid, decimal? BestAsk, decimal? Mid) ComputeTopOfBook(IEnumerable<LmaxFixMarketDataEntry> entries)
    {
        var bestBid = entries.Where(x => x.EntryType == "0" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Max();
        var bestAsk = entries.Where(x => x.EntryType == "1" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Min();
        decimal? bid = bestBid == 0m ? null : bestBid;
        decimal? ask = bestAsk == 0m ? null : bestAsk;
        return (bid, ask, bid.HasValue && ask.HasValue ? (bid.Value + ask.Value) / 2m : null);
    }

    public static bool ContainsTag(string message, string tag, string value)
        => message.Contains($"{Soh}{tag}={value}{Soh}", StringComparison.Ordinal) || message.StartsWith($"{tag}={value}{Soh}", StringComparison.Ordinal);

    public static string SanitizeMessage(string message)
    {
        var safe = ParseFields(message)
            .Where(x => x.Tag is not "553" and not "554")
            .Select(x =>
            {
                if (x.Tag == "49")
                {
                    return $"{x.Tag}=********";
                }

                return $"{x.Tag}={x.Value}";
            });
        return string.Join('|', safe);
    }

    public static string BuildMessage(string messageType, int sequenceNumber, string senderCompId, string targetCompId, IReadOnlyList<(string Tag, string Value)> fields)
    {
        var body = new StringBuilder();
        body.Append("35=").Append(messageType).Append(Soh);
        body.Append("34=").Append(sequenceNumber.ToString(CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("49=").Append(senderCompId).Append(Soh);
        body.Append("52=").Append(DateTimeOffset.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("56=").Append(targetCompId).Append(Soh);
        foreach (var (tag, value) in fields)
        {
            body.Append(tag).Append('=').Append(value).Append(Soh);
        }

        var head = $"8=FIX.4.4{Soh}9={Encoding.ASCII.GetByteCount(body.ToString())}{Soh}";
        var withoutChecksum = head + body;
        var checksum = Encoding.ASCII.GetBytes(withoutChecksum).Sum(x => x) % 256;
        return withoutChecksum + $"10={checksum.ToString("000", CultureInfo.InvariantCulture)}{Soh}";
    }

    public static IReadOnlyList<(string Tag, string Value)> ParseFields(string message)
        => message.Split(Soh, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .Select(x => (x[0], x[1]))
            .ToList();

    private static IEnumerable<(string Tag, string Value)> BuildInstrumentFields(LmaxFixMarketDataRequestOptions options)
    {
        var includeSecurityId = options.SymbolEncodingMode is
            LmaxFixMarketDataSymbolEncodingMode.SecurityId or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdNoIdSource or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource or
            LmaxFixMarketDataSymbolEncodingMode.Auto;
        var includeIdSource = options.SymbolEncodingMode is
            LmaxFixMarketDataSymbolEncodingMode.SecurityId or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource or
            LmaxFixMarketDataSymbolEncodingMode.Auto;
        var includeSlashSymbol = options.SymbolEncodingMode is
            LmaxFixMarketDataSymbolEncodingMode.SlashSymbol or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource or
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource or
            LmaxFixMarketDataSymbolEncodingMode.Auto;

        if (includeSecurityId)
        {
            yield return ("48", options.LmaxInstrumentId);
            if (includeIdSource && !string.IsNullOrWhiteSpace(options.SecurityIdSource))
            {
                yield return ("22", options.SecurityIdSource);
            }
        }

        if (includeSlashSymbol)
        {
            yield return ("55", options.LmaxSlashSymbol);
        }

        if (options.SymbolEncodingMode is LmaxFixMarketDataSymbolEncodingMode.InternalSymbol)
        {
            yield return ("55", options.InstrumentSymbol);
        }
    }

    private static string? GetSingle(IReadOnlyList<(string Tag, string Value)> fields, string tag)
        => fields.LastOrDefault(x => x.Tag == tag).Value;

    private static decimal? ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
