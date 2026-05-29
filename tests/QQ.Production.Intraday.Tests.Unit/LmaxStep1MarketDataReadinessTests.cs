using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxStep1MarketDataReadinessTests
{
    [Fact]
    public async Task Readiness_package_preserves_sandbox_pass_and_production_blocked()
    {
        var source = CreateSourceRun();
        var output = TempRoot("lmax-step1-readiness-test");

        var result = await new LmaxStep1MarketDataReadinessWriter().WriteAsync(
            new LmaxStep1ReadinessOptions("lmax-step1-readiness-test", output, source, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        Assert.Equal("PASS", result.Report.Step1FetchLiveMarketDataSandboxStatus);
        Assert.Equal("BLOCKED", result.Report.Step1FetchLiveMarketDataProductionStatus);
        Assert.Equal("AdoptedWithWarnings", result.Report.MarketDataLmaxDbStatus);
        Assert.Equal("YES", result.Report.NoCredentialValuesPersisted);
        Assert.Equal("PASS", result.Report.NoProductionExecutionPath);
        Assert.Equal(3, result.Report.LmaxSnapshotsObserved);
        Assert.Equal(0, result.Report.LmaxIncrementalsObserved);
        Assert.Equal(0, result.Report.LmaxUnknownMessages);
        Assert.Equal("YES", result.Report.LmaxTerminalTimeoutClean);
    }

    [Fact]
    public async Task Evidence_matrix_contains_required_evidence_and_boundary_non_validations()
    {
        var source = CreateSourceRun();
        var output = TempRoot("lmax-step1-readiness-evidence");

        var result = await new LmaxStep1MarketDataReadinessWriter().WriteAsync(
            new LmaxStep1ReadinessOptions("lmax-step1-readiness-evidence", output, source, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        var names = result.EvidenceMatrix.Evidence.Select(x => x.EvidenceName).ToArray();
        Assert.Contains("LMAX demo Logon observed", names);
        Assert.Contains("MarketDataRequest accepted", names);
        Assert.Contains("Snapshot 35=W observed", names);
        Assert.Contains("Unknown count zero after 35=A classification", names);
        Assert.Contains("Terminal timeout non-primary", names);
        Assert.Contains("No DB write", names);
        Assert.Contains("No A/H/I", names);
        Assert.Contains("No Qubes/PMS/OMS/EMS", names);
        Assert.Contains("No order/fill/route/broker/live-state", names);
        Assert.Contains("No production endpoint", names);
        Assert.Contains("No trading endpoint", names);
        Assert.All(result.EvidenceMatrix.Evidence, x => Assert.Equal("PRESENT", x.Status));

        var boundary = File.ReadAllText(Path.Combine(output, "10_validation", "lmax_step1_boundary_report.md"));
        Assert.Contains("This does not validate production live.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate DB persistence.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate Qubes signal generation.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate PMS/OMS/EMS handoff.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate execution.", boundary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manifest_hashes_are_valid_and_no_forbidden_artifacts_are_created()
    {
        var source = CreateSourceRun();
        var output = TempRoot("lmax-step1-readiness-manifest");

        await new LmaxStep1MarketDataReadinessWriter().WriteAsync(
            new LmaxStep1ReadinessOptions("lmax-step1-readiness-manifest", output, source, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/lmax_step1_marketdata_readiness_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_step1_marketdata_evidence_matrix.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_step1_boundary_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/lmax_step1_marketdata_readiness_summary.md", manifest, StringComparison.Ordinal);

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
        Assert.Empty(Directory.EnumerateFiles(output, "*fill*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*route*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*broker*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*live-state*", SearchOption.AllDirectories));

        var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("fix-order.london-demo.lmax.com", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=F", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=G", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", allText, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSourceRun()
    {
        var root = TempRoot("lmax-sandbox-md-r002-demo-retry-005");
        Directory.CreateDirectory(Path.Combine(root, "marketdata"));
        Directory.CreateDirectory(Path.Combine(root, "10_validation"));
        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json"), JsonSerializer.Serialize(new
        {
            runKey = "lmax-sandbox-md-r002-demo-retry-005",
            snapshots = 3,
            incrementals = 0,
            unknown = 0,
            logonCount = 1,
            terminalTimeoutCount = 1,
            sessionRejectCount = 0,
            logoutCount = 0,
            credentialsRejectedCount = 0,
            primaryFailureReason = "NONE",
            status = "PASS"
        }));
        File.WriteAllText(Path.Combine(root, "marketdata", "lmax_marketdata_events.jsonl"), string.Join(Environment.NewLine, [
            """{"fixMsgType":"A","classification":"Logon","rawRedactedFix":"35=A|49=LMXBDM|56=[redacted]"}""",
            """{"fixMsgType":"W","classification":"Snapshot","bid":1.1,"ask":1.2,"rawRedactedFix":"35=W|48=4002|22=8"}""",
            """{"fixMsgType":"W","classification":"Snapshot","bid":1.1,"ask":1.2,"rawRedactedFix":"35=W|48=4002|22=8"}""",
            """{"fixMsgType":"W","classification":"Snapshot","bid":1.1,"ask":1.2,"rawRedactedFix":"35=W|48=4002|22=8"}""",
            """{"fixMsgType":"TIMEOUT","classification":"Timeout","rawRedactedFix":""}"""
        ]));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_success_classification.json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["FIRST_STEP_FETCH_LIVE_MARKET_DATA_STATUS"] = "SANDBOX_CAPTURED",
            ["LMAX_SANDBOX_EXTERNAL_CAPTURE_STATUS"] = "PASS",
            ["PRIMARY_FAILURE_REASON"] = "NONE",
            ["SNAPSHOTS_OBSERVED"] = 3,
            ["INCREMENTALS_OBSERVED"] = 0,
            ["LOGON_COUNT"] = 1,
            ["UNKNOWN_COUNT"] = 0,
            ["SESSION_REJECTS"] = 0,
            ["LOGOUTS"] = 0,
            ["CREDENTIALS_REJECTED"] = 0,
            ["TERMINAL_TIMEOUT_COUNT"] = 1,
            ["BOUNDED_CAPTURE_ENDED_CLEANLY"] = "YES"
        }));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_success_classification.md"), "# Success\n");
        return root;
    }

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-lmax-step1-readiness-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }
}
