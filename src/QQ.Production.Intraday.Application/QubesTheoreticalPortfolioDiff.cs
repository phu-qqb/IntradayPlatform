using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;
using FoundationTargetWeight = QQ.Production.Intraday.Domain.PmsEmsOmsFoundation.TargetWeight;

namespace QQ.Production.Intraday.Application;

public enum TheoreticalPortfolioDiffCategory
{
    InSync,
    EnterTarget,
    IncreaseTarget,
    DecreaseTarget,
    ExitTarget
}

public sealed record TheoreticalPortfolioDiffLine(
    InstrumentId InstrumentId,
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal DeltaWeight,
    decimal? CurrentNotional,
    decimal? TargetNotional,
    decimal? DeltaNotional,
    TheoreticalPortfolioDiffCategory Category);

public sealed record NonExecutableRebalanceIntentLine(
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

public sealed record QubesTheoreticalPortfolioDiffRequest(
    QubesFxWeightsIngestionResult QubesWeights,
    IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
    PortfolioSnapshot CurrentPortfolio,
    decimal PortfolioNotional,
    DateTimeOffset CreatedAtUtc,
    decimal WeightTolerance,
    decimal NotionalTolerance,
    int Version);

public sealed record QubesTheoreticalPortfolioDiffResult(
    QubesRunId QubesRunId,
    ModelWeightSourceSystem SourceSystem,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    int RawInputRowCount,
    int NormalizedOutputRowCount,
    PortfolioSnapshot CurrentPortfolioSnapshot,
    PortfolioSnapshot TargetPortfolioSnapshot,
    IReadOnlyList<TheoreticalPortfolioDiffLine> DiffLines,
    IReadOnlyList<NonExecutableRebalanceIntentLine> RebalanceIntents,
    RebalanceIntent ExistingNonExecutableIntentEnvelope,
    bool UsedCurrentPortfolioFixture,
    bool UsedLiveBrokerState,
    bool CreatedExecutableOrder,
    bool CalledBrokerGateway,
    bool UsesExistingModelWeightBatchMapping);

public static class CurrentPortfolioFixtureFactory
{
    public static PortfolioSnapshot CreateFlat(DateTimeOffset snapshotTimestampUtc, decimal portfolioNotional)
        => new(
            snapshotTimestampUtc,
            PortfolioStateSource.Simulated,
            portfolioNotional,
            [],
            [new CashComponent("USD", portfolioNotional)]);
}

public sealed class QubesTheoreticalPortfolioDiffService
{
    private static readonly IntentStatus[] NonExecutableStatuses =
    [
        IntentStatus.TheoreticalOnly,
        IntentStatus.NotExecutable,
        IntentStatus.BlockedNoOMS
    ];

    public QubesTheoreticalPortfolioDiffResult CreateDiff(QubesTheoreticalPortfolioDiffRequest request)
    {
        if (!request.QubesWeights.Succeeded)
        {
            throw new DomainRuleViolationException("Qubes weights must be successfully normalized before theoretical diff creation.");
        }

        if (request.QubesWeights.SourceSystem != ModelWeightSourceSystem.Qubes)
        {
            throw new DomainRuleViolationException("R003 theoretical diff requires Qubes model weights.");
        }

        if (request.QubesWeights.CadenceMinutes != 15)
        {
            throw new DomainRuleViolationException("R003 theoretical diff requires 15-minute Qubes cadence.");
        }

        if (request.QubesWeights.ModelWeightBatchRequest is null)
        {
            throw new DomainRuleViolationException("R003 theoretical diff requires the R002 ModelWeightBatch mapping request.");
        }

        var symbolByInstrument = request.InstrumentIdsBySymbol.ToDictionary(x => x.Value, x => x.Key);
        var targetBatch = new TargetWeightsBatch(
            request.QubesWeights.QubesRunId,
            request.QubesWeights.ProducedAtUtc,
            request.QubesWeights.EffectiveAtUtc,
            TargetWeightsCadence.FifteenMinutes,
            request.QubesWeights.NormalizedWeights.Select(weight =>
            {
                if (!request.InstrumentIdsBySymbol.TryGetValue(weight.Symbol, out var instrumentId))
                {
                    throw new DomainRuleViolationException($"No fixture instrument id exists for normalized Qubes symbol '{weight.Symbol}'.");
                }

                return new FoundationTargetWeight(
                    instrumentId,
                    weight.Weight,
                    ModelWeightSourceSystem.Qubes.ToString(),
                    request.Version,
                    TargetWeightsValidationStatus.Accepted);
            }).ToArray(),
            ModelWeightSourceSystem.Qubes.ToString(),
            request.Version,
            TargetWeightsValidationStatus.Accepted);

        var target = new TargetPortfolioSnapshotFactory().Create(targetBatch, request.PortfolioNotional, request.CreatedAtUtc);
        var current = request.CurrentPortfolio;
        var diffLines = BuildDiffLines(current, target, symbolByInstrument, request.WeightTolerance);
        var existingIntent = new RebalanceIntentCalculator().Calculate(current, target, request.CreatedAtUtc, request.NotionalTolerance);
        var intents = existingIntent.Lines.Select(line =>
            new NonExecutableRebalanceIntentLine(
                line.InstrumentId,
                symbolByInstrument.TryGetValue(line.InstrumentId, out var symbol) ? symbol : line.InstrumentId.Value.ToString("N"),
                line.CurrentWeight,
                line.TargetWeight,
                line.DeltaWeight,
                line.CurrentNotional,
                line.TargetNotional,
                line.DeltaNotional,
                line.IntentSide,
                NonExecutableStatuses))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new QubesTheoreticalPortfolioDiffResult(
            request.QubesWeights.QubesRunId,
            request.QubesWeights.SourceSystem,
            request.QubesWeights.ProducedAtUtc,
            request.QubesWeights.EffectiveAtUtc,
            request.QubesWeights.CadenceMinutes,
            request.QubesWeights.RawInputRowCount,
            request.QubesWeights.NormalizedOutputRowCount,
            current,
            target,
            diffLines,
            intents,
            existingIntent,
            UsedCurrentPortfolioFixture: current.StateSource == PortfolioStateSource.Simulated,
            UsedLiveBrokerState: false,
            CreatedExecutableOrder: false,
            CalledBrokerGateway: false,
            UsesExistingModelWeightBatchMapping: true);
    }

    private static IReadOnlyList<TheoreticalPortfolioDiffLine> BuildDiffLines(
        PortfolioSnapshot current,
        PortfolioSnapshot target,
        IReadOnlyDictionary<InstrumentId, string> symbolByInstrument,
        decimal weightTolerance)
    {
        var currentByInstrument = current.Positions.ToDictionary(x => x.InstrumentId);
        var targetByInstrument = target.Positions.ToDictionary(x => x.InstrumentId);
        var instruments = currentByInstrument.Keys.Concat(targetByInstrument.Keys).Distinct();

        return instruments.Select(instrumentId =>
            {
                currentByInstrument.TryGetValue(instrumentId, out var currentPosition);
                targetByInstrument.TryGetValue(instrumentId, out var targetPosition);

                var currentWeight = currentPosition?.WeightExposure ?? 0m;
                var targetWeight = targetPosition?.WeightExposure ?? 0m;
                var deltaWeight = targetWeight - currentWeight;
                var currentNotional = currentPosition?.NotionalExposure ?? 0m;
                var targetNotional = targetPosition?.NotionalExposure ?? 0m;

                return new TheoreticalPortfolioDiffLine(
                    instrumentId,
                    symbolByInstrument.TryGetValue(instrumentId, out var symbol) ? symbol : instrumentId.Value.ToString("N"),
                    currentWeight,
                    targetWeight,
                    deltaWeight,
                    currentNotional,
                    targetNotional,
                    targetNotional - currentNotional,
                    Classify(currentWeight, targetWeight, deltaWeight, weightTolerance));
            })
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static TheoreticalPortfolioDiffCategory Classify(decimal currentWeight, decimal targetWeight, decimal deltaWeight, decimal weightTolerance)
    {
        if (Math.Abs(deltaWeight) <= weightTolerance)
        {
            return TheoreticalPortfolioDiffCategory.InSync;
        }

        if (Math.Abs(currentWeight) <= weightTolerance && Math.Abs(targetWeight) > weightTolerance)
        {
            return TheoreticalPortfolioDiffCategory.EnterTarget;
        }

        if (Math.Abs(targetWeight) <= weightTolerance && Math.Abs(currentWeight) > weightTolerance)
        {
            return TheoreticalPortfolioDiffCategory.ExitTarget;
        }

        return deltaWeight > 0m
            ? TheoreticalPortfolioDiffCategory.IncreaseTarget
            : TheoreticalPortfolioDiffCategory.DecreaseTarget;
    }
}
