using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.Simulator;

public enum FakeLmaxBehavior
{
    FullFill,
    PartialFill,
    Reject,
    NoFill
}

public sealed class FakeLmaxOptions
{
    public FakeLmaxBehavior Behavior { get; set; } = FakeLmaxBehavior.FullFill;
    public decimal PartialFillRatio { get; set; } = 0.5m;
    public decimal FillPrice { get; set; } = 1.1m;
    public bool GenerateBrokerExecutionId { get; set; } = true;
}

public sealed class FakeLmaxGateway(FakeLmaxOptions options, IClock clock) : IVenueExecutionGateway
{
    private int _orderSequence;
    private int _executionSequence;

    public Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var brokerOrderId = $"FAKE-LMAX-O-{Interlocked.Increment(ref _orderSequence):000000}";
        var reports = new List<ExecutionReport>();
        if (options.Behavior != FakeLmaxBehavior.Reject)
        {
            reports.Add(new ExecutionReport(ExecutionReportId.New(), request.ChildOrderId, request.VenueId, brokerOrderId, null, request.ClientOrderId, ExecutionReportType.OrderAck, 0m, 0m, request.VenueQuantity, 0m, 0m, now));
        }

        switch (options.Behavior)
        {
            case FakeLmaxBehavior.FullFill:
                reports.Add(CreateFill(request, brokerOrderId, request.VenueQuantity, 0m, ExecutionReportType.Fill, now));
                break;
            case FakeLmaxBehavior.PartialFill:
                var partial = QuantityRounding.RoundToStep(request.VenueQuantity * options.PartialFillRatio, 0.1m);
                partial = Math.Min(partial, request.VenueQuantity);
                reports.Add(CreateFill(request, brokerOrderId, partial, request.VenueQuantity - partial, ExecutionReportType.PartialFill, now));
                reports.Add(new ExecutionReport(ExecutionReportId.New(), request.ChildOrderId, request.VenueId, brokerOrderId, null, request.ClientOrderId, ExecutionReportType.Expired, 0m, 0m, request.VenueQuantity - partial, partial, options.FillPrice, now));
                break;
            case FakeLmaxBehavior.Reject:
                reports.Add(new ExecutionReport(ExecutionReportId.New(), request.ChildOrderId, request.VenueId, brokerOrderId, null, request.ClientOrderId, ExecutionReportType.OrderReject, 0m, 0m, request.VenueQuantity, 0m, 0m, now));
                break;
            case FakeLmaxBehavior.NoFill:
                reports.Add(new ExecutionReport(ExecutionReportId.New(), request.ChildOrderId, request.VenueId, brokerOrderId, null, request.ClientOrderId, ExecutionReportType.Expired, 0m, 0m, request.VenueQuantity, 0m, 0m, now));
                break;
        }

        return Task.FromResult(new VenueExecutionResult(reports));
    }

    public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken cancellationToken)
    {
        var report = new ExecutionReport(ExecutionReportId.New(), request.ChildOrderId, request.VenueId, "FAKE-LMAX-CANCEL", null, request.ClientOrderId, ExecutionReportType.CancelAck, 0m, 0m, 0m, 0m, 0m, clock.UtcNow);
        return Task.FromResult(new VenueExecutionResult([report]));
    }

    public Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<VenueOpenOrder>>([]);

    private ExecutionReport CreateFill(VenueOrderRequest request, string brokerOrderId, decimal lastVenueQuantity, decimal leavesQuantity, ExecutionReportType type, DateTimeOffset now)
    {
        var executionId = options.GenerateBrokerExecutionId
            ? $"FAKE-LMAX-E-{Interlocked.Increment(ref _executionSequence):000000}"
            : string.Empty;

        return new ExecutionReport(
            ExecutionReportId.New(),
            request.ChildOrderId,
            request.VenueId,
            brokerOrderId,
            executionId,
            request.ClientOrderId,
            type,
            lastVenueQuantity,
            options.FillPrice,
            leavesQuantity,
            lastVenueQuantity,
            options.FillPrice,
            now);
    }
}

public sealed class FakeBrokerPositionProvider(PlatformState state, IClock? clock = null) : IBrokerPositionProvider
{
    public bool ForceMismatch { get; set; }
    public bool ForcePostTradeMismatch { get; set; }
    private int _calls;

    public Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        var account = state.BrokerAccounts.Single(x => x.Id == brokerAccountId);
        var shouldMismatch = ForceMismatch || (ForcePostTradeMismatch && Interlocked.Increment(ref _calls) > 1);
        var positions = state.PositionLedger
            .Where(x => x.FundId == account.FundId)
            .GroupBy(x => x.InstrumentId)
            .Select(x => new BrokerPositionSnapshot(brokerAccountId, x.Key, x.Sum(y => y.BaseQuantityDelta) + (shouldMismatch ? 10_000m : 0m), (clock ?? new SystemClock()).UtcNow))
            .ToList();

        return Task.FromResult<IReadOnlyList<BrokerPositionSnapshot>>(positions);
    }
}
