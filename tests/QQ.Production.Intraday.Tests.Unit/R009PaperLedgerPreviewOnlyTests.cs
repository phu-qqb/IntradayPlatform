using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009PaperLedgerPreviewOnlyTests
{
    private readonly R009DisabledEmsOmsExecutionAdapter _adapter = new();
    private readonly R009PaperLedgerPreviewService _service = new();

    [Fact]
    public void Preview_ready_decision_creates_artifact_only_paper_ledger_preview()
    {
        var context = CreateContext();
        var decision = Decision(ValidIntent());
        var response = _service.Preview(Request(), new[] { decision }, FixedNow());
        var envelope = _service.CreateEnvelope(Request(), response, FixedNow());

        var persisted = new R009PaperLedgerPreviewArtifactWriter(context.PreviewRoot).Persist(envelope);

        Assert.True(response.Accepted);
        Assert.Equal(R009PaperLedgerPreviewStatus.PaperLedgerPreviewReady, response.PreviewStatus);
        Assert.Single(response.PreviewLines);
        Assert.Single(response.HypotheticalPositionDeltas);
        Assert.Single(response.HypotheticalCashImpacts);
        AssertPreviewOnly(response);
        Assert.Equal("Persisted", persisted.Status);
        Assert.True(persisted.Persisted);
        Assert.Contains("paper-ledger-preview", persisted.ArtifactPath);
        Assert.True(File.Exists(persisted.ArtifactPath));
    }

    [Fact]
    public void Held_missing_readiness_creates_held_preview_not_ledger_commit()
    {
        var decision = Decision(ValidIntent(quoteWindowReadinessId: null, closeBenchmarkReadinessId: null, feedQualityReadinessId: null));
        var response = _service.Preview(Request(), new[] { decision }, FixedNow());

        var line = Assert.Single(response.PreviewLines);
        Assert.True(response.Accepted);
        Assert.Equal(R009PaperLedgerPreviewStatus.HeldLedgerPreview, response.PreviewStatus);
        Assert.Equal(R009PaperLedgerPreviewStatus.HeldLedgerPreview, line.Status);
        Assert.Contains("MissingQuoteWindowReadiness", line.HoldReason);
        Assert.Empty(response.HypotheticalPositionDeltas);
        Assert.Empty(response.HypotheticalCashImpacts);
        AssertPreviewOnly(response);
    }

    [Fact]
    public void Rejected_decision_creates_rejected_preview_not_ledger_commit()
    {
        var decision = Decision(ValidIntent(
            symbol: "EURGBP",
            executionSymbol: "EURGBP",
            normalizedSymbol: "EURGBP"));
        var response = _service.Preview(Request(), new[] { decision }, FixedNow());

        var line = Assert.Single(response.PreviewLines);
        Assert.True(response.Accepted);
        Assert.Equal(R009PaperLedgerPreviewStatus.RejectedLedgerPreview, response.PreviewStatus);
        Assert.Equal(R009PaperLedgerPreviewStatus.RejectedLedgerPreview, line.Status);
        Assert.Contains("DirectCrossMustBeNettedBeforeExecutionIntent", line.RejectionReason);
        Assert.Empty(response.HypotheticalPositionDeltas);
        AssertPreviewOnly(response);
    }

    [Theory]
    [InlineData("PaperLedgerCommitEnabled")]
    [InlineData("LedgerMutationAllowed")]
    [InlineData("TradingStateMutationAllowed")]
    [InlineData("OrderDomainInputAllowed")]
    public void Unsafe_request_flags_are_rejected(string unsafeFlag)
    {
        var request = unsafeFlag switch
        {
            "PaperLedgerCommitEnabled" => Request(paperLedgerCommitEnabled: true),
            "LedgerMutationAllowed" => Request(ledgerMutationAllowed: true),
            "TradingStateMutationAllowed" => Request(tradingStateMutationAllowed: true),
            "OrderDomainInputAllowed" => Request(orderDomainInputAllowed: true),
            _ => Request()
        };

        var response = _service.Preview(request, new[] { Decision(ValidIntent()) }, FixedNow());

        Assert.False(response.Accepted);
        Assert.Equal(R009PaperLedgerPreviewStatus.RejectedLedgerPreview, response.PreviewStatus);
        Assert.Empty(response.PreviewLines);
        AssertPreviewOnly(response);
    }

    [Fact]
    public void Artifact_writer_writes_only_under_allowed_preview_path()
    {
        var unsafeRoot = Path.Combine(Path.GetTempPath(), $"r009-r012-unsafe-{Guid.NewGuid():N}");
        var response = _service.Preview(Request(), new[] { Decision(ValidIntent()) }, FixedNow());
        var envelope = _service.CreateEnvelope(Request(), response, FixedNow());

        var result = new R009PaperLedgerPreviewArtifactWriter(unsafeRoot).Persist(envelope);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("PaperLedgerPreviewArtifactPathRequired", result.Reasons);
        Assert.False(result.Persisted);
    }

    [Fact]
    public void Same_request_replay_is_idempotent()
    {
        var writer = new R009PaperLedgerPreviewArtifactWriter(CreateContext().PreviewRoot);
        var response = _service.Preview(Request(), new[] { Decision(ValidIntent()) }, FixedNow());
        var envelope = _service.CreateEnvelope(Request(), response, FixedNow());

        var first = writer.Persist(envelope);
        var second = writer.Persist(envelope);

        Assert.Equal("Persisted", first.Status);
        Assert.Equal("ReplaySafe", second.Status);
        Assert.True(second.ReplaySafe);
        Assert.False(second.Persisted);
        Assert.Equal(first.AuditHash, second.AuditHash);
    }

    [Fact]
    public void Same_request_id_different_input_is_conflict()
    {
        var writer = new R009PaperLedgerPreviewArtifactWriter(CreateContext().PreviewRoot);
        var firstRequest = Request();
        var secondRequest = Request(sourceAuditRecordId: "different-source-audit");
        var firstEnvelope = _service.CreateEnvelope(firstRequest, _service.Preview(firstRequest, new[] { Decision(ValidIntent()) }, FixedNow()), FixedNow());
        var secondEnvelope = _service.CreateEnvelope(secondRequest, _service.Preview(secondRequest, new[] { Decision(ValidIntent(symbol: "EURUSD", executionSymbol: "EURUSD", normalizedSymbol: "EURUSD")) }, FixedNow()), FixedNow());

        var first = writer.Persist(firstEnvelope);
        var second = writer.Persist(secondEnvelope);

        Assert.Equal("Persisted", first.Status);
        Assert.Equal("Conflict", second.Status);
        Assert.True(second.Conflict);
        Assert.Contains("SameRequestIdDifferentInputHash", second.Reasons);
    }

    [Fact]
    public void Artifact_writer_contract_has_no_db_ledger_order_route_fill_or_state_paths()
    {
        var contract = new R009PaperLedgerPreviewArtifactWriter(CreateContext().PreviewRoot).Contract;

        Assert.True(contract.ArtifactOnly);
        Assert.False(contract.DbRequired);
        Assert.False(contract.PaperLedgerTableWritesAllowed);
        Assert.False(contract.OrderDomainPersistenceAllowed);
        Assert.False(contract.RouteSubmissionPersistenceAllowed);
        Assert.False(contract.FillReportPersistenceAllowed);
        Assert.False(contract.TradingStateMutationAllowed);
    }

    [Fact]
    public void Usdjpy_caveat_is_preserved_in_preview_line()
    {
        var decision = Decision(ValidIntent(
            symbol: "USDJPY",
            executionSymbol: "USDJPY",
            normalizedSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8"));

        var response = _service.Preview(Request(), new[] { decision }, FixedNow());

        var line = Assert.Single(response.PreviewLines);
        Assert.Equal("USDJPY", line.ExecutionTradableSymbol);
        Assert.Equal("JPYUSD", line.NormalizedPortfolioSymbol);
        Assert.True(line.RequiresInversion);
        AssertPreviewOnly(response);
    }

    [Fact]
    public void Legacy_06_and_direct_cross_inputs_remain_rejected_preview_lines()
    {
        var legacy = Decision(ValidIntent(
            symbol: "NZDUSD",
            executionSymbol: "NZDUSD",
            normalizedSymbol: "NZDUSD",
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 25, 19, 6, 0, TimeSpan.Zero),
            canonicalTargetCloseLocal: "2026-05-25T15:06:00 America/New_York"));
        var directCross = Decision(ValidIntent(
            symbol: "EURGBP",
            executionSymbol: "EURGBP",
            normalizedSymbol: "EURGBP"));

        var response = _service.Preview(Request(), new[] { legacy, directCross }, FixedNow());

        Assert.True(response.Accepted);
        Assert.Equal(R009PaperLedgerPreviewStatus.RejectedLedgerPreview, response.PreviewStatus);
        Assert.Equal(2, response.PreviewLines.Count(x => x.Status == R009PaperLedgerPreviewStatus.RejectedLedgerPreview));
        Assert.Contains(response.PreviewLines, x => x.RejectionReason is not null && x.RejectionReason.Contains("CanonicalTargetCloseMustBeQuarterHour", StringComparison.Ordinal));
        Assert.Contains(response.PreviewLines, x => x.RejectionReason is not null && x.RejectionReason.Contains("DirectCrossMustBeNettedBeforeExecutionIntent", StringComparison.Ordinal));
        AssertPreviewOnly(response);
    }

    private R009DisabledExecutionDecision Decision(R009EmsOmsExecutionIntent intent)
        => _adapter.Decide(intent, R009LiveFeatureFlags.DisabledDefaults, R009DisabledBoundaryGuard.Disabled, FixedNow());

    private static R009PaperLedgerPreviewRequest Request(
        string requestId = "r012-paper-ledger-preview-request",
        string sourceAuditRecordId = "r010-source-audit",
        bool paperLedgerCommitEnabled = false,
        bool ledgerMutationAllowed = false,
        bool tradingStateMutationAllowed = false,
        bool orderDomainInputAllowed = false)
        => new(
            RequestId: requestId,
            SourceDecisionPreviewId: "r010-source-decision-preview",
            SourceAuditRecordId: sourceAuditRecordId,
            SourceConsumerType: R009PreviewConsumerType.OperatorReviewTool,
            R009ContractVersion: R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            PreviewOnly: true,
            PaperLedgerPreviewEnabled: true,
            PaperLedgerCommitEnabled: paperLedgerCommitEnabled,
            LedgerMutationAllowed: ledgerMutationAllowed,
            TradingStateMutationAllowed: tradingStateMutationAllowed,
            OrderDomainInputAllowed: orderDomainInputAllowed,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
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
            ExecutionIntentId: $"intent-{executionSymbol.ToLowerInvariant()}-{Guid.NewGuid():N}",
            SourcePmsCycleId: "exec-live-r012-source-paper-plan-artifact",
            SourceQubesRunId: "exec-live-r012-qubes-reference",
            SourceRebalanceIntentId: "exec-live-r012-rebalance-reference",
            SourceRiskReviewId: "exec-live-r012-risk-design-only",
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

    private static PreviewContext CreateContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"r009-r012-preview-{Guid.NewGuid():N}");
        return new PreviewContext(Path.Combine(root, "artifacts", "readiness", "execution-live", "paper-ledger-preview"));
    }

    private static DateTimeOffset FixedNow()
        => new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);

    private static void AssertPreviewOnly(R009PaperLedgerPreviewResponse response)
    {
        Assert.True(response.PreviewOnly);
        Assert.False(response.PaperLedgerCommit);
        Assert.False(response.LedgerMutation);
        Assert.False(response.TradingStateMutation);
        Assert.True(response.NonExecutable);
        Assert.True(response.NotAnOrder);
        Assert.True(response.NotSubmitted);
        Assert.True(response.NoBrokerRoute);
        Assert.True(response.NoFill);
        Assert.True(response.NoExecutionReport);
        Assert.True(response.NoRoute);
        Assert.True(response.NoSubmission);
        Assert.False(string.IsNullOrWhiteSpace(response.InputHash));
        Assert.False(string.IsNullOrWhiteSpace(response.PreviewHash));
        Assert.False(string.IsNullOrWhiteSpace(response.AuditHash));
    }

    private sealed record PreviewContext(string PreviewRoot);
}
