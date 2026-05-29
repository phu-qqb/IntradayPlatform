using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LedgerStateR003PaperLedgerPreviewFieldBinderTests
{
    [Fact]
    public void KeepsPmsCycleAvailableButUnboundWhenEvidenceDoesNotLinkToSandboxFill()
    {
        var result = BindWithEvidence(new LedgerStateR003FieldEvidence(
            "PmsCycleId",
            "pms-paper-r010-delta-fields-20260525-001",
            "artifacts/readiness/pms-paper/phase-pms-paper-r015-cross-rail-handoff-contract.json",
            "High",
            "pms-handoff.v1",
            LedgerStateR003BindingEvidenceStatus.AvailableNotLineLinked,
            Array.Empty<string>()));

        Assert.Equal(LedgerStateR003PreviewDecision.PaperLedgerPreviewHardenedWithMissingEconomicFields, result.Decision);
        Assert.Null(result.BoundLines.Single().PmsCycleId);
        Assert.Contains("PmsCycleId", result.EconomicFieldGaps.AvailableButNotLineLinkedFields);
        Assert.Contains("MissingPmsCycleId", result.CommitBlockers);
        Assert.False(result.CommitAllowed);
        Assert.False(result.LedgerMutation);
        Assert.False(result.TradingStateMutation);
    }

    [Fact]
    public void BindsOnlyLineLinkedEvidence()
    {
        var result = BindWithEvidence(new LedgerStateR003FieldEvidence(
            "SourceExecutionIntentId",
            "exec-intent-line-1",
            "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r011-sandbox-order-intent.json",
            "High",
            "execution-intent.v1",
            LedgerStateR003BindingEvidenceStatus.Bound,
            ["R007EURUSD2605261515"]));

        var line = result.BoundLines.Single();

        Assert.Equal("exec-intent-line-1", line.SourceExecutionIntentId);
        Assert.Contains(line.FieldBindings, x => x.FieldName == "SourceExecutionIntentId" && x.EvidenceStatus == LedgerStateR003BindingEvidenceStatus.Bound);
        Assert.DoesNotContain("MissingSourceExecutionIntentId", line.MissingFieldBlockers);
        Assert.Contains("SourceExecutionIntentId", result.EconomicFieldGaps.ResolvedFields);
    }

    [Fact]
    public void BlocksUnsafePreviewFlags()
    {
        var binder = new LedgerStateR003PaperLedgerPreviewFieldBinder();
        var request = new LedgerStateR003BinderRequest(
            "ledger-state-r003-test",
            [SampleLine()],
            Array.Empty<LedgerStateR003FieldEvidence>(),
            PreviewOnly: true,
            CommitAllowed: true,
            LedgerMutationAllowed: false,
            TradingStateMutationAllowed: false);

        var result = binder.Bind(request);

        Assert.Equal(LedgerStateR003PreviewDecision.PaperLedgerPreviewBlockedMissingCoreBindings, result.Decision);
        Assert.Empty(result.BoundLines);
        Assert.Contains("CommitAllowedForbidden", result.Diagnostics);
    }

    [Fact]
    public void HardenedContractRemainsPreviewOnlyAndCommitBlocked()
    {
        var result = BindWithEvidence();

        Assert.True(result.HardenedContract.PreviewOnly);
        Assert.False(result.HardenedContract.CommitAllowed);
        Assert.False(result.HardenedContract.LedgerMutation);
        Assert.False(result.HardenedContract.TradingStateMutation);
        Assert.True(result.HardenedContract.MissingEconomicFieldsAllowedForPreviewOnly);
        Assert.True(result.HardenedContract.MissingEconomicFieldsBlockCommit);
        Assert.Contains("MissingAccountId", result.HardenedContract.CommitBlockers);
        Assert.Contains("MissingCommissionFeeModel", result.HardenedContract.CommitBlockers);
    }

    [Fact]
    public void IdempotencyReviewHashIsStableForSameInput()
    {
        var left = BindWithEvidence();
        var right = BindWithEvidence();

        Assert.Equal(left.IdempotencyReviewHash, right.IdempotencyReviewHash);
        Assert.Equal(left.BoundLines.Single().PreviewHash, right.BoundLines.Single().PreviewHash);
        Assert.Equal(left.BoundLines.Single().AuditHash, right.BoundLines.Single().AuditHash);
    }

    private static LedgerStateR003BinderResult BindWithEvidence(params LedgerStateR003FieldEvidence[] evidence)
    {
        var binder = new LedgerStateR003PaperLedgerPreviewFieldBinder();
        var request = new LedgerStateR003BinderRequest(
            "ledger-state-r003-test",
            [SampleLine()],
            evidence,
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutationAllowed: false,
            TradingStateMutationAllowed: false);

        return binder.Bind(request);
    }

    private static LedgerStateR002PaperLedgerPreviewLine SampleLine()
        => new(
            LineId: "EXEC-SANDBOX-R007:R007EURUSD2605261515:1:paper-ledger-preview-line",
            SourcePhase: "EXEC-SANDBOX-R007",
            SourceExecutionReportId: "EXEC-SANDBOX-R007:R007EURUSD2605261515:execution-report",
            SourceFillId: "EXEC-SANDBOX-R007:R007EURUSD2605261515:fill",
            SourceSandboxOrderId: "EXEC-SANDBOX-R007:R007EURUSD2605261515:order",
            ClOrdId: "R007EURUSD2605261515",
            Symbol: "EURUSD",
            ExecutionTradableSymbol: "EURUSD",
            NormalizedPortfolioSymbol: "EURUSD",
            RequiresInversion: false,
            SecurityID: "4001",
            SecurityIDSource: "8",
            Side: "Buy",
            Quantity: 0.1m,
            Price: 1.16343m,
            TimestampUtc: null,
            CanonicalTargetCloseUtc: null,
            SandboxOnly: true,
            ProductionOrder: false,
            PreviewOnly: true,
            CommitAllowed: false,
            NoLedgerCommit: true,
            LedgerMutation: false,
            TradingStateMutation: false,
            MissingFields: ["AccountId", "PortfolioId", "StrategyId", "PmsCycleId", "QubesRunId", "RiskReviewId", "OperatorApprovalId", "CommissionFees", "FxConversion", "CanonicalTargetCloseUtc"]);
}
