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

public enum LmaxFixTradeCaptureSide
{
    Buy,
    Sell
}

public enum LmaxFixExecutionReportType
{
    New,
    Canceled,
    Rejected,
    Trade,
    OrderStatus,
    Expired,
    Replaced,
    PendingCancel,
    PendingNew,
    Unknown
}

public enum LmaxFixOrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Rejected,
    Expired,
    PendingCancel,
    PendingNew,
    Unknown
}

public enum LmaxFixInternalOrderEventType
{
    OrderAck,
    OrderReject,
    Fill,
    PartialFill,
    CancelAck,
    Expired,
    Unknown
}

public enum LmaxFixLifecycleConsistencyStatus
{
    Passed,
    Failed,
    Warning,
    NotApplicable
}

public enum LmaxFixDemoOrderSide
{
    Buy,
    Sell
}

public enum LmaxFixDemoOrderType
{
    Market,
    Limit
}

public enum LmaxFixDemoOrderTimeInForce
{
    IOC,
    FOK
}

public sealed record LmaxFixDemoOrderSafetyDecision(string Gate, bool Passed, string Message);

public sealed record LmaxFixDemoOrderRequest(
    string InstrumentSymbol,
    string LmaxInstrumentId,
    LmaxFixDemoOrderSide Side,
    LmaxFixDemoOrderType OrderType,
    LmaxFixDemoOrderTimeInForce TimeInForce,
    decimal VenueQuantity,
    decimal? LimitPrice,
    decimal? MaxNotionalUsd,
    string? ClientOrderId,
    string? Account,
    bool ConfirmDemoOrder,
    bool DryRun,
    int MaxWaitSeconds,
    bool ShowFixMessages,
    bool IncludeHandlInst = false)
{
    public static LmaxFixDemoOrderRequest From(LmaxConnectivityLabOptions options, string? side, string? orderType, string? timeInForce, decimal? quantity, decimal? limitPrice, decimal? maxNotionalUsd, string? clientOrderId, string? account, bool confirmDemoOrder, bool dryRun, int maxWaitSeconds, bool showFixMessages)
        => new(
            options.InstrumentSymbol,
            options.LmaxInstrumentId,
            Enum.TryParse<LmaxFixDemoOrderSide>(side, ignoreCase: true, out var parsedSide) ? parsedSide : LmaxFixDemoOrderSide.Buy,
            Enum.TryParse<LmaxFixDemoOrderType>(orderType, ignoreCase: true, out var parsedType) ? parsedType : LmaxFixDemoOrderType.Market,
            Enum.TryParse<LmaxFixDemoOrderTimeInForce>(timeInForce, ignoreCase: true, out var parsedTif) ? parsedTif : LmaxFixDemoOrderTimeInForce.IOC,
            quantity ?? 0.1m,
            limitPrice,
            maxNotionalUsd ?? options.MaxDemoOrderNotionalUsd,
            string.IsNullOrWhiteSpace(clientOrderId) ? null : clientOrderId.Trim(),
            string.IsNullOrWhiteSpace(account) ? null : account.Trim(),
            confirmDemoOrder,
            dryRun,
            Math.Max(1, maxWaitSeconds),
            showFixMessages);
}

public sealed record LmaxFixDemoOrderLifecycleResult(
    string Command,
    string Status,
    bool Connected,
    bool LoggedOn,
    bool OrderSent,
    bool ExecutionReportReceived,
    bool TerminalExecutionReportReceived,
    bool RequestRejected,
    string? RejectMsgType,
    string? RejectRefTagId,
    string? RejectRefMsgType,
    string? RejectReasonCode,
    string? RejectText,
    string? FinalStatus,
    bool LogoutSent,
    string? ClientOrderId,
    string? LastReceivedMsgType,
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<LmaxFixDemoOrderSafetyDecision> DemoSafetyDecisions,
    IReadOnlyList<string> Diagnostics)
{
    public static LmaxFixDemoOrderLifecycleResult Skipped(string message, IReadOnlyList<string> safetyDecisions, IReadOnlyList<LmaxFixDemoOrderSafetyDecision>? demoSafetyDecisions = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new("fix-demo-order-lifecycle", "Skipped", false, false, false, false, false, false, null, null, null, null, null, null, false, null, null, [], now, now, message, safetyDecisions, demoSafetyDecisions ?? [], []);
    }
}

public sealed record LmaxFixLifecycleOrderSubmission(
    bool OrderSent,
    string? ClientOrderId,
    string? BrokerOrderId,
    int ExecutionReportCount,
    int FillExecutionReportCount,
    string? FinalOrdStatus,
    string? FinalExecType,
    decimal? CumQty,
    decimal? LeavesQty,
    decimal? AvgPx,
    string? LastFillExecId,
    string? LastFillSecondaryExecId,
    decimal? LastFillQty,
    decimal? LastFillPx,
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports);

public sealed record LmaxFixLifecycleOrderStatusRecovery(
    bool OrderStatusReceived,
    string? BrokerOrderId,
    string? OrdStatus,
    decimal? CumQty,
    decimal? LeavesQty,
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports);

public sealed record LmaxFixLifecycleTradeCaptureRecovery(
    bool TradeCaptureReceived,
    int TradeCaptureReportCount,
    IReadOnlyList<string> TradeCaptureExecIds,
    IReadOnlyList<LmaxFixTradeCaptureReport> Reports);

public sealed record LmaxFixLifecycleConsistencyCheck(
    string Name,
    LmaxFixLifecycleConsistencyStatus Status,
    string Message,
    string? Expected = null,
    string? Actual = null);

public sealed record LmaxFixLifecycleEvidenceReport(
    string? ClientOrderId,
    string? BrokerOrderId,
    string InstrumentSymbol,
    string SecurityId,
    LmaxFixDemoOrderSide Side,
    decimal RequestedQuantity,
    LmaxFixDemoOrderType RequestedOrderType,
    LmaxFixDemoOrderTimeInForce RequestedTimeInForce,
    bool OrderSent,
    int ExecutionReportCount,
    int FillExecutionReportCount,
    string? FinalOrdStatus,
    string? FinalExecType,
    decimal? CumQty,
    decimal? LeavesQty,
    decimal? AvgPx,
    string? LastFillExecId,
    string? LastFillSecondaryExecId,
    decimal? LastFillQty,
    decimal? LastFillPx,
    bool OrderStatusReceived,
    string? OrderStatusOrdStatus,
    decimal? OrderStatusCumQty,
    decimal? OrderStatusLeavesQty,
    bool TradeCaptureReceived,
    int TradeCaptureReportCount,
    IReadOnlyList<string> TradeCaptureExecIds,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<LmaxFixLifecycleConsistencyCheck> ConsistencyChecks,
    LmaxFixLifecycleOrderSubmission OrderSubmission,
    LmaxFixLifecycleOrderStatusRecovery? OrderStatusRecovery,
    LmaxFixLifecycleTradeCaptureRecovery? TradeCaptureRecovery);

public sealed record LmaxFixLifecycleEvidenceResult(
    string Command,
    string Status,
    LmaxFixDemoOrderLifecycleResult OrderSubmission,
    LmaxFixOrderStatusSmokeResult? OrderStatusRecovery,
    LmaxFixTradeCaptureSmokeResult? TradeCaptureRecovery,
    LmaxFixLifecycleEvidenceReport EvidenceReport,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<string> Diagnostics);

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
    bool LastReportRequested,
    string? TradeReportId = null,
    string? OrderId = null,
    string? ClOrdId = null,
    string? InstrumentId = null,
    string? InternalSymbol = null,
    LmaxFixTradeCaptureSide? NormalizedSide = null,
    string? RawFixMessageSanitized = null,
    DateTimeOffset? ParsedAtUtc = null,
    IReadOnlyList<string>? Warnings = null,
    bool CanMapToEodIndividualTrade = false,
    IReadOnlyList<string>? MissingForEodComparison = null);

public sealed record LmaxFixTradeCaptureEodShape(
    string? ExecutionId,
    string? MtfExecutionId,
    DateTimeOffset? TimestampUtc,
    decimal? TradeQuantity,
    decimal? TradePrice,
    string? TradeDate,
    string? InstrumentId,
    string? SecurityId,
    string? Symbol,
    string? InstructionId,
    string? OrderId,
    string? AccountId,
    decimal? UnitsBoughtSold,
    decimal? NotionalValue,
    string? TradeUti);

public sealed record LmaxFixTradeCaptureNormalizationResult(
    LmaxFixTradeCaptureReport Report,
    LmaxFixTradeCaptureEodShape EodShape,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingForEodComparison);

public sealed record LmaxFixExecutionReport(
    string? ExecId,
    string? OrderId,
    string? ClOrdId,
    string? OrigClOrdId,
    LmaxFixExecutionReportType ExecType,
    string? ExecTypeRaw,
    LmaxFixOrderStatus OrdStatus,
    string? OrdStatusRaw,
    string? SecurityId,
    string? SecurityIdSource,
    string? Symbol,
    string? InternalSymbol,
    LmaxFixTradeCaptureSide? Side,
    string? SideRaw,
    decimal? OrderQty,
    decimal? LeavesQty,
    decimal? CumQty,
    decimal? LastQty,
    decimal? LastPx,
    decimal? AvgPx,
    decimal? Price,
    decimal? StopPx,
    string? TimeInForce,
    string? TimeInForceRaw,
    string? OrdType,
    string? OrdTypeRaw,
    DateTimeOffset? TransactTimeUtc,
    string? Text,
    string? Account,
    string? RawFixMessageSanitized,
    DateTimeOffset ParsedAtUtc,
    IReadOnlyList<string> Warnings,
    bool CanMapToInternalOrderEvent,
    IReadOnlyList<string> MissingForInternalOrderEvent);

public sealed record LmaxFixExecutionReportNormalizationResult(
    LmaxFixExecutionReport Report,
    LmaxFixInternalOrderEvent InternalEvent,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingForInternalOrderEvent);

public sealed record LmaxFixInternalOrderEvent(
    LmaxFixInternalOrderEventType EventType,
    string? ExecId,
    string? OrderId,
    string? ClOrdId,
    string? InternalSymbol,
    LmaxFixTradeCaptureSide? Side,
    decimal? LastQty,
    decimal? LastPx,
    decimal? CumQty,
    decimal? LeavesQty,
    DateTimeOffset? TransactTimeUtc,
    string? Message);

public sealed record LmaxFixOrderStatusRequest(
    string ClOrdId,
    string? Account,
    string? SecurityId,
    string? SecurityIdSource,
    string? Side,
    string? OrdStatusReqId);

public sealed record LmaxFixOrderStatusRequestBuilderResult(
    string Status,
    string Message,
    string? FixMessageSanitized,
    IReadOnlyList<string> Warnings);

public sealed record LmaxFixOrderStatusSmokeRequest(
    string? ClOrdId,
    string? Account,
    string? SecurityId,
    string? SecurityIdSource,
    string? Side,
    string? OrdStatusReqId,
    int MaxWaitSeconds,
    bool ShowFixMessages);

public sealed record LmaxFixOrderStatusSmokeResult(
    string Command,
    string Status,
    bool Connected,
    bool LoggedOn,
    bool RequestSent,
    bool ExecutionReportReceived,
    bool RequestRejected,
    string? RejectRefTagId,
    string? RejectRefMsgType,
    string? RejectText,
    string? ClOrdId,
    string? BrokerOrderId,
    string? FinalOrdStatus,
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool LogoutSent,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<string> Diagnostics)
{
    public static LmaxFixOrderStatusSmokeResult Skipped(string message, IReadOnlyList<string> safetyDecisions, string? clOrdId = null, IReadOnlyList<string>? diagnostics = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new("fix-order-status-smoke", "Skipped", false, false, false, false, false, null, null, null, clOrdId, null, null, [], now, now, false, message, safetyDecisions, diagnostics ?? []);
    }
}

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

    public static LmaxFixOrderStatusRequestBuilderResult BuildOrderStatusRequestDryRun(string senderCompId, string targetCompId, int sequenceNumber, LmaxFixOrderStatusRequest request)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ClOrdId))
        {
            return new("Blocked", "ClOrdID is required for OrderStatusRequest dry-run.", null, warnings);
        }

        if (request.ClOrdId.Length > 32)
        {
            warnings.Add("ClOrdID is longer than 32 characters; LMAX-specific max length was not confirmed, so this is a warning only.");
        }

        var message = BuildOrderStatusRequest(senderCompId, targetCompId, sequenceNumber, request.ClOrdId, request.Account, request.SecurityId, request.SecurityIdSource, request.Side, request.OrdStatusReqId);
        return new("Ok", $"Built OrderStatusRequest dry-run for ClOrdID={request.ClOrdId}.", LmaxFixMarketDataCodec.SanitizeMessage(message), warnings);
    }

    public static string BuildNewOrderSingle(string senderCompId, string targetCompId, int sequenceNumber, LmaxFixDemoOrderRequest request, string clOrdId, string? securityIdSource)
    {
        ValidateClientOrderId(clOrdId);
        var fields = new List<(string Tag, string Value)>
        {
            ("11", clOrdId)
        };
        if (request.IncludeHandlInst) fields.Add(("21", "1"));
        fields.Add(("48", request.LmaxInstrumentId));
        if (!string.IsNullOrWhiteSpace(securityIdSource)) fields.Add(("22", securityIdSource!));
        if (!string.IsNullOrWhiteSpace(request.InstrumentSymbol)) fields.Add(("55", request.InstrumentSymbol));
        fields.Add(("54", request.Side == LmaxFixDemoOrderSide.Buy ? "1" : "2"));
        fields.Add(("60", FormatFixTime(DateTimeOffset.UtcNow)));
        fields.Add(("38", request.VenueQuantity.ToString(CultureInfo.InvariantCulture)));
        fields.Add(("40", request.OrderType == LmaxFixDemoOrderType.Market ? "1" : "2"));
        if (request.OrderType == LmaxFixDemoOrderType.Limit)
        {
            if (request.LimitPrice is null) throw new ArgumentException("LimitPrice is required for Limit demo orders.", nameof(request));
            fields.Add(("44", request.LimitPrice.Value.ToString(CultureInfo.InvariantCulture)));
        }

        fields.Add(("59", request.TimeInForce == LmaxFixDemoOrderTimeInForce.IOC ? "3" : "4"));
        if (!string.IsNullOrWhiteSpace(request.Account)) fields.Add(("1", request.Account!));
        return LmaxFixMarketDataCodec.BuildMessage("D", sequenceNumber, senderCompId, targetCompId, fields);
    }

    public static string GenerateClientOrderId(DateTimeOffset now, int sequenceNumber)
    {
        var suffix = Math.Abs(sequenceNumber % 100).ToString("00", CultureInfo.InvariantCulture);
        return $"DL{now.UtcDateTime:yyMMddHHmmss}{suffix}";
    }

    public static void ValidateClientOrderId(string clOrdId)
    {
        if (string.IsNullOrWhiteSpace(clOrdId)) throw new ArgumentException("ClOrdID must be configured.", nameof(clOrdId));
        if (clOrdId.Length > 20) throw new ArgumentException("ClOrdID tag 11 must be 20 characters or fewer for this demo lab command.", nameof(clOrdId));
        if (clOrdId.Any(ch => ch > 127 || char.IsWhiteSpace(ch))) throw new ArgumentException("ClOrdID tag 11 must be ASCII and contain no whitespace.", nameof(clOrdId));
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
        => NormalizeTradeCaptureReport(message, null).Report;

    public static LmaxFixTradeCaptureNormalizationResult NormalizeTradeCaptureReport(string message, LmaxConnectivityLabOptions? options)
    {
        var warnings = new List<string>();
        var missing = new List<string>();
        var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
        if (msgType != "AE") warnings.Add($"Expected MsgType AE but received {msgType ?? "(missing)"}.");

        var securityId = LmaxFixMarketDataCodec.GetTag(message, "48");
        var symbol = LmaxFixMarketDataCodec.GetTag(message, "55");
        var internalSymbol = ResolveInternalSymbol(securityId, symbol, options);
        var instrumentId = securityId;
        var side = LmaxFixMarketDataCodec.GetTag(message, "54");
        var normalizedSide = ParseSide(side, warnings);
        var lastQty = ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "32"), "LastQty(32)", warnings);
        var lastPx = ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "31"), "LastPx(31)", warnings);
        var transactTime = ParseFixTimeWithWarning(LmaxFixMarketDataCodec.GetTag(message, "60"), "TransactTime(60)", warnings);
        var execId = LmaxFixMarketDataCodec.GetTag(message, "17");
        var secondaryExecId = LmaxFixMarketDataCodec.GetTag(message, "527");
        var account = LmaxFixMarketDataCodec.GetTag(message, "1");
        var orderId = LmaxFixMarketDataCodec.GetTag(message, "37");
        var clOrdId = LmaxFixMarketDataCodec.GetTag(message, "11");
        var tradeReportId = LmaxFixMarketDataCodec.GetTag(message, "571");
        var tradeDate = LmaxFixMarketDataCodec.GetTag(message, "75");

        if (string.IsNullOrWhiteSpace(execId)) warnings.Add("Missing ExecID(17).");
        if (string.IsNullOrWhiteSpace(securityId) && string.IsNullOrWhiteSpace(symbol)) warnings.Add("Missing both SecurityID(48) and Symbol(55).");
        if (lastQty is null && lastPx is null) warnings.Add("Missing both LastQty(32) and LastPx(31).");

        if (string.IsNullOrWhiteSpace(clOrdId)) missing.Add("Missing ClOrdID / instruction id.");
        if (string.IsNullOrWhiteSpace(orderId)) missing.Add("Missing OrderID.");
        if (string.IsNullOrWhiteSpace(symbol) && string.IsNullOrWhiteSpace(internalSymbol)) missing.Add("Missing Symbol.");
        if (string.IsNullOrWhiteSpace(account)) missing.Add("Missing Account.");
        missing.Add("Missing TradeUTI; not currently available from parsed FIX AE.");

        var units = normalizedSide switch
        {
            LmaxFixTradeCaptureSide.Buy when lastQty.HasValue => lastQty,
            LmaxFixTradeCaptureSide.Sell when lastQty.HasValue => -lastQty,
            _ => null
        };
        var notional = lastQty.HasValue && lastPx.HasValue ? lastQty.Value * lastPx.Value : (decimal?)null;
        var canMap = !string.IsNullOrWhiteSpace(execId)
            && (!string.IsNullOrWhiteSpace(securityId) || !string.IsNullOrWhiteSpace(symbol) || !string.IsNullOrWhiteSpace(internalSymbol))
            && lastQty.HasValue
            && lastPx.HasValue;

        var report = new LmaxFixTradeCaptureReport(
            LmaxFixMarketDataCodec.GetTag(message, "568"),
            execId,
            secondaryExecId,
            securityId,
            LmaxFixMarketDataCodec.GetTag(message, "22"),
            symbol,
            lastQty,
            lastPx,
            tradeDate,
            transactTime,
            side,
            account,
            LmaxFixMarketDataCodec.ContainsTag(message, "912", "Y"),
            tradeReportId,
            orderId,
            clOrdId,
            instrumentId,
            internalSymbol,
            normalizedSide,
            LmaxFixMarketDataCodec.SanitizeMessage(message),
            DateTimeOffset.UtcNow,
            warnings,
            canMap,
            missing);

        return new(report, LmaxFixTradeCaptureToEodShapeMapper.Map(report), warnings, missing);
    }

    public static LmaxFixExecutionReportNormalizationResult NormalizeExecutionReport(string message, LmaxConnectivityLabOptions? options)
    {
        var warnings = new List<string>();
        var missing = new List<string>();
        var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
        if (msgType != "8") warnings.Add($"Expected MsgType 8 but received {msgType ?? "(missing)"}.");

        var execTypeRaw = LmaxFixMarketDataCodec.GetTag(message, "150");
        var ordStatusRaw = LmaxFixMarketDataCodec.GetTag(message, "39");
        var sideRaw = LmaxFixMarketDataCodec.GetTag(message, "54");
        var ordTypeRaw = LmaxFixMarketDataCodec.GetTag(message, "40");
        var tifRaw = LmaxFixMarketDataCodec.GetTag(message, "59");
        var securityId = LmaxFixMarketDataCodec.GetTag(message, "48");
        var symbol = LmaxFixMarketDataCodec.GetTag(message, "55");
        var execId = LmaxFixMarketDataCodec.GetTag(message, "17");
        var orderId = LmaxFixMarketDataCodec.GetTag(message, "37");
        var clOrdId = LmaxFixMarketDataCodec.GetTag(message, "11");

        var report = new LmaxFixExecutionReport(
            execId,
            orderId,
            clOrdId,
            LmaxFixMarketDataCodec.GetTag(message, "41"),
            ParseExecType(execTypeRaw, warnings),
            execTypeRaw,
            ParseOrdStatus(ordStatusRaw, warnings),
            ordStatusRaw,
            securityId,
            LmaxFixMarketDataCodec.GetTag(message, "22"),
            symbol,
            ResolveInternalSymbol(securityId, symbol, options),
            ParseSide(sideRaw, warnings),
            sideRaw,
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "38"), "OrderQty(38)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "151"), "LeavesQty(151)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "14"), "CumQty(14)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "32"), "LastQty(32)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "31"), "LastPx(31)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "6"), "AvgPx(6)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "44"), "Price(44)", warnings),
            ParseDecimalWithWarning(LmaxFixMarketDataCodec.GetTag(message, "99"), "StopPx(99)", warnings),
            MapTimeInForce(tifRaw, warnings),
            tifRaw,
            MapOrdType(ordTypeRaw, warnings),
            ordTypeRaw,
            ParseFixTimeWithWarning(LmaxFixMarketDataCodec.GetTag(message, "60"), "TransactTime(60)", warnings),
            LmaxFixMarketDataCodec.GetTag(message, "58"),
            LmaxFixMarketDataCodec.GetTag(message, "1"),
            LmaxFixMarketDataCodec.SanitizeMessage(message),
            DateTimeOffset.UtcNow,
            warnings,
            false,
            missing);

        if (string.IsNullOrWhiteSpace(execId)) missing.Add("Missing ExecID.");
        if (string.IsNullOrWhiteSpace(orderId) && string.IsNullOrWhiteSpace(clOrdId)) missing.Add("Missing both OrderID and ClOrdID.");
        if (string.IsNullOrWhiteSpace(securityId) && string.IsNullOrWhiteSpace(symbol)) missing.Add("Missing both SecurityID and Symbol.");
        if (report.TransactTimeUtc is null) missing.Add("Missing TransactTimeUtc.");

        var canMap = missing.Count == 0 || (!string.IsNullOrWhiteSpace(execId) && (!string.IsNullOrWhiteSpace(orderId) || !string.IsNullOrWhiteSpace(clOrdId)));
        report = report with { CanMapToInternalOrderEvent = canMap, MissingForInternalOrderEvent = missing };
        return new(report, LmaxFixExecutionReportToInternalEventMapper.Map(report), warnings, missing);
    }

    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    private static int? ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    private static DateTimeOffset? ParseFixTime(string? value) => DateTimeOffset.TryParseExact(value, "yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed.ToUniversalTime() : null;
    private static decimal? ParseDecimalWithWarning(string? value, string fieldName, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        warnings.Add($"Could not parse {fieldName} decimal value '{value}'.");
        return null;
    }

    private static DateTimeOffset? ParseFixTimeWithWarning(string? value, string fieldName, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parsed = ParseFixTime(value);
        if (parsed.HasValue) return parsed;
        warnings.Add($"Could not parse {fieldName} UTC timestamp '{value}'.");
        return null;
    }

    private static LmaxFixTradeCaptureSide? ParseSide(string? value, ICollection<string> warnings)
    {
        return value switch
        {
            "1" => LmaxFixTradeCaptureSide.Buy,
            "2" => LmaxFixTradeCaptureSide.Sell,
            null or "" => null,
            _ => AddSideWarning(value, warnings)
        };
    }

    private static LmaxFixTradeCaptureSide? AddSideWarning(string value, ICollection<string> warnings)
    {
        warnings.Add($"Unsupported FIX Side(54) value '{value}'.");
        return null;
    }

    private static LmaxFixExecutionReportType ParseExecType(string? value, ICollection<string> warnings)
    {
        return value switch
        {
            "0" => LmaxFixExecutionReportType.New,
            "4" => LmaxFixExecutionReportType.Canceled,
            "8" => LmaxFixExecutionReportType.Rejected,
            "F" => LmaxFixExecutionReportType.Trade,
            "I" => LmaxFixExecutionReportType.OrderStatus,
            "C" => LmaxFixExecutionReportType.Expired,
            "5" => LmaxFixExecutionReportType.Replaced,
            "6" => LmaxFixExecutionReportType.PendingCancel,
            "A" => LmaxFixExecutionReportType.PendingNew,
            null or "" => LmaxFixExecutionReportType.Unknown,
            _ => AddUnknownExecType(value, warnings)
        };
    }

    private static LmaxFixExecutionReportType AddUnknownExecType(string value, ICollection<string> warnings)
    {
        warnings.Add($"Unsupported ExecType(150) value '{value}'.");
        return LmaxFixExecutionReportType.Unknown;
    }

    private static LmaxFixOrderStatus ParseOrdStatus(string? value, ICollection<string> warnings)
    {
        return value switch
        {
            "0" => LmaxFixOrderStatus.New,
            "1" => LmaxFixOrderStatus.PartiallyFilled,
            "2" => LmaxFixOrderStatus.Filled,
            "4" => LmaxFixOrderStatus.Canceled,
            "8" => LmaxFixOrderStatus.Rejected,
            "C" => LmaxFixOrderStatus.Expired,
            "6" => LmaxFixOrderStatus.PendingCancel,
            "A" => LmaxFixOrderStatus.PendingNew,
            null or "" => LmaxFixOrderStatus.Unknown,
            _ => AddUnknownOrdStatus(value, warnings)
        };
    }

    private static LmaxFixOrderStatus AddUnknownOrdStatus(string value, ICollection<string> warnings)
    {
        warnings.Add($"Unsupported OrdStatus(39) value '{value}'.");
        return LmaxFixOrderStatus.Unknown;
    }

    private static string? MapOrdType(string? value, ICollection<string> warnings)
    {
        return value switch
        {
            "1" => "Market",
            "2" => "Limit",
            "3" => "Stop",
            "4" => "StopLimit",
            null or "" => null,
            _ => AddUnknownNamedValue("OrdType(40)", value, warnings)
        };
    }

    private static string? MapTimeInForce(string? value, ICollection<string> warnings)
    {
        return value switch
        {
            "0" => "Day",
            "1" => "GTC",
            "3" => "IOC",
            "4" => "FOK",
            null or "" => null,
            _ => AddUnknownNamedValue("TimeInForce(59)", value, warnings)
        };
    }

    private static string? AddUnknownNamedValue(string fieldName, string value, ICollection<string> warnings)
    {
        warnings.Add($"Unsupported {fieldName} value '{value}'.");
        return null;
    }

    private static string? ResolveInternalSymbol(string? securityId, string? symbol, LmaxConnectivityLabOptions? options)
    {
        if (options is not null && !string.IsNullOrWhiteSpace(securityId) && securityId == options.LmaxInstrumentId) return options.InstrumentSymbol;
        if (securityId == "4001") return "EURUSD";
        return symbol?.Replace("/", string.Empty, StringComparison.Ordinal);
    }

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

public static class LmaxFixTradeCaptureToEodShapeMapper
{
    public static LmaxFixTradeCaptureEodShape Map(LmaxFixTradeCaptureReport report)
    {
        return new(
            report.ExecId,
            report.SecondaryExecId,
            report.TransactTime,
            report.LastQty,
            report.LastPx,
            report.TradeDate,
            report.InstrumentId,
            report.SecurityId,
            report.InternalSymbol ?? report.Symbol,
            report.ClOrdId,
            report.OrderId,
            report.Account,
            report.NormalizedSide switch
            {
                LmaxFixTradeCaptureSide.Buy when report.LastQty.HasValue => report.LastQty,
                LmaxFixTradeCaptureSide.Sell when report.LastQty.HasValue => -report.LastQty,
                _ => null
            },
            report.LastQty.HasValue && report.LastPx.HasValue ? report.LastQty.Value * report.LastPx.Value : null,
            null);
    }
}

public static class LmaxFixLifecycleEvidenceBuilder
{
    public static LmaxFixLifecycleEvidenceReport Build(
        LmaxFixDemoOrderRequest request,
        LmaxFixDemoOrderLifecycleResult orderSubmission,
        LmaxFixOrderStatusSmokeResult? orderStatus,
        LmaxFixTradeCaptureSmokeResult? tradeCapture)
    {
        var executionReports = orderSubmission.ExecutionReports;
        var fillReports = executionReports.Where(x => x.ExecType == LmaxFixExecutionReportType.Trade).ToList();
        var latestExecution = executionReports.LastOrDefault();
        var lastFill = fillReports.LastOrDefault();
        var latestStatusReport = orderStatus?.ExecutionReports.LastOrDefault();
        var tradeReports = tradeCapture?.Reports ?? [];
        var warnings = new List<string>();
        var checks = new List<LmaxFixLifecycleConsistencyCheck>();

        foreach (var report in tradeReports)
        {
            if (report.MissingForEodComparison?.Any(x => x.Contains("TradeUTI", StringComparison.OrdinalIgnoreCase)) == true)
            {
                warnings.Add($"TradeCapture ExecID={report.ExecId ?? "(missing)"} has no TradeUTI; this is expected for current FIX AE comparison readiness.");
            }
        }

        AddStringCheck(checks, "ClOrdID matches across lifecycle", orderSubmission.ClientOrderId, latestStatusReport?.ClOrdId);
        AddStringCheck(checks, "Broker OrderID matches ExecutionReport and OrderStatus", latestExecution?.OrderId, latestStatusReport?.OrderId);
        AddStringCheck(checks, "SecurityID matches ExecutionReport and OrderStatus", latestExecution?.SecurityId, latestStatusReport?.SecurityId);
        AddSideCheck(checks, "Side matches ExecutionReport and OrderStatus", latestExecution?.Side, latestStatusReport?.Side);
        AddDecimalCheck(checks, "CumQty matches ExecutionReport and OrderStatus", latestExecution?.CumQty, latestStatusReport?.CumQty);
        AddDecimalCheck(checks, "LeavesQty matches ExecutionReport and OrderStatus", latestExecution?.LeavesQty, latestStatusReport?.LeavesQty);
        if (lastFill is not null && latestStatusReport is null)
        {
            checks.Add(new("OrderStatus recovery returned ExecutionReport", LmaxFixLifecycleConsistencyStatus.Failed, "Order filled, but OrderStatusRequest did not return an ExecutionReport."));
        }

        if (lastFill is not null)
        {
            var matchingTradeCapture = tradeReports.FirstOrDefault(x => string.Equals(x.ExecId, lastFill.ExecId, StringComparison.Ordinal));
            var comparableTradeCapture = matchingTradeCapture ?? tradeReports.FirstOrDefault();
            checks.Add(matchingTradeCapture is null
                ? new("Fill ExecID appears in TradeCaptureReport", LmaxFixLifecycleConsistencyStatus.Failed, "Fill ExecutionReport ExecID was not found in TradeCapture AE reports.", lastFill.ExecId, string.Join(",", tradeReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x))))
                : new("Fill ExecID appears in TradeCaptureReport", LmaxFixLifecycleConsistencyStatus.Passed, "Fill ExecutionReport ExecID was recovered via TradeCapture AE.", lastFill.ExecId, matchingTradeCapture.ExecId));

            AddDecimalCheck(checks, "Fill qty matches TradeCapture LastQty", lastFill.LastQty, comparableTradeCapture?.LastQty);
            AddDecimalCheck(checks, "Fill price matches TradeCapture LastPx", lastFill.LastPx, comparableTradeCapture?.LastPx);
        }
        else
        {
            checks.Add(new("Fill ExecID appears in TradeCaptureReport", LmaxFixLifecycleConsistencyStatus.NotApplicable, "No fill ExecutionReport was available to compare."));
            checks.Add(new("Fill qty matches TradeCapture LastQty", LmaxFixLifecycleConsistencyStatus.NotApplicable, "No fill ExecutionReport was available to compare."));
            checks.Add(new("Fill price matches TradeCapture LastPx", LmaxFixLifecycleConsistencyStatus.NotApplicable, "No fill ExecutionReport was available to compare."));
        }

        checks.Add(latestStatusReport?.ExecType == LmaxFixExecutionReportType.OrderStatus
            ? new("ExecType=I OrderStatus report is status-only", LmaxFixLifecycleConsistencyStatus.Passed, "OrderStatusRequest recovery returned ExecType=I and was not counted as a fill.")
            : latestStatusReport is null
                ? new("ExecType=I OrderStatus report is status-only", LmaxFixLifecycleConsistencyStatus.NotApplicable, "No OrderStatus ExecutionReport was available.")
                : new("ExecType=I OrderStatus report is status-only", LmaxFixLifecycleConsistencyStatus.Warning, $"OrderStatus recovery returned ExecType={latestStatusReport.ExecType}; only ExecType=Trade/F is counted as a fill."));

        if (tradeReports.Count > 0)
        {
            var tradeSecurityIds = tradeReports.Select(x => x.SecurityId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (!string.IsNullOrWhiteSpace(latestExecution?.SecurityId) && tradeSecurityIds.Count > 0)
            {
                checks.Add(tradeSecurityIds.Contains(latestExecution.SecurityId, StringComparer.Ordinal)
                    ? new("SecurityID matches TradeCaptureReport", LmaxFixLifecycleConsistencyStatus.Passed, "ExecutionReport SecurityID was present in TradeCapture AE reports.", latestExecution.SecurityId, string.Join(",", tradeSecurityIds))
                    : new("SecurityID matches TradeCaptureReport", LmaxFixLifecycleConsistencyStatus.Failed, "ExecutionReport SecurityID was not present in TradeCapture AE reports.", latestExecution.SecurityId, string.Join(",", tradeSecurityIds)));
            }
        }

        if (tradeReports.Any(x => x.MissingForEodComparison?.Any(y => y.Contains("TradeUTI", StringComparison.OrdinalIgnoreCase)) == true))
        {
            checks.Add(new("TradeCapture missing TradeUTI is warning only", LmaxFixLifecycleConsistencyStatus.Warning, "FIX AE does not currently provide an EOD TradeUTI-equivalent field in parsed reports."));
        }

        var submission = new LmaxFixLifecycleOrderSubmission(
            orderSubmission.OrderSent,
            orderSubmission.ClientOrderId,
            latestExecution?.OrderId,
            executionReports.Count,
            fillReports.Count,
            latestExecution?.OrdStatus.ToString(),
            latestExecution?.ExecType.ToString(),
            latestExecution?.CumQty,
            latestExecution?.LeavesQty,
            latestExecution?.AvgPx,
            lastFill?.ExecId,
            null,
            lastFill?.LastQty,
            lastFill?.LastPx,
            executionReports);

        var statusRecovery = orderStatus is null
            ? null
            : new LmaxFixLifecycleOrderStatusRecovery(
                orderStatus.ExecutionReportReceived,
                latestStatusReport?.OrderId,
                latestStatusReport?.OrdStatus.ToString(),
                latestStatusReport?.CumQty,
                latestStatusReport?.LeavesQty,
                orderStatus.ExecutionReports);

        var captureRecovery = tradeCapture is null
            ? null
            : new LmaxFixLifecycleTradeCaptureRecovery(
                tradeCapture.TradeReportCount > 0,
                tradeCapture.TradeReportCount,
                tradeReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList(),
                tradeReports);

        return new(
            orderSubmission.ClientOrderId,
            latestExecution?.OrderId ?? latestStatusReport?.OrderId,
            request.InstrumentSymbol,
            request.LmaxInstrumentId,
            request.Side,
            request.VenueQuantity,
            request.OrderType,
            request.TimeInForce,
            orderSubmission.OrderSent,
            executionReports.Count,
            fillReports.Count,
            latestExecution?.OrdStatus.ToString(),
            latestExecution?.ExecType.ToString(),
            latestExecution?.CumQty,
            latestExecution?.LeavesQty,
            latestExecution?.AvgPx,
            lastFill?.ExecId,
            null,
            lastFill?.LastQty,
            lastFill?.LastPx,
            orderStatus?.ExecutionReportReceived == true,
            latestStatusReport?.OrdStatus.ToString(),
            latestStatusReport?.CumQty,
            latestStatusReport?.LeavesQty,
            tradeCapture?.TradeReportCount > 0,
            tradeCapture?.TradeReportCount ?? 0,
            captureRecovery?.TradeCaptureExecIds ?? [],
            orderSubmission.StartedAtUtc,
            new[] { orderSubmission.CompletedAtUtc, orderStatus?.CompletedAtUtc, tradeCapture?.CompletedAtUtc }.Where(x => x.HasValue).Max() ?? orderSubmission.CompletedAtUtc,
            warnings.Distinct(StringComparer.Ordinal).ToList(),
            checks,
            submission,
            statusRecovery,
            captureRecovery);
    }

    private static void AddStringCheck(ICollection<LmaxFixLifecycleConsistencyCheck> checks, string name, string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            checks.Add(new(name, LmaxFixLifecycleConsistencyStatus.NotApplicable, "One or both values were missing.", expected, actual));
            return;
        }

        checks.Add(string.Equals(expected, actual, StringComparison.Ordinal)
            ? new(name, LmaxFixLifecycleConsistencyStatus.Passed, "Values matched.", expected, actual)
            : new(name, LmaxFixLifecycleConsistencyStatus.Failed, "Values did not match.", expected, actual));
    }

    private static void AddSideCheck(ICollection<LmaxFixLifecycleConsistencyCheck> checks, string name, LmaxFixTradeCaptureSide? expected, LmaxFixTradeCaptureSide? actual)
        => AddStringCheck(checks, name, expected?.ToString(), actual?.ToString());

    private static void AddDecimalCheck(ICollection<LmaxFixLifecycleConsistencyCheck> checks, string name, decimal? expected, decimal? actual)
    {
        if (!expected.HasValue || !actual.HasValue)
        {
            checks.Add(new(name, LmaxFixLifecycleConsistencyStatus.NotApplicable, "One or both values were missing.", expected?.ToString(CultureInfo.InvariantCulture), actual?.ToString(CultureInfo.InvariantCulture)));
            return;
        }

        checks.Add(expected.Value == actual.Value
            ? new(name, LmaxFixLifecycleConsistencyStatus.Passed, "Values matched.", expected.Value.ToString(CultureInfo.InvariantCulture), actual.Value.ToString(CultureInfo.InvariantCulture))
            : new(name, LmaxFixLifecycleConsistencyStatus.Failed, "Values did not match.", expected.Value.ToString(CultureInfo.InvariantCulture), actual.Value.ToString(CultureInfo.InvariantCulture)));
    }
}

public static class LmaxFixExecutionReportToInternalEventMapper
{
    public static LmaxFixInternalOrderEvent Map(LmaxFixExecutionReport report)
    {
        var eventType = report.ExecType switch
        {
            LmaxFixExecutionReportType.New => LmaxFixInternalOrderEventType.OrderAck,
            LmaxFixExecutionReportType.Rejected => LmaxFixInternalOrderEventType.OrderReject,
            LmaxFixExecutionReportType.Trade when report.LeavesQty.GetValueOrDefault() == 0 => LmaxFixInternalOrderEventType.Fill,
            LmaxFixExecutionReportType.Trade => LmaxFixInternalOrderEventType.PartialFill,
            LmaxFixExecutionReportType.Canceled => LmaxFixInternalOrderEventType.CancelAck,
            LmaxFixExecutionReportType.Expired => LmaxFixInternalOrderEventType.Expired,
            _ => report.OrdStatus switch
            {
                LmaxFixOrderStatus.Filled => LmaxFixInternalOrderEventType.Fill,
                LmaxFixOrderStatus.PartiallyFilled => LmaxFixInternalOrderEventType.PartialFill,
                LmaxFixOrderStatus.Canceled => LmaxFixInternalOrderEventType.CancelAck,
                LmaxFixOrderStatus.Expired => LmaxFixInternalOrderEventType.Expired,
                LmaxFixOrderStatus.Rejected => LmaxFixInternalOrderEventType.OrderReject,
                _ => LmaxFixInternalOrderEventType.Unknown
            }
        };

        return new(
            eventType,
            report.ExecId,
            report.OrderId,
            report.ClOrdId,
            report.InternalSymbol,
            report.Side,
            report.LastQty,
            report.LastPx,
            report.CumQty,
            report.LeavesQty,
            report.TransactTimeUtc,
            report.Text);
    }
}
