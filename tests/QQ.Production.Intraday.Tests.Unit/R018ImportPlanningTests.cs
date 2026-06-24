using System.Text.Json;
using QQ.Production.Intraday.Application.R018ImportPlanning;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R018ImportPlanningTests
{
    [Fact]
    public void Jsonl_output_is_compact_one_object_per_line()
    {
        var bundle = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var output = Path.Combine(CreateTempDirectory(), "out");
        var exit = RunCli(bundle, output);

        Assert.Equal(0, exit);
        var lines = File.ReadAllLines(Path.Combine(output, "normalized_events.jsonl")).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        using var planDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "import_plan_v2.json")));
        Assert.Equal(planDoc.RootElement.GetProperty("normalizedEvents").GetArrayLength(), lines.Length);
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("  ", line, StringComparison.Ordinal);
            using var _ = JsonDocument.Parse(line);
        });
    }

    [Fact]
    public void Partial_trade_execution_report_is_non_terminal_and_rejected_until_terminal_report()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 10, 1),
            ExecutionReport("C1", "E1", "F", "1", 4, 6, 4, 0.7001m, 2)
        })));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "NON_TERMINAL_ORDER");
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.ExecutionReport && e.TerminalState == "PARTIAL_FILL");
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.Fill && e.SourceExecutionReportEventId is not null && e.TerminalState == "PARTIAL_FILL");
    }

    [Fact]
    public void Final_trade_execution_report_is_terminal_and_derives_fill_fact()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents())));

        Assert.NotEqual(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.ExecutionReport && e.ExecType == "F" && e.TerminalState == "FILLED");
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.Fill && e.Provenance == R018ProvenanceType.Derived && e.SourceExecutionReportEventId is not null);
    }

    [Fact]
    public void Cancel_reject_and_expire_are_terminal_states()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 1, 1), ExecutionReport("C1", "EC", "4", "4", 0, 1, null, null, 2),
            Order("C2", 1, 3), ExecutionReport("C2", "ER", "8", "8", 0, 1, null, null, 4),
            Order("C3", 1, 5), ExecutionReport("C3", "EX", "C", "C", 0, 1, null, null, 6)
        })));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Contains(plan.NormalizedEvents, e => e.ClOrdId == "C1" && e.TerminalState == "CANCELED");
        Assert.Contains(plan.NormalizedEvents, e => e.ClOrdId == "C2" && e.TerminalState == "REJECTED");
        Assert.Contains(plan.NormalizedEvents, e => e.ClOrdId == "C3" && e.TerminalState == "EXPIRED");
    }

    [Fact]
    public void Cum_decreasing_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 10, 1),
            ExecutionReport("C1", "E1", "F", "1", 6, 4, 6, 0.7001m, 2),
            ExecutionReport("C1", "E2", "F", "2", 5, 5, 1, 0.7002m, 3)
        })));

        AssertRejected(plan, "CUM_DECREASING");
    }

    [Fact]
    public void Overfill_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 10, 1),
            ExecutionReport("C1", "E1", "F", "2", 11, 0, 11, 0.7001m, 2)
        })));

        AssertRejected(plan, "CUM_LEAVES_INCONSISTENT", "OVERFILL");
    }

    [Fact]
    public void Lastqty_cum_inconsistency_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 10, 1),
            ExecutionReport("C1", "E1", "F", "1", 4, 6, 3, 0.7001m, 2),
            ExecutionReport("C1", "E2", "F", "2", 10, 0, 6, 0.7002m, 3)
        })));

        AssertRejected(plan, "LASTQTY_CUM_INCONSISTENT");
    }

    [Fact]
    public void Manual_export_does_not_create_fill_or_terminal()
    {
        var plan = BuildPlan(CreateBundle(Artifact("manual.jsonl", "MANUAL_EXPORT", new[] { ManualFill("C1") })));

        Assert.Equal(0, plan.NormalizedEvents.Count(e => e.Kind == R018NormalizedEventKind.Fill));
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.ManualObservation);
    }

    [Fact]
    public void Raw_fix_and_eod_matching_facts_corroborate_without_conflict()
    {
        var plan = BuildPlan(CreateBundle(
            Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()),
            Artifact("eod.jsonl", "EOD_LMAX_REPORT", new[] { DirectFill("C1", "E1", 10, 0.7001m, 10, 0, 4, "EOD_LMAX_REPORT") })));

        Assert.DoesNotContain(plan.Validation.Issues, i => i.Code == "DUPLICATE_CONFLICT");
        Assert.True(plan.NormalizedEvents.Count(e => e.Kind == R018NormalizedEventKind.Fill && e.ExecId == "E1") >= 1);
    }

    [Fact]
    public void Raw_fix_and_eod_conflicting_qty_rejects()
    {
        var plan = BuildPlan(CreateBundle(
            Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()),
            Artifact("eod.jsonl", "EOD_LMAX_REPORT", new[] { DirectFill("C1", "E1", 9, 0.7001m, 9, 1, 4, "EOD_LMAX_REPORT") })));

        AssertRejected(plan, "DUPLICATE_CONFLICT");
    }

    [Fact]
    public void Raw_fix_provenance_declared_from_manual_artifact_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("manual.jsonl", "MANUAL_UI", new[] { ManualFill("C1", provenance: "RAW_FIX") })));

        AssertRejected(plan, "PROVENANCE_NOT_ALLOWED_FOR_ARTIFACT");
    }

    [Fact]
    public void Mixed_demo_live_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", 10, 1, environment: "live") })));

        AssertRejected(plan, "SCOPE_ENVIRONMENT_MISMATCH");
    }

    [Fact]
    public void Mixed_accounts_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", 10, 1, account: "OTHER") })));

        AssertRejected(plan, "SCOPE_ACCOUNT_MISMATCH");
    }

    [Fact]
    public void Mixed_venues_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", 10, 1, venue: "OTHER") })));

        AssertRejected(plan, "SCOPE_VENUE_MISMATCH");
    }

    [Fact]
    public void Same_execid_on_two_clordids_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 1, 1), ExecutionReport("C1", "E1", "F", "2", 1, 0, 1, 0.7001m, 2),
            Order("C2", 1, 3), ExecutionReport("C2", "E1", "F", "2", 1, 0, 1, 0.7001m, 4)
        })));

        AssertRejected(plan, "SAME_EXECID_MULTIPLE_CLORDID");
    }

    [Fact]
    public void Explicit_model_run_without_catalog_is_not_linked()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), modelRunId: "MR-1"));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal("NO_CATALOG_EXPLICIT_ID_UNVERIFIED", plan.Lineage.ModelRunResolution);
    }

    [Fact]
    public void Exact_catalog_match_links_canonical_and_records_catalog_hash()
    {
        var bundle = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var catalog = CreateCatalog(("MR-1", "RTEST", "HASH123"));
        var plan = BuildPlan(bundle, catalog);

        Assert.Equal(R018ImportBundleStatus.CANONICAL_LINKED, plan.Status);
        Assert.Equal("OFFLINE_CATALOG_UNIQUE_MATCH", plan.Lineage.ModelRunResolution);
        Assert.NotNull(plan.Lineage.CatalogResolution?.CatalogSha256);
        Assert.Contains(plan.PlannedStagingRows!, r => r.TargetContract == "Fill" && r.ApplyEligible == false);
    }

    [Fact]
    public void Model_run_catalog_contradiction_rejects()
    {
        var bundle = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), modelRunId: "MR-WRONG");
        var catalog = CreateCatalog(("MR-1", "RTEST", "HASH123"));
        var plan = BuildPlan(bundle, catalog);

        AssertRejected(plan, "MODEL_RUN_CATALOG_CONTRADICTION");
    }

    [Fact]
    public void Starting_position_string_alone_never_makes_complete_ledger()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), startingPositionSource: "operator-note"));

        Assert.Equal(R018LedgerApplicability.INCOMPLETE_HISTORY, plan.LedgerApplicability);
    }

    [Fact]
    public void Bundle_without_fills_has_no_ledger_applicability()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", 1, 1), ExecutionReport("C1", "EC", "4", "4", 0, 1, null, null, 2) })));

        Assert.Equal(R018LedgerApplicability.NOT_APPLICABLE, plan.LedgerApplicability);
    }

    [Fact]
    public void Manifest_change_modifies_input_hash()
    {
        var a = new R018ArtifactBundleReader().Read(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), deadline: "2026-06-24T12:15:30Z"));
        var b = new R018ArtifactBundleReader().Read(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), deadline: "2026-06-24T12:16:30Z"));

        Assert.NotEqual(a.InputBundleHash, b.InputBundleHash);
    }

    [Fact]
    public void Deadline_change_modifies_deterministic_hash()
    {
        var a = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), deadline: "2026-06-24T12:15:30Z"));
        var b = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()), deadline: "2026-06-24T12:16:30Z"));

        Assert.NotEqual(a.DeterministicContentHash, b.DeterministicContentHash);
    }

    [Fact]
    public void Provenance_change_modifies_deterministic_hash()
    {
        var a = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents())));
        var b = BuildPlan(CreateBundle(Artifact("ledger.jsonl", "FILL_LEDGER", ValidEvents(provenance: "ARTIFACT_LEDGER"))));

        Assert.NotEqual(a.DeterministicContentHash, b.DeterministicContentHash);
    }

    [Fact]
    public void Phase_wave_clip_change_modifies_deterministic_hash()
    {
        var a = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents(phase: "PHASE_1", wave: "W1", clip: "CLIP1"))));
        var b = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents(phase: "PHASE_2", wave: "W2", clip: "CLIP2"))));

        Assert.NotEqual(a.DeterministicContentHash, b.DeterministicContentHash);
    }

    [Fact]
    public void Sibling_prefix_escape_rejects()
    {
        var path = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var sibling = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "-sibling");
        Directory.CreateDirectory(sibling);
        var siblingFile = Path.Combine(sibling, "raw.jsonl");
        File.WriteAllText(siblingFile, string.Join(Environment.NewLine, ValidEvents()));
        RewriteManifestArtifactPath(path, "..\\" + Path.GetFileName(sibling) + "\\raw.jsonl", R018ArtifactBundleReader.ComputeFileSha256(siblingFile));

        var plan = BuildPlan(path);

        AssertRejected(plan, "ARTIFACT_PARENT_TRAVERSAL_REJECTED");
    }

    [Fact]
    public void Dotdot_escape_rejects()
    {
        var path = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        RewriteManifestArtifactPath(path, "..\\raw.jsonl", new string('0', 64));

        var plan = BuildPlan(path);

        AssertRejected(plan, "ARTIFACT_PARENT_TRAVERSAL_REJECTED");
    }

    [Fact]
    public void Symlink_or_reparse_artifact_is_rejected_when_supported()
    {
        var path = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var link = Path.Combine(path, "link.jsonl");
        try
        {
            File.Delete(Path.Combine(path, "raw.jsonl"));
            File.CreateSymbolicLink(link, Path.Combine(Path.GetTempPath(), "outside-r018-link.jsonl"));
        }
        catch
        {
            return;
        }

        RewriteManifestArtifactPath(path, "link.jsonl", new string('0', 64));
        var plan = BuildPlan(path);
        AssertRejected(plan, "ARTIFACT_REPARSE_POINT_REJECTED");
    }

    [Fact]
    public void Empty_bundle_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", Array.Empty<string>())));

        AssertRejected(plan, "EMPTY_BUNDLE");
    }

    [Fact]
    public void Unsupported_artifact_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "UNKNOWN", ValidEvents())));

        AssertRejected(plan, "UNSUPPORTED_ARTIFACT_TYPE");
    }

    [Fact]
    public void Duplicate_artifact_path_rejects()
    {
        var art = Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents());
        var plan = BuildPlan(CreateBundle(art, art));

        AssertRejected(plan, "DUPLICATE_ARTIFACT_PATH");
    }

    [Fact]
    public void Output_directory_non_empty_rejects()
    {
        var bundle = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var output = Path.Combine(CreateTempDirectory(), "out");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "stale.txt"), "stale");

        var exit = RunCli(bundle, output, out var error);

        Assert.Equal(2, exit);
        Assert.Contains("OUTPUT_DIRECTORY_NOT_EMPTY", error);
    }

    [Fact]
    public void Invalid_order_qty_price_side_and_tif_reject()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", -1, 1, side: "BAD", price: -1, tif: "3") })));

        AssertRejected(plan, "INVALID_ORDER_QTY", "INVALID_SIDE", "LIMIT_PRICE_TAG44_REQUIRED", "UNSUPPORTED_TIME_IN_FORCE");
    }

    [Fact]
    public void Fill_order_symbol_mismatch_rejects()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[]
        {
            Order("C1", 1, 1, symbol: "AUDUSD"),
            ExecutionReport("C1", "E1", "F", "2", 1, 0, 1, 0.7001m, 2, symbol: "EURUSD")
        })));

        AssertRejected(plan, "SYMBOL_MISMATCH_WITH_ORDER");
    }

    [Fact]
    public void Parity_report_is_non_tautological_for_rejections()
    {
        var bundle = new R018ArtifactBundleReader().Read(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", new[] { Order("C1", 1, 1, side: "BAD") })));
        var plan = new R018ImportPlanBuilder().Build(bundle, null, "TEST");
        var parity = new R018ParityReportBuilder().Build(bundle, plan);

        Assert.Contains(parity, r => r.Expected != r.Actual && r.Status == "FAIL");
    }

    [Fact]
    public void Rerun_is_deterministic_outside_generation_timestamp()
    {
        var path = CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents()));
        var first = BuildPlan(path);
        var second = BuildPlan(path);

        Assert.Equal(first.DeterministicContentHash, second.DeterministicContentHash);
    }

    [Fact]
    public void Static_scan_has_no_db_network_gateway_databento_or_r009_path()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/R018ImportPlanning/R018ImportPlanning.cs"));
        var cli = File.ReadAllText(Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.R018ImportPlan/Program.cs"));
        var combined = source + cli;

        Assert.DoesNotContain("TcpClient", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("IVenueExecutionGateway", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessModelRunService", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Databento", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountAPI", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R009", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--apply", cli, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_only_plan_has_no_canonical_trade_order_fill_rows()
    {
        var plan = BuildPlan(CreateBundle(Artifact("raw.jsonl", "RAW_FIX_LOG", ValidEvents())));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.DoesNotContain(plan.PlannedStagingRows!, r => r.TargetContract is "TradeIntent" or "ParentOrder" or "ChildOrder" or "Fill");
        Assert.All(plan.PlannedStagingRows!, r => Assert.False(r.ApplyEligible));
    }

    private static void AssertRejected(R018ImportPlan plan, params string[] expectedCodes)
    {
        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        foreach (var code in expectedCodes)
        {
            Assert.Contains(plan.Validation.Issues, i => i.Code == code);
        }
    }

    private static R018ImportPlan BuildPlan(string bundlePath, string? modelRunCatalogPath = null)
    {
        var bundle = new R018ArtifactBundleReader().Read(bundlePath);
        return new R018ImportPlanBuilder().Build(bundle, modelRunCatalogPath, "TESTCOMMIT");
    }

    private static int RunCli(string bundlePath, string outputPath)
        => RunCli(bundlePath, outputPath, out _);

    private static int RunCli(string bundlePath, string outputPath, out string errorText)
    {
        var cli = new R018OfflineImportPlanCli();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = cli.Execute(new[]
        {
            "build-r018-import-plan",
            "--bundle", bundlePath,
            "--output", outputPath,
            "--no-db",
            "--no-network",
            "--tool-commit", "TESTCOMMIT",
            "--source-baseline-commit", "25db482d043198fd2f332984b3cc02b681367c55"
        }, output, error);
        errorText = error.ToString();
        return exit;
    }

    private static string CreateBundle(params TestArtifact[] artifacts)
        => CreateBundle(artifacts, modelRunId: null, startingPositionSource: null, deadline: "2026-06-24T12:15:30Z");

    private static string CreateBundle(TestArtifact artifact, string? modelRunId = null, string? startingPositionSource = null, string? deadline = "2026-06-24T12:15:30Z")
        => CreateBundle(new[] { artifact }, modelRunId, startingPositionSource, deadline);

    private static string CreateBundle(IReadOnlyList<TestArtifact> artifacts, string? modelRunId = null, string? startingPositionSource = null, string? deadline = "2026-06-24T12:15:30Z")
    {
        var path = CreateTempDirectory();
        var manifestArtifacts = new List<object>();
        foreach (var artifact in artifacts)
        {
            var artifactPath = Path.Combine(path, artifact.Path);
            File.WriteAllText(artifactPath, string.Join(Environment.NewLine, artifact.Lines));
            manifestArtifacts.Add(new
            {
                path = artifact.Path,
                sha256 = R018ArtifactBundleReader.ComputeFileSha256(artifactPath),
                kind = "events",
                artifact_type = artifact.ArtifactType
            });
        }

        var manifest = new Dictionary<string, object?>
        {
            ["schema_version"] = "r018_artifact_bundle_manifest_v2",
            ["environment"] = "demo",
            ["broker_account"] = "LMAX-DEMO-ACCOUNT",
            ["venue"] = "LMAX",
            ["source_run_id"] = "RTEST",
            ["approved_candidate_hash"] = "HASH123",
            ["quantity_unit"] = "LMAX_CONTRACTS",
            ["target_close_utc"] = "2026-06-24T12:15:00Z",
            ["decision_utc"] = "2026-06-24T12:14:00Z",
            ["effective_from_utc"] = "2026-06-24T12:15:00Z",
            ["deadline_utc"] = deadline,
            ["model_run_id"] = modelRunId,
            ["starting_position_source"] = startingPositionSource,
            ["core_commit"] = "CORE",
            ["config_hash"] = "CONFIG",
            ["artifacts"] = manifestArtifacts
        };
        File.WriteAllText(Path.Combine(path, "r018_bundle_manifest_v2.json"), JsonSerializer.Serialize(manifest, JsonOptions()));
        return path;
    }

    private static TestArtifact Artifact(string path, string artifactType, IReadOnlyList<string> lines) => new(path, artifactType, lines);

    private static IReadOnlyList<string> ValidEvents(string provenance = "RAW_FIX", string phase = "PHASE_1", string wave = "W1", string clip = "CLIP1")
        => new[]
        {
            Order("C1", 10, 1, provenance: provenance, phase: phase, wave: wave, clip: clip),
            ExecutionReport("C1", "ACK1", "0", "0", 0, 10, null, null, 2, provenance: provenance, phase: phase, wave: wave, clip: clip),
            ExecutionReport("C1", "E1", "F", "2", 10, 0, 10, 0.7001m, 3, provenance: provenance, phase: phase, wave: wave, clip: clip)
        };

    private static string Order(string clOrdId, decimal qty, long localOrder, string symbol = "AUDUSD", string side = "BUY", decimal price = 0.7000m, string tif = "0", string environment = "demo", string account = "LMAX-DEMO-ACCOUNT", string venue = "LMAX", string provenance = "RAW_FIX", string phase = "PHASE_1", string wave = "W1", string clip = "CLIP1")
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event_type"] = "NewOrderSingle", ["provenance"] = provenance, ["environment"] = environment, ["broker_account"] = account, ["venue"] = venue,
            ["clordid"] = clOrdId, ["symbol"] = symbol, ["security_id"] = "4007", ["side"] = side, ["quantity_unit"] = "LMAX_CONTRACTS",
            ["phase_id"] = phase, ["wave_id"] = wave, ["clip_id"] = clip, ["order_qty"] = qty, ["price"] = price, ["order_type"] = "LIMIT", ["time_in_force"] = tif,
            ["local_event_order"] = localOrder, ["source_timestamp_utc"] = "2026-06-24T12:14:59Z"
        }, JsonOptions());

    private static string ExecutionReport(string clOrdId, string execId, string execType, string ordStatus, decimal cum, decimal leaves, decimal? lastQty, decimal? lastPx, long localOrder, string symbol = "AUDUSD", string provenance = "RAW_FIX", string phase = "PHASE_1", string wave = "W1", string clip = "CLIP1")
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event_type"] = "ExecutionReport", ["provenance"] = provenance, ["clordid"] = clOrdId, ["execid"] = execId,
            ["symbol"] = symbol, ["security_id"] = "4007", ["side"] = "BUY", ["quantity_unit"] = "LMAX_CONTRACTS",
            ["phase_id"] = phase, ["wave_id"] = wave, ["clip_id"] = clip, ["exec_type"] = execType, ["ord_status"] = ordStatus,
            ["cum_qty"] = cum, ["leaves_qty"] = leaves, ["last_qty"] = lastQty, ["last_px"] = lastPx,
            ["local_event_order"] = localOrder, ["source_timestamp_utc"] = "2026-06-24T12:15:00Z"
        }, JsonOptions());

    private static string DirectFill(string clOrdId, string execId, decimal lastQty, decimal price, decimal cum, decimal leaves, long localOrder, string provenance)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event_type"] = "Fill", ["provenance"] = provenance, ["clordid"] = clOrdId, ["execid"] = execId,
            ["symbol"] = "AUDUSD", ["security_id"] = "4007", ["side"] = "BUY", ["quantity_unit"] = "LMAX_CONTRACTS",
            ["last_qty"] = lastQty, ["fill_price"] = price, ["cum_qty"] = cum, ["leaves_qty"] = leaves,
            ["local_event_order"] = localOrder, ["source_timestamp_utc"] = "2026-06-24T12:15:01Z"
        }, JsonOptions());

    private static string ManualFill(string clOrdId, string provenance = "MANUAL_EXPORT")
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event_type"] = "Fill", ["provenance"] = provenance, ["clordid"] = clOrdId, ["execid"] = "MANUAL-1",
            ["symbol"] = "AUDUSD", ["security_id"] = "4007", ["side"] = "BUY", ["quantity_unit"] = "LMAX_CONTRACTS",
            ["last_qty"] = 1, ["fill_price"] = 0.7001m, ["local_event_order"] = 1
        }, JsonOptions());

    private static string CreateCatalog(params (string modelRunId, string sourceRunId, string approvedHash)[] rows)
    {
        var path = Path.Combine(CreateTempDirectory(), "catalog.json");
        var payload = new
        {
            catalog_schema_version = "model_run_catalog_v1",
            model_runs = rows.Select(r => new { model_run_id = r.modelRunId, source_run_id = r.sourceRunId, approved_candidate_hash = r.approvedHash }).ToArray()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions()));
        return path;
    }

    private static void RewriteManifestArtifactPath(string bundlePath, string artifactPath, string hash)
    {
        var manifestPath = Path.Combine(bundlePath, "r018_bundle_manifest_v2.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement.Clone();
        var manifest = JsonSerializer.Deserialize<Dictionary<string, object?>>(root.GetRawText(), JsonOptions())!;
        manifest["artifacts"] = new[] { new { path = artifactPath, sha256 = hash, kind = "events", artifact_type = "RAW_FIX_LOG" } };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "r018-m1c1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "QQ.Production.Intraday.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Repo root not found.");
    }

    private sealed record TestArtifact(string Path, string ArtifactType, IReadOnlyList<string> Lines);
}
