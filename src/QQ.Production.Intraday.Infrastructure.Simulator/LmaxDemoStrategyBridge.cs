using System.Globalization;
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
        LmaxDemoOperatorBrokerStateAttestationProvider.Validate(options, now, instrument.Symbol);
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

/// <summary>
/// Demo-only pre-run gate backed by the operator's fresh observation of the official LMAX Demo UI.
/// It deliberately does not query Account REST or manufacture a broker snapshot. FIX lifecycle evidence
/// remains the machine evidence during the run; final broker UI verification and EOD corroboration stay
/// explicit operator protocol boundaries.
/// </summary>
public sealed class LmaxDemoOperatorBrokerStateAttestationProvider(
    IIntradayRepository repository,
    LmaxConnectivityLabOptions options,
    IClock clock) : IBrokerPositionProvider
{
    private bool preTradeAttestationConsumed;

    public async Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        LmaxDemoStrategyVenueExecutionGateway.EnsureDemoOnly(options);
        var state = await repository.LoadStateAsync(cancellationToken);
        var account = state.BrokerAccounts.SingleOrDefault(x => x.Id == brokerAccountId && x.IsEnabled)
            ?? throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_BROKER_ACCOUNT_NOT_ENABLED");
        if (!string.Equals(account.AccountCode, options.AccountCode, StringComparison.Ordinal))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_TARGET_ACCOUNT_MISMATCH");

        if (!preTradeAttestationConsumed)
        {
            Validate(options, clock.UtcNow, null);
            var scope = ParseScope(options.DemoBrokerStateAttestationInstruments);
            if (scope.Any(symbol => !state.Instruments.Any(x => x.IsEnabled && NormalizeSymbol(x.Symbol) == symbol)))
                throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_INSTRUMENT_UNMAPPED");
            preTradeAttestationConsumed = true;
        }

        // The accepted pre-run UI observation establishes zero only for the configured scope.
        // It is intentionally not repurposed as post-run broker authority.
        return [];
    }

    public static void Validate(LmaxConnectivityLabOptions options, DateTimeOffset now, string? requiredInstrument)
    {
        if (string.IsNullOrWhiteSpace(options.DemoBrokerStateAttestationAccountCode))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_ACCOUNT_REQUIRED");
        if (!string.Equals(options.DemoBrokerStateAttestationAccountCode, options.AccountCode, StringComparison.Ordinal))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_ACCOUNT_MISMATCH");
        if (!options.DemoBrokerStateAttestationFlat)
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_FLAT_REQUIRED");
        if (!options.DemoBrokerStateAttestationNoWorkingOrders)
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_NO_WORKING_ORDERS_REQUIRED");
        if (!IsExplicitApproval(options.DemoBrokerStateAttestationApprovalId))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_APPROVAL_REQUIRED");
        if (options.DemoBrokerStateAttestationMaxAgeSeconds is < 1 or > 900)
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_MAX_AGE_INVALID");
        if (!DateTimeOffset.TryParse(options.DemoBrokerStateAttestationObservedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var observedAtUtc))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_OBSERVED_AT_INVALID");
        if (observedAtUtc > now)
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_OBSERVED_AT_FUTURE");
        if (now - observedAtUtc > TimeSpan.FromSeconds(options.DemoBrokerStateAttestationMaxAgeSeconds))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_STALE");

        var scope = ParseScope(options.DemoBrokerStateAttestationInstruments);
        var configuredInstrument = NormalizeSymbol(options.InstrumentSymbol);
        if (!scope.Contains(configuredInstrument))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_CONFIGURED_INSTRUMENT_MISSING");
        if (requiredInstrument is not null && !scope.Contains(NormalizeSymbol(requiredInstrument)))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_TARGET_INSTRUMENT_MISSING");
    }

    private static HashSet<string> ParseScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_INSTRUMENTS_REQUIRED");
        var scope = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSymbol)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (scope.Count == 0)
            throw new InvalidOperationException("DEMO_STRATEGY_ATTESTATION_INSTRUMENTS_REQUIRED");
        return scope;
    }

    private static string NormalizeSymbol(string value)
        => value.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

    private static bool IsExplicitApproval(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !new[] { "NONE", "N/A", "PLACEHOLDER", "TBD", "UNKNOWN" }
                .Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
