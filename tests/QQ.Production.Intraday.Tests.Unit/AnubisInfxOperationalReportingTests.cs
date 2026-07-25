using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.OperationalReporting;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class AnubisInfxOperationalReportingTests
{
    private static readonly DateTimeOffset SlotStart =
        new(2026, 7, 24, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SlotEnd = SlotStart.AddMinutes(15);
    private static readonly DateTimeOffset AsOf = SlotEnd.AddMinutes(1);
    private static readonly Guid RevisionId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly string Sha = new('a', 64);

    [Fact]
    public void T01_empty_arch7b_lifecycle_is_absent_without_false_go()
    {
        var report = Build();

        Assert.Equal(ReportingAuthority.Absent, report.Reconciliation.Status);
        Assert.DoesNotContain(report.Breaks,
            value => value.Category == "RECONCILIATION" &&
                     value.Severity == OperationalBreakSeverity.Critical);
        Assert.Equal(ReportingAuthority.Absent, Assert.Single(report.Arch7b).Status);
    }

    [Fact]
    public void T02_completed_healthy_slot_has_no_capture_or_import_break()
    {
        var report = Build();

        Assert.DoesNotContain(report.Breaks,
            value => value.Category is "CAPTURE" or "HANDOFF");
        Assert.Equal("slot-1", report.Summary.LatestSlot);
    }

    [Fact]
    public void T03_missed_slot_emits_exact_break()
    {
        var snapshot = Healthy() with
        {
            Slots = [Slot(status: "MISSED", manifestSha: null, qualifying: null)]
        };

        var item = Assert.Single(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "INTRADAY_SLOT_MISSING");

        Assert.Equal(OperationalBreakSeverity.Error, item.Severity);
        Assert.True(item.BlocksTrading);
    }

    [Fact]
    public void T04_stale_latest_slot_emits_exact_warning()
    {
        var snapshot = Healthy() with { AsOfUtc = SlotEnd.AddHours(2) };

        var item = Assert.Single(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "INTRADAY_SLOT_STALE");

        Assert.Equal(OperationalBreakSeverity.Warning, item.Severity);
        Assert.Equal(ReportingAuthority.Stale,
            OperationalReportProjector.Build(snapshot).Summary.SourceFreshness);
    }

    [Fact]
    public void T05_scheduled_reused_model_is_not_reported_stale()
    {
        var snapshot = Healthy();

        var report = OperationalReportProjector.Build(snapshot);

        Assert.All(report.ModelRuns,
            value => Assert.Equal("REUSED_FINALIZED_D1_MODEL", value.FreshOrReusedStatus));
        Assert.DoesNotContain(report.Breaks, value => value.ExactCode.Contains(
            "STALE", StringComparison.Ordinal) && value.Component == "ANUBIS_INFX");
    }

    [Fact]
    public void T06_incomplete_model_lineage_emits_break()
    {
        var snapshot = Healthy();
        var models = snapshot.ModelRuns.ToArray();
        models[0] = models[0] with { LineageComplete = false };

        var item = Assert.Single(OperationalReportProjector.Build(
            snapshot with { ModelRuns = models }).Breaks,
            value => value.ExactCode == "MODEL_RUN_LINEAGE_INCOMPLETE");

        Assert.Equal("INFX7", item.StrategyId);
    }

    [Fact]
    public void T07_expected_99_288_288_4_counts_pass()
    {
        var report = Build();

        Assert.DoesNotContain(report.Breaks, value =>
            value.ExactCode is "MARKET_DATA_OBSERVATION_COUNT_MISMATCH" or
                "TARGET_POSITION_COUNT_MISMATCH" or
                "POSITION_ONLY_DRIFT_COUNT_MISMATCH" or
                "FOUR_REQUIRED_MODEL_RUNS_MISSING");
    }

    [Theory]
    [InlineData(98, 288, 288, 4, "MARKET_DATA_OBSERVATION_COUNT_MISMATCH")]
    [InlineData(99, 287, 288, 4, "TARGET_POSITION_COUNT_MISMATCH")]
    [InlineData(99, 288, 287, 4, "POSITION_ONLY_DRIFT_COUNT_MISMATCH")]
    [InlineData(99, 288, 288, 3, "FOUR_REQUIRED_MODEL_RUNS_MISSING")]
    public void T08_incorrect_economic_count_emits_deterministic_break(
        int observations,
        int targets,
        int drifts,
        int models,
        string expectedCode)
    {
        var snapshot = Healthy();
        var revision = snapshot.EconomicRevisions.Single() with
        {
            ObservationCount = observations,
            TargetPositionCount = targets,
            PositionOnlyDriftCount = drifts,
            ModelRunCount = models
        };

        Assert.Contains(OperationalReportProjector.Build(
            snapshot with { EconomicRevisions = [revision] }).Breaks,
            value => value.ExactCode == expectedCode);
    }

    [Fact]
    public void T09_pms_security_and_lmax_instrument_ids_remain_distinct()
    {
        var line = Healthy().FxLines.Single(value =>
            value.PmsSecurityId == "68" && value.StrategyId == "INFX7");

        Assert.Equal("4002", line.LmaxInstrumentId);
        Assert.NotEqual(line.PmsSecurityId, line.LmaxInstrumentId);
        Assert.Equal(28, Healthy().FxLines.Count);
        Assert.Equal(
            OperationalReportingContract.FxSymbols.Order(StringComparer.Ordinal),
            Healthy().FxLines.Select(value => value.CanonicalSymbol)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.All(Healthy().FxLines, value => Assert.NotEqual(Guid.Empty, value.InstrumentId));
    }

    [Fact]
    public void T10_arch7a_canonical_test_shadow_contract_has_no_safety_break()
    {
        var report = Build();

        Assert.Equal(7, report.Arch7a.Count);
        Assert.All(report.Arch7a, value =>
        {
            Assert.Equal("TEST", value.Environment);
            Assert.Equal("1754288005", value.AccountScope);
            Assert.Equal("SHADOW_ONLY", value.Classification);
            Assert.False(value.BrokerRouteAllowed);
            Assert.False(value.BrokerSendAllowed);
        });
        Assert.DoesNotContain(report.Breaks,
            value => value.ExactCode == "ARCH7A_NO_ORDER_INVARIANT_REQUIRED");
    }

    [Fact]
    public void T11_noncanonical_arch7a_contract_is_critical()
    {
        var snapshot = Healthy();
        var rows = snapshot.Arch7a.ToArray();
        rows[0] = rows[0] with { BrokerSendAllowed = true };

        var item = Assert.Single(OperationalReportProjector.Build(
            snapshot with { Arch7a = rows }).Breaks,
            value => value.ExactCode == "ARCH7A_NO_ORDER_INVARIANT_REQUIRED");

        Assert.Equal(OperationalBreakSeverity.Critical, item.Severity);
    }

    [Fact]
    public void T12_known_leaves_nonzero_is_critical()
    {
        var snapshot = Healthy() with
        {
            Arch7b = [Lifecycle(knownLeaves: 1m, ledger: 0m, broker: 0m, reconciliationCount: 1)]
        };

        Assert.Contains(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "ARCH7B_FINAL_RECONCILIATION_NOT_FLAT" &&
                     value.Severity == OperationalBreakSeverity.Critical);
    }

    [Fact]
    public void T13_nonzero_ledger_is_critical()
    {
        var snapshot = Healthy() with
        {
            Arch7b = [Lifecycle(knownLeaves: 0m, ledger: 5m, broker: 0m, reconciliationCount: 1)]
        };

        Assert.Contains(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "ARCH7B_FINAL_RECONCILIATION_NOT_FLAT" &&
                     value.BlocksAccounting);
    }

    [Fact]
    public void T14_broker_flat_not_proven_is_unknown_and_blocking()
    {
        var snapshot = Healthy() with
        {
            Arch7b = [Lifecycle(null, null, null, reconciliationCount: 0)]
        };

        var item = Assert.Single(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "ARCH7B_FLATTEN_NOT_CONFIRMED");

        Assert.Equal(OperationalBreakStatus.Unknown, item.Status);
        Assert.True(item.BlocksTrading);
    }

    [Fact]
    public void T15_unknown_order_source_code_is_critical()
    {
        var snapshot = Healthy() with { ObservedCodes = ["ARCH7B_UNKNOWN_CLORDID"] };

        var item = Assert.Single(OperationalReportProjector.Build(snapshot).Breaks,
            value => value.ExactCode == "ARCH7B_UNKNOWN_CLORDID");

        Assert.Equal(OperationalBreakSeverity.Critical, item.Severity);
    }

    [Fact]
    public void T16_break_identity_and_order_are_deterministic()
    {
        var first = OperationalReportProjector.Build(Healthy() with
        {
            ObservedCodes = ["ARCH7B_UNKNOWN_ORDERID", "ARCH7B_UNKNOWN_CLORDID"]
        }).Breaks;
        var second = OperationalReportProjector.Build(Healthy() with
        {
            ObservedCodes = ["ARCH7B_UNKNOWN_CLORDID", "ARCH7B_UNKNOWN_ORDERID"]
        }).Breaks;

        Assert.Equal(first.Select(value => value.BreakId),
            second.Select(value => value.BreakId));
        Assert.All(first, value => Assert.Equal(64, value.BreakId.Length));
    }

    [Fact]
    public void T17_bundle_is_byte_deterministic_for_same_snapshot_and_as_of()
    {
        var firstRoot = TemporaryDirectory();
        var secondRoot = TemporaryDirectory();
        try
        {
            var report = Build();
            var first = DeterministicReportingBundleWriter.Write(report, firstRoot);
            var second = DeterministicReportingBundleWriter.Write(report, secondRoot);

            Assert.Equal(first.BundleSha256, second.BundleSha256);
            Assert.Equal(first.Files.Select(value => value.Path),
                second.Files.Select(value => value.Path));
            foreach (var file in first.Files)
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstRoot, file.Path)),
                    File.ReadAllBytes(Path.Combine(secondRoot, file.Path)));
        }
        finally
        {
            Directory.Delete(firstRoot, true);
            Directory.Delete(secondRoot, true);
        }
    }

    [Fact]
    public void T18_csv_contract_has_stable_headers_utf8_and_explicit_null()
    {
        var root = TemporaryDirectory();
        try
        {
            DeterministicReportingBundleWriter.Write(Build(), root);
            var csv = File.ReadAllText(Path.Combine(root, "arch7b-lifecycle.csv"));

            Assert.StartsWith("QualificationRunId,Status,AuthorityStatus,", csv);
            Assert.Contains("NULL,ABSENT,ABSENT,NULL", csv);
            Assert.DoesNotContain('\r', csv);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T19_html_is_local_and_has_no_external_resource()
    {
        var root = TemporaryDirectory();
        try
        {
            DeterministicReportingBundleWriter.Write(Build(), root);
            var html = File.ReadAllText(Path.Combine(root, "report.html"));

            Assert.Contains("<!doctype html>", html);
            Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T20_bundle_contains_required_formats_and_no_secret_value()
    {
        var root = TemporaryDirectory();
        try
        {
            var result = DeterministicReportingBundleWriter.Write(Build(), root);
            var names = result.Files.Select(value => value.Path).ToHashSet(StringComparer.Ordinal);

            Assert.Subset(names, new HashSet<string>([
                "operational-summary.json", "breaks.json", "breaks.csv",
                "infx-model-runs.csv", "slots.csv", "economic-revisions.csv",
                "fx-lines.csv", "arch7a.csv", "arch7b-lifecycle.csv",
                "reconciliation.json", "report.html", "manifest.json"
            ], StringComparer.Ordinal));
            Assert.DoesNotContain(Directory.EnumerateFiles(root)
                    .SelectMany(File.ReadAllLines),
                line => line.Contains("DO_NOT_EXPORT_SECRET", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T21_status_catalog_is_versioned_unique_and_source_exact()
    {
        var catalog = OperationalStatusCodeCatalog.All;

        Assert.Equal(catalog.Count, catalog.Select(value => value.ExactCode)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog, value =>
        {
            Assert.NotEmpty(value.IntroducedByContractVersion);
            Assert.NotEmpty(value.OperatorMeaning);
            Assert.NotEmpty(value.EvidenceRequirements);
        });
        Assert.Contains(catalog, value => value.ExactCode == "ARCH7B_UNKNOWN_CLORDID");
        Assert.Contains(catalog, value => value.ExactCode == "INTRADAY_SLOT_MISSING");
        Assert.Contains(catalog,
            value => value.ExactCode == "NONQUALIFYING_SLOT_ATTEMPT_MISSING");
    }

    [Fact]
    public void T22_break_formula_uses_all_identity_dimensions()
    {
        var first = OperationalReportProjector.BreakId(
            "INTRADAY_SLOT_MISSING", "SLOT", "Slot", "slot-1", "slot-1");
        var same = OperationalReportProjector.BreakId(
            "INTRADAY_SLOT_MISSING", "SLOT", "Slot", "slot-1", "slot-1");
        var different = OperationalReportProjector.BreakId(
            "INTRADAY_SLOT_MISSING", "SLOT", "Slot", "slot-2", "slot-2");

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }

    private static OperationalReportSet Build()
        => OperationalReportProjector.Build(Healthy());

    private static OperationalReportingSnapshot Healthy()
    {
        var models = OperationalReportingContract.Strategies.Select((strategy, index) =>
            new ReportingModelRunFact(
                strategy,
                GuidFrom(index + 10),
                GuidFrom(index + 20),
                SlotEnd,
                SlotEnd.AddDays(-1),
                Sha,
                new string('b', 40),
                "REUSED_FINALIZED_D1_MODEL",
                "REUSED_FINALIZED_D1_MODEL",
                "DUE_OR_FINALIZED",
                72,
                72,
                72,
                true,
                PmsShadowStateContract.ContractVersion)).ToArray();
        var fx = new List<ReportingFxLineFact>();
        foreach (var (symbol, symbolIndex) in OperationalReportingContract.FxSymbols.Select(
                     (value, index) => (value, index)))
        foreach (var (strategy, strategyIndex) in OperationalReportingContract.Strategies.Select(
                     (value, index) => (value, index)))
        {
            var pms = symbol == "EURUSD" ? "68" : (100 + symbolIndex).ToString();
            var lmax = symbol == "EURUSD" ? "4002" : (5000 + symbolIndex).ToString();
            fx.Add(new(
                RevisionId,
                GuidFrom(100 + symbolIndex),
                pms,
                symbol,
                lmax,
                "8",
                strategy,
                strategyIndex + 1,
                strategyIndex + 1,
                0m,
                strategyIndex + 1,
                10m,
                10m,
                ReportingAuthority.Proven,
                1.1m,
                1.2m,
                SlotEnd,
                ReportingAuthority.Proven));
        }
        var arch7a = OperationalReportingContract.FxSymbols.Select((symbol, index) =>
            new ReportingArch7aFact(
                RevisionId,
                GuidFrom(200 + index),
                GuidFrom(220 + index),
                GuidFrom(240 + index),
                GuidFrom(260 + index),
                "1754288005",
                "TEST",
                "SHADOW_ONLY",
                "SHADOW_PLANNED",
                "SHADOW_PLANNED",
                true,
                false,
                false,
                false,
                Sha,
                "QUALIFICATION_RUN_PRESENT",
                symbol,
                GuidFrom(100 + index))).ToArray();
        return new(
            AsOf,
            new string('c', 40),
            Database(),
            [Slot()],
            models,
            [new(
                RevisionId,
                2,
                "slot-1",
                GuidFrom(2),
                "session-1",
                Sha,
                new string('d', 64),
                "COMPLETED",
                true,
                true,
                99,
                288,
                288,
                4,
                Sha,
                Sha,
                Sha,
                SlotEnd.AddMinutes(1))],
            fx,
            arch7a,
            [new(null, ReportingAuthority.Absent, ReportingAuthority.Absent,
                null, null, 0, 0, 0, 0, 0, 0, null, null, null, null,
                ReportingAuthority.Absent, null)],
            []);
    }

    private static ReportingDatabaseIdentity Database() => new(
        "qq_pms_shadow_arch7b_test",
        "PostgreSQL 18.4",
        18,
        "pms_shadow",
        35,
        7291,
        PmsShadowStateContract.MigrationIds,
        true,
        false,
        "ARCH7B_RDS_TEST",
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
        "REMOTE_TLS",
        "VERIFYFULL");

    private static ReportingSlotFact Slot(
        string status = "COMPLETED",
        string? manifestSha = null,
        bool? qualifying = true) => new(
        "slot-1",
        SlotStart,
        SlotEnd,
        status,
        SlotStart,
        SlotEnd.AddMinutes(1),
        "session-1",
        Sha,
        ReportingAuthority.Proven,
        49,
        49,
        2,
        0,
        ReportingAuthority.Proven,
        5,
        60,
        2,
        qualifying,
        true,
        manifestSha ?? Sha,
        null,
        "pms_shadow_intraday_cadence_v1");

    private static ReportingArch7bFact Lifecycle(
        decimal? knownLeaves,
        decimal? ledger,
        decimal? broker,
        int reconciliationCount) => new(
        GuidFrom(500),
        reconciliationCount == 0 ? "LIFECYCLE_REGISTERED" : "NOT_FLAT",
        ReportingAuthority.Proven,
        Sha,
        AsOf.AddMinutes(5),
        2,
        2,
        2,
        1,
        1,
        reconciliationCount,
        knownLeaves,
        ledger,
        broker,
        reconciliationCount == 0 ? null : 1,
        ReportingAuthority.Unknown,
        reconciliationCount == 0 ? null : AsOf);

    private static Guid GuidFrom(int value)
        => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qq-reporting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
