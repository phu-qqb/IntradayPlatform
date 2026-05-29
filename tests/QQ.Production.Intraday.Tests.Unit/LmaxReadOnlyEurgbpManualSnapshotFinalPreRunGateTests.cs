using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateTests
{
    [Fact]
    public void Valid_eurgbp_final_pre_run_gate_validates_pass()
    {
        var result = Validate(Gate());

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.PASS, result.FinalDecision);
        Assert.False(result.Gate.CanRunExternalSnapshot);
        Assert.False(result.Gate.IsApprovedForExternalRun);
        Assert.False(result.Gate.EligibleForManualSnapshotAttempt);
        Assert.True(result.Gate.OneInstrumentAtATime);
        Assert.False(result.Gate.BatchExecutionAllowed);
    }

    [Theory]
    [InlineData("GBPUSD", "EUR/GBP", "4003", "8")]
    [InlineData("EURGBP", "EUR/GBP", "4999", "8")]
    [InlineData("EURGBP", "EUR/GBP", "4003", "4")]
    public void Wrong_identity_fails(string symbol, string slashSymbol, string securityId, string source)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate() with { Symbol = symbol, SlashSymbol = slashSymbol, PlanningSecurityId = securityId, SecurityIdSource = source }).FinalDecision);

    [Theory]
    [InlineData("PendingGbpusdMarketHoursAttempt", "PASS", "PASS")]
    [InlineData("ProceedToEurgbpPlanning", "FAIL", "PASS")]
    [InlineData("ProceedToEurgbpPlanning", "PASS", "FAIL")]
    public void Source_decision_chain_must_be_safe(string previousDecision, string closureDecision, string readinessDecision)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate() with
            {
                PreviousDecision = previousDecision,
                PreviousInstrumentClosureDecision = closureDecision,
                SourceEurgbpReadinessDecision = readinessDecision
            }).FinalDecision);

    [Fact]
    public void Checklist_not_pass_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate() with { SourceExecutionChecklistDecision = "FAIL" }, checklist: Checklist() with { Decision = LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL }).FinalDecision);

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Run_authorization_flags_fail(bool external, bool canRun, bool eligible, bool approved)
    {
        var result = Validate(Gate() with
        {
            ExternalRunAuthorized = external,
            CanRunExternalSnapshot = canRun,
            EligibleForManualSnapshotAttempt = eligible,
            IsApprovedForExternalRun = approved
        });

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Runtime_power_flags_fail(bool scheduler, bool shadow, bool order, bool mutation, bool gateway)
    {
        var result = Validate(Gate() with
        {
            SchedulerOrPolling = scheduler,
            RuntimeShadowReplaySubmit = shadow,
            OrderSubmission = order,
            TradingMutation = mutation,
            GatewayRegistration = gateway
        });

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL, result.FinalDecision);
    }

    [Fact]
    public void Batch_execution_allowed_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate() with { BatchExecutionAllowed = true }).FinalDecision);

    [Fact]
    public void One_instrument_at_a_time_false_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate() with { OneInstrumentAtATime = false }).FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    [InlineData("production run is authorized")]
    public void Sensitive_or_authorization_language_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL,
            Validate(Gate(), rawText: rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7g2()
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

    private static LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateValidation Validate(
        LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate gate,
        LmaxReadOnlyPostGbpusdNextInstrumentDecision? phase7D = null,
        LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration? readiness = null,
        LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist? checklist = null,
        string rawText = "")
        => LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateValidator.Validate(
            gate,
            phase7D ?? Phase7DDecision(),
            readiness ?? Readiness(),
            checklist ?? Checklist(),
            rawText);

    private static LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate Gate()
        => new(
            "phase7g2-eurgbp-final-prerun",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7G2 EURGBP final pre-run gate",
            "EURGBP",
            "EUR/GBP",
            "4003",
            "8",
            "Demo",
            "DemoLondon",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            "phase7d.json",
            "eurgbp-readiness.json",
            "eurgbp-checklist.json",
            "PASS",
            "PASS",
            "GBPUSD",
            "PASS",
            "ProceedToEurgbpPlanning",
            OneInstrumentAtATime: true,
            BatchExecutionAllowed: false,
            ExternalRunAuthorized: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            IsApprovedForExternalRun: false,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            TradingMutation: false,
            GatewayRegistration: false,
            ApiWorkerGatewayMode: "FakeLmaxGateway",
            NoSensitiveContent: true,
            LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.PASS);

    private static LmaxReadOnlyPostGbpusdNextInstrumentDecision Phase7DDecision()
        => new(
            "phase7d-decision",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7D post-GBPUSD decision",
            "workflow-plan.json",
            "closure.json",
            "review.json",
            "GBPUSD",
            "EURGBP",
            1,
            "Completed",
            "PASS",
            "CompletedWithBook",
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning,
            "Phase 7E - EURGBP Manual Snapshot Readiness Refresh / No External Run.",
            CanRunExternalSnapshot: false,
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false,
            BatchExecutionAllowed: false,
            ExecutableCount: 0,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            ApiWorkerGatewayMode: "FakeLmaxGateway",
            NoSensitiveContent: true);

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration Readiness()
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

    private static LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist Checklist()
        => new(
            "checklist-EURGBP",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7F2 EURGBP checklist",
            "EURGBP",
            "EUR/GBP",
            "4003",
            "8",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            "eurgbp-readiness.json",
            "PASS",
            "GBPUSD",
            "PASS",
            "ProceedToEurgbpPlanning",
            "DO NOT RUN IN PHASE 7F2. Future template only: powershell -File .\\scripts\\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -Instrument EURGBP -SlashSymbol \"EUR/GBP\" -LmaxInstrumentId 4003 -RequestMode SnapshotPlusUpdates -SymbolEncodingMode SecurityIdOnly -MarketDepth 1",
            ExternalRunAuthorized: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            IsApprovedForExternalRun: false,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            TradingMutation: false,
            BatchExecutionAllowed: false,
            OneInstrumentAtATime: true,
            ApiWorkerGatewayMode: "FakeLmaxGateway",
            NoSensitiveContent: true,
            AbortCriteria: ["Wrong symbol", "Any order flag true", "Scheduler", "Runtime shadow replay", "Credential exposure", "Unknown failure", "Non-Demo", "Gateway registration", "Mutation guard", "Batch"],
            RollbackSteps: ["Stop process", "Clear shell variables", "Verify FakeLmaxGateway", "Run Phase 7E2 gate", "Inspect artifacts"],
            PostRunValidationSteps: ["Artifact review", "Evidence preview", "Manual replay if appropriate", "Closure manifest", "Next-instrument decision"],
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
