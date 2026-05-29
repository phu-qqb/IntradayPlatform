using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdManualSnapshotFinalReadinessTests
{
    [Fact]
    public void Valid_gbpusd_final_readiness_passes_but_remains_non_executable()
    {
        var result = Validate(Readiness());

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS, result.FinalDecision);
        Assert.False(result.Readiness.CanRunExternalSnapshot);
        Assert.False(result.Readiness.IsApprovedForExternalRun);
        Assert.False(result.Readiness.EligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Missing_source_artifacts_fail()
    {
        var r = Readiness();
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, null, Safety(), Preflight(), Approval(), DryRun(), AttemptGate(), ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), null, Preflight(), Approval(), DryRun(), AttemptGate(), ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), null, Approval(), DryRun(), AttemptGate(), ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), Preflight(), null, DryRun(), AttemptGate(), ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), Preflight(), Approval(), null, AttemptGate(), ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), Preflight(), Approval(), DryRun(), null, ExecutionPlan(), Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), Preflight(), Approval(), DryRun(), AttemptGate(), null, Signoff(), "PASS", "PASS").FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(r, Planning(), Safety(), Preflight(), Approval(), DryRun(), AttemptGate(), ExecutionPlan(), null, "PASS", "PASS").FinalDecision);
    }

    [Theory]
    [InlineData("EURGBP", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Source_symbol_or_securityid_mismatch_fails(string symbol, string securityId)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { Symbol = symbol, PlanningSecurityId = securityId }).FinalDecision);

    [Fact]
    public void Non_expected_source_decisions_fail()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { SafetyGateDecision = "FAIL" }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { PreflightDecision = "FAIL" }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { ApprovalEnvelopeDecision = "Draft" }, approval: Approval() with { Decision = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.Draft }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { DryRunDecision = "FAIL" }, dryRun: DryRun() with { DryRunDecision = LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { AttemptGateDecision = "FAIL" }, attemptGate: AttemptGate() with { GateDecision = LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { ExecutionPlanDecision = "FAIL" }, executionPlan: ExecutionPlan() with { Decision = LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL }).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { OperatorSignoffDecision = "Draft" }, signoff: Signoff() with { SignoffDecision = LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.Draft }).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, true, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false, false, false, false, false, true)]
    public void Executable_or_attempt_flags_fail(bool approved, bool eligible, bool canRun, bool external, bool snapshot, bool replay, bool order, bool shadow, bool mutation, bool scheduler, bool runtimeShadow)
    {
        var result = Validate(Readiness() with
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
            SchedulerStarted = scheduler,
            RuntimeShadowReplaySubmit = runtimeShadow
        });

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    public void Sensitive_or_authorization_language_fails(string reason)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, Validate(Readiness() with { Reason = reason }).FinalDecision);

    [Fact]
    public void Review_returns_warning_with_no_readiness_and_pass_with_valid_readiness()
    {
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS_WITH_KNOWN_WARNINGS, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Review([]).FinalDecision);
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Review([Readiness()]).FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6v()
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

    private static LmaxReadOnlyGbpusdManualSnapshotFinalReadinessResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotFinalReadiness readiness,
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? approval = null,
        LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport? dryRun = null,
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate? attemptGate = null,
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan? executionPlan = null,
        LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff? signoff = null)
        => LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator.Validate(readiness, Planning(), Safety(), Preflight(), approval ?? Approval(), dryRun ?? DryRun(), attemptGate ?? AttemptGate(), executionPlan ?? ExecutionPlan(), signoff ?? Signoff(), "PASS", "PASS");

    private static LmaxReadOnlyGbpusdManualSnapshotFinalReadiness Readiness()
        => new("readiness-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6V final readiness", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "planning.json", "safety.json", "preflight.json", "approval.json", "dryrun.json", "attempt.json", "plan.json", "signoff.json", "6t.json", "6u.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", "PASS", "PASS", "PASS", "SignedForPlanning", LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS, false, false, false, false, false, false, false, false, false, false, false, "FakeLmaxGateway", true, "Phase 6W operator-approved manual GBPUSD snapshot attempt", "Phase 6V is final readiness only; external snapshot not authorized.");

    private static LmaxReadOnlyInstrumentSecurityIdPlanningManifest Planning()
        => new("planning", DateTimeOffset.UtcNow, "Demo", "DemoLondon", [new("GBPUSD", "GBP/USD", "4002", "8", "OfficialLmaxDocument", "LMAX CSV", "record-GBPUSD", "record.json", LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning, false, "Demo", "DemoLondon", true)], false, false, false, false, false, false, false, false, false, false, false, true);
    private static LmaxReadOnlyAdditionalInstrumentSafetyGateManifest Safety() => LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(Planning(), "planning.json");
    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest Preflight() => LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(Planning(), Safety(), "planning.json", "safety.json", "local-operator", "Phase 6P design");
    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope Approval() => new("approval-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "local-operator", "Phase 6Q planning", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "preflight.json", "PASS", true, true, true, true, true, true, true, true, false, false, false, true, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning);
    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport DryRun() => new("dryrun-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6R dry-run report", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "planning.json", "safety.json", "preflight.json", "approval.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual run phase", "Phase 6R is dry-run only; external snapshot not authorized.");
    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGate AttemptGate() => new("gate-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6S gate", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "planning.json", "safety.json", "preflight.json", "approval.json", "dryrun.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", "PASS", LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual execution phase", "Phase 6S is a gate only; external snapshot not authorized.");
    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlan ExecutionPlan() => new("plan-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6T planning", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "attempt-gate.json", "PASS", "DO NOT RUN IN PHASE 6T. Future template only.", false, false, false, false, false, false, false, false, "FakeLmaxGateway", true, ["Wrong symbol or SecurityID", "Any order flag true", "Scheduler or polling detected", "Runtime shadow replay submit true", "Credential exposure", "Unknown failure classification", "Non-Demo environment", "Gateway registration change"], ["Stop process", "Clear shell variables", "Verify API health FakeLmaxGateway", "Run Phase 6S gate", "Inspect artifacts for noSensitiveContent"], ["Artifact validator", "Evidence preview mapping", "Optional manual replay in later phase", "No observation or mutation guard", "Operator review"], LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS);
    private static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff Signoff() => new("signoff-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "local-operator", "Operator", "Phase 6U planning signoff", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "plan.json", "PASS", "gate.json", "PASS", true, true, true, true, true, true, true, true, true, true, true, true, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.SignedForPlanning, false, false, false, false, false, false, false, false, false, false, true);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
