using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPmsLineageAndTemporalTests
{
    [Fact]
    public void Four_qubes_inputs_are_required_and_linked()
    {
        var plan = Plan();
        var universe = Universe(plan);
        Assert.Equal(4, universe.QubesInputs.Count);
        Assert.All(universe.Models, model =>
            Assert.Contains(universe.QubesInputs,
                input => input.SnapshotId == model.QubesInputSnapshotId));
    }

    [Fact]
    public void Missing_qubes_input_is_rejected()
    {
        var plan = Plan();
        AssertCode("ARCH7B_PMS_QUBES_INPUT_MISSING",
            () => Universe(plan, qubes: plan.QubesInputSnapshots.Skip(1).ToArray()));
    }

    [Fact]
    public void Wrong_qubes_ingestion_is_rejected()
    {
        var plan = Plan();
        var qubes = plan.QubesInputSnapshots.ToArray();
        qubes[0] = qubes[0] with { IngestionId = Guid.NewGuid() };
        AssertCode("ARCH7B_PMS_QUBES_INPUT_LINEAGE_MISMATCH",
            () => Universe(plan, qubes: qubes));
    }

    [Fact]
    public void Qubes_target_close_mismatch_is_rejected()
    {
        var plan = Plan();
        var qubes = plan.QubesInputSnapshots.ToArray();
        qubes[0] = qubes[0] with
        {
            TargetCloseUtc = qubes[0].TargetCloseUtc.AddMinutes(15)
        };
        AssertCode("ARCH7B_PMS_TARGET_CLOSE_LINEAGE_MISMATCH",
            () => Universe(plan, qubes: qubes));
    }

    [Fact]
    public void Unexpected_model_weight_is_rejected()
    {
        var plan = Plan();
        var extra = plan.TargetWeights[0] with { ModelRunId = Guid.NewGuid() };
        AssertCode("ARCH7B_PMS_UNEXPECTED_MODEL_WEIGHT",
            () => Universe(plan, weights: [.. plan.TargetWeights, extra]));
    }

    [Fact]
    public void Latest_completed_incomplete_model_set_fails_closed()
    {
        var plan = Plan();
        AssertCode("ARCH7B_LATEST_COMPLETED_PMS_INGESTION_INVALID",
            () => Universe(plan, models: plan.ModelRuns.Skip(1).ToArray()));
    }

    [Theory]
    [InlineData("symbol")]
    [InlineData("lmax")]
    [InlineData("mapping-sha")]
    public void Incomplete_mapping_identity_is_rejected(string field)
    {
        var plan = Plan();
        var mappings = plan.SecurityMappings.ToArray();
        mappings[0] = field switch
        {
            "symbol" => mappings[0] with { Symbol = "" },
            "lmax" => mappings[0] with { LmaxInstrumentId = "" },
            _ => mappings[0] with { MappingSha256 = "invalid" }
        };
        AssertCode("ARCH7B_PMS_SECURITY_MAPPING_IDENTITY_MISMATCH",
            () => Universe(plan, mappings: mappings));
    }

    [Fact]
    public void Mapping_change_changes_line_identity_and_evidence()
    {
        var plan = Plan();
        var firstUniverse = Universe(plan);
        var mappings = plan.SecurityMappings.ToArray();
        mappings[0] = mappings[0] with { MappingSha256 = Hash('9') };
        var secondUniverse = Universe(plan, mappings: mappings);
        var time = firstUniverse.LatestModelAsOfUtc.AddHours(1);
        var first = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(time), firstUniverse);
        var second = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(time), secondUniverse);
        Assert.NotEqual(first.Lines[0].PositionSnapshotLineId,
            second.Lines[0].PositionSnapshotLineId);
        Assert.NotEqual(first.Lines[0].EvidenceSha256,
            second.Lines[0].EvidenceSha256);
    }

    [Fact]
    public void Normalized_csv_contains_complete_mapping_lineage()
    {
        var plan = Plan();
        var universe = Universe(plan);
        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(universe.LatestModelAsOfUtc.AddHours(1)), universe);
        var smoke = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
        var root = Path.Combine(Path.GetTempPath(),
            "arch7b-mapping-csv-" + Guid.NewGuid().ToString("N"));
        try
        {
            Arch7bGlobalFlatOutputWriter.Write(
                root, Core(universe.LatestModelAsOfUtc.AddHours(1)),
                universe, snapshot, smoke, smoke);
            var header = File.ReadLines(Path.Combine(
                root, "normalized-position-lines.csv")).First();
            Assert.Contains("lmax_instrument_id", header);
            Assert.Contains("mapping_sha256", header);
            Assert.Contains("source_ingestion_id", header);
            Assert.Contains("pms_source_session_id", header);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Broker_snapshot_after_ingestion_and_model_asof_is_accepted()
    {
        var universe = Universe(Plan());
        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(universe.LatestModelAsOfUtc.AddHours(1)), universe);
        Assert.True(snapshot.BrokerSnapshotAfterIngestion);
        Assert.Equal(
            "PROVEN_BROKER_SNAPSHOT_AFTER_PMS_INGESTION_AND_MODEL_ASOF",
            snapshot.TemporalLineageStatus);
    }

    [Fact]
    public void Broker_snapshot_before_ingestion_is_rejected()
    {
        var universe = Universe(Plan());
        AssertCode("ARCH7B_BROKER_POSITION_SNAPSHOT_PREDATES_PMS_SOURCE",
            () => Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                Core(universe.IngestionCompletedAtUtc.AddSeconds(-1)), universe));
    }

    [Fact]
    public void Broker_snapshot_before_model_asof_is_rejected()
    {
        var universe = Universe(Plan());
        AssertCode("ARCH7B_BROKER_POSITION_SNAPSHOT_PREDATES_PMS_SOURCE",
            () => Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                Core(universe.LatestModelAsOfUtc.AddSeconds(-1)), universe));
    }

    [Fact]
    public void Temporal_fields_participate_in_snapshot_evidence()
    {
        var universe = Universe(Plan());
        var first = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(universe.LatestModelAsOfUtc.AddHours(1)), universe);
        var second = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(universe.LatestModelAsOfUtc.AddHours(2)), universe);
        Assert.NotEqual(first.EvidenceSha256, second.EvidenceSha256);
    }

    [Fact]
    public void Import_remains_explicitly_not_authorized()
    {
        var universe = Universe(Plan());
        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            Core(universe.LatestModelAsOfUtc.AddHours(1)), universe);
        Assert.Equal(Arch7bBracketedGlobalFlatContract.ImportEligibility,
            snapshot.ImportEligibility);
        Assert.Equal(Arch7bBracketedGlobalFlatContract.ImportFreshnessStatus,
            snapshot.ImportFreshnessStatus);
    }

    private static PmsShadowPersistencePlan Plan()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var venueId = plan.SecurityMappings[0].VenueId;
        var lineageVersion = plan.TargetWeights[0].LineageVersion;
        var mappings = Enumerable.Range(1, 99).Select(index =>
        {
            var securityId = index.ToString();
            var symbol = Pair(index - 1);
            return new PmsShadowSecurityMappingRow(
                plan.Ingestion.IngestionId,
                Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                venueId,
                Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"),
                securityId,
                symbol,
                "lmax-" + symbol,
                1m,
                1m,
                0.00001m,
                Arch5bHashing.Sha256Hex($"mapping:{securityId}"));
        }).ToArray();
        var weights = plan.ModelRuns.SelectMany(model =>
            StrategySecurityIds(model.StrategyId).Select((index, sourceOrder) =>
            {
                var securityId = index.ToString();
                return new PmsShadowTargetWeightRow(
                    model.ModelRunId,
                    Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                    securityId,
                    0.001m,
                    model.TargetCloseUtc,
                    $"{model.StrategyId}:{securityId}",
                    sourceOrder,
                    model.OutputSha256,
                    lineageVersion);
            })).ToArray();
        return plan with { SecurityMappings = mappings, TargetWeights = weights };
    }

    private static Arch7bRequiredPmsUniverse Universe(
        PmsShadowPersistencePlan plan,
        IReadOnlyList<PmsShadowModelRunRow>? models = null,
        IReadOnlyList<PmsShadowQubesInputSnapshotRow>? qubes = null,
        IReadOnlyList<PmsShadowTargetWeightRow>? weights = null,
        IReadOnlyList<PmsShadowSecurityMappingRow>? mappings = null) =>
        Arch7bRequiredPmsUniverseBuilder.Build(
            plan.Ingestion,
            plan.AccountSnapshot,
            models ?? plan.ModelRuns,
            qubes ?? plan.QubesInputSnapshots,
            weights ?? plan.TargetWeights,
            mappings ?? plan.SecurityMappings,
            Arch7bBracketedGlobalFlatContract.TargetProfile,
            Hash('1'),
            transactionReadOnly: true,
            pendingModelChanges: false);

    private static Arch7bCoreBracketEvidence Core(DateTimeOffset timestamp) => new(
        Hash('a', 40),
        Arch7bBracketedGlobalFlatContract.CoreContractVersion,
        Arch7bBracketedGlobalFlatContract.DownloaderVersion,
        Arch7bBracketedGlobalFlatContract.AccountId,
        Arch7bBracketedGlobalFlatContract.Environment,
        Arch7bBracketedGlobalFlatContract.SessionMode,
        0,
        0,
        true,
        true,
        Arch7bBracketedGlobalFlatContract.ExecutionReportSchemaVersion,
        Arch7bBracketedGlobalFlatContract.PositionReportSchemaVersion,
        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256,
        Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256,
        Arch7bBracketedGlobalFlatContract.EmptyPositionSetAuthority,
        Arch7bBracketedGlobalFlatContract.AccountAuthorityMode,
        Arch7bBracketedGlobalFlatContract.CurrentSnapshotStatus,
        Arch7bBracketedGlobalFlatContract.BrokerDateSequenceStatus,
        0,
        30,
        timestamp,
        timestamp,
        timestamp,
        Hash('b'),
        Hash('c'),
        Hash('d'),
        Hash('e'),
        Hash('f'),
        true,
        true,
        true,
        true,
        true,
        "fixture",
        15);

    private static IEnumerable<int> StrategySecurityIds(string strategyId) =>
        strategyId switch
        {
            "INFX7" => Enumerable.Range(1, 66),
            "INFX8" => Enumerable.Range(34, 66),
            "INFX9" => Enumerable.Range(1, 78),
            "INFX10" => Enumerable.Range(22, 78),
            _ => throw new InvalidDataException("ARCH7B_TEST_UNKNOWN_STRATEGY")
        };

    private static string Pair(int index)
    {
        var currencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF",
            "NZD", "NOK", "SEK", "DKK", "SGD", "HKD" };
        return (from baseCurrency in currencies
                from quoteCurrency in currencies
                where baseCurrency != quoteCurrency
                select baseCurrency + quoteCurrency).ElementAt(index);
    }

    private static string Hash(char value, int length = 64) => new(value, length);

    private static void AssertCode(string expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<InvalidDataException>(action).Message);
}
