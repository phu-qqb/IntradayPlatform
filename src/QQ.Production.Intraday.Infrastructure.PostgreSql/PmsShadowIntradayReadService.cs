namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public interface IPmsShadowIntradayReadService
{
    Task<PmsShadowIntradayReadModels> GetAsync(DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class EfPmsShadowIntradayReadService(IPmsShadowIntradaySlotStore slotStore,
    IPmsShadowOperationalReadService sessionReads) : IPmsShadowIntradayReadService
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

        var session = await sessionReads.GetSessionAsync(latest.SourceSessionId,
            new(latest.OperationalDate,
                TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.FreshnessMinutes)),
            nowUtc, cancellationToken);
        if (session is null)
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
                    Blockers = ["SOURCE_SESSION_READ_MODEL_MISSING"]
                },
                Alerts = [.. slots.Alerts, .. alerts]
            };
        }

        if (session.TargetPositions.Count != slots.LatestTargetPositionBySlot.Count ||
            session.PositionOnlyDrifts.Count != slots.LatestPositionOnlyDriftBySlot.Count)
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
                    Blockers = ["SLOT_FACT_ROW_COUNT_MISMATCH"]
                },
                Alerts = [.. slots.Alerts, .. alerts]
            };
        }

        return slots with
        {
            LatestTargetPositionBySlot = slots.LatestTargetPositionBySlot with
                { Positions = session.TargetPositions },
            LatestPositionOnlyDriftBySlot = slots.LatestPositionOnlyDriftBySlot with
                { Drifts = session.PositionOnlyDrifts }
        };
    }
}
