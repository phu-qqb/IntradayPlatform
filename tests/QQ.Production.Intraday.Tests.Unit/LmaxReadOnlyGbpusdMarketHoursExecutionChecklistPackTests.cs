using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackTests
{
    [Fact]
    public void Valid_checklist_pack_validates_pass()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.PASS,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack()).FinalDecision);

    [Fact]
    public void Missing_kill_switch_fails()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack() with { DuringRunMonitoring = ["one attempt only", "no retry"] }).FinalDecision);

    [Fact]
    public void Missing_post_run_phase7c_sequence_fails()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack() with { PostRunSequence = ["review artifact"] }).FinalDecision);

    [Fact]
    public void Missing_market_hours_warning_fails()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack() with { ManualCommandWarning = "Manual command template" }).FinalDecision);

    [Theory]
    [InlineData(" -EnableScheduler")]
    [InlineData(" -EnablePolling")]
    [InlineData(" -SubmitToShadowReplay")]
    [InlineData(" -NewOrderSingle")]
    public void Command_with_runtime_power_or_order_path_fails(string unsafeFragment)
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack() with { RequiredManualCommand = Pack().RequiredManualCommand + unsafeFragment }).FinalDecision);

    [Theory]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, true)]
    public void Runtime_power_flags_fail(bool automatic, bool scheduler, bool replay, bool order, bool gateway, bool mutation)
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack() with
            {
                CanRunAutomatically = automatic,
                SchedulerOrPolling = scheduler,
                RuntimeShadowReplaySubmit = replay,
                OrderSubmission = order,
                GatewayRegistration = gateway,
                TradingMutation = mutation
            }).FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("553=operator")]
    public void Sensitive_text_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator.Validate(Pack(), rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7e()
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

    private static LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPack Pack()
        => new(
            "phase7e-checklist",
            DateTimeOffset.UtcNow,
            "GBPUSD",
            "GBP/USD",
            "4002",
            "8",
            "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 -FinalReadinessFile \"artifacts\\lmax-readonly-runtime-securityid-planning\\final-readiness\\lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json\" -AllowExternalConnections -ConfirmDemoReadOnly -Reason \"Phase 6Z-B operator-approved GBPUSD market-hours read-only snapshot attempt\"",
            "DO NOT RUN UNTIL MARKET HOURS.",
            [
                "Confirm market hours.",
                "Confirm credentials presence only.",
                "Confirm API/Worker FakeLmaxGateway only.",
                "Confirm final readiness PASS.",
                "Confirm Phase 7C closure scripts exist."
            ],
            [
                "One attempt only.",
                "No retry.",
                "Ctrl+C or close process as kill switch."
            ],
            [
                "Review artifact with Phase 7C review script.",
                "Map evidence preview if safe.",
                "Optionally replay local only if appropriate.",
                "Build closure manifest.",
                "Run Phase 7C gate.",
                "Run Phase 7D next-instrument decision."
            ],
            ["Wrong instrument", "Wrong SecurityID", "Credential exposure", "Unknown failure classification"],
            ["Stop process.", "Clear shell-only variables if needed.", "Verify /health FakeLmaxGateway.", "No DB rollback expected because mutation prohibited."],
            ["No scheduler.", "No polling.", "No runtime shadow replay submit.", "No orders.", "No gateway registration.", "No production/UAT.", "No multi-instrument batch."],
            CanRunAutomatically: false,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            "FakeLmaxGateway",
            NoSensitiveContent: true,
            LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.PASS);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
