using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application.R018ImportPlanning;
using QQ.Production.Intraday.Application.R018StageLocalDbReplay;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R018StageLocalDbReplayTests
{
    [Fact]
    public void Independent_parity_passes_for_self_consistent_m1c2_outputs()
    {
        var dir = CreatePlanDirectory();
        var report = new R018StageLocalDbReplayLoader().RecalculateParity(dir, Now);

        Assert.True(report.CriticalPassed);
        Assert.DoesNotContain(report.Rows, x => x.Status == "FAIL" && x.Severity == "CRITICAL");
    }

    [Fact]
    public void Independent_parity_recomputes_output_hashes_and_fails_on_tamper()
    {
        var dir = CreatePlanDirectory();
        File.AppendAllText(Path.Combine(dir, "normalized_events.jsonl"), "\n", Encoding.UTF8);

        var report = new R018StageLocalDbReplayLoader().RecalculateParity(dir, Now);

        Assert.False(report.CriticalPassed);
        Assert.Contains(report.Rows, x => x.Check == "output_hash_match:normalized_events.jsonl" && x.Status == "FAIL");
    }

    [Fact]
    public void Entry_gate_uses_replay_eligibility_not_identity_scope()
    {
        var dir = CreatePlanDirectory(plan => plan with
        {
            ReplayEligibility = plan.ReplayEligibility with { LocalIsolatedReplayAllowed = false },
            IdentityScope = plan.IdentityScope with { LocalIsolatedReplayAllowed = true }
        });
        var loader = new R018StageLocalDbReplayLoader();
        var bundle = loader.Load(dir);
        var parity = loader.RecalculateParity(dir, Now);

        var gate = R018StageOnlyEntryGate.Evaluate(bundle, parity, Now);

        Assert.False(gate.CanImport);
        Assert.Contains(gate.Checks, x => x.Check == "replay_eligibility_local_isolated_allowed" && x.Status == "FAIL");
    }

    [Theory]
    [InlineData("REJECTED_STATUS")]
    [InlineData("VALIDATION_ERROR")]
    [InlineData("DB_APPLY_TRUE")]
    [InlineData("NETWORK_ALLOWED_TRUE")]
    [InlineData("CREATES_MODEL_RUN_TRUE")]
    [InlineData("CREATES_TARGET_WEIGHTS_TRUE")]
    [InlineData("CREATES_POSITION_LEDGER_EVENTS_TRUE")]
    [InlineData("APPLY_ELIGIBLE_ROW")]
    public void Entry_gate_blocks_unsafe_plan_conditions(string scenario)
    {
        var dir = CreatePlanDirectory(plan => scenario switch
        {
            "REJECTED_STATUS" => plan with { Status = R018ImportBundleStatus.REJECTED },
            "VALIDATION_ERROR" => plan with { Validation = plan.Validation with { Issues = [new R018ValidationIssue("ERROR", "X", "x")] } },
            "DB_APPLY_TRUE" => plan with { DbApply = true },
            "NETWORK_ALLOWED_TRUE" => plan with { NetworkAllowed = true },
            "CREATES_MODEL_RUN_TRUE" => plan with { CreatesModelRun = true },
            "CREATES_TARGET_WEIGHTS_TRUE" => plan with { CreatesTargetWeights = true },
            "CREATES_POSITION_LEDGER_EVENTS_TRUE" => plan with { CreatesPositionLedgerEvents = true },
            "APPLY_ELIGIBLE_ROW" => plan with { PlannedStagingRows = [plan.PlannedStagingRows!.Single() with { ApplyEligible = true }] },
            _ => plan
        });
        var loader = new R018StageLocalDbReplayLoader();
        var bundle = loader.Load(dir);
        var parity = loader.RecalculateParity(dir, Now);

        var gate = R018StageOnlyEntryGate.Evaluate(bundle, parity, Now);

        Assert.False(gate.CanImport);
    }

    [Theory]
    [InlineData(@"(localdb)\MSSQLLocalDB", true)]
    [InlineData("localhost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData(@".\SQLEXPRESS", true)]
    [InlineData("prod-sql.company.net", false)]
    [InlineData("10.2.3.4", false)]
    [InlineData("qq-prod-rds.amazonaws.com", false)]
    public void Connection_policy_allows_only_local_datasources(string dataSource, bool expected)
    {
        Assert.Equal(expected, R018StageLocalConnectionPolicy.IsLocalDataSource(dataSource));
    }

    [Theory]
    [InlineData("QQIntraday_M1D_StageOnly_ABC123", true)]
    [InlineData("QQIntraday_M1D_StageOnly_0f4c5a", true)]
    [InlineData("QQProductionIntraday", false)]
    [InlineData("QQIntraday_M1D_StageOnly_", false)]
    [InlineData("QQIntraday_M1D_StageOnly_bad-name", false)]
    public void Connection_policy_requires_exact_disposable_db_prefix(string databaseName, bool expected)
    {
        Assert.Equal(expected, R018StageLocalConnectionPolicy.IsSafeDisposableDatabaseName(databaseName));
    }

    [Fact]
    public void Missing_required_file_is_critical_parity_failure()
    {
        var dir = CreatePlanDirectory();
        File.Delete(Path.Combine(dir, "business_events.json"));

        var report = new R018StageLocalDbReplayLoader().RecalculateParity(dir, Now);

        Assert.False(report.CriticalPassed);
        Assert.Contains(report.Rows, x => x.Check == "required_file_present:business_events.json" && x.Status == "FAIL");
    }

    [Fact]
    public void Forbidden_canonical_table_list_includes_m1d_blocked_contracts()
    {
        Assert.Contains("ModelRuns", R018StageLocalDbReplayConstants.ForbiddenCanonicalTables);
        Assert.Contains("TargetWeights", R018StageLocalDbReplayConstants.ForbiddenCanonicalTables);
        Assert.Contains("PositionLedgerEvents", R018StageLocalDbReplayConstants.ForbiddenCanonicalTables);
        Assert.Contains("Fills", R018StageLocalDbReplayConstants.ForbiddenCanonicalTables);
    }

    private static readonly DateTimeOffset Now = new(2026, 6, 25, 10, 0, 0, TimeSpan.Zero);

    private static string CreatePlanDirectory(Func<R018ImportPlan, R018ImportPlan>? mutate = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "r018-stage-m1d-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var evidence = new R018SourceEvidence(
            R018ProvenanceType.RAW_FIX,
            "raw.fix",
            new string('a', 64),
            "line:1",
            "test-parser-v1",
            "RAW_FIX_AUTHORITATIVE",
            new string('b', 64),
            R018ArtifactType.RAW_FIX_LOG,
            Now,
            1,
            1,
            false);
        var ev = new R018NormalizedEvent(
            "EV-1",
            R018NormalizedEventKind.Order,
            R018ProvenanceType.RAW_FIX,
            "LMAX_DEMO",
            "DEMO_ACCOUNT",
            "LMAX",
            "EURUSD",
            "4001",
            "BUY",
            "BASE",
            "PHASE_1",
            "W1",
            "C1",
            "CL1",
            null,
            null,
            null,
            "LIMIT",
            "0",
            1,
            null,
            null,
            null,
            1.1m,
            null,
            null,
            null,
            Now,
            Now,
            1,
            null,
            false,
            false,
            evidence,
            """{"kind":"order"}""",
            1,
            false,
            "FIX-1",
            null,
            null);
        var occurrence = new R018EvidenceOccurrence(
            "OCC-1",
            ev.StableKey,
            ev.Kind,
            ev.Provenance,
            R018ArtifactType.RAW_FIX_LOG,
            evidence.SourcePath,
            evidence.SourceFileHash,
            evidence.SourceLocator,
            evidence.RawPayloadHash,
            Now,
            Now,
            1,
            1,
            false);
        var business = new R018BusinessEvent(
            "BE-1",
            ev.StableKey,
            ev.Kind,
            new string('c', 64),
            new SortedDictionary<string, string>(StringComparer.Ordinal) { ["clordid"] = "CL1" },
            [evidence]);
        var validation = new R018ValidationReport(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            R018ImportBundleStatus.EVIDENCE_ONLY,
            [],
            1,
            1,
            0,
            0,
            0,
            0,
            false);
        var eligibility = new R018ReplayEligibility(true, true, true, 0, true, true, ["LOCAL_ISOLATED_REPLAY_ALLOWED"]);
        var staging = new R018PlannedStagingRow(
            "PLAN-1",
            "LMAX_DEMO|DEMO_ACCOUNT|LMAX|ORDER|CL1",
            "ChildOrder",
            "EVIDENCE_STAGING_ONLY",
            ev.StableKey,
            null,
            new SortedDictionary<string, string>(StringComparer.Ordinal) { ["clordid"] = "CL1" },
            [],
            [],
            [evidence],
            "NOT_CANONICAL_REPLAY_ELIGIBLE",
            false,
            ["DB_APPLY_FALSE_M1C2"]);
        var plan = new R018ImportPlan(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            R018ImportBundleStatus.EVIDENCE_ONLY,
            "SRC-RUN",
            "candidate-hash",
            new string('d', 64),
            new string('e', 64),
            "test-commit",
            "test",
            "baseline",
            null,
            null,
            R018ImportPlanningConstants.InstrumentCatalogVersion,
            new string('f', 64),
            Now,
            "stage-only test",
            R018LedgerApplicability.INCOMPLETE_HISTORY,
            false,
            false,
            false,
            false,
            false,
            ["ChildOrder"],
            [ev],
            [occurrence],
            [business],
            validation,
            new R018LineageReport(R018ImportPlanningConstants.PlanSchemaVersion, R018ImportPlanningConstants.ToolVersion, R018ImportBundleStatus.EVIDENCE_ONLY, "NO_CATALOG", null, R018LedgerApplicability.INCOMPLETE_HISTORY.ToString(), [], []),
            new R018IdentityScopeReport(R018ImportPlanningConstants.PlanSchemaVersion, R018ImportPlanningConstants.ToolVersion, "LMAX_DEMO", "DEMO_ACCOUNT", "LMAX", true, true, true, true, true, false, true, []),
            eligibility,
            [],
            [staging]);
        plan = mutate?.Invoke(plan) ?? plan;

        var manifest = new R018ArtifactBundleManifest(
            R018ImportPlanningConstants.BundleManifestSchemaVersion,
            "LMAX_DEMO",
            "DEMO_ACCOUNT",
            "LMAX",
            "SRC-RUN",
            "candidate-hash",
            "BASE",
            Now,
            Now.AddMinutes(-15),
            Now.AddMinutes(-15),
            Now.AddMinutes(1),
            null,
            "fixture",
            [],
            "core",
            "config");

        WriteJson(Path.Combine(dir, "bundle_manifest.json"), manifest);
        WriteJson(Path.Combine(dir, "validation_report.json"), plan.Validation);
        WriteJson(Path.Combine(dir, "replay_eligibility.json"), plan.ReplayEligibility);
        WriteJsonLines(Path.Combine(dir, "normalized_events.jsonl"), plan.NormalizedEvents);
        WriteJsonLines(Path.Combine(dir, "evidence_occurrences.jsonl"), plan.EvidenceOccurrences);
        WriteJson(Path.Combine(dir, "business_events.json"), plan.BusinessEvents);
        WriteJson(Path.Combine(dir, "typed_staging_plan.json"), plan.PlannedStagingRows!);
        WriteJson(Path.Combine(dir, "import_plan_v3.json"), plan);
        var hashes = R018StageLocalDbReplayConstants.RequiredPlanFiles
            .Where(file => file != "output_hashes.json")
            .ToDictionary(file => file, file => R018ArtifactBundleReader.ComputeFileSha256(Path.Combine(dir, file)), StringComparer.Ordinal);
        WriteJson(Path.Combine(dir, "output_hashes.json"), hashes);
        return dir;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
    static R018StageLocalDbReplayTests()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
        JsonLineOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private static void WriteJson<T>(string path, T value)
        => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static void WriteJsonLines<T>(string path, IEnumerable<T> values)
        => File.WriteAllLines(path, values.Select(value => JsonSerializer.Serialize(value, JsonLineOptions)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
