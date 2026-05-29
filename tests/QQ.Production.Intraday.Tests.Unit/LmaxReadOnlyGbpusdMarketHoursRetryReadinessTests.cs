using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdMarketHoursRetryReadinessTests
{
    [Fact]
    public void Valid_retry_readiness_after_empty_book_validates_pass()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.PASS,
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness(), "PASS_WITH_KNOWN_WARNINGS").FinalDecision);

    [Fact]
    public void Retry_plan_fails_if_previous_result_was_not_empty_book()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness() with { PreviousResultStatus = "Completed" }, "PASS").FinalDecision);

    [Theory]
    [InlineData("EURUSD", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Retry_plan_fails_if_symbol_or_security_id_mismatch(string symbol, string securityId)
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness() with { Symbol = symbol, SecurityId = securityId }, "PASS_WITH_KNOWN_WARNINGS").FinalDecision);

    [Fact]
    public void Retry_plan_fails_if_can_run_automatically_true()
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness() with { CanRunAutomatically = true }, "PASS_WITH_KNOWN_WARNINGS").FinalDecision);

    [Theory]
    [InlineData(false, true, true, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, true, true, false)]
    public void Unsafe_retry_power_flags_fail(bool noScheduler, bool noPolling, bool noShadow, bool noOrders, bool noMutation)
    {
        var result = LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness() with
        {
            NoScheduler = noScheduler,
            NoPolling = noPolling,
            NoRuntimeShadowReplaySubmit = noShadow,
            NoOrderSubmission = noOrders,
            NoTradingMutation = noMutation
        }, "PASS_WITH_KNOWN_WARNINGS");

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("run automatically")]
    [InlineData("NewOrderSingle")]
    [InlineData("production authorization")]
    public void Sensitive_or_authorization_language_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL,
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator.Validate(Readiness(), "PASS_WITH_KNOWN_WARNINGS", rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6y()
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
        Assert.DoesNotContain("BackgroundService", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("OrderStatusRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static LmaxReadOnlyGbpusdMarketHoursRetryReadiness Readiness()
        => new(
            "retry",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 6Y preparation for Monday market-hours GBPUSD retry after Saturday empty-book result",
            "GBPUSD",
            "GBP/USD",
            "4002",
            "8",
            "final-readiness.json",
            "phase6x-review.json",
            "CompletedWithEmptyBook",
            PreviousAttemptWasOutsideMarketHours: true,
            RetryAllowedOnlyDuringMarketHours: true,
            RetryIsManualOnly: true,
            RetryAttemptCount: 1,
            NoScheduler: true,
            NoPolling: true,
            NoRuntimeShadowReplaySubmit: true,
            NoOrderSubmission: true,
            NoTradingMutation: true,
            "FakeLmaxGateway",
            CanRunAutomatically: false,
            NoSensitiveContent: true,
            "DO NOT RUN FROM THIS SCRIPT. Future Phase 6Z operator-approved command only.",
            "Phase 6Z operator-approved GBPUSD market-hours snapshot attempt",
            "Phase 6Y prepares the market-hours retry plan and does not run GBPUSD.",
            LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.PASS);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
