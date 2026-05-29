using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;
using DomainTargetWeight = QQ.Production.Intraday.Domain.TargetWeight;

namespace QQ.Production.Intraday.Application;

public enum QubesIntradayCycleStatus
{
    CompletedNoExternal,
    CompletedWithMissingMarks,
    FailedValidation,
    FailedPersistence,
    FailedReconciliation,
    InconclusiveSafe
}

public sealed record QubesIntradayCycleStatusSnapshot(
    QubesIntradayCycleStatus CycleStatus,
    string TargetWeightsStatus,
    string PersistenceStatus,
    string TheoreticalPortfolioStatus,
    PnLComputationStatus PnLStatus,
    string ReconciliationStatus,
    TheoreticalVsRealStatus ComparatorStatus,
    string RebalanceIntentStatus,
    string SafetyStatus);

public sealed record QubesIntradayCycleFixtureRequest(
    string CycleRunId,
    QubesRunId QubesRunId,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    string FundCode,
    string ModelName,
    decimal PortfolioNotional,
    TargetQuantityMode TargetQuantityMode,
    IReadOnlyList<string> RawQubesLines,
    IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    decimal WeightTolerance,
    decimal NotionalTolerance,
    decimal PnLTolerance,
    TimeSpan MaxMarkAge,
    int Version);

public sealed record QubesIntradayCycleFixtureResult(
    string CycleRunId,
    QubesRunId QubesRunId,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    int CycleCadenceMinutes,
    QubesIntradayCycleStatus CycleStatus,
    QubesIntradayCycleStatusSnapshot Status,
    QubesFxWeightsIngestionResult QubesWeights,
    ModelWeightBatch ModelWeightBatch,
    ModelWeightPromotionResult ModelWeightPromotion,
    PersistQubesWeightsResult Persistence,
    QubesTheoreticalPortfolioDiffResult TheoreticalPortfolioDiff,
    QubesTheoreticalPnlFixtureResult TheoreticalPnl,
    QubesReconciliationComparatorResult ReconciliationComparator,
    bool IsNoExternalFixture,
    bool StartedApiOrWorker,
    bool StartedBackgroundExecution,
    bool UsedLiveMarketData,
    bool CalledBrokerGateway,
    bool SubmittedOrders,
    bool CreatedExecutableOrder,
    bool MutatedLiveTradingState,
    bool RebalanceIntentsRemainNonExecutable);

public sealed class QubesIntradayCycleFixtureService(
    IFakeModelWeightGenerator modelWeightGenerator,
    IModelWeightPromotionService modelWeightPromotion,
    QubesWeightPersistenceService qubesPersistence)
{
    public async Task<QubesIntradayCycleFixtureResult> RunOneCycleAsync(
        QubesIntradayCycleFixtureRequest request,
        CancellationToken cancellationToken)
    {
        var qubes = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            request.QubesRunId,
            request.ProducedAtUtc,
            request.EffectiveAtUtc,
            15,
            request.FundCode,
            request.ModelName,
            request.PortfolioNotional,
            request.TargetQuantityMode,
            request.RawQubesLines));

        if (!qubes.Succeeded || qubes.ModelWeightBatchRequest is null)
        {
            throw new DomainRuleViolationException("R006 cycle requires a valid 15-minute Qubes weights fixture.");
        }

        var batch = await modelWeightGenerator.CreateFakeBatchAsync(qubes.ModelWeightBatchRequest, cancellationToken);
        var promotion = await modelWeightPromotion.PromoteBatchAsync(batch.Id, cancellationToken);
        if (!promotion.Succeeded)
        {
            throw new DomainRuleViolationException("R006 cycle requires successful ModelWeightBatch promotion.");
        }

        var targetWeights = qubes.NormalizedWeights
            .Select(weight =>
            {
                if (!request.InstrumentIdsBySymbol.TryGetValue(weight.Symbol, out var instrumentId))
                {
                    throw new DomainRuleViolationException($"R006 cycle missing fixture instrument id for '{weight.Symbol}'.");
                }

                return new DomainTargetWeight(promotion.ModelRunId!.Value, instrumentId, weight.Weight, weight.BloombergTicker);
            })
            .ToArray();
        var persisted = await qubesPersistence.PersistAsync(
            new PersistQubesWeightsRequest(qubes, batch, promotion, targetWeights),
            cancellationToken);
        if (persisted is null)
        {
            throw new DomainRuleViolationException("R006 cycle requires persisted Qubes audit lineage.");
        }

        var r003 = new QubesTheoreticalPortfolioDiffService().CreateDiff(new QubesTheoreticalPortfolioDiffRequest(
            qubes,
            request.InstrumentIdsBySymbol,
            CurrentPortfolioFixtureFactory.CreateFlat(request.CycleCompletedAtUtc, request.PortfolioNotional),
            request.PortfolioNotional,
            request.CycleCompletedAtUtc,
            request.WeightTolerance,
            request.NotionalTolerance,
            request.Version));
        var symbolsByInstrument = request.InstrumentIdsBySymbol.ToDictionary(x => x.Value, x => x.Key);
        var marks = CreateMarkFixtures(r003, request.ProducedAtUtc, request.EffectiveAtUtc);
        var r004 = new QubesTheoreticalPnlFixtureService().MarkAndCompute(new QubesTheoreticalPnlFixtureRequest(
            r003,
            marks.Previous,
            marks.Current,
            request.CycleCompletedAtUtc,
            request.MaxMarkAge));
        var actualPortfolio = ActualPortfolioFixtureFactory.CreateWithDeterministicDrifts(
            r003.TargetPortfolioSnapshot,
            symbolsByInstrument,
            request.CycleCompletedAtUtc);
        var actualPnl = ActualPnlFixtureFactory.Create(
            actualPortfolio,
            r004.TheoreticalPnLSnapshot,
            symbolsByInstrument,
            request.CycleCompletedAtUtc);
        var r005 = new QubesReconciliationComparatorFixtureService().Create(new QubesReconciliationComparatorRequest(
            r003,
            r004,
            persisted,
            actualPortfolio,
            actualPnl,
            symbolsByInstrument,
            request.CycleCompletedAtUtc,
            request.WeightTolerance,
            request.NotionalTolerance,
            request.PnLTolerance));
        var cycleStatus = ResolveCycleStatus(r004, r005);
        var status = new QubesIntradayCycleStatusSnapshot(
            cycleStatus,
            qubes.Succeeded ? "Accepted" : "Rejected",
            persisted.AlreadyPersisted ? "AlreadyPersisted" : "Persisted",
            r003.TargetPortfolioSnapshot.Positions.Count > 0 ? "Produced" : "Missing",
            r004.TheoreticalPnLSnapshot.Status,
            r005.FoundationReconciliationReport.HasBlockingItems ? "CompletedWithBreaks" : "Completed",
            r005.FoundationTheoreticalVsRealReport.Status,
            r003.RebalanceIntents.All(x => !x.IsExecutable) ? "NonExecutable" : "Executable",
            "NoExternalFixtureOnly");

        return new QubesIntradayCycleFixtureResult(
            request.CycleRunId,
            request.QubesRunId,
            request.CycleStartedAtUtc,
            request.CycleCompletedAtUtc,
            15,
            cycleStatus,
            status,
            qubes,
            batch,
            promotion,
            persisted,
            r003,
            r004,
            r005,
            IsNoExternalFixture: true,
            StartedApiOrWorker: false,
            StartedBackgroundExecution: false,
            UsedLiveMarketData: false,
            CalledBrokerGateway: false,
            SubmittedOrders: false,
            CreatedExecutableOrder: false,
            MutatedLiveTradingState: false,
            RebalanceIntentsRemainNonExecutable: r003.RebalanceIntents.All(x => !x.IsExecutable));
    }

    private static QubesIntradayCycleStatus ResolveCycleStatus(
        QubesTheoreticalPnlFixtureResult pnl,
        QubesReconciliationComparatorResult reconciliation)
    {
        if (!reconciliation.RebalanceIntentsRemainNonExecutable)
        {
            return QubesIntradayCycleStatus.FailedReconciliation;
        }

        return pnl.TheoreticalPnLSnapshot.Status is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark
            ? QubesIntradayCycleStatus.CompletedWithMissingMarks
            : QubesIntradayCycleStatus.CompletedNoExternal;
    }

    private static (IReadOnlyList<MarketDataMarkFixture> Previous, IReadOnlyList<MarketDataMarkFixture> Current) CreateMarkFixtures(
        QubesTheoreticalPortfolioDiffResult r003,
        DateTimeOffset previousTimestampUtc,
        DateTimeOffset currentTimestampUtc)
    {
        var ids = r003.DiffLines.ToDictionary(x => x.Symbol, x => x.InstrumentId, StringComparer.OrdinalIgnoreCase);
        return (
            [
                Mark(ids["AUDUSD"], "AUDUSD", previousTimestampUtc, 1.0000m),
                Mark(ids["EURUSD"], "EURUSD", previousTimestampUtc, 1.1000m),
                Mark(ids["GBPUSD"], "GBPUSD", previousTimestampUtc, 1.3000m),
                Mark(ids["JPYUSD"], "JPYUSD", previousTimestampUtc, 0.0067m),
                Mark(ids["NOKUSD"], "NOKUSD", previousTimestampUtc, 10.0000m)
            ],
            [
                Mark(ids["AUDUSD"], "AUDUSD", currentTimestampUtc, 1.0100m),
                Mark(ids["EURUSD"], "EURUSD", currentTimestampUtc, 1.1110m),
                Mark(ids["GBPUSD"], "GBPUSD", currentTimestampUtc, 1.2870m),
                Mark(ids["NOKUSD"], "NOKUSD", currentTimestampUtc.AddHours(-1), 10.1000m, MarketDataStalenessCategory.Stale)
            ]);
    }

    private static MarketDataMarkFixture Mark(
        InstrumentId instrumentId,
        string symbol,
        DateTimeOffset timestampUtc,
        decimal? mid,
        MarketDataStalenessCategory staleness = MarketDataStalenessCategory.Fresh)
        => new(instrumentId, symbol, timestampUtc, mid, "NoExternalR006Fixture", true, staleness);
}
