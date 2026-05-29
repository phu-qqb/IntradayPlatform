using System.Security.Cryptography;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxBoundedMarketDataLoopDesignTests
{
    [Fact]
    public async Task Design_preserves_status_only_marketdata_boundaries()
    {
        var output = TempRoot("lmax-bounded-md-loop-design-test");
        var result = await new LmaxBoundedMarketDataLoopDesignWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopDesignOptions("lmax-bounded-md-loop-design-test", output, RepoRoot()),
            CancellationToken.None);

        Assert.Equal("PASS", result.Design["LMAX_BOUNDED_LOOP_DESIGN_STATUS"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_NO_EXECUTION"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]);
        Assert.Equal("YES", result.Design["LMAX_BOUNDED_LOOP_SECRET_REDACTION_REQUIRED"]);
        Assert.Equal("AdoptedWithWarnings", result.Design["MARKETDATA_LMAX_DB_STATUS"]);
        Assert.Equal("IMPLEMENT_BOUNDED_LOOP_FAKE_ONLY", result.Design["SAFE_NEXT_PHASE"]);
    }

    [Fact]
    public async Task Artifacts_contract_and_boundary_block_execution_db_qubes_and_scheduler()
    {
        var output = TempRoot("lmax-bounded-md-loop-contract-test");
        var result = await new LmaxBoundedMarketDataLoopDesignWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopDesignOptions("lmax-bounded-md-loop-contract-test", output, RepoRoot()),
            CancellationToken.None);

        var marketdataArtifacts = ((IEnumerable<string>)result.ArtifactsContract["marketdataArtifacts"]).ToArray();
        Assert.Contains("marketdata/lmax_marketdata_events.jsonl", marketdataArtifacts);
        Assert.Contains("marketdata/lmax_marketdata_summary.json", marketdataArtifacts);

        var eventColumns = ((IEnumerable<string>)result.ArtifactsContract["eventJsonlColumns"]).ToArray();
        Assert.Contains("raw_redacted_fix", eventColumns);
        Assert.Contains("security_id", eventColumns);
        Assert.Contains("classification", eventColumns);

        Assert.Equal("NO", result.Boundary["FIX_SESSION_OPENED"]);
        Assert.Equal("NO", result.Boundary["LMAX_CALL_MADE"]);
        Assert.Equal("NO", result.Boundary["DB_WRITE"]);
        Assert.Equal("NO", result.Boundary["DB_MIGRATION"]);
        Assert.Equal("NO", result.Boundary["A_H_I_GENERATED"]);
        Assert.Equal("NO", result.Boundary["QUBES_EXECUTED"]);
        Assert.Equal("NO", result.Boundary["PMS_OMS_EMS_TOUCHED"]);
        Assert.Equal("NO", result.Boundary["SCHEDULER_WORKER_SERVICE_STARTED"]);
        Assert.Equal("NO", result.Boundary["ORDER_FILL_ROUTE_BROKER_LIVE_STATE_CREATED"]);
        Assert.Equal("NO", result.Boundary["CREDENTIAL_VALUES_PERSISTED"]);
    }

    [Fact]
    public async Task Manifest_hashes_are_valid_and_forbidden_artifacts_absent()
    {
        var output = TempRoot("lmax-bounded-md-loop-manifest-test");
        await new LmaxBoundedMarketDataLoopDesignWriter().WriteAsync(
            new LmaxBoundedMarketDataLoopDesignOptions("lmax-bounded-md-loop-manifest-test", output, RepoRoot()),
            CancellationToken.None);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/lmax_bounded_marketdata_loop_design.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_marketdata_loop_gates.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_marketdata_artifacts_contract.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_bounded_marketdata_no_execution_boundary.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/lmax_bounded_marketdata_loop_design_summary.md", manifest, StringComparison.Ordinal);

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
        Assert.DoesNotContain("\"CredentialValue\"", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"PasswordValue\"", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"SecretValue\"", allText, StringComparison.OrdinalIgnoreCase);
    }

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-lmax-bounded-md-loop-design-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "artifacts", "lmax-sandbox-marketdata")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
