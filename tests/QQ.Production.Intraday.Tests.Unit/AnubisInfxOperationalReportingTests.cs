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
    public void T01_saturday_is_outside_calendar_without_false_stale()
    {
        var report = Build(Healthy() with
        {
            AsOfUtc = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero)
        });
        Assert.Equal("OUTSIDE_OPERATIONAL_CALENDAR",
            report.OperationalExpectation.SlotDueStatus);
        Assert.DoesNotContain(report.Breaks, value => value.ExactCode == "INTRADAY_SLOT_STALE");
    }

    [Fact]
    public void T02_sunday_is_outside_calendar_without_false_stale()
    {
        var report = Build(Healthy() with
        {
            AsOfUtc = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero)
        });
        Assert.Equal("OUTSIDE_OPERATIONAL_CALENDAR",
            report.OperationalExpectation.MarketCalendarStatus);
        Assert.DoesNotContain(report.Breaks, value => value.ExactCode == "INTRADAY_SLOT_STALE");
    }

    [Fact]
    public void T03_monday_before_first_model_due_is_not_due()
    {
        var status = ReportingInfxSchedules.Status("INFX10",
            new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero),
            new(2026, 7, 24, 11, 6, 0, TimeSpan.Zero), false, "FINALIZED_NOT_SELECTED");
        Assert.Equal("NOT_DUE", status);
    }

    [Fact]
    public void T04_monday_after_model_due_without_selection_is_due_missing()
    {
        var status = ReportingInfxSchedules.Status("INFX10",
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
            new(2026, 7, 24, 11, 6, 0, TimeSpan.Zero), false, "FINALIZED_NOT_SELECTED");
        Assert.Equal("DUE_MISSING", status);
    }

    [Fact]
    public void T05_missed_slot_remains_active_during_weekend()
    {
        var missed = Slot() with { Status = "MISSED", Qualifying = null };
        var report = Build(Healthy() with
        {
            AsOfUtc = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero),
            Slots = [missed]
        });
        Assert.Contains(report.Breaks, value =>
            value.ExactCode == "INTRADAY_SLOT_MISSING" &&
            value.Status == OperationalBreakStatus.Active);
    }

    [Fact]
    public void T06_risk_reason_code_is_not_automatically_a_break()
    {
        var snapshot = Healthy() with
        {
            ObservedCodeFacts = [Observed("LIMIT_CHECKED",
                OperationalFactKinds.RiskReasonCode, false)]
        };
        Assert.DoesNotContain(Build(snapshot).Breaks,
            value => value.SourceExactCode == "LIMIT_CHECKED");
    }

    [Fact]
    public void T07_blocking_risk_fact_keeps_risk_and_intent_scope()
    {
        var fact = Observed("ARCH7A_NO_ORDER_INVARIANT_REQUIRED",
            OperationalFactKinds.RiskBlockingBreak, true);
        var item = Assert.Single(Build(Healthy() with
        {
            ObservedCodeFacts = [fact]
        }).Breaks, value => value.SourceExactCode == fact.SourceExactCode);
        Assert.Equal(fact.TradeIntentId, item.TradeIntentId);
        Assert.Equal(fact.RiskDecisionId, item.RiskDecisionId);
    }

    [Fact]
    public void T08_old_slot_failure_is_historical()
    {
        var old = Slot("slot-old", SlotStart.AddMinutes(-15), SlotEnd.AddMinutes(-15))
            with { Status = "FAILED_CLOSED", FailureCode = "INGESTION_FAILED" };
        var item = Assert.Single(Build(Healthy() with { Slots = [old, Slot()] }).Breaks,
            value => value.SourceExactCode == "INGESTION_FAILED");
        Assert.Equal(OperationalBreakStatus.Historical, item.Status);
    }

    [Fact]
    public void T09_later_authoritative_fact_can_mark_source_resolved()
    {
        var fact = Observed("INGESTION_FAILED", OperationalFactKinds.OperationalAlert, true)
            with { DerivedOperationalStatus = "RESOLVED_BY_LATER_FACT" };
        var item = Assert.Single(Build(Healthy() with
        {
            ObservedCodeFacts = [fact]
        }).Breaks, value => value.SourceExactCode == "INGESTION_FAILED");
        Assert.Equal(OperationalBreakStatus.ResolvedByLaterFact, item.Status);
    }

    [Fact]
    public void T10_same_code_on_two_slots_has_two_break_identities()
    {
        var first = Observed("INGESTION_FAILED", OperationalFactKinds.SlotFailureCode, true)
            with { ScopeType = "Slot", ScopeId = "slot-a", SlotId = "slot-a" };
        var second = first with { ScopeId = "slot-b", SlotId = "slot-b" };
        var rows = Build(Healthy() with { ObservedCodeFacts = [first, second] }).Breaks
            .Where(value => value.SourceExactCode == "INGESTION_FAILED").ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(2, rows.Select(value => value.BreakId).Distinct().Count());
    }

    [Fact]
    public void T11_unknown_source_code_preserves_exact_provenance()
    {
        var item = Assert.Single(Build(Healthy() with
        {
            ObservedCodeFacts =
            [
                Observed("ARCH_NEW_BLOCKER", OperationalFactKinds.LifecycleBreak, true)
            ]
        }).Breaks, value => value.ExactCode == "REPORTING_UNCATALOGUED_SOURCE_CODE");
        Assert.Equal("ARCH_NEW_BLOCKER", item.SourceExactCode);
        Assert.Equal(OperationalBreakStatus.Unknown, item.Status);
    }

    [Fact]
    public void T12_legacy_manifest_fields_are_unknown_not_pass()
    {
        var manifest = ReportingSlotManifestReader.Read(
            $$"""{"LmaxCaptureSha256":"{{Sha}}","ClockPreflightStatus":"PASS","BboCoverageCount":49}""");
        Assert.Null(manifest.ContractVersion);
        Assert.Equal(ReportingAuthority.Unknown, manifest.AuthorityStatus);
    }

    [Fact]
    public void T13_exact_pr38_manifest_contract_is_proven()
    {
        var manifest = ReportingSlotManifestReader.Read(ManifestJson());
        Assert.Equal(49, manifest.BboSymbolCount);
        Assert.Equal(ReportingAuthority.Proven, manifest.AuthorityStatus);
        Assert.Equal(Sha, manifest.ClockPostCloseSnapshotSha256);
    }

    [Fact]
    public void T14_ready_marker_absence_is_unknown_and_never_inferred()
    {
        var slot = Slot() with
        {
            ReadyMarker = new("slot-1", ReportingAuthority.Absent,
                ReportingAuthority.Absent, null, null, "ready_v1"),
            ReadyMarkerStatus = ReportingAuthority.Absent
        };
        Assert.Contains(Build(Healthy() with { Slots = [slot] }).Breaks, value =>
            value.ExactCode == "READY_MARKER_NOT_PROVEN" &&
            value.Status == OperationalBreakStatus.Unknown);
    }

    [Fact]
    public void T15_selected_model_set_is_exactly_infx7_to_infx10()
    {
        var report = Build();
        Assert.Equal(OperationalReportingContract.Strategies,
            report.ModelRuns.Select(value => value.StrategyId));
        Assert.DoesNotContain(report.Breaks, value =>
            value.ExactCode.StartsWith("SELECTED_STRATEGY_", StringComparison.Ordinal));
    }

    [Fact]
    public void T16_missing_selected_strategy_is_blocking()
    {
        var snapshot = Healthy();
        var report = Build(snapshot with { ModelRuns = snapshot.ModelRuns.Take(3).ToArray() });
        Assert.Contains(report.Breaks, value =>
            value.ExactCode == "SELECTED_STRATEGY_MISSING" &&
            value.StrategyId == "INFX10");
    }

    [Fact]
    public void T17_per_model_counts_are_66_66_78_78()
    {
        var report = Build();
        Assert.Equal([66, 66, 78, 78], report.ModelRuns.Select(value => value.WeightCount));
        Assert.DoesNotContain(report.Breaks,
            value => value.ExactCode == "SELECTED_MODEL_WEIGHT_TARGET_COUNT_MISMATCH");
    }

    [Fact]
    public void T18_global_288_does_not_hide_per_model_mismatch()
    {
        var snapshot = Healthy();
        var models = snapshot.ModelRuns.ToArray();
        models[0] = models[0] with { WeightCount = 65, TargetCount = 67, DriftCount = 67 };
        var report = Build(snapshot with { ModelRuns = models });
        Assert.Equal(288, snapshot.EconomicRevisions.Single().TargetPositionCount);
        Assert.Contains(report.Breaks,
            value => value.ExactCode == "SELECTED_MODEL_WEIGHT_TARGET_COUNT_MISMATCH");
    }

    [Fact]
    public void T19_selected_qubes_input_absence_blocks_lineage()
    {
        var snapshot = Healthy();
        var models = snapshot.ModelRuns.ToArray();
        models[0] = models[0] with { QubesInputSnapshotId = Guid.Empty, LineageComplete = false };
        Assert.Contains(Build(snapshot with { ModelRuns = models }).Breaks,
            value => value.ExactCode == "SELECTED_QUBES_INPUT_MISSING");
    }

    [Fact]
    public void T20_historical_gbpusd_preserves_pms_68_and_lmax_4002()
    {
        var row = Assert.Single(Healthy().FxNetLines,
            value => value.CanonicalSymbol == "GBPUSD");
        Assert.Equal("68", row.PmsSecurityId);
        Assert.Equal("4002", row.LmaxInstrumentId);
        Assert.Equal("8", row.SecurityIdSource);
    }

    [Fact]
    public void T21_fx_net_has_exactly_seven_rows()
    {
        Assert.Equal(7, Build().FxNetLines.Count);
        Assert.Equal(7, Build().FxNetLines.Select(value => value.TradeIntentId).Distinct().Count());
    }

    [Fact]
    public void T22_fx_strategy_contributions_have_exactly_28_rows()
    {
        Assert.Equal(28, Build().FxStrategyContributions.Count);
    }

    [Fact]
    public void T23_current_quantity_is_not_duplicated_in_contribution_contract()
    {
        Assert.Null(typeof(ReportingFxStrategyContributionFact).GetProperty("CurrentQuantity"));
        Assert.Null(typeof(ReportingFxStrategyContributionFact).GetProperty("CurrentBaseQuantity"));
    }

    [Fact]
    public void T24_allocated_quantity_sums_to_net_target()
    {
        var report = Build();
        foreach (var net in report.FxNetLines)
            Assert.Equal(net.TargetQuantity, report.FxStrategyContributions
                .Where(value => value.TradeIntentId == net.TradeIntentId)
                .Sum(value => value.AllocatedExecutionQuantity));
    }

    [Fact]
    public void T25_derived_attribution_is_probable()
    {
        Assert.All(Build().FxStrategyContributions, value =>
        {
            Assert.Equal("PROPORTIONAL_NET_ATTRIBUTION_V1", value.AttributionMethod);
            Assert.Equal(ReportingAuthority.Probable, value.AttributionAuthority);
        });
    }

    [Fact]
    public void T26_unprovable_attribution_is_unknown_not_zero()
    {
        var row = Healthy().FxStrategyContributions[0] with
        {
            AllocatedExecutionQuantity = null,
            AttributionAuthority = ReportingAuthority.Unknown
        };
        Assert.Null(row.AllocatedExecutionQuantity);
        Assert.Equal(ReportingAuthority.Unknown, row.AttributionAuthority);
    }

    [Fact]
    public void T27_source_code_inventory_is_complete()
    {
        var inventory = OperationalStatusCodeScanner.ScanAuthoritativeSource(RepositoryRoot());
        OperationalStatusCodeScanner.RequireComplete(inventory);
        Assert.NotEmpty(inventory);
    }

    [Fact]
    public void T28_new_unclassified_source_code_fails_inventory()
    {
        var item = new OperationalSourceCodeInventoryItem(
            "source.cs", 1, "source.cs:1", "NEW_BLOCKING_CODE", "\"NEW_BLOCKING_CODE\"",
            OperationalFactKinds.StatusCode, "UNCLASSIFIED", null, null);
        Assert.Throws<InvalidDataException>(() =>
            OperationalStatusCodeScanner.RequireComplete([item]));
    }

    [Fact]
    public void T29_global_status_ignores_historical_breaks()
    {
        var fact = Observed("INGESTION_FAILED", OperationalFactKinds.OperationalAlert, true)
            with { DerivedOperationalStatus = "HISTORICAL" };
        var report = Build(Healthy() with { ObservedCodeFacts = [fact] });
        Assert.Equal(OperationalBreakStatus.Historical,
            Assert.Single(report.Breaks, value => value.SourceExactCode == "INGESTION_FAILED").Status);
        Assert.DoesNotContain(report.Summary.AuthorityGaps, value => value == "INGESTION_FAILED");
    }

    [Fact]
    public void T30_absent_arch7b_is_normal_without_false_go()
    {
        var report = Build();
        Assert.Equal(ReportingAuthority.Absent, report.Reconciliation.Status);
        Assert.Empty(report.Arch7b);
    }

    [Fact]
    public void T31_registered_lifecycle_without_reconciliation_blocks_unknown()
    {
        var snapshot = Healthy() with { Arch7b = [Lifecycle()] };
        var item = Assert.Single(Build(snapshot).Breaks,
            value => value.ExactCode == "ARCH7B_FLATTEN_NOT_CONFIRMED");
        Assert.Equal(OperationalBreakStatus.Unknown, item.Status);
        Assert.True(item.BlocksTrading);
    }

    [Fact]
    public void T32_bundle_bytes_are_deterministic()
    {
        var firstRoot = TemporaryDirectory();
        var secondRoot = TemporaryDirectory();
        try
        {
            var first = DeterministicReportingBundleWriter.Write(Build(), firstRoot);
            var second = DeterministicReportingBundleWriter.Write(Build(), secondRoot);
            Assert.Equal(first.BundleSha256, second.BundleSha256);
            Assert.Contains(first.Files, value => value.Path == "fx-net-lines.csv");
            Assert.Contains(first.Files, value => value.Path == "fx-strategy-contributions.csv");
            foreach (var file in first.Files)
                Assert.Equal(File.ReadAllBytes(Path.Combine(firstRoot, file.Path)),
                    File.ReadAllBytes(Path.Combine(secondRoot, file.Path)));
        }
        finally
        {
            Directory.Delete(firstRoot, true);
            Directory.Delete(secondRoot, true);
        }
    }

    [Fact]
    public void T33_database_reader_enforces_repeatable_read_and_read_only_transaction()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.OperationalReporting",
            "PmsShadowReadOnlyReportingReader.cs"));
        Assert.Contains("IsolationLevel.RepeatableRead", source, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", source, StringComparison.Ordinal);
        Assert.Contains("SHOW transaction_read_only", source, StringComparison.Ordinal);
    }

    [Fact]
    public void T34_summary_exposes_true_arch7a_qualification_run_id()
    {
        var report = Build();
        Assert.Equal(GuidFrom(900), report.Summary.LatestArch7aQualificationRunId);
        Assert.Equal(RevisionId, report.Summary.LatestArch7aEconomicRevisionId);
        Assert.Equal("COMPLETED", report.Summary.LatestArch7aQualificationStatus);
        Assert.Equal(SlotEnd.AddMinutes(2),
            report.Summary.LatestArch7aQualificationCompletedAtUtc);
    }

    [Fact]
    public void T35_latest_arch7a_authority_uses_completed_time_not_guid_order()
    {
        var snapshot = Healthy();
        var currentRevision = snapshot.EconomicRevisions.Single();
        var olderRevisionId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var olderQualificationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var olderRevision = currentRevision with
        {
            EconomicRevisionId = olderRevisionId,
            CompletedAtUtc = SlotEnd
        };
        var historical = snapshot.Arch7a.Select(value => value with
        {
            EconomicRevisionId = olderRevisionId,
            QualificationRunId = olderQualificationId,
            QualificationCompletedAtUtc = SlotEnd
        }).ToArray();
        var report = Build(snapshot with
        {
            EconomicRevisions = [olderRevision, currentRevision],
            Arch7a = [.. historical, .. snapshot.Arch7a]
        });
        Assert.Equal(RevisionId, report.Summary.LatestQualifyingEconomicRevisionId);
        Assert.Equal(GuidFrom(900), report.Summary.LatestArch7aQualificationRunId);
    }

    [Fact]
    public void T36_qualification_authority_uses_time_and_rejects_ties()
    {
        var older = Qualification(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), SlotEnd);
        var newer = Qualification(GuidFrom(1), SlotEnd.AddMinutes(1));
        var selected = PmsShadowReadOnlyReportingReader.SelectAuthoritativeQualification(
            [older, newer], RevisionId, Sha);
        Assert.False(selected.Ambiguous);
        Assert.Equal(newer.QualificationRunId, selected.Run?.QualificationRunId);
        var ambiguous = PmsShadowReadOnlyReportingReader.SelectAuthoritativeQualification(
            [newer, newer with { QualificationRunId = GuidFrom(2) }], RevisionId, Sha);
        Assert.True(ambiguous.Ambiguous);
        Assert.Null(ambiguous.Run);
        var mismatched = PmsShadowReadOnlyReportingReader.SelectAuthoritativeQualification(
            [newer with { PlanSha256 = new string('b', 64) }], RevisionId, Sha);
        Assert.False(mismatched.Ambiguous);
        Assert.Null(mismatched.Run);
    }

    [Fact]
    public void T37_missing_lineage_is_unknown_and_raw_status_is_not_authority()
    {
        Assert.Equal("UNKNOWN", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            false, true, true, "COMPLETED", true, false, true));
        Assert.Equal("UNKNOWN", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            true, false, true, "COMPLETED", true, false, true));
        Assert.Equal("UNKNOWN", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            true, true, true, "COMPLETED", false, false, true));
        Assert.Equal("UNKNOWN", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            true, true, true, "COMPLETED", true, true, true));
        Assert.Equal("HISTORICAL", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            true, true, true, "COMPLETED", true, false, false));
        Assert.Equal("ACTIVE", PmsShadowReadOnlyReportingReader.DeriveRiskOperationalStatus(
            true, true, true, "COMPLETED", true, false, true));
        var fact = Observed("INGESTION_FAILED", OperationalFactKinds.OperationalAlert, true)
            with { SourceStatus = "HISTORICAL", DerivedOperationalStatus = "ACTIVE" };
        var item = Assert.Single(Build(Healthy() with { ObservedCodeFacts = [fact] }).Breaks,
            value => value.SourceExactCode == "INGESTION_FAILED");
        Assert.Equal(OperationalBreakStatus.Active, item.Status);
    }

    [Fact]
    public void T38_three_revisions_keep_only_latest_seven_risk_breaks_active()
    {
        var facts = Enumerable.Range(0, 3).SelectMany(revisionIndex =>
            Enumerable.Range(0, 7).Select(riskIndex =>
                Observed("BROKER_WORKING_LEAVES_UNOBSERVABLE",
                    OperationalFactKinds.RiskBlockingBreak, true) with
                {
                    ScopeId = GuidFrom(1000 + revisionIndex * 7 + riskIndex).ToString("D"),
                    RiskDecisionId = GuidFrom(1000 + revisionIndex * 7 + riskIndex),
                    TradeIntentId = GuidFrom(1100 + revisionIndex * 7 + riskIndex),
                    EconomicRevisionId = GuidFrom(1200 + revisionIndex),
                    SourceStatus = "ACTIVE",
                    DerivedOperationalStatus = revisionIndex == 2 ? "ACTIVE" : "HISTORICAL"
                })).ToArray();
        var report = Build(Healthy() with { ObservedCodeFacts = facts });
        var rows = report.Breaks.Where(value =>
            value.SourceExactCode == "BROKER_WORKING_LEAVES_UNOBSERVABLE").ToArray();
        Assert.Equal(21, rows.Length);
        Assert.Equal(7, rows.Count(value => value.Status == OperationalBreakStatus.Active));
        Assert.Equal(14, rows.Count(value => value.Status == OperationalBreakStatus.Historical));
        Assert.Equal(21, rows.Select(value => value.BreakId).Distinct().Count());
    }

    private static OperationalReportSet Build(OperationalReportingSnapshot? snapshot = null) =>
        OperationalReportProjector.Build(snapshot ?? Healthy());

    private static OperationalReportingSnapshot Healthy()
    {
        var counts = new[] { 66, 66, 78, 78 };
        var models = OperationalReportingContract.Strategies.Select((strategy, index) =>
            new ReportingModelRunFact(
                strategy, GuidFrom(index + 10), GuidFrom(index + 20),
                new(2026, 7, 23, 10 + index, 0, 0, TimeSpan.Zero),
                new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero), Sha, new string('b', 40),
                "REUSED_FINALIZED_D1_MODEL", "REUSED_FINALIZED_D1_MODEL",
                "SELECTED_REUSED_AS_SCHEDULED", counts[index], counts[index], counts[index],
                true, PmsShadowStateContract.ContractVersion,
                ReportingInfxSchedules.ExpectedTargetClose(strategy,
                    DateOnly.FromDateTime(AsOf.UtcDateTime)))).ToArray();
        var identities = new[]
        {
            ("AUDUSD", "59", "4007"), ("EURUSD", "66", "4001"),
            ("GBPUSD", "68", "4002"), ("NZDUSD", "58", "100613"),
            ("USDCAD", "148", "4013"), ("USDCHF", "136", "4010"),
            ("USDJPY", "150", "4004")
        };
        var net = identities.Select((item, index) => new ReportingFxNetLineFact(
            RevisionId, GuidFrom(100 + index), GuidFrom(200 + index), item.Item2,
            item.Item1, item.Item3, "8", index, 4m, 4m - index,
            ReportingAuthority.Proven, 1.1m, 1.2m, SlotEnd, ReportingAuthority.Proven,
            Sha, "arch7a_shadow_execution_v1")).ToArray();
        var contributions = identities.SelectMany((item, symbolIndex) =>
            OperationalReportingContract.Strategies.Select((strategy, strategyIndex) =>
                new ReportingFxStrategyContributionFact(
                    RevisionId, GuidFrom(100 + symbolIndex), item.Item1, strategy, 1,
                    [GuidFrom(300 + symbolIndex * 4 + strategyIndex)], 10m, 10m,
                    1m, 1m, 1m, 1m, "PROPORTIONAL_NET_ATTRIBUTION_V1",
                    ReportingAuthority.Probable, Sha))).ToArray();
        var arch7a = identities.Select((item, index) => new ReportingArch7aFact(
            RevisionId, GuidFrom(100 + index), GuidFrom(400 + index),
            GuidFrom(500 + index), GuidFrom(600 + index), "1754288005", "TEST",
            "SHADOW_ONLY", "SHADOW_PLANNED", "SHADOW_PLANNED", true, false,
            false, false, Sha, "QUALIFICATION_RUN_PRESENT", item.Item1,
            GuidFrom(200 + index))
        {
            QualificationRunId = GuidFrom(900),
            QualificationRunStatus = "COMPLETED",
            QualificationCompletedAtUtc = SlotEnd.AddMinutes(2),
            IsAuthoritativeForEconomicRevision = true
        }).ToArray();
        return new(
            AsOf, new string('c', 40), Database(), [Slot()], models,
            [new(RevisionId, 2, "slot-1", GuidFrom(2), "session-1", Sha,
                new string('d', 64), "COMPLETED", true, true, 99, 288, 288, 4,
                Sha, Sha, Sha, SlotEnd.AddMinutes(1))],
            net, contributions, arch7a, [], []);
    }

    private static ReportingDatabaseIdentity Database() => new(
        "qq_pms_shadow_arch7b_test", "PostgreSQL 18.4", 18, "pms_shadow", 35,
        7291, PmsShadowStateContract.MigrationIds, true, false, "ARCH7B_RDS_TEST",
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
        "REMOTE_TLS", "VERIFYFULL");

    private static ReportingSlotFact Slot(
        string id = "slot-1",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null)
    {
        var slotStart = start ?? SlotStart;
        var slotEnd = end ?? SlotEnd;
        var manifest = ReportingSlotManifestReader.Read(ManifestJson());
        var ready = new ReportingReadyMarkerFact(id, "PRESENT",
            ReportingAuthority.Proven, Sha, slotEnd.AddSeconds(1), "ready_v1");
        return new(id, slotStart, slotEnd, "COMPLETED", slotStart,
            slotEnd.AddMinutes(1), "session-1", Sha, ReportingAuthority.Proven,
            49, 49, 0, 0, ready.Status, 1, 60, 2, true, true, Sha, null,
            PmsShadowIntradayCadenceContract.Version, manifest, ready);
    }

    private static ObservedOperationalCodeFact Observed(
        string code,
        string kind,
        bool blocking) => new(
        code, kind, "TEST_SOURCE", "pms_shadow.test",
        OperationalReportingContract.Version, "RiskDecision", GuidFrom(700).ToString("D"),
        null, null, RevisionId, GuidFrom(701), GuidFrom(700), null, null,
        AsOf, AsOf, Sha, ReportingAuthority.Proven, "ACTIVE", blocking)
    {
        DerivedOperationalStatus = "ACTIVE"
    };

    private static PmsShadowExecutionQualificationRunRow Qualification(
        Guid qualificationRunId,
        DateTimeOffset completedAtUtc) => new(
        qualificationRunId, RevisionId, "session-1", "slot-1", completedAtUtc,
        Sha, Sha, 7, 7, 7, 7, "COMPLETED", Sha, true, true, true, true,
        completedAtUtc);

    private static ReportingArch7bFact Lifecycle() => new(
        GuidFrom(800), "LIFECYCLE_REGISTERED", ReportingAuthority.Proven, Sha,
        AsOf.AddMinutes(5), 2, 1, 0, 0, 0, 0, null, null, null, null,
        ReportingAuthority.Unknown, null);

    private static string ManifestJson() => $$"""
        {
          "slot_bbo_selection_contract_version": "pms_shadow_real_slot_bbo_selection_v2",
          "artifact_sha256": "{{Sha}}",
          "bbo_symbol_count": 49,
          "in_slot_bbo_event_count": 49,
          "post_close_bbo_event_count": 2,
          "excluded_post_close_by_symbol": { "4001": 2 },
          "selection_sha256": "{{Sha}}",
          "clock_preflight_status": "PASS",
          "clock_authority_snapshot_sha256": "{{Sha}}",
          "clock_post_close_snapshot_sha256": "{{Sha}}",
          "clock_reference_source": "NTP",
          "clock_offset_ms": 1.2,
          "clock_uncertainty_ms": 2.3,
          "maximum_late_receipt_after_close_ms": 1000,
          "maximum_cross_clock_lead_ms": 100,
          "cross_clock_comparison": "PASS"
        }
        """;

    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qq-reporting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("test repository root not found");
    }
}
