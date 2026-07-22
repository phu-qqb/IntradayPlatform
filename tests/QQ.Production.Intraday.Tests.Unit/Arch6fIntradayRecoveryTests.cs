using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fIntradayRecoveryTests
{
    [Fact]
    public async Task StaleRunningSlotCanBeTakenOverAndCompletedWithoutDuplicateRow()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
        await store.ClaimAsync(slot, "coordinator-a", slot.SlotEndUtc);
        var recovered = await store.ClaimAsync(slot, "coordinator-b", slot.SlotEndUtc.AddMinutes(31));
        Assert.Equal(PmsShadowIntradayClaimResult.RestartRecoveryRequired, recovered.Result);
        await store.CompleteAsync(slot.SlotId, "coordinator-b", Manifest(slot));
        var row = Assert.Single(await store.ReadAllAsync());
        Assert.Equal("COMPLETED", row.Status);
        Assert.Equal("coordinator-b", row.CoordinatorId);
    }

    [Fact]
    public async Task CommitBeforeAcknowledgementIsRecoveredAsCompleted()
    {
        var store = new CommitThenInterruptStore();
        var scheduler = new PmsShadowIntradayScheduler(store, new Pipeline());
        var result = await scheduler.RunClosedSlotAsync(Utc(2026, 7, 21, 13, 30), "coordinator-a");
        Assert.Equal(PmsShadowIntradaySlotStatus.Completed, result.FinalStatus);
        Assert.Contains(result.Alerts, value => value.Code == "RESTART_RECOVERY_REQUIRED");
        Assert.Equal("COMPLETED", Assert.Single(await store.ReadAllAsync()).Status);
    }

    [Fact]
    public async Task LateSchedulerTickIsRecordedMissedAndDoesNotInvokePipeline()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var pipeline = new Pipeline();
        var scheduler = new PmsShadowIntradayScheduler(store, pipeline);
        var result = await scheduler.RunClosedSlotAsync(Utc(2026, 7, 21, 13, 37), "coordinator-a");
        Assert.Equal(PmsShadowIntradaySlotStatus.Missed, result.FinalStatus);
        Assert.Contains(result.Alerts, value => value.Code == "INTRADAY_SLOT_MISSING");
        Assert.Equal(0, pipeline.Invocations);
        Assert.Equal("MISSED", Assert.Single(await store.ReadAllAsync()).Status);
    }

    private static PmsShadowIntradaySlotManifest Manifest(PmsShadowIntradaySlotWindow slot)
    {
        var ids = Enumerable.Range(1, 4).Select(index =>
            Guid.Parse($"00000000-0000-0000-0000-{index:D12}")).ToArray();
        return new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, slot.OperationalDate,
            "lmax-capture", Hash('a'), 0, [], 0, [], false, ids, [], ids,
            new Dictionary<string, string>
            {
                ["INFX7"] = Hash('7'), ["INFX8"] = Hash('8'),
                ["INFX9"] = Hash('9'), ["INFX10"] = Hash('a')
            }, 288, 288, PmsShadowStateContract.BrokerAdjustedBlocker, Hash('b'),
            "arch6b-daily-tier1-20260721T130346Z-422530a8", Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "ALREADY_APPLIED_IDENTICAL", new Dictionary<string, int> { ["ingestions"] = 1 },
            PmsShadowIntradayFreshness.Fresh, PmsShadowIntradayNoOrderCounters.Zero,
            true, slot.SlotEndUtc.AddMinutes(1));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
    private static string Hash(char value) => new(value, 64);

    private sealed class Pipeline : IPmsShadowIntradaySlotPipeline
    {
        public int Invocations { get; private set; }
        public Task<PmsShadowIntradaySlotManifest> ExecuteAsync(PmsShadowIntradaySlotWindow slot,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            return Task.FromResult(Manifest(slot));
        }
    }

    private sealed class CommitThenInterruptStore : IPmsShadowIntradaySlotStore
    {
        private readonly InMemoryPmsShadowIntradaySlotStore inner = new();
        private bool interrupt = true;

        public Task<PmsShadowIntradayClaim> ClaimAsync(PmsShadowIntradaySlotWindow slot,
            string coordinatorId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            inner.ClaimAsync(slot, coordinatorId, nowUtc, cancellationToken);

        public async Task<PmsShadowIntradaySlotRow> CompleteAsync(string slotId, string coordinatorId,
            PmsShadowIntradaySlotManifest manifest, CancellationToken cancellationToken = default)
        {
            var result = await inner.CompleteAsync(slotId, coordinatorId, manifest, cancellationToken);
            if (interrupt)
            {
                interrupt = false;
                throw new InvalidOperationException("CONTROLLER_ACK_INTERRUPTED");
            }
            return result;
        }

        public Task<PmsShadowIntradaySlotRow> FailClosedAsync(string slotId, string coordinatorId,
            string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default) =>
            inner.FailClosedAsync(slotId, coordinatorId, failureCode, failedAtUtc, cancellationToken);

        public Task<PmsShadowIntradaySlotRow> RecordMissedAsync(PmsShadowIntradaySlotWindow slot,
            string reason, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default) =>
            inner.RecordMissedAsync(slot, reason, observedAtUtc, cancellationToken);

        public Task<IReadOnlyList<PmsShadowIntradaySlotRow>> ReadAllAsync(
            CancellationToken cancellationToken = default) => inner.ReadAllAsync(cancellationToken);
    }
}
