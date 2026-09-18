using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Simulator;

/// <summary>Only the explicit Demo coordinator may submit a prepared model run.
/// Repository access is sequential; FIX parent lifecycles share one account owner.</summary>
public sealed class LmaxDemoContinuingGateway(
    IIntradayRepository repository, LmaxConnectivityLabOptions options,
    LmaxDemoContinuingSession session, IClock clock) : ILmaxDemoBatchExecutionGateway
{
    public async Task<IReadOnlyList<InstrumentId>> GetExecutionScopeAsync(CancellationToken token)
    {
        var state = await repository.LoadStateAsync(token);
        var venue = state.Venues.Single(x => x.Name == "LMAX" && x.IsEnabled);
        var scope = new List<InstrumentId>();
        foreach (var binding in session.StartingObservation.Instruments)
        {
            var instrument = state.Instruments.SingleOrDefault(x => x.IsEnabled && x.IsTradingEnabled && x.Symbol == binding.Symbol)
                ?? throw new InvalidOperationException("DEMO_CONTINUING_EXECUTION_INSTRUMENT_UNMAPPED");
            if (!state.VenueInstrumentMappings.Any(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id
                    && x.IsEnabled && x.ContractSize == binding.ContractSize)
                || !state.InstrumentAliases.Any(x => x.InstrumentId == instrument.Id && x.IsEnabled && x.ExternalInstrumentId == binding.SecurityId))
                throw new InvalidOperationException("DEMO_CONTINUING_EXECUTION_BINDING_MISMATCH");
            scope.Add(instrument.Id);
        }
        if (scope.Count == 0 || scope.Distinct().Count() != scope.Count)
            throw new InvalidOperationException("DEMO_CONTINUING_EXECUTION_SCOPE_INVALID");
        return scope;
    }

    public async Task<IReadOnlyList<VenueExecutionResult>> SendModelRunAsync(ModelRun run,
        IReadOnlyList<TargetPosition> targets, IReadOnlyList<VenueOrderRequest> orders, CancellationToken token)
    {
        var state = await repository.LoadStateAsync(token);
        var executionScope = (await GetExecutionScopeAsync(token)).ToHashSet();
        if (targets.Any(x => !executionScope.Contains(x.InstrumentId))
            || orders.Any(x => !executionScope.Contains(x.InstrumentId)))
            throw new InvalidOperationException("DEMO_CONTINUING_ORDER_OUTSIDE_OBSERVED_SCOPE");
        var targetMap = targets.ToDictionary(
            x => state.Instruments.Single(i => i.Id == x.InstrumentId).Symbol, x => x.TargetBaseQuantity, StringComparer.Ordinal);
        if (run.AsOfUtc >= LmaxDemoDaySchedule.FinalClose(run.AsOfUtc).AddMinutes(-15)
            && (!LmaxDemoDaySchedule.IsFinalExit(run.AsOfUtc) || targetMap.Values.Any(x => x != 0m)))
            throw new InvalidOperationException("DEMO_CONTINUING_NEW_RISK_AFTER_EXIT_CUTOFF");
        // Missing positions must never disappear from a portfolio target.
        if (session.Positions().Any(x => x.Value != 0m && !targetMap.ContainsKey(x.Key)))
            throw new InvalidOperationException("DEMO_CONTINUING_TARGET_OMITS_EXISTING_POSITION");
        var bridge = new LmaxDemoStrategyVenueExecutionGateway(repository, options, session, clock);
        var prepared = new List<LmaxDemoStrategyVenueExecutionGateway.PreparedParent>();
        foreach (var order in orders) prepared.Add(await bridge.PrepareAsync(order, token));
        session.BeginCycle(run.Id.Value.ToString("N"), targetMap);
        return await Task.WhenAll(prepared.Select(x => bridge.ExecutePreparedAsync(x, token)));
    }

    public Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken token)
        => throw new InvalidOperationException("DEMO_CONTINUING_EXPLICIT_MODELRUN_BATCH_REQUIRED");
    public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken token)
        => throw new InvalidOperationException("DEMO_CONTINUING_CANCEL_OWNED_BY_PARENT_LIFECYCLE");

    public async Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken token)
    {
        var state = await repository.LoadStateAsync(token);
        if (!state.Venues.Any(x => x.Id == venueId && x.Name == "LMAX"))
            throw new InvalidOperationException("DEMO_CONTINUING_VENUE_MISMATCH");
        return session.Orders().Where(x => !x.Terminal).Select(x => new VenueOpenOrder(
            new ChildOrderId(Guid.ParseExact(x.Intent.ParentId, "N")), venueId,
            x.BrokerOrderId ?? throw new InvalidOperationException("DEMO_CONTINUING_ORDER_UNACKNOWLEDGED"), x.LeavesQuantity)).ToArray();
    }
}

public sealed class LmaxDemoContinuingBrokerPositionProvider(
    IIntradayRepository repository, LmaxDemoContinuingSession session, IClock clock) : IBrokerPositionProvider
{
    public async Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId accountId, CancellationToken token)
    {
        var state = await repository.LoadStateAsync(token);
        var account = state.BrokerAccounts.SingleOrDefault(x => x.Id == accountId && x.IsEnabled
            && x.AccountCode == (session.StartingObservation.InternalBrokerAccountCode ?? session.StartingObservation.AccountId))
            ?? throw new InvalidOperationException("DEMO_CONTINUING_BROKER_ACCOUNT_MISMATCH");
        var positions = session.Positions();
        var venueId = state.Venues.Single(x => x.Name == "LMAX" && x.IsEnabled).Id;
        foreach (var binding in session.StartingObservation.Instruments)
        {
            var instrument = state.Instruments.SingleOrDefault(x => x.IsEnabled && x.Symbol == binding.Symbol)
                ?? throw new InvalidOperationException("DEMO_CONTINUING_INSTRUMENT_UNMAPPED");
            if (!state.VenueInstrumentMappings.Any(x => x.InstrumentId == instrument.Id && x.VenueId == venueId
                && x.IsEnabled && x.ContractSize == binding.ContractSize)
                || !state.InstrumentAliases.Any(x => x.InstrumentId == instrument.Id && x.IsEnabled && x.ExternalInstrumentId == binding.SecurityId))
                throw new InvalidOperationException("DEMO_CONTINUING_OBSERVED_INSTRUMENT_BINDING_MISMATCH");
        }
        return positions.Where(x => x.Value != 0m).Select(x => new BrokerPositionSnapshot(accountId,
            state.Instruments.Single(i => i.IsEnabled && i.Symbol == x.Key).Id, x.Value, clock.UtcNow)).ToArray();
    }
}
