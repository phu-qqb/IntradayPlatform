using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyControlledManualWorkflowPlanTests
{
    [Fact]
    public void Valid_plan_with_four_instruments_validates_pass()
        => Assert.Equal(
            LmaxReadOnlyControlledManualWorkflowPlanDecision.PASS,
            LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(Plan()).FinalDecision);

    [Theory]
    [InlineData(1)]
    public void Executable_count_above_zero_fails(int executableCount)
        => Assert.Equal(
            LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL,
            LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(Plan() with { ExecutableCount = executableCount }).FinalDecision);

    [Fact]
    public void Batch_execution_allowed_fails()
        => Assert.Equal(
            LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL,
            LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(Plan() with { BatchExecutionAllowed = true }).FinalDecision);

    [Theory]
    [InlineData(false, 1, true, true, true)]
    [InlineData(true, 2, true, true, true)]
    [InlineData(true, 1, false, true, true)]
    [InlineData(true, 1, true, false, true)]
    [InlineData(true, 1, true, true, false)]
    public void Manual_one_instrument_rules_are_required(bool oneAtATime, int maxAttempts, bool retryRequiresNewPhase, bool marketHoursOnly, bool manualOnly)
    {
        var plan = Plan(i => i.Symbol == "GBPUSD"
            ? i with
            {
                OneInstrumentAtATime = oneAtATime,
                MaxAttemptsPerInstrument = maxAttempts,
                RetryRequiresNewPhase = retryRequiresNewPhase,
                MarketHoursOnly = marketHoursOnly,
                ManualOperatorCommandOnly = manualOnly
            }
            : i);

        Assert.Equal(LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL, LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(plan).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Run_eligibility_flags_fail(bool canRun, bool approved, bool eligible)
    {
        var plan = Plan(i => i.Symbol == "EURGBP"
            ? i with { CanRunExternalSnapshot = canRun, IsApprovedForExternalRun = approved, EligibleForManualSnapshotAttempt = eligible }
            : i);

        Assert.Equal(LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL, LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(plan).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Runtime_power_flags_fail(bool scheduler, bool runtimeReplay, bool order, bool gateway, bool mutation)
    {
        var plan = Plan(i => i.Symbol == "USDJPY"
            ? i with
            {
                NoSchedulerOrPolling = !scheduler,
                NoRuntimeShadowReplaySubmit = !runtimeReplay,
                NoOrderSubmission = !order,
                NoGatewayRegistration = !gateway,
                NoTradingMutation = !mutation
            }
            : i);

        Assert.Equal(LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL, LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(plan).FinalDecision);
    }

    [Fact]
    public void Missing_gbpusd_first_sequence_fails()
    {
        var plan = Plan(i => i.Symbol == "GBPUSD" ? i with { ProposedSequenceOrder = 2 } : i.Symbol == "EURGBP" ? i with { ProposedSequenceOrder = 1 } : i);

        Assert.Equal(LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL, LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(plan).FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    [InlineData("ReplaySubmitAsync")]
    [InlineData("PeriodicTimer")]
    public void Sensitive_or_forbidden_runtime_text_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL,
            LmaxReadOnlyControlledManualWorkflowPlanValidator.Validate(Plan(), rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7b()
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

    private static LmaxReadOnlyControlledManualMultiInstrumentWorkflowPlan Plan(Func<LmaxReadOnlyControlledManualInstrumentPlan, LmaxReadOnlyControlledManualInstrumentPlan>? mutate = null)
    {
        var instruments = new[]
        {
            Instrument("GBPUSD", "GBP/USD", "4002", 1),
            Instrument("EURGBP", "EUR/GBP", "4003", 2),
            Instrument("USDJPY", "USD/JPY", "4004", 3),
            Instrument("AUDUSD", "AUD/USD", "4007", 4)
        }.Select(x => mutate?.Invoke(x) ?? x).ToArray();

        return new(
            "phase7b-plan",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7B controlled manual workflow plan",
            "docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md",
            "pipeline.json",
            "status.json",
            instruments,
            InstrumentCount: instruments.Length,
            SelectedCount: instruments.Length,
            ExecutableCount: 0,
            BatchExecutionAllowed: false,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            "FakeLmaxGateway",
            NoSensitiveContent: true,
            LmaxReadOnlyControlledManualWorkflowPlanDecision.PASS);
    }

    private static LmaxReadOnlyControlledManualInstrumentPlan Instrument(string symbol, string slashSymbol, string securityId, int sequence)
        => new(
            symbol,
            slashSymbol,
            securityId,
            "8",
            "PASS",
            SelectedForFutureManualConsideration: true,
            sequence,
            OneInstrumentAtATime: true,
            MaxAttemptsPerInstrument: 1,
            RetryRequiresNewPhase: true,
            MarketHoursOnly: true,
            ManualOperatorCommandOnly: true,
            NoSchedulerOrPolling: true,
            NoRuntimeShadowReplaySubmit: true,
            NoOrderSubmission: true,
            NoTradingMutation: true,
            NoGatewayRegistration: true,
            CanRunExternalSnapshot: false,
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
