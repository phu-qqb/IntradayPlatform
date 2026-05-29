using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationTests
{
    [Fact]
    public void Valid_eurgbp_rehydration_after_proceed_decision_validates_pass()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(Rehydration());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS, validation.FinalDecision);
    }

    [Theory]
    [InlineData("PendingGbpusdMarketHoursAttempt")]
    [InlineData("BlockSequenceForDiagnostics")]
    public void Non_proceed_phase7d_decision_fails(string previousDecision)
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { PreviousDecision = previousDecision });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Next_candidate_not_eurgbp_fails()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { NextCandidateInstrument = "USDJPY" });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Rejected")]
    public void Missing_or_wrong_planning_decision_fails(string planningDecision)
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { PlanningDecision = planningDecision });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Wrong_eurgbp_securityid_fails()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { SecurityId = "4999" });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Run_eligibility_flags_fail(bool canRun, bool approved, bool eligible)
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with
            {
                CanRunExternalSnapshot = canRun,
                IsApprovedForExternalRun = approved,
                EligibleForManualSnapshotAttempt = eligible
            });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Batch_execution_allowed_fails()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { BatchExecutionAllowed = true });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Executable_count_above_zero_fails()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with { ExecutableCount = 1 });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false, true)]
    public void Attempt_or_runtime_flags_fail(
        bool external,
        bool snapshot,
        bool replay,
        bool order,
        bool shadow,
        bool scheduler,
        bool mutation)
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(
            Rehydration() with
            {
                ExternalConnectionAttempted = external,
                SnapshotAttempted = snapshot,
                ReplayAttempted = replay,
                OrderSubmissionAttempted = order,
                ShadowReplaySubmitAttempted = shadow,
                SchedulerStarted = scheduler,
                TradingMutationAttempted = mutation
            });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    [InlineData("production run is authorized")]
    public void Sensitive_or_authorization_language_fails(string rawText)
    {
        var validation = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator.Validate(Rehydration(), rawText);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7e2()
    {
        var repoRoot = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgramPath = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;
        var combined = apiProgram + Environment.NewLine + workerProgram;

        Assert.Contains("FakeLmaxGateway", apiProgram);
        Assert.DoesNotContain("RealLmaxGateway", combined);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", combined);
        Assert.DoesNotContain("PeriodicTimer", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("OrderStatusRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration Rehydration()
        => new(
            "phase7e2-eurgbp-readiness",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7E2 EURGBP readiness rehydration",
            "phase7d.json",
            "pipeline.json",
            "planning.json",
            "safety.json",
            "preflight.json",
            "EURGBP",
            "EUR/GBP",
            "4003",
            "8",
            "Demo",
            "DemoLondon",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            "GBPUSD",
            "PASS",
            "ProceedToEurgbpPlanning",
            "EURGBP",
            "PASS",
            "AcceptedForPlanning",
            "PASS",
            "PASS",
            "AcceptedForPlanning",
            "PASS",
            "PASS",
            "PASS",
            "SignedForPlanning",
            "PASS",
            "approval.json",
            "dry-run.json",
            "attempt-gate.json",
            "execution-plan.json",
            "signoff.json",
            "final-readiness.json",
            OneInstrumentAtATime: true,
            BatchExecutionAllowed: false,
            ExecutableCount: 0,
            IsApprovedForExternalRun: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            ExternalConnectionAttempted: false,
            SnapshotAttempted: false,
            ReplayAttempted: false,
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            SchedulerStarted: false,
            NoSensitiveContent: true,
            LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
