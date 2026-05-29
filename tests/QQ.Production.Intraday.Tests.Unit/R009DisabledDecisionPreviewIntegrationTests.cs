using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009DisabledDecisionPreviewIntegrationTests
{
    [Fact]
    public void Paper_plan_line_converts_to_execution_intent()
    {
        var intent = new R009PaperPlanExecutionIntentConverter().Convert(PreviewLine(), "phase-exec-paper-r012-r009-design-only-preview-lines.json");

        Assert.Equal("line-audusd:r009-disabled-intent", intent.ExecutionIntentId);
        Assert.Equal("paper-cycle-001", intent.SourcePmsCycleId);
        Assert.Equal("qubes-001", intent.SourceQubesRunId);
        Assert.Equal("AUDUSD", intent.ExecutionTradableSymbol);
        Assert.Equal(R009LiveIntentSide.Buy, intent.Side);
        Assert.Equal(172684m, intent.TargetNotional);
        Assert.False(intent.LiveTradingEnabled);
        Assert.False(intent.BrokerRoutingEnabled);
        Assert.False(intent.OrderSubmissionEnabled);
        Assert.True(intent.NonExecutable);
    }

    [Fact]
    public void Valid_usd_pair_line_produces_design_only_decision_preview()
    {
        var result = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[] { PreviewLine() },
            "phase-exec-paper-r012-r009-design-only-preview-lines.json",
            new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero));

        var decision = Assert.Single(result.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.True(decision.PreTradeRiskGate.Passed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Audit.InputHash));
        Assert.False(string.IsNullOrWhiteSpace(decision.Audit.R009DecisionHash));
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Held_missing_readiness_line_produces_hold_decision_not_order()
    {
        var result = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[] { PreviewLine(quoteReady: false, closeReady: false, feedReady: false) },
            "phase-exec-paper-r019-r009-design-only-preview-lines.json");

        var decision = Assert.Single(result.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, decision.LineStatus);
        Assert.Contains("MissingQuoteWindowReadiness", decision.HoldReason);
        Assert.Contains(R009DisabledDecisionOutput.HoldReason, decision.Outputs);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Direct_cross_intent_is_rejected_by_disabled_preview_flow()
    {
        var result = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[] { PreviewLine(symbol: "EURGBP", executionSymbol: "EURGBP", normalizedSymbol: "EURGBP") },
            "synthetic-direct-cross-negative-control");

        var decision = Assert.Single(result.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldDirectCrossNotNetted, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.DirectCrossExcluded);
        AssertPreviewOnly(decision);
    }

    [Theory]
    [InlineData("USDCNH")]
    [InlineData("USDSEK")]
    [InlineData("USDZAR")]
    public void Unsupported_nonmajor_em_scandi_cnh_is_rejected(string unsupportedSymbol)
    {
        var result = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[] { PreviewLine(symbol: unsupportedSymbol, executionSymbol: unsupportedSymbol, normalizedSymbol: unsupportedSymbol, requiresInversion: true) },
            "unsupported-symbol-negative-control");

        var decision = Assert.Single(result.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldUnsupportedInstrument, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.SupportedSymbol);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Usdjpy_inversion_and_security_id_caveat_are_preserved_during_conversion()
    {
        var intent = new R009PaperPlanExecutionIntentConverter().Convert(
            PreviewLine(symbol: "JPYUSD", executionSymbol: "USDJPY", normalizedSymbol: "JPYUSD", requiresInversion: true),
            "phase-exec-paper-r012-r009-design-only-preview-lines.json");

        Assert.Equal("USDJPY", intent.ExecutionTradableSymbol);
        Assert.Equal("JPYUSD", intent.NormalizedPortfolioSymbol);
        Assert.True(intent.RequiresInversion);
        Assert.Equal("4004", intent.SecurityID);
        Assert.Equal("8", intent.SecurityIDSource);

        var decision = new R009DisabledEmsOmsExecutionAdapter().Decide(intent);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Legacy_06_close_is_rejected_by_decision_preview()
    {
        var result = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[]
            {
                PreviewLine(
                    canonicalCloseUtc: "2025-10-21T18:06:00Z",
                    canonicalCloseLocal: "2025-10-21T14:06:00 America/New_York")
            },
            "legacy-close-negative-control");

        var decision = Assert.Single(result.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.InconclusiveSafe, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.QuarterHourTargetClose);
        Assert.Contains("CanonicalTargetCloseMustBeQuarterHour", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Disabled_flags_remain_false_for_decision_preview_flow()
    {
        var flags = R009LiveFeatureFlags.DisabledDefaults;
        var guard = R009DisabledBoundaryGuard.Disabled;

        Assert.False(flags.R009LiveTradingEnabled);
        Assert.False(flags.R009BrokerRoutingEnabled);
        Assert.False(flags.R009OrderSubmissionEnabled);
        Assert.False(flags.R009ExecutableScheduleEnabled);
        Assert.False(flags.R009PaperLedgerCommitEnabled);
        Assert.False(flags.R009SchedulerEnabled);
        Assert.False(flags.R009BackgroundWorkerEnabled);
        Assert.True(flags.R009DryRunOnly);
        Assert.False(guard.OrderCreationAllowed);
        Assert.False(guard.BrokerRouteCreationAllowed);
        Assert.False(guard.ScheduleExecutionAllowed);
        Assert.False(guard.PaperLedgerCommitAllowed);
    }

    [Fact]
    public void Adapter_cannot_create_order_route_fill_report_schedule_or_ledger_commit()
    {
        var decision = new R009DisabledDecisionPreviewIntegrationService().GenerateDecisionPreviews(
            new[] { PreviewLine() },
            "phase-exec-paper-r012-r009-design-only-preview-lines.json").DecisionPreviews.Single();

        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesSubmission);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.NoPaperLedgerCommit);
        AssertPreviewOnly(decision);
    }

    private static R009PaperPlanPreviewLine PreviewLine(
        string symbol = "AUDUSD",
        string executionSymbol = "AUDUSD",
        string normalizedSymbol = "AUDUSD",
        bool requiresInversion = false,
        bool quoteReady = true,
        bool closeReady = true,
        bool feedReady = true,
        string canonicalCloseUtc = "2025-10-21T18:30:00Z",
        string canonicalCloseLocal = "2025-10-21T14:30:00 America/New_York")
        => new(
            BatchEntryId: "batch-001",
            FixturePath: "data/qubes-fixtures/disabled-preview/qubes-001.txt",
            QubesRunId: "qubes-001",
            RequestedCycleRunId: "paper-cycle-001",
            PaperExecutionPlanLineId: $"line-{executionSymbol.ToLowerInvariant()}",
            Symbol: symbol,
            ExecutionTradableSymbol: executionSymbol,
            NormalizedPortfolioSymbol: normalizedSymbol,
            RequiresInversion: requiresInversion,
            Side: "Buy",
            TargetQuantity: null,
            TargetNotional: 172684m,
            CanonicalTargetCloseTimestamp: canonicalCloseUtc,
            CanonicalTargetCloseLocal: canonicalCloseLocal,
            CanonicalSession: "14:15-21:00 America/New_York",
            BarRole: "IntradayRebalance",
            CanonicalQuarterHourTimestampConfirmed: true,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            QuoteWindowReadinessBinding: quoteReady ? Ready("quote-ready-001", executionSymbol) : null,
            CloseBenchmarkReadinessBinding: closeReady ? Ready("close-ready-001", executionSymbol) : null,
            FeedQualityReadinessBinding: feedReady ? Ready("feed-ready-001", executionSymbol) : null,
            RiskReviewStatus: "ApprovedForNonExecutablePreview",
            OperatorApprovalStatus: "ApprovedForDesignOnlyPreviewOnly",
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true);

    private static R009ReadinessBindingPreview Ready(string id, string symbol)
        => new(id, symbol, "2025-10-21T18:30:00Z", "Ready", "EXEC-SIM-R053");

    private static void AssertPreviewOnly(R009DisabledExecutionDecision decision)
    {
        Assert.True(decision.DesignOnly);
        Assert.True(decision.PaperOnly);
        Assert.True(decision.NonExecutable);
        Assert.True(decision.NotAnOrder);
        Assert.True(decision.NotSubmitted);
        Assert.True(decision.NoBrokerRoute);
        Assert.True(decision.NoFill);
        Assert.True(decision.NoExecutionReport);
        Assert.True(decision.NoRoute);
        Assert.True(decision.NoSubmission);
        Assert.True(decision.NoPaperLedgerCommit);
        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesSubmission);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.Audit.NoOrderDomainOutput);
        Assert.True(decision.Audit.NoBrokerRoute);
        Assert.True(decision.Audit.DryRunOnly);
    }
}
