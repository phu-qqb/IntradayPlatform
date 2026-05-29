using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009InternalDisabledPreviewTrialTests
{
    private readonly R009PreviewConsumerBoundaryService _boundary = new();
    private readonly R009OperatorPreviewReviewService _review = new();

    [Fact]
    public void Internal_disabled_preview_trial_accepts_allowed_consumers_and_persists_artifact_audits()
    {
        var context = CreateTrialContext();
        var writer = new R009PreviewArtifactAuditWriter(context.AuditRoot);
        var allowedConsumers = new[]
        {
            R009PreviewConsumerType.InternalPmsPreviewConsumer,
            R009PreviewConsumerType.InternalEmsPreviewConsumer,
            R009PreviewConsumerType.InternalOmsPreviewConsumer,
            R009PreviewConsumerType.OperatorReviewTool
        };

        foreach (var consumer in allowedConsumers)
        {
            var envelope = _boundary.RequestSinglePreview(
                SingleEnvelope(consumer, SingleRequest(ValidIntent(symbol: consumer.ToString()[..6].ToUpperInvariant() == "INTERN" ? "AUDUSD" : "EURUSD"))),
                createdAtUtc: FixedNow());

            Assert.True(envelope.Accepted);
            AssertPreviewEnvelopeSafe(envelope);
            var decision = Assert.Single(envelope.SinglePreviewResponse!.DecisionPreviews);
            Assert.Equal(R009LiveLineStatus.PreviewReady, decision.LineStatus);
            AssertDecisionCannotCreateExecutableArtifact(decision);

            var persisted = writer.Persist(envelope);
            Assert.Equal("Persisted", persisted.Status);
            Assert.True(persisted.Persisted);
            Assert.Contains("artifacts", persisted.ArtifactPath);
            Assert.Contains("execution-live", persisted.ArtifactPath);
            Assert.True(File.Exists(persisted.ArtifactPath));
        }

        var reviewResponse = _review.Review(ReviewRequest(context, R009OperatorPreviewReviewMode.ExportOperatorReport));

        Assert.True(reviewResponse.Accepted);
        Assert.Equal(4, reviewResponse.Summary.AuditRecordCount);
        Assert.NotNull(reviewResponse.Export);
        Assert.True(reviewResponse.Export.Written);
        Assert.Contains("operator-review", reviewResponse.Export.ArtifactPath);
        AssertReviewOutputSafe(reviewResponse);
    }

    [Fact]
    public void Batch_trial_covers_preview_ready_held_readiness_direct_cross_and_legacy_rejections()
    {
        var context = CreateTrialContext();
        var batch = BatchRequest(
            BatchItem("ready-usdjpy", ValidIntent(
                symbol: "USDJPY",
                executionSymbol: "USDJPY",
                normalizedSymbol: "JPYUSD",
                requiresInversion: true,
                securityId: "4004",
                securityIdSource: "8",
                barRole: R009LiveBarRole.OpeningBuild)),
            BatchItem("held-missing-readiness", ValidIntent(
                symbol: "GBPUSD",
                executionSymbol: "GBPUSD",
                normalizedSymbol: "GBPUSD",
                quoteWindowReadinessId: null,
                closeBenchmarkReadinessId: null,
                feedQualityReadinessId: null,
                barRole: R009LiveBarRole.IntradayRebalance)),
            BatchItem("rejected-direct-cross", ValidIntent(
                symbol: "EURGBP",
                executionSymbol: "EURGBP",
                normalizedSymbol: "EURGBP",
                barRole: R009LiveBarRole.ClosingFlatten)),
            BatchItem("rejected-legacy-06", ValidIntent(
                symbol: "NZDUSD",
                executionSymbol: "NZDUSD",
                normalizedSymbol: "NZDUSD",
                canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
                canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York")));

        var envelope = _boundary.RequestBatchPreview(BatchEnvelope(batch), FixedNow());

        Assert.True(envelope.Accepted);
        AssertPreviewEnvelopeSafe(envelope);
        Assert.Equal(1, envelope.BatchPreviewResponse!.PreviewReadyCount);
        Assert.Equal(1, envelope.BatchPreviewResponse.HeldMissingReadinessCount);
        Assert.Equal(2, envelope.BatchPreviewResponse.RejectedCount);
        Assert.Equal("PreviewReady", envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "ready-usdjpy").Status);
        Assert.Equal("HeldMissingReadiness", envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "held-missing-readiness").Status);
        Assert.Contains("DirectCrossExecutionIntentRejected", envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "rejected-direct-cross").RejectionReasons);
        Assert.Contains("CanonicalQuarterHourTargetCloseRequired", envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "rejected-legacy-06").RejectionReasons);

        var usdjpyDecision = Assert.Single(envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "ready-usdjpy").PreviewResponse!.DecisionPreviews);
        Assert.True(usdjpyDecision.PreTradeRiskGate.InversionMetadataValid);
        AssertDecisionCannotCreateExecutableArtifact(usdjpyDecision);
        var heldDecision = Assert.Single(envelope.BatchPreviewResponse.ItemResults.Single(x => x.ItemId == "held-missing-readiness").PreviewResponse!.DecisionPreviews);
        Assert.Equal(R009LiveLineStatus.HeldMissingReadiness, heldDecision.LineStatus);
        AssertDecisionCannotCreateExecutableArtifact(heldDecision);

        var persisted = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);
        Assert.Equal("Persisted", persisted.Status);
        Assert.True(persisted.Persisted);

        var reviewResponse = _review.Review(ReviewRequest(
            context,
            R009OperatorPreviewReviewMode.SummarizeBatch,
            batchRequestId: "r010-internal-trial-batch"));

        Assert.True(reviewResponse.Accepted);
        Assert.Equal(1, reviewResponse.Summary.PreviewReadyCount);
        Assert.Equal(1, reviewResponse.Summary.HeldMissingReadinessCount);
        Assert.Equal(2, reviewResponse.Summary.RejectedCount);
        AssertReviewOutputSafe(reviewResponse);
    }

    [Theory]
    [InlineData(R009PreviewConsumerType.BrokerGateway)]
    [InlineData(R009PreviewConsumerType.OrderRouter)]
    [InlineData(R009PreviewConsumerType.Scheduler)]
    [InlineData(R009PreviewConsumerType.PaperLedgerCommitter)]
    [InlineData(R009PreviewConsumerType.ExecutionReportHandler)]
    [InlineData(R009PreviewConsumerType.ProductionTradingRuntime)]
    public void Forbidden_consumers_are_rejected_and_cannot_persist_trial_audit(R009PreviewConsumerType consumerType)
    {
        var context = CreateTrialContext();
        var envelope = _boundary.RequestSinglePreview(
            SingleEnvelope(consumerType, SingleRequest(ValidIntent())),
            createdAtUtc: FixedNow());

        Assert.False(envelope.Accepted);
        Assert.Contains($"ForbiddenConsumer:{consumerType}", envelope.RejectionReasons);
        Assert.True(envelope.NotAnOrder);
        Assert.True(envelope.NoBrokerRoute);

        var persisted = new R009PreviewArtifactAuditWriter(context.AuditRoot).Persist(envelope);
        Assert.Equal("Rejected", persisted.Status);
        Assert.Contains("ForbiddenConsumerCannotPersistAudit", persisted.Reasons);
        Assert.False(persisted.Persisted);
    }

    [Fact]
    public void Trial_kill_switch_defaults_remain_disabled_and_output_cannot_be_executable()
    {
        var flags = R009LiveFeatureFlags.DisabledDefaults;
        var response = _boundary.RequestSinglePreview(
            SingleEnvelope(R009PreviewConsumerType.InternalEmsPreviewConsumer, SingleRequest(ValidIntent())),
            createdAtUtc: FixedNow());

        Assert.False(flags.R009LiveTradingEnabled);
        Assert.False(flags.R009BrokerRoutingEnabled);
        Assert.False(flags.R009OrderSubmissionEnabled);
        Assert.False(flags.R009ExecutableScheduleEnabled);
        Assert.False(flags.R009PaperLedgerCommitEnabled);
        Assert.False(flags.R009SchedulerEnabled);
        Assert.False(flags.R009BackgroundWorkerEnabled);
        Assert.True(flags.R009DryRunOnly);
        Assert.True(response.Accepted);
        AssertPreviewEnvelopeSafe(response);
        AssertDecisionCannotCreateExecutableArtifact(Assert.Single(response.SinglePreviewResponse!.DecisionPreviews));
    }

    private static TrialContext CreateTrialContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"r009-r010-trial-{Guid.NewGuid():N}");
        return new TrialContext(
            Path.Combine(root, "artifacts", "readiness", "execution-live", "audit"),
            Path.Combine(root, "artifacts", "readiness", "execution-live", "operator-review"));
    }

    private static R009PreviewConsumerRequestEnvelope SingleEnvelope(
        R009PreviewConsumerType consumerType,
        R009DisabledPreviewRequest request)
        => new(
            ConsumerRequestId: $"r010-single-{consumerType}-{Guid.NewGuid():N}",
            ConsumerType: consumerType,
            ConsumerName: consumerType.ToString(),
            RequestedUsages: new[] { "PersistAsReadinessArtifact" },
            SinglePreviewRequest: request,
            BatchPreviewRequest: null);

    private static R009PreviewConsumerRequestEnvelope BatchEnvelope(R009DisabledPreviewBatchRequest request)
        => new(
            ConsumerRequestId: "r010-batch-internal-pms",
            ConsumerType: R009PreviewConsumerType.InternalPmsPreviewConsumer,
            ConsumerName: "InternalPmsPreviewConsumer",
            RequestedUsages: new[] { "PersistAsReadinessArtifact" },
            SinglePreviewRequest: null,
            BatchPreviewRequest: request);

    private static R009DisabledPreviewRequest SingleRequest(R009EmsOmsExecutionIntent intent)
        => new(
            RequestId: $"r010-request-{intent.ExecutionIntentId}",
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
            BatchRequestId: "r010-internal-trial-batch",
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

    private static R009OperatorPreviewReviewRequest ReviewRequest(
        TrialContext context,
        R009OperatorPreviewReviewMode mode,
        string? batchRequestId = null)
        => new(
            ReviewRequestId: "r010-operator-review",
            CommandMode: mode,
            ConsumerType: R009PreviewConsumerType.OperatorReviewTool,
            RequestId: null,
            BatchRequestId: batchRequestId,
            AuditRootPath: context.AuditRoot,
            OutputRootPath: context.OutputRoot);

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
        string? securityIdSource = null,
        R009LiveBarRole barRole = R009LiveBarRole.IntradayRebalance)
        => new(
            ExecutionIntentId: $"intent-{executionSymbol.ToLowerInvariant()}-{Guid.NewGuid():N}",
            SourcePmsCycleId: "exec-live-r010-source-paper-plan-artifact",
            SourceQubesRunId: "exec-live-r010-qubes-reference",
            SourceRebalanceIntentId: "exec-live-r010-rebalance-reference",
            SourceRiskReviewId: "exec-live-r010-risk-design-only",
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
            BarRole: barRole,
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

    private static void AssertPreviewEnvelopeSafe(R009PreviewConsumerResponseEnvelope response)
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
    }

    private static void AssertReviewOutputSafe(R009OperatorPreviewReviewResponse response)
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
        Assert.True(response.ReviewOnly);
        Assert.False(response.ExecutableApproval);
        Assert.False(response.BrokerApproval);
        Assert.False(response.LiveApproval);
    }

    private static void AssertDecisionCannotCreateExecutableArtifact(R009DisabledExecutionDecision decision)
    {
        Assert.True(decision.NonExecutable);
        Assert.True(decision.NotAnOrder);
        Assert.True(decision.NotSubmitted);
        Assert.True(decision.NoBrokerRoute);
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
    }

    private sealed record TrialContext(string AuditRoot, string OutputRoot);
}
