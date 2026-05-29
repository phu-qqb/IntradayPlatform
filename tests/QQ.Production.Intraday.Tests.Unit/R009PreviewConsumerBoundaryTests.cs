using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009PreviewConsumerBoundaryTests
{
    private readonly R009PreviewConsumerBoundaryService _service = new();

    [Theory]
    [InlineData(R009PreviewConsumerType.InternalPmsPreviewConsumer)]
    [InlineData(R009PreviewConsumerType.InternalEmsPreviewConsumer)]
    [InlineData(R009PreviewConsumerType.InternalOmsPreviewConsumer)]
    [InlineData(R009PreviewConsumerType.OperatorReviewTool)]
    [InlineData(R009PreviewConsumerType.TestHarness)]
    public void Allowed_consumers_can_request_disabled_preview_only(R009PreviewConsumerType consumerType)
    {
        var response = _service.RequestSinglePreview(Envelope(consumerType, SingleRequest(ValidIntent())), createdAtUtc: FixedNow());

        Assert.True(response.Accepted);
        Assert.True(response.BoundaryGuardResult.ConsumerAllowed);
        Assert.True(response.BoundaryGuardResult.UsageAllowed);
        Assert.NotNull(response.SinglePreviewResponse);
        AssertPreviewConsumerSafe(response);
    }

    [Theory]
    [InlineData(R009PreviewConsumerType.BrokerGateway)]
    [InlineData(R009PreviewConsumerType.LiveMarketDataWorker)]
    [InlineData(R009PreviewConsumerType.Scheduler)]
    [InlineData(R009PreviewConsumerType.BackgroundWorker)]
    [InlineData(R009PreviewConsumerType.OrderRouter)]
    [InlineData(R009PreviewConsumerType.ExecutionReportHandler)]
    [InlineData(R009PreviewConsumerType.PaperLedgerCommitter)]
    [InlineData(R009PreviewConsumerType.ProductionTradingRuntime)]
    public void Forbidden_consumers_are_rejected(R009PreviewConsumerType consumerType)
    {
        var response = _service.RequestSinglePreview(Envelope(consumerType, SingleRequest(ValidIntent())), createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.False(response.BoundaryGuardResult.ConsumerAllowed);
        Assert.Contains($"ForbiddenConsumer:{consumerType}", response.RejectionReasons);
        Assert.Null(response.SinglePreviewResponse);
        AssertPreviewConsumerSafe(response);
    }

    [Theory]
    [InlineData("ConvertToOrder")]
    [InlineData("ConvertToRouteSubmission")]
    [InlineData("TriggerSchedulerWorker")]
    [InlineData("CommitLedger")]
    public void Forbidden_usage_is_rejected(string usage)
    {
        var response = _service.RequestSinglePreview(
            Envelope(R009PreviewConsumerType.InternalEmsPreviewConsumer, SingleRequest(ValidIntent()), usage),
            createdAtUtc: FixedNow());

        Assert.False(response.Accepted);
        Assert.Contains($"ForbiddenUsage:{usage}", response.RejectionReasons);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Preview_response_cannot_be_converted_to_order_route_schedule_or_ledger()
    {
        var response = _service.RequestSinglePreview(Envelope(R009PreviewConsumerType.InternalOmsPreviewConsumer, SingleRequest(ValidIntent())), createdAtUtc: FixedNow());
        var decision = Assert.Single(response.SinglePreviewResponse!.DecisionPreviews);

        Assert.False(response.UsagePolicy.PreviewOutputIsOrderIntent);
        Assert.False(response.UsagePolicy.PreviewOutputIsRouteable);
        Assert.False(response.UsagePolicy.PreviewOutputIsExecutableSchedule);
        Assert.Contains("CommitLedger", response.UsagePolicy.ForbiddenUsages);
        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.NoPaperLedgerCommit);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Batch_preview_response_preserves_per_line_non_executable_flags()
    {
        var batch = BatchRequest(
            BatchItem("ready", ValidIntent()),
            BatchItem("held", ValidIntent(quoteWindowReadinessId: null, closeBenchmarkReadinessId: null, feedQualityReadinessId: null)));

        var response = _service.RequestBatchPreview(Envelope(R009PreviewConsumerType.InternalPmsPreviewConsumer, batch), FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal(1, response.BatchPreviewResponse!.PreviewReadyCount);
        Assert.Equal(1, response.BatchPreviewResponse.HeldMissingReadinessCount);
        foreach (var item in response.BatchPreviewResponse.ItemResults.Where(x => x.PreviewResponse is not null))
        {
            Assert.True(item.PreviewResponse!.NonExecutable);
            Assert.True(item.PreviewResponse.NotAnOrder);
            Assert.True(item.PreviewResponse.NoBrokerRoute);
            Assert.True(item.PreviewResponse.NoPaperLedgerCommit);
        }
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Held_missing_readiness_cannot_become_order()
    {
        var response = _service.RequestSinglePreview(Envelope(
            R009PreviewConsumerType.OperatorReviewTool,
            SingleRequest(ValidIntent(quoteWindowReadinessId: null, closeBenchmarkReadinessId: null, feedQualityReadinessId: null))),
            createdAtUtc: FixedNow());

        var decision = Assert.Single(response.SinglePreviewResponse!.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, decision.LineStatus);
        Assert.False(decision.CreatesOrder);
        Assert.True(decision.NotAnOrder);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Direct_cross_rejection_remains_at_preview_boundary()
    {
        var response = _service.RequestSinglePreview(Envelope(
            R009PreviewConsumerType.TestHarness,
            SingleRequest(ValidIntent(symbol: "EURGBP", executionSymbol: "EURGBP", normalizedSymbol: "EURGBP"))),
            createdAtUtc: FixedNow());

        var decision = Assert.Single(response.SinglePreviewResponse!.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldDirectCrossNotNetted, decision.LineStatus);
        Assert.False(decision.CreatesOrder);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Legacy_06_rejection_remains_at_preview_boundary()
    {
        var response = _service.RequestSinglePreview(Envelope(
            R009PreviewConsumerType.TestHarness,
            SingleRequest(ValidIntent(
                canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
                canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York"))),
            createdAtUtc: FixedNow());

        var decision = Assert.Single(response.SinglePreviewResponse!.DecisionPreviews);
        Assert.False(decision.PreTradeRiskGate.QuarterHourTargetClose);
        Assert.Contains("CanonicalTargetCloseMustBeQuarterHour", decision.HoldReason);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Usdjpy_caveat_remains_at_preview_boundary()
    {
        var response = _service.RequestSinglePreview(Envelope(
            R009PreviewConsumerType.InternalEmsPreviewConsumer,
            SingleRequest(ValidIntent(
                symbol: "USDJPY",
                executionSymbol: "USDJPY",
                normalizedSymbol: "JPYUSD",
                requiresInversion: true,
                securityId: "4004",
                securityIdSource: "8"))),
            createdAtUtc: FixedNow());

        var decision = Assert.Single(response.SinglePreviewResponse!.DecisionPreviews);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        AssertPreviewConsumerSafe(response);
    }

    [Fact]
    public void Kill_switch_defaults_remain_disabled()
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

    private static R009PreviewConsumerRequestEnvelope Envelope(
        R009PreviewConsumerType consumerType,
        R009DisabledPreviewRequest request,
        params string[] usages)
        => new(
            ConsumerRequestId: $"consumer-{consumerType}-single",
            ConsumerType: consumerType,
            ConsumerName: consumerType.ToString(),
            RequestedUsages: usages.Length == 0 ? new[] { "DisplayToOperator" } : usages,
            SinglePreviewRequest: request,
            BatchPreviewRequest: null);

    private static R009PreviewConsumerRequestEnvelope Envelope(
        R009PreviewConsumerType consumerType,
        R009DisabledPreviewBatchRequest request)
        => new(
            ConsumerRequestId: $"consumer-{consumerType}-batch",
            ConsumerType: consumerType,
            ConsumerName: consumerType.ToString(),
            RequestedUsages: new[] { "DisplayToOperator" },
            SinglePreviewRequest: null,
            BatchPreviewRequest: request);

    private static R009DisabledPreviewRequest SingleRequest(R009EmsOmsExecutionIntent intent)
        => new(
            RequestId: $"request-{intent.ExecutionIntentId}",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            SourceType: R009DisabledPreviewSourceType.ExecutionIntent,
            ExecutionIntent: intent,
            SourceArtifactPath: null,
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

    private static R009DisabledPreviewBatchRequest BatchRequest(params R009DisabledPreviewBatchItem[] items)
        => new(
            BatchRequestId: "boundary-batch",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            Items: items,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            DryRunOnly: true,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            ExecutableScheduleEnabled: false,
            PaperLedgerCommitEnabled: false,
            OperatorApprovalScope: "DesignOnlyPreviewOnly",
            RiskApprovalScope: "DesignOnlyPreviewOnly",
            NoBrokerRoute: true,
            MaxBatchSize: R009DisabledPreviewBatchService.DefaultMaxBatchSize);

    private static R009DisabledPreviewBatchItem BatchItem(string id, R009EmsOmsExecutionIntent intent)
        => new(id, R009DisabledPreviewSourceType.ExecutionIntent, intent, PaperPlanLine: null);

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

    private static DateTimeOffset FixedNow()
        => new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);

    private static void AssertPreviewConsumerSafe(R009PreviewConsumerResponseEnvelope response)
    {
        Assert.True(response.NonExecutable);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NotSubmitted);
        Assert.True(response.NoBrokerRoute);
        Assert.True(response.NoRoute);
        Assert.True(response.NoSubmission);
        Assert.True(response.NoFill);
        Assert.True(response.NoExecutionReport);
        Assert.True(response.NoPaperLedgerCommit);
        Assert.True(response.NoStateMutation);
        Assert.True(response.AuditRecord.NoOrderDomainOutput);
        Assert.True(response.AuditRecord.NoBrokerRoute);
        Assert.True(response.AuditRecord.NoStateMutation);
        Assert.True(response.AuditRecord.DryRunOnly);
        Assert.False(response.UsagePolicy.PreviewOutputIsOrderIntent);
        Assert.False(response.UsagePolicy.PreviewOutputIsRouteable);
        Assert.False(response.UsagePolicy.PreviewOutputIsExecutableSchedule);
        Assert.False(response.UsagePolicy.PreviewOutputIsFillReportInput);
        Assert.True(response.BoundaryGuard.NonExecutable);
        Assert.True(response.BoundaryGuard.NoPaperLedgerCommit);
    }
}
