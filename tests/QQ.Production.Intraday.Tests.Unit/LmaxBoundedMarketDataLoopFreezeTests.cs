using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxBoundedMarketDataLoopFreezeTests
{
    [Fact]
    public async Task Freeze_reads_existing_summary_and_preserves_status_only_boundaries()
    {
        var source = CreateSourcePackage("lmax-bounded-md-loop-demo-002");
        var output = TempRoot("lmax-bounded-md-loop-demo-002-freeze-test");

        var result = await new LmaxBoundedMarketDataLoopFreezeWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopFreezeOptions("lmax-bounded-md-loop-demo-002-freeze-test", output, "lmax-bounded-md-loop-demo-002", source),
            CancellationToken.None);

        Assert.Equal("PASS", result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]);
        Assert.Equal("PASS", result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_STATUS"]);
        Assert.Equal("SANDBOX_LOOP_CAPTURED", result.FreezeReport["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]);
        Assert.Equal("NONE", result.FreezeReport["PRIMARY_FAILURE_REASON"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_NO_EXECUTION"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]);
        Assert.Equal("YES", result.FreezeReport["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]);
        Assert.Equal("AdoptedWithWarnings", result.FreezeReport["MARKETDATA_LMAX_DB_STATUS"]);
        Assert.Equal("BLOCKED", result.FreezeReport["PRODUCTION_STATUS"]);
        Assert.Equal("PASS", result.SecretScanReport["SECRET_SCAN_STATUS"]);
        Assert.Equal("YES", result.SecretScanReport["NO_CREDENTIAL_VALUES_PERSISTED"]);
        Assert.Equal("PASS", result.BoundaryVerification["BOUNDARY_STATUS"]);
    }

    [Fact]
    public async Task Evidence_matrix_contains_required_rows_and_manifest_hash_is_valid()
    {
        var source = CreateSourcePackage("lmax-bounded-md-loop-demo-002");
        var output = TempRoot("lmax-bounded-md-loop-demo-002-freeze-manifest-test");

        var result = await new LmaxBoundedMarketDataLoopFreezeWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopFreezeOptions("lmax-bounded-md-loop-demo-002-freeze-manifest-test", output, "lmax-bounded-md-loop-demo-002", source),
            CancellationToken.None);

        var names = result.EvidenceMatrix.Select(row => row["evidenceName"].ToString()).ToArray();
        Assert.Contains("Manual operator run summary exists", names);
        Assert.Contains("Events JSONL exists", names);
        Assert.Contains("No DB write = YES", names);
        Assert.Contains("No execution = YES", names);
        Assert.Contains("No production endpoint = YES", names);
        Assert.Contains("No trading endpoint = YES", names);
        Assert.Contains("No scheduler = YES", names);
        Assert.Contains("No credential values persisted", names);
        Assert.Contains("No A/H/I", names);
        Assert.Contains("No Qubes/PMS/OMS/EMS", names);
        Assert.Contains("No order/fill/route/broker/live-state", names);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/lmax_bounded_loop_demo_freeze_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_loop_demo_evidence_matrix.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_loop_demo_boundary_verification.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_loop_demo_secret_scan_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/lmax_bounded_loop_demo_freeze_summary.md", manifest, StringComparison.Ordinal);

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
    }

    [Fact]
    public async Task Secret_scan_detects_unredacted_fix_password_tag()
    {
        var source = CreateSourcePackage("lmax-bounded-md-loop-demo-002", rawFix: "8=FIX.4.4|35=A|553=user|554=secret|10=000");
        var output = TempRoot("lmax-bounded-md-loop-demo-002-freeze-secret-fail-test");

        var result = await new LmaxBoundedMarketDataLoopFreezeWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopFreezeOptions("lmax-bounded-md-loop-demo-002-freeze-secret-fail-test", output, "lmax-bounded-md-loop-demo-002", source),
            CancellationToken.None);

        Assert.Equal("FAIL", result.SecretScanReport["SECRET_SCAN_STATUS"]);
        Assert.Equal("NO", result.SecretScanReport["NO_CREDENTIAL_VALUES_PERSISTED"]);
        Assert.Equal("WARN", result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]);
    }

    [Fact]
    public async Task Missing_source_summary_fails_freeze()
    {
        var source = TempRoot("missing-source-package");
        Directory.CreateDirectory(Path.Combine(source, "marketdata"));
        Directory.CreateDirectory(Path.Combine(source, "10_validation"));
        var output = TempRoot("lmax-bounded-md-loop-demo-002-freeze-missing-test");

        var result = await new LmaxBoundedMarketDataLoopFreezeWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopFreezeOptions("lmax-bounded-md-loop-demo-002-freeze-missing-test", output, "lmax-bounded-md-loop-demo-002", source),
            CancellationToken.None);

        Assert.Equal("FAIL", result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]);
    }

    private static string CreateSourcePackage(string runKey, string rawFix = "8=FIX.4.4|35=W|49=LMXBDM|56=[redacted]|55=GBPUSD|48=4002|22=8|269=0|270=1.2|269=1|270=1.3|10=000")
    {
        var root = TempRoot(runKey);
        Directory.CreateDirectory(Path.Combine(root, "marketdata"));
        Directory.CreateDirectory(Path.Combine(root, "10_validation"));
        Directory.CreateDirectory(Path.Combine(root, "share"));

        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["run_key"] = runKey,
            ["status"] = "PASS",
            ["messages_read"] = 5,
            ["logons"] = 1,
            ["snapshots"] = 4,
            ["incrementals"] = 0,
            ["terminal_timeouts"] = 1,
            ["primary_failure_reason"] = "NONE",
            ["final_fix_session_state"] = "MarketDataObserved"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_events.jsonl"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["run_key"] = runKey,
            ["fix_msg_type"] = "W",
            ["classification"] = "Snapshot",
            ["raw_redacted_fix"] = rawFix
        }) + Environment.NewLine);
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_marketdata_loop_capture_report.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["LMAX_BOUNDED_LOOP_DEMO_STATUS"] = "PASS",
            ["LMAX_BOUNDED_LOOP_FAKE_STATUS"] = "PASS",
            ["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"] = "YES",
            ["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_DB_WRITE"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_EXECUTION"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_SCHEDULER"] = "YES",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"] = "SANDBOX_LOOP_CAPTURED",
            ["PRIMARY_FAILURE_REASON"] = "NONE"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_marketdata_loop_boundary_report.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["PRODUCTION_ENDPOINT_USED"] = "NO",
            ["TRADING_ENDPOINT_USED"] = "NO",
            ["ORDER_MESSAGES_SENT"] = "NO",
            ["FILL_ARTIFACTS_CREATED"] = "NO",
            ["ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"] = "NO",
            ["DB_WRITE_ATTEMPTED"] = "NO",
            ["MIGRATION_ATTEMPTED"] = "NO",
            ["QUBES_EXECUTED"] = "NO",
            ["QUBES_WEIGHTS_GENERATED"] = "NO",
            ["PMS_OMS_EMS_TOUCHED"] = "NO",
            ["MANAGER_ANUBIS_TOUCHED"] = "NO",
            ["A_H_I_CREATED"] = "NO",
            ["SCHEDULER_STARTED"] = "NO",
            ["WORKER_SERVICE_STARTED"] = "NO",
            ["CREDENTIAL_VALUES_PERSISTED"] = "NO"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_marketdata_loop_capture_report.md"), "capture");
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_marketdata_loop_boundary_report.md"), "boundary");
        return root;
    }

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-lmax-bounded-loop-freeze-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
