namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoCycleWorkerRoutingTests
{
    [Fact]
    public void EnabledCycleRoutesBeforeEveryOrdinaryQueueProcessingPath()
    {
        var workerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "QQ.Production.Intraday.Worker", "Worker.cs"));
        var workerSource = File.ReadAllText(workerPath);
        var cycleBranch = workerSource.IndexOf("if (lmaxDemoCycleEnabled)", StringComparison.Ordinal);
        var ordinaryStartupBranch = workerSource.IndexOf(
            "if (configuration.GetValue(\"Worker:ProcessImmediatelyOnStartup\", true))",
            StringComparison.Ordinal);
        var ordinaryTimer = workerSource.IndexOf("using var timer = new PeriodicTimer", StringComparison.Ordinal);

        Assert.True(cycleBranch >= 0);
        Assert.True(ordinaryStartupBranch > cycleBranch);
        Assert.True(ordinaryTimer > cycleBranch);

        var cycleOnlyBranch = workerSource.Substring(cycleBranch, ordinaryStartupBranch - cycleBranch);
        Assert.Contains("await RunLmaxDemoCycleAsync(stoppingToken);", cycleOnlyBranch, StringComparison.Ordinal);
        Assert.Contains("applicationLifetime.StopApplication();", cycleOnlyBranch, StringComparison.Ordinal);
        Assert.Contains("return;", cycleOnlyBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessOnce", cycleOnlyBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessNextAsync", cycleOnlyBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedCycleLeavesAnAttemptMarkerAndRefusesAutomaticReplay()
    {
        var workerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "QQ.Production.Intraday.Worker", "Worker.cs"));
        var workerSource = File.ReadAllText(workerPath);

        var attemptPath = workerSource.IndexOf("var attemptPath = manifestFullPath + \".attempt.json\";", StringComparison.Ordinal);
        var coordinator = workerSource.IndexOf("await coordinator.RunAsync", StringComparison.Ordinal);
        var attemptWrite = workerSource.IndexOf("await WriteCycleAttemptAsync", StringComparison.Ordinal);
        var priorAttemptRejection = workerSource.IndexOf("LMAX_DEMO_CYCLE_PRIOR_ATTEMPT_UNRESOLVED", StringComparison.Ordinal);
        var nonCompletedResultRejection = workerSource.IndexOf("LMAX_DEMO_CYCLE_RESULT_RECONCILIATION_REQUIRED", StringComparison.Ordinal);
        var reconciliationResult = workerSource.IndexOf("\"ReconciliationRequired\"", StringComparison.Ordinal);

        Assert.True(attemptPath >= 0);
        Assert.True(attemptWrite > attemptPath);
        Assert.True(coordinator > attemptWrite);
        Assert.True(priorAttemptRejection > attemptPath);
        Assert.True(nonCompletedResultRejection > attemptPath);
        Assert.True(reconciliationResult > coordinator);
        Assert.Contains("automaticRetryAllowed = false", workerSource, StringComparison.Ordinal);
        Assert.Contains("reconciliationRequiredOnInterruption = true", workerSource, StringComparison.Ordinal);
    }

}
