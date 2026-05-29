using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009DisabledPreviewBatchServiceTests
{
    private readonly R009DisabledPreviewBatchService _service = new();

    [Fact]
    public void Batch_request_returns_preview_ready_and_held_missing_readiness_items()
    {
        var response = _service.PreviewBatch(BatchRequest(
            Item("ready", ValidIntent()),
            Item("held-readiness", ValidIntent(quoteWindowReadinessId: null, closeBenchmarkReadinessId: null, feedQualityReadinessId: null))),
            FixedNow());

        Assert.Equal("PreviewBatchGenerated", response.BatchStatus);
        Assert.True(response.Validation.IsValid);
        Assert.Equal(1, response.PreviewReadyCount);
        Assert.Equal(1, response.HeldMissingReadinessCount);
        Assert.Equal(0, response.RejectedCount);
        Assert.Equal("PreviewReady", response.ItemResults.Single(x => x.ItemId == "ready").Status);
        Assert.Equal("HeldMissingReadiness", response.ItemResults.Single(x => x.ItemId == "held-readiness").Status);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Batch_schema_rejects_live_broker_order_schedule_and_ledger_flags()
    {
        var response = _service.PreviewBatch(BatchRequest(
            new[] { Item("ready", ValidIntent()) },
            liveTradingEnabled: true,
            brokerRoutingEnabled: true,
            orderSubmissionEnabled: true,
            executableScheduleEnabled: true,
            paperLedgerCommitEnabled: true),
            FixedNow());

        Assert.Equal("Rejected", response.BatchStatus);
        Assert.False(response.Validation.IsValid);
        Assert.Contains("LiveTradingMustRemainDisabled", response.Validation.RejectionReasons);
        Assert.Contains("BrokerRoutingMustRemainDisabled", response.Validation.RejectionReasons);
        Assert.Contains("OrderSubmissionMustRemainDisabled", response.Validation.RejectionReasons);
        Assert.Contains("ExecutableScheduleMustRemainDisabled", response.Validation.RejectionReasons);
        Assert.Contains("PaperLedgerCommitMustRemainDisabled", response.Validation.RejectionReasons);
        Assert.Empty(response.ItemResults);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Batch_schema_enforces_max_batch_size()
    {
        var response = _service.PreviewBatch(BatchRequest(
            new[] { Item("one", ValidIntent()), Item("two", ValidIntent()) },
            maxBatchSize: 1),
            FixedNow());

        Assert.Equal("Rejected", response.BatchStatus);
        Assert.False(response.Validation.IsValid);
        Assert.Contains("MaxBatchSizeExceeded", response.Validation.RejectionReasons);
        Assert.Equal(2, response.RejectedCount);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Direct_cross_item_is_rejected_per_line()
    {
        var response = _service.PreviewBatch(BatchRequest(Item("direct-cross", ValidIntent(
            symbol: "EURGBP",
            executionSymbol: "EURGBP",
            normalizedSymbol: "EURGBP"))), FixedNow());

        var item = Assert.Single(response.ItemResults);
        Assert.Equal("Rejected", item.Status);
        Assert.Contains("DirectCrossExecutionIntentRejected", item.RejectionReasons);
        Assert.Null(item.PreviewResponse);
        AssertBatchPreviewOnly(response);
    }

    [Theory]
    [InlineData("USDCNH")]
    [InlineData("USDSEK")]
    [InlineData("USDZAR")]
    public void Unsupported_nonmajor_em_scandi_cnh_item_is_rejected(string symbol)
    {
        var response = _service.PreviewBatch(BatchRequest(Item("unsupported", ValidIntent(
            symbol: symbol,
            executionSymbol: symbol,
            normalizedSymbol: symbol,
            requiresInversion: true))), FixedNow());

        var item = Assert.Single(response.ItemResults);
        Assert.Equal("Rejected", item.Status);
        Assert.Contains("UnsupportedInstrumentRejected", item.RejectionReasons);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Legacy_06_target_close_item_is_rejected()
    {
        var response = _service.PreviewBatch(BatchRequest(Item("legacy", ValidIntent(
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
            canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York"))), FixedNow());

        var item = Assert.Single(response.ItemResults);
        Assert.Equal("Rejected", item.Status);
        Assert.Contains("CanonicalQuarterHourTargetCloseRequired", item.RejectionReasons);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Usdjpy_item_preserves_inversion_and_security_id_caveat()
    {
        var response = _service.PreviewBatch(BatchRequest(Item("usdjpy", ValidIntent(
            symbol: "USDJPY",
            executionSymbol: "USDJPY",
            normalizedSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8"))), FixedNow());

        var item = Assert.Single(response.ItemResults);
        Assert.Equal("PreviewReady", item.Status);
        var decision = Assert.Single(item.PreviewResponse!.DecisionPreviews);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        AssertBatchPreviewOnly(response);
    }

    [Fact]
    public void Batch_hashes_are_stable_for_same_request()
    {
        var request = BatchRequest(Item("ready", ValidIntent()));

        var first = _service.PreviewBatch(request, FixedNow());
        var second = _service.PreviewBatch(request, FixedNow());

        Assert.Equal(first.IdempotencyHash, second.IdempotencyHash);
        Assert.Equal(first.AuditHash, second.AuditHash);
        Assert.Equal(first.ItemResults.Single().IdempotencyHash, second.ItemResults.Single().IdempotencyHash);
        Assert.Equal(first.ItemResults.Single().AuditHash, second.ItemResults.Single().AuditHash);
    }

    [Fact]
    public void Batch_item_cannot_produce_order_route_fill_schedule_or_ledger_output()
    {
        var response = _service.PreviewBatch(BatchRequest(Item("ready", ValidIntent())), FixedNow());
        var item = Assert.Single(response.ItemResults);
        var decision = Assert.Single(item.PreviewResponse!.DecisionPreviews);

        Assert.False(decision.CreatesOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesRoute);
        Assert.False(decision.CreatesSubmission);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.CreatesExecutableSchedule);
        Assert.True(decision.NoPaperLedgerCommit);
        AssertBatchPreviewOnly(response);
    }

    private static R009DisabledPreviewBatchRequest BatchRequest(params R009DisabledPreviewBatchItem[] items)
        => BatchRequest(items.AsEnumerable());

    private static R009DisabledPreviewBatchRequest BatchRequest(
        IEnumerable<R009DisabledPreviewBatchItem> items,
        bool liveTradingEnabled = false,
        bool brokerRoutingEnabled = false,
        bool orderSubmissionEnabled = false,
        bool executableScheduleEnabled = false,
        bool paperLedgerCommitEnabled = false,
        int maxBatchSize = R009DisabledPreviewBatchService.DefaultMaxBatchSize)
        => new(
            BatchRequestId: "r009-disabled-preview-batch",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            Items: items.ToArray(),
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            DryRunOnly: true,
            LiveTradingEnabled: liveTradingEnabled,
            BrokerRoutingEnabled: brokerRoutingEnabled,
            OrderSubmissionEnabled: orderSubmissionEnabled,
            ExecutableScheduleEnabled: executableScheduleEnabled,
            PaperLedgerCommitEnabled: paperLedgerCommitEnabled,
            OperatorApprovalScope: "DesignOnlyPreviewOnly",
            RiskApprovalScope: "DesignOnlyPreviewOnly",
            NoBrokerRoute: true,
            MaxBatchSize: maxBatchSize);

    private static R009DisabledPreviewBatchItem Item(string id, R009EmsOmsExecutionIntent intent)
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
            ExecutionIntentId: $"intent-{executionSymbol.ToLowerInvariant()}-{canonicalTargetCloseLocal}",
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

    private static void AssertBatchPreviewOnly(R009DisabledPreviewBatchResponse response)
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
        Assert.False(string.IsNullOrWhiteSpace(response.IdempotencyHash));
        Assert.False(string.IsNullOrWhiteSpace(response.AuditHash));
    }
}
