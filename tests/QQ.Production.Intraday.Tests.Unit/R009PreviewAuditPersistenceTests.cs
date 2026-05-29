using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009PreviewAuditPersistenceTests
{
    private readonly R009PreviewConsumerBoundaryService _boundary = new();

    [Fact]
    public void Audit_record_written_for_valid_single_preview()
    {
        var writer = Writer();
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent())), createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Persisted", result.Status);
        Assert.True(result.Persisted);
        Assert.False(result.Conflict);
        Assert.NotNull(result.ArtifactPath);
        Assert.Contains("artifacts", result.ArtifactPath);
        Assert.True(File.Exists(result.ArtifactPath));
    }

    [Fact]
    public void Audit_record_written_for_valid_batch_preview()
    {
        var writer = Writer();
        var batch = BatchRequest(BatchItem("ready", ValidIntent()), BatchItem("held", ValidIntent(quoteWindowReadinessId: null)));
        var envelope = _boundary.RequestBatchPreview(BatchEnvelope(batch), FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Persisted", result.Status);
        Assert.True(result.Persisted);
        Assert.NotNull(result.ArtifactPath);
        var json = File.ReadAllText(result.ArtifactPath);
        Assert.Contains("\"batchAudit\"", json);
        Assert.Contains("\"retentionCategory\": \"PreviewAuditOnly\"", json);
    }

    [Fact]
    public void Same_request_replay_is_idempotent()
    {
        var writer = Writer();
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent())), createdAtUtc: FixedNow());

        var first = writer.Persist(envelope);
        var second = writer.Persist(envelope);

        Assert.Equal("Persisted", first.Status);
        Assert.Equal("ReplaySafe", second.Status);
        Assert.True(second.ReplaySafe);
        Assert.False(second.Persisted);
        Assert.Equal(first.AuditHash, second.AuditHash);
    }

    [Fact]
    public void Same_request_id_with_different_input_is_conflict()
    {
        var writer = Writer();
        var firstEnvelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent())), createdAtUtc: FixedNow());
        var secondEnvelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent(executionSymbol: "EURUSD", symbol: "EURUSD", normalizedSymbol: "EURUSD"))), createdAtUtc: FixedNow());

        var first = writer.Persist(firstEnvelope);
        var second = writer.Persist(secondEnvelope);

        Assert.Equal("Persisted", first.Status);
        Assert.Equal("Conflict", second.Status);
        Assert.True(second.Conflict);
        Assert.Contains("SameRequestIdDifferentInputHash", second.Reasons);
    }

    [Fact]
    public void Audit_path_must_be_artifact_only()
    {
        var unsafeRoot = Path.Combine(Path.GetTempPath(), $"r009-audit-{Guid.NewGuid():N}");
        var writer = new R009PreviewArtifactAuditWriter(unsafeRoot);
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent())), createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("AuditPathMustBeArtifactsReadinessExecutionLiveAudit", result.Reasons);
        Assert.False(result.Persisted);
    }

    [Fact]
    public void Audit_store_contract_never_allows_order_route_ledger_or_state_persistence()
    {
        var store = Writer().StoreContract;

        Assert.True(store.ArtifactOnly);
        Assert.False(store.DbRequired);
        Assert.False(store.ExternalServiceRequired);
        Assert.False(store.OrderDomainPersistenceAllowed);
        Assert.False(store.RouteSubmissionPersistenceAllowed);
        Assert.False(store.LedgerPersistenceAllowed);
        Assert.False(store.TradingStateMutationAllowed);
    }

    [Fact]
    public void Forbidden_consumer_cannot_persist_audit()
    {
        var writer = Writer();
        var envelope = _boundary.RequestSinglePreview(
            ConsumerEnvelope(SingleRequest(ValidIntent()), R009PreviewConsumerType.BrokerGateway),
            createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("ForbiddenConsumerCannotPersistAudit", result.Reasons);
        Assert.False(result.Persisted);
    }

    [Fact]
    public void Live_broker_order_enabled_request_cannot_persist_audit()
    {
        var writer = Writer();
        var unsafeRequest = new R009DisabledPreviewRequest(
            RequestId: "unsafe-live-request",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            SourceType: R009DisabledPreviewSourceType.ExecutionIntent,
            ExecutionIntent: ValidIntent(),
            SourceArtifactPath: null,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            DryRunOnly: true,
            LiveTradingEnabled: true,
            BrokerRoutingEnabled: true,
            OrderSubmissionEnabled: true,
            ExecutableScheduleEnabled: false,
            PaperLedgerCommitEnabled: false,
            OperatorApprovalScope: "DesignOnlyPreviewOnly",
            RiskApprovalScope: "DesignOnlyPreviewOnly",
            NoBrokerRoute: true);
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(unsafeRequest), createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("UnsafeRequestCannotPersistAudit", result.Reasons);
    }

    [Fact]
    public void Legacy_06_is_rejected_and_not_persisted_as_valid_preview()
    {
        var writer = Writer();
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent(
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
            canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York"))), createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Persisted", result.Status);
        var decision = Assert.Single(envelope.SinglePreviewResponse!.DecisionPreviews);
        Assert.NotEqual(R009LiveLineStatus.PreviewReady, decision.LineStatus);
        Assert.Contains("CanonicalTargetCloseMustBeQuarterHour", decision.HoldReason);
    }

    [Fact]
    public void Direct_cross_is_audited_as_rejection_only()
    {
        var writer = Writer();
        var batch = BatchRequest(BatchItem("direct-cross", ValidIntent(symbol: "EURGBP", executionSymbol: "EURGBP", normalizedSymbol: "EURGBP")));
        var envelope = _boundary.RequestBatchPreview(BatchEnvelope(batch), FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Persisted", result.Status);
        Assert.Equal(1, envelope.BatchPreviewResponse!.RejectedCount);
        Assert.Equal("Rejected", envelope.BatchPreviewResponse.ItemResults.Single().Status);
    }

    [Fact]
    public void Usdjpy_caveat_is_preserved_in_audit()
    {
        var writer = Writer();
        var envelope = _boundary.RequestSinglePreview(ConsumerEnvelope(SingleRequest(ValidIntent(
            symbol: "USDJPY",
            executionSymbol: "USDJPY",
            normalizedSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8"))), createdAtUtc: FixedNow());

        var result = writer.Persist(envelope);

        Assert.Equal("Persisted", result.Status);
        var decision = Assert.Single(envelope.SinglePreviewResponse!.DecisionPreviews);
        Assert.True(decision.PreTradeRiskGate.InversionMetadataValid);
        Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
    }

    private static R009PreviewArtifactAuditWriter Writer()
        => new(Path.Combine(Path.GetTempPath(), $"r009-audit-{Guid.NewGuid():N}", "artifacts", "readiness", "execution-live", "audit"));

    private static R009PreviewConsumerRequestEnvelope ConsumerEnvelope(
        R009DisabledPreviewRequest request,
        R009PreviewConsumerType consumerType = R009PreviewConsumerType.InternalEmsPreviewConsumer)
        => new(
            ConsumerRequestId: "consumer-audit-request",
            ConsumerType: consumerType,
            ConsumerName: consumerType.ToString(),
            RequestedUsages: new[] { "PersistAsReadinessArtifact" },
            SinglePreviewRequest: request,
            BatchPreviewRequest: null);

    private static R009PreviewConsumerRequestEnvelope BatchEnvelope(R009DisabledPreviewBatchRequest request)
        => new(
            ConsumerRequestId: "consumer-audit-batch-request",
            ConsumerType: R009PreviewConsumerType.InternalPmsPreviewConsumer,
            ConsumerName: "InternalPmsPreviewConsumer",
            RequestedUsages: new[] { "PersistAsReadinessArtifact" },
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
            BatchRequestId: "audit-batch",
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
}
