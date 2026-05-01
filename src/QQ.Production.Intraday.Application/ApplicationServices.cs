using System.Security.Cryptography;
using System.Text;
using System.Globalization;
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

public interface IModelWeightBatchRepository
{
    Task<ModelWeightBatch?> GetBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<ModelWeightBatch?> GetBatchByExternalIdAsync(ModelWeightSourceSystem sourceSystem, string externalBatchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightBatch>> GetRecentBatchesAsync(int limit, ModelWeightBatchStatus? status, ModelWeightSourceSystem? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightBatch>> GetReadyBatchesAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightRow>> GetRowsAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightValidationIssue>> GetValidationIssuesAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task AddBatchAsync(ModelWeightBatch batch, IReadOnlyList<ModelWeightRow> rows, CancellationToken cancellationToken);
    Task UpdateBatchAsync(ModelWeightBatch batch, CancellationToken cancellationToken);
    Task AddValidationIssuesAsync(ModelWeightBatchId batchId, IReadOnlyList<ModelWeightValidationIssue> issues, bool replaceExisting, CancellationToken cancellationToken);
    Task MarkPromotedAsync(ModelWeightBatchId batchId, ModelRunId modelRunId, DateTimeOffset promotedAtUtc, CancellationToken cancellationToken);
}

public interface IModelWeightPromotionService
{
    Task<ModelWeightPromotionResult> ValidateBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<ModelWeightPromotionResult> PromoteBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightPromotionResult>> PromoteReadyBatchesAsync(int limit, CancellationToken cancellationToken);
}

public interface IFakeModelWeightGenerator
{
    Task<ModelWeightBatch> CreateFakeBatchAsync(CreateFakeModelWeightBatchRequest request, CancellationToken cancellationToken);
}

public interface ILmaxEodReportRepository
{
    Task AddImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken);
    Task UpdateImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken);
    Task AddValidationIssuesAsync(IReadOnlyList<LmaxReportValidationIssue> issues, CancellationToken cancellationToken);
    Task AddIndividualTradesAsync(IReadOnlyList<LmaxIndividualTrade> trades, CancellationToken cancellationToken);
    Task AddTradeSummariesAsync(IReadOnlyList<LmaxTradeSummary> summaries, CancellationToken cancellationToken);
    Task AddCurrencyWalletsAsync(IReadOnlyList<LmaxCurrencyWallet> wallets, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxReportImportRun>> GetImportRunsAsync(int limit, DateOnly? reportDate, LmaxReportType? reportType, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxReportValidationIssue>> GetValidationIssuesAsync(int limit, LmaxReportImportRunId? importRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxIndividualTrade>> GetIndividualTradesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxTradeSummary>> GetTradeSummariesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxCurrencyWallet>> GetCurrencyWalletsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task AddEodReconciliationAsync(EodReconciliationRun run, IReadOnlyList<EodReconciliationBreak> breaks, CancellationToken cancellationToken);
    Task<IReadOnlyList<EodReconciliationRun>> GetEodReconciliationRunsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<EodReconciliationBreak>> GetEodReconciliationBreaksAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
}

public interface ILmaxEodReportImportService
{
    Task<LmaxReportImportResult> ImportIndividualTradesAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportTradesSummaryAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportCurrencyWalletsAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportReportSetAsync(string individualTradesPath, string tradesSummaryPath, string currencyWalletsPath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface ILmaxReportPairConsistencyService
{
    Task<IReadOnlyList<LmaxReportValidationIssue>> CheckAsync(LmaxReportImportRunId importRunId, DateOnly reportDate, VenueId venueId, BrokerAccountId brokerAccountId, CancellationToken cancellationToken);
}

public interface IEodReconciliationService
{
    Task<EodReconciliationResult> RunAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface IEodPnlSummaryService
{
    Task<EodPnlSummary?> GetSummaryAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface IFakeLmaxEodReportGenerator
{
    Task<FakeLmaxEodReportGenerationResult> GenerateAsync(DateOnly reportDate, string venueName, string brokerAccountCode, LmaxEodMutationMode mutationMode, CancellationToken cancellationToken);
}

public sealed record BarUpsertResult(bool Created);
public sealed record BarBuildResult(BarBuildRunId RunId, int BarsCreated, int BarsUpdated, BarBuildRunStatus Status, string? ErrorMessage = null);
public sealed record CreateFakeModelWeightRowRequest(string RawSecurityId, string Symbol, decimal Weight);
public sealed record CreateFakeModelWeightBatchRequest(
    string? ExternalBatchId,
    ModelWeightSourceSystem SourceSystem,
    string FundCode,
    string ModelName,
    DateTimeOffset? AsOfUtc,
    DateTimeOffset? EffectiveAtUtc,
    int FrequencyMinutes,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode,
    ModelWeightBatchStatus Status,
    IReadOnlyList<CreateFakeModelWeightRowRequest> Weights);
public sealed record ModelWeightPromotionResult(
    ModelWeightBatchId? BatchId,
    ModelWeightBatchStatus? Status,
    ModelRunId? PromotedModelRunId,
    ModelRunId? ModelRunId,
    int ValidationIssueCount,
    IReadOnlyList<ModelWeightValidationIssue> Issues,
    string Message,
    bool Succeeded,
    bool AlreadyPromoted);
public sealed record LmaxReportImportResult(LmaxReportImportRunId ImportRunId, LmaxReportImportStatus Status, int RowCount, int BlockingIssueCount, IReadOnlyList<LmaxReportValidationIssue> Issues, string Message);
public sealed record EodReconciliationResult(Guid RunId, DateOnly ReportDate, int BreakCount, int BlockingBreakCount, IReadOnlyList<EodReconciliationBreak> Breaks);
public sealed record FakeLmaxEodReportGenerationResult(DateOnly ReportDate, string IndividualTradesPath, string TradesSummaryPath, string CurrencyWalletsPath, int IndividualTradeCount, int TradeSummaryCount, int CurrencyWalletCount, LmaxEodMutationMode MutationMode);

public enum ReferenceDataIntegrityIssueType
{
    DuplicateFund,
    DuplicateBrokerAccount,
    DuplicateInstrument,
    DuplicateVenue,
    DuplicateVenueInstrumentMapping,
    DuplicateRiskLimitSet,
    DuplicateRiskLimit,
    DuplicateInstrumentRiskLimit,
    DuplicateVenueRiskLimit,
    DuplicateTradingWindow,
    DuplicateKillSwitchState,
    MissingRequiredReferenceData,
    AmbiguousReferenceData,
    DisabledRequiredReferenceData
}

public enum ReferenceDataIntegritySeverity { Info, Warning, Blocking }
public enum ReferenceDataIntegrityStatus { Open, Acknowledged, Resolved }

public sealed record ReferenceDataIntegrityIssue(
    Guid Id,
    ReferenceDataIntegrityIssueType Type,
    ReferenceDataIntegritySeverity Severity,
    ReferenceDataIntegrityStatus Status,
    string Key,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record ReferenceDataIntegrityResult(
    DateTimeOffset CheckedAtUtc,
    int BlockingIssueCount,
    int WarningIssueCount,
    IReadOnlyList<ReferenceDataIntegrityIssue> Issues);

public interface IReferenceDataIntegrityService
{
    Task<ReferenceDataIntegrityResult> CheckAsync(CancellationToken cancellationToken);
}

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
    public List<InstrumentAlias> InstrumentAliases { get; } = [];
    public List<Venue> Venues { get; } = [];
    public List<VenueInstrumentMapping> VenueInstrumentMappings { get; } = [];
    public List<NavSnapshot> NavSnapshots { get; } = [];
    public List<ModelRun> ModelRuns { get; } = [];
    public List<TargetWeight> TargetWeights { get; } = [];
    public List<ModelWeightBatch> ModelWeightBatches { get; } = [];
    public List<ModelWeightRow> ModelWeightRows { get; } = [];
    public List<ModelWeightValidationIssue> ModelWeightValidationIssues { get; } = [];
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
    public List<RiskLimit> RiskLimits { get; } = [];
    public List<InstrumentRiskLimit> InstrumentRiskLimits { get; } = [];
    public List<VenueRiskLimit> VenueRiskLimits { get; } = [];
    public List<TradingWindow> TradingWindows { get; } = [];
    public List<KillSwitchState> KillSwitchStates { get; } = [];
    public List<LmaxReportImportRun> LmaxReportImportRuns { get; } = [];
    public List<LmaxReportValidationIssue> LmaxReportValidationIssues { get; } = [];
    public List<LmaxIndividualTrade> LmaxIndividualTrades { get; } = [];
    public List<LmaxTradeSummary> LmaxTradeSummaries { get; } = [];
    public List<LmaxCurrencyWallet> LmaxCurrencyWallets { get; } = [];
    public List<EodReconciliationRun> EodReconciliationRuns { get; } = [];
    public List<EodReconciliationBreak> EodReconciliationBreaks { get; } = [];
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

public sealed class InMemoryModelWeightBatchRepository(PlatformState state) : IModelWeightBatchRepository
{
    private readonly object _sync = new();

    public Task<ModelWeightBatch?> GetBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelWeightBatches.FirstOrDefault(x => x.Id == batchId));
        }
    }

    public Task<ModelWeightBatch?> GetBatchByExternalIdAsync(ModelWeightSourceSystem sourceSystem, string externalBatchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelWeightBatches.FirstOrDefault(x => x.SourceSystem == sourceSystem && x.ExternalBatchId == externalBatchId));
        }
    }

    public Task<IReadOnlyList<ModelWeightBatch>> GetRecentBatchesAsync(int limit, ModelWeightBatchStatus? status, ModelWeightSourceSystem? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.ModelWeightBatches.AsEnumerable();
            if (status is not null) query = query.Where(x => x.Status == status);
            if (sourceSystem is not null) query = query.Where(x => x.SourceSystem == sourceSystem);
            if (!string.IsNullOrWhiteSpace(modelName)) query = query.Where(x => x.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            if (fromUtc is not null) query = query.Where(x => x.AsOfUtc >= fromUtc.Value);
            if (toUtc is not null) query = query.Where(x => x.AsOfUtc < toUtc.Value);
            return Task.FromResult<IReadOnlyList<ModelWeightBatch>>(query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightBatch>> GetReadyBatchesAsync(int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightBatch>>(state.ModelWeightBatches
                .Where(x => x.Status is ModelWeightBatchStatus.Ready or ModelWeightBatchStatus.Accepted)
                .OrderBy(x => x.AsOfUtc)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightRow>> GetRowsAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightRow>>(state.ModelWeightRows.Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightValidationIssue>> GetValidationIssuesAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightValidationIssue>>(state.ModelWeightValidationIssues.Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToList());
        }
    }

    public Task AddBatchAsync(ModelWeightBatch batch, IReadOnlyList<ModelWeightRow> rows, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ModelWeightBatches.Any(x => x.SourceSystem == batch.SourceSystem && x.ExternalBatchId == batch.ExternalBatchId))
            {
                return Task.CompletedTask;
            }

            state.ModelWeightBatches.Add(batch);
            state.ModelWeightRows.AddRange(rows);
        }

        return Task.CompletedTask;
    }

    public Task UpdateBatchAsync(ModelWeightBatch batch, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelWeightBatches.FindIndex(x => x.Id == batch.Id);
            if (index >= 0)
            {
                state.ModelWeightBatches[index] = batch;
            }
        }

        return Task.CompletedTask;
    }

    public Task AddValidationIssuesAsync(ModelWeightBatchId batchId, IReadOnlyList<ModelWeightValidationIssue> issues, bool replaceExisting, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (replaceExisting)
            {
                state.ModelWeightValidationIssues.RemoveAll(x => x.BatchId == batchId);
            }

            state.ModelWeightValidationIssues.AddRange(issues);
        }

        return Task.CompletedTask;
    }

    public Task MarkPromotedAsync(ModelWeightBatchId batchId, ModelRunId modelRunId, DateTimeOffset promotedAtUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelWeightBatches.FindIndex(x => x.Id == batchId);
            if (index >= 0)
            {
                var batch = state.ModelWeightBatches[index];
                state.ModelWeightBatches[index] = batch with { Status = ModelWeightBatchStatus.Promoted, PromotedAtUtc = promotedAtUtc, PromotedModelRunId = modelRunId, Message = "Promoted to model run." };
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

public sealed class ReferenceDataIntegrityService(IIntradayRepository repository, IClock clock) : IReferenceDataIntegrityService
{
    public async Task<ReferenceDataIntegrityResult> CheckAsync(CancellationToken cancellationToken)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var now = clock.UtcNow;
        var issues = new List<ReferenceDataIntegrityIssue>();

        AddDuplicateIssues(issues, state.Funds.Where(x => x.IsEnabled), x => x.Name, ReferenceDataIntegrityIssueType.DuplicateFund, "enabled fund", now);
        AddDuplicateIssues(issues, state.BrokerAccounts.Where(x => x.IsEnabled), x => $"{x.FundId.Value:N}|{x.AccountCode}", ReferenceDataIntegrityIssueType.DuplicateBrokerAccount, "enabled broker account", now);
        AddDuplicateIssues(issues, state.Instruments.Where(x => x.IsEnabled), x => $"{x.Symbol}|{x.AssetClass}", ReferenceDataIntegrityIssueType.DuplicateInstrument, "enabled instrument", now);
        AddDuplicateIssues(issues, state.Venues.Where(x => x.IsEnabled), x => x.Name, ReferenceDataIntegrityIssueType.DuplicateVenue, "enabled venue", now);
        AddDuplicateIssues(issues, state.VenueInstrumentMappings.Where(x => x.IsEnabled), x => $"{x.VenueId.Value:N}|{x.InstrumentId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, "enabled venue/instrument mapping", now);
        AddDuplicateIssues(issues, state.VenueInstrumentMappings.Where(x => x.IsEnabled), x => $"{x.VenueId.Value:N}|{x.VenueSymbol}", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, "enabled venue symbol mapping", now);
        AddDuplicateIssues(issues, state.RiskLimitSets, x => x.FundId.Value.ToString("N"), ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet, "risk limit set", now);
        AddDuplicateIssues(issues, state.RiskLimits, x => $"{x.RiskLimitSetId:N}|{x.Name}", ReferenceDataIntegrityIssueType.DuplicateRiskLimit, "risk limit", now);
        AddDuplicateIssues(issues, state.InstrumentRiskLimits.Where(x => x.IsEnabled), x => $"{x.RiskLimitSetId:N}|{x.InstrumentId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateInstrumentRiskLimit, "enabled instrument risk limit", now);
        AddDuplicateIssues(issues, state.VenueRiskLimits.Where(x => x.IsEnabled), x => $"{x.RiskLimitSetId:N}|{x.VenueId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateVenueRiskLimit, "enabled venue risk limit", now);
        AddDuplicateIssues(issues, state.TradingWindows.Where(x => x.IsEnabled), x => $"{x.FundId.Value:N}|{x.ModelName}|{x.DayOfWeek}", ReferenceDataIntegrityIssueType.DuplicateTradingWindow, "enabled trading window", now);
        AddAmbiguousCurrentKillSwitchIssue(issues, state.KillSwitchStates, now);

        var fund = RequireExactlyOne(issues, state.Funds.Where(x => x.Name == "QQ Intraday Fund").ToList(), x => x.IsEnabled, "QQ Intraday Fund", ReferenceDataIntegrityIssueType.DuplicateFund, now);
        var instrument = RequireExactlyOne(issues, state.Instruments.Where(x => x.Symbol == "EURUSD" && x.AssetClass == AssetClass.FxSpot).ToList(), x => x.IsEnabled, "EURUSD/FxSpot", ReferenceDataIntegrityIssueType.DuplicateInstrument, now);
        var venue = RequireExactlyOne(issues, state.Venues.Where(x => x.Name == "LMAX").ToList(), x => x.IsEnabled, "LMAX", ReferenceDataIntegrityIssueType.DuplicateVenue, now);

        if (fund is not null)
        {
            RequireExactlyOne(issues, state.BrokerAccounts.Where(x => x.FundId == fund.Id).ToList(), x => x.IsEnabled, $"BrokerAccount:{fund.Id.Value:N}", ReferenceDataIntegrityIssueType.DuplicateBrokerAccount, now);
            RequireExactlyOne(issues, state.RiskLimitSets.Where(x => x.FundId == fund.Id).ToList(), _ => true, $"RiskLimitSet:{fund.Id.Value:N}", ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet, now);
            RequireAtLeastOne(issues, state.TradingWindows.Where(x => x.FundId == fund.Id && x.ModelName == "IntradayFxModel" && x.IsEnabled), "TradingWindow:IntradayFxModel", now);
        }

        if (venue is not null && instrument is not null)
        {
            RequireExactlyOne(issues, state.VenueInstrumentMappings.Where(x => x.VenueId == venue.Id && x.InstrumentId == instrument.Id).ToList(), x => x.IsEnabled, "LMAX:EURUSD", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, now);
        }

        if (state.KillSwitchStates.Count == 0 && state.KillSwitch.UpdatedAtUtc == DateTimeOffset.UnixEpoch)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, "KillSwitchState", "No kill-switch state exists.", now));
        }

        return new ReferenceDataIntegrityResult(
            now,
            issues.Count(x => x.Severity == ReferenceDataIntegritySeverity.Blocking),
            issues.Count(x => x.Severity == ReferenceDataIntegritySeverity.Warning),
            issues);
    }

    private static void AddDuplicateIssues<T>(List<ReferenceDataIntegrityIssue> issues, IEnumerable<T> values, Func<T, string> keySelector, ReferenceDataIntegrityIssueType type, string label, DateTimeOffset now)
    {
        foreach (var group in values.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            issues.Add(NewIssue(type, group.Key, $"Duplicate {label} rows exist for key '{group.Key}'.", now));
        }
    }

    private static T? RequireExactlyOne<T>(List<ReferenceDataIntegrityIssue> issues, IReadOnlyList<T> values, Func<T, bool> enabledSelector, string key, ReferenceDataIntegrityIssueType duplicateType, DateTimeOffset now)
    {
        if (values.Count == 0)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, key, $"Required reference data is missing for '{key}'.", now));
            return default;
        }

        var enabled = values.Where(enabledSelector).ToList();
        if (enabled.Count == 0)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.DisabledRequiredReferenceData, key, $"Required reference data exists but is disabled for '{key}'.", now));
            return default;
        }

        if (enabled.Count > 1)
        {
            issues.Add(NewIssue(duplicateType, key, $"Required reference data is ambiguous for '{key}'.", now));
            return default;
        }

        return enabled[0];
    }

    private static void RequireAtLeastOne<T>(List<ReferenceDataIntegrityIssue> issues, IEnumerable<T> values, string key, DateTimeOffset now)
    {
        if (!values.Any())
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, key, $"Required reference data is missing for '{key}'.", now));
        }
    }

    private static void AddAmbiguousCurrentKillSwitchIssue(List<ReferenceDataIntegrityIssue> issues, IReadOnlyList<KillSwitchState> states, DateTimeOffset now)
    {
        if (states.Count <= 1)
        {
            return;
        }

        var latestTimestamp = states.Max(x => x.UpdatedAtUtc);
        if (states.Count(x => x.UpdatedAtUtc == latestTimestamp) > 1)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.DuplicateKillSwitchState, "KillSwitchState:Current", "Multiple kill-switch rows share the latest timestamp, so the current kill-switch state is ambiguous.", now));
        }
    }

    private static ReferenceDataIntegrityIssue NewIssue(ReferenceDataIntegrityIssueType type, string key, string description, DateTimeOffset now)
        => new(Guid.NewGuid(), type, ReferenceDataIntegritySeverity.Blocking, ReferenceDataIntegrityStatus.Open, key, description, now);
}

public sealed class FakeModelWeightGenerator(IModelWeightBatchRepository repository, IClock clock) : IFakeModelWeightGenerator
{
    public async Task<ModelWeightBatch> CreateFakeBatchAsync(CreateFakeModelWeightBatchRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var asOf = request.AsOfUtc ?? now;
        var effective = request.EffectiveAtUtc ?? asOf;
        var rows = request.Weights;
        var externalBatchId = string.IsNullOrWhiteSpace(request.ExternalBatchId)
            ? $"fake_intraday_fx_{asOf:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..42]
            : request.ExternalBatchId.Trim();
        var contentHash = ModelWeightHash.Compute(request.SourceSystem, externalBatchId, request.FundCode, request.ModelName, asOf, effective, request.FrequencyMinutes, request.NavUsd, request.TargetQuantityMode, rows);

        var existing = await repository.GetBatchByExternalIdAsync(request.SourceSystem, externalBatchId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainRuleViolationException("A model weight batch with the same source system and external batch id already exists with different content.");
            }

            return existing;
        }

        var status = request.Status == ModelWeightBatchStatus.Draft ? ModelWeightBatchStatus.Draft : ModelWeightBatchStatus.Ready;
        var batch = new ModelWeightBatch(
            ModelWeightBatchId.New(),
            externalBatchId,
            request.SourceSystem,
            string.IsNullOrWhiteSpace(request.FundCode) ? "QQ_MASTER" : request.FundCode,
            null,
            string.IsNullOrWhiteSpace(request.ModelName) ? "IntradayFxModel" : request.ModelName,
            asOf,
            effective,
            request.FrequencyMinutes,
            request.NavUsd,
            request.TargetQuantityMode,
            status,
            rows.Count,
            contentHash,
            now,
            status == ModelWeightBatchStatus.Ready ? now : null,
            null,
            null,
            null,
            null,
            "Local fake model weight batch.");
        var modelRows = rows.Select(x => new ModelWeightRow(ModelWeightRowId.New(), batch.Id, x.RawSecurityId, x.Symbol, null, x.Weight, now)).ToList();
        await repository.AddBatchAsync(batch, modelRows, cancellationToken);
        return batch;
    }
}

public sealed class ModelWeightPromotionService(
    IModelWeightBatchRepository batchRepository,
    IIntradayRepository intradayRepository,
    IReferenceDataIntegrityService referenceDataIntegrityService,
    IClock clock) : IModelWeightPromotionService
{
    public async Task<ModelWeightPromotionResult> ValidateBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var batch = await batchRepository.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ModelWeightPromotionResult(batchId, null, null, null, 1, [Issue(batchId, ModelWeightValidationIssueType.MissingBatch, "Model weight batch was not found.", now)], "Model weight batch was not found.", false, false);
        }

        if (batch.Status == ModelWeightBatchStatus.Promoted && batch.PromotedModelRunId is not null)
        {
            return new ModelWeightPromotionResult(batch.Id, batch.Status, batch.PromotedModelRunId, batch.PromotedModelRunId, 0, [], "Batch is already promoted.", true, true);
        }

        await batchRepository.UpdateBatchAsync(batch with { Status = ModelWeightBatchStatus.Validating, Message = "Validating model weight batch." }, cancellationToken);
        var issues = await BuildValidationIssuesAsync(batch, now, cancellationToken);
        await batchRepository.AddValidationIssuesAsync(batch.Id, issues, replaceExisting: true, cancellationToken);
        var blocking = issues.Count(x => x.Severity == ModelWeightValidationSeverity.Blocking);
        var finalStatus = blocking > 0 ? ModelWeightBatchStatus.Rejected : ModelWeightBatchStatus.Accepted;
        var acceptedAt = blocking > 0 ? batch.AcceptedAtUtc : now;
        var rejectedAt = blocking > 0 ? now : batch.RejectedAtUtc;
        var message = blocking > 0 ? $"Validation failed with {blocking} blocking issue(s)." : "Validation accepted.";
        await batchRepository.UpdateBatchAsync(batch with { Status = finalStatus, AcceptedAtUtc = acceptedAt, RejectedAtUtc = rejectedAt, Message = message }, cancellationToken);

        return new ModelWeightPromotionResult(batch.Id, finalStatus, batch.PromotedModelRunId, batch.PromotedModelRunId, issues.Count, issues, message, blocking == 0, false);
    }

    public async Task<ModelWeightPromotionResult> PromoteBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var batch = await batchRepository.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ModelWeightPromotionResult(batchId, null, null, null, 1, [Issue(batchId, ModelWeightValidationIssueType.MissingBatch, "Model weight batch was not found.", now)], "Model weight batch was not found.", false, false);
        }

        if (batch.Status == ModelWeightBatchStatus.Promoted && batch.PromotedModelRunId is not null)
        {
            return new ModelWeightPromotionResult(batch.Id, batch.Status, batch.PromotedModelRunId, batch.PromotedModelRunId, 0, await batchRepository.GetValidationIssuesAsync(batch.Id, cancellationToken), "Batch is already promoted; returning existing model run id.", true, true);
        }

        var validation = await ValidateBatchAsync(batch.Id, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        batch = await batchRepository.GetBatchAsync(batchId, cancellationToken) ?? batch;
        var rows = await batchRepository.GetRowsAsync(batch.Id, cancellationToken);
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var fund = ResolveFund(state, batch.FundCode);
        if (fund is null)
        {
            var issue = Issue(batch.Id, ModelWeightValidationIssueType.InvalidFund, $"Fund code '{batch.FundCode}' is not valid.", now);
            await batchRepository.AddValidationIssuesAsync(batch.Id, [issue], replaceExisting: true, cancellationToken);
            await batchRepository.UpdateBatchAsync(batch with { Status = ModelWeightBatchStatus.Rejected, RejectedAtUtc = now, Message = issue.Message }, cancellationToken);
            return new ModelWeightPromotionResult(batch.Id, ModelWeightBatchStatus.Rejected, null, null, 1, [issue], issue.Message, false, false);
        }

        var run = new ModelRun(
            ModelRunId.New(),
            fund.Id,
            batch.ModelName,
            batch.AsOfUtc,
            now,
            batch.EffectiveAtUtc,
            batch.FrequencyMinutes,
            batch.NavUsd,
            ModelRunStatus.Received,
            batch.ContentHash ?? ModelWeightHash.Compute(batch.SourceSystem, batch.ExternalBatchId, batch.FundCode, batch.ModelName, batch.AsOfUtc, batch.EffectiveAtUtc, batch.FrequencyMinutes, batch.NavUsd, batch.TargetQuantityMode, rows.Select(x => new CreateFakeModelWeightRowRequest(x.RawSecurityId, x.Symbol, x.Weight)).ToList()),
            "db-weight-source",
            false,
            batch.TargetQuantityMode);
        var weights = rows.Select(row =>
        {
            var instrument = state.Instruments.Single(x => x.Symbol.Equals(row.Symbol, StringComparison.OrdinalIgnoreCase) && x.IsEnabled);
            return new TargetWeight(run.Id, instrument.Id, row.Weight, row.RawSecurityId);
        }).ToList();

        await intradayRepository.AddModelRunAsync(run, weights, cancellationToken);
        await batchRepository.MarkPromotedAsync(batch.Id, run.Id, now, cancellationToken);
        return new ModelWeightPromotionResult(batch.Id, ModelWeightBatchStatus.Promoted, run.Id, run.Id, 0, [], "Promoted to model run. Processing remains explicit.", true, false);
    }

    public async Task<IReadOnlyList<ModelWeightPromotionResult>> PromoteReadyBatchesAsync(int limit, CancellationToken cancellationToken)
    {
        var batches = await batchRepository.GetReadyBatchesAsync(Math.Clamp(limit, 1, 500), cancellationToken);
        var results = new List<ModelWeightPromotionResult>();
        foreach (var batch in batches)
        {
            results.Add(await PromoteBatchAsync(batch.Id, cancellationToken));
        }

        return results;
    }

    private async Task<IReadOnlyList<ModelWeightValidationIssue>> BuildValidationIssuesAsync(ModelWeightBatch batch, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var issues = new List<ModelWeightValidationIssue>();
        if (batch.Status is not (ModelWeightBatchStatus.Ready or ModelWeightBatchStatus.Accepted or ModelWeightBatchStatus.Validating))
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.BatchNotReady, $"Batch status {batch.Status} is not promotable.", now));
        }

        if (string.IsNullOrWhiteSpace(batch.ModelName)) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidModelName, "Model name is required.", now));
        if (batch.AsOfUtc.Offset != TimeSpan.Zero || batch.EffectiveAtUtc.Offset != TimeSpan.Zero || batch.EffectiveAtUtc < batch.AsOfUtc) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidTimestamp, "As-of and effective timestamps must be UTC and effective must not precede as-of.", now));
        if (batch.NavUsd <= 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidNav, "NAV must be positive.", now));
        if (batch.FrequencyMinutes <= 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidFrequency, "Frequency minutes must be positive.", now));
        if (!Enum.IsDefined(batch.TargetQuantityMode)) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidTargetQuantityMode, "Target quantity mode is invalid.", now));

        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        if (ResolveFund(state, batch.FundCode) is null)
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidFund, $"Fund code '{batch.FundCode}' did not resolve to the local seeded fund.", now));
        }

        var integrity = await referenceDataIntegrityService.CheckAsync(cancellationToken);
        if (integrity.BlockingIssueCount > 0)
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.ReferenceDataInvalid, $"Reference data integrity has {integrity.BlockingIssueCount} blocking issue(s).", now));
        }

        var rows = (await batchRepository.GetRowsAsync(batch.Id, cancellationToken)).ToList();
        if (rows.Count == 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.MissingRows, "At least one model weight row is required.", now));
        if (batch.ExpectedRowCount is not null && batch.ExpectedRowCount.Value != rows.Count) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.RowCountMismatch, $"Expected {batch.ExpectedRowCount.Value} row(s), found {rows.Count}.", now));

        AddDuplicateRowIssues(issues, batch.Id, rows, x => x.Symbol, "symbol", now);
        AddDuplicateRowIssues(issues, batch.Id, rows, x => x.RawSecurityId, "raw security id", now);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (string.IsNullOrWhiteSpace(row.RawSecurityId) || string.IsNullOrWhiteSpace(row.Symbol))
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidWeight, "Raw security id and symbol are required.", now, row.Id, index + 1));
                continue;
            }

            var instruments = state.Instruments.Where(x => x.Symbol.Equals(row.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            if (instruments.Count == 0)
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.UnknownInstrument, $"Symbol '{row.Symbol}' does not resolve to an instrument.", now, row.Id, index + 1));
            }
            else if (!instruments.Any(x => x.IsEnabled))
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.DisabledInstrument, $"Symbol '{row.Symbol}' resolves only to disabled instruments.", now, row.Id, index + 1));
            }
        }

        return issues;
    }

    private static Fund? ResolveFund(PlatformState state, string fundCode)
        => state.Funds.FirstOrDefault(x => x.Name.Equals(fundCode, StringComparison.OrdinalIgnoreCase) && x.IsEnabled)
            ?? (fundCode.Equals("QQ_MASTER", StringComparison.OrdinalIgnoreCase) ? state.Funds.FirstOrDefault(x => x.IsEnabled) : null);

    private static void AddDuplicateRowIssues(List<ModelWeightValidationIssue> issues, ModelWeightBatchId batchId, IReadOnlyList<ModelWeightRow> rows, Func<ModelWeightRow, string> selector, string label, DateTimeOffset now)
    {
        foreach (var duplicate in rows.GroupBy(selector, StringComparer.OrdinalIgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1))
        {
            issues.Add(Issue(batchId, ModelWeightValidationIssueType.DuplicateSecurity, $"Duplicate {label} '{duplicate.Key}' exists in the batch.", now));
        }
    }

    private static ModelWeightValidationIssue Issue(ModelWeightBatchId batchId, ModelWeightValidationIssueType type, string message, DateTimeOffset now, ModelWeightRowId? rowId = null, int? rowNumber = null)
        => new(Guid.NewGuid(), batchId, type, ModelWeightValidationSeverity.Blocking, message, rowId, rowNumber, now);
}

public static class ModelWeightHash
{
    public static string Compute(ModelWeightSourceSystem sourceSystem, string externalBatchId, string fundCode, string modelName, DateTimeOffset asOfUtc, DateTimeOffset effectiveAtUtc, int frequencyMinutes, decimal navUsd, TargetQuantityMode targetQuantityMode, IReadOnlyList<CreateFakeModelWeightRowRequest> rows)
    {
        var builder = new StringBuilder();
        builder.Append(sourceSystem).Append('|').Append(externalBatchId).Append('|').Append(fundCode).Append('|').Append(modelName).Append('|')
            .Append(asOfUtc.ToUniversalTime().ToString("O")).Append('|').Append(effectiveAtUtc.ToUniversalTime().ToString("O")).Append('|')
            .Append(frequencyMinutes).Append('|').Append(navUsd.ToString("0.##########")).Append('|').Append(targetQuantityMode);
        foreach (var row in rows.OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.RawSecurityId, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('|').Append(row.RawSecurityId).Append(':').Append(row.Symbol).Append(':').Append(row.Weight.ToString("0.##########"));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
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
    ReferenceDataInvalid,
    ReferenceDataAmbiguous,
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

public sealed class ProcessModelRunService(IIntradayRepository repository, IVenueExecutionGateway venueGateway, IBrokerPositionProvider brokerPositionProvider, IClock clock, IReferenceDataIntegrityService referenceDataIntegrityService)
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
        var integrity = await referenceDataIntegrityService.CheckAsync(cancellationToken);
        if (integrity.BlockingIssueCount > 0)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, DetermineIntegrityBlockedReason(integrity), $"Reference data integrity check failed with {integrity.BlockingIssueCount} blocking issue(s).", false, now);
        }

        var previousBlock = GetExistingBlock(state, run.Id, now);
        if (previousBlock is not null)
        {
            return previousBlock;
        }

        var fund = state.Funds.SingleOrDefault(x => x.Id == run.FundId && x.IsEnabled);
        var brokerAccount = fund is null ? null : state.BrokerAccounts.SingleOrDefault(x => x.FundId == fund.Id && x.IsEnabled);
        var venue = state.Venues.SingleOrDefault(x => x.Name == "LMAX" && x.IsEnabled);
        var riskLimitSet = fund is null ? null : state.RiskLimitSets.SingleOrDefault(x => x.FundId == fund.Id);
        if (fund is null || brokerAccount is null || venue is null || riskLimitSet is null)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.ReferenceDataInvalid, "Required enabled reference data is missing.", false, now);
        }
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

    private static ProcessModelRunBlockedReason DetermineIntegrityBlockedReason(ReferenceDataIntegrityResult check)
        => check.Issues.Any(x => x.Severity == ReferenceDataIntegritySeverity.Blocking
            && x.Type is ReferenceDataIntegrityIssueType.AmbiguousReferenceData
                or ReferenceDataIntegrityIssueType.DuplicateFund
                or ReferenceDataIntegrityIssueType.DuplicateBrokerAccount
                or ReferenceDataIntegrityIssueType.DuplicateInstrument
                or ReferenceDataIntegrityIssueType.DuplicateVenue
                or ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping
                or ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet
                or ReferenceDataIntegrityIssueType.DuplicateRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateInstrumentRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateVenueRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateTradingWindow
                or ReferenceDataIntegrityIssueType.DuplicateKillSwitchState)
            ? ProcessModelRunBlockedReason.ReferenceDataAmbiguous
            : ProcessModelRunBlockedReason.ReferenceDataInvalid;

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
        var intradayTradingWindowId = Guid.Parse("44444444-4444-4444-4444-444444444445");
        var killSwitchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var startOfDayEventId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var seedMarketDataSnapshotId = new MarketDataSnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        var state = new PlatformState();

        state.Funds.Add(new Fund(fundId, "QQ Intraday Fund", Currency.Usd));
        state.BrokerAccounts.Add(new BrokerAccount(accountId, fundId, "LMAX_DEMO_LOCAL", true, "LMAX_DEMO_LOCAL"));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Simulator));
        state.VenueInstrumentMappings.Add(new VenueInstrumentMapping(venueInstrumentId, venueId, instrumentId, "EURUSD", "EUR/USD", 10000m, 0.1m, 0.1m, 0.00001m));
        state.InstrumentAliases.Add(new InstrumentAlias(new InstrumentAliasId(Guid.Parse("12111111-1111-1111-1111-111111114001")), instrumentId, "LMAX_REPORT", "EUR/USD", "4001", true, now));
        state.NavSnapshots.Add(new NavSnapshot(fundId, 1_000_000m, NavSource.Seed, now));
        state.MarketData.Add(new MarketDataSnapshot(seedMarketDataSnapshotId, instrumentId, venueId, 1.09995m, 1.10005m, null, "Seed", now, now) { IsSynthetic = true, CreatedAtUtc = now });
        state.PositionLedger.Add(new PositionLedgerEvent(startOfDayEventId, fundId, instrumentId, PositionLedgerEventType.StartOfDay, 0m, "SOD", now.AddHours(-1)));
        state.RiskLimitSets.Add(new RiskLimitSet(limitSetId, fundId, true, 2_000_000m, TimeSpan.FromHours(24), TimeSpan.FromMinutes(30), 0.0001m, 0.1m));
        state.InstrumentRiskLimits.Add(new InstrumentRiskLimit(instrumentRiskLimitId, limitSetId, instrumentId, 500_000m, 1_500_000m));
        state.VenueRiskLimits.Add(new VenueRiskLimit(venueRiskLimitId, limitSetId, venueId, 500_000m));
        state.TradingWindows.Add(new TradingWindow(tradingWindowId, fundId, "Sample FX Intraday", "UTC", now.DayOfWeek, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null));
        state.TradingWindows.Add(new TradingWindow(intradayTradingWindowId, fundId, "IntradayFxModel", "UTC", now.DayOfWeek, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null));
        state.KillSwitch = new KillSwitchState(killSwitchId, false, null, now);
        state.KillSwitchStates.Add(state.KillSwitch);
        state.ModelRuns.Add(new ModelRun(runId, fundId, "Sample FX Intraday", now.AddMinutes(-1), now, now, 15, 1_000_000m, ModelRunStatus.Received, "sample", "sample.csv", false));
        state.TargetWeights.Add(new TargetWeight(runId, instrumentId, -0.10m, "EURUSD"));
        return state;
    }
}
