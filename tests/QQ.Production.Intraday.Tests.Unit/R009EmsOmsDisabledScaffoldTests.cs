using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009EmsOmsDisabledScaffoldTests
{
    private readonly R009DisabledEmsOmsExecutionAdapter _adapter = new();

    [Fact]
    public void Adapter_accepts_valid_usd_pair_intent_as_preview_only()
    {
        var decision = _adapter.Decide(ValidIntent());

        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.True(decision.PreTradeRiskGate.Passed);
        Assert.Contains(R009DisabledDecisionOutput.DesignOnlyExecutionDecision, decision.Outputs);
        Assert.Contains(R009DisabledDecisionOutput.ExecutionPlanPreview, decision.Outputs);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_rejects_direct_cross_execution_intent()
    {
        var decision = _adapter.Decide(ValidIntent(
            symbol: "EURGBP",
            executionTradableSymbol: "EURGBP",
            normalizedPortfolioSymbol: "EURGBP"));

        Assert.Equal(R009LiveLineStatus.HeldDirectCrossNotNetted, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.DirectCrossExcluded);
        Assert.Contains("DirectCrossExecutionDisabled", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_preserves_usdjpy_inversion_and_security_id_caveat()
    {
        var decision = _adapter.Decide(ValidIntent(
            symbol: "USDJPY",
            executionTradableSymbol: "USDJPY",
            normalizedPortfolioSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8"));

        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        Assert.Equal(R009DisabledEmsOmsExecutionAdapter.ContractVersion, decision.Audit.ContractVersion);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_rejects_legacy_06_as_future_canonical_target_close()
    {
        var intent = ValidIntent(
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
            canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York");

        var decision = _adapter.Decide(intent);

        Assert.Equal(R009LiveLineStatus.InconclusiveSafe, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.CanonicalTargetClose);
        Assert.False(decision.PreTradeRiskGate.QuarterHourTargetClose);
        Assert.Contains("CanonicalTargetCloseMustBeQuarterHour", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_requires_readiness_bindings_and_holds_without_order()
    {
        var decision = _adapter.Decide(ValidIntent(quoteWindowReadinessId: null));

        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.QuoteWindowReadinessPresent);
        Assert.Contains("MissingQuoteWindowReadiness", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_requires_risk_and_operator_approval()
    {
        var decision = _adapter.Decide(ValidIntent(
            riskApprovalStatus: R009ApprovalStatus.Missing,
            operatorApprovalStatus: R009ApprovalStatus.Missing));

        Assert.Equal(R009LiveLineStatus.HeldRiskOperatorMissing, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.RiskApprovalPresent);
        Assert.False(decision.PreTradeRiskGate.OperatorApprovalPresent);
        Assert.Contains("MissingRiskApproval", decision.HoldReason);
        Assert.Contains("MissingOperatorApproval", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Adapter_cannot_create_orders_routes_fills_reports_or_executable_schedules()
    {
        var decision = _adapter.Decide(ValidIntent());

        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesSubmission);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.NotAnOrder);
        Assert.True(decision.NoRoute);
        Assert.True(decision.NoFill);
        Assert.True(decision.NoExecutionReport);
        Assert.True(decision.NoExecutableSchedule);
    }

    [Fact]
    public void Kill_switch_defaults_are_disabled_and_dry_run_only()
    {
        var flags = R009LiveFeatureFlags.DisabledDefaults;

        Assert.False(flags.R009LiveTradingEnabled);
        Assert.False(flags.R009BrokerRoutingEnabled);
        Assert.False(flags.R009OrderSubmissionEnabled);
        Assert.False(flags.R009ExecutableScheduleEnabled);
        Assert.False(flags.R009PaperLedgerCommitEnabled);
        Assert.False(flags.R009SchedulerEnabled);
        Assert.False(flags.R009BackgroundWorkerEnabled);
        Assert.True(flags.R009DryRunOnly);
    }

    [Fact]
    public void Controlled_residual_cross_cannot_become_always_market_at_close()
    {
        var decision = _adapter.Decide(ValidIntent(
            residualNotional: 250_000m,
            residualOpportunityCostBps: 1.6m,
            expectedSpreadCostBps: 0.4m));

        Assert.True(decision.ControlledResidualCrossSelected);
        Assert.False(decision.ControlledResidualCrossAlwaysMarketAtClose);
        Assert.Contains("preview-eligible", decision.ResidualRiskAssessment);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Held_missing_readiness_produces_hold_not_order()
    {
        var decision = _adapter.Decide(ValidIntent(
            quoteWindowReadinessId: null,
            closeBenchmarkReadinessId: null,
            feedQualityReadinessId: null));

        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, decision.LineStatus);
        Assert.Contains(R009DisabledDecisionOutput.HoldReason, decision.Outputs);
        Assert.Contains(R009DisabledDecisionOutput.ManualReviewRecommendation, decision.Outputs);
        AssertPreviewOnly(decision);
    }

    [Theory]
    [InlineData("USDCNH")]
    [InlineData("USDSEK")]
    [InlineData("USDZAR")]
    public void Nonmajor_em_scandi_and_cnh_symbols_remain_calibration_required(string unsupportedSymbol)
    {
        var decision = _adapter.Decide(ValidIntent(
            symbol: unsupportedSymbol,
            executionTradableSymbol: unsupportedSymbol,
            normalizedPortfolioSymbol: unsupportedSymbol,
            requiresInversion: true));

        Assert.Equal(R009LiveLineStatus.HeldUnsupportedInstrument, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.SupportedSymbol);
        Assert.Contains("UnsupportedInstrument", decision.HoldReason);
        AssertPreviewOnly(decision);
    }

    [Fact]
    public void Audusd_remains_supported_and_not_misclassified_as_failed()
    {
        var decision = _adapter.Decide(ValidIntent(
            symbol: "AUDUSD",
            executionTradableSymbol: "AUDUSD",
            normalizedPortfolioSymbol: "AUDUSD"));

        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.True(decision.PreTradeRiskGate.SupportedSymbol);
        AssertPreviewOnly(decision);
    }

    private static R009EmsOmsExecutionIntent ValidIntent(
        string symbol = "EURUSD",
        string executionTradableSymbol = "EURUSD",
        string normalizedPortfolioSymbol = "EURUSD",
        bool requiresInversion = false,
        DateTimeOffset? canonicalTargetCloseUtc = null,
        string canonicalTargetCloseLocal = "2026-05-25T15:15:00 America/New_York",
        string? quoteWindowReadinessId = "quote-ready-001",
        string? closeBenchmarkReadinessId = "close-ready-001",
        string? feedQualityReadinessId = "feed-ready-001",
        R009ApprovalStatus operatorApprovalStatus = R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly,
        R009ApprovalStatus riskApprovalStatus = R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly,
        string? securityId = null,
        string? securityIdSource = null,
        decimal expectedSpreadCostBps = 0.8m,
        decimal residualNotional = 0m,
        decimal residualOpportunityCostBps = 0m)
        => new(
            ExecutionIntentId: $"intent-{executionTradableSymbol.ToLowerInvariant()}",
            SourcePmsCycleId: "pms-cycle-disabled-preview",
            SourceQubesRunId: "qubes-run-disabled-preview",
            SourceRebalanceIntentId: "rebalance-intent-disabled-preview",
            SourceRiskReviewId: "risk-review-design-only",
            Symbol: symbol,
            ExecutionTradableSymbol: executionTradableSymbol,
            NormalizedPortfolioSymbol: normalizedPortfolioSymbol,
            RequiresInversion: requiresInversion,
            Side: R009LiveIntentSide.Buy,
            TargetQuantity: 1_000_000m,
            TargetNotional: 1_000_000m,
            CanonicalTargetCloseUtc: canonicalTargetCloseUtc ?? new DateTimeOffset(2026, 5, 25, 19, 15, 0, TimeSpan.Zero),
            CanonicalTargetCloseLocal: canonicalTargetCloseLocal,
            CanonicalSession: "14:15-21:00 America/New_York",
            BarRole: R009LiveBarRole.IntradayRebalance,
            MustEndFlat: true,
            OvernightAllowed: false,
            QuoteWindowReadinessId: quoteWindowReadinessId,
            CloseBenchmarkReadinessId: closeBenchmarkReadinessId,
            FeedQualityReadinessId: feedQualityReadinessId,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            OperatorApprovalStatus: operatorApprovalStatus,
            RiskApprovalStatus: riskApprovalStatus,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            NonExecutable: true,
            SecurityID: securityId,
            SecurityIDSource: securityIdSource,
            ExpectedSpreadCostBps: expectedSpreadCostBps,
            MaxSpreadCostBps: 5.0m,
            ResidualNotional: residualNotional,
            ResidualOpportunityCostBps: residualOpportunityCostBps);

    private static void AssertPreviewOnly(R009DisabledExecutionDecision decision)
    {
        Assert.True(decision.DesignOnly);
        Assert.True(decision.PaperOnly);
        Assert.True(decision.NonExecutable);
        Assert.True(decision.NotAnOrder);
        Assert.True(decision.NotSubmitted);
        Assert.True(decision.NoBrokerRoute);
        Assert.True(decision.NoChildSlices);
        Assert.True(decision.NoChildOrders);
        Assert.True(decision.NoExecutableSchedule);
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
