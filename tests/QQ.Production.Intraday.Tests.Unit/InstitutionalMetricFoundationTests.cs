using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.OperationalReporting;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class InstitutionalMetricFoundationTests
{
    private const string Sha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RoadmapSha =
        "90d740d12a210e1df118523b7bb885e84aafaddc72c7570d6009cc54bad2d363";
    private static readonly DateTimeOffset AsOf =
        new(2026, 7, 26, 0, 35, 0, TimeSpan.Zero);
    private static readonly Guid IngestionId = Id(1);
    private static readonly string[] Symbols =
        ["AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY"];

    [Fact]
    public void CatalogHasVersionAndStableUniqueCodes()
    {
        var catalog = InstitutionalMetricCatalog.Build();
        Assert.Equal("anubis_infx_institutional_metric_catalog_v1",
            InstitutionalMetricContract.CatalogVersion);
        Assert.Equal(40, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Select(value => value.MetricCode).Distinct().Count());
        Assert.All(catalog, value => Assert.Equal("RPT2", value.RptPhase));
    }

    [Fact]
    public void FourInfxSetsHaveExactCounts()
    {
        var report = Build();
        Assert.Equal(4, report.DataQuality.SelectedInfxCounts.Count);
        Assert.Equal(66, report.DataQuality.SelectedInfxCounts["INFX7"]);
        Assert.Equal(66, report.DataQuality.SelectedInfxCounts["INFX8"]);
        Assert.Equal(78, report.DataQuality.SelectedInfxCounts["INFX9"]);
        Assert.Equal(78, report.DataQuality.SelectedInfxCounts["INFX10"]);
        Assert.True(report.DataQuality.SelectedInfxComplete);
    }

    [Fact]
    public void LatestRevisionHasRequiredEconomicCounts()
    {
        var quality = Build().DataQuality;
        Assert.Equal(99, quality.LatestMarketObservationCount);
        Assert.Equal(288, quality.LatestTargetPositionCount);
        Assert.Equal(288, quality.LatestPositionOnlyDriftCount);
    }

    [Fact]
    public void GrossNetLongAndShortFollowVersionedFormula()
    {
        var row = Build().ExposureByRevision.Last();
        Assert.True(row.GrossTargetNotionalUsd >= Math.Abs(row.NetTargetNotionalUsd));
        Assert.Equal(row.GrossTargetNotionalUsd,
            row.LongTargetNotionalUsd + row.ShortTargetNotionalUsd);
        Assert.Equal(row.NetTargetNotionalUsd,
            row.LongTargetNotionalUsd - row.ShortTargetNotionalUsd);
        Assert.Equal(InstitutionalMetricContract.ExposureFormula, row.FormulaVersion);
    }

    [Fact]
    public void PairAndStrategyConcentrationsAreBounded()
    {
        var report = Build();
        Assert.All(report.Concentrations.Where(value => value.Concentration.HasValue),
            value => Assert.InRange(value.Concentration!.Value, 0m, 1m));
        Assert.Contains(report.Concentrations, value => value.DimensionType == "PAIR");
        Assert.Contains(report.Concentrations, value => value.DimensionType == "STRATEGY");
    }

    [Fact]
    public void PmsSecurityAndLmaxIdentityRemainSeparate()
    {
        var pair = Build().ExposureByPair.First();
        Assert.NotNull(pair.PmsSecurityId);
        Assert.NotNull(pair.LmaxInstrumentId);
        Assert.NotEqual(pair.PmsSecurityId, pair.LmaxInstrumentId);
        Assert.NotEqual(Guid.Empty, pair.InstrumentId);
    }

    [Fact]
    public void UsdJpyCurrencyLegsPreserveCanonicalInversion()
    {
        var report = Build();
        var latest = report.ExposureByPair.Last(value => value.CanonicalSymbol == "USDJPY");
        var usd = report.ExposureByCurrency.Single(value =>
            value.EconomicRevisionId == latest.EconomicRevisionId && value.Currency == "USD");
        var jpy = report.ExposureByCurrency.Single(value =>
            value.EconomicRevisionId == latest.EconomicRevisionId && value.Currency == "JPY");
        Assert.True(usd.SourceTargetCount >= 1);
        Assert.True(jpy.SourceTargetCount >= 1);
        Assert.Equal(InstitutionalMetricContract.CurrencyFormula, usd.FormulaVersion);
        Assert.Equal(InstitutionalMetricContract.CurrencyFormula, jpy.FormulaVersion);
    }

    [Fact]
    public void TurnoverUsesSuccessiveQualifyingRevisionsOnly()
    {
        var rows = Build().Turnover;
        Assert.NotEmpty(rows);
        Assert.All(rows, value => Assert.Equal("TARGET_TURNOVER", value.MetricCode));
        Assert.All(rows, value => Assert.Equal(
            MetricAvailabilityStatus.DerivableProven, value.AvailabilityStatus));
        Assert.DoesNotContain(rows, value => value.MetricCode.Contains("EXECUTED",
            StringComparison.Ordinal));
    }

    [Fact]
    public void TurnoverClassifiesNewClosedIncreaseReductionAndInversion()
    {
        var total = Build().Turnover.Single(value =>
            value.DimensionType == "TOTAL" && value.EconomicRevisionId == Id(3002));
        Assert.True(total.NewTargetCount > 0);
        Assert.True(total.ClosedTargetCount > 0);
        Assert.True(total.IncreaseCount > 0);
        Assert.True(total.ReductionCount > 0);
        Assert.True(total.InversionCount > 0);
    }

    [Fact]
    public void PositionAuthorityAbsentDoesNotBecomeZero()
    {
        var report = Build(positionAuthority: string.Empty);
        Assert.Equal(ReportingAuthority.Absent, report.DataQuality.PositionAuthority);
        Assert.All(report.DriftByPair, value =>
            Assert.Equal(MetricAvailabilityStatus.BlockedAuthorityUnproven,
                value.AvailabilityStatus));
        Assert.DoesNotContain(report.Availability, value =>
            value.MetricCode == "BROKER_POSITION" && value.Value == 0m);
    }

    [Fact]
    public void LeverageAndPerformanceRemainBlockedWithoutAumNav()
    {
        var availability = Build().Availability;
        AssertBlocked(availability, "LEVERAGE");
        AssertBlocked(availability, "GROSS_PERFORMANCE");
        AssertBlocked(availability, "NET_PERFORMANCE");
        Assert.Equal(ReportingAuthority.Absent, Build().DataQuality.AumNavAuthority);
    }

    [Fact]
    public void FillAndLedgerAbsenceBlockRealizedPnlAndTca()
    {
        var report = Build();
        Assert.Equal(ReportingAuthority.Absent, report.DataQuality.FillAuthority);
        Assert.Equal(ReportingAuthority.Absent, report.DataQuality.LedgerAuthority);
        AssertBlocked(report.Availability, "REALIZED_PNL");
        AssertBlocked(report.Availability, "LIVE_TCA");
    }

    [Fact]
    public void BlockedMetricsNeverCarryNumericValues()
    {
        var blocked = Build().Availability.Where(value =>
            value.AvailabilityStatus == MetricAvailabilityStatus.BlockedMissingSource).ToArray();
        Assert.Equal(26, blocked.Length);
        Assert.All(blocked, value =>
        {
            Assert.Null(value.Value);
            Assert.NotEmpty(value.MissingRequiredFacts);
            Assert.NotEmpty(value.ActivationCondition);
            Assert.NotEmpty(value.Caveat);
        });
    }

    [Theory]
    [MemberData(nameof(BlockedMetricCodes))]
    public void RequiredUnavailableMetricIsExplicitlyBlocked(string metricCode)
    {
        AssertBlocked(Build().Availability, metricCode);
    }

    [Fact]
    public void WriterProducesExactStableFileInventory()
    {
        var path = TemporaryDirectory();
        try
        {
            var bundle = DeterministicInstitutionalMetricBundleWriter.Write(Build(), path);
            Assert.Equal(20, bundle.Files.Count);
            Assert.Equal(ExpectedFiles, bundle.Files.Select(value => value.Path)
                .Order(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            Directory.Delete(path, true);
        }
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
    public void CsvHeadersAndNullLiteralAreStable()
    {
        var path = TemporaryDirectory();
        try
        {
            DeterministicInstitutionalMetricBundleWriter.Write(Build(), path);
            var availability = File.ReadAllLines(Path.Combine(path, "metric-availability.csv"));
            Assert.Equal("MetricCode,AvailabilityStatus,Value,Unit,Currency,MissingRequiredFacts,ActivationCondition,Caveat,AuthorityStatus,DataQualityStatus",
                availability[0]);
            Assert.Contains(",NULL,", availability.Single(value =>
                value.StartsWith("REALIZED_PNL,", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void HtmlHasNoExternalResources()
    {
        var path = TemporaryDirectory();
        try
        {
            DeterministicInstitutionalMetricBundleWriter.Write(Build(), path);
            var html = File.ReadAllText(Path.Combine(path, "report.html"));
            Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void BundleManifestLinksRoadmapAndReadOnlySafety()
    {
        var path = TemporaryDirectory();
        try
        {
            DeterministicInstitutionalMetricBundleWriter.Write(Build(), path);
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(path, "manifest.json")));
            var root = document.RootElement;
            Assert.Equal(RoadmapSha, root.GetProperty("roadmap_sha256").GetString());
            Assert.True(root.GetProperty("read_only").GetBoolean());
            Assert.True(root.GetProperty("no_order").GetBoolean());
            Assert.True(root.GetProperty("no_secrets").GetBoolean());
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void OutputContainsNoSecretOrForbiddenProviderPath()
    {
        var path = TemporaryDirectory();
        try
        {
            DeterministicInstitutionalMetricBundleWriter.Write(Build(), path);
            var text = string.Join('\n', Directory.EnumerateFiles(path)
                .Select(File.ReadAllText));
            Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SecretAccessKey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("databento", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/api/", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void SourceSnapshotIsReadOnlyAndHasNoPendingModelChanges()
    {
        var database = Build().Database;
        Assert.True(database.TransactionReadOnly);
        Assert.False(database.PendingModelChanges);
        Assert.Equal("VERIFYFULL", database.TlsPolicy);
    }

    [Fact]
    public void PowerBiContractsCoverEveryCsv()
    {
        var contracts = Build().PowerBiContracts;
        Assert.Equal(13, contracts.Count);
        Assert.All(contracts, value =>
        {
            Assert.NotEmpty(value.Grain);
            Assert.NotEmpty(value.LogicalPrimaryKey);
            Assert.Contains("NULL", value.NullPolicy, StringComparison.Ordinal);
            Assert.Contains("AsOfUtc", value.AsOfBehavior, StringComparison.Ordinal);
        });
    }

    public static IEnumerable<object[]> BlockedMetricCodes() =>
        InstitutionalMetricCatalog.BlockedMetrics().Select(value => new object[] { value.Code });

    private static InstitutionalMetricReportSet Build(string positionAuthority = "PMS_SNAPSHOT")
    {
        var snapshot = new OperationalReportingSnapshot(
            AsOf,
            new string('c', 40),
            Database(),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [])
        {
            EconomicProjectionSources =
            [
                Projection(1, positionAuthority),
                Projection(2, positionAuthority)
            ],
            SecurityMappingSources = Mappings()
        };
        return InstitutionalMetricProjector.Build(snapshot, RoadmapSha);
    }

    private static PmsShadowIntradayEconomicProjection Projection(
        int revision,
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
        return new(Id(3000 + revision), revision, $"slot-{revision}",
            completed.AddMinutes(-16), completed.AddMinutes(-1), Sha, Id(6000 + revision),
            Sha, IngestionId, "session-rpt2", Id(7000), Id(7001),
            AsOf.AddMinutes(-30), positionAuthority, selected.Select(value => value.ModelRunId).ToArray(),
            selected.Select(value => value.QubesInputSnapshotId).ToArray(), selected, market,
            targets, drifts, Sha, Sha, Sha, Sha, revision == 1 ? null : Sha,
            "COMPLETED", "COMPLETED", true, true, completed);
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
                0 => 0m,
                1 => 10m,
                2 => 10m,
                3 => 20m,
                4 => 10m,
                _ => ordinal % 2 == 0 ? 100m + ordinal : -50m - ordinal
            };
        return ordinal switch
        {
            0 => 10m,
            1 => 0m,
            2 => 20m,
            3 => 10m,
            4 => -10m,
            _ => ordinal % 2 == 0 ? 110m + ordinal : -45m - ordinal
        };
    }

    private static ReportingDatabaseIdentity Database() => new(
        "qq_pms_shadow_arch7b_test", "PostgreSQL 18.4", 18, "pms_shadow", 35,
        7291, PmsShadowStateContract.MigrationIds, true, false, "ARCH7B_RDS_TEST",
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
        "REMOTE_TLS", "VERIFYFULL");

    private static void AssertBlocked(
        IReadOnlyList<InstitutionalMetricAvailability> availability,
        string code)
    {
        var metric = availability.Single(value => value.MetricCode == code);
        Assert.Equal(MetricAvailabilityStatus.BlockedMissingSource,
            metric.AvailabilityStatus);
        Assert.Null(metric.Value);
    }

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
        "drift-by-model.csv",
        "drift-by-pair.csv",
        "drift-by-strategy.csv",
        "institutional-metric-catalog.json",
        "institutional-reporting-roadmap.json",
        "manifest.json",
        "metric-availability.csv",
        "performance-availability.json",
        "pms-risk-summary.json",
        "report.html",
        "target-concentration.csv",
        "target-exposure-by-currency.csv",
        "target-exposure-by-model.csv",
        "target-exposure-by-pair.csv",
        "target-exposure-by-revision.csv",
        "target-exposure-by-strategy.csv",
        "target-gross-net.csv",
        "target-turnover.csv"
    ];
}
