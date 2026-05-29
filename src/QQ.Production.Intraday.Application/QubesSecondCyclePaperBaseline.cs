using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;
using DomainTargetWeight = QQ.Production.Intraday.Domain.TargetWeight;

namespace QQ.Production.Intraday.Application;

public enum PaperBaselineSecondCycleStatus
{
    CompletedNoExternal,
    CompletedWithMissingMarks,
    RejectedInvalidBaseline,
    InconclusiveSafe
}

public sealed record PaperBaselineInputLine(
    string CurrencyOrSymbol,
    InstrumentId InstrumentId,
    string QuantityCurrency,
    decimal CurrentPaperQuantity,
    decimal FixtureMid,
    decimal CurrentPaperNotional,
    decimal CurrentPaperWeight,
    string SourceLineageReference);

public sealed record PaperBaselineCycleInput(
    string PaperBaselineInputId,
    PaperLedgerStateArchiveId PaperLedgerStateArchiveId,
    PaperLedgerStateId PaperLedgerStateId,
    PaperLedgerCommitId PaperLedgerCommitId,
    string BaselineSource,
    string NextCycleBaselineType,
    bool BaselineIsProduction,
    bool BaselineIsBroker,
    bool BaselineIsLiveTrading,
    bool PaperOnly,
    bool NoExternal,
    bool FixtureState,
    bool R025PaperBaselineMutated,
    IReadOnlyList<PaperBaselineInputLine> Lines);

public sealed record SecondCycleTargetPortfolioLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TargetWeight,
    decimal TargetNotional,
    string ModelWeightBatchId,
    string ModelRunId,
    string TargetWeightLinkageStatus);

public sealed record SecondCycleCurrentPaperBaselineLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentPaperQuantity,
    string QuantityCurrency,
    decimal CurrentPaperNotional,
    decimal CurrentPaperWeight,
    bool FromR025PaperLedgerFixture,
    bool FromBrokerState,
    bool FromProductionLedgerState);

public sealed record SecondCycleTargetVsCurrentDiffLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal? CurrentNotional,
    decimal? TargetNotional,
    decimal? DeltaNotional,
    TheoreticalPortfolioDiffCategory Category);

public sealed record SecondCycleTheoreticalPnlLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TargetNotional,
    decimal UnrealizedPnl,
    PnLComputationStatus PnLStatus,
    MarkAvailabilityStatus MarkAvailabilityStatus,
    string Reason);

public sealed record SecondCycleReconciliationLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TargetWeight,
    decimal CurrentPaperWeight,
    decimal WeightDifference,
    decimal TargetNotional,
    decimal CurrentPaperNotional,
    decimal NotionalDifference,
    QubesReconciliationLineStatus Status,
    ReconciliationSeverity Severity);

public sealed record SecondCycleTheoreticalVsRealLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal TheoreticalWeight,
    decimal CurrentPaperWeight,
    decimal WeightDifference,
    decimal TheoreticalNotional,
    decimal CurrentPaperNotional,
    decimal NotionalDifference,
    decimal TheoreticalPnl,
    decimal CurrentPaperPnl,
    decimal PnlDifference,
    QubesReconciliationLineStatus Status);

public sealed record SecondCycleRebalanceIntentLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal? CurrentNotional,
    decimal? TargetNotional,
    decimal? DeltaNotional,
    IntentSide IntentSide,
    IReadOnlyList<IntentStatus> IntentStatuses)
{
    public bool IsExecutable => false;
}

public sealed record PaperBaselineSecondCycleSummary(
    int RawQubesRowCount,
    int NormalizedQubesRowCount,
    int CurrentPaperBaselineLineCount,
    int TargetPortfolioLineCount,
    int DiffLineCount,
    int RebalanceIntentCount,
    bool CurrentPaperBaselineIsFlatZero,
    bool UsedR025PaperLedgerBaseline,
    bool PaperLedgerStateCommittedOrMutated,
    int LivePositionMutationCount,
    int BrokerPositionMutationCount,
    int ProductionLedgerMutationCount,
    int TradingStateMutationCount,
    int OrderCount,
    int FillCount,
    int ExecutionReportCount,
    int BrokerRouteCount,
    string SafetyStatus);

public sealed record PaperBaselineSecondCycleResult(
    string SecondCycleRunId,
    QubesRunId QubesRunId,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    int CycleCadenceMinutes,
    PaperBaselineSecondCycleStatus CycleStatus,
    PaperBaselineCycleInput PaperBaselineInput,
    QubesFxWeightsIngestionResult QubesWeights,
    ModelWeightBatch ModelWeightBatch,
    ModelWeightPromotionResult ModelWeightPromotion,
    PersistQubesWeightsResult QubesPersistence,
    PortfolioSnapshot CurrentPaperBaseline,
    QubesTheoreticalPortfolioDiffResult TheoreticalPortfolioDiff,
    QubesTheoreticalPnlFixtureResult TheoreticalPnl,
    QubesReconciliationComparatorResult Reconciliation,
    IReadOnlyList<SecondCycleTargetPortfolioLine> TargetPortfolioLines,
    IReadOnlyList<SecondCycleCurrentPaperBaselineLine> CurrentPaperBaselineLines,
    IReadOnlyList<SecondCycleTargetVsCurrentDiffLine> TargetVsCurrentDiffLines,
    IReadOnlyList<SecondCycleTheoreticalPnlLine> TheoreticalPnlLines,
    IReadOnlyList<SecondCycleReconciliationLine> ReconciliationLines,
    IReadOnlyList<SecondCycleTheoreticalVsRealLine> TheoreticalVsRealLines,
    IReadOnlyList<SecondCycleRebalanceIntentLine> RebalanceIntents,
    PaperBaselineSecondCycleSummary Summary,
    bool IsNoExternalFixture,
    bool StartedApiOrWorker,
    bool StartedBackgroundExecution,
    bool UsedLiveMarketData,
    bool CalledBrokerGateway,
    bool SubmittedOrders,
    bool CreatedExecutableOrder,
    bool MutatedLiveTradingState,
    bool MutatedLivePositionState,
    bool MutatedBrokerPositionState,
    bool MutatedProductionLedgerState,
    bool MutatedPaperLedgerState,
    bool MutatedR025PaperBaseline,
    bool CreatedOrderState,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool CreatedBrokerRoute,
    bool RebalanceIntentsRemainNonExecutable);

public interface IPaperBaselineSecondCycleRepository
{
    Task<PaperBaselineSecondCycleResult?> GetByRunIdAsync(string secondCycleRunId, CancellationToken cancellationToken);
    Task AddAsync(PaperBaselineSecondCycleResult result, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperBaselineSecondCycleRepository : IPaperBaselineSecondCycleRepository
{
    private readonly List<PaperBaselineSecondCycleResult> results = [];

    public Task<PaperBaselineSecondCycleResult?> GetByRunIdAsync(string secondCycleRunId, CancellationToken cancellationToken)
        => Task.FromResult(results.FirstOrDefault(x => x.SecondCycleRunId.Equals(secondCycleRunId, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(PaperBaselineSecondCycleResult result, CancellationToken cancellationToken)
    {
        if (results.Any(x => x.SecondCycleRunId.Equals(result.SecondCycleRunId, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        results.Add(result);
        return Task.CompletedTask;
    }
}

public sealed class PaperBaselineSecondCycleService(
    IFakeModelWeightGenerator modelWeightGenerator,
    IModelWeightPromotionService modelWeightPromotion,
    QubesWeightPersistenceService qubesPersistence,
    IPaperBaselineSecondCycleRepository repository)
{
    private static readonly IReadOnlyDictionary<string, decimal> PaperBaselineFixtureMids =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUDUSD"] = 1.0100m,
            ["EURUSD"] = 1.1110m,
            ["GBPUSD"] = 1.2870m
        };

    public async Task<PaperBaselineSecondCycleResult> RunSecondCycleAsync(
        PaperNextCycleBaselineReference baselineReference,
        PaperLedgerStateArchiveRecord paperLedgerStateArchive,
        string secondCycleRunId,
        QubesRunId qubesRunId,
        DateTimeOffset producedAtUtc,
        DateTimeOffset effectiveAtUtc,
        DateTimeOffset cycleStartedAtUtc,
        DateTimeOffset cycleCompletedAtUtc,
        string fundCode,
        string modelName,
        decimal portfolioNotional,
        IReadOnlyList<string> rawQubesLines,
        IReadOnlyDictionary<string, InstrumentId> instrumentIdsBySymbol,
        decimal weightTolerance,
        decimal notionalTolerance,
        decimal pnlTolerance,
        TimeSpan maxMarkAge,
        int version,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByRunIdAsync(secondCycleRunId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        ValidateBaseline(baselineReference, paperLedgerStateArchive);
        var baselineInput = CreateBaselineInput(baselineReference, paperLedgerStateArchive, instrumentIdsBySymbol, portfolioNotional);
        var currentPortfolio = CreateCurrentPortfolio(baselineInput, cycleStartedAtUtc, portfolioNotional);
        var qubes = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            qubesRunId,
            producedAtUtc,
            effectiveAtUtc,
            15,
            fundCode,
            modelName,
            portfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            rawQubesLines));

        if (!qubes.Succeeded || qubes.ModelWeightBatchRequest is null)
        {
            throw new DomainRuleViolationException("R026 second cycle requires a valid no-external 15-minute Qubes fixture.");
        }

        var batch = await modelWeightGenerator.CreateFakeBatchAsync(qubes.ModelWeightBatchRequest, cancellationToken);
        var promotion = await modelWeightPromotion.PromoteBatchAsync(batch.Id, cancellationToken);
        if (!promotion.Succeeded)
        {
            throw new DomainRuleViolationException("R026 second cycle requires successful no-external ModelWeightBatch promotion.");
        }

        var targetWeights = qubes.NormalizedWeights
            .Select(weight =>
            {
                if (!instrumentIdsBySymbol.TryGetValue(weight.Symbol, out var instrumentId))
                {
                    throw new DomainRuleViolationException($"R026 second cycle missing fixture instrument id for '{weight.Symbol}'.");
                }

                return new DomainTargetWeight(promotion.ModelRunId!.Value, instrumentId, weight.Weight, weight.BloombergTicker);
            })
            .ToArray();
        var persisted = await qubesPersistence.PersistAsync(
            new PersistQubesWeightsRequest(qubes, batch, promotion, targetWeights),
            cancellationToken);
        if (persisted is null)
        {
            throw new DomainRuleViolationException("R026 second cycle requires persisted Qubes lineage.");
        }

        var diff = new QubesTheoreticalPortfolioDiffService().CreateDiff(new QubesTheoreticalPortfolioDiffRequest(
            qubes,
            instrumentIdsBySymbol,
            currentPortfolio,
            portfolioNotional,
            cycleCompletedAtUtc,
            weightTolerance,
            notionalTolerance,
            version));
        var symbolsByInstrument = instrumentIdsBySymbol.ToDictionary(x => x.Value, x => x.Key);
        var marks = CreateMarkFixtures(diff, producedAtUtc, effectiveAtUtc);
        var pnl = new QubesTheoreticalPnlFixtureService().MarkAndCompute(new QubesTheoreticalPnlFixtureRequest(
            diff,
            marks.Previous,
            marks.Current,
            cycleCompletedAtUtc,
            maxMarkAge));
        var currentPnl = ActualPnlFixtureFactory.Create(
            currentPortfolio,
            pnl.TheoreticalPnLSnapshot,
            symbolsByInstrument,
            cycleCompletedAtUtc);
        var reconciliation = new QubesReconciliationComparatorFixtureService().Create(new QubesReconciliationComparatorRequest(
            diff,
            pnl,
            persisted,
            currentPortfolio,
            currentPnl,
            symbolsByInstrument,
            cycleCompletedAtUtc,
            weightTolerance,
            notionalTolerance,
            pnlTolerance));
        var result = CreateResult(
            secondCycleRunId,
            qubesRunId,
            cycleStartedAtUtc,
            cycleCompletedAtUtc,
            baselineInput,
            qubes,
            batch,
            promotion,
            persisted,
            currentPortfolio,
            diff,
            pnl,
            reconciliation);

        await repository.AddAsync(result, cancellationToken);
        return result;
    }

    private static void ValidateBaseline(
        PaperNextCycleBaselineReference baselineReference,
        PaperLedgerStateArchiveRecord archive)
    {
        if (!baselineReference.NextNoExternalCycleMayUsePaperLedgerFixtureAsCurrentState() ||
            baselineReference.BaselineIsProduction ||
            baselineReference.BaselineIsBroker ||
            baselineReference.BaselineIsLiveTrading ||
            archive.ArchiveStatus is not PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState ||
            archive.PaperLedgerMutatedAgain ||
            archive.NewCycleRan ||
            archive.NewQubesBatchIngested)
        {
            throw new DomainRuleViolationException("R026 requires the R025 no-external paper ledger fixture baseline.");
        }
    }

    private static PaperBaselineCycleInput CreateBaselineInput(
        PaperNextCycleBaselineReference baselineReference,
        PaperLedgerStateArchiveRecord archive,
        IReadOnlyDictionary<string, InstrumentId> instrumentIdsBySymbol,
        decimal portfolioNotional)
    {
        var lines = archive.Lines
            .OrderBy(x => x.CurrencyOrSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(line =>
            {
                if (!instrumentIdsBySymbol.TryGetValue(line.CurrencyOrSymbol, out var instrumentId))
                {
                    throw new DomainRuleViolationException($"R026 paper baseline missing fixture instrument id for '{line.CurrencyOrSymbol}'.");
                }

                if (!PaperBaselineFixtureMids.TryGetValue(line.CurrencyOrSymbol, out var mid))
                {
                    throw new DomainRuleViolationException($"R026 paper baseline missing no-external fixture mark for '{line.CurrencyOrSymbol}'.");
                }

                var notional = line.EndingPaperQuantity * mid;
                return new PaperBaselineInputLine(
                    line.CurrencyOrSymbol,
                    instrumentId,
                    line.QuantityCurrency,
                    line.EndingPaperQuantity,
                    mid,
                    notional,
                    notional / portfolioNotional,
                    line.SourceLineageReference);
            })
            .ToArray();

        return new PaperBaselineCycleInput(
            "r026-paper-baseline-input",
            archive.PaperLedgerStateArchiveId,
            archive.PaperLedgerStateId,
            archive.PaperLedgerCommitId,
            baselineReference.BaselineSource,
            baselineReference.NextCycleBaselineType,
            baselineReference.BaselineIsProduction,
            baselineReference.BaselineIsBroker,
            baselineReference.BaselineIsLiveTrading,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            R025PaperBaselineMutated: false,
            lines);
    }

    private static PortfolioSnapshot CreateCurrentPortfolio(
        PaperBaselineCycleInput baseline,
        DateTimeOffset snapshotTimestampUtc,
        decimal portfolioNotional)
    {
        var positions = baseline.Lines.Select(line =>
            new PortfolioPosition(
                line.InstrumentId,
                line.CurrentPaperQuantity,
                line.FixtureMid,
                line.CurrentPaperNotional,
                line.CurrentPaperWeight,
                new Dictionary<string, decimal> { ["USD"] = line.CurrentPaperNotional }))
            .ToArray();

        return new PortfolioSnapshot(
            snapshotTimestampUtc,
            PortfolioStateSource.Simulated,
            portfolioNotional,
            positions,
            [new CashComponent("USD", portfolioNotional - positions.Sum(x => x.NotionalExposure))]);
    }

    private static PaperBaselineSecondCycleResult CreateResult(
        string secondCycleRunId,
        QubesRunId qubesRunId,
        DateTimeOffset cycleStartedAtUtc,
        DateTimeOffset cycleCompletedAtUtc,
        PaperBaselineCycleInput baselineInput,
        QubesFxWeightsIngestionResult qubes,
        ModelWeightBatch batch,
        ModelWeightPromotionResult promotion,
        PersistQubesWeightsResult persisted,
        PortfolioSnapshot currentPortfolio,
        QubesTheoreticalPortfolioDiffResult diff,
        QubesTheoreticalPnlFixtureResult pnl,
        QubesReconciliationComparatorResult reconciliation)
    {
        var targetLines = diff.TargetPortfolioSnapshot.Positions
            .Select(position =>
            {
                var symbol = diff.DiffLines.First(x => x.InstrumentId == position.InstrumentId).Symbol;
                return new SecondCycleTargetPortfolioLine(
                    position.InstrumentId,
                    symbol,
                    position.WeightExposure,
                    position.NotionalExposure,
                    batch.Id.Value.ToString("N"),
                    promotion.ModelRunId?.Value.ToString("N") ?? string.Empty,
                    "ModelWeightBatchModelRunTargetWeightLinked");
            })
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var baselineByInstrument = baselineInput.Lines.ToDictionary(x => x.InstrumentId);
        var currentLines = currentPortfolio.Positions
            .Select(position =>
            {
                var baseline = baselineByInstrument[position.InstrumentId];
                return new SecondCycleCurrentPaperBaselineLine(
                    position.InstrumentId,
                    baseline.CurrencyOrSymbol,
                    position.Quantity,
                    baseline.QuantityCurrency,
                    position.NotionalExposure,
                    position.WeightExposure,
                    FromR025PaperLedgerFixture: true,
                    FromBrokerState: false,
                    FromProductionLedgerState: false);
            })
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var diffLines = diff.DiffLines
            .Select(line => new SecondCycleTargetVsCurrentDiffLine(
                line.InstrumentId,
                line.Symbol,
                line.CurrentWeight,
                line.TargetWeight,
                line.DeltaWeight,
                line.CurrentNotional,
                line.TargetNotional,
                line.DeltaNotional,
                line.Category))
            .ToArray();
        var pnlLines = pnl.InstrumentDetails
            .Select(line => new SecondCycleTheoreticalPnlLine(
                line.InstrumentId,
                line.Symbol,
                line.NotionalExposure,
                line.UnrealizedPnL,
                line.PnLStatus,
                line.MarkAvailabilityStatus,
                line.Reason))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reconciliationLines = reconciliation.ReconciliationLines
            .Select(line => new SecondCycleReconciliationLine(
                line.InstrumentId,
                line.Symbol,
                line.TargetWeight,
                line.ActualWeight,
                line.WeightDifference,
                line.TargetNotional,
                line.ActualNotional,
                line.NotionalDifference,
                line.Status,
                line.Severity))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var theoreticalVsReal = reconciliation.ComparatorLines
            .Select(line => new SecondCycleTheoreticalVsRealLine(
                line.InstrumentId,
                line.Symbol,
                line.TheoreticalWeight,
                line.ActualWeight,
                line.WeightDifference,
                line.TheoreticalNotional,
                line.ActualNotional,
                line.NotionalDifference,
                line.TheoreticalPnL,
                line.ActualPnL,
                line.PnLDifference,
                line.Status))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var intents = diff.RebalanceIntents
            .Select(intent => new SecondCycleRebalanceIntentLine(
                intent.InstrumentId,
                intent.Symbol,
                intent.CurrentWeight,
                intent.TargetWeight,
                intent.DeltaWeight,
                intent.CurrentNotional,
                intent.TargetNotional,
                intent.DeltaNotional,
                intent.IntentSide,
                intent.IntentStatuses))
            .ToArray();
        var missingMarks = pnl.TheoreticalPnLSnapshot.Status is PnLComputationStatus.MissingMark or PnLComputationStatus.StaleMark;
        var summary = new PaperBaselineSecondCycleSummary(
            qubes.RawInputRowCount,
            qubes.NormalizedOutputRowCount,
            currentLines.Length,
            targetLines.Length,
            diffLines.Length,
            intents.Length,
            CurrentPaperBaselineIsFlatZero: currentLines.All(x => x.CurrentPaperQuantity == 0m),
            UsedR025PaperLedgerBaseline: true,
            PaperLedgerStateCommittedOrMutated: false,
            LivePositionMutationCount: 0,
            BrokerPositionMutationCount: 0,
            ProductionLedgerMutationCount: 0,
            TradingStateMutationCount: 0,
            OrderCount: 0,
            FillCount: 0,
            ExecutionReportCount: 0,
            BrokerRouteCount: 0,
            SafetyStatus: "NoExternalSecondCyclePaperBaselineOnly");

        return new PaperBaselineSecondCycleResult(
            secondCycleRunId,
            qubesRunId,
            cycleStartedAtUtc,
            cycleCompletedAtUtc,
            15,
            missingMarks ? PaperBaselineSecondCycleStatus.CompletedWithMissingMarks : PaperBaselineSecondCycleStatus.CompletedNoExternal,
            baselineInput,
            qubes,
            batch,
            promotion,
            persisted,
            currentPortfolio,
            diff,
            pnl,
            reconciliation,
            targetLines,
            currentLines,
            diffLines,
            pnlLines,
            reconciliationLines,
            theoreticalVsReal,
            intents,
            summary,
            IsNoExternalFixture: true,
            StartedApiOrWorker: false,
            StartedBackgroundExecution: false,
            UsedLiveMarketData: false,
            CalledBrokerGateway: false,
            SubmittedOrders: false,
            CreatedExecutableOrder: false,
            MutatedLiveTradingState: false,
            MutatedLivePositionState: false,
            MutatedBrokerPositionState: false,
            MutatedProductionLedgerState: false,
            MutatedPaperLedgerState: false,
            MutatedR025PaperBaseline: false,
            CreatedOrderState: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            CreatedBrokerRoute: false,
            RebalanceIntentsRemainNonExecutable: intents.All(x => !x.IsExecutable));
    }

    private static (IReadOnlyList<MarketDataMarkFixture> Previous, IReadOnlyList<MarketDataMarkFixture> Current) CreateMarkFixtures(
        QubesTheoreticalPortfolioDiffResult diff,
        DateTimeOffset previousTimestampUtc,
        DateTimeOffset currentTimestampUtc)
    {
        var ids = diff.DiffLines.ToDictionary(x => x.Symbol, x => x.InstrumentId, StringComparer.OrdinalIgnoreCase);
        var previous = new List<MarketDataMarkFixture>();
        var current = new List<MarketDataMarkFixture>();

        AddMarkIfPresent(ids, previous, "AUDUSD", previousTimestampUtc, 1.0000m);
        AddMarkIfPresent(ids, previous, "EURUSD", previousTimestampUtc, 1.1000m);
        AddMarkIfPresent(ids, previous, "GBPUSD", previousTimestampUtc, 1.3000m);
        AddMarkIfPresent(ids, current, "AUDUSD", currentTimestampUtc, 1.0100m);
        AddMarkIfPresent(ids, current, "EURUSD", currentTimestampUtc, 1.1110m);
        AddMarkIfPresent(ids, current, "GBPUSD", currentTimestampUtc, 1.2870m);

        return (previous, current);
    }

    private static void AddMarkIfPresent(
        IReadOnlyDictionary<string, InstrumentId> ids,
        List<MarketDataMarkFixture> marks,
        string symbol,
        DateTimeOffset timestampUtc,
        decimal fixtureMid)
    {
        if (!ids.TryGetValue(symbol, out var instrumentId))
        {
            return;
        }

        marks.Add(new MarketDataMarkFixture(
            instrumentId,
            symbol,
            timestampUtc,
            fixtureMid,
            "NoExternalR026Fixture",
            IsNoExternalFixture: true,
            MarketDataStalenessCategory.Fresh));
    }
}

internal static class PaperNextCycleBaselineReferenceExtensions
{
    public static bool NextNoExternalCycleMayUsePaperLedgerFixtureAsCurrentState(this PaperNextCycleBaselineReference reference)
        => reference.NextCycleBaselineType.Equals("PaperLedgerFixture", StringComparison.OrdinalIgnoreCase) &&
           reference.PaperOnly &&
           reference.NoExternal &&
           reference.FixtureState &&
           !reference.NewCycleRan &&
           !reference.NewQubesBatchIngested &&
           !reference.PaperLedgerMutatedAgain;
}
