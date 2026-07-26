using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.OperationalReporting;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class InstitutionalMetricFoundationTests
{
    private const string Sha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RoadmapSha =
        "65fdbe260823ff38afa62e2d7e328fb9f9741296211f363b4f5d7e6b2fab8405";
    private static readonly DateTimeOffset AsOf =
        new(2026, 7, 26, 0, 35, 0, TimeSpan.Zero);
    private static readonly Guid IngestionId = Id(1);
    private static readonly string[] Symbols =
        ["AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY"];

    [Fact]
    public void CatalogSeparatesTwoSourceFactsFromDerivedMetrics()
    {
        var catalog = InstitutionalMetricCatalog.Build();
        Assert.Equal("anubis_infx_institutional_metric_catalog_v2",
            InstitutionalMetricContract.CatalogVersion);
        Assert.Equal(44, catalog.Count);
        Assert.Equal(2, catalog.Count(value =>
            value.CurrentAvailability == MetricAvailabilityStatus.SourceProven));
        Assert.Equal(14, catalog.Count(value =>
            value.CurrentAvailability == MetricAvailabilityStatus.DerivableProven));
        Assert.Equal(28, catalog.Count(value =>
            value.CurrentAvailability == MetricAvailabilityStatus.BlockedMissingSource));
        Assert.Equal(catalog.Count, catalog.Select(value => value.MetricCode).Distinct().Count());
    }

    [Fact]
    public void NonCompletedRevisionIsExcluded()
    {
        var projection = Projection(1, "slot-1", "PMS_SNAPSHOT") with { Status = "FAILED" };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void NonQualifyingRevisionIsExcluded()
    {
        var projection = Projection(1, "slot-1", "PMS_SNAPSHOT") with { Qualifying = false };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void NonNoOrderRevisionIsExcluded()
    {
        var projection = Projection(1, "slot-1", "PMS_SNAPSHOT") with { NoOrder = false };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void SupersededRevisionOfSameSlotIsExcluded()
    {
        var first = Projection(1, "same-slot", "PMS_SNAPSHOT");
        var second = Projection(2, "same-slot", "PMS_SNAPSHOT") with
        {
            SupersedesSlotManifestSha256 = Sha
        };
        var selected = InstitutionalAuthoritativeRevisionResolver.Resolve(
            [first, second], SlotManifests(first, second));
        Assert.Single(selected);
        Assert.Equal(second.ProjectionRevisionId, selected[0].ProjectionRevisionId);
    }

    [Fact]
    public void LatestRevisionIsTimestampDrivenAfterAuthorityResolution()
    {
        var earlier = Projection(7, "slot-a", "PMS_SNAPSHOT") with
        {
            CompletedAtUtc = AsOf.AddMinutes(-30),
            SlotEndUtc = AsOf.AddMinutes(-31)
        };
        var later = Projection(1, "slot-b", "PMS_SNAPSHOT") with
        {
            CompletedAtUtc = AsOf.AddMinutes(-2),
            SlotEndUtc = AsOf.AddMinutes(-3)
        };
        Assert.Equal(later.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [later, earlier], SlotManifests(later, earlier)).Last()
                .ProjectionRevisionId);
    }

    [Fact]
    public void SameAuthoritativeRankFailsClosed()
    {
        var first = Projection(2, "same-slot", "PMS_SNAPSHOT");
        var second = first with { ProjectionRevisionId = Id(987654) };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [first, second], SlotManifests(first, second)));
        Assert.Equal(InstitutionalAuthoritativeRevisionResolver.AmbiguousCode, error.Message);
    }

    [Fact]
    public void RevisionTwoMayBeAuthoritativeWithoutPersistedRevisionOne()
    {
        var revision = Projection(2, "same-slot", "PMS_SNAPSHOT") with
        {
            SupersedesSlotManifestSha256 = Sha
        };
        Assert.Equal(revision.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [revision], SlotManifests(revision))
                .Single().ProjectionRevisionId);
    }

    [Fact]
    public void IncoherentSupersessionFailsClosed()
    {
        var first = Projection(1, "same-slot", "PMS_SNAPSHOT");
        var second = Projection(2, "same-slot", "PMS_SNAPSHOT") with
        {
            SupersedesSlotManifestSha256 = new string('b', 64)
        };
        Assert.Equal("RPT2_SUPERSESSION_LINEAGE_INCOHERENT",
            Assert.Throws<InvalidDataException>(() =>
                InstitutionalAuthoritativeRevisionResolver.Resolve(
                    [first, second], SlotManifests(first, second))).Message);
    }

    [Fact]
    public void TurnoverNeverUsesTwoRevisionsOfSameSlot()
    {
        var first = Projection(1, "same-slot", "PMS_SNAPSHOT");
        var second = Projection(2, "same-slot", "PMS_SNAPSHOT") with
        {
            SupersedesSlotManifestSha256 = first.ManifestSha256
        };
        var report = Build([first, second], Mappings());
        Assert.Empty(report.Turnover);
    }

    [Fact]
    public void SourceFactsArePublishedAtPersistedGrain()
    {
        var report = Build();
        Assert.Equal(576, report.TargetPositionFacts.Count);
        Assert.Equal(576, report.PositionOnlyDriftFacts.Count);
        Assert.All(report.TargetPositionFacts,
            value => Assert.Equal(ReportingAuthority.Proven, value.AuthorityStatus));
        Assert.All(report.PositionOnlyDriftFacts,
            value => Assert.Equal(ReportingAuthority.Proven, value.AuthorityStatus));
        Assert.Equal(MetricAvailabilityStatus.SourceProven,
            Metric(report, "TARGET_POSITION_NOTIONAL").AvailabilityStatus);
        Assert.Equal(MetricAvailabilityStatus.SourceProven,
            Metric(report, "POSITION_ONLY_DRIFT_SOURCE").AvailabilityStatus);
    }

    [Fact]
    public void GrossNetLongAndShortAreDerived()
    {
        var report = Build();
        var row = report.ExposureByRevision.Last();
        Assert.Equal(row.GrossTargetNotionalUsd,
            row.LongTargetNotionalUsd + row.ShortTargetNotionalUsd);
        Assert.Equal(row.NetTargetNotionalUsd,
            row.LongTargetNotionalUsd - row.ShortTargetNotionalUsd);
        foreach (var code in new[]
                 {
                     "GROSS_TARGET_EXPOSURE", "NET_TARGET_EXPOSURE",
                     "LONG_TARGET_NOTIONAL", "SHORT_TARGET_NOTIONAL"
                 })
            Assert.Equal(MetricAvailabilityStatus.DerivableProven,
                Metric(report, code).AvailabilityStatus);
    }

    [Fact]
    public void CurrencyExposureIsDerivedAndPreservesUsdJpyIdentity()
    {
        var report = Build();
        var latest = report.ExposureByPair.Last(value => value.CanonicalSymbol == "USDJPY");
        Assert.Contains(report.ExposureByCurrency, value =>
            value.EconomicRevisionId == latest.EconomicRevisionId && value.Currency == "USD");
        Assert.Contains(report.ExposureByCurrency, value =>
            value.EconomicRevisionId == latest.EconomicRevisionId && value.Currency == "JPY");
        Assert.Equal(MetricAvailabilityStatus.DerivableProven,
            Metric(report, "TARGET_CURRENCY_EXPOSURE").AvailabilityStatus);
    }

    [Fact]
    public void NoAuthoritativeRevisionBlocksAllDependentMetrics()
    {
        var report = Build([], []);
        foreach (var metric in report.Catalog.Where(value =>
                     value.CurrentAvailability != MetricAvailabilityStatus.BlockedMissingSource))
            Assert.Equal(MetricAvailabilityStatus.BlockedMissingSource,
                Metric(report, metric.MetricCode).AvailabilityStatus);
    }

    [Fact]
    public void MultiRowMetricPointsToFactFileAndHasNoScalarValue()
    {
        var metric = Metric(Build(), "TARGET_POSITION_NOTIONAL");
        Assert.Null(metric.Value);
        Assert.False(metric.ValueIsScalar);
        Assert.Equal("target-position-facts.csv", metric.FactFile);
        Assert.Equal("VALUE_AVAILABLE_IN_FACT_FILE_NOT_SCALAR", metric.Caveat);
        Assert.True(metric.FactRowCount > 0);
    }

    [Fact]
    public void GrossSharesSumToOne()
    {
        AssertNormalized(Build().GrossConcentrations);
    }

    [Fact]
    public void NetSharesSumToOneWhenDefined()
    {
        AssertNormalized(Build().NetConcentrations);
    }

    [Fact]
    public void HhiIsWithinMathematicalBounds()
    {
        var report = Build();
        foreach (var summary in report.ConcentrationSummaries.Where(value => value.Hhi.HasValue))
        {
            var count = (summary.Family == "GROSS"
                    ? report.GrossConcentrations
                    : report.NetConcentrations)
                .Count(value => value.EconomicRevisionId == summary.EconomicRevisionId &&
                                value.DimensionType == summary.DimensionType &&
                                value.Share > 0m);
            Assert.InRange(summary.Hhi!.Value, 1m / count, 1m);
        }
    }

    [Fact]
    public void FourUniformStrategiesHaveHhiQuarter()
    {
        var data = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m),
            new TargetSpec("S2", "GBPUSD", 100m),
            new TargetSpec("S3", "USDJPY", 100m),
            new TargetSpec("S4", "AUDUSD", 100m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Equal(0.25m, Summary(report, "STRATEGY", "GROSS").Hhi);
        Assert.Equal(0.25m, Summary(report, "STRATEGY", "NET").Hhi);
    }

    [Fact]
    public void NinetyNineUniformPairsHaveHhiOneOverNinetyNine()
    {
        var specs = Enumerable.Range(0, 99)
            .Select(index => new TargetSpec("S1", $"A{index:D5}", 100m)).ToArray();
        var data = SimpleData(1, "slot-a", 1, specs);
        var hhi = Summary(Build([data.Projection], data.Mappings), "PAIR", "GROSS").Hhi;
        Assert.InRange(hhi!.Value, 1m / 99m - 0.00000000000000000000000001m,
            1m / 99m + 0.00000000000000000000000001m);
    }

    [Fact]
    public void OneDimensionHasHhiOne()
    {
        var data = SimpleData(1, "slot-a", 1, new TargetSpec("S1", "EURUSD", 100m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Equal(1m, Summary(report, "PAIR", "GROSS").Hhi);
        Assert.Equal(1m, Summary(report, "STRATEGY", "NET").Hhi);
    }

    [Fact]
    public void PairOffsetsDistinguishGrossFromNet()
    {
        var data = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m),
            new TargetSpec("S2", "EURUSD", -100m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Equal(1m, report.GrossConcentrations.Single(value => value.DimensionType == "PAIR").Share);
        Assert.Null(report.NetConcentrations.Single(value => value.DimensionType == "PAIR").Share);
        Assert.Equal("UNDEFINED_ZERO_NET_ABSOLUTE",
            report.NetConcentrations.Single(value => value.DimensionType == "PAIR").DataQualityStatus);
    }

    [Fact]
    public void ZeroGrossMakesGrossConcentrationUndefined()
    {
        var data = SimpleData(1, "slot-a", 1, new TargetSpec("S1", "EURUSD", 0m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Null(report.GrossConcentrations.Single(value => value.DimensionType == "PAIR").Share);
        Assert.Equal("UNDEFINED_ZERO_GROSS",
            report.GrossConcentrations.Single(value => value.DimensionType == "PAIR").DataQualityStatus);
    }

    [Fact]
    public void ZeroNetAbsoluteMakesNetConcentrationUndefined()
    {
        var data = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m), new TargetSpec("S2", "EURUSD", -100m));
        Assert.Null(Summary(Build([data.Projection], data.Mappings),
            "PAIR", "NET").Hhi);
    }

    [Fact]
    public void TurnoverUsesPreviousAndCurrentMappingSets()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "EURUSD", 120m));
        var report = Build([previous.Projection, current.Projection],
            previous.Mappings.Concat(current.Mappings).ToArray());
        var row = report.Turnover.Single(value => value.DimensionType == "PAIR");
        Assert.NotEqual(row.PreviousMappingSetSha256, row.CurrentMappingSetSha256);
        Assert.Equal(20m, row.TargetTurnoverUsd);
    }

    [Fact]
    public void ChangedMappingIdsWithSameCanonicalSymbolPreserveTurnoverIdentity()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "EURUSD", 120m));
        var report = Build([previous.Projection, current.Projection],
            previous.Mappings.Concat(current.Mappings).ToArray());
        Assert.Equal("EURUSD",
            report.Turnover.Single(value => value.DimensionType == "PAIR").DimensionId);
    }

    [Fact]
    public void MissingPreviousMappingFailsClosed()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "EURUSD", 120m));
        Assert.Equal("RPT2_SECURITY_MAPPING_MISSING",
            Assert.Throws<InvalidDataException>(() => Build(
                [previous.Projection, current.Projection], current.Mappings)).Message);
    }

    [Fact]
    public void MissingCurrentMappingFailsClosed()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "EURUSD", 120m));
        Assert.Equal("RPT2_SECURITY_MAPPING_MISSING",
            Assert.Throws<InvalidDataException>(() => Build(
                [previous.Projection, current.Projection], previous.Mappings)).Message);
    }

    [Fact]
    public void ContradictorySymbolForSameInstrumentFailsClosed()
    {
        var previous = SimpleDataWithInstrumentOffset(1, "slot-a", 1, 0,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleDataWithInstrumentOffset(1, "slot-b", 2, -1000,
            new TargetSpec("S1", "GBPUSD", 120m));
        Assert.Equal("RPT2_SECURITY_MAPPING_CONTRADICTORY",
            Assert.Throws<InvalidDataException>(() => Build(
                [previous.Projection, current.Projection],
                previous.Mappings.Concat(current.Mappings).ToArray())).Message);
    }

    [Fact]
    public void TurnoverClassifiesNewClosedIncreaseReductionAndInversion()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "AUDUSD", 0m), new TargetSpec("S1", "EURUSD", 10m),
            new TargetSpec("S1", "GBPUSD", 10m), new TargetSpec("S1", "NZDUSD", 20m),
            new TargetSpec("S1", "USDJPY", 10m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "AUDUSD", 10m), new TargetSpec("S1", "EURUSD", 0m),
            new TargetSpec("S1", "GBPUSD", 20m), new TargetSpec("S1", "NZDUSD", 10m),
            new TargetSpec("S1", "USDJPY", -10m));
        var total = Build([previous.Projection, current.Projection],
                previous.Mappings.Concat(current.Mappings).ToArray())
            .Turnover.Single(value => value.DimensionType == "TOTAL");
        Assert.Equal(1, total.NewTargetCount);
        Assert.Equal(1, total.ClosedTargetCount);
        Assert.Equal(1, total.IncreaseCount);
        Assert.Equal(1, total.ReductionCount);
        Assert.Equal(1, total.InversionCount);
    }

    [Fact]
    public void EurAndJpyBaseQuantitiesAreNeverSummed()
    {
        var data = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m), new TargetSpec("S1", "USDJPY", 200m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Equal(2, report.DriftByStrategyPair.Count);
        Assert.Equal(new[] { "EUR", "USD" },
            report.DriftByStrategyPair.Select(value => value.Unit).Order().ToArray());
        Assert.Equal(2, report.RiskSummary.AbsoluteDriftByPair!.Count);
    }

    [Fact]
    public void DriftGrainsArePairStrategyPairAndModelPair()
    {
        var report = Build();
        Assert.All(report.DriftByPair, value => Assert.Equal("PAIR", value.DimensionType));
        Assert.All(report.DriftByStrategyPair,
            value => Assert.Equal("STRATEGY_PAIR", value.DimensionType));
        Assert.All(report.DriftByModelPair,
            value => Assert.Equal("MODEL_PAIR", value.DimensionType));
    }

    [Fact]
    public void PositionAuthorityAbsentBlocksDriftAuthority()
    {
        var report = Build(positionAuthority: string.Empty);
        Assert.Equal(MetricAvailabilityStatus.BlockedAuthorityUnproven,
            Metric(report, "POSITION_ONLY_DRIFT_SOURCE").AvailabilityStatus);
        Assert.Equal(MetricAvailabilityStatus.BlockedAuthorityUnproven,
            Metric(report, "ABSOLUTE_POSITION_ONLY_DRIFT").AvailabilityStatus);
        Assert.All(report.DriftByPair, value =>
            Assert.Equal(MetricAvailabilityStatus.BlockedAuthorityUnproven,
                value.AvailabilityStatus));
    }

    [Fact]
    public void RiskSummaryDoesNotExposeMixedUnitDriftScalar()
    {
        var risk = Build().RiskSummary;
        Assert.NotNull(risk.AbsoluteDriftByPair);
        Assert.True(risk.AbsoluteDriftByPair!.Count > 1);
        Assert.DoesNotContain("AbsoluteDrift", typeof(PmsRiskSummary).GetProperties()
            .Where(value => value.PropertyType == typeof(decimal?))
            .Select(value => value.Name));
    }

    [Fact]
    public void StaleHistoricalAuthorityIsNotPresentedAsCurrent()
    {
        var report = Build(asOf: AsOf.AddDays(1));
        Assert.Equal(ReportingAuthority.Stale, report.DataQuality.Freshness);
        Assert.Equal(ReportingAuthority.Stale,
            Metric(report, "TARGET_POSITION_NOTIONAL").AuthorityStatus);
    }

    [Fact]
    public void SourceSnapshotChangesWhenTargetShaChanges()
    {
        var projection = Projection(1, "slot-a", "PMS_SNAPSHOT");
        var changed = projection with { TargetPositionsSha256 = new string('b', 64) };
        Assert.NotEqual(Build([projection], Mappings()).SourceSnapshotSha256,
            Build([changed], Mappings()).SourceSnapshotSha256);
    }

    [Fact]
    public void SourceSnapshotChangesWhenSelectedModelRunChanges()
    {
        var projection = Projection(1, "slot-a", "PMS_SNAPSHOT");
        var models = projection.SelectedModelRuns.ToArray();
        models[0] = models[0] with { OutputSha256 = new string('b', 64) };
        var changed = projection with { SelectedModelRuns = models };
        Assert.NotEqual(Build([projection], Mappings()).SourceSnapshotSha256,
            Build([changed], Mappings()).SourceSnapshotSha256);
    }

    [Fact]
    public void SourceSnapshotChangesWhenBreakChanges()
    {
        var snapshot = Build().SourceSnapshot;
        var changed = snapshot with { ActiveOrUnknownBreakIds = ["BREAK-X"] };
        Assert.NotEqual(InstitutionalSourceSnapshotContentAddress.ComputeSha256(snapshot),
            InstitutionalSourceSnapshotContentAddress.ComputeSha256(changed));
    }

    [Fact]
    public void SourceSnapshotChangesWhenMappingChanges()
    {
        var mappings = Mappings().ToArray();
        var changed = mappings.ToArray();
        changed[0] = changed[0] with { MappingSha256 = new string('b', 64) };
        Assert.NotEqual(Build([Projection(1, "slot-a", "PMS_SNAPSHOT")], mappings)
                .SourceSnapshotSha256,
            Build([Projection(1, "slot-a", "PMS_SNAPSHOT")], changed)
                .SourceSnapshotSha256);
    }

    [Fact]
    public void BundleManifestReferencesExactSourceSnapshotSha()
    {
        WithBundle(Build(), (path, result) =>
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(path, "manifest.json")));
            Assert.Equal(result.SourceSnapshotSha256,
                document.RootElement.GetProperty("source_snapshot_sha256").GetString());
            Assert.Equal(FileSha(path, "source-snapshot.json"), result.SourceSnapshotSha256);
        });
    }

    [Fact]
    public void BundleSupersedesPreviousSemanticVersion()
    {
        WithBundle(Build(), (path, _) =>
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(path, "manifest.json")));
            var root = document.RootElement;
            Assert.Equal(InstitutionalMetricContract.SupersededBundleSha256,
                root.GetProperty("supersedes_bundle_sha256").GetString());
            Assert.Equal(InstitutionalMetricContract.SupersessionReason,
                root.GetProperty("supersession_reason").GetString());
        });
    }

    [Fact]
    public void WriterProducesExactStableFileInventory()
    {
        WithBundle(Build(), (_, bundle) =>
        {
            Assert.Equal(24, bundle.Files.Count);
            Assert.Equal(ExpectedFiles, bundle.Files.Select(value => value.Path)
                .Order(StringComparer.Ordinal).ToArray());
        });
    }

    [Fact]
    public void TwoRunsAreByteForByteIdentical()
    {
        var first = TemporaryDirectory();
        var second = TemporaryDirectory();
        try
        {
            var a = DeterministicInstitutionalMetricBundleWriter.Write(Build(), first);
            var b = DeterministicInstitutionalMetricBundleWriter.Write(Build(), second);
            Assert.Equal(a.BundleSha256, b.BundleSha256);
            Assert.Equal(a.SourceSnapshotSha256, b.SourceSnapshotSha256);
            Assert.Equal(a.Files.Select(value => (value.Path, value.SizeBytes, value.Sha256)),
                b.Files.Select(value => (value.Path, value.SizeBytes, value.Sha256)));
            Assert.All(a.Files, file => Assert.Equal(
                File.ReadAllBytes(Path.Combine(first, file.Path)),
                File.ReadAllBytes(Path.Combine(second, file.Path))));
        }
        finally
        {
            Directory.Delete(first, true);
            Directory.Delete(second, true);
        }
    }

    [Fact]
    public void CsvAvailabilityDistinguishesScalarAndFactValues()
    {
        WithBundle(Build(), (path, _) =>
        {
            var lines = File.ReadAllLines(Path.Combine(path, "metric-availability.csv"));
            Assert.Equal("MetricCode,AvailabilityStatus,Value,Unit,Currency,MissingRequiredFacts,ActivationCondition,Caveat,AuthorityStatus,DataQualityStatus,ValueLocation,FactFile,FactRowCount,ValueIsScalar,Grain",
                lines[0]);
            Assert.Contains(lines, value => value.StartsWith(
                "TARGET_POSITION_NOTIONAL,SOURCE_PROVEN,NULL", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void PowerBiContractsCoverEveryCsv()
    {
        var report = Build();
        Assert.Equal(16, report.PowerBiContracts.Count);
        Assert.Equal(ExpectedFiles.Count(value => value.EndsWith(".csv",
                StringComparison.Ordinal)),
            report.PowerBiContracts.Count);
        Assert.DoesNotContain(report.PowerBiContracts,
            value => value.File is "drift-by-strategy.csv" or "drift-by-model.csv");
    }

    [Fact]
    public void BundleContainsNoSecretOrDatabentoPath()
    {
        WithBundle(Build(), (path, _) =>
        {
            var text = string.Join('\n', Directory.EnumerateFiles(path)
                .Select(File.ReadAllText));
            Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SecretAccessKey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("databento", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/api/", text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SnapshotRetainsReadOnlyTlsAndNoPendingModelChanges()
    {
        var database = Build().Database;
        Assert.True(database.TransactionReadOnly);
        Assert.False(database.PendingModelChanges);
        Assert.Equal("VERIFYFULL", database.TlsPolicy);
    }

    [Theory]
    [MemberData(nameof(BlockedMetricCodes))]
    public void UnavailableMetricIsExplicitlyBlocked(string metricCode)
    {
        var metric = Metric(Build(), metricCode);
        Assert.Equal(MetricAvailabilityStatus.BlockedMissingSource,
            metric.AvailabilityStatus);
        Assert.Null(metric.Value);
        Assert.NotEmpty(metric.MissingRequiredFacts);
    }

    public static IEnumerable<object[]> BlockedMetricCodes() =>
        InstitutionalMetricCatalog.BlockedMetrics().Select(value => new object[] { value.Code });

    private static InstitutionalMetricReportSet Build(
        string positionAuthority = "PMS_SNAPSHOT",
        DateTimeOffset? asOf = null) =>
        Build([Projection(1, "slot-1", positionAuthority),
                Projection(2, "slot-2", positionAuthority)],
            Mappings(), asOf);

    private static InstitutionalMetricReportSet Build(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> projections,
        IEnumerable<PmsShadowSecurityMappingRow> mappings,
        DateTimeOffset? asOf = null)
    {
        var snapshot = new OperationalReportingSnapshot(
            asOf ?? AsOf,
            new string('c', 40),
            Database(),
            [], [], [], [], [], [], [], [])
        {
            SlotManifestSha256BySlotId = SlotManifests(projections.ToArray()),
            EconomicProjectionSources = projections,
            SecurityMappingSources = mappings.ToArray()
        };
        return InstitutionalMetricProjector.Build(snapshot, RoadmapSha);
    }

    private static IReadOnlyDictionary<string, string?> SlotManifests(
        params PmsShadowIntradayEconomicProjection[] projections) =>
        projections.Select(value => value.SlotId).Distinct(StringComparer.Ordinal)
            .ToDictionary(value => value, _ => (string?)Sha, StringComparer.Ordinal);

    private static PmsShadowIntradayEconomicProjection Projection(
        int revision,
        string slotId,
        string positionAuthority)
    {
        var targets = new List<PmsShadowSlotTargetPosition>();
        var drifts = new List<PmsShadowSlotPositionOnlyDrift>();
        var selected = new List<PmsShadowSelectedModelRun>();
        var counts = new[] { 66, 66, 78, 78 };
        var ordinal = 0;
        for (var strategyIndex = 0; strategyIndex < counts.Length; strategyIndex++)
        {
            var strategy = OperationalReportingContract.Strategies[strategyIndex];
            var modelId = Id(1000 + strategyIndex);
            var targetClose = AsOf.AddHours(strategyIndex + 1);
            selected.Add(new(modelId, Id(1100 + strategyIndex), strategy,
                AsOf.AddHours(-1), targetClose, Sha, new string('b', 40),
                "REUSED_FINALIZED_D1_MODEL"));
            for (var index = 0; index < counts[strategyIndex]; index++)
            {
                var instrumentId = Id(2000 + index);
                var notional = Notional(revision, ordinal);
                targets.Add(new(Id(revision * 10000 + ordinal), Id(4000 + strategyIndex),
                    modelId, strategy, instrumentId, $"PMS-{index:D3}", notional,
                    notional / 10m, notional / 10m, 1m, targetClose, AsOf.AddMinutes(-5),
                    Sha, Sha, new string('b', 40)));
                drifts.Add(new(Id(revision * 20000 + ordinal), Id(5000 + strategyIndex),
                    modelId, strategy, instrumentId, $"PMS-{index:D3}",
                    notional / 20m, notional / 10m, notional / 20m, AsOf.AddMinutes(-5),
                    Sha, Sha));
                ordinal++;
            }
        }
        var market = Enumerable.Range(0, 99).Select(index =>
        {
            var mapping = Mappings()[index % 78];
            return new PmsShadowSlotMarketObservation(mapping.InstrumentId,
                mapping.SecurityId, mapping.Symbol, mapping.LmaxInstrumentId,
                1m, 1.01m, 1.005m, AsOf.AddMinutes(-6), "LMAX_PRIMARY", []);
        }).ToArray();
        var completed = AsOf.AddMinutes(revision == 1 ? -20 : -4);
        return new(Id(3000 + revision), revision, slotId,
            completed.AddMinutes(-16), completed.AddMinutes(-1), Sha, Id(6000 + revision),
            Sha, IngestionId, "session-rpt2", Id(7000), Id(7001),
            AsOf.AddMinutes(-30), positionAuthority,
            selected.Select(value => value.ModelRunId).ToArray(),
            selected.Select(value => value.QubesInputSnapshotId).ToArray(), selected, market,
            targets, drifts, Sha, Sha, Sha, Sha, revision == 1 ? null : Sha,
            "COMPLETED", "COMPLETED", true, true, completed);
    }

    private sealed record TargetSpec(string Strategy, string Symbol, decimal Notional);
    private sealed record SimpleProjectionData(
        PmsShadowIntradayEconomicProjection Projection,
        IReadOnlyList<PmsShadowSecurityMappingRow> Mappings);

    private static SimpleProjectionData SimpleData(
        int revision,
        string slotId,
        int ingestionOrdinal,
        params TargetSpec[] specs) =>
        SimpleDataWithInstrumentOffset(revision, slotId, ingestionOrdinal, 0, specs);

    private static SimpleProjectionData SimpleDataWithInstrumentOffset(
        int revision,
        string slotId,
        int ingestionOrdinal,
        int instrumentOffset,
        params TargetSpec[] specs)
    {
        var ingestionId = Id(100000 + ingestionOrdinal);
        var strategies = specs.Select(value => value.Strategy)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var models = strategies.Select((strategy, index) =>
            new PmsShadowSelectedModelRun(Id(200000 + ingestionOrdinal * 100 + index),
                Id(210000 + ingestionOrdinal * 100 + index), strategy,
                AsOf.AddHours(-1), AsOf.AddHours(1), Sha, new string('b', 40),
                "REUSED_FINALIZED_D1_MODEL")).ToArray();
        var modelByStrategy = models.ToDictionary(value => value.StrategyId,
            value => value, StringComparer.Ordinal);
        var mappings = specs.Select((spec, index) =>
        {
            var instrumentId = Id(300000 + ingestionOrdinal * 1000 + index + instrumentOffset);
            return new PmsShadowSecurityMappingRow(ingestionId, instrumentId,
                Id(400000), Id(410000 + ingestionOrdinal * 1000 + index),
                $"PMS-{ingestionOrdinal}-{index}", spec.Symbol,
                $"LMAX-{ingestionOrdinal}-{index}", 1m, 0.01m, 0.00001m, Sha);
        }).ToArray();
        var targets = specs.Select((spec, index) =>
        {
            var mapping = mappings[index];
            var model = modelByStrategy[spec.Strategy];
            return new PmsShadowSlotTargetPosition(
                Id(500000 + ingestionOrdinal * 1000 + index), Id(600000 + index),
                model.ModelRunId, spec.Strategy, mapping.InstrumentId, mapping.SecurityId,
                spec.Notional, spec.Notional / 10m, spec.Notional / 10m, 1m,
                model.TargetCloseUtc, AsOf.AddMinutes(-5), Sha, Sha, new string('b', 40));
        }).ToArray();
        var drifts = targets.Select((target, index) =>
            new PmsShadowSlotPositionOnlyDrift(
                Id(700000 + ingestionOrdinal * 1000 + index), Id(710000 + index),
                target.ModelRunId, target.StrategyId, target.InstrumentId, target.SecurityId,
                0m, target.TargetBaseQuantity, target.TargetBaseQuantity,
                AsOf.AddMinutes(-5), Sha, Sha)).ToArray();
        var completed = AsOf.AddMinutes(-30 + ingestionOrdinal);
        var projection = new PmsShadowIntradayEconomicProjection(
            Id(800000 + ingestionOrdinal * 100 + revision), revision, slotId,
            completed.AddMinutes(-16), completed.AddMinutes(-1), Sha,
            Id(820000 + ingestionOrdinal), Sha, ingestionId,
            $"session-{ingestionOrdinal}", Id(830000), Id(830001),
            AsOf.AddMinutes(-40), "PMS_SNAPSHOT",
            models.Select(value => value.ModelRunId).ToArray(),
            models.Select(value => value.QubesInputSnapshotId).ToArray(),
            models, [], targets, drifts, Sha, Sha, Sha, Sha,
            revision == 1 ? null : Sha, "COMPLETED", "COMPLETED", true, true, completed);
        return new(projection, mappings);
    }

    private static IReadOnlyList<PmsShadowSecurityMappingRow> Mappings() =>
        Enumerable.Range(0, 78).Select(index => new PmsShadowSecurityMappingRow(
            IngestionId, Id(2000 + index), Id(8000), Id(9000 + index),
            $"PMS-{index:D3}", Symbols[index % Symbols.Length],
            $"LMAX-{4000 + index}", 1m, 0.01m, 0.00001m, Sha)).ToArray();

    private static decimal Notional(int revision, int ordinal)
    {
        if (revision == 1)
            return ordinal switch
            {
                0 => 0m, 1 => 10m, 2 => 10m, 3 => 20m, 4 => 10m,
                _ => ordinal % 2 == 0 ? 100m + ordinal : -50m - ordinal
            };
        return ordinal switch
        {
            0 => 10m, 1 => 0m, 2 => 20m, 3 => 10m, 4 => -10m,
            _ => ordinal % 2 == 0 ? 110m + ordinal : -45m - ordinal
        };
    }

    private static ReportingDatabaseIdentity Database() => new(
        "qq_pms_shadow_arch7b_test", "PostgreSQL 18.4", 18, "pms_shadow", 35,
        7291, PmsShadowStateContract.MigrationIds, true, false, "ARCH7B_RDS_TEST",
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
        "REMOTE_TLS", "VERIFYFULL");

    private static InstitutionalMetricAvailability Metric(
        InstitutionalMetricReportSet report,
        string code) => report.Availability.Single(value => value.MetricCode == code);

    private static TargetConcentrationSummaryRow Summary(
        InstitutionalMetricReportSet report,
        string dimension,
        string family) => report.ConcentrationSummaries.Last(value =>
            value.DimensionType == dimension && value.Family == family);

    private static void AssertNormalized(IReadOnlyList<TargetConcentrationRow> values)
    {
        foreach (var group in values.Where(value => value.Share.HasValue)
                     .GroupBy(value => (value.EconomicRevisionId, value.DimensionType)))
            Assert.InRange(group.Sum(value => value.Share!.Value),
                0.999999999999999999999999999m,
                1.000000000000000000000000001m);
    }

    private static void WithBundle(
        InstitutionalMetricReportSet report,
        Action<string, InstitutionalBundleResult> assertion)
    {
        var path = TemporaryDirectory();
        try
        {
            assertion(path, DeterministicInstitutionalMetricBundleWriter.Write(report, path));
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    private static string FileSha(string root, string file) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(Path.Combine(root, file))));

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qq-rpt2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static readonly string[] ExpectedFiles =
    [
        "active-breaks.csv",
        "data-quality.json",
        "drift-by-model-pair.csv",
        "drift-by-pair.csv",
        "drift-by-strategy-pair.csv",
        "institutional-metric-catalog.json",
        "institutional-reporting-roadmap.json",
        "manifest.json",
        "metric-availability.csv",
        "performance-availability.json",
        "pms-risk-summary.json",
        "position-only-drift-facts.csv",
        "report.html",
        "source-snapshot.json",
        "target-concentration-gross.csv",
        "target-concentration-net.csv",
        "target-concentration-summary.csv",
        "target-exposure-by-currency.csv",
        "target-exposure-by-model.csv",
        "target-exposure-by-pair.csv",
        "target-exposure-by-revision.csv",
        "target-exposure-by-strategy.csv",
        "target-position-facts.csv",
        "target-turnover.csv"
    ];
}
