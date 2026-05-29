using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum MarkAvailabilityStatus
{
    Available,
    MissingCurrentMark,
    MissingPreviousMark,
    StaleCurrentMark,
    StalePreviousMark,
    InconclusiveSafe
}

public sealed record MarketDataMarkFixture(
    InstrumentId InstrumentId,
    string Symbol,
    DateTimeOffset MarkTimestampUtc,
    decimal? FixtureMid,
    string FixtureSource,
    bool IsNoExternalFixture,
    MarketDataStalenessCategory StalenessCategory);

public sealed record MarkedPortfolioPosition(
    InstrumentId InstrumentId,
    string Symbol,
    decimal WeightExposure,
    decimal NotionalExposure,
    decimal? PreviousFixtureMid,
    decimal? CurrentFixtureMid,
    decimal? MarkedNotionalExposure,
    decimal UnrealizedPnL,
    MarkAvailabilityStatus MarkAvailabilityStatus,
    PnLComputationStatus PnLStatus,
    string Reason);

public sealed record TheoreticalMarkedPortfolioSnapshot(
    DateTimeOffset SnapshotTimestampUtc,
    QubesRunId QubesRunId,
    ModelWeightSourceSystem SourceSystem,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    PortfolioSnapshot SourceTargetPortfolioSnapshot,
    IReadOnlyList<MarkedPortfolioPosition> Positions,
    PortfolioPnL PortfolioPnL,
    PnLComputationStatus Status);

public sealed record QubesTheoreticalPnlFixtureRequest(
    QubesTheoreticalPortfolioDiffResult R003Diff,
    IReadOnlyList<MarketDataMarkFixture> PreviousMarks,
    IReadOnlyList<MarketDataMarkFixture> CurrentMarks,
    DateTimeOffset CreatedAtUtc,
    TimeSpan MaxMarkAge);

public sealed record QubesTheoreticalPnlFixtureResult(
    QubesRunId QubesRunId,
    ModelWeightSourceSystem SourceSystem,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    TheoreticalMarkedPortfolioSnapshot MarkedPortfolioSnapshot,
    PnLSnapshot TheoreticalPnLSnapshot,
    IReadOnlyList<MarkedPortfolioPosition> InstrumentDetails,
    bool UsedNoExternalMarkFixture,
    bool UsedLiveBrokerMarketData,
    bool CalledBrokerGateway,
    bool CreatedExecutableOrder,
    bool RebalanceIntentsRemainNonExecutable);

public sealed class QubesTheoreticalPnlFixtureService
{
    public QubesTheoreticalPnlFixtureResult MarkAndCompute(QubesTheoreticalPnlFixtureRequest request)
    {
        if (request.R003Diff.SourceSystem != ModelWeightSourceSystem.Qubes)
        {
            throw new DomainRuleViolationException("R004 theoretical PnL requires Qubes source metadata.");
        }

        if (request.R003Diff.CadenceMinutes != 15)
        {
            throw new DomainRuleViolationException("R004 theoretical PnL requires 15-minute Qubes cadence.");
        }

        if (request.PreviousMarks.Concat(request.CurrentMarks).Any(x => !x.IsNoExternalFixture))
        {
            throw new DomainRuleViolationException("R004 marks must be explicit no-external fixtures.");
        }

        var previousByInstrument = request.PreviousMarks.ToDictionary(x => x.InstrumentId);
        var currentByInstrument = request.CurrentMarks.ToDictionary(x => x.InstrumentId);
        var markedPositions = new List<MarkedPortfolioPosition>();
        var pnlRows = new List<InstrumentPnL>();

        foreach (var position in request.R003Diff.TargetPortfolioSnapshot.Positions)
        {
            var symbol = request.R003Diff.DiffLines.FirstOrDefault(x => x.InstrumentId == position.InstrumentId)?.Symbol
                ?? position.InstrumentId.Value.ToString("N");
            previousByInstrument.TryGetValue(position.InstrumentId, out var previous);
            currentByInstrument.TryGetValue(position.InstrumentId, out var current);

            var detail = MarkPosition(position, symbol, previous, current, request.CreatedAtUtc, request.MaxMarkAge);
            markedPositions.Add(detail);
            pnlRows.Add(new InstrumentPnL(position.InstrumentId, detail.UnrealizedPnL, 0m, detail.PnLStatus, detail.Reason));
        }

        var computedUnrealized = pnlRows
            .Where(x => x.Status == PnLComputationStatus.Computed)
            .Sum(x => x.UnrealizedPnL);
        var portfolioStatus = ResolvePortfolioStatus(pnlRows);
        var portfolioPnl = new PortfolioPnL(computedUnrealized, 0m, computedUnrealized);
        var markedSnapshot = new TheoreticalMarkedPortfolioSnapshot(
            request.CreatedAtUtc,
            request.R003Diff.QubesRunId,
            request.R003Diff.SourceSystem,
            request.R003Diff.ProducedAtUtc,
            request.R003Diff.EffectiveAtUtc,
            request.R003Diff.CadenceMinutes,
            request.R003Diff.TargetPortfolioSnapshot,
            markedPositions,
            portfolioPnl,
            portfolioStatus);
        var pnlSnapshot = new PnLSnapshot(
            request.CreatedAtUtc,
            PnLSource.Theoretical,
            portfolioPnl,
            pnlRows,
            portfolioStatus);

        return new QubesTheoreticalPnlFixtureResult(
            request.R003Diff.QubesRunId,
            request.R003Diff.SourceSystem,
            request.R003Diff.ProducedAtUtc,
            request.R003Diff.EffectiveAtUtc,
            request.R003Diff.CadenceMinutes,
            markedSnapshot,
            pnlSnapshot,
            markedPositions,
            UsedNoExternalMarkFixture: true,
            UsedLiveBrokerMarketData: false,
            CalledBrokerGateway: false,
            CreatedExecutableOrder: false,
            RebalanceIntentsRemainNonExecutable: request.R003Diff.RebalanceIntents.All(x => !x.IsExecutable));
    }

    private static MarkedPortfolioPosition MarkPosition(
        PortfolioPosition position,
        string symbol,
        MarketDataMarkFixture? previous,
        MarketDataMarkFixture? current,
        DateTimeOffset createdAtUtc,
        TimeSpan maxMarkAge)
    {
        if (current is null || current.FixtureMid is null)
        {
            return Missing(position, symbol, previous?.FixtureMid, null, MarkAvailabilityStatus.MissingCurrentMark, "Current no-external fixture mark is missing.");
        }

        if (previous is null || previous.FixtureMid is null)
        {
            return Missing(position, symbol, null, current.FixtureMid, MarkAvailabilityStatus.MissingPreviousMark, "Previous no-external fixture mark is missing.");
        }

        if (IsStale(current, createdAtUtc, maxMarkAge))
        {
            return Missing(position, symbol, previous.FixtureMid, current.FixtureMid, MarkAvailabilityStatus.StaleCurrentMark, "Current no-external fixture mark is stale.", PnLComputationStatus.StaleMark);
        }

        if (IsStale(previous, createdAtUtc, maxMarkAge * 2))
        {
            return Missing(position, symbol, previous.FixtureMid, current.FixtureMid, MarkAvailabilityStatus.StalePreviousMark, "Previous no-external fixture mark is stale.", PnLComputationStatus.StaleMark);
        }

        if (previous.FixtureMid.Value == 0m)
        {
            return Missing(position, symbol, previous.FixtureMid, current.FixtureMid, MarkAvailabilityStatus.InconclusiveSafe, "Previous no-external fixture mark is zero.", PnLComputationStatus.Inconclusive);
        }

        var markReturn = (current.FixtureMid.Value - previous.FixtureMid.Value) / previous.FixtureMid.Value;
        var unrealized = position.NotionalExposure * markReturn;
        return new MarkedPortfolioPosition(
            position.InstrumentId,
            symbol,
            position.WeightExposure,
            position.NotionalExposure,
            previous.FixtureMid,
            current.FixtureMid,
            position.NotionalExposure,
            unrealized,
            MarkAvailabilityStatus.Available,
            PnLComputationStatus.Computed,
            "Theoretical PnL computed from no-external previous/current fixture marks.");
    }

    private static bool IsStale(MarketDataMarkFixture mark, DateTimeOffset createdAtUtc, TimeSpan maxAge)
        => mark.StalenessCategory == MarketDataStalenessCategory.Stale || createdAtUtc - mark.MarkTimestampUtc > maxAge;

    private static MarkedPortfolioPosition Missing(
        PortfolioPosition position,
        string symbol,
        decimal? previousMid,
        decimal? currentMid,
        MarkAvailabilityStatus availability,
        string reason,
        PnLComputationStatus status = PnLComputationStatus.MissingMark)
        => new(
            position.InstrumentId,
            symbol,
            position.WeightExposure,
            position.NotionalExposure,
            previousMid,
            currentMid,
            null,
            0m,
            availability,
            status,
            reason);

    private static PnLComputationStatus ResolvePortfolioStatus(IReadOnlyList<InstrumentPnL> rows)
    {
        if (rows.Count == 0)
        {
            return PnLComputationStatus.MissingPosition;
        }

        if (rows.Any(x => x.Status == PnLComputationStatus.MissingMark))
        {
            return PnLComputationStatus.MissingMark;
        }

        if (rows.Any(x => x.Status == PnLComputationStatus.StaleMark))
        {
            return PnLComputationStatus.StaleMark;
        }

        if (rows.Any(x => x.Status == PnLComputationStatus.Inconclusive))
        {
            return PnLComputationStatus.Inconclusive;
        }

        return PnLComputationStatus.Computed;
    }
}
