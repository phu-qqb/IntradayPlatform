using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffTests
{
    [Fact]
    public void Valid_signed_for_planning_signoff_passes_but_remains_non_executable()
    {
        var result = Validate(Signoff());

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS, result.FinalDecision);
        Assert.False(result.Signoff.CanRunExternalSnapshot);
        Assert.False(result.Signoff.IsApprovedForExternalRun);
        Assert.False(result.Signoff.EligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Draft_signoff_can_be_incomplete_but_non_executable()
    {
        var result = Validate(Signoff() with
        {
            SignoffDecision = LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.Draft,
            SignedByOperatorId = "",
            ConfirmsExecutionPlanReviewed = false
        });

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS, result.FinalDecision);
    }

    [Fact]
    public void Missing_attestation_signedby_or_reason_fails_signed_for_planning()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { ConfirmsExecutionPlanReviewed = false }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { SignedByOperatorId = "" }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { Reason = "" }).FinalDecision);
    }

    [Fact]
    public void Execution_plan_or_phase6t_gate_not_pass_fails()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { SourceExecutionPlanDecision = "FAIL" }, ExecutionPlan() with { Decision = LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { SourcePhase6TGateDecision = "FAIL" }, phase6TGateDecision: "FAIL").FinalDecision);
    }

    [Theory]
    [InlineData("EURGBP", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string securityId)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { Symbol = symbol, PlanningSecurityId = securityId }).FinalDecision);

    [Theory]
    [InlineData(true, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false, false, false, false, true)]
    public void Executable_or_attempt_flags_fail(bool approved, bool eligible, bool canRun, bool external, bool snapshot, bool replay, bool order, bool shadow, bool mutation, bool scheduler)
    {
        var result = Validate(Signoff() with
        {
            IsApprovedForExternalRun = approved,
            EligibleForManualSnapshotAttempt = eligible,
            CanRunExternalSnapshot = canRun,
            ExternalConnectionAttempted = external,
            SnapshotAttempted = snapshot,
            ReplayAttempted = replay,
            OrderSubmissionAttempted = order,
            ShadowReplaySubmitAttempted = shadow,
            TradingMutationAttempted = mutation,
            SchedulerStarted = scheduler
        });

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    public void Sensitive_or_authorization_language_fails(string reason)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, Validate(Signoff() with { Reason = reason }).FinalDecision);

    [Fact]
    public void Review_returns_warning_with_no_signoffs_and_pass_with_valid_signed_signoff()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS_WITH_KNOWN_WARNINGS, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidator.Review([]).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidator.Review([Signoff()]).FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6u()
    {
        var repoRoot = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgramPath = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;
        var combined = apiProgram + Environment.NewLine + workerProgram;
        Assert.Contains("FakeLmaxGateway", apiProgram);
        Assert.DoesNotContain("RealLmaxGateway", combined);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", combined);
        Assert.DoesNotContain("SecurityListRequest", combined);
        Assert.DoesNotContain("PeriodicTimer", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff signoff,
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan? plan = null,
        string phase6TGateDecision = "PASS")
        => LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidator.Validate(signoff, plan ?? ExecutionPlan(), phase6TGateDecision);

    private static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff Signoff()
        => new("signoff-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "local-operator", "Operator", "Phase 6U planning signoff", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "plan.json", "PASS", "gate.json", "PASS", true, true, true, true, true, true, true, true, true, true, true, true, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.SignedForPlanning, false, false, false, false, false, false, false, false, false, false, true);

    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlan ExecutionPlan()
        => new("plan-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6T planning", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "attempt-gate.json", "PASS", "DO NOT RUN IN PHASE 6T. Future template only.", false, false, false, false, false, false, false, false, "FakeLmaxGateway", true, ["Wrong symbol or SecurityID", "Any order flag true", "Scheduler or polling detected", "Runtime shadow replay submit true", "Credential exposure", "Unknown failure classification", "Non-Demo environment", "Gateway registration change"], ["Stop process", "Clear shell variables", "Verify API health FakeLmaxGateway", "Run Phase 6S gate", "Inspect artifacts for noSensitiveContent"], ["Artifact validator", "Evidence preview mapping", "Optional manual replay in later phase", "No observation or mutation guard", "Operator review"], LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
