using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CrossRailR008ControlledSandboxFailureDiagnosisTests
{
    [Fact]
    public async Task R008_diagnoses_blocked_presubmission_without_execution()
    {
        var output = TempRoot("cross-rail-r008-controlled-sandbox-failure-diagnosis-test");
        var result = await new CrossRailR008ControlledSandboxFailureDiagnosisWriter().WriteAsync(
            new CrossRailR008Options("cross-rail-r008-controlled-sandbox-failure-diagnosis-test", output, SourceRoot()),
            CancellationToken.None);

        Assert.Equal("PASS", result.FailureDiagnosis["CROSS_RAIL_R008_STATUS"]);
        Assert.Equal("CONTROLLED_BLOCKED_PRESUBMISSION_DIAGNOSED", result.FailureDiagnosis["R008_RESULT"]);
        Assert.Equal("PASS", result.FailureDiagnosis["PHASE_A_APPROVAL_BINDING_STATUS"]);
        Assert.Equal("NO", result.FailureDiagnosis["PHASE_B_SANDBOX_EXECUTION_ATTEMPTED"]);
        Assert.Equal("BlockedPreSubmission", result.FailureDiagnosis["PHASE_B_GATE_STATUS"]);
        Assert.Equal(0, result.FailureDiagnosis["ORDERS_SUBMITTED"]);
        Assert.Equal(0, result.FailureDiagnosis["FILLS_CAPTURED"]);
        Assert.Equal("FlattenNotRequired", result.FailureDiagnosis["FLATTEN_STATUS"]);
        Assert.Equal("NotAvailable", result.FailureDiagnosis["RESIDUAL_STATUS"]);
        Assert.Equal("NotAvailable", result.FailureDiagnosis["RECONCILIATION_STATUS"]);
        Assert.Equal("NO", result.FailureDiagnosis["PRODUCTION_LIVE_TOUCHED"]);
        Assert.Equal("NO", result.FailureDiagnosis["FIX_SESSION_OPENED"]);
        Assert.Equal("NO", result.FailureDiagnosis["LMAX_CALL_MADE"]);
        Assert.Equal("NO", result.FailureDiagnosis["QUBES_NETTING_NETTEDUSDWEIGHTS_TOUCHED"]);
        Assert.Equal("CROSS_RAIL_R008B_INVOCATION_REPAIR_ONLY", result.FailureDiagnosis["SAFE_NEXT_GATE"]);
        Assert.Equal("PASS", result.NoExecutionBoundary["NO_EXECUTION_BOUNDARY_STATUS"]);
    }

    [Fact]
    public async Task Invocation_review_marks_missing_runnable_elements_and_preserves_candidates()
    {
        var output = TempRoot("cross-rail-r008-invocation-review-test");
        var result = await new CrossRailR008ControlledSandboxFailureDiagnosisWriter().WriteAsync(
            new CrossRailR008Options("cross-rail-r008-invocation-review-test", output, SourceRoot()),
            CancellationToken.None);

        Assert.Equal("YES", result.CandidateSetIntegrity["CANDIDATE_SET_PRESERVED"]);
        Assert.Equal("PASS", result.CandidateSetIntegrity["CANDIDATE_SET_EXECUTION_COMPATIBILITY"]);
        Assert.Equal(3, result.CandidateSetIntegrity["orderCount"]);
        Assert.Equal("PASS", result.SafetyFlagsMatrix["SAFETY_FLAG_MATRIX_STATUS"]);

        var reviewJson = File.ReadAllText(Path.Combine(output, "10_validation", "cross_rail_r008_explicit_invocation_review.json"));
        using var review = JsonDocument.Parse(reviewJson);
        Assert.Equal("REVIEW_ONLY", review.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal("NOT_RUN", review.RootElement.GetProperty("templateStatus").GetString());
        Assert.True(review.RootElement.GetProperty("requiresOperatorApproval").GetBoolean());
        Assert.False(review.RootElement.GetProperty("runnableApprovedResultProduced").GetBoolean());

        var missing = review.RootElement.GetProperty("missingRunnableInvocationElements").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("operator approval phrase", missing);
        Assert.Contains("bounded lifecycle", missing);
        Assert.True(review.RootElement.GetProperty("placeholdersRejected").GetBoolean());
    }

    [Fact]
    public async Task Manifest_hashes_are_valid_and_forbidden_artifacts_absent()
    {
        var output = TempRoot("cross-rail-r008-manifest-test");
        await new CrossRailR008ControlledSandboxFailureDiagnosisWriter().WriteAsync(
            new CrossRailR008Options("cross-rail-r008-manifest-test", output, SourceRoot()),
            CancellationToken.None);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("10_validation/cross_rail_r008_failure_diagnosis_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008_explicit_invocation_review.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008_candidate_set_integrity_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008_safety_flags_matrix.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/cross_rail_r008_no_execution_boundary_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/cross_rail_r008_status_summary.md", manifest, StringComparison.Ordinal);

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

    private static string SourceRoot()
        => Path.Combine(RepoRoot(), "artifacts", "readiness", "cross-rail-sandbox-handoff");

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-cross-rail-r008-tests", Guid.NewGuid().ToString("N"), leaf);
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
