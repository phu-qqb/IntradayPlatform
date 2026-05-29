using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class MarketDataDbPersistencePreflightTests
{
    [Fact]
    public async Task Missing_db_connection_yields_warn_without_external_or_mutation()
    {
        var source = CreateSourceRun("lmax-bounded-md-loop-demo-002");
        var freeze = CreateFreezePackage("lmax-bounded-md-loop-demo-002-freeze-001");
        var output = TempRoot("marketdata-db-persistence-preflight-r001");

        var result = await new MarketDataDbPersistencePreflightWriter().WriteAsync(
            Options(output, source, freeze, connectionPresent: false),
            new MissingConnectionStringMarketDataDbInventoryProvider("QQPRODUCTIONINTRADAY_CONNECTION_STRING"),
            CancellationToken.None);

        Assert.Equal("WARN", result.PreflightReport["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"]);
        Assert.Equal("MISSING", result.PreflightReport["DB_CONNECTION_STATUS"]);
        Assert.Equal("PARTIAL", result.PreflightReport["DB_SCHEMA_INVENTORY_STATUS"]);
        Assert.Equal("MISSING_CONNECTION_STRING", result.PreflightReport["DB_ROW_COUNTS_STATUS"]);
        Assert.Equal("WARN", result.PreflightReport["LMAX_EVENT_TO_DB_MAPPING_STATUS"]);
        Assert.Null(result.PreflightReport["TARGET_STORAGE_TABLE_RECOMMENDATION"]);
        Assert.Equal("NONE", result.PreflightReport["TARGET_STORAGE_TABLE_CONFIDENCE"]);
        Assert.Equal("BLOCKED", result.PreflightReport["DB_WRITE_GATE_STATUS"]);
        Assert.Equal("NO", result.PreflightReport["DB_MUTATION_ATTEMPTED"]);
        Assert.Equal("AdoptedWithWarnings", result.PreflightReport["MARKETDATA_LMAX_DB_STATUS"]);
        Assert.Equal("BLOCKED", result.PreflightReport["PRODUCTION_STATUS"]);
        Assert.Equal("BLOCKED", result.PreflightReport["EXECUTION_STATUS"]);
        Assert.Equal("CLOSE_DB_ROW_COUNT_EVIDENCE", result.PreflightReport["SAFE_NEXT_PHASE"]);
        Assert.Equal("PASS", result.BoundaryReport["BOUNDARY_STATUS"]);
        Assert.Equal("NO", result.BoundaryReport["LMAX_EXTERNAL_CALLS_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["DB_WRITE_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["MIGRATION_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["QUBES_EXECUTED"]);
        Assert.Equal("NO", result.BoundaryReport["PMS_OMS_EMS_TOUCHED"]);
        Assert.Equal("NO", result.BoundaryReport["A_H_I_CREATED"]);
    }

    [Fact]
    public async Task Source_events_and_table_mapping_are_reported_without_selecting_ohlc_or_trade_tables()
    {
        var source = CreateSourceRun("lmax-bounded-md-loop-demo-002");
        var freeze = CreateFreezePackage("lmax-bounded-md-loop-demo-002-freeze-001");
        var output = TempRoot("marketdata-db-persistence-preflight-mapping");

        var result = await new MarketDataDbPersistencePreflightWriter().WriteAsync(
            Options(output, source, freeze, connectionPresent: true),
            new FakeDbInventoryProvider(new MarketDataDbInventoryResult(
                "PRESENT",
                "PRESENT",
                "PRESENT",
                true,
                "QQPRODUCTIONINTRADAY_CONNECTION_STRING",
                MarketDataDbPersistencePreflightWriter.StaticCandidateTables("fake-db-select-only", rowCountsAvailable: true)
                    .Select(table => table with { RowCount = table.TableName == "MarketDataSnapshots" ? 12 : 0, EvidenceStatus = table.TableName == "MarketDataSnapshots" ? "PRESENT" : "PRESENT_EMPTY" })
                    .ToArray(),
                ["SELECT COUNT_BIG(*) FROM [dbo].[MarketDataSnapshots]"],
                [])),
            CancellationToken.None);

        Assert.Equal("PASS", result.PreflightReport["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"]);
        Assert.Equal("PASS", result.PreflightReport["LMAX_EVENT_TO_DB_MAPPING_STATUS"]);
        Assert.Equal("MarketDataSnapshots", result.PreflightReport["TARGET_STORAGE_TABLE_RECOMMENDATION"]);
        Assert.Equal("MEDIUM", result.PreflightReport["TARGET_STORAGE_TABLE_CONFIDENCE"]);
        Assert.Equal(2, result.PreflightReport["LMAX_EVENTS_COUNT"]);
        Assert.Equal(1, result.PreflightReport["LMAX_SNAPSHOTS_COUNT"]);
        Assert.Equal(1, result.PreflightReport["LMAX_INCREMENTALS_COUNT"]);

        var mappingJson = File.ReadAllText(Path.Combine(output, "10_validation", "marketdata_lmax_event_to_db_mapping_report.json"));
        Assert.Contains("\"tableName\": \"MarketDataSnapshots\"", mappingJson, StringComparison.Ordinal);
        Assert.Contains("\"can_store_quote\": \"YES\"", mappingJson, StringComparison.Ordinal);
        Assert.Contains("OHLC/bar storage only; do not use for raw LMAX quote snapshots.", mappingJson, StringComparison.Ordinal);
        Assert.Contains("Trade summary table shape; not suitable for quote snapshots.", mappingJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blockers_include_write_gate_and_manifest_hashes_are_valid()
    {
        var source = CreateSourceRun("lmax-bounded-md-loop-demo-002");
        var freeze = CreateFreezePackage("lmax-bounded-md-loop-demo-002-freeze-001");
        var output = TempRoot("marketdata-db-persistence-preflight-manifest");

        var result = await new MarketDataDbPersistencePreflightWriter().WriteAsync(
            Options(output, source, freeze, connectionPresent: false),
            new MissingConnectionStringMarketDataDbInventoryProvider("QQPRODUCTIONINTRADAY_CONNECTION_STRING"),
            CancellationToken.None);

        var blockers = File.ReadAllText(Path.Combine(output, "10_validation", "marketdata_db_persistence_blockers_report.json"));
        Assert.Contains("WRITE_GATE_NOT_APPROVED", blockers, StringComparison.Ordinal);
        Assert.Contains("IDEMPOTENCY_KEY_UNDEFINED", blockers, StringComparison.Ordinal);
        Assert.Contains("MARKETDATA_LMAX_DB_ADOPTED_WITH_WARNINGS", blockers, StringComparison.Ordinal);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/marketdata_db_persistence_preflight_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_db_schema_inventory.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_lmax_event_to_db_mapping_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_db_row_counts_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_db_persistence_blockers_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_db_no_mutation_boundary_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/marketdata_db_persistence_preflight_summary.md", manifest, StringComparison.Ordinal);

        var manifestHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "manifest.json")))).ToLowerInvariant();
        Assert.Equal($"{manifestHash}  manifest.json", File.ReadAllText(Path.Combine(output, "manifest.sha256")).Trim());
        var hashes = File.ReadAllText(Path.Combine(output, "hashes.json"));
        Assert.Contains("manifest.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("hashes.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest.sha256", hashes, StringComparison.Ordinal);

        Assert.Empty(Directory.EnumerateFiles(output, "A.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "H.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "I.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*order*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*route*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*broker*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*live-state*", SearchOption.AllDirectories));
        Assert.DoesNotContain("Server=", string.Join(Environment.NewLine, result.Files), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Row_count_evidence_missing_connection_is_not_attempted_and_keeps_write_gate_blocked()
    {
        var source = CreateSourceRun("lmax-bounded-md-loop-demo-002");
        var freeze = CreateFreezePackage("lmax-bounded-md-loop-demo-002-freeze-001");
        var output = TempRoot("marketdata-db-row-count-evidence-r001");

        var result = await new MarketDataDbPersistencePreflightWriter().WriteAsync(
            new MarketDataDbPersistencePreflightOptions(
                "marketdata-db-row-count-evidence-r001",
                ".",
                output,
                source,
                freeze,
                "QQPRODUCTIONINTRADAY_CONNECTION_STRING",
                ConnectionStringPresent: false,
                NoExternal: true,
                NoExecution: true,
                NoDbWrite: true),
            new MissingConnectionStringMarketDataDbInventoryProvider("QQPRODUCTIONINTRADAY_CONNECTION_STRING"),
            CancellationToken.None);

        Assert.Equal("NOT_ATTEMPTED", result.PreflightReport["MARKETDATA_DB_ROW_COUNT_EVIDENCE_STATUS"]);
        Assert.Equal("MISSING", result.PreflightReport["DB_CONNECTION_STATUS"]);
        Assert.Equal("MISSING_CONNECTION_STRING", result.PreflightReport["DB_ROW_COUNTS_STATUS"]);
        Assert.Equal("BLOCKED", result.PreflightReport["DB_WRITE_GATE_STATUS"]);
        Assert.Equal("NO", result.PreflightReport["DB_MUTATION_ATTEMPTED"]);
        Assert.Equal("AdoptedWithWarnings", result.PreflightReport["MARKETDATA_LMAX_DB_STATUS"]);
        Assert.Equal("CLOSE_DB_CONNECTION_SECRET_ACCESS", result.PreflightReport["SAFE_NEXT_PHASE"]);
        Assert.True(File.Exists(Path.Combine(output, "10_validation", "marketdata_db_row_count_evidence_report.json")));
        Assert.True(File.Exists(Path.Combine(output, "share", "marketdata_db_row_count_evidence_summary.md")));
        Assert.False(File.Exists(Path.Combine(output, "10_validation", "marketdata_db_persistence_preflight_report.json")));
        Assert.False(File.Exists(Path.Combine(output, "10_validation", "marketdata_db_persistence_blockers_report.json")));
    }

    private static MarketDataDbPersistencePreflightOptions Options(string output, string source, string freeze, bool connectionPresent)
        => new(
            "marketdata-db-persistence-preflight-r001",
            ".",
            output,
            source,
            freeze,
            "QQPRODUCTIONINTRADAY_CONNECTION_STRING",
            connectionPresent,
            NoExternal: true,
            NoExecution: true,
            NoDbWrite: true);

    private static string CreateSourceRun(string runKey)
    {
        var root = TempRoot(runKey);
        Directory.CreateDirectory(Path.Combine(root, "marketdata"));
        Directory.CreateDirectory(Path.Combine(root, "10_validation"));

        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["run_key"] = runKey,
            ["source"] = "lmax-demo",
            ["status"] = "PASS",
            ["messages_read"] = 5,
            ["snapshots"] = 1,
            ["incrementals"] = 1,
            ["primary_failure_reason"] = "NONE",
            ["final_fix_session_state"] = "MarketDataObserved"
        }, JsonOptions));

        var snapshot = new Dictionary<string, object?>
        {
            ["run_key"] = runKey,
            ["received_at_utc"] = "2026-05-26T10:00:00Z",
            ["environment"] = "demo",
            ["source"] = "lmax-demo",
            ["instrument"] = "GBPUSD",
            ["security_id"] = "4002",
            ["security_id_source"] = "8",
            ["fix_msg_type"] = "W",
            ["classification"] = "Snapshot",
            ["bid"] = 1.2701,
            ["ask"] = 1.2703,
            ["bid_size"] = 1000000,
            ["ask_size"] = 1000000,
            ["raw_redacted_fix"] = "8=FIX.4.4|35=W|49=[redacted]|56=LMXBDM|48=4002|22=8|10=000"
        };
        var incremental = new Dictionary<string, object?>(snapshot)
        {
            ["received_at_utc"] = "2026-05-26T10:00:01Z",
            ["fix_msg_type"] = "X",
            ["classification"] = "Incremental",
            ["raw_redacted_fix"] = "8=FIX.4.4|35=X|49=[redacted]|56=LMXBDM|48=4002|22=8|10=000"
        };
        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_events.jsonl"),
            JsonSerializer.Serialize(snapshot, CompactJsonOptions) + Environment.NewLine +
            JsonSerializer.Serialize(incremental, CompactJsonOptions) + Environment.NewLine);

        return root;
    }

    private static string CreateFreezePackage(string runKey)
    {
        var root = TempRoot(runKey);
        Directory.CreateDirectory(Path.Combine(root, "10_validation"));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_bounded_loop_demo_freeze_report.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"] = "PASS",
            ["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"] = "SANDBOX_LOOP_CAPTURED",
            ["PRIMARY_FAILURE_REASON"] = "NONE"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_bounded_loop_demo_evidence_matrix.json"), "[]");
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_bounded_loop_demo_boundary_verification.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["BOUNDARY_STATUS"] = "PASS"
        }, JsonOptions));
        return root;
    }

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-marketdata-db-preflight-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeDbInventoryProvider(MarketDataDbInventoryResult result) : IMarketDataDbInventoryProvider
    {
        public Task<MarketDataDbInventoryResult> InspectAsync(CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJsonOptions = new(JsonSerializerDefaults.Web);
}
