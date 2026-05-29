using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistTests
{
    [Fact]
    public void Valid_eurgbp_checklist_validates_pass()
    {
        var result = Validate(Checklist());

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS, result.FinalDecision);
        Assert.False(result.Checklist.ExternalRunAuthorized);
        Assert.False(result.Checklist.CanRunExternalSnapshot);
        Assert.False(result.Checklist.EligibleForManualSnapshotAttempt);
        Assert.False(result.Checklist.IsApprovedForExternalRun);
        Assert.True(result.Checklist.OneInstrumentAtATime);
        Assert.False(result.Checklist.BatchExecutionAllowed);
    }

    [Theory]
    [InlineData("GBPUSD", "EUR/GBP", "4003")]
    [InlineData("EURGBP", "EUR/GBP", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string slashSymbol, string securityId)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { Symbol = symbol, SlashSymbol = slashSymbol, PlanningSecurityId = securityId }).FinalDecision);

    [Fact]
    public void Readiness_not_pass_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { EurgbpReadinessDecision = "FAIL" }, Readiness() with { FinalDecision = LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL }).FinalDecision);

    [Theory]
    [InlineData("PendingGbpusdMarketHoursAttempt", "PASS")]
    [InlineData("ProceedToEurgbpPlanning", "FAIL")]
    public void Previous_decision_or_gbpusd_closure_not_safe_fails(string previousDecision, string previousClosure)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { PreviousDecision = previousDecision, PreviousInstrumentClosureDecision = previousClosure }).FinalDecision);

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Run_authorization_flags_fail(bool external, bool canRun, bool eligible, bool approved)
    {
        var result = Validate(Checklist() with
        {
            ExternalRunAuthorized = external,
            CanRunExternalSnapshot = canRun,
            EligibleForManualSnapshotAttempt = eligible,
            IsApprovedForExternalRun = approved
        });

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Runtime_or_mutation_flags_fail(bool scheduler, bool shadow, bool order, bool mutation)
    {
        var result = Validate(Checklist() with
        {
            SchedulerOrPolling = scheduler,
            RuntimeShadowReplaySubmit = shadow,
            OrderSubmission = order,
            TradingMutation = mutation
        });

        Assert.Equal(LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL, result.FinalDecision);
    }

    [Fact]
    public void Batch_execution_allowed_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { BatchExecutionAllowed = true }).FinalDecision);

    [Fact]
    public void One_instrument_at_a_time_false_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { OneInstrumentAtATime = false }).FinalDecision);

    [Fact]
    public void Command_template_without_phase7f2_warning_fails()
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist() with { FutureCommandTemplate = "powershell -File future-eurgbp-wrapper.ps1 -Instrument EURGBP -SecurityId 4003" }).FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    [InlineData("production run is authorized")]
    public void Sensitive_or_authorization_language_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL,
            Validate(Checklist(), rawText: rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7f2()
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

    private static LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistValidation Validate(
        LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist checklist,
        LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration? readiness = null,
        string rawText = "")
        => LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistValidator.Validate(checklist, readiness ?? Readiness(), rawText);

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
            "DO NOT RUN IN PHASE 7F2. Future template only: powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 -Instrument EURGBP -SlashSymbol \"EUR/GBP\" -LmaxInstrumentId 4003 -RequestMode SnapshotPlusUpdates -SymbolEncodingMode SecurityIdOnly -MarketDepth 1 -AllowExternalConnections -ConfirmDemoReadOnly -Reason \"future explicit operator reason\"",
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
            AbortCriteria: ["Wrong symbol or SecurityID", "Any order flag true", "Scheduler or polling detected", "Runtime shadow replay submit true", "Credential exposure", "Unknown failure classification", "Non-Demo environment", "Gateway registration change", "Mutation guard change", "Batch or multi-instrument attempt"],
            RollbackSteps: ["Stop process", "Clear shell variables", "Verify API health FakeLmaxGateway", "Run Phase 7E2 gate", "Inspect artifacts for noSensitiveContent", "No DB rollback expected"],
            PostRunValidationSteps: ["Artifact review", "Evidence preview mapping", "Optional manual local replay", "Closure manifest", "Next-instrument decision", "Operator review"],
            LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS);

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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
