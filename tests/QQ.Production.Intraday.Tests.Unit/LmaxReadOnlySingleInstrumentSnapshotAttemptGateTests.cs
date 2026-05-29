using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlySingleInstrumentSnapshotAttemptGateTests
{
    [Fact]
    public void Valid_gbpusd_attempt_gate_passes_but_remains_non_executable()
    {
        var result = Validate(Gate());

        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, result.FinalDecision);
        Assert.False(result.Gate.CanRunExternalSnapshot);
        Assert.False(result.Gate.IsApprovedForExternalRun);
        Assert.False(result.Gate.EligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Missing_source_artifacts_fail()
    {
        var gate = Gate();

        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, null, Safety(), Preflight(), Approval(), DryRun()).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, Planning(), null, Preflight(), Approval(), DryRun()).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, Planning(), Safety(), null, Approval(), DryRun()).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, Planning(), Safety(), Preflight(), null, DryRun()).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, Planning(), Safety(), Preflight(), Approval(), null).FinalDecision);
    }

    [Theory]
    [InlineData("EURGBP", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Source_symbol_or_securityid_mismatch_fails(string symbol, string securityId)
        => Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { Symbol = symbol, PlanningSecurityId = securityId }).FinalDecision);

    [Fact]
    public void Non_pass_source_decisions_fail()
    {
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { DryRunDecision = "FAIL" }).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { ApprovalEnvelopeDecision = "Draft" }, approval: Approval() with { Decision = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.Draft }).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { PreflightDecision = "FAIL" }).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { SafetyGateDecision = "FAIL" }).FinalDecision);
    }

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
        var result = Validate(Gate() with
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

        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    public void Sensitive_or_authorization_language_fails(string reason)
        => Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, Validate(Gate() with { Reason = reason }).FinalDecision);

    [Fact]
    public void Review_returns_warning_with_no_gates_and_pass_with_valid_gate()
    {
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS_WITH_KNOWN_WARNINGS, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Review([]).FinalDecision);
        Assert.Equal(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Review([Gate()]).FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6s()
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

    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGateResult Validate(
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate gate,
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? approval = null)
        => LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator.Validate(gate, Planning(), Safety(), Preflight(), approval ?? Approval(), DryRun());

    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGate Gate()
        => new("gate-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6S gate", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, "planning.json", "safety.json", "preflight.json", "approval.json", "dryrun.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", "PASS", LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual execution phase", "Phase 6S is a gate only; external snapshot not authorized.");

    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport DryRun()
        => new("dryrun-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6R dry-run report", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "planning.json", "safety.json", "preflight.json", "approval.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual run phase", "Phase 6R is dry-run only; external snapshot not authorized.");

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope Approval()
        => new("approval-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "local-operator", "Phase 6Q planning", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "preflight.json", "PASS", true, true, true, true, true, true, true, true, false, false, false, true, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning);

    private static LmaxReadOnlyAdditionalInstrumentSafetyGateManifest Safety()
        => LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(Planning(), "planning.json");

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest Preflight()
        => LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(Planning(), Safety(), "planning.json", "safety.json", "local-operator", "Phase 6P design");

    private static LmaxReadOnlyInstrumentSecurityIdPlanningManifest Planning()
        => new("planning", DateTimeOffset.UtcNow, "Demo", "DemoLondon", [new("GBPUSD", "GBP/USD", "4002", "8", "OfficialLmaxDocument", "LMAX CSV", "record-GBPUSD", "record.json", LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning, false, "Demo", "DemoLondon", true)], false, false, false, false, false, false, false, false, false, false, false, true);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
