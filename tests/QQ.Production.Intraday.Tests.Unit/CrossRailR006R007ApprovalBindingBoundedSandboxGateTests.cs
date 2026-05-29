using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CrossRailR006R007ApprovalBindingBoundedSandboxGateTests
{
    private const string ExpectedRiskReviewId = "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001";
    private const string ExpectedOperatorApprovalId = "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001";

    [Fact]
    public void Approval_binding_preserves_r005_r006a_ids_and_candidate_set()
    {
        var root = ArtifactRoot();
        var r005 = Read(root, "cross-rail-r006r007-r005-reference.json");
        var r006a = Read(root, "cross-rail-r006r007-r006a-approval-reference.json");
        var binding = Read(root, "cross-rail-r006r007-approval-binding-validation.json");
        var candidates = Read(root, "cross-rail-r006r007-approved-candidate-set.json");

        Assert.Equal("BlockedMissingSandboxApprovals", r005.RootElement.GetProperty("R005Status").GetString());
        Assert.Equal("ExistingLmaxDemoProfile", r005.RootElement.GetProperty("R005PaperAccountProfile").GetString());
        Assert.Contains("MissingRiskApprovalId", r005.RootElement.GetProperty("R005MissingFields").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("MissingOperatorApprovalId", r005.RootElement.GetProperty("R005MissingFields").EnumerateArray().Select(x => x.GetString()));

        Assert.Equal(ExpectedRiskReviewId, r006a.RootElement.GetProperty("RiskReviewId").GetString());
        Assert.Equal(ExpectedOperatorApprovalId, r006a.RootElement.GetProperty("OperatorApprovalId").GetString());
        Assert.True(r006a.RootElement.GetProperty("ApprovalsAreSandboxOnly").GetBoolean());
        Assert.False(r006a.RootElement.GetProperty("ApprovalsAuthorizeProduction").GetBoolean());
        Assert.False(r006a.RootElement.GetProperty("ApprovalsAuthorizeExecutionNow").GetBoolean());

        Assert.Equal("InputsResolved", binding.RootElement.GetProperty("Status").GetString());
        Assert.True(binding.RootElement.GetProperty("RiskReviewIdMatchesR006A").GetBoolean());
        Assert.True(binding.RootElement.GetProperty("OperatorApprovalIdMatchesR006A").GetBoolean());
        Assert.False(binding.RootElement.GetProperty("RiskReviewIdIsPlaceholder").GetBoolean());
        Assert.False(binding.RootElement.GetProperty("OperatorApprovalIdIsPlaceholder").GetBoolean());

        Assert.Equal("CandidateSetApprovedForSandbox", candidates.RootElement.GetProperty("Status").GetString());
        AssertCandidateRows(candidates.RootElement.GetProperty("CandidateRows"));
        Assert.Equal("ExistingLmaxDemoProfile", candidates.RootElement.GetProperty("PaperAccountProfile").GetString());
        Assert.Equal(ExpectedRiskReviewId, candidates.RootElement.GetProperty("RiskReviewId").GetString());
        Assert.Equal(ExpectedOperatorApprovalId, candidates.RootElement.GetProperty("OperatorApprovalId").GetString());
        Assert.True(candidates.RootElement.GetProperty("SandboxOnly").GetBoolean());
        Assert.True(candidates.RootElement.GetProperty("NoProduction").GetBoolean());
    }

    [Fact]
    public void Bounded_sandbox_execution_is_blocked_without_runnable_safety_invocation()
    {
        var root = ArtifactRoot();
        var pre = Read(root, "cross-rail-r006r007-pre-submission-safety-check.json");
        var command = Read(root, "cross-rail-r006r007-lmax-demo-sandbox-command.json");
        var orders = Read(root, "cross-rail-r006r007-sandbox-order-submission-result.json");
        var fills = Read(root, "cross-rail-r006r007-sandbox-fill-report.json");
        var flatten = Read(root, "cross-rail-r006r007-sandbox-flatten-result.json");
        var residual = Read(root, "cross-rail-r006r007-sandbox-residual-check.json");

        Assert.Equal("Failed", pre.RootElement.GetProperty("Status").GetString());
        Assert.False(pre.RootElement.GetProperty("AllPreconditionsSatisfied").GetBoolean());
        Assert.True(pre.RootElement.GetProperty("MissingExecutionSafetyInvocation").GetBoolean());
        Assert.False(pre.RootElement.GetProperty("PhaseBExecutionAllowed").GetBoolean());
        Assert.False(pre.RootElement.GetProperty("FixDemoSessionAllowedNow").GetBoolean());
        Assert.False(pre.RootElement.GetProperty("LmaxDemoCallAllowedNow").GetBoolean());
        Assert.True(pre.RootElement.GetProperty("CredentialValuesRedacted").GetBoolean());

        var envVars = pre.RootElement.GetProperty("LmaxDemoEnvVars").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToArray();
        Assert.Equal(new[] { "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID" }, envVars);
        Assert.DoesNotContain("\"CredentialValue\"", pre.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"PasswordValue\"", pre.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"SecretValue\"", pre.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal("NotExecuted", command.RootElement.GetProperty("Status").GetString());
        Assert.False(command.RootElement.GetProperty("LmaxDemoCallMade").GetBoolean());
        Assert.False(command.RootElement.GetProperty("FixDemoSessionOpened").GetBoolean());
        Assert.False(command.RootElement.GetProperty("ProductionLiveAllowed").GetBoolean());

        Assert.Equal("NotExecuted", orders.RootElement.GetProperty("Status").GetString());
        Assert.Equal(0, orders.RootElement.GetProperty("SubmittedOrderCount").GetInt32());
        Assert.Empty(orders.RootElement.GetProperty("Orders").EnumerateArray());
        Assert.Equal("NoFills", fills.RootElement.GetProperty("Status").GetString());
        Assert.Equal(0, fills.RootElement.GetProperty("FillCount").GetInt32());
        Assert.Equal("FlattenNotRequired", flatten.RootElement.GetProperty("Status").GetString());
        Assert.Equal("NotAvailable", residual.RootElement.GetProperty("Status").GetString());
    }

    [Fact]
    public void Idempotency_no_production_and_validator_artifacts_are_clean()
    {
        var root = ArtifactRoot();
        var idempotency = Read(root, "cross-rail-r006r007-final-idempotency-plan.json");
        var safety = Read(root, "cross-rail-r006r007-sandbox-safety-boundary.json");
        var audit = Read(root, "cross-rail-r006r007-no-production-safety-audit.json");
        var validator = Path.Combine(RepoRoot(), "scripts", "check-cross-rail-r006r007-approval-binding-bounded-sandbox-order-gate.ps1");

        Assert.True(File.Exists(validator));
        Assert.Equal("IdempotencyPlanDefined", idempotency.RootElement.GetProperty("Status").GetString());
        var keys = idempotency.RootElement.GetProperty("PerCandidateIdempotencyKeyPreview")
            .EnumerateArray()
            .Select(x => x.GetProperty("IdempotencyKeyPreview").GetString())
            .ToArray();
        Assert.Equal(3, keys.Length);
        Assert.Equal(3, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.True(idempotency.RootElement.GetProperty("DuplicateSubmissionPrevented").GetBoolean());

        Assert.Equal("SafetyBoundaryPreserved", safety.RootElement.GetProperty("Status").GetString());
        Assert.True(safety.RootElement.GetProperty("R009Selected").GetBoolean());
        Assert.True(safety.RootElement.GetProperty("SandboxOnly").GetBoolean());
        Assert.True(safety.RootElement.GetProperty("LmaxSandboxOnly").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("ProductionLiveAllowed").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("ProductionCredentialUse").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("ProductionLedgerMutation").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("DirectCrossExecutionAllowed").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("QubesExecutableRun").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("NettingRun").GetBoolean());
        Assert.False(safety.RootElement.GetProperty("NettedUsdWeightsProduced").GetBoolean());

        Assert.Equal("Passed", audit.RootElement.GetProperty("Status").GetString());
        foreach (var name in new[]
        {
            "NoProductionLmaxCall", "NoProductionFixSession", "NoProductionOrders", "NoProductionRoutes",
            "NoProductionFills", "NoProductionSchedules", "NoProductionBrokerSubmission",
            "NoProductionLiveTradingStateMutation", "NoProductionCredentialUse", "CredentialValuesRedacted",
            "NoQubesExecutableRun", "NoNettingRun", "NoNettedUsdWeightsProduced"
        })
        {
            Assert.True(audit.RootElement.GetProperty(name).GetBoolean(), name);
        }

        var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(root, "cross-rail-r006r007-*", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
        Assert.DoesNotContain("\"CredentialValue\"", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"PasswordValue\"", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"SecretValue\"", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fix-order.london-demo.lmax.com", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=F", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=G", allText, StringComparison.Ordinal);
    }

    private static void AssertCandidateRows(JsonElement rows)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = "SELL",
            ["EURUSD"] = "SELL",
            ["GBPUSD"] = "BUY"
        };
        var supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"
        };

        var actual = rows.EnumerateArray().ToArray();
        Assert.Equal(3, actual.Length);
        foreach (var row in actual)
        {
            var symbol = row.GetProperty("Symbol").GetString() ?? string.Empty;
            Assert.True(expected.ContainsKey(symbol), $"Unexpected symbol {symbol}");
            Assert.Contains(symbol, supported);
            Assert.Equal(expected[symbol], row.GetProperty("Side").GetString());
            Assert.Equal(0.1m, row.GetProperty("Quantity").GetDecimal());
        }
    }

    private static JsonDocument Read(string root, string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(root, fileName)));

    private static string ArtifactRoot()
        => Path.Combine(RepoRoot(), "artifacts", "readiness", "cross-rail-sandbox-handoff");

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
