using System.Globalization;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxFixSession
{
    string SessionName { get; }
}

public interface ILmaxFixMarketDataSession : ILmaxFixSession
{
}

public interface ILmaxFixTradingSession : ILmaxFixSession
{
}

public interface ILmaxFixOrderGateway
{
}

public interface ILmaxFixExecutionReportNormalizer
{
    LmaxNormalizedExecutionReport Normalize(IReadOnlyDictionary<string, string> fields, string? rawFixMessageSanitized = null);
}

public interface ILmaxFixTradeCaptureRecoveryService
{
}

public interface ILmaxFixOrderStatusRecoveryService
{
}

public interface ILmaxInstrumentMapper
{
    LmaxInstrumentMapping Map(string? securityId, string? symbol);
}

public interface ILmaxFixSafetyGate
{
    LmaxFixSafetyEvaluation Evaluate(LmaxAdapterSafetyOptions options, LmaxAdapterSafetyIntent intent);
}

public interface ILmaxShadowModeService
{
    IReadOnlyList<LmaxShadowObservation> Compare(
        IReadOnlyList<LmaxNormalizedExecutionReport> executionReports,
        IReadOnlyList<LmaxNormalizedTradeCaptureReport> tradeCaptureReports,
        IReadOnlyList<LmaxShadowInternalFillReference> internalFills,
        IReadOnlyList<LmaxShadowInternalOrderReference> internalOrders);
}

public sealed class LmaxAdapterSafetyOptions
{
    public bool Enabled { get; init; }
    public bool ShadowModeEnabled { get; init; }
    public bool AllowExternalConnections { get; init; }
    public bool AllowOrderSubmission { get; init; }
    public bool AllowLiveTrading { get; init; }
    public string EnvironmentName { get; init; } = "Local";
    public bool RequireGovernanceApproval { get; init; } = true;
    public decimal MaxOrderQuantity { get; init; } = 0.1m;
    public decimal MaxOrderNotionalUsd { get; init; } = 5000m;
    public string? Host { get; init; }
    public bool GovernanceApproved { get; init; }
}

public enum LmaxAdapterSafetyIntent
{
    DesignOnly,
    ShadowMode,
    MarketDataConnection,
    OrderStatusRecovery,
    TradeCaptureRecovery,
    OrderSubmission
}

public sealed record LmaxFixSafetyEvaluation(bool Passed, IReadOnlyList<LmaxFixSafetyDecision> Decisions)
{
    public IReadOnlyList<string> Reasons => Decisions.Where(x => !x.Passed).Select(x => x.Message).ToList();
}

public sealed record LmaxFixSafetyDecision(string Gate, bool Passed, string Message);

public sealed class LmaxFixSafetyGate : ILmaxFixSafetyGate
{
    public LmaxFixSafetyEvaluation Evaluate(LmaxAdapterSafetyOptions options, LmaxAdapterSafetyIntent intent)
    {
        var decisions = new List<LmaxFixSafetyDecision>
        {
            new("Enabled", options.Enabled, options.Enabled ? "Adapter design gate is enabled." : "LMAX adapter is disabled by default.")
        };

        if (intent == LmaxAdapterSafetyIntent.ShadowMode)
        {
            decisions.Add(new("ShadowModeEnabled", options.ShadowModeEnabled, options.ShadowModeEnabled ? "Shadow mode is enabled." : "Shadow mode is disabled by default."));
        }

        decisions.Add(new("AllowLiveTrading", !options.AllowLiveTrading, options.AllowLiveTrading ? "AllowLiveTrading=true is blocked for the adapter design gate." : "Live trading remains disabled."));

        var isDemoOrUat = IsDemoOrUat(options.EnvironmentName);
        decisions.Add(new("Environment", isDemoOrUat, isDemoOrUat ? "Environment is Demo/UAT." : $"Environment '{options.EnvironmentName}' is not allowed for LMAX adapter runtime work."));

        if (RequiresExternalConnection(intent))
        {
            decisions.Add(new("AllowExternalConnections", options.AllowExternalConnections, options.AllowExternalConnections ? "External connections are explicitly allowed for this intent." : "External connections are disabled."));
            decisions.Add(new("Host", IsDemoOrUatHost(options.Host), IsDemoOrUatHost(options.Host) ? "Host is Demo/UAT." : "LMAX adapter connections require an LMAX Demo/UAT host."));
        }

        if (intent == LmaxAdapterSafetyIntent.OrderSubmission)
        {
            decisions.Add(new("AllowOrderSubmission", false, "Order submission from non-lab adapter code is blocked in this design gate."));
            decisions.Add(new("GovernanceApproval", options.RequireGovernanceApproval && options.GovernanceApproved, options.GovernanceApproved ? "Governance approval is present." : "Governance approval is required before any future order submission path."));
        }

        return new LmaxFixSafetyEvaluation(decisions.All(x => x.Passed), decisions);
    }

    private static bool RequiresExternalConnection(LmaxAdapterSafetyIntent intent)
        => intent is LmaxAdapterSafetyIntent.MarketDataConnection or LmaxAdapterSafetyIntent.OrderStatusRecovery or LmaxAdapterSafetyIntent.TradeCaptureRecovery or LmaxAdapterSafetyIntent.OrderSubmission;

    private static bool IsDemoOrUat(string? environmentName)
        => string.Equals(environmentName, "Demo", StringComparison.OrdinalIgnoreCase)
           || string.Equals(environmentName, "UAT", StringComparison.OrdinalIgnoreCase);

    private static bool IsDemoOrUatHost(string? host)
        => !string.IsNullOrWhiteSpace(host)
           && (host.Contains("demo", StringComparison.OrdinalIgnoreCase) || host.Contains("uat", StringComparison.OrdinalIgnoreCase));
}

public sealed record LmaxInstrumentMapping(string? SecurityId, string? ExternalSymbol, string? InternalSymbol, string? InstrumentId, IReadOnlyList<string> Warnings);

public sealed class LmaxInstrumentMapper : ILmaxInstrumentMapper
{
    public LmaxInstrumentMapping Map(string? securityId, string? symbol)
    {
        if (string.Equals(securityId, "4001", StringComparison.Ordinal))
        {
            return new LmaxInstrumentMapping(securityId, symbol ?? "EURUSD", "EURUSD", "EURUSD", []);
        }

        if (string.Equals(symbol, "EURUSD", StringComparison.OrdinalIgnoreCase) || string.Equals(symbol, "EUR/USD", StringComparison.OrdinalIgnoreCase))
        {
            return new LmaxInstrumentMapping(securityId, symbol, "EURUSD", "EURUSD", []);
        }

        return new LmaxInstrumentMapping(securityId, symbol, symbol, null, ["No LMAX instrument mapping is configured for this security."]);
    }
}

public sealed record LmaxNormalizedMarketDataSnapshot(
    string? SecurityId,
    string? SecurityIdSource,
    string? Symbol,
    string? InternalSymbol,
    decimal? Bid,
    decimal? Ask,
    decimal? Mid,
    DateTimeOffset? SourceTimestampUtc,
    string Source,
    IReadOnlyList<string> Warnings);

public sealed record LmaxNormalizedExecutionReport(
    string? ExecId,
    string? BrokerOrderId,
    string? ClientOrderId,
    string? OrigClientOrderId,
    LmaxNormalizedExecutionType ExecType,
    string? RawExecType,
    LmaxNormalizedOrderStatusValue OrderStatus,
    string? RawOrderStatus,
    string? SecurityId,
    string? SecurityIdSource,
    string? Symbol,
    string? InternalSymbol,
    LmaxNormalizedSide? Side,
    decimal? OrderQty,
    decimal? LeavesQty,
    decimal? CumQty,
    decimal? LastQty,
    decimal? LastPx,
    decimal? AvgPx,
    decimal? Price,
    DateTimeOffset? TransactTimeUtc,
    string? Account,
    string? Text,
    bool IsFillCandidate,
    string? RawFixMessageSanitized,
    DateTimeOffset ParsedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record LmaxNormalizedOrderStatus(
    string? ClientOrderId,
    string? BrokerOrderId,
    LmaxNormalizedOrderStatusValue Status,
    decimal? CumQty,
    decimal? LeavesQty,
    DateTimeOffset? TransactTimeUtc,
    IReadOnlyList<string> Warnings);

public sealed record LmaxNormalizedTradeCaptureReport(
    string? TradeRequestId,
    string? ExecId,
    string? SecondaryExecId,
    string? BrokerOrderId,
    string? ClientOrderId,
    string? SecurityId,
    string? SecurityIdSource,
    string? Symbol,
    string? InternalSymbol,
    LmaxNormalizedSide? Side,
    decimal? LastQty,
    decimal? LastPx,
    DateOnly? TradeDate,
    DateTimeOffset? TransactTimeUtc,
    string? Account,
    string? TradeUti,
    bool LastReportRequested,
    bool IsRecoveryFillCandidate,
    IReadOnlyList<string> MissingForEodComparison,
    string? RawFixMessageSanitized,
    DateTimeOffset ParsedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record LmaxNormalizedOrderLifecycleEvidence(
    string? ClientOrderId,
    string? BrokerOrderId,
    string? SecurityId,
    string? Symbol,
    decimal? RequestedQuantity,
    LmaxNormalizedOrderStatusValue FinalOrderStatus,
    LmaxNormalizedExecutionType FinalExecType,
    decimal? CumQty,
    decimal? LeavesQty,
    decimal? AvgPx,
    string? LastFillExecId,
    bool OrderStatusReceived,
    bool TradeCaptureReceived,
    IReadOnlyList<LmaxLifecycleConsistencyCheck> ConsistencyChecks,
    IReadOnlyList<string> Warnings);

public sealed record LmaxNormalizedFixReject(
    string? RefSeqNum,
    string? RefTagId,
    string? RefMsgType,
    string? SessionRejectReason,
    string? Text,
    string? RawFixMessageSanitized);

public sealed record LmaxLifecycleConsistencyCheck(string Name, LmaxLifecycleConsistencyStatus Status, string Message);

public enum LmaxLifecycleConsistencyStatus
{
    Passed,
    Warning,
    Failed,
    NotApplicable
}

public enum LmaxNormalizedExecutionType
{
    Unknown,
    New,
    Canceled,
    Rejected,
    Trade,
    OrderStatus,
    Expired,
    Replaced,
    PendingCancel,
    PendingNew
}

public enum LmaxNormalizedOrderStatusValue
{
    Unknown,
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Rejected,
    Expired,
    PendingCancel,
    PendingNew
}

public enum LmaxNormalizedSide
{
    Buy,
    Sell
}

public sealed class LmaxFixExecutionReportNormalizer(ILmaxInstrumentMapper? mapper = null) : ILmaxFixExecutionReportNormalizer
{
    private readonly ILmaxInstrumentMapper _mapper = mapper ?? new LmaxInstrumentMapper();

    public LmaxNormalizedExecutionReport Normalize(IReadOnlyDictionary<string, string> fields, string? rawFixMessageSanitized = null)
    {
        var warnings = new List<string>();
        var securityId = Get(fields, "48");
        var symbol = Get(fields, "55");
        var mapping = _mapper.Map(securityId, symbol);
        warnings.AddRange(mapping.Warnings);

        var rawExecType = Get(fields, "150");
        var execType = MapExecType(rawExecType, warnings);
        var rawOrdStatus = Get(fields, "39");
        var orderStatus = MapOrderStatus(rawOrdStatus, warnings);

        var report = new LmaxNormalizedExecutionReport(
            Get(fields, "17"),
            Get(fields, "37"),
            Get(fields, "11"),
            Get(fields, "41"),
            execType,
            rawExecType,
            orderStatus,
            rawOrdStatus,
            securityId,
            Get(fields, "22"),
            symbol,
            mapping.InternalSymbol,
            MapSide(Get(fields, "54"), warnings),
            ParseDecimal(Get(fields, "38"), "OrderQty", warnings),
            ParseDecimal(Get(fields, "151"), "LeavesQty", warnings),
            ParseDecimal(Get(fields, "14"), "CumQty", warnings),
            ParseDecimal(Get(fields, "32"), "LastQty", warnings),
            ParseDecimal(Get(fields, "31"), "LastPx", warnings),
            ParseDecimal(Get(fields, "6"), "AvgPx", warnings),
            ParseDecimal(Get(fields, "44"), "Price", warnings),
            ParseFixTimestamp(Get(fields, "60"), "TransactTime", warnings),
            Get(fields, "1"),
            Get(fields, "58"),
            execType == LmaxNormalizedExecutionType.Trade,
            LmaxDiagnosticSanitizer.Sanitize(rawFixMessageSanitized),
            DateTimeOffset.UtcNow,
            warnings);

        return report;
    }

    private static LmaxNormalizedExecutionType MapExecType(string? value, ICollection<string> warnings)
        => value switch
        {
            "0" => LmaxNormalizedExecutionType.New,
            "4" => LmaxNormalizedExecutionType.Canceled,
            "5" => LmaxNormalizedExecutionType.Replaced,
            "6" => LmaxNormalizedExecutionType.PendingCancel,
            "8" => LmaxNormalizedExecutionType.Rejected,
            "A" => LmaxNormalizedExecutionType.PendingNew,
            "C" => LmaxNormalizedExecutionType.Expired,
            "F" => LmaxNormalizedExecutionType.Trade,
            "I" => LmaxNormalizedExecutionType.OrderStatus,
            null or "" => WarnUnknown(warnings, "ExecType", value),
            _ => WarnUnknown(warnings, "ExecType", value)
        };

    private static LmaxNormalizedOrderStatusValue MapOrderStatus(string? value, ICollection<string> warnings)
        => value switch
        {
            "0" => LmaxNormalizedOrderStatusValue.New,
            "1" => LmaxNormalizedOrderStatusValue.PartiallyFilled,
            "2" => LmaxNormalizedOrderStatusValue.Filled,
            "4" => LmaxNormalizedOrderStatusValue.Canceled,
            "6" => LmaxNormalizedOrderStatusValue.PendingCancel,
            "8" => LmaxNormalizedOrderStatusValue.Rejected,
            "A" => LmaxNormalizedOrderStatusValue.PendingNew,
            "C" => LmaxNormalizedOrderStatusValue.Expired,
            null or "" => WarnUnknownStatus(warnings, "OrdStatus", value),
            _ => WarnUnknownStatus(warnings, "OrdStatus", value)
        };

    private static LmaxNormalizedExecutionType WarnUnknown(ICollection<string> warnings, string name, string? value)
    {
        warnings.Add($"Unknown {name} '{value ?? "<missing>"}'.");
        return LmaxNormalizedExecutionType.Unknown;
    }

    private static LmaxNormalizedOrderStatusValue WarnUnknownStatus(ICollection<string> warnings, string name, string? value)
    {
        warnings.Add($"Unknown {name} '{value ?? "<missing>"}'.");
        return LmaxNormalizedOrderStatusValue.Unknown;
    }

    internal static LmaxNormalizedSide? MapSide(string? value, ICollection<string> warnings)
        => value switch
        {
            "1" => LmaxNormalizedSide.Buy,
            "2" => LmaxNormalizedSide.Sell,
            null or "" => null,
            _ => WarnSide(warnings, value)
        };

    private static LmaxNormalizedSide? WarnSide(ICollection<string> warnings, string value)
    {
        warnings.Add($"Unknown Side '{value}'.");
        return null;
    }

    internal static decimal? ParseDecimal(string? value, string fieldName, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        warnings.Add($"Could not parse {fieldName} as decimal.");
        return null;
    }

    internal static DateTimeOffset? ParseFixTimestamp(string? value, string fieldName, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyyMMdd-HH:mm:ss.fff", "yyyyMMdd-HH:mm:ss" };
        if (DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        warnings.Add($"Could not parse {fieldName} as UTC FIX timestamp.");
        return null;
    }

    internal static DateOnly? ParseTradeDate(string? value, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        warnings.Add("Could not parse TradeDate.");
        return null;
    }

    internal static string? Get(IReadOnlyDictionary<string, string> fields, string tag)
        => fields.TryGetValue(tag, out var value) ? value : null;
}

public sealed class LmaxFixTradeCaptureNormalizer(ILmaxInstrumentMapper? mapper = null)
{
    private readonly ILmaxInstrumentMapper _mapper = mapper ?? new LmaxInstrumentMapper();

    public LmaxNormalizedTradeCaptureReport Normalize(IReadOnlyDictionary<string, string> fields, string? rawFixMessageSanitized = null)
    {
        var warnings = new List<string>();
        var missing = new List<string>();
        var securityId = LmaxFixExecutionReportNormalizer.Get(fields, "48");
        var symbol = LmaxFixExecutionReportNormalizer.Get(fields, "55");
        var mapping = _mapper.Map(securityId, symbol);
        warnings.AddRange(mapping.Warnings);

        var tradeUti = LmaxFixExecutionReportNormalizer.Get(fields, "1003");
        if (string.IsNullOrWhiteSpace(tradeUti))
        {
            missing.Add("TradeUTI");
            warnings.Add("FIX TradeCaptureReport does not include TradeUTI; EOD individual-trades.csv remains the daily source for TradeUTI.");
        }

        return new LmaxNormalizedTradeCaptureReport(
            LmaxFixExecutionReportNormalizer.Get(fields, "568"),
            LmaxFixExecutionReportNormalizer.Get(fields, "17"),
            LmaxFixExecutionReportNormalizer.Get(fields, "527"),
            LmaxFixExecutionReportNormalizer.Get(fields, "37"),
            LmaxFixExecutionReportNormalizer.Get(fields, "11"),
            securityId,
            LmaxFixExecutionReportNormalizer.Get(fields, "22"),
            symbol,
            mapping.InternalSymbol,
            LmaxFixExecutionReportNormalizer.MapSide(LmaxFixExecutionReportNormalizer.Get(fields, "54"), warnings),
            LmaxFixExecutionReportNormalizer.ParseDecimal(LmaxFixExecutionReportNormalizer.Get(fields, "32"), "LastQty", warnings),
            LmaxFixExecutionReportNormalizer.ParseDecimal(LmaxFixExecutionReportNormalizer.Get(fields, "31"), "LastPx", warnings),
            LmaxFixExecutionReportNormalizer.ParseTradeDate(LmaxFixExecutionReportNormalizer.Get(fields, "75"), warnings),
            LmaxFixExecutionReportNormalizer.ParseFixTimestamp(LmaxFixExecutionReportNormalizer.Get(fields, "60"), "TransactTime", warnings),
            LmaxFixExecutionReportNormalizer.Get(fields, "1"),
            tradeUti,
            string.Equals(LmaxFixExecutionReportNormalizer.Get(fields, "912"), "Y", StringComparison.OrdinalIgnoreCase),
            !string.IsNullOrWhiteSpace(LmaxFixExecutionReportNormalizer.Get(fields, "17")),
            missing,
            LmaxDiagnosticSanitizer.Sanitize(rawFixMessageSanitized),
            DateTimeOffset.UtcNow,
            warnings);
    }
}

public sealed record LmaxShadowInternalFillReference(string InternalFillId, string? BrokerExecutionId, string? ClientOrderId, string? BrokerOrderId, decimal? Quantity, decimal? Price);

public sealed record LmaxShadowInternalOrderReference(string InternalOrderId, string? ClientOrderId, string? BrokerOrderId, LmaxNormalizedOrderStatusValue Status, decimal? CumQty, decimal? LeavesQty);

public sealed record LmaxShadowObservation(
    LmaxShadowObservationType Type,
    LmaxShadowObservationSeverity Severity,
    string Message,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId,
    string? InternalEntityId,
    DateTimeOffset ObservedAtUtc,
    string? MetadataJson);

public enum LmaxShadowObservationSeverity
{
    Info,
    Warning,
    Critical
}

public enum LmaxShadowObservationType
{
    ExecutionReportMatchesInternalFill,
    ExecutionReportMissingInternalFill,
    TradeCaptureMatchesInternalFill,
    TradeCaptureMissingInternalFill,
    OrderStatusMatchesInternalOrder,
    OrderStatusMismatch,
    MarketDataSnapshotReceived,
    UnknownLmaxExecution
}

public sealed class LmaxShadowModeService : ILmaxShadowModeService
{
    public IReadOnlyList<LmaxShadowObservation> Compare(
        IReadOnlyList<LmaxNormalizedExecutionReport> executionReports,
        IReadOnlyList<LmaxNormalizedTradeCaptureReport> tradeCaptureReports,
        IReadOnlyList<LmaxShadowInternalFillReference> internalFills,
        IReadOnlyList<LmaxShadowInternalOrderReference> internalOrders)
    {
        var observations = new List<LmaxShadowObservation>();
        var now = DateTimeOffset.UtcNow;

        foreach (var report in executionReports.Where(x => x.IsFillCandidate))
        {
            var match = internalFills.FirstOrDefault(x => string.Equals(x.BrokerExecutionId, report.ExecId, StringComparison.Ordinal));
            observations.Add(match is null
                ? new LmaxShadowObservation(LmaxShadowObservationType.ExecutionReportMissingInternalFill, LmaxShadowObservationSeverity.Warning, "LMAX ExecutionReport fill has no matching internal fill. Shadow mode did not mutate internal state.", report.ClientOrderId, report.BrokerOrderId, report.ExecId, null, now, SafeJson(new { report.LastQty, report.LastPx }))
                : new LmaxShadowObservation(LmaxShadowObservationType.ExecutionReportMatchesInternalFill, LmaxShadowObservationSeverity.Info, "LMAX ExecutionReport fill matches an internal fill reference.", report.ClientOrderId, report.BrokerOrderId, report.ExecId, match.InternalFillId, now, SafeJson(new { report.LastQty, report.LastPx })));
        }

        foreach (var report in tradeCaptureReports.Where(x => x.IsRecoveryFillCandidate))
        {
            var match = internalFills.FirstOrDefault(x => string.Equals(x.BrokerExecutionId, report.ExecId, StringComparison.Ordinal));
            observations.Add(match is null
                ? new LmaxShadowObservation(LmaxShadowObservationType.TradeCaptureMissingInternalFill, LmaxShadowObservationSeverity.Warning, "LMAX TradeCaptureReport fill has no matching internal fill. Shadow mode did not mutate internal state.", report.ClientOrderId, report.BrokerOrderId, report.ExecId, null, now, SafeJson(new { report.LastQty, report.LastPx, report.MissingForEodComparison }))
                : new LmaxShadowObservation(LmaxShadowObservationType.TradeCaptureMatchesInternalFill, LmaxShadowObservationSeverity.Info, "LMAX TradeCaptureReport fill matches an internal fill reference.", report.ClientOrderId, report.BrokerOrderId, report.ExecId, match.InternalFillId, now, SafeJson(new { report.LastQty, report.LastPx, report.MissingForEodComparison })));
        }

        foreach (var status in executionReports.Where(x => x.ExecType == LmaxNormalizedExecutionType.OrderStatus))
        {
            var match = internalOrders.FirstOrDefault(x => string.Equals(x.ClientOrderId, status.ClientOrderId, StringComparison.Ordinal) || string.Equals(x.BrokerOrderId, status.BrokerOrderId, StringComparison.Ordinal));
            var matchesStatus = match is not null && match.Status == status.OrderStatus;
            observations.Add(matchesStatus
                ? new LmaxShadowObservation(LmaxShadowObservationType.OrderStatusMatchesInternalOrder, LmaxShadowObservationSeverity.Info, "LMAX order status matches an internal order reference.", status.ClientOrderId, status.BrokerOrderId, status.ExecId, match!.InternalOrderId, now, SafeJson(new { status.CumQty, status.LeavesQty }))
                : new LmaxShadowObservation(LmaxShadowObservationType.OrderStatusMismatch, LmaxShadowObservationSeverity.Warning, "LMAX order status does not match an internal order reference. Shadow mode did not mutate internal state.", status.ClientOrderId, status.BrokerOrderId, status.ExecId, match?.InternalOrderId, now, SafeJson(new { status.OrderStatus, status.CumQty, status.LeavesQty })));
        }

        return observations;
    }

    private static string SafeJson(object value)
        => LmaxDiagnosticSanitizer.Sanitize(JsonSerializer.Serialize(value)) ?? "{}";
}

public static class LmaxDiagnosticSanitizer
{
    private static readonly string[] SensitiveTokens = ["password", "secret", "token", "apikey", "api-key", "authorization", "username", "sendercompid"];

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sanitized = value;
        foreach (var token in SensitiveTokens)
        {
            sanitized = MaskTokenValue(sanitized, token);
        }

        return MaskSensitiveFieldValues(sanitized);
    }

    private static string MaskTokenValue(string value, string token)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        var index = value.IndexOf(token, comparison);
        while (index >= 0)
        {
            var equalsIndex = value.IndexOf('=', index);
            if (equalsIndex < 0)
            {
                break;
            }

            var end = value.IndexOf('\u0001', equalsIndex);
            if (end < 0)
            {
                end = value.IndexOf('|', equalsIndex);
            }

            end = end < 0 ? value.Length : end;
            value = string.Concat(value.AsSpan(0, equalsIndex + 1), "********", value.AsSpan(end));
            index = value.IndexOf(token, equalsIndex + 9, comparison);
        }

        return value;
    }

    private static string MaskSensitiveFieldValues(string value)
    {
        var separator = value.Contains('\u0001', StringComparison.Ordinal) ? '\u0001' : '|';
        var parts = value.Split(separator);
        for (var i = 0; i < parts.Length; i++)
        {
            var equalsIndex = parts[i].IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var fieldValue = parts[i][(equalsIndex + 1)..];
            if (SensitiveTokens.Any(token => fieldValue.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                parts[i] = string.Concat(parts[i].AsSpan(0, equalsIndex + 1), "********");
            }
        }

        return string.Join(separator, parts);
    }
}
