using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fEconomicRefreshTests
{
    [Fact]
    public void FreshSlotPricesRecalculateCanonicalTargetsAndDriftsWhileReusingDailyModels()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var source = Source(plan);
        var end = PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value);
        var first = new PmsShadowIntradayEconomicProjectionBuilder().Build(
            Capture(PmsShadowIntradayCadenceContract.WindowEnding(end), source, 1m, 'a'), source, Hash('c'));
        var second = new PmsShadowIntradayEconomicProjectionBuilder().Build(
            Capture(PmsShadowIntradayCadenceContract.WindowEnding(end.AddMinutes(15)), source, 1.01m, 'b'),
            source, Hash('d'));

        Assert.Equal(288, first.TargetPositions.Count);
        Assert.Equal(288, first.PositionOnlyDrifts.Count);
        Assert.Equal(source.Models.Select(value => value.ModelRunId).Order(), first.ReusedModelRunIds.Order());
        Assert.NotEqual(first.MarketDataSnapshotSha256, second.MarketDataSnapshotSha256);
        Assert.NotEqual(first.InputSha256, second.InputSha256);
        Assert.NotEqual(first.TargetPositionsSha256, second.TargetPositionsSha256);
        Assert.NotEqual(first.DriftsSha256, second.DriftsSha256);
        Assert.All(first.PositionOnlyDrifts, value =>
            Assert.Equal(value.TargetBaseQuantity - value.CurrentBaseQuantity, value.Delta));
        Assert.All(first.TargetPositions, value => Assert.Equal(40, value.CoreCommitId.Length));
        Assert.True(first.Qualifying);
        Assert.True(first.NoOrder);
    }

    [Fact]
    public async Task LatestReadModelUsesQualifyingEconomicRevisionAndPreservesSupersededLineage()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var source = Source(plan);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value));
        var projection = new PmsShadowIntradayEconomicProjectionBuilder().Build(
            Capture(slot, source, 1m, 'a'), source, Hash('c'));
        var slots = new InMemoryPmsShadowIntradaySlotStore();
        await slots.ClaimAsync(slot, "economic-test", slot.SlotEndUtc);
        await slots.CompleteAsync(slot.SlotId, "economic-test", Manifest(slot, plan));

        var result = await new EfPmsShadowIntradayReadService(slots, new MustNotReadDailySession(),
            new ProjectionStore(projection)).GetAsync(slot.SlotEndUtc.AddMinutes(2));

        Assert.Equal(288, result.LatestTargetPositionBySlot.Positions!.Count);
        Assert.Equal(288, result.LatestPositionOnlyDriftBySlot.Drifts!.Count);
        Assert.Equal(projection.MarketDataSnapshotSha256,
            result.SlotLineageSummary.MarketDataSnapshotSha256);
        Assert.Equal(projection.SupersedesSlotManifestSha256,
            result.SlotLineageSummary.SupersedesSlotManifestSha256);
        Assert.Single(result.EconomicProjectionHistory!);
        Assert.True(result.SlotFreshnessAndCompleteness.Complete);
    }

    [Fact]
    public async Task MissingEconomicRevisionFailsClosedInsteadOfReloadingDailyTargets()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value));
        var slots = new InMemoryPmsShadowIntradaySlotStore();
        await slots.ClaimAsync(slot, "economic-test", slot.SlotEndUtc);
        await slots.CompleteAsync(slot.SlotId, "economic-test", Manifest(slot, plan));

        var result = await new EfPmsShadowIntradayReadService(slots, new MustNotReadDailySession(),
            new ProjectionStore()).GetAsync(slot.SlotEndUtc.AddMinutes(2));

        Assert.Equal(PmsShadowIntradayFreshness.Incomplete,
            result.SlotFreshnessAndCompleteness.Freshness);
        Assert.Equal(["SLOT_ECONOMIC_PROJECTION_MISSING"],
            result.SlotFreshnessAndCompleteness.Blockers);
    }

    [Fact]
    public void PostgreSqlFirstImportHasNoSupersededManifest()
    {
        var reader = typeof(EfPmsShadowIntradayEconomicProjectionStore).GetMethod(
            "ReadOptionalManifestSha", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(reader);
        Assert.Null(reader.Invoke(null, [null]));
        Assert.Null(reader.Invoke(null, [DBNull.Value]));
        Assert.Equal(Hash('a'), reader.Invoke(null, [Hash('a')]));
        var invalid = Assert.Throws<TargetInvocationException>(() => reader.Invoke(null, [42]));
        Assert.IsType<InvalidDataException>(invalid.InnerException);
        Assert.Equal("INVALID_SLOT_MANIFEST_SHA", invalid.InnerException.Message);
    }

    [Fact]
    public void MigrationIsAdditiveAppendOnlyAndDownRemovesOnlyRevisionTables()
    {
        using var context = new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript(PmsShadowStateContract.IntradayMigrationId,
            PmsShadowStateContract.IntradayEconomicRevisionMigrationId);
        var down = migrator.GenerateScript(PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
            PmsShadowStateContract.IntradayMigrationId);

        Assert.Contains("CREATE TABLE pms_shadow.intraday_projection_revisions", up);
        Assert.Contains("CREATE TABLE pms_shadow.intraday_market_data_observations", up);
        Assert.Contains("CREATE TABLE pms_shadow.intraday_target_positions", up);
        Assert.Contains("CREATE TABLE pms_shadow.intraday_position_only_drifts", up);
        Assert.Contains("supersedes_slot_manifest_sha256", up);
        Assert.Contains("external_completion_status character varying(32) NOT NULL", up);
        Assert.Contains("decision_price = round((bid + ask) / 2, 12)", up);
        Assert.Contains("calculated_at_utc timestamp with time zone NOT NULL", up);
        Assert.Contains("CHECK (qualifying)", up);
        Assert.Contains("CHECK (no_order)", up);
        Assert.DoesNotContain("DELETE FROM", up, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE pms_shadow.intraday_slots", up, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP TABLE IF EXISTS pms_shadow.intraday_projection_revisions", down);
        Assert.DoesNotContain("DROP TABLE IF EXISTS pms_shadow.intraday_slots", down);
        Assert.DoesNotContain("DROP SCHEMA", down, StringComparison.OrdinalIgnoreCase);
    }

    private static PmsShadowEconomicSource Source(PmsShadowPersistencePlan plan)
    {
        var weights = plan.TargetWeights.GroupBy(value => value.ModelRunId)
            .ToDictionary(group => group.Key, group => group.Select(value =>
                new PmsShadowEconomicWeight(value.InstrumentId, value.SecurityId, value.Weight)).ToArray());
        var persistedPositions = plan.PositionSnapshotLines.ToDictionary(
            value => value.InstrumentId, value => value.CurrentBaseQuantity);
        var explicitPositions = plan.TargetWeights.Select(value => value.InstrumentId)
            .Distinct().ToDictionary(value => value,
                value => persistedPositions.GetValueOrDefault(value));
        return new(plan.Ingestion.IngestionId, plan.Ingestion.SourceSessionId,
            plan.AccountSnapshot.AccountSnapshotId, plan.AccountSnapshot.NavOrEquity,
            plan.PositionSnapshot.PositionSnapshotId, plan.PositionSnapshot.AsOfUtc,
            plan.AccountSnapshot.Authority, explicitPositions,
            plan.SecurityMappings.OrderBy(value => value.SecurityId, StringComparer.Ordinal)
                .Select((value, index) => new PmsShadowEconomicMapping(value.InstrumentId,
                    value.VenueId, value.VenueInstrumentId, value.SecurityId, TestPair(index),
                    "lmax-" + TestPair(index), value.QuantityMultiplier, value.QuantityIncrement,
                    value.PriceIncrement)).ToArray(),
            plan.ModelRuns.Select(value => new PmsShadowEconomicModel(value.ModelRunId,
                value.QubesInputSnapshotId, value.StrategyId, value.TargetCloseUtc, value.AsOfUtc,
                value.OutputSha256, value.CoreMasterCommitId, weights[value.ModelRunId])).ToArray());
    }

    private static PmsShadowRealSlotCapture Capture(PmsShadowIntradaySlotWindow slot,
        PmsShadowEconomicSource source, decimal multiplier, char hash)
    {
        var currencies = source.Mappings.SelectMany(value =>
            new[] { Pair(value.Symbol).Base, Pair(value.Symbol).Quote })
            .Where(value => value != "USD").Distinct(StringComparer.Ordinal).Order().ToArray();
        var bbo = currencies.Select((currency, index) => new PmsShadowRealSlotBbo(
            currency + "USD", "lmax-" + currency, (1m + index / 100m) * multiplier,
            (1.001m + index / 100m) * multiplier, slot.SlotEndUtc.AddSeconds(-1),
            slot.SlotEndUtc.AddMilliseconds(-500))).ToArray();
        return new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, "fixture", "fixture.jsonl",
            Hash(hash), bbo, true, 0, true, true);
    }

    private static string TestPair(int index)
    {
        var currencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD",
            "NOK", "SEK", "DKK", "SGD", "HKD" };
        return (from baseCurrency in currencies
                from quoteCurrency in currencies
                where baseCurrency != quoteCurrency
                select baseCurrency + quoteCurrency).ElementAt(index);
    }

    private static (string Base, string Quote) Pair(string symbol)
    {
        var value = new string(symbol.ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray());
        return (value[..3], value[3..]);
    }

    private static PmsShadowIntradaySlotManifest Manifest(PmsShadowIntradaySlotWindow slot,
        PmsShadowPersistencePlan plan) => new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
        slot.OperationalDate, "lmax-capture", Hash('a'), 0, [], 0, [], false,
        plan.QubesInputSnapshots.Select(value => value.SnapshotId).ToArray(), [],
        plan.ModelRuns.Select(value => value.ModelRunId).ToArray(),
        plan.ModelRuns.ToDictionary(value => value.StrategyId, value => value.OutputSha256),
        288, 288, PmsShadowStateContract.BrokerAdjustedBlocker, Hash('b'),
        plan.Ingestion.SourceSessionId, plan.Ingestion.IngestionId, "ALREADY_APPLIED_IDENTICAL",
        EfPmsShadowSessionImportStore.ExpectedRowCounts(plan), PmsShadowIntradayFreshness.Fresh,
        PmsShadowIntradayNoOrderCounters.Zero, true, slot.SlotEndUtc.AddMinutes(1));

    private static string Hash(char value) => new(value, 64);

    private sealed class ProjectionStore(params PmsShadowIntradayEconomicProjection[] values)
        : IPmsShadowIntradayEconomicProjectionStore
    {
        public Task<IReadOnlyList<PmsShadowIntradayEconomicProjection>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PmsShadowIntradayEconomicProjection>>(values);
        public Task<string?> LoadSupersededManifestShaAsync(string slotId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PmsShadowEconomicSource> LoadSourceAsync(
            string sourceSessionId, DateTimeOffset slotStartUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PmsShadowEconomicApplyOutcome> ApplyAsync(PmsShadowIntradayEconomicProjection projection,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MustNotReadDailySession : IPmsShadowOperationalReadService
    {
        public Task<PmsShadowOperationalReadSnapshot?> GetLatestAsync(PmsShadowFreshnessPolicy policy,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("D1_READ_MODEL_RELOAD_FORBIDDEN");
        public Task<PmsShadowOperationalReadSnapshot?> GetSessionAsync(string sourceSessionId,
            PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("D1_READ_MODEL_RELOAD_FORBIDDEN");
    }
}
