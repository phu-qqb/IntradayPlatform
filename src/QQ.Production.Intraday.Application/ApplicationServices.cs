using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record VenueOrderRequest(ChildOrderId ChildOrderId, VenueId VenueId, InstrumentId InstrumentId, ClientOrderId ClientOrderId, OrderSide Side, OrderType OrderType, TimeInForce TimeInForce, decimal BaseQuantity, decimal VenueQuantity);
public sealed record VenueCancelRequest(ChildOrderId ChildOrderId, VenueId VenueId, ClientOrderId ClientOrderId);
public sealed record VenueOpenOrder(ChildOrderId ChildOrderId, VenueId VenueId, string BrokerOrderId, decimal LeavesQuantity);
public sealed record VenueExecutionResult(IReadOnlyList<ExecutionReport> Reports);

public interface IVenueExecutionGateway
{
    Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken cancellationToken);
    Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken cancellationToken);
}

public interface IBrokerPositionProvider
{
    Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

public interface IIntradayRepository
{
    Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken);
    Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken);
    Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken);
    Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken);
    Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken);
    Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken);
    Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken);
    Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken);
    Task AddRiskDecisionAsync(RiskDecision decision, CancellationToken cancellationToken);
    Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken);
    Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken);
    Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken);
    Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken);
    Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken);
}

public interface IMarketDataSnapshotRepository
{
    Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken);
    Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}

public interface IMarketDataBarRepository
{
    Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken);
    Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}

public interface IBarBuildRunRepository
{
    Task AddAsync(BarBuildRun run, CancellationToken cancellationToken);
    Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken);
    Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken);
}

public interface IMarketDataProvider
{
    Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(Instrument instrument, Venue venue, DateTimeOffset startUtc, TimeSpan interval, int count, decimal bid, decimal ask, decimal bidStep, decimal askStep, CancellationToken cancellationToken);
}

public interface IBarBuilderService
{
    Task<BarBuildResult> BuildBarsAsync(VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
    Task<BarBuildResult> BuildLatestFifteenMinuteBarsAsync(VenueId venueId, CancellationToken cancellationToken);
}

public sealed record BarUpsertResult(bool Created);
public sealed record BarBuildResult(BarBuildRunId RunId, int BarsCreated, int BarsUpdated, BarBuildRunStatus Status, string? ErrorMessage = null);

public sealed class BarBuilderOptions
{
    public int FifteenMinuteMinimumObservationCount { get; set; } = 3;
    public bool CreateNoDataBars { get; set; }
    public string Source { get; set; } = "LocalSnapshotStore";
    public string BuilderVersion { get; set; } = "bar-builder-v1";
}

public sealed class PlatformState
{
    public List<Fund> Funds { get; } = [];
    public List<BrokerAccount> BrokerAccounts { get; } = [];
    public List<Instrument> Instruments { get; } = [];
    public List<Venue> Venues { get; } = [];
    public List<VenueInstrumentMapping> VenueInstrumentMappings { get; } = [];
    public List<NavSnapshot> NavSnapshots { get; } = [];
    public List<ModelRun> ModelRuns { get; } = [];
    public List<TargetWeight> TargetWeights { get; } = [];
    public List<MarketDataSnapshot> MarketData { get; } = [];
    public List<TargetPosition> TargetPositions { get; } = [];
    public List<DriftSnapshot> DriftSnapshots { get; } = [];
    public List<MarketDataBar> MarketDataBars { get; } = [];
    public List<BarBuildRun> BarBuildRuns { get; } = [];
    public List<PositionLedgerEvent> PositionLedger { get; } = [];
    public List<ReconciliationRun> ReconciliationRuns { get; } = [];
    public List<ReconciliationBreak> ReconciliationBreaks { get; } = [];
    public List<TradeIntent> TradeIntents { get; } = [];
    public List<RiskDecision> RiskDecisions { get; } = [];
    public List<ParentOrder> ParentOrders { get; } = [];
    public List<ChildOrder> ChildOrders { get; } = [];
    public List<ExecutionReport> ExecutionReports { get; } = [];
    public List<Fill> Fills { get; } = [];
    public List<RiskLimitSet> RiskLimitSets { get; } = [];
    public List<InstrumentRiskLimit> InstrumentRiskLimits { get; } = [];
    public List<VenueRiskLimit> VenueRiskLimits { get; } = [];
    public List<TradingWindow> TradingWindows { get; } = [];
    public KillSwitchState KillSwitch { get; set; } = new(Guid.NewGuid(), false, null, DateTimeOffset.UnixEpoch);
}

public sealed class InMemoryIntradayRepository(PlatformState state) : IIntradayRepository
{
    private readonly object _sync = new();

    public Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken) => Task.FromResult(state);

    public Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelRuns.OrderBy(x => x.ReceivedAtUtc).FirstOrDefault(x => !x.IsProcessed));
        }
    }

    public Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelRuns.FirstOrDefault(x => x.Id == modelRunId));
        }
    }

    public Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ModelRuns.Any(x => x.Id == modelRun.Id))
            {
                return Task.CompletedTask;
            }

            state.ModelRuns.Add(modelRun);
            state.TargetWeights.AddRange(weights);
        }

        return Task.CompletedTask;
    }

    public Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelRuns.FindIndex(x => x.Id == modelRunId);
            if (index >= 0)
            {
                var run = state.ModelRuns[index];
                state.ModelRuns[index] = run with { IsProcessed = true, Status = status };
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ReconciliationRuns.Any(x => x.ModelRunId == run.ModelRunId && x.Phase == run.Phase))
            {
                return Task.CompletedTask;
            }

            state.ReconciliationRuns.Add(run);
            state.ReconciliationBreaks.AddRange(breaks);
        }

        return Task.CompletedTask;
    }

    public Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!state.TargetPositions.Any(x => x.ModelRunId == targetPosition.ModelRunId && x.InstrumentId == targetPosition.InstrumentId))
            {
                state.TargetPositions.Add(targetPosition);
            }

            if (!state.DriftSnapshots.Any(x => x.ModelRunId == driftSnapshot.ModelRunId && x.InstrumentId == driftSnapshot.InstrumentId))
            {
                state.DriftSnapshots.Add(driftSnapshot);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!state.TradeIntents.Any(x => x.Id == intent.Id))
            {
                state.TradeIntents.Add(intent);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddRiskDecisionAsync(RiskDecision decision, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.RiskDecisions.Any(x => x.TradeIntentId == decision.TradeIntentId))
            {
                return Task.CompletedTask;
            }

            state.RiskDecisions.Add(decision);
        }

        return Task.CompletedTask;
    }

    public Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ParentOrders.Any(x => x.ClientOrderId == parentOrder.ClientOrderId) || state.ChildOrders.Any(x => x.ClientOrderId == childOrder.ClientOrderId))
            {
                return Task.CompletedTask;
            }

            state.ParentOrders.Add(parentOrder);
            state.ChildOrders.Add(childOrder);
        }

        return Task.CompletedTask;
    }

    public Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ExecutionReports.Any(x => x.Id == report.Id))
            {
                return Task.CompletedTask;
            }

            state.ExecutionReports.Add(report);
            var childIndex = state.ChildOrders.FindIndex(x => x.Id == report.ChildOrderId);
            if (childIndex >= 0)
            {
                var machine = new OrderStateMachine();
                var child = state.ChildOrders[childIndex];
                var childStatus = machine.Transition(child.Status, report.ExecutionReportType);
                state.ChildOrders[childIndex] = child with { Status = childStatus };

                var parentIndex = state.ParentOrders.FindIndex(x => x.Id == child.ParentOrderId);
                if (parentIndex >= 0)
                {
                    var parent = state.ParentOrders[parentIndex];
                    var parentStatus = report.ExecutionReportType switch
                    {
                        ExecutionReportType.OrderReject => OrderStatus.Rejected,
                        ExecutionReportType.Fill => OrderStatus.Filled,
                        ExecutionReportType.PartialFill => OrderStatus.PartiallyFilled,
                        ExecutionReportType.Expired when parent.Status == OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                        ExecutionReportType.Expired => OrderStatus.Expired,
                        _ => parent.Status
                    };
                    state.ParentOrders[parentIndex] = parent with { Status = parentStatus };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.Fills.Any(x => x.VenueId == fill.VenueId && x.BrokerExecutionId == fill.BrokerExecutionId))
            {
                return Task.FromResult(false);
            }

            state.Fills.Add(fill);
            return Task.FromResult(true);
        }
    }

    public Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.PositionLedger.Add(ledgerEvent);
        }

        return Task.CompletedTask;
    }

    public Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.KillSwitch = new KillSwitchState(state.KillSwitch.Id, isActive, reason, DateTimeOffset.UtcNow);
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryMarketDataSnapshotRepository(PlatformState state) : IMarketDataSnapshotRepository
{
    private readonly object _sync = new();

    public Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken)
        => AddRangeAsync([snapshot], cancellationToken);

    public Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Validate();
                if (!state.MarketData.Any(x => x.Id == snapshot.Id))
                {
                    state.MarketData.Add(snapshot);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.MarketData.Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId).OrderBy(x => x.SourceTimestampUtc).ThenBy(x => x.ReceivedAtUtc).LastOrDefault());
        }
    }

    public Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<MarketDataSnapshot>>(state.MarketData
                .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.SourceTimestampUtc >= startUtc && x.SourceTimestampUtc < endUtc)
                .OrderBy(x => x.SourceTimestampUtc)
                .ToList());
        }
    }
}

public sealed class InMemoryMarketDataBarRepository(PlatformState state) : IMarketDataBarRepository
{
    private readonly object _sync = new();

    public Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken)
    {
        bar.Validate();
        lock (_sync)
        {
            var index = state.MarketDataBars.FindIndex(x => x.InstrumentId == bar.InstrumentId && x.VenueId == bar.VenueId && x.Timeframe == bar.Timeframe && x.BarStartUtc == bar.BarStartUtc);
            if (index >= 0)
            {
                state.MarketDataBars[index] = bar with { Id = state.MarketDataBars[index].Id };
                return Task.FromResult(new BarUpsertResult(false));
            }

            state.MarketDataBars.Add(bar);
            return Task.FromResult(new BarUpsertResult(true));
        }
    }

    public Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.MarketDataBars.FirstOrDefault(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc == barStartUtc));
        }
    }

    public Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<MarketDataBar>>(state.MarketDataBars
                .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc >= startUtc && x.BarStartUtc < endUtc)
                .OrderBy(x => x.BarStartUtc)
                .ToList());
        }
    }
}

public sealed class InMemoryBarBuildRunRepository(PlatformState state, IClock clock) : IBarBuildRunRepository
{
    private readonly object _sync = new();

    public Task AddAsync(BarBuildRun run, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.BarBuildRuns.Add(run);
        }

        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.BarBuildRuns.FindIndex(x => x.Id == runId);
            if (index >= 0)
            {
                var run = state.BarBuildRuns[index];
                state.BarBuildRuns[index] = run with { Status = BarBuildRunStatus.Completed, CompletedAtUtc = clock.UtcNow, BarsCreated = barsCreated, BarsUpdated = barsUpdated };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.BarBuildRuns.FindIndex(x => x.Id == runId);
            if (index >= 0)
            {
                var run = state.BarBuildRuns[index];
                state.BarBuildRuns[index] = run with { Status = BarBuildRunStatus.Failed, CompletedAtUtc = clock.UtcNow, ErrorMessage = errorMessage };
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class BarBuilderService(
    PlatformState state,
    IMarketDataSnapshotRepository snapshotRepository,
    IMarketDataBarRepository barRepository,
    IBarBuildRunRepository buildRunRepository,
    IClock clock,
    BarBuilderOptions options) : IBarBuilderService
{
    public async Task<BarBuildResult> BuildBarsAsync(VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        var run = new BarBuildRun(BarBuildRunId.New(), timeframe, clock.UtcNow, null, options.Source, options.BuilderVersion, BarBuildRunStatus.Started, null, 0, 0);
        await buildRunRepository.AddAsync(run, cancellationToken);
        var created = 0;
        var updated = 0;

        try
        {
            if (timeframe != BarTimeframe.FifteenMinutes)
            {
                throw new NotSupportedException("Only 15-minute bar building is implemented.");
            }

            foreach (var instrument in state.Instruments.Where(x => x.IsEnabled))
            {
                foreach (var (barStart, barEnd) in BarIntervalAlignment.EnumerateIntervals(startUtc, endUtc, timeframe))
                {
                    var snapshots = await snapshotRepository.GetRangeAsync(instrument.Id, venueId, barStart, barEnd, cancellationToken);
                    if (snapshots.Count == 0 && !options.CreateNoDataBars)
                    {
                        continue;
                    }

                    var bar = BuildBar(instrument.Id, venueId, timeframe, barStart, barEnd, snapshots, run.Id);
                    var result = await barRepository.UpsertAsync(bar, cancellationToken);
                    if (result.Created) created++;
                    else updated++;
                }
            }

            await buildRunRepository.MarkCompletedAsync(run.Id, created, updated, cancellationToken);
            return new BarBuildResult(run.Id, created, updated, BarBuildRunStatus.Completed);
        }
        catch (Exception ex)
        {
            await buildRunRepository.MarkFailedAsync(run.Id, ex.Message, cancellationToken);
            return new BarBuildResult(run.Id, created, updated, BarBuildRunStatus.Failed, ex.Message);
        }
    }

    public Task<BarBuildResult> BuildLatestFifteenMinuteBarsAsync(VenueId venueId, CancellationToken cancellationToken)
    {
        var end = BarIntervalAlignment.GetBarStart(clock.UtcNow, BarTimeframe.FifteenMinutes);
        return BuildBarsAsync(venueId, BarTimeframe.FifteenMinutes, end.AddMinutes(-15), end, cancellationToken);
    }

    private MarketDataBar BuildBar(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStart, DateTimeOffset barEnd, IReadOnlyList<MarketDataSnapshot> snapshots, BarBuildRunId runId)
    {
        var ordered = snapshots.OrderBy(x => x.SourceTimestampUtc).ToList();
        var isComplete = barEnd <= clock.UtcNow;
        var quality = ordered.Count switch
        {
            0 => isComplete ? BarQualityStatus.NoData : BarQualityStatus.Incomplete,
            _ when !isComplete => BarQualityStatus.Incomplete,
            _ when ordered.Count < options.FifteenMinuteMinimumObservationCount => BarQualityStatus.SparseData,
            _ => BarQualityStatus.Complete
        };

        decimal First(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : selector(ordered[0]);
        decimal Last(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : selector(ordered[^1]);
        decimal Max(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Max(selector);
        decimal Min(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Min(selector);
        decimal Avg(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Sum(selector) / ordered.Count;

        return new MarketDataBar(
            MarketDataBarId.New(),
            instrumentId,
            venueId,
            timeframe,
            barStart,
            barEnd,
            options.Source,
            First(x => x.Bid), Max(x => x.Bid), Min(x => x.Bid), Last(x => x.Bid),
            First(x => x.Ask), Max(x => x.Ask), Min(x => x.Ask), Last(x => x.Ask),
            First(x => x.Mid), Max(x => x.Mid), Min(x => x.Mid), Last(x => x.Mid),
            First(x => x.Spread), Max(x => x.Spread), Min(x => x.Spread), Last(x => x.Spread), Avg(x => x.Spread),
            ordered.Count,
            ordered.FirstOrDefault()?.SourceTimestampUtc,
            ordered.LastOrDefault()?.SourceTimestampUtc,
            isComplete,
            quality,
            runId,
            options.BuilderVersion,
            clock.UtcNow);
    }
}

public sealed record RiskContext(Fund Fund, Venue Venue, Instrument Instrument, ModelRun ModelRun, MarketDataSnapshot MarketData, decimal CurrentBaseQuantity, bool PositionsMatch, decimal ExistingGrossExposureUsd, DateTimeOffset Now);

public sealed class RiskEngine
{
    public RiskDecision Evaluate(TradeIntent intent, RiskContext context, RiskLimitSet limitSet, InstrumentRiskLimit instrumentLimit, VenueRiskLimit venueLimit, TradingWindow tradingWindow, KillSwitchState killSwitch)
    {
        var reject = RiskRejectReason.None;
        var status = RiskDecisionStatus.Approved;
        var notional = intent.RequestedBaseQuantity * context.MarketData.Mid;

        if (!limitSet.GlobalTradingEnabled) reject = RiskRejectReason.GlobalTradingDisabled;
        else if (killSwitch.IsActive) reject = RiskRejectReason.KillSwitchActive;
        else if (!context.Fund.IsEnabled) reject = RiskRejectReason.FundDisabled;
        else if (!context.Venue.IsEnabled) reject = RiskRejectReason.VenueDisabled;
        else if (!context.Instrument.IsEnabled) reject = RiskRejectReason.InstrumentDisabled;
        else if (!context.PositionsMatch) reject = RiskRejectReason.PositionMismatch;
        else if (context.Now - context.ModelRun.AsOfUtc > limitSet.MaxModelRunAge) reject = RiskRejectReason.StaleModelRun;
        else if (context.MarketData.IsStale(limitSet.MaxMarketDataAge, context.Now)) reject = RiskRejectReason.StaleMarketData;
        else if (intent.RequestedBaseQuantity <= 0 || intent.RequestedVenueQuantity <= 0) reject = RiskRejectReason.InvalidQuantity;
        else if (notional > instrumentLimit.MaxTradeNotionalUsd || notional > venueLimit.MaxTradeNotionalUsd) reject = RiskRejectReason.MaxTradeNotionalExceeded;
        else if (Math.Abs(context.CurrentBaseQuantity * context.MarketData.Mid) + notional > instrumentLimit.MaxExposureUsd) reject = RiskRejectReason.MaxInstrumentExposureExceeded;
        else if (context.ExistingGrossExposureUsd + notional > limitSet.MaxGrossExposureUsd) reject = RiskRejectReason.MaxGrossExposureExceeded;
        else if (!IsTradingWindowOpen(tradingWindow, context.Now)) reject = RiskRejectReason.TradingWindowClosed;

        if (reject != RiskRejectReason.None)
        {
            status = reject is RiskRejectReason.PositionMismatch or RiskRejectReason.KillSwitchActive ? RiskDecisionStatus.Blocked : RiskDecisionStatus.Rejected;
        }

        return new RiskDecision(Guid.NewGuid(), intent.Id, status, reject, reject.ToString(), context.Now);
    }

    private static bool IsTradingWindowOpen(TradingWindow window, DateTimeOffset now)
    {
        if (!window.IsEnabled || !window.TradingEnabled || now.DayOfWeek != window.DayOfWeek)
        {
            return false;
        }

        var time = TimeOnly.FromTimeSpan(now.UtcDateTime.TimeOfDay);
        return time >= window.OpensAtUtc && time <= window.ClosesAtUtc && time <= window.NoNewOrdersAfterUtc;
    }
}

public enum ProcessModelRunStatus { Processed, Blocked, AlreadyProcessed, NoActionRequired, Failed }
public enum ProcessModelRunBlockedReason
{
    None,
    StaleModelRun,
    StaleMarketData,
    PositionMismatch,
    UnknownCurrentPosition,
    RiskRejected,
    RiskBlocked,
    TradingWindowClosed,
    KillSwitchActive,
    NoMarketData,
    NoTargetWeights,
    NoDrift,
    Other
}

public sealed record ProcessModelRunResult(
    ModelRunId? ModelRunId,
    bool Processed,
    ProcessModelRunStatus Status,
    ProcessModelRunBlockedReason? BlockedReason,
    string? Message,
    int TradeIntentCount,
    int RiskDecisionCount,
    int OrderCount,
    int ExecutionReportCount,
    int FillCount,
    int ReconciliationBreakCount,
    bool IsAlreadyProcessed,
    DateTimeOffset CompletedAtUtc)
{
    public bool Blocked => Status == ProcessModelRunStatus.Blocked;

    public static ProcessModelRunResult NoWork(DateTimeOffset now)
        => new(null, false, ProcessModelRunStatus.NoActionRequired, null, "No unprocessed model runs.", 0, 0, 0, 0, 0, 0, false, now);
}

public sealed class ProcessModelRunService(IIntradayRepository repository, IVenueExecutionGateway venueGateway, IBrokerPositionProvider brokerPositionProvider, IClock clock)
{
    public async Task<ProcessModelRunResult> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var run = await repository.GetNextUnprocessedModelRunAsync(cancellationToken);
        if (run is null)
        {
            return ProcessModelRunResult.NoWork(clock.UtcNow);
        }

        return await ProcessAsync(run.Id, cancellationToken);
    }

    public async Task<ProcessModelRunResult> ProcessAsync(ModelRunId modelRunId, CancellationToken cancellationToken = default)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var run = state.ModelRuns.FirstOrDefault(x => x.Id == modelRunId);
        if (run is null)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Failed, ProcessModelRunBlockedReason.Other, "Model run not found.", false, clock.UtcNow);
        }

        if (run.IsProcessed)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.AlreadyProcessed, null, "Model run already processed.", true, clock.UtcNow);
        }

        var now = clock.UtcNow;
        var previousBlock = GetExistingBlock(state, run.Id, now);
        if (previousBlock is not null)
        {
            return previousBlock;
        }

        var fund = state.Funds.Single(x => x.Id == run.FundId);
        var brokerAccount = state.BrokerAccounts.Single(x => x.FundId == fund.Id);
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var riskLimitSet = state.RiskLimitSets.Single(x => x.FundId == fund.Id);
        var tradingWindow = state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id && x.ModelName == run.ModelName && x.DayOfWeek == now.DayOfWeek)
            ?? state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id && x.ModelName == run.ModelName)
            ?? state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id)
            ?? new TradingWindow(Guid.Empty, fund.Id, run.ModelName, "UTC", now.DayOfWeek, TimeOnly.MaxValue, TimeOnly.MinValue, TimeOnly.MinValue, null, false, false);
        var targetWeights = state.TargetWeights.Where(x => x.ModelRunId == run.Id).ToList();
        if (targetWeights.Count == 0)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.NoTargetWeights, "No target weights exist for the model run.", false, now);
        }

        var brokerPositions = await brokerPositionProvider.GetPositionsAsync(brokerAccount.Id, cancellationToken);
        var internalPositions = BuildInternalPositions(state, fund.Id, now);
        var reconciliation = Reconcile(run.Id, ReconciliationPhase.PreTrade, targetWeights.Select(x => x.InstrumentId).ToList(), internalPositions, brokerPositions, riskLimitSet.PositionToleranceBaseQuantity, now);
        await repository.SaveReconciliationAsync(reconciliation.Run, reconciliation.Breaks, cancellationToken);
        if (reconciliation.Run.HasBlockingBreaks)
        {
            state = await repository.LoadStateAsync(cancellationToken);
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading blocked by pre-trade reconciliation breaks.", false, now);
        }

        var calculator = new TargetPositionCalculator();
        var riskEngine = new RiskEngine();

        foreach (var weight in targetWeights)
        {
            var instrument = state.Instruments.Single(x => x.Id == weight.InstrumentId);
            var mapping = state.VenueInstrumentMappings
                .Where(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id && x.IsEnabled)
                .OrderBy(x => x.Id.Value)
                .FirstOrDefault();
            if (mapping is null)
            {
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.Other, $"No enabled venue mapping exists for {instrument.Symbol}.", false, now);
            }

            var marketData = state.MarketData.Where(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id).MaxBy(x => x.ReceivedAtUtc);
            if (marketData is null)
            {
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.NoMarketData, $"No market data exists for {instrument.Symbol}.", false, now);
            }

            var target = calculator.Calculate(run, weight, marketData, mapping);
            var currentBase = internalPositions.GetValueOrDefault(instrument.Id, 0m);
            var currentVenue = currentBase / mapping.ContractSize;
            var driftBase = target.TargetBaseQuantity - currentBase;
            var driftVenue = target.TargetVenueQuantity - currentVenue;
            var drift = new DriftSnapshot(run.Id, instrument.Id, target.TargetBaseQuantity, currentBase, driftBase, target.TargetVenueQuantity, currentVenue, driftVenue);
            await repository.SaveTargetAndDriftAsync(target, drift, cancellationToken);

            if (Math.Abs(drift.DriftVenueQuantity) < riskLimitSet.MinDriftVenueQuantity)
            {
                continue;
            }

            var intent = new TradeIntent(
                TradeIntentId.New(),
                run.Id,
                fund.Id,
                instrument.Id,
                drift.DriftBaseQuantity > 0 ? TradeSide.Buy : TradeSide.Sell,
                Math.Abs(drift.DriftBaseQuantity),
                Math.Abs(drift.DriftVenueQuantity),
                "Model drift",
                TradeIntentStatus.Created,
                now);

            await repository.AddTradeIntentAsync(intent, cancellationToken);
            var instrumentLimit = state.InstrumentRiskLimits
                .Where(x => x.RiskLimitSetId == riskLimitSet.Id && x.InstrumentId == instrument.Id && x.IsEnabled)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            var venueLimit = state.VenueRiskLimits
                .Where(x => x.RiskLimitSetId == riskLimitSet.Id && x.VenueId == venue.Id && x.IsEnabled)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (instrumentLimit is null || venueLimit is null)
            {
                state = await repository.LoadStateAsync(cancellationToken);
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.Other, "Risk configuration is missing for the requested instrument or venue.", false, now);
            }

            var riskContext = new RiskContext(fund, venue, instrument, run, marketData, currentBase, true, CalculateGrossExposure(state, fund.Id, marketData.Mid), now);
            var decision = riskEngine.Evaluate(intent, riskContext, riskLimitSet, instrumentLimit, venueLimit, tradingWindow, state.KillSwitch);
            await repository.AddRiskDecisionAsync(decision, cancellationToken);
            if (decision.Status != RiskDecisionStatus.Approved)
            {
                state = await repository.LoadStateAsync(cancellationToken);
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, MapBlockedReason(decision.RejectReason), BuildRiskMessage(decision.RejectReason), false, now);
            }

            var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new ClientOrderId($"P-{run.Id.Value:N}-{state.ParentOrders.Count + 1}"), intent.Side == TradeSide.Buy ? OrderSide.Buy : OrderSide.Sell, intent.RequestedBaseQuantity, ExecutionAlgo.MarketImmediate, OrderStatus.Created, now);
            var child = new ChildOrder(ChildOrderId.New(), parent.Id, venue.Id, new ClientOrderId($"C-{run.Id.Value:N}-{state.ChildOrders.Count + 1}"), parent.Side, OrderType.Market, TimeInForce.IOC, intent.RequestedBaseQuantity, intent.RequestedVenueQuantity, OrderStatus.PendingNew, now);
            await repository.AddOrdersAsync(parent, child, cancellationToken);

            var result = await venueGateway.SendOrderAsync(new VenueOrderRequest(child.Id, venue.Id, instrument.Id, child.ClientOrderId, child.Side, child.OrderType, child.TimeInForce, child.BaseQuantity, child.VenueQuantity), cancellationToken);
            foreach (var report in result.Reports)
            {
                await repository.AddExecutionReportAsync(report, cancellationToken);
                if (report.ExecutionReportType is ExecutionReportType.Fill or ExecutionReportType.PartialFill && report.BrokerExecutionId is not null && report.LastQuantity > 0)
                {
                    var side = child.Side == OrderSide.Buy ? TradeSide.Buy : TradeSide.Sell;
                    var fill = new Fill(FillId.New(), report.BrokerExecutionId, child.Id, instrument.Id, venue.Id, side, report.LastQuantity * mapping.ContractSize, report.LastQuantity, report.LastPrice, report.ReceivedAtUtc, report.ReceivedAtUtc);
                    if (await repository.TryAddFillAsync(fill, cancellationToken))
                    {
                        var signed = side == TradeSide.Buy ? fill.BaseQuantity : -fill.BaseQuantity;
                        await repository.AddPositionLedgerEventAsync(new PositionLedgerEvent(Guid.NewGuid(), fund.Id, instrument.Id, PositionLedgerEventType.Fill, signed, fill.BrokerExecutionId, now), cancellationToken);
                    }
                }
            }
        }

        state = await repository.LoadStateAsync(cancellationToken);
        internalPositions = BuildInternalPositions(state, fund.Id, now);
        brokerPositions = await brokerPositionProvider.GetPositionsAsync(brokerAccount.Id, cancellationToken);
        var postTrade = Reconcile(run.Id, ReconciliationPhase.PostTrade, targetWeights.Select(x => x.InstrumentId).ToList(), internalPositions, brokerPositions, riskLimitSet.PositionToleranceBaseQuantity, now);
        await repository.SaveReconciliationAsync(postTrade.Run, postTrade.Breaks, cancellationToken);

        if (postTrade.Run.HasBlockingBreaks)
        {
            state = await repository.LoadStateAsync(cancellationToken);
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading completed but post-trade reconciliation has blocking breaks.", false, now);
        }

        await repository.MarkModelRunProcessedAsync(run.Id, ModelRunStatus.Processed, cancellationToken);
        state = await repository.LoadStateAsync(cancellationToken);
        var noDrift = state.TradeIntents.All(x => x.ModelRunId != run.Id);
        return BuildResult(state, run.Id, true, noDrift ? ProcessModelRunStatus.NoActionRequired : ProcessModelRunStatus.Processed, noDrift ? ProcessModelRunBlockedReason.NoDrift : null, noDrift ? "No rebalance drift exceeded the configured threshold." : "Processed.", false, now);
    }

    private static Dictionary<InstrumentId, decimal> BuildInternalPositions(PlatformState state, FundId fundId, DateTimeOffset now)
        => state.PositionLedger
            .Where(x => x.FundId == fundId && x.CreatedAtUtc <= now)
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.BaseQuantityDelta));

    private static decimal CalculateGrossExposure(PlatformState state, FundId fundId, decimal price)
        => Math.Abs(state.PositionLedger.Where(x => x.FundId == fundId).Sum(x => x.BaseQuantityDelta) * price);

    private static (ReconciliationRun Run, IReadOnlyList<ReconciliationBreak> Breaks) Reconcile(ModelRunId modelRunId, ReconciliationPhase phase, IReadOnlyList<InstrumentId> instrumentIds, Dictionary<InstrumentId, decimal> internalPositions, IReadOnlyList<BrokerPositionSnapshot> brokerPositions, decimal tolerance, DateTimeOffset now)
    {
        var breaks = new List<ReconciliationBreak>();
        foreach (var instrumentId in instrumentIds.Distinct())
        {
            var internalPosition = internalPositions.GetValueOrDefault(instrumentId, 0m);
            var brokerPosition = brokerPositions.FirstOrDefault(x => x.InstrumentId == instrumentId)?.BaseQuantity ?? 0m;
            if (Math.Abs(internalPosition - brokerPosition) > tolerance)
            {
                breaks.Add(new ReconciliationBreak(Guid.NewGuid(), Guid.Empty, ReconciliationBreakType.InternalBrokerPositionMismatch, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open, instrumentId, $"Internal {internalPosition} vs broker {brokerPosition}."));
            }
        }

        var run = new ReconciliationRun(Guid.NewGuid(), modelRunId, phase, now, breaks.Any(x => x.Severity == ReconciliationBreakSeverity.Blocking));
        return (run, breaks.Select(x => x with { ReconciliationRunId = run.Id }).ToList());
    }

    private static ProcessModelRunResult? GetExistingBlock(PlatformState state, ModelRunId modelRunId, DateTimeOffset now)
    {
        var riskDecision = state.RiskDecisions
            .Where(x => state.TradeIntents.Any(t => t.Id == x.TradeIntentId && t.ModelRunId == modelRunId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault(x => x.Status != RiskDecisionStatus.Approved);
        if (riskDecision is not null)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Blocked, MapBlockedReason(riskDecision.RejectReason), BuildRiskMessage(riskDecision.RejectReason), false, now);
        }

        if (state.ReconciliationRuns.Any(x => x.ModelRunId == modelRunId && x.Phase == ReconciliationPhase.PreTrade && x.HasBlockingBreaks))
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading blocked by pre-trade reconciliation breaks.", false, now);
        }

        return null;
    }

    private static ProcessModelRunResult BuildResult(PlatformState state, ModelRunId? modelRunId, bool processed, ProcessModelRunStatus status, ProcessModelRunBlockedReason? blockedReason, string? message, bool alreadyProcessed, DateTimeOffset now)
    {
        if (modelRunId is null)
        {
            return new ProcessModelRunResult(null, processed, status, blockedReason, message, 0, 0, 0, 0, 0, 0, alreadyProcessed, now);
        }

        var intents = state.TradeIntents.Where(x => x.ModelRunId == modelRunId).ToList();
        var intentIds = intents.Select(x => x.Id).ToHashSet();
        var parentOrders = state.ParentOrders.Where(x => intentIds.Contains(x.TradeIntentId)).ToList();
        var parentIds = parentOrders.Select(x => x.Id).ToHashSet();
        var childOrders = state.ChildOrders.Where(x => parentIds.Contains(x.ParentOrderId)).ToList();
        var childIds = childOrders.Select(x => x.Id).ToHashSet();
        var reconciliationRunIds = state.ReconciliationRuns.Where(x => x.ModelRunId == modelRunId).Select(x => x.Id).ToHashSet();

        return new ProcessModelRunResult(
            modelRunId,
            processed,
            status,
            blockedReason,
            message,
            intents.Count,
            state.RiskDecisions.Count(x => intentIds.Contains(x.TradeIntentId)),
            parentOrders.Count,
            state.ExecutionReports.Count(x => childIds.Contains(x.ChildOrderId)),
            state.Fills.Count(x => childIds.Contains(x.ChildOrderId)),
            state.ReconciliationBreaks.Count(x => reconciliationRunIds.Contains(x.ReconciliationRunId)),
            alreadyProcessed,
            now);
    }

    private static ProcessModelRunBlockedReason MapBlockedReason(RiskRejectReason reason)
        => reason switch
        {
            RiskRejectReason.StaleModelRun => ProcessModelRunBlockedReason.StaleModelRun,
            RiskRejectReason.StaleMarketData => ProcessModelRunBlockedReason.StaleMarketData,
            RiskRejectReason.PositionMismatch => ProcessModelRunBlockedReason.PositionMismatch,
            RiskRejectReason.UnknownCurrentPosition => ProcessModelRunBlockedReason.UnknownCurrentPosition,
            RiskRejectReason.TradingWindowClosed => ProcessModelRunBlockedReason.TradingWindowClosed,
            RiskRejectReason.KillSwitchActive => ProcessModelRunBlockedReason.KillSwitchActive,
            _ => ProcessModelRunBlockedReason.RiskRejected
        };

    private static string BuildRiskMessage(RiskRejectReason reason)
        => reason switch
        {
            RiskRejectReason.StaleModelRun => "Model run is stale.",
            RiskRejectReason.StaleMarketData => "Market data is stale.",
            RiskRejectReason.TradingWindowClosed => "Trading window is closed.",
            RiskRejectReason.KillSwitchActive => "Kill switch is active.",
            RiskRejectReason.PositionMismatch => "Positions do not match.",
            _ => $"Risk rejected the trade: {reason}."
        };
}

public static class SeedData
{
    public static PlatformState Create(DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var fundId = new FundId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var accountId = new BrokerAccountId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var instrumentId = new InstrumentId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        var venueId = new VenueId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var runId = new ModelRunId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
        var limitSetId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var venueInstrumentId = new VenueInstrumentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var instrumentRiskLimitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var venueRiskLimitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tradingWindowId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var killSwitchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var startOfDayEventId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var seedMarketDataSnapshotId = new MarketDataSnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        var state = new PlatformState();

        state.Funds.Add(new Fund(fundId, "QQ Intraday Fund", Currency.Usd));
        state.BrokerAccounts.Add(new BrokerAccount(accountId, fundId, "QQ-LMAX-SIM"));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Simulator));
        state.VenueInstrumentMappings.Add(new VenueInstrumentMapping(venueInstrumentId, venueId, instrumentId, "EURUSD", "EUR/USD", 10000m, 0.1m, 0.1m, 0.00001m));
        state.NavSnapshots.Add(new NavSnapshot(fundId, 1_000_000m, NavSource.Seed, now));
        state.MarketData.Add(new MarketDataSnapshot(seedMarketDataSnapshotId, instrumentId, venueId, 1.09995m, 1.10005m, null, "Seed", now, now) { IsSynthetic = true, CreatedAtUtc = now });
        state.PositionLedger.Add(new PositionLedgerEvent(startOfDayEventId, fundId, instrumentId, PositionLedgerEventType.StartOfDay, 0m, "SOD", now.AddHours(-1)));
        state.RiskLimitSets.Add(new RiskLimitSet(limitSetId, fundId, true, 2_000_000m, TimeSpan.FromHours(24), TimeSpan.FromMinutes(30), 0.0001m, 0.1m));
        state.InstrumentRiskLimits.Add(new InstrumentRiskLimit(instrumentRiskLimitId, limitSetId, instrumentId, 500_000m, 1_500_000m));
        state.VenueRiskLimits.Add(new VenueRiskLimit(venueRiskLimitId, limitSetId, venueId, 500_000m));
        state.TradingWindows.Add(new TradingWindow(tradingWindowId, fundId, "Sample FX Intraday", "UTC", now.DayOfWeek, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null));
        state.KillSwitch = new KillSwitchState(killSwitchId, false, null, now);
        state.ModelRuns.Add(new ModelRun(runId, fundId, "Sample FX Intraday", now.AddMinutes(-1), now, now, 15, 1_000_000m, ModelRunStatus.Received, "sample", "sample.csv", false));
        state.TargetWeights.Add(new TargetWeight(runId, instrumentId, -0.10m, "EURUSD"));
        return state;
    }
}
