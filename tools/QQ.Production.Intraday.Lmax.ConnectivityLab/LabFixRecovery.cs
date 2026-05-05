namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Globalization;
using System.Xml.Linq;

public sealed record LmaxFixCapability(string MessageName, string MsgType, bool Supported, IReadOnlyList<string> RequiredFields, IReadOnlyList<string> OptionalFields);

public sealed record LmaxFixCapabilityScanResult(string Command, string Status, string Message, string? DictionaryPath, IReadOnlyList<LmaxFixCapability> Capabilities)
{
    public static LmaxFixCapabilityScanResult Skipped(string message, string? path = null) => new("fix-capabilities", "Skipped", message, path, []);
}

public sealed record LmaxFixTradeCaptureRequestOptions(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Account, int MaxWaitSeconds, int MaxReports, bool ShowFixMessages)
{
    public static LmaxFixTradeCaptureRequestOptions From(DateTimeOffset now, int lookbackMinutes, DateTimeOffset? startUtc, DateTimeOffset? endUtc, string? account, int maxWaitSeconds, int maxReports, bool showFixMessages)
    {
        var end = endUtc ?? now;
        var start = startUtc ?? end.AddMinutes(-Math.Max(1, lookbackMinutes));
        return new(start.ToUniversalTime(), end.ToUniversalTime(), string.IsNullOrWhiteSpace(account) ? null : account.Trim(), Math.Max(1, maxWaitSeconds), Math.Max(1, maxReports), showFixMessages);
    }
}

public sealed record LmaxFixTradeCaptureReport(
    string? TradeRequestId,
    string? ExecId,
    string? SecondaryExecId,
    string? SecurityId,
    string? SecurityIdSource,
    string? Symbol,
    decimal? LastQty,
    decimal? LastPx,
    string? TradeDate,
    DateTimeOffset? TransactTime,
    string? Side,
    string? Account,
    bool LastReportRequested);

public sealed record LmaxFixTradeCaptureSmokeResult(
    string Command,
    string Status,
    bool Connected,
    bool LoggedOn,
    bool RequestSent,
    bool AckReceived,
    bool AckAccepted,
    bool RequestRejected,
    string? AckRejectText,
    string? RejectMsgType,
    string? RejectRefTagId,
    string? RejectRefMsgType,
    string? RejectReasonCode,
    string? RejectText,
    string? LastReceivedMsgType,
    int? ExpectedTradeReportCount,
    bool NoMoreReports,
    bool LogoutSent,
    int TradeReportCount,
    bool LastReportRequested,
    IReadOnlyList<LmaxFixTradeCaptureReport> Reports,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<string> Diagnostics)
{
    public static LmaxFixTradeCaptureSmokeResult Skipped(string message, IReadOnlyList<string> safetyDecisions)
    {
        var now = DateTimeOffset.UtcNow;
        return new("fix-trade-capture-smoke", "Skipped", false, false, false, false, false, false, null, null, null, null, null, null, null, null, false, false, 0, false, [], now, now, message, safetyDecisions, []);
    }
}

public sealed record LmaxFixTradeCaptureAck(
    bool IsAck,
    bool Accepted,
    string? Text,
    string? RequestId,
    string? TradeRequestType,
    string? SubscriptionRequestType,
    int? TotNumTradeReports,
    string? Result,
    string? Status);

public sealed record LmaxFixSessionReject(
    bool IsReject,
    string? RefSeqNum,
    string? RefTagId,
    string? RefMsgType,
    string? SessionRejectReason,
    string? Text);

public static class LmaxFixRecoveryCodec
{
    private static readonly IReadOnlyList<(string Name, string MsgType)> CapabilityMessages =
    [
        ("OrderStatusRequest", "H"),
        ("ExecutionReport", "8"),
        ("TradeCaptureReportRequest", "AD"),
        ("TradeCaptureReportRequestAck", "AQ"),
        ("TradeCaptureReport", "AE"),
        ("OrderMassStatusRequest", "AF"),
        ("RequestForPositions", "AN"),
        ("PositionReport", "AP")
    ];

    public static LmaxFixCapabilityScanResult ScanDefaultDictionary()
    {
        var root = FindRepoRoot();
        var path = Directory.EnumerateFiles(root, "brokerFixTradingGateway-QuickFix-DataDictionary.xml", SearchOption.AllDirectories).FirstOrDefault();
        if (path is null)
        {
            return LmaxFixCapabilityScanResult.Skipped("Dictionary file not found. Place brokerFixTradingGateway-QuickFix-DataDictionary.xml anywhere under the repo and rerun. No network call was made.");
        }

        return ScanDictionary(path);
    }

    public static LmaxFixCapabilityScanResult ScanDictionary(string path)
    {
        if (!File.Exists(path)) return LmaxFixCapabilityScanResult.Skipped("Dictionary file not found. No network call was made.", path);
        var document = XDocument.Load(path);
        var messages = document.Descendants("message").ToList();
        var capabilities = CapabilityMessages.Select(item =>
        {
            var message = messages.FirstOrDefault(x =>
                string.Equals((string?)x.Attribute("msgtype"), item.MsgType, StringComparison.Ordinal)
                || string.Equals((string?)x.Attribute("name"), item.Name, StringComparison.OrdinalIgnoreCase));
            if (message is null) return new LmaxFixCapability(item.Name, item.MsgType, false, [], []);
            var required = message.Elements().Where(x => (string?)x.Attribute("required") == "Y")
                .Select(x => ((string?)x.Attribute("name") ?? x.Name.LocalName))
                .Take(20)
                .ToList();
            var optional = message.Elements().Where(x => (string?)x.Attribute("required") != "Y")
                .Select(x => ((string?)x.Attribute("name") ?? x.Name.LocalName))
                .Take(20)
                .ToList();
            return new LmaxFixCapability(item.Name, item.MsgType, true, required, optional);
        }).ToList();
        return new("fix-capabilities", "Ok", "Scanned FIX trading dictionary. No network call was made.", path, capabilities);
    }

    public static string BuildTradeCaptureReportRequest(string senderCompId, string targetCompId, int sequenceNumber, string tradeRequestId, LmaxFixTradeCaptureRequestOptions options)
    {
        ValidateTradeRequestId(tradeRequestId);
        var fields = new List<(string Tag, string Value)>
        {
            ("568", tradeRequestId),
            ("569", "1"),
            ("263", "0")
        };
        if (!string.IsNullOrWhiteSpace(options.Account)) fields.Add(("1", options.Account!));
        fields.Add(("580", "2"));
        fields.Add(("60", FormatFixTime(options.StartUtc)));
        fields.Add(("60", FormatFixTime(options.EndUtc)));
        return LmaxFixMarketDataCodec.BuildMessage("AD", sequenceNumber, senderCompId, targetCompId, fields);
    }

    public static string GenerateTradeRequestId(DateTimeOffset now, int sequenceNumber)
    {
        var suffix = Math.Abs(sequenceNumber % 100).ToString("00", CultureInfo.InvariantCulture);
        return $"TC{now.UtcDateTime:yyMMddHHmmss}{suffix}";
    }

    public static void ValidateTradeRequestId(string tradeRequestId)
    {
        if (string.IsNullOrWhiteSpace(tradeRequestId)) throw new ArgumentException("TradeRequestID must be configured.", nameof(tradeRequestId));
        if (tradeRequestId.Length > 16) throw new ArgumentException("TradeRequestID tag 568 must be 16 characters or fewer.", nameof(tradeRequestId));
        if (tradeRequestId.Any(ch => ch > 127 || char.IsWhiteSpace(ch))) throw new ArgumentException("TradeRequestID tag 568 must be ASCII and contain no whitespace.", nameof(tradeRequestId));
    }

    public static string BuildOrderStatusRequest(string senderCompId, string targetCompId, int sequenceNumber, string clOrdId, string? account = null, string? securityId = null, string? securityIdSource = null, string? side = null, string? ordStatusReqId = null)
    {
        var fields = new List<(string Tag, string Value)> { ("11", clOrdId) };
        if (!string.IsNullOrWhiteSpace(account)) fields.Add(("1", account!));
        if (!string.IsNullOrWhiteSpace(securityId)) fields.Add(("48", securityId!));
        if (!string.IsNullOrWhiteSpace(securityIdSource)) fields.Add(("22", securityIdSource!));
        if (!string.IsNullOrWhiteSpace(side)) fields.Add(("54", side!));
        if (!string.IsNullOrWhiteSpace(ordStatusReqId)) fields.Add(("790", ordStatusReqId!));
        return LmaxFixMarketDataCodec.BuildMessage("H", sequenceNumber, senderCompId, targetCompId, fields);
    }

    public static LmaxFixTradeCaptureAck ParseTradeCaptureAck(string message)
    {
        var isAck = LmaxFixMarketDataCodec.ContainsTag(message, "35", "AQ");
        var result = LmaxFixMarketDataCodec.GetTag(message, "749");
        var status = LmaxFixMarketDataCodec.GetTag(message, "750");
        var accepted = isAck && (string.IsNullOrWhiteSpace(result) || result == "0") && (string.IsNullOrWhiteSpace(status) || status is "0" or "1");
        return new(
            isAck,
            accepted,
            LmaxFixMarketDataCodec.GetTag(message, "58"),
            LmaxFixMarketDataCodec.GetTag(message, "568"),
            LmaxFixMarketDataCodec.GetTag(message, "569"),
            LmaxFixMarketDataCodec.GetTag(message, "263"),
            ParseInt(LmaxFixMarketDataCodec.GetTag(message, "748")),
            result,
            status);
    }

    public static LmaxFixSessionReject ParseSessionReject(string message)
    {
        return new(
            LmaxFixMarketDataCodec.ContainsTag(message, "35", "3"),
            LmaxFixMarketDataCodec.GetTag(message, "45"),
            LmaxFixMarketDataCodec.GetTag(message, "371"),
            LmaxFixMarketDataCodec.GetTag(message, "372"),
            LmaxFixMarketDataCodec.GetTag(message, "373"),
            LmaxFixMarketDataCodec.GetTag(message, "58"));
    }

    public static LmaxFixTradeCaptureReport ParseTradeCaptureReport(string message)
    {
        return new(
            LmaxFixMarketDataCodec.GetTag(message, "568"),
            LmaxFixMarketDataCodec.GetTag(message, "17"),
            LmaxFixMarketDataCodec.GetTag(message, "527"),
            LmaxFixMarketDataCodec.GetTag(message, "48"),
            LmaxFixMarketDataCodec.GetTag(message, "22"),
            LmaxFixMarketDataCodec.GetTag(message, "55"),
            ParseDecimal(LmaxFixMarketDataCodec.GetTag(message, "32")),
            ParseDecimal(LmaxFixMarketDataCodec.GetTag(message, "31")),
            LmaxFixMarketDataCodec.GetTag(message, "75"),
            ParseFixTime(LmaxFixMarketDataCodec.GetTag(message, "60")),
            LmaxFixMarketDataCodec.GetTag(message, "54"),
            LmaxFixMarketDataCodec.GetTag(message, "1"),
            LmaxFixMarketDataCodec.ContainsTag(message, "912", "Y"));
    }

    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    private static int? ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    private static DateTimeOffset? ParseFixTime(string? value) => DateTimeOffset.TryParseExact(value, "yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed.ToUniversalTime() : null;
    private static string FormatFixTime(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln"))) return current.FullName;
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
