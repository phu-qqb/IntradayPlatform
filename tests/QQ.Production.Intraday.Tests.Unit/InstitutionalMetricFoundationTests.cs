using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.OperationalReporting;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class InstitutionalMetricFoundationTests
{
    private const string Sha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RoadmapSha =
        "1bca7a5d086674454a580c38ae42202c6fc9e8121c6ee76be786c87985fa5dc2";
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
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with { Status = "FAILED" };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void NonQualifyingRevisionIsExcluded()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with { Qualifying = false };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void NonNoOrderRevisionIsExcluded()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with { NoOrder = false };
        Assert.Empty(InstitutionalAuthoritativeRevisionResolver.Resolve(
            [projection], SlotManifests(projection)));
    }

    [Fact]
    public void SupersededRevisionOfSameSlotIsExcluded()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var second = Rehash(Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SupersedesSlotManifestSha256 = Sha
        });
        var selected = InstitutionalAuthoritativeRevisionResolver.Resolve(
            [first, second], SlotManifests(first, second));
        Assert.Single(selected);
        Assert.Equal(second.ProjectionRevisionId, selected[0].ProjectionRevisionId);
    }

    [Fact]
    public void EconomicTimelineUsesSlotEndBeforeLateCompletionTime()
    {
        var earlier = Projection(1, "slot-a", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SlotStartUtc = AsOf.AddMinutes(-35),
            SlotEndUtc = AsOf.AddMinutes(-20),
            CompletedAtUtc = AsOf.AddMinutes(2)
        };
        var later = Projection(1, "slot-b", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SlotStartUtc = AsOf.AddMinutes(-20),
            SlotEndUtc = AsOf.AddMinutes(-5),
            CompletedAtUtc = AsOf.AddMinutes(-4)
        };
        Assert.Equal(later.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [later, earlier], SlotManifests(later, earlier)).Last()
                .ProjectionRevisionId);
    }

    [Fact]
    public void SameAuthoritativeRankFailsClosed()
    {
        var first = Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var second = first with { ProjectionRevisionId = Id(987654) };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [first, second], SlotManifests(first, second)));
        Assert.Equal(InstitutionalAuthoritativeRevisionResolver.AmbiguousCode, error.Message);
    }

    [Fact]
    public void RevisionTwoMayBeAuthoritativeWithoutPersistedRevisionOne()
    {
        var revision = Rehash(Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SupersedesSlotManifestSha256 = Sha
        });
        Assert.Equal(revision.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [revision], SlotManifests(revision))
                .Single().ProjectionRevisionId);
    }

    [Fact]
    public void IncoherentSupersessionFailsClosed()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var second = Rehash(Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SupersedesSlotManifestSha256 = new string('b', 64)
        });
        Assert.Equal(
            "RPT2_LATEST_QUALIFYING_REVISION_INVALID:RPT2_SUPERSESSION_LINEAGE_INCOHERENT",
            Assert.Throws<InvalidDataException>(() =>
                InstitutionalAuthoritativeRevisionResolver.Resolve(
                    [first, second], SlotManifests(first, second))).Message);
    }

    [Fact]
    public void TurnoverNeverUsesTwoRevisionsOfSameSlot()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var second = Rehash(Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode) with
        {
            SupersedesSlotManifestSha256 = Sha
        });
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
    public void SixtySixUniformPairsHaveHhiOneOverSixtySix()
    {
        var specs = Enumerable.Range(0, 66)
            .Select(index => new TargetSpec("S1", $"A{index:D5}", 100m)).ToArray();
        var data = SimpleData(1, "slot-a", 1, specs);
        var hhi = Summary(Build([data.Projection], data.Mappings), "PAIR", "GROSS").Hhi;
        Assert.InRange(hhi!.Value, 1m / 66m - 0.00000000000000000000000001m,
            1m / 66m + 0.00000000000000000000000001m);
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
        Assert.Equal(1m, report.GrossConcentrations.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").Share);
        Assert.Null(report.NetConcentrations.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").Share);
        Assert.Equal("UNDEFINED_ZERO_NET_ABSOLUTE",
            report.NetConcentrations.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").DataQualityStatus);
    }

    [Fact]
    public void ZeroGrossMakesGrossConcentrationUndefined()
    {
        var data = SimpleData(1, "slot-a", 1, new TargetSpec("S1", "EURUSD", 0m));
        var report = Build([data.Projection], data.Mappings);
        Assert.Null(report.GrossConcentrations.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").Share);
        Assert.Equal("UNDEFINED_ZERO_GROSS",
            report.GrossConcentrations.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").DataQualityStatus);
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
        var row = report.Turnover.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD");
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
            report.Turnover.Single(value => value.DimensionType == "PAIR" && value.DimensionId == "EURUSD").DimensionId);
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
        var material = report.DriftByStrategyPair
            .Where(value => value.AbsoluteDrift > 0m).ToArray();
        Assert.Equal(2, material.Length);
        Assert.Equal(new[] { "EUR", "USD" },
            material.Select(value => value.Unit).Order().ToArray());
        Assert.Contains("EURUSD", report.RiskSummary.AbsoluteDriftByPair!.Keys);
        Assert.Contains("USDJPY", report.RiskSummary.AbsoluteDriftByPair.Keys);
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
    public void WeekendCompletedLastRequiredSlotIsCurrentOutsideCalendar()
    {
        var revision = Projection(2, "slot-friday", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var snapshot = SnapshotWithSlots(AsOf, [revision],
            [Slot(revision, "COMPLETED")]);
        var result = InstitutionalMetricCurrentnessPolicy.Evaluate(snapshot, [revision]);
        Assert.Equal(InstitutionalCurrentnessStatuses.OutsideCalendarCurrent,
            result.MetricCurrentnessStatus);
    }

    [Fact]
    public void WeekendMissedLastRequiredSlotIsObsolete()
    {
        var revision = Projection(2, "slot-friday", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var snapshot = SnapshotWithSlots(AsOf, [revision],
            [Slot(revision, "MISSED")]);
        var result = InstitutionalMetricCurrentnessPolicy.Evaluate(snapshot, [revision]);
        Assert.Equal(InstitutionalCurrentnessStatuses.LastRequiredSlotMissed,
            result.MetricCurrentnessStatus);
    }

    [Fact]
    public void MondaySlotInsideGraceIsDueNotYetLate()
    {
        var monday = new DateTimeOffset(2026, 7, 27, 0, 1, 0, TimeSpan.Zero);
        var snapshot = SnapshotWithSlots(monday, [], []);
        var result = InstitutionalMetricCurrentnessPolicy.Evaluate(snapshot, []);
        Assert.Equal(InstitutionalCurrentnessStatuses.DueNotYetLate,
            result.MetricCurrentnessStatus);
    }

    [Fact]
    public void ChangedTargetShaIsRejectedBeforeSourceSnapshotPublication()
    {
        var projection = Projection(1, "slot-a", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var changed = projection with { TargetPositionsSha256 = new string('b', 64) };
        var error = Assert.Throws<InvalidDataException>(() => Build([changed], Mappings()));
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.TargetPositionMismatch,
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangedSelectedModelRunIsRejectedBeforeSourceSnapshotPublication()
    {
        var projection = Projection(1, "slot-a", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var models = projection.SelectedModelRuns.ToArray();
        models[0] = models[0] with { OutputSha256 = new string('b', 64) };
        var changed = projection with { SelectedModelRuns = models };
        var error = Assert.Throws<InvalidDataException>(() => Build([changed], Mappings()));
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.InputMismatch,
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceSnapshotChangesWhenBreakChanges()
    {
        var snapshot = Build().SourceSnapshot;
        var changed = snapshot with
        {
            ActiveOrUnknownBreakFacts =
            [
                new("BREAK-X", "RPT2_TEST_BREAK", null, "ACTIVE", "WARNING",
                    ReportingAuthority.Unknown, "RPT2", "GLOBAL", "RPT2", null,
                    null, null, null, AsOf.AddMinutes(-1), AsOf, Sha, true, false)
            ]
        };
        Assert.NotEqual(InstitutionalSourceSnapshotContentAddress.ComputeSha256(snapshot),
            InstitutionalSourceSnapshotContentAddress.ComputeSha256(changed));
    }

    [Fact]
    public void SourceSnapshotChangesWhenMappingChanges()
    {
        var mappings = Mappings().ToArray();
        var changed = mappings.ToArray();
        changed[0] = changed[0] with { MappingSha256 = new string('b', 64) };
        Assert.NotEqual(Build([Projection(1, "slot-a", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode)], mappings)
                .SourceSnapshotSha256,
            Build([Projection(1, "slot-a", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode)], changed)
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

    [Fact]
    public void ExactModelSetRejectsDuplicateModelRun()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var models = projection.SelectedModelRuns.ToArray();
        models[1] = models[1] with { ModelRunId = models[0].ModelRunId };
        var invalid = projection with { SelectedModelRuns = models };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [invalid], SlotManifests(invalid)));
        Assert.Contains("RPT2_SELECTED_MODEL_SET_DUPLICATED", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactModelSetRejectsUnexpectedStrategy()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var models = projection.SelectedModelRuns.ToArray();
        models[0] = models[0] with { StrategyId = "INFX11" };
        var invalid = projection with { SelectedModelRuns = models };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [invalid], SlotManifests(invalid)));
        Assert.Contains("RPT2_SELECTED_MODEL_SET_UNEXPECTED", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactModelSetRejectsCountMismatch()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var invalid = projection with
        {
            TargetPositions = projection.TargetPositions.Skip(1).ToArray()
        };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [invalid], SlotManifests(invalid)));
        Assert.Contains("RPT2_SELECTED_MODEL_COUNTS_MISMATCH", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactModelSetRejectsTargetLineageMismatch()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var targets = projection.TargetPositions.ToArray();
        targets[0] = targets[0] with { StrategyId = "INFX8" };
        var invalid = projection with { TargetPositions = targets };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [invalid], SlotManifests(invalid)));
        Assert.Contains("RPT2_SELECTED_MODEL_LINEAGE_MISMATCH", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceFactsExposeTrueSourceAndRevisionAuditFields()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var report = Build([projection], Mappings());
        var target = report.TargetPositionFacts.First();
        var sourceTarget = projection.TargetPositions.Single(value =>
            value.TargetPositionId == target.TargetPositionId);
        Assert.Equal(sourceTarget.CalculatedAtUtc, target.SourceAsOfUtc);
        Assert.Equal(projection.CompletedAtUtc, target.RevisionCompletedAtUtc);
        Assert.Equal(sourceTarget.DecisionPrice, target.DecisionPrice);
        Assert.Equal(sourceTarget.CoreCommitId, target.CoreCommitId);
        Assert.Equal(sourceTarget.InputSha256, target.InputSha256);
        Assert.Equal(sourceTarget.OutputSha256, target.OutputSha256);
        var drift = report.PositionOnlyDriftFacts.First();
        Assert.Equal(drift.TargetBaseQuantity - drift.CurrentBaseQuantity, drift.Delta);
        Assert.Equal(projection.AccountSnapshotId, drift.AccountSnapshotId);
        Assert.Equal(projection.PositionSnapshotId, drift.PositionSnapshotId);
        Assert.Equal(projection.PositionSnapshotAsOfUtc, drift.PositionSnapshotAsOfUtc);
    }

    [Fact]
    public void DriftArithmeticMismatchFailsClosed()
    {
        var projection = Projection(1, "slot-1", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var drifts = projection.PositionOnlyDrifts.ToArray();
        drifts[0] = drifts[0] with { Delta = drifts[0].Delta + 1m };
        var error = Assert.Throws<InvalidDataException>(() => Build(
            [projection with { PositionOnlyDrifts = drifts }], Mappings()));
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.DriftMismatch,
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrokerPortalEodPositionAuthorityIsProvenWithCompleteLineage()
    {
        var projection = Projection(1, "slot-1",
            InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var coverage = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(
            projection, PositionLines([projection]));
        var decision = InstitutionalPositionAuthorityPolicy.Evaluate(
            projection, coverage);
        Assert.Equal(ReportingAuthority.Proven, decision.AuthorityStatus);
        Assert.Equal(MetricAvailabilityStatus.SourceProven,
            decision.AvailabilityStatus);
    }

    [Fact]
    public void UnknownPositionAuthorityTextIsNotProven()
    {
        var projection = Projection(1, "slot-1", "UNVERSIONED_TEXT");
        var coverage = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(
            projection, PositionLines([projection]));
        var decision = InstitutionalPositionAuthorityPolicy.Evaluate(
            projection, coverage);
        Assert.Equal(ReportingAuthority.Unknown, decision.AuthorityStatus);
        Assert.Equal(MetricAvailabilityStatus.BlockedAuthorityUnproven,
            decision.AvailabilityStatus);
    }

    [Fact]
    public void FillAndLedgerPresenceDoNotProveAuthority()
    {
        Assert.Equal(ReportingAuthority.Absent,
            InstitutionalExecutionAuthorityPolicy.FillAuthority(0));
        Assert.Equal(ReportingAuthority.Unknown,
            InstitutionalExecutionAuthorityPolicy.FillAuthority(7));
        Assert.Equal(ReportingAuthority.Absent,
            InstitutionalExecutionAuthorityPolicy.LedgerAuthority(0,
                ReportingAuthority.Absent));
        Assert.Equal(ReportingAuthority.Unknown,
            InstitutionalExecutionAuthorityPolicy.LedgerAuthority(7,
                ReportingAuthority.Unknown));
    }

    [Fact]
    public void ZeroGrossProducesNullGrossWeightWithExplicitQuality()
    {
        var data = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 0m));
        var row = Build([data.Projection], data.Mappings).ExposureByRevision.Single();
        Assert.Null(row.GrossWeight);
        Assert.Equal("UNDEFINED_ZERO_GROSS", row.DataQualityStatus);
        Assert.NotEmpty(row.Caveat);
    }

    [Fact]
    public void TurnoverCarriesEconomicSlotContinuity()
    {
        var previous = SimpleData(1, "slot-a", 1,
            new TargetSpec("S1", "EURUSD", 100m));
        var current = SimpleData(1, "slot-b", 2,
            new TargetSpec("S1", "EURUSD", 120m));
        var row = Build([previous.Projection, current.Projection],
                previous.Mappings.Concat(current.Mappings).ToArray())
            .Turnover.Single(value => value.DimensionType == "TOTAL");
        Assert.Equal("slot-a", row.PreviousSlotId);
        Assert.Equal("slot-b", row.CurrentSlotId);
        Assert.Equal(previous.Projection.SlotEndUtc, row.PeriodStartUtc);
        Assert.Equal(current.Projection.SlotEndUtc, row.PeriodEndUtc);
        Assert.Equal(0, row.OperationalSlotGapCount);
        Assert.Equal("CONSECUTIVE_OPERATIONAL_SLOTS", row.PeriodContinuityStatus);
    }

    [Fact]
    public void RoadmapAuthorityAcceptsOnlyCanonicalRepositoryPathAndIdentity()
    {
        var root = TemporaryDirectory();
        try
        {
            var roadmap = Path.Combine(root,
                InstitutionalRoadmapAuthority.RelativePath.Replace('/',
                    Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(roadmap)!);
            File.WriteAllText(roadmap,
                "ManifestId | `hedge_fund_institutional_reporting_roadmap`\n" +
                "ManifestVersion | `v1`\n" +
                "Status | `AUTHORITATIVE_REPORTING_ROADMAP`\n" +
                "CurrentMasterAtCreation | `abc`\n" +
                "reporting_source reporting_mart reporting_control " +
                "reporting_publication RPT1 RPT2 RPT3 RPT4\n");
            var accepted = InstitutionalRoadmapAuthority.Resolve(root);
            Assert.Equal(Path.GetFullPath(roadmap), accepted.RoadmapPath);
            Assert.Equal(64, accepted.Sha256.Length);
            Assert.Equal("RPT2_ROADMAP_AUTHORITY_PATH_MISMATCH",
                Assert.Throws<InvalidDataException>(() =>
                    InstitutionalRoadmapAuthority.Resolve(root,
                        Path.Combine(root, "roadmap.md"))).Message);
            File.WriteAllText(roadmap, "wrong identity");
            Assert.Equal("RPT2_ROADMAP_MANIFEST_ID_MISMATCH",
                Assert.Throws<InvalidDataException>(() =>
                    InstitutionalRoadmapAuthority.Resolve(root)).Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void ExplicitZeroLineProvesCompletePositionCoverage()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(
            projection, PositionLines([projection]));
        Assert.Equal(ReportingAuthority.Proven, decision.CurrentPositionAuthorityDecision);
        Assert.Equal(InstitutionalPositionSnapshotCoveragePolicy.FullUniverseExplicitZero,
            decision.CoverageMode);
        Assert.Equal(0, decision.MissingCount);
        Assert.Contains(PositionLines([projection]), value =>
            value.CurrentBaseQuantity == 0m);
    }

    [Fact]
    public void MissingPositionLineIsSparseUnknownAndNeverZero()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var lines = PositionLines([projection]).Where(value =>
            value.CurrentBaseQuantity != 0m).ToArray();
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(projection, lines);
        Assert.Equal(InstitutionalPositionSnapshotCoveragePolicy.SparseNonzero,
            decision.CoverageMode);
        Assert.Equal(1, decision.MissingCount);
        Assert.Equal(ReportingAuthority.Unknown, decision.CurrentPositionAuthorityDecision);
    }

    [Fact]
    public void DuplicatePositionInstrumentLineFailsClosed()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var lines = PositionLines([projection]).ToList();
        lines.Add(lines[0] with { RowIdentity = lines[0].RowIdentity + ":duplicate" });
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(projection, lines);
        Assert.Equal(1, decision.DuplicateCount);
        Assert.Equal(ReportingAuthority.Unknown, decision.CurrentPositionAuthorityDecision);
    }

    [Fact]
    public void PositionCurrentQuantityMismatchFailsClosed()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var lines = PositionLines([projection]).ToArray();
        lines[0] = lines[0] with { CurrentBaseQuantity = 1m };
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(projection, lines);
        Assert.Equal(1, decision.MismatchCount);
        Assert.Equal(ReportingAuthority.Unknown, decision.CurrentPositionAuthorityDecision);
    }

    [Fact]
    public void ExtraPositionLineIsCountedWithoutDoubleCountingRequiredCoverage()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var lines = PositionLines([projection]).ToList();
        lines.Add(new(projection.PositionSnapshotId, Id(999001), "EXTRA", "EXTRA",
            7m, projection.SourceIngestionId,
            $"{projection.PositionSnapshotId:D}:{Id(999001):D}",
            projection.PositionSnapshotAsOfUtc, Sha));
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(projection, lines);
        Assert.Equal(1, decision.ExtraCount);
        Assert.Equal(decision.RequiredInstrumentCount, decision.CoveredInstrumentCount);
        Assert.Equal(ReportingAuthority.Proven, decision.CurrentPositionAuthorityDecision);
    }

    [Fact]
    public void NoPositionLinesProducesAbsentAuthority()
    {
        var projection = Projection(1, "slot-coverage", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var decision = InstitutionalPositionSnapshotCoveragePolicy.Evaluate(projection, []);
        Assert.Equal(ReportingAuthority.Absent, decision.CurrentPositionAuthorityDecision);
        Assert.Equal(decision.RequiredInstrumentCount, decision.MissingCount);
    }

    [Fact]
    public void LatestCompletedQualifyingInvalidRevisionBlocksWithoutFallback()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var second = Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode)
            with { TargetPositionsSha256 = new string('b', 64) };
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [first, second], SlotManifests(first, second)));
        Assert.Contains(InstitutionalAuthoritativeRevisionResolver.LatestQualifyingInvalid,
            error.Message, StringComparison.Ordinal);
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.TargetPositionMismatch,
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HigherFailedRevisionDoesNotSupersedeLowerQualifyingRevision()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var failed = Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode)
            with { Status = "FAILED" };
        Assert.Equal(first.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [first, failed], SlotManifests(first, failed)).Single().ProjectionRevisionId);
    }

    [Fact]
    public void HigherNonqualifyingRevisionDoesNotSupersedeLowerQualifyingRevision()
    {
        var first = Projection(1, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var nonqualifying = Projection(2, "same-slot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode)
            with { Qualifying = false };
        Assert.Equal(first.ProjectionRevisionId,
            InstitutionalAuthoritativeRevisionResolver.Resolve(
                [first, nonqualifying], SlotManifests(first, nonqualifying))
                .Single().ProjectionRevisionId);
    }

    [Fact]
    public void ProjectionIntegrityRejectsEveryContentAndIdentityMutation()
    {
        var projection = Projection(1, "slot-integrity", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.Proven,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(projection).Status);

        var market = projection.MarketData.ToArray();
        market[0] = market[0] with { Bid = market[0].Bid + 0.0001m };
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.MarketDataMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(
                projection with { MarketData = market }).Blockers);

        var targets = projection.TargetPositions.ToArray();
        targets[0] = targets[0] with
        {
            TargetBaseQuantity = targets[0].TargetBaseQuantity + 1m
        };
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.TargetPositionMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(
                projection with { TargetPositions = targets }).Blockers);

        var drifts = projection.PositionOnlyDrifts.ToArray();
        drifts[0] = drifts[0] with { Delta = drifts[0].Delta + 1m };
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.DriftMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(
                projection with { PositionOnlyDrifts = drifts }).Blockers);

        var models = projection.SelectedModelRuns.ToArray();
        models[0] = models[0] with { OutputSha256 = new string('b', 64) };
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.InputMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(
                projection with { SelectedModelRuns = models }).Blockers);

        var revisionTwo = Projection(2, "slot-integrity-two",
            InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.ManifestMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(revisionTwo with
            {
                SupersedesSlotManifestSha256 = new string('b', 64)
            }).Blockers);
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.IdentityMismatch,
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(
                projection with { ProjectionRevisionId = Id(999999) }).Blockers);
    }

    [Fact]
    public void ProjectionIntegrityRecognizesExactHistoricalAndCurrentIdentityContracts()
    {
        var current = Projection(2, "slot-identity-contract",
            InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var currentResult = PmsShadowEconomicProjectionIntegrityVerifier.Verify(current);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.Proven, currentResult.Status);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.ProjectionIdentityV2,
            currentResult.ProjectionIdentityContractVersion);

        var revisionIdentity =
            $"{PmsShadowIntradayEconomicContract.TestEnvironment}:{current.SlotId}:{current.RawCaptureSha256}:{PmsShadowIntradayEconomicContract.Version}";
        var historicalRevisionId = Arch5bHashing.GuidFromSha256(revisionIdentity);
        var historicalManifest = Arch5bHashing.HashCanonical(new
        {
            RevisionId = historicalRevisionId,
            Input = current.InputSha256,
            Targets = current.TargetPositionsSha256,
            Drifts = current.DriftsSha256,
            Supersedes = current.SupersedesSlotManifestSha256,
            NoOrder = true,
            Blocker = PmsShadowStateContract.BrokerAdjustedBlocker
        });
        var historical = current with
        {
            ProjectionRevisionId = historicalRevisionId,
            ManifestSha256 = historicalManifest
        };
        var historicalResult =
            PmsShadowEconomicProjectionIntegrityVerifier.Verify(historical);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.Proven,
            historicalResult.Status);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.ProjectionIdentityV1,
            historicalResult.ProjectionIdentityContractVersion);
        Assert.Equal(historicalRevisionId,
            historicalResult.RecalculatedProjectionRevisionId);
        Assert.Equal(historicalManifest, historicalResult.RecalculatedManifestSha256);

        var inventedId = Id(999998);
        var inventedManifest = Arch5bHashing.HashCanonical(new
        {
            RevisionId = inventedId,
            Input = current.InputSha256,
            Targets = current.TargetPositionsSha256,
            Drifts = current.DriftsSha256,
            Supersedes = current.SupersedesSlotManifestSha256,
            NoOrder = true,
            Blocker = PmsShadowStateContract.BrokerAdjustedBlocker
        });
        var inventedResult = PmsShadowEconomicProjectionIntegrityVerifier.Verify(
            current with
            {
                ProjectionRevisionId = inventedId,
                ManifestSha256 = inventedManifest
            });
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.Invalid,
            inventedResult.Status);
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.UnknownProjectionIdentity,
            inventedResult.ProjectionIdentityContractVersion);
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.IdentityMismatch,
            inventedResult.Blockers);
        Assert.Contains(PmsShadowEconomicProjectionIntegrityVerifier.ManifestMismatch,
            inventedResult.Blockers);
    }

    [Fact]
    public void RepositoryAuthorityAcceptsMatchingCleanHead()
    {
        var root = Path.GetFullPath("rpt2-repository-fixture");
        var result = InstitutionalRepositoryStateAuthority.Resolve(root,
            new string('c', 40), new FakeRepositoryProbe(RawRepository(root)));
        Assert.Equal(new string('c', 40), result.ActualHead);
        Assert.True(result.WorktreeClean);
    }

    [Fact]
    public void RepositoryAuthorityRejectsSuppliedCommitMismatch()
    {
        var root = Path.GetFullPath("rpt2-repository-fixture");
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalRepositoryStateAuthority.Resolve(root, new string('e', 40),
                new FakeRepositoryProbe(RawRepository(root))));
        Assert.Equal("RPT2_REPOSITORY_COMMIT_MISMATCH", error.Message);
    }

    [Fact]
    public void RepositoryAuthorityRejectsDirtyWorktree()
    {
        var root = Path.GetFullPath("rpt2-repository-fixture");
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalRepositoryStateAuthority.Resolve(root, new string('c', 40),
                new FakeRepositoryProbe(RawRepository(root) with
                {
                    WorktreeClean = false
                })));
        Assert.Equal("RPT2_REPOSITORY_WORKTREE_NOT_CLEAN", error.Message);
    }

    [Fact]
    public void RepositoryAuthorityRejectsRoadmapOutsideHead()
    {
        var root = Path.GetFullPath("rpt2-repository-fixture");
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalRepositoryStateAuthority.Resolve(root, new string('c', 40),
                new FakeRepositoryProbe(RawRepository(root) with
                {
                    WorktreeRoadmapBlobId = new string('e', 40)
                })));
        Assert.Equal("RPT2_ROADMAP_NOT_AT_HEAD", error.Message);
    }

    [Fact]
    public void RepositoryAuthorityRejectsNonGitRoot()
    {
        var root = Path.GetFullPath("rpt2-repository-fixture");
        var error = Assert.Throws<InvalidDataException>(() =>
            InstitutionalRepositoryStateAuthority.Resolve(root, new string('c', 40),
                new ThrowingRepositoryProbe()));
        Assert.Equal("RPT2_REPOSITORY_ROOT_NOT_GIT", error.Message);
    }

    [Fact]
    public void InstitutionalModeAcceptsExplicitAsOf()
    {
        Assert.Equal(AsOf, ReportingArguments.Parse(
            ReportingArgs(AsOf.ToString("O"))).AsOfUtc);
    }

    [Fact]
    public void InstitutionalModeRejectsMissingExplicitAsOf()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            ReportingArguments.Parse(ReportingArgs(null)));
        Assert.Equal("RPT2_EXPLICIT_AS_OF_REQUIRED", error.Message);
    }

    [Fact]
    public void IdenticalExplicitAsOfArgumentsAreDeterministic()
    {
        var first = ReportingArguments.Parse(ReportingArgs(AsOf.ToString("O")));
        var second = ReportingArguments.Parse(ReportingArgs(AsOf.ToString("O")));
        Assert.Equal(first.AsOfUtc, second.AsOfUtc);
    }

    [Fact]
    public void PositionLineAndCoverageChangesAlterSourceSnapshotSha()
    {
        var projection = Projection(1, "slot-snapshot", InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var lines = PositionLines([projection]).ToArray();
        var baseline = Build([projection], Mappings(), positionLines: lines);
        var changedLines = lines.Skip(1).ToArray();
        var changed = Build([projection], Mappings(), positionLines: changedLines);
        Assert.NotEqual(baseline.SourceSnapshotSha256, changed.SourceSnapshotSha256);
        Assert.NotEqual(
            baseline.SourceSnapshot.PositionSnapshotCoverage[0].CurrentPositionAuthorityDecision,
            changed.SourceSnapshot.PositionSnapshotCoverage[0].CurrentPositionAuthorityDecision);
    }

    [Fact]
    public void ProjectionIntegrityStatusAndRepositoryHeadAlterSourceSnapshotSha()
    {
        var baseline = Build().SourceSnapshot;
        var integrity = baseline.ProjectionIntegrity.ToArray();
        integrity[0] = integrity[0] with { Status = PmsShadowEconomicProjectionIntegrityVerifier.Invalid };
        var changedIntegrity = baseline with { ProjectionIntegrity = integrity };
        Assert.NotEqual(InstitutionalSourceSnapshotContentAddress.ComputeSha256(baseline),
            InstitutionalSourceSnapshotContentAddress.ComputeSha256(changedIntegrity));

        var changedRepository = baseline with
        {
            RepositoryAuthority = baseline.RepositoryAuthority with
            {
                ActualHead = new string('e', 40),
                EvidenceSha256 = new string('f', 64)
            }
        };
        Assert.NotEqual(InstitutionalSourceSnapshotContentAddress.ComputeSha256(baseline),
            InstitutionalSourceSnapshotContentAddress.ComputeSha256(changedRepository));
    }

    public static IEnumerable<object[]> BlockedMetricCodes() =>
        InstitutionalMetricCatalog.BlockedMetrics().Select(value => new object[] { value.Code });

    private static InstitutionalMetricReportSet Build(
        string positionAuthority = InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode,
        DateTimeOffset? asOf = null) =>
        Build([Projection(1, "slot-1", positionAuthority),
                Projection(2, "slot-2", positionAuthority)],
            Mappings(), asOf);

    private static InstitutionalMetricReportSet Build(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> projections,
        IEnumerable<PmsShadowSecurityMappingRow> mappings,
        DateTimeOffset? asOf = null,
        IReadOnlyList<ReportingPositionSnapshotLineFact>? positionLines = null)
    {
        var snapshot = new OperationalReportingSnapshot(
            asOf ?? AsOf,
            new string('c', 40),
            Database(),
            [], [], [], [], [], [], [], [])
        {
            SlotManifestSha256BySlotId = SlotManifests(projections.ToArray()),
            EconomicProjectionSources = projections,
            SecurityMappingSources = mappings.ToArray(),
            PositionSnapshotLineSources = positionLines ?? PositionLines(projections),
            RepositoryAuthority = RepositoryAuthority()
        };
        return InstitutionalMetricProjector.Build(snapshot, RoadmapSha);
    }

    private static OperationalReportingSnapshot SnapshotWithSlots(
        DateTimeOffset asOf,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> projections,
        IReadOnlyList<ReportingSlotFact> slots) =>
        new(asOf, new string('c', 40), Database(), slots, [], [], [], [], [], [], [])
        {
            SlotManifestSha256BySlotId = SlotManifests(projections.ToArray()),
            EconomicProjectionSources = projections,
            SecurityMappingSources = Mappings(),
            PositionSnapshotLineSources = PositionLines(projections),
            RepositoryAuthority = RepositoryAuthority()
        };

    private static ReportingSlotFact Slot(
        PmsShadowIntradayEconomicProjection revision,
        string status) =>
        new(revision.SlotId, revision.SlotStartUtc, revision.SlotEndUtc, status,
            revision.SlotStartUtc, status == "COMPLETED" ? revision.CompletedAtUtc : null,
            revision.SourceSessionId, revision.RawCaptureSha256, ReportingAuthority.Proven,
            99, 99, 0, 0, status == "COMPLETED" ? "READY" : "ABSENT",
            1d, 2d, revision.RevisionNumber, revision.Qualifying, true,
            revision.ManifestSha256, status == "MISSED" ? "TEST_MISSED" : null,
            OperationalReportingContract.Version, ReportingSlotManifestReader.Read(null),
            new(revision.SlotId, status == "COMPLETED" ? "READY" : "ABSENT",
                status == "COMPLETED" ? ReportingAuthority.Proven : ReportingAuthority.Absent,
                revision.RawCaptureSha256, revision.CompletedAtUtc,
                OperationalReportingContract.Version));

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
                var current = index == 0 ? 0m : (index + 1m) / 20m;
                drifts.Add(new(Id(revision * 20000 + ordinal), Id(5000 + strategyIndex),
                    modelId, strategy, instrumentId, $"PMS-{index:D3}",
                    current, notional / 10m, notional / 10m - current,
                    AsOf.AddMinutes(-5), Sha, Sha));
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
        var completed = AsOf.AddMinutes(revision == 1 ? -19 : -4);
        var slotEnd = AsOf.AddMinutes(revision == 1 ? -20 : -5);
        return Rehash(new(Id(3000 + revision), revision, slotId,
            slotEnd.AddMinutes(-15), slotEnd, Sha, Id(6000 + revision),
            Sha, IngestionId, "session-rpt2", Id(7000), Id(7001),
            AsOf.AddMinutes(-30), positionAuthority,
            selected.Select(value => value.ModelRunId).ToArray(),
            selected.Select(value => value.QubesInputSnapshotId).ToArray(), selected, market,
            targets, drifts, Sha, Sha, Sha, Sha, revision == 1 ? null : Sha,
            "COMPLETED", "COMPLETED", true, true, completed));
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
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["S1"] = "INFX7", ["S2"] = "INFX8",
            ["S3"] = "INFX9", ["S4"] = "INFX10"
        };
        var pending = specs.Select(value => value with
            {
                Strategy = aliases.GetValueOrDefault(value.Strategy) ?? value.Strategy
            })
            .GroupBy(value => value.Strategy, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new Queue<TargetSpec>(group),
                StringComparer.Ordinal);
        var template = Projection(revision, slotId, InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode);
        var symbols = new string[template.TargetPositions.Count];
        var targets = template.TargetPositions.Select((target, index) =>
        {
            var spec = pending.GetValueOrDefault(target.StrategyId) is { Count: > 0 } queue
                ? queue.Dequeue()
                : new TargetSpec(target.StrategyId, $"Z{index:D5}", 0m);
            var instrumentId = Id(300000 + ingestionOrdinal * 1000 + index +
                                  instrumentOffset);
            symbols[index] = spec.Symbol;
            return target with
            {
                TargetPositionId = Id(500000 + ingestionOrdinal * 1000 + index),
                InstrumentId = instrumentId,
                SecurityId = $"PMS-{ingestionOrdinal}-{index}",
                TargetNotionalUsd = spec.Notional,
                TargetBaseQuantity = spec.Notional / 10m,
                TargetVenueQuantity = spec.Notional / 10m
            };
        }).ToArray();
        if (pending.Values.Any(value => value.Count > 0))
            throw new InvalidDataException("TEST_TARGET_SPEC_EXCEEDS_MODEL_COUNT");
        var mappings = targets.Select((target, index) =>
        {
            return new PmsShadowSecurityMappingRow(ingestionId, target.InstrumentId,
                Id(400000), Id(410000 + ingestionOrdinal * 1000 + index),
                target.SecurityId, symbols[index], $"LMAX-{ingestionOrdinal}-{index}",
                1m, 0.01m, 0.00001m, Sha);
        }).ToArray();
        var drifts = template.PositionOnlyDrifts.Select((drift, index) =>
        {
            var target = targets[index];
            return drift with
            {
                DriftId = Id(700000 + ingestionOrdinal * 1000 + index),
                InstrumentId = target.InstrumentId,
                SecurityId = target.SecurityId,
                CurrentBaseQuantity = 0m,
                TargetBaseQuantity = target.TargetBaseQuantity,
                Delta = target.TargetBaseQuantity
            };
        }).ToArray();
        var slotEnd = AsOf.AddMinutes(-20 + 15 * (ingestionOrdinal - 1));
        var projection = Rehash(template with
        {
            SlotStartUtc = slotEnd.AddMinutes(-15),
            SlotEndUtc = slotEnd,
            CompletedAtUtc = slotEnd.AddMinutes(1),
            SourceIngestionId = ingestionId,
            SourceSessionId = $"session-{ingestionOrdinal}",
            AccountSnapshotId = Id(810000 + ingestionOrdinal),
            PositionSnapshotId = Id(820000 + ingestionOrdinal),
            TargetPositions = targets,
            PositionOnlyDrifts = drifts
        });
        return new(projection, mappings);
    }

    private static PmsShadowIntradayEconomicProjection Rehash(
        PmsShadowIntradayEconomicProjection projection)
    {
        var integrity = PmsShadowEconomicProjectionIntegrityVerifier.Verify(projection);
        return projection with
        {
            ProjectionRevisionId = integrity.RecalculatedProjectionRevisionId,
            MarketDataSnapshotSha256 = integrity.RecalculatedMarketDataSnapshotSha256,
            TargetPositionsSha256 = integrity.RecalculatedTargetPositionsSha256,
            DriftsSha256 = integrity.RecalculatedDriftsSha256,
            InputSha256 = integrity.RecalculatedInputSha256,
            ManifestSha256 = integrity.RecalculatedManifestSha256
        };
    }

    private static IReadOnlyList<ReportingPositionSnapshotLineFact> PositionLines(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> projections) =>
        projections.GroupBy(value => value.PositionSnapshotId)
            .SelectMany(group =>
            {
                var revision = group.First();
                return group.SelectMany(value => value.PositionOnlyDrifts)
                    .GroupBy(value => value.InstrumentId)
                    .Select(instrument =>
                    {
                        var drift = instrument.First();
                        var rowIdentity =
                            $"{revision.PositionSnapshotId:D}:{drift.InstrumentId:D}";
                        return new ReportingPositionSnapshotLineFact(
                            revision.PositionSnapshotId,
                            drift.InstrumentId,
                            drift.SecurityId,
                            drift.SecurityId,
                            drift.CurrentBaseQuantity,
                            revision.SourceIngestionId,
                            rowIdentity,
                            revision.PositionSnapshotAsOfUtc,
                            Sha);
                    });
            })
            .OrderBy(value => value.PositionSnapshotId)
            .ThenBy(value => value.InstrumentId)
            .ToArray();

    private static InstitutionalRepositoryStateAuthorityResult RepositoryAuthority() =>
        new(InstitutionalRepositoryStateAuthority.ContractVersion, Sha,
            new string('c', 40), true, true, true, new string('d', 40),
            new string('c', 40), Sha);

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

    private static InstitutionalRepositoryRawState RawRepository(string root) =>
        new(root, new string('c', 40), true, true, true,
            new string('d', 40), new string('d', 40), null);

    private sealed class FakeRepositoryProbe(InstitutionalRepositoryRawState state)
        : IInstitutionalRepositoryStateProbe
    {
        public InstitutionalRepositoryRawState Read(string repositoryRoot,
            string roadmapRelativePath) => state;
    }

    private sealed class ThrowingRepositoryProbe : IInstitutionalRepositoryStateProbe
    {
        public InstitutionalRepositoryRawState Read(string repositoryRoot,
            string roadmapRelativePath) =>
            throw new InvalidDataException("RPT2_REPOSITORY_ROOT_NOT_GIT");
    }

    private static string[] ReportingArgs(string? asOf)
    {
        var values = new List<string>
        {
            "report-institutional-metric-foundation", "--no-order",
            "--expected-environment", "TEST",
            "--expected-database", "qq_pms_shadow_arch7b_test",
            "--expected-schema", "pms_shadow",
            "--expected-postgresql-major", "18",
            "--target-profile", "ARCH7B_RDS_TEST",
            "--expected-target-fingerprint", Sha,
            "--output-directory", "rpt2-output",
            "--repository-commit", new string('c', 40),
            "--repository-root", "."
        };
        if (asOf is not null)
        {
            values.Add("--as-of-utc");
            values.Add(asOf);
        }
        return values.ToArray();
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
