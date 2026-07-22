using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fIntradayPmsShadowOperationsTests
{
    private static readonly Guid IngestionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid[] ModelRuns = Enumerable.Range(1, 4)
        .Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:D12}")).ToArray();

    [Fact]
    public void CadenceContractHasNoImplicitOperationalValues()
    {
        Assert.Equal("pms_shadow_intraday_15m_cadence_v1", PmsShadowIntradayCadenceContract.Version);
        Assert.Equal("UTC", PmsShadowIntradayCadenceContract.TimeZone);
        Assert.Equal(15, PmsShadowIntradayCadenceContract.SlotMinutes);
        Assert.Equal(5, PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes);
        Assert.Equal(14, PmsShadowIntradayCadenceContract.MaximumFinalizationDelayMinutes);
        Assert.Equal(20, PmsShadowIntradayCadenceContract.FreshnessMinutes);
        Assert.Equal(30, PmsShadowIntradayCadenceContract.StaleMinutes);
        Assert.Equal(0, PmsShadowIntradayCadenceContract.RetryCount);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.OperationalCalendar);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.TargetClosePolicy);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.Treatments);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.OverlapPolicy);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.PreviousActivePolicy);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.LmaxIncompletePolicy);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.PolygonFailurePolicy);
        Assert.NotEmpty(PmsShadowIntradayCadenceContract.EngineFailurePolicy);
    }

    [Fact]
    public void CadenceDecisionUsesQualifiedDailyModelSchedule()
    {
        var decision = PmsShadowIntradayCadenceDecision.Authoritative;
        Assert.Equal("FRESH_DRIFT_EVERY_15_MINUTES_WITH_MODEL_SCHEDULE", decision.Mode);
        Assert.Equal(["INFX7", "INFX8", "INFX9", "INFX10"], decision.Strategies);
        Assert.Contains("TIER_1_DAILY_TEST_ENV", decision.ContractualJustification);
        Assert.Contains("Every completed fifteen-minute slot", decision.TargetPositionsFrequency);
        Assert.Contains("Every completed fifteen-minute slot", decision.DriftsFrequency);
    }

    [Fact]
    public void SlotBoundariesFloorCeilingAndIdAreDeterministicUtc()
    {
        var now = Utc(2026, 7, 21, 13, 37);
        Assert.Equal(Utc(2026, 7, 21, 13, 30), PmsShadowIntradayCadenceContract.Floor(now));
        Assert.Equal(Utc(2026, 7, 21, 13, 45), PmsShadowIntradayCadenceContract.Ceiling(now));
        var slot = PmsShadowIntradayCadenceContract.ClosedSlotAt(now);
        Assert.Equal("pms-shadow-15m-20260721T1315Z", slot.SlotId);
        Assert.Equal(Utc(2026, 7, 21, 13, 15), slot.SlotStartUtc);
        Assert.Equal(Utc(2026, 7, 21, 13, 30), slot.SlotEndUtc);
        Assert.Throws<ArgumentException>(() => PmsShadowIntradayCadenceContract.Floor(
            new DateTimeOffset(2026, 7, 21, 13, 37, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public async Task FutureSlotIsNeverClaimed()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var future = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 14, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ClaimAsync(future, "a",
            Utc(2026, 7, 21, 13, 59)));
        Assert.Empty(await store.ReadAllAsync());
    }

    [Fact]
    public async Task EightConsecutiveSlotsAcrossTwoOperationalDatesAreCompletedAndQueryable()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var pipeline = new FixturePipeline();
        var scheduler = new PmsShadowIntradayScheduler(store, pipeline);
        var firstEnd = Utc(2026, 7, 21, 23, 15);
        for (var index = 0; index < 8; index++)
        {
            var tick = await scheduler.RunClosedSlotAsync(firstEnd.AddMinutes(index * 15), "coordinator-a");
            Assert.Equal(PmsShadowIntradaySlotStatus.Completed, tick.FinalStatus);
        }

        var rows = await store.ReadAllAsync();
        Assert.Equal(8, rows.Count);
        Assert.Equal(2, rows.Select(value => value.OperationalDate).Distinct().Count());
        Assert.All(rows, value => Assert.Equal("COMPLETED", value.Status));
        Assert.Equal(8, rows.Select(value => value.SlotId).Distinct().Count());
        Assert.Equal(8, pipeline.Invocations);

        var readModels = PmsShadowIntradayProjection.Build(rows, firstEnd.AddMinutes(7 * 15 + 1));
        Assert.Equal(rows[^1].SlotId, readModels.LatestIntradayShadowSlot.Slot!.SlotId);
        Assert.Equal(8, readModels.IntradayShadowSlotHistory.Slots.Count);
        Assert.Equal(288, readModels.LatestTargetPositionBySlot.Count);
        Assert.Equal(288, readModels.LatestPositionOnlyDriftBySlot.Count);
        Assert.Equal(PmsShadowIntradayFreshness.Fresh,
            readModels.SlotFreshnessAndCompleteness.Freshness);
        Assert.Empty(readModels.MissingSlotSummary.SlotIds);
    }

    [Fact]
    public async Task TwoConcurrentCoordinatorsExecuteSameSlotOnlyOnce()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var pipeline = new FixturePipeline(TimeSpan.FromMilliseconds(50));
        var scheduler = new PmsShadowIntradayScheduler(store, pipeline);
        var now = Utc(2026, 7, 21, 13, 30);
        var results = await Task.WhenAll(scheduler.RunClosedSlotAsync(now, "a"),
            scheduler.RunClosedSlotAsync(now, "b"));
        Assert.Single(results, value => value.FinalStatus == PmsShadowIntradaySlotStatus.Completed);
        Assert.Single(results, value => value.ClaimResult == PmsShadowIntradayClaimResult.OverlapRejected);
        Assert.Equal(1, pipeline.Invocations);
        Assert.Single(await store.ReadAllAsync());
    }

    [Fact]
    public async Task DuplicateTriggerReturnsCompletedWithoutSecondPipelineExecution()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var pipeline = new FixturePipeline();
        var scheduler = new PmsShadowIntradayScheduler(store, pipeline);
        var now = Utc(2026, 7, 21, 13, 30);
        Assert.Equal(PmsShadowIntradaySlotStatus.Completed,
            (await scheduler.RunClosedSlotAsync(now, "a")).FinalStatus);
        Assert.Equal(PmsShadowIntradayClaimResult.AlreadyCompleted,
            (await scheduler.RunClosedSlotAsync(now, "b")).ClaimResult);
        Assert.Equal(1, pipeline.Invocations);
    }

    [Fact]
    public async Task InterruptionBeforeIngestionFailsClosedWithoutPartialCompletion()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var scheduler = new PmsShadowIntradayScheduler(store,
            new FixturePipeline(failure: "INGESTION_FAILED"));
        var tick = await scheduler.RunClosedSlotAsync(Utc(2026, 7, 21, 13, 30), "a");
        Assert.Equal(PmsShadowIntradaySlotStatus.FailedClosed, tick.FinalStatus);
        var row = Assert.Single(await store.ReadAllAsync());
        Assert.Equal("FAILED_CLOSED", row.Status);
        Assert.Null(row.ManifestJson);
        Assert.Equal("INGESTION_FAILED", row.FailureCode);
    }

    [Fact]
    public async Task ActiveAndStaleClaimsProduceOverlapAndRecoveryAlerts()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
        Assert.Equal(PmsShadowIntradayClaimResult.Claimed,
            (await store.ClaimAsync(slot, "a", slot.SlotEndUtc)).Result);
        var overlap = await store.ClaimAsync(slot, "b", slot.SlotEndUtc.AddMinutes(1));
        Assert.Equal(PmsShadowIntradayClaimResult.OverlapRejected, overlap.Result);
        Assert.Contains(overlap.Alerts, value => value.Code == "SLOT_OVERLAP_REJECTED");
        var recovery = await store.ClaimAsync(slot, "b", slot.SlotEndUtc.AddMinutes(31));
        Assert.Equal(PmsShadowIntradayClaimResult.RestartRecoveryRequired, recovery.Result);
        Assert.Contains(recovery.Alerts, value => value.Code == "RESTART_RECOVERY_REQUIRED");
    }

    [Fact]
    public async Task MissingStaleAndFailedClosedNeverReplaceLatestCompletedSlot()
    {
        var store = new InMemoryPmsShadowIntradaySlotStore();
        var pipeline = new FixturePipeline();
        var scheduler = new PmsShadowIntradayScheduler(store, pipeline);
        var first = Utc(2026, 7, 21, 13, 30);
        await scheduler.RunClosedSlotAsync(first, "a");
        var failedSlot = PmsShadowIntradayCadenceContract.WindowEnding(first.AddMinutes(30));
        await store.ClaimAsync(failedSlot, "b", failedSlot.SlotEndUtc);
        await store.FailClosedAsync(failedSlot.SlotId, "b", "LMAX_GAP_UNFILLED", failedSlot.SlotEndUtc);

        var projection = PmsShadowIntradayProjection.Build(await store.ReadAllAsync(), first.AddMinutes(45));
        Assert.Equal(PmsShadowIntradayCadenceContract.WindowEnding(first).SlotId,
            projection.LatestIntradayShadowSlot.Slot!.SlotId);
        Assert.Equal(PmsShadowIntradayFreshness.Stale,
            projection.SlotFreshnessAndCompleteness.Freshness);
        Assert.Contains(PmsShadowIntradayCadenceContract.WindowEnding(first.AddMinutes(15)).SlotId,
            projection.MissingSlotSummary.SlotIds);
        Assert.Contains(failedSlot.SlotId, projection.FailedClosedSlotSummary.SlotIds);
        Assert.Contains(projection.Alerts, value => value.Code == "INTRADAY_SLOT_MISSING");
        Assert.Contains(projection.Alerts, value => value.Code == "INTRADAY_SLOT_STALE");
        Assert.Contains(projection.Alerts, value => value.Code == "INTRADAY_SLOT_FAILED_CLOSED");
    }

    [Theory]
    [InlineData("lmax", "LMAX_GAP_UNFILLED")]
    [InlineData("polygon", "POLYGON_SOURCE_CONFLICT")]
    [InlineData("handoff", "SLOT_HANDOFF_NOT_FINALIZED")]
    [InlineData("order", "NO_ORDER_INVARIANT_VIOLATION")]
    [InlineData("ingestion", "INGESTION_FAILED")]
    public void IncompleteOrUnsafeManifestFailsClosedDeterministically(string mutation, string issue)
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
        var manifest = Manifest(slot);
        manifest = mutation switch
        {
            "lmax" => manifest with { LmaxGapCount = 1, LmaxGapIds = ["EURGBP@13:30"],
                PolygonCallCount = 0, PolygonFilledGapIds = [] },
            "polygon" => manifest with { PolygonCallCount = 1,
                PolygonFilledGapIds = ["AUDJPY@13:30"] },
            "handoff" => manifest with { Finalized = false },
            "order" => manifest with { NoOrderCounters = manifest.NoOrderCounters with { OrderCount = 1 } },
            "ingestion" => manifest with { IngestionStatus = "FAILED_CLOSED" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var validation = PmsShadowIntradayManifestValidation.Validate(manifest);
        Assert.False(validation.IsValid);
        Assert.Contains(issue, validation.Issues);
    }

    [Fact]
    public void NoOrderAndPrimarySourceContractIsExplicit()
    {
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
        var manifest = Manifest(slot);
        Assert.True(PmsShadowIntradayManifestValidation.Validate(manifest).IsValid);
        Assert.True(manifest.NoOrderCounters.IsValid);
        Assert.Equal(0, manifest.PolygonCallCount);
        Assert.Equal(PmsShadowStateContract.BrokerAdjustedBlocker, manifest.BrokerAdjustedDriftBlocker);
        Assert.Equal(PmsShadowStateContract.WorkingLeavesUnavailable,
            PmsShadowIntradayCadenceContract.WorkingLeavesStatus);
    }

    private static PmsShadowIntradaySlotManifest Manifest(PmsShadowIntradaySlotWindow slot) => new(
        slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, slot.OperationalDate,
        $"lmax-{slot.SlotId}", Hash('a'), 0, [], 0, [], false,
        Enumerable.Range(10, 4).Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:D12}")).ToArray(),
        [], ModelRuns,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["INFX7"] = Hash('7'), ["INFX8"] = Hash('8'),
            ["INFX9"] = Hash('9'), ["INFX10"] = Hash('a')
        },
        288, 288, PmsShadowStateContract.BrokerAdjustedBlocker, Hash('b'),
        "arch6b-daily-tier1-20260721T130346Z-422530a8", IngestionId,
        "ALREADY_APPLIED_IDENTICAL", new Dictionary<string, int> { ["ingestions"] = 1,
            ["target_positions"] = 288, ["position_only_drifts"] = 288 },
        PmsShadowIntradayFreshness.Fresh, PmsShadowIntradayNoOrderCounters.Zero,
        true, slot.SlotEndUtc.AddMinutes(1));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static string Hash(char value) => new(value, 64);

    private sealed class FixturePipeline(TimeSpan? delay = null, string? failure = null)
        : IPmsShadowIntradaySlotPipeline
    {
        private int invocations;
        public int Invocations => invocations;

        public async Task<PmsShadowIntradaySlotManifest> ExecuteAsync(PmsShadowIntradaySlotWindow slot,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocations);
            if (delay is { } value) await Task.Delay(value, cancellationToken);
            if (failure is not null) throw new InvalidOperationException(failure);
            return Manifest(slot);
        }
    }
}
