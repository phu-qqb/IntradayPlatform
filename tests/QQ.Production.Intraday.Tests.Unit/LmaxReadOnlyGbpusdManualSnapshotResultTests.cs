using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdManualSnapshotResultTests
{
    [Fact]
    public void Successful_fake_gbpusd_snapshot_validates()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result()).FinalDecision);

    [Fact]
    public void Completed_with_bid_ask_validates_pass()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result()).FinalDecision);

    [Fact]
    public void Completed_with_empty_book_validates_pass_with_known_warnings()
    {
        var result = LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(EmptyBookResult());

        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS_WITH_KNOWN_WARNINGS, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "EmptyBookHasOneSnapshotAndNoRejects" && x.Decision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS);
    }

    [Fact]
    public void Failed_safe_gbpusd_result_validates_when_no_unsafe_flags()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.PASS, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result() with { Status = "FailedSafeSnapshotTimeout", SnapshotReceived = false, BestBid = null, BestAsk = null, Mid = null, EntryCount = 0 }).FinalDecision);

    [Fact]
    public void Empty_book_with_rejects_fails()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(EmptyBookResult() with { MarketDataRequestRejectCount = 1 }).FinalDecision);

    [Fact]
    public void Empty_book_with_snapshot_not_received_fails()
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(EmptyBookResult() with { SnapshotReceived = false }).FinalDecision);

    [Theory]
    [InlineData("EURUSD", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string securityId)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result() with { Symbol = symbol, SecurityId = securityId }).FinalDecision);

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Unsafe_flags_fail(bool order, bool shadow, bool mutation, bool scheduler, bool credentials)
    {
        var result = LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result() with
        {
            OrderSubmissionAttempted = order,
            ShadowReplaySubmitAttempted = shadow,
            TradingMutationAttempted = mutation,
            SchedulerStarted = scheduler,
            CredentialValuesReturned = credentials
        });
        Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    public void Sensitive_or_order_content_fails(string rawText)
        => Assert.Equal(LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL, LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(Result(), rawText).FinalDecision);

    [Fact]
    public void Wrapper_is_single_instrument_gbpusd_only_and_requires_readiness()
    {
        var repoRoot = FindRepoRoot();
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1"));
        Assert.Contains("GBPUSD", script);
        Assert.Contains("4002", script);
        Assert.Contains("SecurityIdSource", script);
        Assert.Contains("FinalReadinessFile", script);
        Assert.Contains("SnapshotPlusUpdates", script);
        Assert.Contains("SecurityIdOnly", script);
        Assert.Contains("no retry", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6w()
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

    private static LmaxReadOnlyGbpusdManualSnapshotResult Result()
        => new("run", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, "Completed", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, true, true, false, true, true, true, true, true, true, false, false, false, false, 1.2501, 1.2503, 1.2502, 2, 1000, true, "Redacted", "final-readiness.json", 1, 0, 0, 0, [], []);

    private static LmaxReadOnlyGbpusdManualSnapshotResult EmptyBookResult()
        => Result() with
        {
            Status = "CompletedWithEmptyBook",
            BestBid = null,
            BestAsk = null,
            Mid = null,
            EntryCount = 0,
            MarketDataSnapshotCount = 1,
            MarketDataRequestRejectCount = 0,
            BusinessMessageRejectCount = 0,
            RejectCount = 0,
            Warnings = ["Market data snapshot was received with no entries."],
            Errors = []
        };

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
