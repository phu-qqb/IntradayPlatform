using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoExecutionPersistence(string AccountId, string InternalAccountCode, string SessionId,
    ParentOrderId ParentId, ChildOrderId InitialChildId, IReadOnlyList<ChildOrder> PhysicalChildren,
    IReadOnlyList<ExecutionReport> Reports, OrderStatus ParentStatus)
{
    public IReadOnlyList<Fill> Fills { get; init; } = [];
    public IReadOnlyList<PositionLedgerEvent> Ledger { get; init; } = [];
    public void Validate(PlatformState state)
    {
        var parent = state.ParentOrders.Single(x => x.Id == ParentId);
        var intent = state.TradeIntents.Single(x => x.Id == parent.TradeIntentId);
        if (AccountId != LmaxDemoControlledSession.DemoAccountId || string.IsNullOrWhiteSpace(SessionId)
            || !state.BrokerAccounts.Any(x => x.FundId == intent.FundId && x.IsEnabled && x.AccountCode == InternalAccountCode)
            || PhysicalChildren.Count == 0 || PhysicalChildren.Count(x => x.Id == InitialChildId) != 1
            || PhysicalChildren.Select(x => x.Id).Distinct().Count() != PhysicalChildren.Count
            || PhysicalChildren.Select(x => x.ClientOrderId).Distinct().Count() != PhysicalChildren.Count
            || PhysicalChildren.Any(x => x.ParentOrderId != ParentId || x.Side != parent.Side || !Terminal(x.Status))
            || !Terminal(ParentStatus) || !state.ChildOrders.Any(x => x.Id == InitialChildId && x.ParentOrderId == ParentId))
            throw new InvalidOperationException("DEMO_PERSISTENCE_PARENT_BINDING_INVALID");
        if (Reports.Count == 0 || Reports.Select(x => x.Id).Distinct().Count() != Reports.Count
            || Reports.Any(x => !PhysicalChildren.Any(c => c.Id == x.ChildOrderId && c.VenueId == x.VenueId && c.ClientOrderId == x.ClientOrderId))
            || Fills.Any(x => x.InstrumentId != intent.InstrumentId || !Reports.Any(r => r.BrokerExecutionId == x.BrokerExecutionId
                && r.ChildOrderId == x.ChildOrderId && r.LastQuantity == x.VenueQuantity && r.LastPrice == x.Price))
            || Ledger.Count != Fills.Count || Ledger.Any(x => x.FundId != intent.FundId || x.InstrumentId != intent.InstrumentId
                || x.Type != PositionLedgerEventType.Fill || !Fills.Any(f => f.BrokerExecutionId == x.ReferenceId
                    && x.BaseQuantityDelta == (f.Side == TradeSide.Buy ? f.BaseQuantity : -f.BaseQuantity))))
            throw new InvalidOperationException("DEMO_PERSISTENCE_REPORT_OR_FILL_BINDING_INVALID");
        if (Fills.Sum(x => x.BaseQuantity) > intent.RequestedBaseQuantity)
            throw new InvalidOperationException("DEMO_PERSISTENCE_PARENT_OVERFILL");
    }
    private static bool Terminal(OrderStatus status) => status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired;
}

public interface ILmaxDemoExecutionRepository
{
    Task PersistDemoParentAsync(LmaxDemoExecutionPersistence execution, CancellationToken cancellationToken);
}
