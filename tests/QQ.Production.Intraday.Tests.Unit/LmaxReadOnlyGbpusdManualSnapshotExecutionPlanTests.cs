using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdManualSnapshotExecutionPlanTests
{
    [Fact]
    public void Valid_gbpusd_execution_plan_validates_pass()
    {
        var result = Validate(Plan());

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS, result.FinalDecision);
        Assert.False(result.Plan.ExternalRunAuthorized);
        Assert.False(result.Plan.CanRunExternalSnapshot);
        Assert.False(result.Plan.EligibleForManualSnapshotAttempt);
        Assert.False(result.Plan.IsApprovedForExternalRun);
    }

    [Theory]
    [InlineData("EURGBP", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string securityId)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, Validate(Plan() with { Symbol = symbol, PlanningSecurityId = securityId }).FinalDecision);

    [Fact]
    public void Attempt_gate_missing_or_not_pass_fails()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotExecutionPlanValidator.Validate(Plan(), null).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, Validate(Plan() with { AttemptGateDecision = "FAIL" }, AttemptGate() with { GateDecision = LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL }).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false, false, true)]
    public void Run_or_mutation_flags_fail(bool external, bool canRun, bool eligible, bool approved, bool scheduler, bool shadow, bool order, bool mutation)
    {
        var result = Validate(Plan() with
        {
            ExternalRunAuthorized = external,
            CanRunExternalSnapshot = canRun,
            EligibleForManualSnapshotAttempt = eligible,
            IsApprovedForExternalRun = approved,
            SchedulerOrPolling = scheduler,
            RuntimeShadowReplaySubmit = shadow,
            OrderSubmission = order,
            TradingMutation = mutation
        });

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, result.FinalDecision);
    }

    [Fact]
    public void Command_template_must_be_marked_do_not_run_in_phase6t()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, Validate(Plan() with { FutureCommandTemplate = "powershell run future command" }).FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    public void Sensitive_or_authorization_language_fails(string reason)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, Validate(Plan() with { Reason = reason }).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6t()
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

    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlanResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan plan,
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate? gate = null)
        => LmaxReadOnlyGbpusdManualSnapshotExecutionPlanValidator.Validate(plan, gate ?? AttemptGate());

    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlan Plan()
        => new(
            "plan-GBPUSD",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 6T planning",
            "GBPUSD",
            "GBP/USD",
            "4002",
            "8",
            "Demo",
            "DemoLondon",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            "attempt-gate.json",
            "PASS",
            "DO NOT RUN IN PHASE 6T. Future template only: powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -Symbol GBPUSD -SecurityId 4002 -SecurityIdSource 8",
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            "FakeLmaxGateway",
            true,
            ["Wrong symbol or SecurityID", "Any order flag true", "Scheduler or polling detected", "Runtime shadow replay submit true", "Credential exposure", "Unknown failure classification", "Non-Demo environment", "Gateway registration change"],
            ["Stop process", "Clear shell variables", "Verify API health FakeLmaxGateway", "Run Phase 6S gate", "Inspect artifacts for noSensitiveContent"],
            ["Artifact validator", "Evidence preview mapping", "Optional manual replay in later phase", "No observation or mutation guard", "Operator review"],
            LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS);

    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGate AttemptGate()
        => new("gate-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6S gate", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "planning.json", "safety.json", "preflight.json", "approval.json", "dryrun.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", "PASS", LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual execution phase", "Phase 6S is a gate only; external snapshot not authorized.");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
