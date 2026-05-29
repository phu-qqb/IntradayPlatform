using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009DisabledPreviewApiCliContractTests
{
    private readonly R009DisabledPreviewContractService _service = new();

    [Fact]
    public void Valid_inline_execution_intent_request_returns_preview_response()
    {
        var response = _service.Preview(InlineRequest(ValidIntent()), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal("PreviewGenerated", response.DecisionStatus);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.False(string.IsNullOrWhiteSpace(response.IdempotencyHash));
        Assert.False(string.IsNullOrWhiteSpace(response.AuditHash));
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Valid_artifact_based_request_returns_preview_response()
    {
        var response = _service.Preview(
            ArtifactRequest(),
            new[] { PreviewLine() },
            FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal("PreviewGenerated", response.DecisionStatus);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.Equal("phase-exec-paper-r012-r009-design-only-preview-lines.json", ArtifactRequest().SourceArtifactPath);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Request_requiring_live_trading_is_rejected()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(), liveTradingEnabled: true), createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.Equal("Rejected", response.DecisionStatus);
        Assert.Empty(response.DecisionPreviews);
        Assert.Contains("LiveTradingMustRemainDisabled", response.RejectionReasons);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Request_enabling_broker_routing_is_rejected()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(), brokerRoutingEnabled: true), createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.Contains("BrokerRoutingMustRemainDisabled", response.RejectionReasons);
        Assert.Empty(response.DecisionPreviews);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Request_enabling_order_submission_is_rejected()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(), orderSubmissionEnabled: true), createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.Contains("OrderSubmissionMustRemainDisabled", response.RejectionReasons);
        Assert.Empty(response.DecisionPreviews);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Request_asking_for_executable_schedule_is_rejected()
    {
        var response = _service.Preview(
            InlineRequest(ValidIntent(), requestedOutputs: new[] { "ExecutableSchedule" }),
            createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.Contains("ForbiddenOutputRequested:ExecutableSchedule", response.RejectionReasons);
        Assert.Empty(response.DecisionPreviews);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Direct_cross_intent_is_rejected_by_decision_not_converted_to_order()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(
            symbol: "EURGBP",
            executionSymbol: "EURGBP",
            normalizedSymbol: "EURGBP")), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal("PreviewGeneratedWithHeldLines", response.DecisionStatus);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldDirectCrossNotNetted, decision.LineStatus);
        AssertDecisionPreviewOnly(decision);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Legacy_06_target_close_is_rejected_by_decision_preview()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
            canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York")), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal("PreviewGeneratedWithHeldLines", response.DecisionStatus);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.InconclusiveSafe, decision.LineStatus);
        Assert.False(decision.PreTradeRiskGate.QuarterHourTargetClose);
        Assert.Contains("CanonicalTargetCloseMustBeQuarterHour", decision.HoldReason);
        AssertDecisionPreviewOnly(decision);
    }

    [Fact]
    public void Usdjpy_caveat_is_preserved_in_response()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(
            symbol: "USDJPY",
            executionSymbol: "USDJPY",
            normalizedSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8")), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        AssertDecisionPreviewOnly(decision);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Missing_readiness_returns_held_missing_readiness_not_order()
    {
        var response = _service.Preview(InlineRequest(ValidIntent(
            quoteWindowReadinessId: null,
            closeBenchmarkReadinessId: null,
            feedQualityReadinessId: null)), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal("PreviewGeneratedWithHeldLines", response.DecisionStatus);
        var decision = Assert.Single(response.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, decision.LineStatus);
        Assert.Contains("MissingQuoteWindowReadiness", decision.HoldReason);
        AssertDecisionPreviewOnly(decision);
        AssertResponsePreviewOnly(response);
    }

    [Fact]
    public void Response_cannot_produce_order_fill_route_submission_schedule_or_ledger_output()
    {
        var response = _service.Preview(InlineRequest(ValidIntent()), createdAtUtc: FixedNow());
        var decision = Assert.Single(response.DecisionPreviews);

        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesSubmission);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.NoPaperLedgerCommit);
        AssertResponsePreviewOnly(response);
        AssertDecisionPreviewOnly(decision);
    }

    private static R009DisabledPreviewRequest InlineRequest(
        R009EmsOmsExecutionIntent intent,
        bool dryRunOnly = true,
        bool liveTradingEnabled = false,
        bool brokerRoutingEnabled = false,
        bool orderSubmissionEnabled = false,
        bool executableScheduleEnabled = false,
        bool paperLedgerCommitEnabled = false,
        IReadOnlyList<string>? requestedOutputs = null)
        => new(
            RequestId: "r009-disabled-preview-inline-request",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            SourceType: R009DisabledPreviewSourceType.ExecutionIntent,
            ExecutionIntent: intent,
            SourceArtifactPath: null,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            DryRunOnly: dryRunOnly,
            LiveTradingEnabled: liveTradingEnabled,
            BrokerRoutingEnabled: brokerRoutingEnabled,
            OrderSubmissionEnabled: orderSubmissionEnabled,
            ExecutableScheduleEnabled: executableScheduleEnabled,
            PaperLedgerCommitEnabled: paperLedgerCommitEnabled,
            OperatorApprovalScope: "DesignOnlyPreviewOnly",
            RiskApprovalScope: "DesignOnlyPreviewOnly",
            NoBrokerRoute: true,
            RequestedOutputs: requestedOutputs);

    private static R009DisabledPreviewRequest ArtifactRequest()
        => new(
            RequestId: "r009-disabled-preview-artifact-request",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            SourceType: R009DisabledPreviewSourceType.PaperPlanLineArtifact,
            ExecutionIntent: null,
            SourceArtifactPath: "phase-exec-paper-r012-r009-design-only-preview-lines.json",
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            DryRunOnly: true,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            ExecutableScheduleEnabled: false,
            PaperLedgerCommitEnabled: false,
            OperatorApprovalScope: "DesignOnlyPreviewOnly",
            RiskApprovalScope: "DesignOnlyPreviewOnly",
            NoBrokerRoute: true);

    private static R009EmsOmsExecutionIntent ValidIntent(
        string symbol = "AUDUSD",
        string executionSymbol = "AUDUSD",
        string normalizedSymbol = "AUDUSD",
        bool requiresInversion = false,
        DateTimeOffset? canonicalTargetCloseUtc = null,
        string canonicalTargetCloseLocal = "2026-05-25T15:15:00 America/New_York",
        string? quoteWindowReadinessId = "quote-ready-001",
        string? closeBenchmarkReadinessId = "close-ready-001",
        string? feedQualityReadinessId = "feed-ready-001",
        string? securityId = null,
        string? securityIdSource = null)
        => new(
            ExecutionIntentId: $"intent-{executionSymbol.ToLowerInvariant()}",
            SourcePmsCycleId: "pms-cycle-disabled-preview",
            SourceQubesRunId: "qubes-run-disabled-preview",
            SourceRebalanceIntentId: "rebalance-intent-disabled-preview",
            SourceRiskReviewId: "risk-review-design-only",
            Symbol: symbol,
            ExecutionTradableSymbol: executionSymbol,
            NormalizedPortfolioSymbol: normalizedSymbol,
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
            OperatorApprovalStatus: R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly,
            RiskApprovalStatus: R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            NonExecutable: true,
            SecurityID: securityId,
            SecurityIDSource: securityIdSource);

    private static R009PaperPlanPreviewLine PreviewLine()
        => new(
            BatchEntryId: "batch-001",
            FixturePath: "data/qubes-fixtures/disabled-preview/qubes-001.txt",
            QubesRunId: "qubes-001",
            RequestedCycleRunId: "paper-cycle-001",
            PaperExecutionPlanLineId: "paper-line-audusd",
            Symbol: "AUDUSD",
            ExecutionTradableSymbol: "AUDUSD",
            NormalizedPortfolioSymbol: "AUDUSD",
            RequiresInversion: false,
            Side: "Buy",
            TargetQuantity: null,
            TargetNotional: 172684m,
            CanonicalTargetCloseTimestamp: "2025-10-21T18:30:00Z",
            CanonicalTargetCloseLocal: "2025-10-21T14:30:00 America/New_York",
            CanonicalSession: "14:15-21:00 America/New_York",
            BarRole: "IntradayRebalance",
            CanonicalQuarterHourTimestampConfirmed: true,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            QuoteWindowReadinessBinding: Ready("quote-ready-001"),
            CloseBenchmarkReadinessBinding: Ready("close-ready-001"),
            FeedQualityReadinessBinding: Ready("feed-ready-001"),
            RiskReviewStatus: "ApprovedForNonExecutablePreview",
            OperatorApprovalStatus: "ApprovedForDesignOnlyPreviewOnly",
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true);

    private static R009ReadinessBindingPreview Ready(string id)
        => new(id, "AUDUSD", "2025-10-21T18:30:00Z", "Ready", "EXEC-SIM-R053");

    private static DateTimeOffset FixedNow()
        => new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);

    private static void AssertResponsePreviewOnly(R009DisabledPreviewResponse response)
    {
        Assert.True(response.NonExecutable);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NotSubmitted);
        Assert.True(response.NoBrokerRoute);
        Assert.True(response.NoFill);
        Assert.True(response.NoExecutionReport);
        Assert.True(response.NoRoute);
        Assert.True(response.NoSubmission);
        Assert.True(response.NoPaperLedgerCommit);
        Assert.True(response.SafetyFlags.DryRunOnly);
        Assert.False(response.SafetyFlags.LiveTradingEnabled);
        Assert.False(response.SafetyFlags.BrokerRoutingEnabled);
        Assert.False(response.SafetyFlags.OrderSubmissionEnabled);
        Assert.False(response.SafetyFlags.ExecutableScheduleEnabled);
        Assert.False(response.SafetyFlags.PaperLedgerCommitEnabled);
        Assert.True(response.SafetyFlags.NoBrokerRoute);
    }

    private static void AssertDecisionPreviewOnly(R009DisabledExecutionDecision decision)
    {
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
    }
}
