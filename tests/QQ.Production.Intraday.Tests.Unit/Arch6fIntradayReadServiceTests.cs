using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fIntradayReadServiceTests
{
    [Fact]
    public async Task LatestFreshSlotJoinsAllTargetAndDriftRowsFromItsSourceSession()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value));
        await store.ClaimAsync(slot, "a", slot.SlotEndUtc);
        await store.CompleteAsync(slot.SlotId, "a", Manifest(slot, plan));
        var projection = PmsShadowOperationalProjection.Build(plan,
            new(plan.AccountSnapshot.ReportDate, TimeSpan.FromDays(2)),
            plan.Ingestion.CompletedAtUtc.Value.AddHours(1));

        var result = await new EfPmsShadowIntradayReadService(store, new FakeReads(projection))
            .GetAsync(slot.SlotEndUtc.AddMinutes(2));
        Assert.Equal(288, result.LatestTargetPositionBySlot.Positions!.Count);
        Assert.Equal(288, result.LatestPositionOnlyDriftBySlot.Drifts!.Count);
        Assert.Equal(slot.SlotId, result.SlotLineageSummary.SlotId);
        Assert.True(result.SlotFreshnessAndCompleteness.Complete);
    }

    [Fact]
    public async Task MissingSourceSessionMakesSlotIncompleteInsteadOfCurrent()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value));
        await store.ClaimAsync(slot, "a", slot.SlotEndUtc);
        await store.CompleteAsync(slot.SlotId, "a", Manifest(slot, plan));

        var result = await new EfPmsShadowIntradayReadService(store, new FakeReads(null))
            .GetAsync(slot.SlotEndUtc.AddMinutes(2));
        Assert.Equal(PmsShadowIntradayFreshness.Incomplete,
            result.SlotFreshnessAndCompleteness.Freshness);
        Assert.Contains(result.Alerts, value => value.Code == "INTRADAY_SLOT_INCOMPLETE");
    }

    private static PmsShadowIntradaySlotManifest Manifest(PmsShadowIntradaySlotWindow slot,
        PmsShadowPersistencePlan plan) => new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
        slot.OperationalDate, "lmax-capture", Hash('a'), 0, [], 0, [], false,
        plan.QubesInputSnapshots.Select(value => value.SnapshotId).ToArray(), [],
        plan.ModelRuns.Select(value => value.ModelRunId).ToArray(),
        plan.ModelRuns.ToDictionary(value => value.StrategyId, value => value.OutputSha256,
            StringComparer.Ordinal), plan.TargetPositions.Count, plan.PositionOnlyDrifts.Count,
        PmsShadowStateContract.BrokerAdjustedBlocker, Hash('b'), plan.Ingestion.SourceSessionId,
        plan.Ingestion.IngestionId, "ALREADY_APPLIED_IDENTICAL",
        EfPmsShadowSessionImportStore.ExpectedRowCounts(plan), PmsShadowIntradayFreshness.Fresh,
        PmsShadowIntradayNoOrderCounters.Zero, true, slot.SlotEndUtc.AddMinutes(1));

    private static string Hash(char value) => new(value, 64);

    private sealed class FakeReads(PmsShadowOperationalReadSnapshot? snapshot)
        : IPmsShadowOperationalReadService
    {
        public Task<PmsShadowOperationalReadSnapshot?> GetLatestAsync(PmsShadowFreshnessPolicy policy,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task<PmsShadowOperationalReadSnapshot?> GetSessionAsync(string sourceSessionId,
            PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }
}
