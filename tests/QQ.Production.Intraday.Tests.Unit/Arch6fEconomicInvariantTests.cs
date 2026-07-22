using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fEconomicInvariantTests
{
    [Theory]
    [InlineData(0, 1.00, 'a')]
    [InlineData(1, 1.01, 'b')]
    [InlineData(2, 0.99, 'c')]
    public void EachSlotDecisionPriceComesFromItsOwnBbo(int offset, double multiplier, char hash)
    {
        var projection = Projection(offset, (decimal)multiplier, hash, 'd');
        var target = projection.TargetPositions[0];
        var market = projection.MarketData.Single(value => value.InstrumentId == target.InstrumentId);
        Assert.Equal((market.Bid + market.Ask) / 2m, target.DecisionPrice);
        Assert.Equal(projection.SlotEndUtc, target.CalculatedAtUtc);
    }

    [Fact]
    public void NewSlotStartsAtRevisionOneWithoutArtificialSupersession()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var source = Source(plan);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value));
        var projection = new PmsShadowIntradayEconomicProjectionBuilder()
            .Build(Capture(slot, source, 1m, 'a'), source, null);
        Assert.Equal(1, projection.RevisionNumber);
        Assert.Null(projection.SupersedesSlotManifestSha256);
    }

    [Fact]
    public void TargetIdsAndInputFingerprintsDifferBySlot()
    {
        var first = Projection(0, 1m, 'a', 'c');
        var second = Projection(1, 1.01m, 'b', 'd');
        Assert.Empty(first.TargetPositions.Select(value => value.TargetPositionId)
            .Intersect(second.TargetPositions.Select(value => value.TargetPositionId)));
        Assert.NotEqual(first.InputSha256, second.InputSha256);
        Assert.NotEqual(first.MarketDataSnapshotId, second.MarketDataSnapshotId);
    }

    [Fact]
    public void IdenticalBuildIsDeterministicForReplay()
    {
        var first = Projection(0, 1m, 'a', 'c');
        var replay = Projection(0, 1m, 'a', 'c');
        PmsShadowEconomicProjectionConflictGuard.RequireIdentical(first, replay);
        Assert.Equal(first.TargetPositions.ToArray(), replay.TargetPositions.ToArray());
        Assert.Equal(first.PositionOnlyDrifts.ToArray(), replay.PositionOnlyDrifts.ToArray());
    }

    [Fact]
    public void SameRevisionWithChangedEconomicsFailsClosed()
    {
        var stored = Projection(0, 1m, 'a', 'c');
        var conflicting = stored with { InputSha256 = Hash('f') };
        var failure = Assert.Throws<InvalidDataException>(() =>
            PmsShadowEconomicProjectionConflictGuard.RequireIdentical(stored, conflicting));
        Assert.Equal("FAILED_CLOSED_CONFLICT", failure.Message);
    }

    [Fact]
    public void SelectedModelsAreExposedAsReusedNotFresh()
    {
        var projection = Projection(0, 1m, 'a', 'c');
        Assert.Equal(4, projection.SelectedModelRuns.Count);
        Assert.All(projection.SelectedModelRuns, value =>
            Assert.Equal("REUSED_FINALIZED_D1_MODEL", value.Classification));
        Assert.All(projection.SelectedModelRuns, value =>
            Assert.True(value.AsOfUtc < projection.CompletedAtUtc));
    }

    [Fact]
    public void PositionSnapshotIdentityAgeAndAuthorityAreExposed()
    {
        var projection = Projection(0, 1m, 'a', 'c');
        Assert.NotEqual(Guid.Empty, projection.PositionSnapshotId);
        Assert.True(projection.PositionSnapshotAsOfUtc <= projection.SlotEndUtc);
        Assert.False(string.IsNullOrWhiteSpace(projection.PositionAuthority));
    }

    [Fact]
    public void SupersededNonqualifyingAttemptHashIsPreserved()
        => Assert.Equal(Hash('c'), Projection(0, 1m, 'a', 'c').SupersedesSlotManifestSha256);

    [Fact]
    public void RevisionCompletesWithoutExternalExecution()
    {
        var projection = Projection(0, 1m, 'a', 'c');
        Assert.Equal(PmsShadowStateContract.CompletedNoExternal, projection.ExternalCompletionStatus);
        Assert.True(projection.NoOrder);
    }

    [Fact]
    public void ProductionPathInvokesOnlyCanonicalTargetPositionCalculator()
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "PmsShadowIntradayEconomicRefresh.cs"));
        Assert.Contains("new TargetPositionCalculator()", source);
        Assert.Contains("calculator.Calculate(domainRun", source);
        Assert.DoesNotContain("TargetNotionalUsd =", source);
    }

    [Fact]
    public void PersistenceSerializesTwoCoordinatorsAndHasUniqueRevisionKeys()
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "PmsShadowIntradayEconomicRefresh.cs"));
        var migration = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Migrations", "20260722231500_AddIntradayEconomicProjectionRevisions.cs"));
        Assert.Contains("IsolationLevel.Serializable", source);
        Assert.Contains("pg_advisory_xact_lock", source);
        Assert.Contains("(\"external_completion\", projection.ExternalCompletionStatus)", source);
        Assert.Contains("UNIQUE (slot_id, revision_number)", migration);
    }

    [Theory]
    [InlineData("IVenueExecutionGateway")]
    [InlineData("ILmaxOrder")]
    [InlineData("FixSession")]
    [InlineData("Databento")]
    [InlineData("RealAccount")]
    public void CorrectedPathHasNoExternalOrOrderSurface(string forbidden)
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "PmsShadowIntradayEconomicRefresh.cs"));
        Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplayToolDeclaresAllNewExternalCountersZero()
    {
        var source = File.ReadAllText(RepositoryFile("tools",
            "QQ.Production.Intraday.Tools.Arch6fEconomicReplay", "Program.cs"));
        Assert.Contains("lmax_sessions = 0", source);
        Assert.Contains("polygon_calls = 0", source);
        Assert.Contains("gpu_invocations = 0", source);
        Assert.Contains("no_order = true", source);
    }

    [Fact]
    public void TargetCalculationTimestampIsBoundToSlotClose()
    {
        var projection = Projection(0, 1m, 'a', 'c');
        Assert.All(projection.TargetPositions,
            value => Assert.Equal(projection.SlotEndUtc, value.CalculatedAtUtc));
    }

    [Fact]
    public void DistinctInputsMayKeepSameRoundedVenueQuantity()
    {
        var first = Projection(0, 1m, 'a', 'c');
        var second = Projection(1, 1.000000001m, 'b', 'd');
        Assert.NotEqual(first.InputSha256, second.InputSha256);
        Assert.Contains(first.TargetPositions.Join(second.TargetPositions,
            left => (left.ModelRunId, left.InstrumentId),
            right => (right.ModelRunId, right.InstrumentId),
            (left, right) => (left, right)), pair =>
                pair.left.InputSha256 != pair.right.InputSha256 &&
                pair.left.TargetVenueQuantity == pair.right.TargetVenueQuantity);
    }

    [Fact]
    public void MigrationNeverRewritesOrDeletesOldSlotAttempts()
    {
        var migration = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Migrations", "20260722231500_AddIntradayEconomicProjectionRevisions.cs"));
        Assert.DoesNotContain("UPDATE pms_shadow.intraday_slots", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM pms_shadow.intraday_slots", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrokerAdjustedDriftRemainsBlocked()
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "PmsShadowIntradayEconomicRefresh.cs"));
        Assert.Contains("PmsShadowStateContract.BrokerAdjustedBlocker", source);
        Assert.Contains("broker_adjusted_calculated,working_leaves_blocker,no_order", source);
    }

    private static PmsShadowIntradayEconomicProjection Projection(int slotOffset,
        decimal multiplier, char captureHash, char supersededHash)
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var source = Source(plan);
        var end = PmsShadowIntradayCadenceContract.Floor(plan.Ingestion.CompletedAtUtc!.Value)
            .AddMinutes(slotOffset * 15);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(end);
        return new PmsShadowIntradayEconomicProjectionBuilder().Build(
            Capture(slot, source, multiplier, captureHash), source, Hash(supersededHash));
    }

    private static PmsShadowEconomicSource Source(PmsShadowPersistencePlan plan)
    {
        var weights = plan.TargetWeights.GroupBy(value => value.ModelRunId)
            .ToDictionary(group => group.Key, group => group.Select(value =>
                new PmsShadowEconomicWeight(value.InstrumentId, value.SecurityId, value.Weight)).ToArray());
        return new(plan.Ingestion.IngestionId, plan.Ingestion.SourceSessionId,
            plan.AccountSnapshot.AccountSnapshotId, plan.AccountSnapshot.NavOrEquity,
            plan.PositionSnapshot.PositionSnapshotId, plan.PositionSnapshot.AsOfUtc,
            plan.AccountSnapshot.Authority, plan.PositionSnapshotLines.ToDictionary(
                value => value.InstrumentId, value => value.CurrentBaseQuantity),
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

    private static (string Base, string Quote) Pair(string symbol) => (symbol[..3], symbol[3..]);
    private static string Hash(char value) => new(value, 64);

    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return Path.Combine([directory!.FullName, .. parts]);
    }
}
