using System.Globalization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed class LmaxFixAdapterOptions
{
    public bool Enabled { get; init; }
    public bool ShadowModeEnabled { get; init; }
    public bool AllowExternalConnections { get; init; }
    public bool AllowOrderSubmission { get; init; }
    public bool AllowLiveTrading { get; init; }
    public string EnvironmentName { get; init; } = "Local";
    public string VenueName { get; init; } = "LMAX";
    public string? OrderHost { get; init; }
    public int OrderPort { get; init; } = 443;
    public string? MarketDataHost { get; init; }
    public int MarketDataPort { get; init; } = 443;
    public string? SenderCompId { get; init; }
    public string? TargetCompId { get; init; }
    public string? MarketDataTargetCompId { get; init; }
    public decimal MaxOrderQuantity { get; init; } = 0.1m;
    public decimal MaxOrderNotionalUsd { get; init; } = 5000m;
    public bool RequireGovernanceApproval { get; init; } = true;
    public bool DryRun { get; init; } = true;
    public bool GovernanceApproved { get; init; }
    public bool RiskApproved { get; init; }
}

public sealed record LmaxFixAdapterSafetyResult(bool Passed, IReadOnlyList<LmaxFixAdapterSafetyDecision> Decisions)
{
    public IReadOnlyList<string> BlockingMessages => Decisions.Where(x => !x.Passed).Select(x => x.Message).ToList();
}

public sealed record LmaxFixAdapterSafetyDecision(string Gate, bool Passed, string Message);

public enum LmaxFixAdapterRuntimeIntent
{
    ShadowOnly,
    MarketData,
    OrderStatusRecovery,
    TradeCaptureRecovery,
    OrderSubmission
}

public sealed class LmaxFixAdapterRuntimeSafetyValidator
{
    public LmaxFixAdapterSafetyResult Validate(LmaxFixAdapterOptions options, LmaxFixAdapterRuntimeIntent intent)
    {
        var decisions = new List<LmaxFixAdapterSafetyDecision>();
        var isShadowOnly = intent == LmaxFixAdapterRuntimeIntent.ShadowOnly;

        decisions.Add(new("Enabled", !options.Enabled || isShadowOnly && options.ShadowModeEnabled, options.Enabled ? "Adapter is enabled only for explicit shadow-only evaluation." : "Adapter is disabled by default."));
        decisions.Add(new("AllowLiveTrading", !options.AllowLiveTrading, options.AllowLiveTrading ? "AllowLiveTrading=true is always rejected for the adapter skeleton." : "Live trading remains disabled."));
        decisions.Add(new("Environment", IsDemoOrUat(options.EnvironmentName), $"Environment '{options.EnvironmentName}' must be Demo or UAT for adapter skeleton work."));
        decisions.Add(new("Production", !string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase), "Production environment is rejected by the adapter skeleton."));

        if (intent is LmaxFixAdapterRuntimeIntent.MarketData or LmaxFixAdapterRuntimeIntent.OrderStatusRecovery or LmaxFixAdapterRuntimeIntent.TradeCaptureRecovery or LmaxFixAdapterRuntimeIntent.OrderSubmission)
        {
            decisions.Add(new("AllowExternalConnections", options.AllowExternalConnections, options.AllowExternalConnections ? "External connections are explicitly allowed." : "External connections are disabled."));
            decisions.Add(new("OrderHost", IsDemoHost(options.OrderHost), "Order host must be an LMAX Demo/UAT host."));
        }

        if (intent == LmaxFixAdapterRuntimeIntent.MarketData)
        {
            decisions.Add(new("MarketDataHost", IsDemoHost(options.MarketDataHost), "Market data host must be an LMAX Demo/UAT host."));
        }

        if (intent == LmaxFixAdapterRuntimeIntent.OrderSubmission)
        {
            decisions.Add(new("RuntimeOrderSubmission", false, "Runtime order submission through the LMAX adapter skeleton is blocked."));
            decisions.Add(new("AllowOrderSubmission", !options.AllowOrderSubmission, options.AllowOrderSubmission ? "AllowOrderSubmission=true is rejected outside the isolated Connectivity Lab." : "Runtime adapter order submission is not allowed."));
            decisions.Add(new("DryRun", options.DryRun, "DryRun must remain true for the adapter skeleton."));
            decisions.Add(new("GovernanceApproval", !options.RequireGovernanceApproval || options.GovernanceApproved, "Governance approval is required before any future order submission path."));
            decisions.Add(new("RiskApproval", options.RiskApproved, "Risk approval is required before any future order submission path."));
        }

        return new LmaxFixAdapterSafetyResult(decisions.All(x => x.Passed), decisions);
    }

    private static bool IsDemoOrUat(string? environment)
        => string.Equals(environment, "Demo", StringComparison.OrdinalIgnoreCase)
           || string.Equals(environment, "UAT", StringComparison.OrdinalIgnoreCase);

    private static bool IsDemoHost(string? host)
        => !string.IsNullOrWhiteSpace(host)
           && host.Contains("lmax.com", StringComparison.OrdinalIgnoreCase)
           && (host.Contains("demo", StringComparison.OrdinalIgnoreCase) || host.Contains("uat", StringComparison.OrdinalIgnoreCase));
}

public sealed record LmaxFixBuiltMessage(string MessageType, string SanitizedMessage, IReadOnlyList<string> Warnings)
{
    public IReadOnlyDictionary<string, string> Tags => LmaxFixTagParser.ParseFirstValues(SanitizedMessage);
}

public sealed record LmaxFixNewOrderSingleRequest(
    string ClOrdId,
    string SecurityId,
    string SecurityIdSource,
    string? Symbol,
    VenueAdapterContractSide Side,
    decimal OrderQty,
    string OrderType,
    string TimeInForce,
    decimal? LimitPrice,
    string? Account,
    DateTimeOffset TransactTimeUtc);

public sealed record LmaxFixTradeCaptureRequest(
    string TradeRequestId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Account);

public sealed record LmaxFixOrderStatusRequest(
    string ClOrdId,
    string? SecurityId,
    string? SecurityIdSource,
    VenueAdapterContractSide? Side,
    string? Account,
    string? OrdStatusReqId);

public sealed record LmaxFixMarketDataRequest(
    string MarketDataRequestId,
    string SecurityId,
    string SecurityIdSource,
    string? Symbol,
    int MarketDepth,
    bool SnapshotOnly);

public sealed class LmaxFixOrderMessageBuilder
{
    public const char Soh = '\u0001';

    public LmaxFixBuiltMessage BuildNewOrderSingle(LmaxFixNewOrderSingleRequest request)
    {
        var warnings = new List<string>();
        var fields = new List<(string Tag, string Value)>
        {
            ("35", "D"),
            ("11", request.ClOrdId),
            ("48", request.SecurityId),
            ("22", string.IsNullOrWhiteSpace(request.SecurityIdSource) ? "8" : request.SecurityIdSource),
            ("54", request.Side == VenueAdapterContractSide.Sell ? "2" : "1"),
            ("60", FixTime(request.TransactTimeUtc)),
            ("38", DecimalString(request.OrderQty)),
            ("40", string.Equals(request.OrderType, "Limit", StringComparison.OrdinalIgnoreCase) ? "2" : "1"),
            ("59", string.Equals(request.TimeInForce, "FOK", StringComparison.OrdinalIgnoreCase) ? "4" : "3")
        };

        if (!string.IsNullOrWhiteSpace(request.Symbol)) fields.Add(("55", request.Symbol!));
        if (string.Equals(request.OrderType, "Limit", StringComparison.OrdinalIgnoreCase))
        {
            if (request.LimitPrice is null)
            {
                warnings.Add("Limit order is missing price.");
            }
            else
            {
                fields.Add(("44", DecimalString(request.LimitPrice.Value)));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Account)) fields.Add(("1", request.Account!));
        return new LmaxFixBuiltMessage("D", Sanitize(Build(fields)), warnings);
    }

    public LmaxFixBuiltMessage BuildTradeCaptureReportRequest(LmaxFixTradeCaptureRequest request)
    {
        var warnings = new List<string>();
        if (request.TradeRequestId.Length > 16)
        {
            warnings.Add("TradeRequestID tag 568 must be <= 16 characters.");
        }

        var fields = new List<(string Tag, string Value)>
        {
            ("35", "AD"),
            ("568", request.TradeRequestId),
            ("569", "1"),
            ("263", "0"),
            ("580", "2"),
            ("60", FixTime(request.StartUtc)),
            ("60", FixTime(request.EndUtc))
        };
        if (!string.IsNullOrWhiteSpace(request.Account)) fields.Add(("1", request.Account!));
        return new LmaxFixBuiltMessage("AD", Sanitize(Build(fields)), warnings);
    }

    public LmaxFixBuiltMessage BuildOrderStatusRequest(LmaxFixOrderStatusRequest request)
    {
        var fields = new List<(string Tag, string Value)> { ("35", "H"), ("11", request.ClOrdId) };
        if (!string.IsNullOrWhiteSpace(request.SecurityId))
        {
            fields.Add(("48", request.SecurityId!));
            fields.Add(("22", string.IsNullOrWhiteSpace(request.SecurityIdSource) ? "8" : request.SecurityIdSource!));
        }

        if (request.Side is not null) fields.Add(("54", request.Side == VenueAdapterContractSide.Sell ? "2" : "1"));
        if (!string.IsNullOrWhiteSpace(request.Account)) fields.Add(("1", request.Account!));
        if (!string.IsNullOrWhiteSpace(request.OrdStatusReqId)) fields.Add(("790", request.OrdStatusReqId!));
        return new LmaxFixBuiltMessage("H", Sanitize(Build(fields)), []);
    }

    public LmaxFixBuiltMessage BuildMarketDataRequest(LmaxFixMarketDataRequest request)
    {
        var fields = new List<(string Tag, string Value)>
        {
            ("35", "V"),
            ("262", request.MarketDataRequestId),
            ("263", request.SnapshotOnly ? "0" : "1"),
            ("264", request.MarketDepth.ToString(CultureInfo.InvariantCulture)),
            ("267", "2"),
            ("269", "0"),
            ("269", "1"),
            ("146", "1"),
            ("48", request.SecurityId),
            ("22", string.IsNullOrWhiteSpace(request.SecurityIdSource) ? "8" : request.SecurityIdSource)
        };
        if (!string.IsNullOrWhiteSpace(request.Symbol)) fields.Add(("55", request.Symbol!));
        return new LmaxFixBuiltMessage("V", Sanitize(Build(fields)), []);
    }

    public static string GenerateTradeRequestId(DateTimeOffset nowUtc, int sequence)
        => $"TC{nowUtc:yyMMddHHmmss}{Math.Abs(sequence) % 100:00}";

    private static string Build(IEnumerable<(string Tag, string Value)> fields)
        => string.Join(Soh, fields.Select(x => $"{x.Tag}={x.Value}")) + Soh;

    private static string FixTime(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string DecimalString(decimal value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Sanitize(string message)
        => LmaxDiagnosticSanitizer.Sanitize(message) ?? string.Empty;
}

public static class LmaxFixTagParser
{
    public static IReadOnlyDictionary<string, string> ParseFirstValues(string message)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in message.Split([LmaxFixOrderMessageBuilder.Soh, '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            if (index <= 0) continue;
            fields.TryAdd(part[..index], part[(index + 1)..]);
        }

        return fields;
    }

    public static IReadOnlyList<string> GetValues(string message, string tag)
        => message.Split([LmaxFixOrderMessageBuilder.Soh, '|'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith(tag + "=", StringComparison.Ordinal))
            .Select(x => x[(tag.Length + 1)..])
            .ToList();
}

public sealed class LmaxFixExecutionEventMapper
{
    public VenueExecutionEvent Map(LmaxNormalizedExecutionReport report)
        => LmaxVenueExecutionEventMapper.FromExecutionReport(report);
}

public sealed class LmaxFixTradeCaptureMapper
{
    public VenueExecutionEvent Map(LmaxNormalizedTradeCaptureReport report)
        => LmaxVenueExecutionEventMapper.FromTradeCaptureReport(report);
}

public sealed class LmaxFixOrderStatusMapper
{
    public VenueExecutionEvent Map(LmaxNormalizedExecutionReport report)
        => LmaxVenueExecutionEventMapper.FromExecutionReport(report);
}

public sealed class LmaxFixRejectMapper
{
    public VenueExecutionEvent Map(LmaxNormalizedFixReject reject)
        => LmaxVenueExecutionEventMapper.FromFixReject(reject);
}

public sealed record LmaxFixMarketDataEntry(string EntryType, decimal? Price, decimal? Size, DateTimeOffset? SourceTimestampUtc);

public sealed class LmaxFixMarketDataMapper
{
    public LmaxNormalizedMarketDataSnapshot Map(string? securityId, string? symbol, IReadOnlyList<LmaxFixMarketDataEntry> entries, DateTimeOffset? sourceTimestampUtc = null)
    {
        var mapper = new LmaxInstrumentMapper();
        var mapping = mapper.Map(securityId, symbol);
        var bid = entries.Where(x => x.EntryType == "0" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Max();
        var ask = entries.Where(x => x.EntryType == "1" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Min();
        decimal? bidValue = bid == 0m ? null : bid;
        decimal? askValue = ask == 0m ? null : ask;
        decimal? mid = bidValue.HasValue && askValue.HasValue ? (bidValue.Value + askValue.Value) / 2m : null;

        return new LmaxNormalizedMarketDataSnapshot(
            securityId,
            "8",
            symbol,
            mapping.InternalSymbol,
            bidValue,
            askValue,
            mid,
            sourceTimestampUtc ?? entries.Select(x => x.SourceTimestampUtc).FirstOrDefault(x => x.HasValue),
            "LMAX_FIX_MARKET_DATA",
            mapping.Warnings);
    }
}

public sealed record LmaxVenueGatewaySkeletonResult(bool Submitted, bool Blocked, string Message, IReadOnlyList<string> SafetyMessages);

public sealed class LmaxVenueGatewaySkeleton(LmaxFixAdapterRuntimeSafetyValidator? safetyValidator = null, LmaxFixAdapterOptions? options = null)
{
    private readonly LmaxFixAdapterRuntimeSafetyValidator _safetyValidator = safetyValidator ?? new LmaxFixAdapterRuntimeSafetyValidator();
    private readonly LmaxFixAdapterOptions _options = options ?? new LmaxFixAdapterOptions();

    public Task<LmaxVenueGatewaySkeletonResult> SubmitOrderAsync(LmaxFixNewOrderSingleRequest request, CancellationToken cancellationToken = default)
    {
        var safety = _safetyValidator.Validate(_options, LmaxFixAdapterRuntimeIntent.OrderSubmission);
        return Task.FromResult(new LmaxVenueGatewaySkeletonResult(
            Submitted: false,
            Blocked: true,
            "LMAX adapter skeleton is disabled and not registered for runtime execution.",
            safety.BlockingMessages));
    }
}
