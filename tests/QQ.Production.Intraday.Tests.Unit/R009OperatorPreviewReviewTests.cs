using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009OperatorPreviewReviewTests
{
    private readonly R009PreviewConsumerBoundaryService _boundary = new();
    private readonly R009OperatorPreviewReviewService _review = new();

    [Fact]
    public void Operator_review_can_list_audit_records_from_artifact_path()
    {
        var context = CreateAuditContext();
        PersistSingle(context);
        PersistBatch(context);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ListAuditRecords));

        Assert.True(response.Accepted);
        Assert.Equal(2, response.AuditRecords.Count);
        Assert.True(response.NonExecutable);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NoBrokerRoute);
        Assert.True(response.ReviewOnly);
        Assert.False(response.ExecutableApproval);
    }

    [Fact]
    public void Operator_review_can_summarize_batch()
    {
        var context = CreateAuditContext();
        PersistBatch(context);

        var response = _review.Review(ReviewRequest(
            context,
            R009OperatorPreviewReviewMode.SummarizeBatch,
            batchRequestId: "operator-review-batch"));

        Assert.True(response.Accepted);
        Assert.Equal(1, response.Summary.BatchPreviewAuditCount);
        Assert.Equal(1, response.Summary.PreviewReadyCount);
        Assert.Equal(1, response.Summary.HeldMissingReadinessCount);
        Assert.Equal(1, response.Summary.RejectedCount);
        Assert.Contains(response.Summary.HeldReasons, x => x.Reason == "HeldMissingReadiness" && x.HeldNotOrder);
        Assert.Contains(response.Summary.RejectedReasons, x => x.Reason == "Rejected" && x.RejectedNotOrder);
    }

    [Fact]
    public void Held_readiness_and_rejected_direct_cross_are_review_only_not_orders()
    {
        var context = CreateAuditContext();
        PersistBatch(context);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.SummarizeBatch));

        Assert.True(response.Accepted);
        Assert.Equal(1, response.Summary.HeldMissingReadinessCount);
        Assert.Equal(1, response.Summary.RejectedCount);
        Assert.True(response.NotAnOrder);
        Assert.False(response.ExecutableApproval);
        Assert.False(response.BrokerApproval);
        Assert.False(response.LiveApproval);
    }

    [Theory]
    [InlineData(R009OperatorPreviewReviewMode.Execute)]
    [InlineData(R009OperatorPreviewReviewMode.Submit)]
    [InlineData(R009OperatorPreviewReviewMode.Route)]
    [InlineData(R009OperatorPreviewReviewMode.CommitLedger)]
    [InlineData(R009OperatorPreviewReviewMode.StartScheduler)]
    public void Forbidden_command_modes_are_rejected(R009OperatorPreviewReviewMode mode)
    {
        var context = CreateAuditContext();
        PersistSingle(context);

        var response = _review.Review(ReviewRequest(context, mode));

        Assert.False(response.Accepted);
        Assert.Contains($"ForbiddenCommandMode:{mode}", response.RejectionReasons);
        Assert.True(response.NonExecutable);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NoBrokerRoute);
    }

    [Fact]
    public void Forbidden_consumer_broker_gateway_is_rejected()
    {
        var context = CreateAuditContext();
        PersistSingle(context);

        var response = _review.Review(ReviewRequest(
            context,
            R009OperatorPreviewReviewMode.ListAuditRecords,
            consumerType: R009PreviewConsumerType.BrokerGateway));

        Assert.False(response.Accepted);
        Assert.Contains("ForbiddenConsumer:BrokerGateway", response.RejectionReasons);
    }

    [Fact]
    public void Operator_review_output_cannot_be_order_route_schedule_or_ledger()
    {
        var context = CreateAuditContext();
        PersistBatch(context);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ListAuditRecords));

        Assert.True(response.Accepted);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NoRoute);
        Assert.True(response.NoSubmission);
        Assert.True(response.NoFill);
        Assert.True(response.NoExecutionReport);
        Assert.True(response.NoPaperLedgerCommit);
        Assert.False(response.ExecutableApproval);
    }

    [Fact]
    public void Export_operator_report_writes_only_allowed_artifact_path()
    {
        var context = CreateAuditContext();
        PersistBatch(context);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ExportOperatorReport));

        Assert.True(response.Accepted);
        Assert.NotNull(response.Export);
        Assert.True(response.Export.Written);
        Assert.Contains("artifacts", response.Export.ArtifactPath);
        Assert.Contains("operator-review", response.Export.ArtifactPath);
        Assert.True(File.Exists(response.Export.ArtifactPath));
    }

    [Fact]
    public void Review_rejects_paths_outside_allowed_artifacts()
    {
        var context = CreateAuditContext();
        var unsafeRequest = ReviewRequest(context, R009OperatorPreviewReviewMode.ListAuditRecords) with
        {
            AuditRootPath = Path.Combine(Path.GetTempPath(), $"r009-unsafe-{Guid.NewGuid():N}"),
            OutputRootPath = Path.Combine(Path.GetTempPath(), $"r009-unsafe-output-{Guid.NewGuid():N}")
        };

        var response = _review.Review(unsafeRequest);

        Assert.False(response.Accepted);
        Assert.Contains("AuditReadPathMustBeArtifactsReadinessExecutionLiveAudit", response.RejectionReasons);
        Assert.Contains("ReviewWritePathMustBeArtifactsReadinessExecutionLiveOperatorReview", response.RejectionReasons);
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

    [Fact]
    public void Legacy_06_is_not_accepted_as_future_canonical_in_reviewed_preview()
    {
        var context = CreateAuditContext();
        var envelope = _boundary.RequestSinglePreview(
            SingleEnvelope(SingleRequest(ValidIntent(
                canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
                canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York"))),
            createdAtUtc: FixedNow());
        var persisted = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ShowAuditRecord, requestId: "operator-single-request"));

        Assert.Equal("Persisted", persisted.Status);
        Assert.True(response.Accepted);
        Assert.NotEqual("PreviewReady", response.SelectedAuditRecord!.ResponseAudit!.DecisionStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved_in_reviewed_preview()
    {
        var context = CreateAuditContext();
        var envelope = _boundary.RequestSinglePreview(
            SingleEnvelope(SingleRequest(ValidIntent(
                symbol: "USDJPY",
                executionSymbol: "USDJPY",
                normalizedSymbol: "JPYUSD",
                requiresInversion: true,
                securityId: "4004",
                securityIdSource: "8"))),
            createdAtUtc: FixedNow());
        var persisted = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);

        var response = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ShowAuditRecord, requestId: "operator-single-request"));

        Assert.Equal("Persisted", persisted.Status);
        Assert.True(response.Accepted);
        Assert.Equal("PreviewGenerated", response.SelectedAuditRecord!.ResponseAudit!.DecisionStatus);
    }

    private static AuditContext CreateAuditContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"r009-operator-review-{Guid.NewGuid():N}");
        return new AuditContext(
            Path.Combine(root, "artifacts", "readiness", "execution-live", "audit"),
            Path.Combine(root, "artifacts", "readiness", "execution-live", "operator-review"));
    }

    private void PersistSingle(AuditContext context)
    {
        var envelope = _boundary.RequestSinglePreview(SingleEnvelope(SingleRequest(ValidIntent())), createdAtUtc: FixedNow());
        var result = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);
        Assert.Equal("Persisted", result.Status);
    }

    private void PersistBatch(AuditContext context)
    {
        var batch = BatchRequest(
            BatchItem("ready", ValidIntent()),
            BatchItem("held", ValidIntent(quoteWindowReadinessId: null)),
            BatchItem("direct-cross", ValidIntent(symbol: "EURGBP", executionSymbol: "EURGBP", normalizedSymbol: "EURGBP")));
        var envelope = _boundary.RequestBatchPreview(BatchEnvelope(batch), FixedNow());
        var result = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);
        Assert.Equal("Persisted", result.Status);
    }

    private static R009OperatorPreviewReviewRequest ReviewRequest(
        AuditContext context,
        R009OperatorPreviewReviewMode mode,
        string? requestId = null,
        string? batchRequestId = null,
        R009PreviewConsumerType consumerType = R009PreviewConsumerType.OperatorReviewTool)
        => new(
            ReviewRequestId: "operator-review-request",
            CommandMode: mode,
            ConsumerType: consumerType,
            RequestId: requestId,
            BatchRequestId: batchRequestId,
            AuditRootPath: context.AuditRoot,
            OutputRootPath: context.OutputRoot);

    private static R009PreviewConsumerRequestEnvelope SingleEnvelope(R009DisabledPreviewRequest request)
        => new(
            ConsumerRequestId: "operator-single-request",
            ConsumerType: R009PreviewConsumerType.InternalEmsPreviewConsumer,
            ConsumerName: "InternalEmsPreviewConsumer",
            RequestedUsages: new[] { "PersistAsReadinessArtifact" },
            SinglePreviewRequest: request,
            BatchPreviewRequest: null);

    private static R009PreviewConsumerRequestEnvelope BatchEnvelope(R009DisabledPreviewBatchRequest request)
        => new(
            ConsumerRequestId: "operator-batch-request",
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
            BatchRequestId: "operator-review-batch",
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
            ExecutionIntentId: $"intent-{executionSymbol.ToLowerInvariant()}-{Guid.NewGuid():N}",
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

    private sealed record AuditContext(string AuditRoot, string OutputRoot);
}
