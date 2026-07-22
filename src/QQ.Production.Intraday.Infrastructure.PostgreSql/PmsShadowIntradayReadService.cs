namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public interface IPmsShadowIntradayReadService
{
    Task<PmsShadowIntradayReadModels> GetAsync(DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class EfPmsShadowIntradayReadService(IPmsShadowIntradaySlotStore slotStore,
    IPmsShadowOperationalReadService sessionReads,
    IPmsShadowIntradayEconomicProjectionStore? economicStore = null) : IPmsShadowIntradayReadService
{
    public async Task<PmsShadowIntradayReadModels> GetAsync(DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await slotStore.ReadAllAsync(cancellationToken);
        var slots = PmsShadowIntradayProjection.Build(rows, nowUtc);
        var latest = slots.LatestIntradayShadowSlot.Slot;
        if (latest?.SourceSessionId is null ||
            slots.SlotFreshnessAndCompleteness.Freshness != PmsShadowIntradayFreshness.Fresh)
            return slots;

        if (economicStore is not null)
            return await ProjectEconomicRevisionAsync(slots, latest, nowUtc, cancellationToken);

        return await ProjectLegacySessionAsync(slots, latest, nowUtc, cancellationToken);
    }

    private async Task<PmsShadowIntradayReadModels> ProjectEconomicRevisionAsync(
        PmsShadowIntradayReadModels slots, PmsShadowIntradaySlotRow latest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var projections = (await economicStore!.ReadAllAsync(cancellationToken))
            .OrderBy(value => value.SlotEndUtc)
            .ThenBy(value => value.RevisionNumber)
            .ThenBy(value => value.ProjectionRevisionId)
            .ToArray();
        var projection = projections.LastOrDefault(value => value.SlotId == latest.SlotId &&
            value.Status == "COMPLETED" && value.Qualifying && value.NoOrder);
        if (projection is null)
            return Incomplete(slots, latest, nowUtc, "SLOT_ECONOMIC_PROJECTION_MISSING");

        var targets = projection.TargetPositions
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal)
            .Select(value => new LatestTargetPositionReadModel(value.StrategyId, value.SecurityId,
                value.TargetBaseQuantity, value.DecisionPrice, value.TargetCloseUtc,
                projection.AccountSnapshotId, projection.MarketDataSnapshotId, value.ModelRunId,
                value.InputSha256, value.OutputSha256, value.CoreCommitId)).ToArray();
        var drifts = projection.PositionOnlyDrifts
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal)
            .Select(value => new LatestPositionOnlyDriftReadModel(value.StrategyId, value.SecurityId,
                value.CurrentBaseQuantity, value.TargetBaseQuantity, value.Delta, value.AsOfUtc,
                "COMPLETED", value.ModelRunId)).ToArray();
        if (targets.Length != 288 || drifts.Length != 288 ||
            targets.Select(value => (value.StrategyId, value.SecurityId)).Distinct().Count() != 288 ||
            drifts.Select(value => (value.StrategyId, value.SecurityId)).Distinct().Count() != 288)
            return Incomplete(slots, latest, nowUtc, "SLOT_ECONOMIC_FACT_ROW_COUNT_MISMATCH");

        var history = projections.Select(value => new IntradayEconomicProjectionRevisionSummary(
            value.ProjectionRevisionId, value.RevisionNumber, value.SlotId, value.RawCaptureSha256,
            value.MarketDataSnapshotSha256, value.InputSha256, value.TargetPositionsSha256,
            value.DriftsSha256, value.ManifestSha256, value.SupersedesSlotManifestSha256,
            value.TargetPositions.Count, value.PositionOnlyDrifts.Count, value.Qualifying,
            value.NoOrder, value.CompletedAtUtc)).ToArray();
        return slots with
        {
            LatestTargetPositionBySlot = new(latest.SlotId, targets.Length, targets),
            LatestPositionOnlyDriftBySlot = new(latest.SlotId, drifts.Length, drifts),
            SlotLineageSummary = slots.SlotLineageSummary with
            {
                EconomicRevisionManifestSha256 = projection.ManifestSha256,
                MarketDataSnapshotSha256 = projection.MarketDataSnapshotSha256,
                SupersedesSlotManifestSha256 = projection.SupersedesSlotManifestSha256
            },
            EconomicProjectionHistory = history
        };
    }

    private async Task<PmsShadowIntradayReadModels> ProjectLegacySessionAsync(
        PmsShadowIntradayReadModels slots, PmsShadowIntradaySlotRow latest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var session = await sessionReads.GetSessionAsync(latest.SourceSessionId!,
            new(latest.OperationalDate,
                TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.FreshnessMinutes)),
            nowUtc, cancellationToken);
        if (session is null)
            return Incomplete(slots, latest, nowUtc, "SOURCE_SESSION_READ_MODEL_MISSING");
        if (session.TargetPositions.Count != slots.LatestTargetPositionBySlot.Count ||
            session.PositionOnlyDrifts.Count != slots.LatestPositionOnlyDriftBySlot.Count)
            return Incomplete(slots, latest, nowUtc, "SLOT_FACT_ROW_COUNT_MISMATCH");
        return slots with
        {
            LatestTargetPositionBySlot = slots.LatestTargetPositionBySlot with
                { Positions = session.TargetPositions },
            LatestPositionOnlyDriftBySlot = slots.LatestPositionOnlyDriftBySlot with
                { Drifts = session.PositionOnlyDrifts }
        };
    }

    private static PmsShadowIntradayReadModels Incomplete(PmsShadowIntradayReadModels slots,
        PmsShadowIntradaySlotRow latest, DateTimeOffset nowUtc, string blocker)
    {
        var alerts = PmsShadowIntradayAlertPolicy.ForIssues(latest.SlotId,
            latest.OperationalDate, nowUtc, latest.ManifestSha256 ?? new string('0', 64),
            ["INTRADAY_SLOT_INCOMPLETE"]);
        return slots with
        {
            SlotFreshnessAndCompleteness = slots.SlotFreshnessAndCompleteness with
            {
                Freshness = PmsShadowIntradayFreshness.Incomplete,
                Complete = false,
                Blockers = [blocker]
            },
            Alerts = [.. slots.Alerts, .. alerts]
        };
    }
}
