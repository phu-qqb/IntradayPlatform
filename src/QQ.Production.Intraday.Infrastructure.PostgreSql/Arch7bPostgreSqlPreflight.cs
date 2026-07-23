using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bPostgreSqlPreflightSnapshot(
    Arch7bSelectedChildOrder ChildOrder,
    decimal CurrentKnownPosition,
    int PlatformKnownWorkingOrderCount,
    PmsArch7bQualificationRunRow? ExistingRun,
    bool OpeningSendIntentExists);

public sealed class EfArch7bPostgreSqlPreflightReader(
    IDbContextFactory<PmsShadowDbContext> contextFactory)
{
    public async Task<Arch7bPostgreSqlPreflightSnapshot> ReadAsync(
        Guid childOrderId,
        Guid qualificationRunId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (childOrderId == Guid.Empty || qualificationRunId == Guid.Empty)
            throw new InvalidOperationException("ARCH7B_PREFLIGHT_IDENTITY_MISSING");
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("ARCH7B_PREFLIGHT_TIME_NOT_UTC");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var child = await context.ShadowChildOrders.AsNoTracking()
            .SingleAsync(value => value.ChildOrderId == childOrderId, cancellationToken);
        var parent = await context.ShadowParentOrders.AsNoTracking()
            .SingleAsync(value => value.ParentOrderId == child.ParentOrderId, cancellationToken);
        var intent = await context.ShadowTradeIntents.AsNoTracking()
            .SingleAsync(value => value.TradeIntentId == parent.TradeIntentId, cancellationToken);
        var risk = await context.ShadowRiskDecisions.AsNoTracking()
            .SingleAsync(value => value.RiskDecisionId == parent.RiskDecisionId, cancellationToken);
        var sourceRun = await context.ShadowExecutionQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.EconomicRevisionId == intent.EconomicRevisionId, cancellationToken);
        var latestSourceRun = await context.ShadowExecutionQualificationRuns.AsNoTracking()
            .Where(value => value.Status == "COMPLETED")
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.QualificationRunId)
            .FirstOrDefaultAsync(cancellationToken);
        var existingRun = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleOrDefaultAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);
        var openingSendIntentExists = await context.Arch7bOrderSendLedger.AsNoTracking()
            .AnyAsync(value =>
                value.QualificationRunId == qualificationRunId &&
                value.MessageType == "D" &&
                value.LifecycleRole == "OPEN", cancellationToken);

        var marketSnapshot = await context.MarketDataSnapshots.AsNoTracking()
            .SingleAsync(value =>
                value.IngestionId == intent.IngestionId &&
                value.SnapshotSha256 == intent.MarketDataSnapshotSha256, cancellationToken);
        var marketObservation = await context.MarketDataObservations.AsNoTracking()
            .SingleAsync(value =>
                value.MarketDataSnapshotId == marketSnapshot.MarketDataSnapshotId &&
                value.SecurityId == intent.SecurityId, cancellationToken);
        var positionSnapshot = await context.PositionSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == intent.IngestionId, cancellationToken);
        var positionLine = await context.PositionSnapshotLines.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.PositionSnapshotId == positionSnapshot.PositionSnapshotId &&
                value.SecurityId == intent.SecurityId, cancellationToken);
        var sourcePosition = positionLine?.CurrentBaseQuantity ??
            (positionSnapshot.EmptyStateWasExplicitlyObserved &&
             !positionSnapshot.EmptyStateWasInferred
                ? 0m
                : throw new InvalidOperationException("ARCH7B_POSITION_SOURCE_MISSING"));
        var subsequentOtherRunPosition = await context.Arch7bPositionLedgerEvents.AsNoTracking()
            .Where(value =>
                value.QualificationRunId != qualificationRunId &&
                value.SecurityId == intent.SecurityId &&
                value.EventTimeUtc > intent.CreatedAtUtc)
            .SumAsync(value => value.SignedQuantity, cancellationToken);
        var platformKnownWorkingOrderCount = await CountOtherKnownWorkingOrdersAsync(
            context,
            qualificationRunId,
            cancellationToken);

        var slotRows = await new EfPmsShadowIntradaySlotStore(contextFactory)
            .ReadAllAsync(cancellationToken);
        var intraday = PmsShadowIntradayProjection.Build(slotRows, nowUtc);
        var latestSlot = intraday.LatestIntradayShadowSlot.Slot;
        var sourceFresh = latestSlot?.SlotId == intent.SlotId &&
                          intraday.SlotFreshnessAndCompleteness.Freshness ==
                          PmsShadowIntradayFreshness.Fresh &&
                          intraday.SlotFreshnessAndCompleteness.Complete;
        var sourceSuperseded = latestSourceRun is null ||
                               latestSourceRun.EconomicRevisionId != intent.EconomicRevisionId;
        var lmaxMarketData = marketObservation.ProjectionMethod is
            "LMAX_DIRECT" or "LMAX_DIRECT_INVERTED";
        var sourceCompleted =
            sourceRun.Status == "COMPLETED" &&
            sourceRun.NoFixLogon &&
            sourceRun.NoBrokerSend &&
            sourceRun.NoFill &&
            sourceRun.NoPositionLedgerEvent &&
            sourceRun.IntentCount == 7 &&
            sourceRun.RiskDecisionCount == 7 &&
            sourceRun.ParentOrderCount == 7 &&
            sourceRun.ChildOrderCount == 7 &&
            risk.SourceComplete &&
            risk.NoOrderInvariant &&
            !risk.BrokerSendAllowed &&
            !parent.RouteAllowed &&
            !child.BrokerSendAllowed;
        var lineageComplete =
            IsSha256(intent.MarketDataSnapshotSha256) &&
            IsSha256(intent.SourceLineageSha256) &&
            IsSha256(intent.LineageSha256) &&
            IsSha256(intent.PlanSha256) &&
            intent.PlanSha256 == risk.PlanSha256 &&
            intent.PlanSha256 == parent.PlanSha256 &&
            intent.PlanSha256 == child.PlanSha256 &&
            intent.PlanSha256 == sourceRun.PlanSha256 &&
            intent.SourceLineageSha256 == sourceRun.SourceLineageSha256 &&
            intent.CurrentQuantity == sourcePosition &&
            marketSnapshot.SnapshotSha256 == intent.MarketDataSnapshotSha256;

        var selected = new Arch7bSelectedChildOrder(
            intent.TradeIntentId,
            parent.ParentOrderId,
            child.ChildOrderId,
            child.ClientOrderId,
            intent.SourceSessionId,
            intent.SlotId,
            intent.EconomicRevisionId,
            intent.EconomicRevisionNumber,
            intent.MarketDataSnapshotSha256,
            intent.SourceLineageSha256,
            intent.PlanSha256,
            intent.Environment,
            intent.AccountScope,
            parent.Symbol,
            intent.SecurityId,
            intent.SecurityIdSource,
            child.Side,
            child.Quantity,
            intent.TargetCloseUtc,
            intent.EffectiveFromUtc,
            intent.DeadlineUtc,
            intent.Classification,
            parent.Status,
            child.Status,
            LatestQualifyingRevision: !sourceSuperseded,
            sourceCompleted,
            sourceFresh,
            sourceSuperseded,
            lmaxMarketData,
            PolygonOrderPrice: !lmaxMarketData,
            lineageComplete);

        return new(
            selected,
            sourcePosition + subsequentOtherRunPosition,
            platformKnownWorkingOrderCount,
            existingRun,
            openingSendIntentExists);
    }

    private static async Task<int> CountOtherKnownWorkingOrdersAsync(
        PmsShadowDbContext context,
        Guid qualificationRunId,
        CancellationToken cancellationToken)
    {
        var sends = await context.Arch7bOrderSendLedger.AsNoTracking()
            .Where(value =>
                value.QualificationRunId != qualificationRunId &&
                value.MessageType == "D")
            .ToArrayAsync(cancellationToken);
        if (sends.Length == 0)
            return await context.ShadowChildOrders.AsNoTracking()
                .CountAsync(value => value.BrokerSendAllowed, cancellationToken);

        var otherRunIds = sends.Select(value => value.QualificationRunId).Distinct().ToArray();
        var reports = await context.Arch7bExecutionReports.AsNoTracking()
            .Where(value => otherRunIds.Contains(value.QualificationRunId))
            .ToArrayAsync(cancellationToken);
        var unresolved = sends.Count(send =>
        {
            var latest = reports
                .Where(value =>
                    value.QualificationRunId == send.QualificationRunId &&
                    (value.ClientOrderId == send.ClientOrderId ||
                     value.OriginalClientOrderId == send.ClientOrderId))
                .OrderBy(value => value.TransactTimeUtc)
                .ThenBy(value => value.FixSequenceNumber)
                .LastOrDefault();
            return latest is null || latest.OrderStatus is not ("2" or "4" or "8" or "C");
        });
        var routedShadowOrders = await context.ShadowChildOrders.AsNoTracking()
            .CountAsync(value => value.BrokerSendAllowed, cancellationToken);
        return unresolved + routedShadowOrders;
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);
}
