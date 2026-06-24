using System.Text.Json;
using QQ.Production.Intraday.Application.R018ImportPlanning;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R018ImportPlanningTests
{
    [Fact]
    public void Valid_bundle_without_model_run_is_evidence_only()
    {
        var bundlePath = CreateBundle(ValidEvents());
        var plan = BuildPlan(bundlePath);

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.False(plan.CreatesModelRun);
        Assert.False(plan.CreatesTargetWeights);
        Assert.False(plan.CreatesPositionLedgerEvents);
        Assert.DoesNotContain("PositionLedgerEvent", plan.ProposedMutationTargets);
    }

    [Fact]
    public void Explicit_model_run_is_canonical_linked_without_ledger_mutation()
    {
        var bundlePath = CreateBundle(ValidEvents(), modelRunId: "MR-1");
        var plan = BuildPlan(bundlePath);

        Assert.Equal(R018ImportBundleStatus.CANONICAL_LINKED, plan.Status);
        Assert.Contains("TradeIntent", plan.ProposedMutationTargets);
        Assert.Contains("ChildOrder", plan.ProposedMutationTargets);
        Assert.Contains("Fill", plan.ProposedMutationTargets);
        Assert.Contains("PositionLedgerEvent_DESCRIBED_ONLY_NOT_PLANNED", plan.ProposedMutationTargets);
        Assert.False(plan.CreatesPositionLedgerEvents);
    }

    [Fact]
    public void Missing_manifest_rejects_closed()
    {
        var path = CreateTempDirectory();
        File.WriteAllText(Path.Combine(path, "events.jsonl"), string.Join(Environment.NewLine, ValidEvents()));

        var plan = BuildPlan(path);

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "MISSING_MANIFEST");
    }

    [Fact]
    public void Hash_mismatch_rejects_closed()
    {
        var bundlePath = CreateBundle(ValidEvents(), artifactHashOverride: new string('0', 64));

        var plan = BuildPlan(bundlePath);

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "HASH_MISMATCH");
    }

    [Fact]
    public void Unknown_instrument_rejects()
    {
        var events = ValidEvents().Select(e => e.Replace("AUDUSD", "FOOUSD", StringComparison.Ordinal)).ToArray();

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "UNKNOWN_INSTRUMENT");
    }

    [Fact]
    public void Unknown_unit_rejects()
    {
        var plan = BuildPlan(CreateBundle(ValidEvents(), quantityUnit: "UNKNOWN_UNIT"));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "UNKNOWN_QUANTITY_UNIT");
    }

    [Fact]
    public void Missing_environment_rejects_and_blocks_future_db_apply()
    {
        var plan = BuildPlan(CreateBundle(ValidEvents(), environment: "unknown"));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.False(plan.IdentityScope.FutureDbApplyAllowed);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "MISSING_ENVIRONMENT");
    }

    [Fact]
    public void Missing_broker_account_rejects_and_blocks_future_db_apply()
    {
        var plan = BuildPlan(CreateBundle(ValidEvents(), brokerAccount: "unknown"));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.False(plan.IdentityScope.FutureDbApplyAllowed);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "MISSING_BROKER_ACCOUNT");
    }

    [Fact]
    public void Exact_duplicate_is_idempotent()
    {
        var events = ValidEvents().Concat(new[] { ValidEvents()[0] }).ToArray();

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal(1, plan.Validation.DuplicateExactCount);
        Assert.DoesNotContain(plan.Validation.Issues, i => i.Code == "DUPLICATE_CONFLICT");
    }

    [Fact]
    public void Duplicate_same_key_different_payload_rejects()
    {
        var events = ValidEvents().Concat(new[]
        {
            Order("C1", "AUDUSD", 11, 1)
        }).ToArray();

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "DUPLICATE_CONFLICT");
    }

    [Fact]
    public void Fill_without_child_rejects()
    {
        var events = new[] { Fill("MISSING", "E1", "AUDUSD", 10, 0.7001m, 1, 0, 2) };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "FILL_WITHOUT_CHILD_ORDER");
    }

    [Fact]
    public void Fill_without_exec_id_rejects()
    {
        var events = new[] { Order("C1", "AUDUSD", 10, 1), Fill("C1", "", "AUDUSD", 10, 0.7001m, 10, 0, 2) };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "FILL_WITHOUT_EXECID");
    }

    [Fact]
    public void Partial_fill_followed_by_final_fill_is_valid()
    {
        var events = new[]
        {
            Order("C1", "AUDUSD", 10, 1),
            Fill("C1", "E1", "AUDUSD", 4, 0.7001m, 4, 6, 2),
            Fill("C1", "E2", "AUDUSD", 6, 0.7002m, 10, 0, 3)
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal(2, plan.Validation.FillCount);
    }

    [Fact]
    public void Inconsistent_cum_plus_leaves_rejects()
    {
        var events = new[]
        {
            Order("C1", "AUDUSD", 10, 1),
            ExecutionReport("C1", "ER1", "AUDUSD", "0", "0", cum: 4, leaves: 7, localOrder: 2)
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "CUM_LEAVES_INCONSISTENT");
    }

    [Fact]
    public void Cancel_pending_without_terminal_rejects()
    {
        var events = new[]
        {
            Order("C1", "AUDUSD", 10, 1),
            ExecutionReport("C1", "ER1", "AUDUSD", "6", "6", cum: 0, leaves: 10, localOrder: 2)
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.REJECTED, plan.Status);
        Assert.Contains(plan.Validation.Issues, i => i.Code == "NON_TERMINAL_ORDER");
    }

    [Fact]
    public void Out_of_order_events_are_sorted_without_rejection()
    {
        var events = new[]
        {
            Fill("C1", "E1", "AUDUSD", 10, 0.7001m, 10, 0, 3),
            Order("C1", "AUDUSD", 10, 1)
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal("C1", plan.NormalizedEvents.First(e => e.Kind == R018NormalizedEventKind.Order).ClOrdId);
    }

    [Fact]
    public void Same_clordid_across_environments_is_identity_scope_risk_not_db_safe()
    {
        var events = new[]
        {
            Order("C1", "AUDUSD", 10, 1, environment: "demo"),
            Fill("C1", "E1", "AUDUSD", 10, 0.7001m, 10, 0, 2, environment: "demo"),
            Order("C1", "AUDUSD", 10, 3, environment: "live"),
            Fill("C1", "E2", "AUDUSD", 10, 0.7002m, 10, 0, 4, environment: "live")
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.False(plan.IdentityScope.FutureDbApplyAllowed);
        Assert.Contains(plan.IdentityScope.Risks, r => r.Contains("CLIENT_ORDER_ID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Manual_ui_fill_is_downgraded_to_observation_and_creates_no_fill()
    {
        var events = new[] { MANUAL_UIFill("C1", "AUDUSD") };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(0, plan.Validation.FillCount);
        Assert.Contains(plan.NormalizedEvents, e => e.Kind == R018NormalizedEventKind.ManualObservation);
    }

    [Fact]
    public void Eod_report_can_corroborate_fill()
    {
        var events = new[]
        {
            Order("C1", "AUDUSD", 10, 1),
            Fill("C1", "EOD-EXEC-1", "AUDUSD", 10, 0.7001m, 10, 0, 2, provenance: "EOD_LMAX_REPORT")
        };

        var plan = BuildPlan(CreateBundle(events));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Contains(plan.NormalizedEvents, e => e.Provenance == R018ProvenanceType.EOD_LMAX_REPORT && e.Kind == R018NormalizedEventKind.Fill);
    }

    [Fact]
    public void Offline_catalog_unique_match_is_canonical_linked()
    {
        var bundlePath = CreateBundle(ValidEvents());
        var catalogPath = Path.Combine(CreateTempDirectory(), "catalog.json");
        File.WriteAllText(catalogPath, """
        [{"model_run_id":"MR-CAT-1","source_run_id":"RTEST","approved_candidate_hash":"HASH123"}]
        """);

        var plan = BuildPlan(bundlePath, catalogPath);

        Assert.Equal(R018ImportBundleStatus.CANONICAL_LINKED, plan.Status);
        Assert.Equal("OFFLINE_CATALOG_UNIQUE_MATCH", plan.Lineage.ModelRunResolution);
    }

    [Fact]
    public void No_model_run_is_evidence_only()
    {
        var plan = BuildPlan(CreateBundle(ValidEvents()));

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal("NO_CANONICAL_MODEL_RUN_LINK", plan.Lineage.ModelRunResolution);
    }

    [Fact]
    public void Ambiguous_model_run_catalog_never_selects_arbitrarily()
    {
        var bundlePath = CreateBundle(ValidEvents());
        var catalogPath = Path.Combine(CreateTempDirectory(), "catalog.json");
        File.WriteAllText(catalogPath, """
        [
          {"model_run_id":"MR-1","source_run_id":"RTEST","approved_candidate_hash":"HASH123"},
          {"model_run_id":"MR-2","source_run_id":"RTEST","approved_candidate_hash":"HASH123"}
        ]
        """);

        var plan = BuildPlan(bundlePath, catalogPath);

        Assert.Equal(R018ImportBundleStatus.EVIDENCE_ONLY, plan.Status);
        Assert.Equal("OFFLINE_CATALOG_AMBIGUOUS", plan.Lineage.ModelRunResolution);
    }

    [Fact]
    public void Missing_deadline_is_preserved_as_missing_not_derived()
    {
        var bundle = new R018ArtifactBundleReader().Read(CreateBundle(ValidEvents(), includeDeadline: false));

        Assert.Null(bundle.Manifest.DeadlineUtc);
    }

    [Fact]
    public void Rerun_is_deterministic_outside_generation_timestamp()
    {
        var path = CreateBundle(ValidEvents());
        var first = BuildPlan(path);
        var second = BuildPlan(path);

        Assert.Equal(first.DeterministicContentHash, second.DeterministicContentHash);
    }

    [Fact]
    public void Cli_rejects_urls_connection_strings_apply_and_missing_guards()
    {
        var cli = new R018OfflineImportPlanCli();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = cli.Execute(new[]
        {
            "build-r018-import-plan",
            "--bundle",
            "https://example.invalid/bundle",
            "--output",
            "Server=prod;Database=x",
            "--apply"
        }, output, error);

        Assert.Equal(2, exit);
        var text = error.ToString();
        Assert.Contains("URL_REJECTED:bundle", text);
        Assert.Contains("CONNECTION_STRING_REJECTED:output", text);
        Assert.Contains("FORBIDDEN_FLAG:--apply", text);
        Assert.Contains("MISSING_NO_DB_FLAG", text);
        Assert.Contains("MISSING_NO_NETWORK_FLAG", text);
    }

    [Fact]
    public void Cli_rejects_output_inside_source_bundle_and_secrets()
    {
        var bundlePath = CreateBundle(ValidEvents());
        File.WriteAllText(Path.Combine(bundlePath, "credential.txt"), "password=abc");
        var outputPath = Path.Combine(bundlePath, "out");
        var cli = new R018OfflineImportPlanCli();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = cli.Execute(new[]
        {
            "build-r018-import-plan",
            "--bundle",
            bundlePath,
            "--output",
            outputPath,
            "--no-db",
            "--no-network"
        }, output, error);

        Assert.Equal(2, exit);
        Assert.Contains("OUTPUT_INSIDE_SOURCE_BUNDLE_REJECTED", error.ToString());
    }

    [Fact]
    public void Cli_writes_required_outputs_for_valid_bundle()
    {
        var bundlePath = CreateBundle(ValidEvents());
        var outputPath = Path.Combine(CreateTempDirectory(), "out");
        var cli = new R018OfflineImportPlanCli();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = cli.Execute(new[]
        {
            "build-r018-import-plan",
            "--bundle",
            bundlePath,
            "--output",
            outputPath,
            "--no-db",
            "--no-network",
            "--code-commit",
            "TESTCOMMIT"
        }, output, error);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(outputPath, "bundle_manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "validation_report.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "normalized_events.jsonl")));
        Assert.True(File.Exists(Path.Combine(outputPath, "lineage_report.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "identity_scope_report.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "import_plan_v1.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "parity_report.csv")));
        Assert.True(File.Exists(Path.Combine(outputPath, "human_summary.md")));
    }

    [Fact]
    public void M1c_source_introduces_no_network_db_gateway_or_r009_runtime_boundary()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/R018ImportPlanning/R018ImportPlanning.cs"));
        var cli = File.ReadAllText(Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.R018ImportPlan/Program.cs"));
        var combined = source + cli;

        Assert.DoesNotContain("TcpClient", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Socket", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("IVenueExecutionGateway", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessModelRunService", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Databento", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountAPI", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R009", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--apply", cli, StringComparison.Ordinal);
    }

    private static R018ImportPlan BuildPlan(string bundlePath, string? modelRunCatalogPath = null)
    {
        var bundle = new R018ArtifactBundleReader().Read(bundlePath);
        return new R018ImportPlanBuilder().Build(bundle, modelRunCatalogPath, "TESTCOMMIT");
    }

    private static string CreateBundle(
        IReadOnlyList<string> eventLines,
        string environment = "demo",
        string brokerAccount = "LMAX-DEMO-ACCOUNT",
        string venue = "LMAX",
        string quantityUnit = "LMAX_CONTRACTS",
        string? modelRunId = null,
        string? artifactHashOverride = null,
        bool includeDeadline = true)
    {
        var path = CreateTempDirectory();
        var eventsPath = Path.Combine(path, "events.jsonl");
        File.WriteAllText(eventsPath, string.Join(Environment.NewLine, eventLines));
        var hash = artifactHashOverride ?? R018ArtifactBundleReader.ComputeFileSha256(eventsPath);
        var deadline = includeDeadline ? ",\"deadline_utc\":\"2026-06-24T12:15:30Z\"" : string.Empty;
        var modelRun = modelRunId is null ? string.Empty : $",\"model_run_id\":\"{modelRunId}\"";
        var manifest = $$"""
        {
          "schema_version":"r018_artifact_bundle_manifest_v1",
          "environment":"{{environment}}",
          "broker_account":"{{brokerAccount}}",
          "venue":"{{venue}}",
          "source_run_id":"RTEST",
          "approved_candidate_hash":"HASH123",
          "quantity_unit":"{{quantityUnit}}",
          "target_close_utc":"2026-06-24T12:15:00Z",
          "decision_utc":"2026-06-24T12:14:00Z",
          "effective_from_utc":"2026-06-24T12:15:00Z"{{deadline}}{{modelRun}},
          "artifacts":[{"path":"events.jsonl","sha256":"{{hash}}","kind":"events"}]
        }
        """;
        File.WriteAllText(Path.Combine(path, "r018_bundle_manifest_v1.json"), manifest);
        return path;
    }

    private static IReadOnlyList<string> ValidEvents()
        => new[]
        {
            Order("C1", "AUDUSD", 10, 1),
            ExecutionReport("C1", "ER-ACK", "AUDUSD", "0", "0", cum: 0, leaves: 10, localOrder: 2),
            Fill("C1", "E1", "AUDUSD", 10, 0.7001m, 10, 0, 3)
        };

    private static string Order(string clOrdId, string symbol, decimal qty, long localOrder, string environment = "demo")
        => JsonSerializer.Serialize(new
        {
            event_type = "NewOrderSingle",
            provenance = "RAW_FIX",
            environment,
            broker_account = "LMAX-DEMO-ACCOUNT",
            venue = "LMAX",
            clordid = clOrdId,
            symbol,
            security_id = "4007",
            side = "BUY",
            quantity_unit = "LMAX_CONTRACTS",
            phase_id = "PHASE_1",
            wave_id = "W1",
            order_qty = qty,
            price = 0.7000m,
            order_type = "LIMIT",
            time_in_force = "0",
            local_event_order = localOrder,
            source_timestamp_utc = "2026-06-24T12:14:59Z"
        });

    private static string ExecutionReport(string clOrdId, string execId, string symbol, string execType, string ordStatus, decimal cum, decimal leaves, long localOrder)
        => JsonSerializer.Serialize(new
        {
            event_type = "ExecutionReport",
            provenance = "RAW_FIX",
            clordid = clOrdId,
            execid = execId,
            symbol,
            security_id = "4007",
            side = "BUY",
            quantity_unit = "LMAX_CONTRACTS",
            exec_type = execType,
            ord_status = ordStatus,
            cum_qty = cum,
            leaves_qty = leaves,
            local_event_order = localOrder,
            source_timestamp_utc = "2026-06-24T12:15:00Z"
        });

    private static string Fill(
        string clOrdId,
        string execId,
        string symbol,
        decimal lastQty,
        decimal price,
        decimal cum,
        decimal leaves,
        long localOrder,
        string provenance = "RAW_FIX",
        string environment = "demo")
        => JsonSerializer.Serialize(new
        {
            event_type = "Fill",
            provenance,
            environment,
            broker_account = "LMAX-DEMO-ACCOUNT",
            venue = "LMAX",
            clordid = clOrdId,
            execid = execId,
            symbol,
            security_id = "4007",
            side = "BUY",
            quantity_unit = "LMAX_CONTRACTS",
            last_qty = lastQty,
            fill_price = price,
            cum_qty = cum,
            leaves_qty = leaves,
            local_event_order = localOrder,
            source_timestamp_utc = "2026-06-24T12:15:01Z"
        });

    private static string MANUAL_UIFill(string clOrdId, string symbol)
        => JsonSerializer.Serialize(new
        {
            event_type = "Fill",
            provenance = "MANUAL_UI",
            clordid = clOrdId,
            symbol,
            security_id = "4007",
            side = "BUY",
            quantity_unit = "LMAX_CONTRACTS",
            last_qty = 10,
            fill_price = 0.7001m,
            local_event_order = 1
        });

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "r018-m1c-" + Guid.NewGuid().ToString("N"));
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
}


