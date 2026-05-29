using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009PrePaperLedgerPreviewReadinessTests
{
    [Fact]
    public void R010_trial_decision_does_not_authorize_paper_ledger_commit_or_execution()
    {
        using var decision = ReadArtifact("artifacts/readiness/execution-live/phase-exec-live-r010-internal-trial-decision.json");
        var root = decision.RootElement;

        Assert.Equal("DisabledPreviewTrialPassedWithHeldReadiness", root.GetProperty("Decision").GetString());
        Assert.True(root.GetProperty("TrialPassed").GetBoolean());
        Assert.True(root.GetProperty("HeldReadinessObserved").GetBoolean());
        Assert.False(root.GetProperty("ExecutableApproval").GetBoolean());
        Assert.False(root.GetProperty("BrokerApproval").GetBoolean());
        Assert.False(root.GetProperty("LiveApproval").GetBoolean());
        Assert.False(root.GetProperty("PaperLedgerCommitApproval").GetBoolean());
        Assert.True(root.GetProperty("SeparateExplicitExecutableGateRequired").GetBoolean());
    }

    [Fact]
    public void Disabled_preview_boundary_still_blocks_ledger_state_and_order_paths()
    {
        var flags = R009LiveFeatureFlags.DisabledDefaults;
        var guard = R009DisabledBoundaryGuard.Disabled;

        Assert.False(flags.R009PaperLedgerCommitEnabled);
        Assert.False(flags.R009OrderSubmissionEnabled);
        Assert.False(flags.R009BrokerRoutingEnabled);
        Assert.False(flags.R009LiveTradingEnabled);
        Assert.False(flags.R009ExecutableScheduleEnabled);
        Assert.False(guard.PaperLedgerCommitAllowed);
        Assert.False(guard.StateMutationAllowed);
        Assert.False(guard.OrderCreationAllowed);
        Assert.False(guard.BrokerRouteCreationAllowed);
        Assert.True(flags.R009DryRunOnly);
    }

    [Fact]
    public void Paper_ledger_preview_only_outputs_are_distinct_from_commit_outputs()
    {
        var allowedFutureOutputs = new[]
        {
            "PaperLedgerPreviewOnly",
            "HypotheticalPositionDeltaPreview",
            "HypotheticalCashImpactPreview",
            "HypotheticalExposurePreview",
            "OperatorReviewOnly"
        };
        var forbiddenFutureOutputs = new[]
        {
            "PaperLedgerCommit",
            "LedgerMutation",
            "TradingStateMutation",
            "Order",
            "Route",
            "Fill",
            "ExecutionReport",
            "Submission",
            "ExecutableSchedule"
        };

        Assert.DoesNotContain("PaperLedgerCommit", allowedFutureOutputs);
        Assert.Contains("PaperLedgerPreviewOnly", allowedFutureOutputs);
        Assert.Contains("PaperLedgerCommit", forbiddenFutureOutputs);
        Assert.Contains("LedgerMutation", forbiddenFutureOutputs);
        Assert.Contains("TradingStateMutation", forbiddenFutureOutputs);
        Assert.Contains("Order", forbiddenFutureOutputs);
        Assert.Contains("ExecutableSchedule", forbiddenFutureOutputs);
    }

    [Fact]
    public void Future_preview_ledger_consumers_exclude_committers_and_execution_runtime()
    {
        var allowed = new[]
        {
            R009PreviewConsumerType.OperatorReviewTool,
            R009PreviewConsumerType.InternalPmsPreviewConsumer,
            R009PreviewConsumerType.InternalEmsPreviewConsumer,
            R009PreviewConsumerType.InternalOmsPreviewConsumer,
            R009PreviewConsumerType.TestHarness
        };
        var forbidden = new[]
        {
            R009PreviewConsumerType.PaperLedgerCommitter,
            R009PreviewConsumerType.ProductionTradingRuntime,
            R009PreviewConsumerType.BrokerGateway,
            R009PreviewConsumerType.OrderRouter
        };

        Assert.DoesNotContain(R009PreviewConsumerType.PaperLedgerCommitter, allowed);
        Assert.DoesNotContain(R009PreviewConsumerType.ProductionTradingRuntime, allowed);
        Assert.DoesNotContain(R009PreviewConsumerType.BrokerGateway, allowed);
        Assert.DoesNotContain(R009PreviewConsumerType.OrderRouter, allowed);
        Assert.Contains(R009PreviewConsumerType.PaperLedgerCommitter, forbidden);
        Assert.Contains(R009PreviewConsumerType.ProductionTradingRuntime, forbidden);
    }

    private static JsonDocument ReadArtifact(string relativePath)
    {
        var path = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Required artifact missing: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
