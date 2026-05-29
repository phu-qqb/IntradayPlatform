using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class MarketDataStep1CrossThreadStatusTests
{
    [Fact]
    public async Task Cross_thread_report_preserves_status_only_boundaries()
    {
        var readiness = CreateReadinessPackage();
        var output = TempRoot("marketdata-step1-status-test");

        var result = await new MarketDataStep1CrossThreadStatusWriter().WriteAsync(
            new MarketDataStep1CrossThreadStatusOptions("marketdata-step1-status-test", output, readiness, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        Assert.Equal("PASS", result.Report["STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS"]);
        Assert.Equal("BLOCKED", result.Report["STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS"]);
        Assert.Equal("AdoptedWithWarnings", result.Report["MARKETDATA_LMAX_DB_STATUS"]);
        Assert.Equal("AdoptedWithWarnings", result.Report["MARKETDATA_CONTRACT_ADOPTION_STATUS"]);
        Assert.Equal("Partial", result.Report["MARKETDATA_DATA_READINESS_STATUS"]);
        Assert.Equal("Safe", result.Report["MARKETDATA_SECRETS_STATUS"]);
        Assert.Equal("Blocked / Safe", result.Report["MARKETDATA_PRODUCTION_PATH_STATUS"]);
        Assert.Equal("PASS", result.Report["NO_PRODUCTION_EXECUTION_PATH"]);
        Assert.Equal("YES", result.Report["NO_CREDENTIAL_VALUES_PERSISTED"]);
    }

    [Fact]
    public async Task Boundary_invariants_do_not_promote_production_persistence_or_execution()
    {
        var readiness = CreateReadinessPackage();
        var output = TempRoot("marketdata-step1-boundary-test");

        await new MarketDataStep1CrossThreadStatusWriter().WriteAsync(
            new MarketDataStep1CrossThreadStatusOptions("marketdata-step1-boundary-test", output, readiness, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        var boundary = File.ReadAllText(Path.Combine(output, "10_validation", "marketdata_step1_boundary_invariants.md"));
        Assert.Contains("This validates sandbox/demo bounded market data only.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate production live.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate continuous feed.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate DB persistence.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate Qubes signal generation.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate PMS/OMS/EMS handoff.", boundary, StringComparison.Ordinal);
        Assert.Contains("This does not validate execution.", boundary, StringComparison.Ordinal);
        Assert.Contains("MarketData-LMAX-DB remains AdoptedWithWarnings, not PASS complete and not FAIL.", boundary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manifest_hashes_are_valid_and_forbidden_artifacts_absent()
    {
        var readiness = CreateReadinessPackage();
        var output = TempRoot("marketdata-step1-manifest-test");

        await new MarketDataStep1CrossThreadStatusWriter().WriteAsync(
            new MarketDataStep1CrossThreadStatusOptions("marketdata-step1-manifest-test", output, readiness, "lmax-sandbox-md-r002-demo-retry-005"),
            CancellationToken.None);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/marketdata_step1_cross_thread_status_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/marketdata_step1_boundary_invariants.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/marketdata_step1_status_summary.md", manifest, StringComparison.Ordinal);

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

    private static string CreateReadinessPackage()
    {
        var root = TempRoot("lmax-step1-readiness-001");
        Directory.CreateDirectory(Path.Combine(root, "10_validation"));
        Directory.CreateDirectory(Path.Combine(root, "share"));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_step1_marketdata_readiness_report.json"), JsonSerializer.Serialize(new
        {
            lmaxSnapshotsObserved = 3,
            lmaxIncrementalsObserved = 0,
            lmaxUnknownMessages = 0,
            lmaxTerminalTimeoutClean = "YES",
            primaryFailureReason = "NONE"
        }));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_step1_marketdata_evidence_matrix.json"), JsonSerializer.Serialize(new
        {
            evidence = new[]
            {
                Evidence("LMAX demo Logon observed"),
                Evidence("MarketDataRequest accepted"),
                Evidence("Snapshot 35=W observed"),
                Evidence("Bid parsed"),
                Evidence("Ask parsed"),
                Evidence("Terminal timeout non-primary"),
                Evidence("No session reject"),
                Evidence("No logout / credentials rejected"),
                Evidence("No DB write"),
                Evidence("No A/H/I"),
                Evidence("No Qubes/PMS/OMS/EMS"),
                Evidence("No order/fill/route/broker/live-state"),
                Evidence("No production endpoint"),
                Evidence("No trading endpoint"),
                Evidence("No credential values persisted"),
                Evidence("MarketData-LMAX-DB remains AdoptedWithWarnings")
            }
        }));
        File.WriteAllText(Path.Combine(root, "10_validation", "lmax_step1_boundary_report.json"), "{}");
        File.WriteAllText(Path.Combine(root, "share", "lmax_step1_marketdata_readiness_summary.md"), "summary");
        File.WriteAllText(Path.Combine(root, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(root, "manifest.sha256"), "abc  manifest.json");
        File.WriteAllText(Path.Combine(root, "hashes.json"), "{}");
        return root;
    }

    private static object Evidence(string name)
        => new { evidenceName = name, status = "PRESENT" };

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-marketdata-step1-cross-thread-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }
}
