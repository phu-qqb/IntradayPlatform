using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Simulator;

/// <summary>
/// Explicit Demo-only venue bridge. This class has no Production authorization mode.
/// It reuses the qualified ConnectivityLab FIX session and preserves the existing
/// ProcessModelRunService target/risk/persistence path.
/// </summary>
public sealed class LmaxDemoStrategyVenueExecutionGateway(
    IIntradayRepository repository,
    LmaxConnectivityLabOptions options,
    ILmaxDemoStrategySession session,
    IClock clock) : IVenueExecutionGateway
{
    public async Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken cancellationToken)
    {
        EnsureDemoOnly(options);
        var now = clock.UtcNow;
        var state = await repository.LoadStateAsync(cancellationToken);
        var child = state.ChildOrders.SingleOrDefault(x => x.Id == request.ChildOrderId)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_CHILD_NOT_PERSISTED");
        var parent = state.ParentOrders.SingleOrDefault(x => x.Id == child.ParentOrderId)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_PARENT_NOT_PERSISTED");
        var intent = state.TradeIntents.SingleOrDefault(x => x.Id == parent.TradeIntentId)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_INTENT_NOT_PERSISTED");
        var run = state.ModelRuns.SingleOrDefault(x => x.Id == intent.ModelRunId)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_MODELRUN_NOT_FOUND");
        var target = state.TargetPositions.SingleOrDefault(x => x.ModelRunId == run.Id && x.InstrumentId == request.InstrumentId)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_TARGET_NOT_PERSISTED");
        var mapping = state.VenueInstrumentMappings.SingleOrDefault(x => x.VenueId == request.VenueId && x.InstrumentId == request.InstrumentId && x.IsEnabled)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_MAPPING_NOT_VALID");
        var instrument = state.Instruments.SingleOrDefault(x => x.Id == request.InstrumentId && x.IsEnabled && x.IsTradingEnabled)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_INSTRUMENT_NOT_ENABLED");
        var preTrade = state.ReconciliationRuns
            .Where(x => x.ModelRunId == run.Id && x.Phase == ReconciliationPhase.PreTrade)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
        if (preTrade is null || preTrade.HasBlockingBreaks)
            throw new InvalidOperationException("DEMO_STRATEGY_POSITION_NOT_RECONCILED");

        var isQubesPromotion = state.ModelWeightBatches.Any(x =>
            x.PromotedModelRunId == run.Id &&
            x.SourceSystem == ModelWeightSourceSystem.Qubes &&
            x.Status == ModelWeightBatchStatus.Promoted);
        var isLegacyQubesRun = run.SourceFileName.Contains("qubes", StringComparison.OrdinalIgnoreCase)
            || run.SourceFileName.Contains("anubis", StringComparison.OrdinalIgnoreCase);
        if (!isQubesPromotion && !isLegacyQubesRun)
            throw new InvalidOperationException("DEMO_STRATEGY_REAL_TARGET_PROVENANCE_REQUIRED");

        if (run.ReceivedAtUtc.Offset != TimeSpan.Zero || run.EffectiveAtUtc.Offset != TimeSpan.Zero || run.ReceivedAtUtc > run.EffectiveAtUtc)
            throw new InvalidOperationException("DEMO_STRATEGY_TARGET_TIMESTAMP_INVALID");

        var limitSet = state.RiskLimitSets
            .Where(x => x.FundId == run.FundId && x.ModelName == run.ModelName && x.IsActive && x.Status == RiskLimitSetStatus.Active)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault() ?? throw new InvalidOperationException("DEMO_STRATEGY_RISK_LIMIT_SET_MISSING");
        var market = state.MarketData
            .Where(x => x.InstrumentId == request.InstrumentId && x.VenueId == request.VenueId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .FirstOrDefault() ?? throw new InvalidOperationException("DEMO_STRATEGY_MARKET_DATA_MISSING");
        if (market.IsStale(limitSet.MaxMarketDataAge, now))
            throw new InvalidOperationException("DEMO_STRATEGY_MARKET_DATA_STALE");

        if (target.TargetBaseQuantity == 0m || target.TargetVenueQuantity == 0m)
            throw new InvalidOperationException("DEMO_STRATEGY_ZERO_TARGET_NOT_EXECUTABLE");
        if (request.VenueQuantity != intent.RequestedVenueQuantity || request.BaseQuantity != intent.RequestedBaseQuantity)
            throw new InvalidOperationException("DEMO_STRATEGY_TARGET_ECONOMICS_MUTATED");

        // A retry may reach the gateway after an earlier parent was persisted. Never send a
        // second economic parent for the same real ModelRun/instrument.
        var economicParents = state.ParentOrders
            .Join(state.TradeIntents, p => p.TradeIntentId, i => i.Id, (p, i) => new { Parent = p, Intent = i })
            .Where(x => x.Intent.ModelRunId == run.Id && x.Intent.InstrumentId == request.InstrumentId)
            .ToList();
        if (economicParents.Count > 1)
            throw new InvalidOperationException("DEMO_STRATEGY_DUPLICATE_PARENT_RETRY_BLOCKED");

        var securityId = state.InstrumentAliases
            .Where(x => x.InstrumentId == instrument.Id && x.IsEnabled && !string.IsNullOrWhiteSpace(x.ExternalInstrumentId))
            .OrderByDescending(x => x.Source.StartsWith("LMAX", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ExternalInstrumentId!)
            .FirstOrDefault() ?? throw new InvalidOperationException("DEMO_STRATEGY_LMAX_SECURITY_ID_MISSING");

        var requestOptions = CloneOptions(options);
        requestOptions.InstrumentSymbol = instrument.Symbol;
        requestOptions.LmaxInstrumentId = securityId;
        requestOptions.LmaxSlashSymbol = mapping.VenueInstrumentCode;

        var rootClOrdId = $"DS{run.Id.Value:N}"[..18];
        var execution = await session.ExecuteStrategyParentAsync(
            requestOptions,
            new LmaxDemoStrategyExecutionRequest(
                instrument.Symbol,
                securityId,
                mapping.VenueInstrumentCode,
                request.Side == OrderSide.Buy ? LmaxFixDemoOrderSide.Buy : LmaxFixDemoOrderSide.Sell,
                request.VenueQuantity,
                mapping.PriceTickSize,
                rootClOrdId,
                options.AccountCode,
                run.ReceivedAtUtc,
                run.EffectiveAtUtc,
                limitSet.MaxMarketDataAge,
                Math.Max(1, options.RequestTimeoutSeconds),
                options.ShowFixMessages),
            cancellationToken);

        if (!execution.Terminal)
            throw new InvalidOperationException("DEMO_STRATEGY_PARENT_NOT_TERMINAL");

        return new VenueExecutionResult(execution.ExecutionReports.Select(x => ToDomainReport(x, request)).ToList());
    }

    public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("DEMO_STRATEGY_CANCEL_IS_OWNED_BY_BOUNDED_PARENT_LIFECYCLE");

    public Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<VenueOpenOrder>>([]);

    public static void EnsureDemoOnly(LmaxConnectivityLabOptions options)
    {
        if (!options.EnvironmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEMO_STRATEGY_ENVIRONMENT_MUST_BE_DEMO");
        if (options.AllowLiveTrading)
            throw new InvalidOperationException("DEMO_STRATEGY_LIVE_TRADING_FORBIDDEN");
        if (!options.AllowExternalConnections || !options.AllowOrderSubmission || options.DryRun)
            throw new InvalidOperationException("DEMO_STRATEGY_RUNTIME_FLAGS_INVALID");
        if (string.IsNullOrWhiteSpace(options.FixOrderHost) || !options.FixOrderHost.Contains("demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEMO_STRATEGY_ORDER_HOST_NOT_DEMO");
    }

    private static ExecutionReport ToDomainReport(LmaxFixExecutionReport report, VenueOrderRequest request)
    {
        var type = report.ExecType switch
        {
            LmaxFixExecutionReportType.New => ExecutionReportType.OrderAck,
            LmaxFixExecutionReportType.Rejected => ExecutionReportType.OrderReject,
            LmaxFixExecutionReportType.Canceled => ExecutionReportType.CancelAck,
            LmaxFixExecutionReportType.Expired => ExecutionReportType.Expired,
            LmaxFixExecutionReportType.Trade when (report.LeavesQty ?? 0m) > 0m => ExecutionReportType.PartialFill,
            LmaxFixExecutionReportType.Trade => ExecutionReportType.Fill,
            _ => ExecutionReportType.Unknown
        };
        return new ExecutionReport(
            ExecutionReportId.New(), request.ChildOrderId, request.VenueId,
            report.OrderId ?? string.Empty, report.ExecId, request.ClientOrderId, type,
            report.LastQty ?? 0m, report.LastPx ?? 0m, report.LeavesQty ?? 0m,
            report.CumQty ?? 0m, report.AvgPx ?? 0m,
            report.TransactTimeUtc ?? report.ParsedAtUtc);
    }

    private static LmaxConnectivityLabOptions CloneOptions(LmaxConnectivityLabOptions source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<LmaxConnectivityLabOptions>(json)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_OPTIONS_CLONE_FAILED");
    }
}

/// <summary>Read-only LMAX Demo instrument-position provider used by pre/post-trade reconciliation.</summary>
public sealed class LmaxDemoBrokerPositionProvider(
    IIntradayRepository repository,
    LmaxConnectivityLabOptions options,
    HttpMessageHandler? handler = null) : IBrokerPositionProvider
{
    public async Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        LmaxDemoStrategyVenueExecutionGateway.EnsureDemoOnly(options);
        if (string.IsNullOrWhiteSpace(options.AccountApiBaseUrl)
            || !Uri.TryCreate(options.AccountApiBaseUrl, UriKind.Absolute, out var baseUri)
            || !baseUri.Host.Contains("demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_API_NOT_DEMO");
        if (string.IsNullOrWhiteSpace(options.AccountApiBearerToken))
            throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_API_BEARER_TOKEN_REQUIRED");

        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AccountApiRequestTimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/account/positions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccountApiBearerToken);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DEMO_STRATEGY_ACCOUNT_POSITIONS_HTTP_{(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("positions", out var positions) || positions.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_POSITIONS_SHAPE_INVALID");

        var state = await repository.LoadStateAsync(cancellationToken);
        var snapshots = new List<BrokerPositionSnapshot>();
        foreach (var item in positions.EnumerateArray())
        {
            var instrumentId = item.GetProperty("instrument_id").GetString();
            var openQuantityRaw = item.GetProperty("open_quantity").GetString();
            var side = item.GetProperty("side").GetString();
            var timestampRaw = item.GetProperty("timestamp").GetString();
            if (string.IsNullOrWhiteSpace(instrumentId)
                || !decimal.TryParse(openQuantityRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity)
                || !DateTimeOffset.TryParse(timestampRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var asOfUtc))
                throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_POSITION_ROW_INVALID");

            var normalizedSymbol = instrumentId.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var instrument = state.Instruments.SingleOrDefault(x => x.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase));
            if (instrument is null)
                throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_POSITION_INSTRUMENT_UNMAPPED");
            var signed = side switch
            {
                "BID" => Math.Abs(quantity),
                "ASK" => -Math.Abs(quantity),
                "ZERO" => 0m,
                _ => throw new InvalidOperationException("DEMO_STRATEGY_ACCOUNT_POSITION_SIDE_INVALID")
            };
            snapshots.Add(new BrokerPositionSnapshot(brokerAccountId, instrument.Id, signed, asOfUtc));
        }

        return snapshots;
    }
}
