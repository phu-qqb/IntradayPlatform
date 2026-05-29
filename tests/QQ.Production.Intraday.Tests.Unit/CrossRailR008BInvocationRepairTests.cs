using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CrossRailR008BInvocationRepairTests
{
    [Fact]
    public async Task R008b_produces_not_run_reviewed_template_with_candidate_hash_bound()
    {
        var output = TempRoot("cross-rail-r008b-template-test");
        var result = await new CrossRailR008BInvocationRepairWriter().WriteAsync(
            new CrossRailR008BOptions("cross-rail-r008b-template-test", output, R008Root()),
            CancellationToken.None);

        Assert.Equal("PASS", result.RepairReport["CROSS_RAIL_R008B_STATUS"]);
        Assert.Equal("REVIEWED_INVOCATION_TEMPLATE_READY_NOT_RUN", result.RepairReport["R008B_RESULT"]);
        Assert.Equal("NO", result.RepairReport["R009_RUNNABLE_INVOCATION_COMPLETE"]);
        Assert.Equal("YES", result.RepairReport["R009_OPERATOR_APPROVAL_REQUIRED"]);
        Assert.Equal("YES", result.RepairReport["R009_NOT_RUNNABLE_UNTIL_APPROVAL"]);
        Assert.True((bool)result.RepairReport["R009_NOT_RUNNABLE_UNTIL_PLACEHOLDERS_REPLACED"]);
        Assert.Equal("CROSS_RAIL_R009_EXPLICIT_OPERATOR_APPROVED_SANDBOX_EXECUTION", result.RepairReport["SAFE_NEXT_GATE"]);

        Assert.Equal("NOT_RUN", result.Template["templateStatus"]);
        Assert.Equal("REVIEW_ONLY", result.Template["reviewStatus"]);
        Assert.True((bool)result.Template["requiresOperatorApproval"]);
        Assert.True((bool)result.Template["requiresR009Gate"]);
        Assert.False((bool)result.Template["approvalPlaceholdersAreAcceptedForExecution"]);
        Assert.Equal("ExistingLmaxDemoProfile", result.Template["brokerEnvironmentSelector"]);
        Assert.Equal("YES", result.CandidateBinding["CANDIDATE_SET_HASH_BOUND"]);
    }

    [Fact]
    public async Task R008b_preserves_candidate_set_and_marks_approval_placeholders_non_runnable()
    {
        var output = TempRoot("cross-rail-r008b-candidate-test");
        await new CrossRailR008BInvocationRepairWriter().WriteAsync(
            new CrossRailR008BOptions("cross-rail-r008b-candidate-test", output, R008Root()),
            CancellationToken.None);

        using var candidate = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "10_validation", "cross_rail_r008b_candidate_binding_report.json")));
        Assert.Equal("YES", candidate.RootElement.GetProperty("CANDIDATE_SET_PRESERVED").GetString());
        Assert.Equal("YES", candidate.RootElement.GetProperty("CANDIDATE_SET_HASH_BOUND").GetString());
        Assert.Equal(3, candidate.RootElement.GetProperty("EXPECTED_CANDIDATE_COUNT").GetInt32());
        Assert.Equal(3, candidate.RootElement.GetProperty("OBSERVED_CANDIDATE_COUNT").GetInt32());
        Assert.Equal("PASS", candidate.RootElement.GetProperty("CANDIDATE_SET_EXECUTION_COMPATIBILITY").GetString());

        var rows = candidate.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(("AUDUSD", "SELL", 0.1m), Row(rows[0]));
        Assert.Equal(("EURUSD", "SELL", 0.1m), Row(rows[1]));
        Assert.Equal(("GBPUSD", "BUY", 0.1m), Row(rows[2]));

        using var safety = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "10_validation", "cross_rail_r008b_sandbox_safety_flags_matrix.json")));
        Assert.Equal("PASS", safety.RootElement.GetProperty("SAFETY_FLAG_MATRIX_STATUS").GetString());
        Assert.Equal("NO", safety.RootElement.GetProperty("R009_RUNNABLE_INVOCATION_COMPLETE").GetString());
        Assert.Equal("YES", safety.RootElement.GetProperty("R009_OPERATOR_APPROVAL_REQUIRED").GetString());

        var flags = safety.RootElement.GetProperty("flags").EnumerateArray().ToArray();
        AssertFlag(flags, "sandbox-only", "PRESENT");
        AssertFlag(flags, "no-production", "PRESENT");
        AssertFlag(flags, "no-live", "PRESENT");
        AssertFlag(flags, "bounded-lifecycle", "PRESENT");
        AssertFlag(flags, "max-orders", "PRESENT");
        AssertFlag(flags, "candidate-set-hash", "PRESENT");
        AssertFlag(flags, "operator-approval-phrase", "PLACEHOLDER");
        AssertFlag(flags, "no-Qubes-mutation", "PRESENT");
        AssertFlag(flags, "no-NettedUsdWeights-mutation", "PRESENT");
    }

    [Fact]
    public async Task R008b_no_execution_boundary_and_manifest_are_clean()
    {
        var output = TempRoot("cross-rail-r008b-boundary-test");
        await new CrossRailR008BInvocationRepairWriter().WriteAsync(
            new CrossRailR008BOptions("cross-rail-r008b-boundary-test", output, R008Root()),
            CancellationToken.None);

        using var boundary = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "10_validation", "cross_rail_r008b_no_execution_boundary_report.json")));
        Assert.Equal("PASS", boundary.RootElement.GetProperty("NO_EXECUTION_BOUNDARY_STATUS").GetString());
        Assert.Equal("NO", boundary.RootElement.GetProperty("FIX_SESSION_OPENED").GetString());
        Assert.Equal("NO", boundary.RootElement.GetProperty("LMAX_CALL_MADE").GetString());
        Assert.Equal(0, boundary.RootElement.GetProperty("ORDERS_SUBMITTED").GetInt32());
        Assert.Equal(0, boundary.RootElement.GetProperty("FILLS_CAPTURED").GetInt32());
        Assert.Equal("NO", boundary.RootElement.GetProperty("FLATTEN_ORDER_CREATED").GetString());
        Assert.Equal("NO", boundary.RootElement.GetProperty("ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED").GetString());
        Assert.Equal("NO", boundary.RootElement.GetProperty("PRODUCTION_LIVE_TOUCHED").GetString());

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/cross_rail_r008b_invocation_repair_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008b_reviewed_invocation_template.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008b_sandbox_safety_flags_matrix.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008b_candidate_binding_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008b_no_execution_boundary_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/cross_rail_r008b_status_summary.md", manifest, StringComparison.Ordinal);

        var manifestHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "manifest.json")))).ToLowerInvariant();
        Assert.Equal($"{manifestHash}  manifest.json", File.ReadAllText(Path.Combine(output, "manifest.sha256")).Trim());
        var hashes = File.ReadAllText(Path.Combine(output, "hashes.json"));
        Assert.Contains("manifest.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("hashes.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest.sha256", hashes, StringComparison.Ordinal);

        Assert.Empty(Directory.EnumerateFiles(output, "A.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "H.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "I.txt", SearchOption.AllDirectories));
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

    private static (string Symbol, string Side, decimal Quantity) Row(JsonElement row)
        => (row.GetProperty("symbol").GetString() ?? string.Empty, row.GetProperty("side").GetString() ?? string.Empty, row.GetProperty("quantity").GetDecimal());

    private static void AssertFlag(JsonElement[] flags, string name, string status)
    {
        var match = flags.Single(x => x.GetProperty("flagName").GetString() == name);
        Assert.Equal(status, match.GetProperty("status").GetString());
    }

    private static string R008Root()
        => Path.Combine(RepoRoot(), "artifacts", "readiness", "cross-rail-sandbox-handoff", "cross-rail-r008-controlled-sandbox-failure-diagnosis-001");

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-cross-rail-r008b-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "artifacts", "readiness", "cross-rail-sandbox-handoff")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
