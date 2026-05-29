using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyPostGbpusdNextInstrumentDecisionTests
{
    [Fact]
    public void No_gbpusd_closure_returns_pending_market_hours_attempt()
    {
        var decision = Decision(null, null);
        var validation = LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision);

        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.PendingGbpusdMarketHoursAttempt, decision.Decision);
        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS_WITH_KNOWN_WARNINGS, validation.FinalDecision);
    }

    [Fact]
    public void Completed_with_book_pass_proceeds_to_eurgbp_planning()
    {
        var decision = Decision("CompletedWithBook", "PASS");
        var validation = LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision);

        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning, decision.Decision);
        Assert.Equal("EURGBP", decision.NextCandidateInstrument);
        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS, validation.FinalDecision);
    }

    [Fact]
    public void Completed_with_empty_book_warning_requires_gbpusd_retry_phase()
    {
        var decision = Decision("CompletedWithEmptyBook", "PASS_WITH_KNOWN_WARNINGS");
        var validation = LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision);

        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.RetryGbpusdAtLaterMarketHours, decision.Decision);
        Assert.Null(decision.NextCandidateInstrument);
        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS_WITH_KNOWN_WARNINGS, validation.FinalDecision);
    }

    [Theory]
    [InlineData("FailedSafe", "PASS_WITH_KNOWN_WARNINGS")]
    [InlineData("UnsafeFail", "FAIL")]
    public void Failed_safe_or_unsafe_closure_blocks_sequence(string classification, string closureDecision)
    {
        var decision = Decision(classification, closureDecision);
        var validation = LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision);

        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.BlockSequenceForDiagnostics, decision.Decision);
        Assert.Equal(LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS_WITH_KNOWN_WARNINGS, validation.FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Run_eligibility_flags_fail(bool canRun, bool approved, bool eligible)
    {
        var decision = Decision(null, null) with
        {
            CanRunExternalSnapshot = canRun,
            IsApprovedForExternalRun = approved,
            EligibleForManualSnapshotAttempt = eligible
        };

        Assert.Equal(
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision).FinalDecision);
    }

    [Fact]
    public void Batch_execution_allowed_fails()
    {
        var decision = Decision(null, null) with { BatchExecutionAllowed = true };

        Assert.Equal(
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision).FinalDecision);
    }

    [Fact]
    public void Executable_count_above_zero_fails()
    {
        var decision = Decision(null, null) with { ExecutableCount = 1 };

        Assert.Equal(
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Runtime_power_flags_fail(bool scheduler, bool replay, bool order, bool gateway, bool mutation)
    {
        var decision = Decision(null, null) with
        {
            SchedulerOrPolling = scheduler,
            RuntimeShadowReplaySubmit = replay,
            OrderSubmission = order,
            GatewayRegistration = gateway,
            TradingMutation = mutation
        };

        Assert.Equal(
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(decision).FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("ReplaySubmitAsync")]
    [InlineData("NewOrderSingle")]
    public void Sensitive_or_forbidden_runtime_text_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Validate(Decision(null, null), rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7d()
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

    private static LmaxReadOnlyPostGbpusdNextInstrumentDecision Decision(string? classification, string? closureDecision)
    {
        var outcome = LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.Decide(classification, closureDecision);
        return new(
            "phase7d-decision",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7D post-GBPUSD decision",
            "workflow-plan.json",
            classification is null && closureDecision is null ? null : "closure.json",
            classification is null && closureDecision is null ? null : "review.json",
            "GBPUSD",
            outcome == LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning ? "EURGBP" : null,
            1,
            classification,
            closureDecision,
            classification,
            outcome,
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator.RequiredNextPhaseFor(outcome),
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
            "FakeLmaxGateway",
            NoSensitiveContent: true);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
