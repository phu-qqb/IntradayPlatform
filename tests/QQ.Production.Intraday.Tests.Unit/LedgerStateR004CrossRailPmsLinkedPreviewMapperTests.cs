using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LedgerStateR004CrossRailPmsLinkedPreviewMapperTests
{
    [Fact]
    public void MapsCrossRailR014FillsToPmsLinkedPreviewLines()
    {
        var result = Map();

        Assert.Equal(LedgerStateR004Decision.CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly, result.Decision);
        Assert.Equal(6, result.PreviewLines.Count);
        Assert.All(result.PreviewLines, line =>
        {
            Assert.True(line.PreviewOnly);
            Assert.False(line.CommitAllowed);
            Assert.True(line.NoLedgerCommit);
            Assert.True(line.NoTradingStateMutation);
            Assert.True(line.SandboxOnly);
            Assert.False(line.ProductionFill);
            Assert.True(line.NotProductionPnl);
            Assert.Equal("pms-paper-r010-delta-fields-20260525-001", line.PmsCycleId);
            Assert.Equal("risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001", line.RiskReviewId);
            Assert.Equal("operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001", line.OperatorApprovalId);
            Assert.False(string.IsNullOrWhiteSpace(line.SourceRebalanceIntentId));
        });
    }

    [Fact]
    public void ProducesSandboxPriceDeltaOnlyWithoutFullPnl()
    {
        var result = Map();

        Assert.Equal(3, result.PriceDeltaPreview.Count);
        Assert.Contains(result.PriceDeltaPreview, x => x.Symbol == "AUDUSD" && x.RawPriceDelta == 0.00002m && x.SideAdjustedPriceDelta == -0.00002m);
        Assert.Contains(result.PriceDeltaPreview, x => x.Symbol == "EURUSD" && x.RawPriceDelta == 0.00005m && x.SideAdjustedPriceDelta == -0.00005m);
        Assert.Contains(result.PriceDeltaPreview, x => x.Symbol == "GBPUSD" && x.RawPriceDelta == -0.00010m && x.SideAdjustedPriceDelta == -0.00010m);
        Assert.All(result.PriceDeltaPreview, x =>
        {
            Assert.True(x.SandboxPriceDeltaOnly);
            Assert.True(x.NotProductionPnl);
            Assert.True(x.NotAccountingPnl);
            Assert.False(x.FullTheoreticalPnlProduced);
        });
    }

    [Fact]
    public void KeepsFullPnlAndCommitBlockedByEconomicGaps()
    {
        var result = Map();

        Assert.False(result.CommitAllowed);
        Assert.False(result.LedgerMutation);
        Assert.False(result.TradingStateMutation);
        Assert.Contains("MissingMarkPrices", result.CommitBlockers);
        Assert.Contains("MissingCostSpreadCommissionModel", result.CommitBlockers);
        Assert.Contains("MissingFxConversion", result.CommitBlockers);
        Assert.Contains("MissingAccountCurrency", result.CommitBlockers);
        Assert.Contains("MissingAttributionPolicy", result.CommitBlockers);
        Assert.Contains("MissingAccountId", result.CommitBlockers);
        Assert.Contains("MissingQubesRunId", result.CommitBlockers);
        Assert.All(result.PnlPreviewInputs, input =>
        {
            Assert.False(input.FullTheoreticalPnlProduced);
            Assert.Contains("MissingFxConversion", input.MissingFullPnlInputs);
        });
    }

    [Fact]
    public void ReconcilesThreeOpenThreeFlattenAndZeroResiduals()
    {
        var result = Map();

        Assert.Equal(3, result.Reconciliation.ExpectedOrders);
        Assert.Equal(3, result.Reconciliation.ActualOrders);
        Assert.Equal(3, result.Reconciliation.ExpectedFills);
        Assert.Equal(3, result.Reconciliation.ActualFills);
        Assert.Equal(3, result.Reconciliation.ExpectedFlattenOrders);
        Assert.Equal(3, result.Reconciliation.ActualFlattenOrders);
        Assert.Equal(3, result.Reconciliation.ExpectedFlattenFills);
        Assert.Equal(3, result.Reconciliation.ActualFlattenFills);
        Assert.True(result.Reconciliation.AllResidualsZero);
        Assert.Empty(result.Reconciliation.Breaks);
    }

    [Fact]
    public void BlocksUnsafeProductionFill()
    {
        var request = Request() with
        {
            OpenFills = [OpenAudusd() with { ProductionFill = true }]
        };

        var result = new LedgerStateR004CrossRailPmsLinkedPreviewMapper().Map(request);

        Assert.Equal(LedgerStateR004Decision.CrossRailPmsLinkedSandboxLedgerPreviewBlockedMissingCoreLineage, result.Decision);
        Assert.Empty(result.PreviewLines);
        Assert.Contains(result.Diagnostics, x => x.StartsWith("ProductionFillForbidden", StringComparison.Ordinal));
    }

    private static LedgerStateR004MapperResult Map()
        => new LedgerStateR004CrossRailPmsLinkedPreviewMapper().Map(Request());

    private static LedgerStateR004MapperRequest Request()
        => new(
            RequestId: "ledger-state-r004-test",
            OpenFills: [OpenAudusd(), OpenEurusd(), OpenGbpusd()],
            FlattenFills: [FlattenAudusd(), FlattenEurusd(), FlattenGbpusd()],
            LineageBindings: [Lineage("AUDUSD"), Lineage("EURUSD"), Lineage("GBPUSD")],
            ExpectedResidualsBySymbol: new Dictionary<string, decimal>
            {
                ["AUDUSD"] = 0m,
                ["EURUSD"] = 0m,
                ["GBPUSD"] = 0m
            },
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutationAllowed: false,
            TradingStateMutationAllowed: false);

    private static LedgerStateR004LineageBinding Lineage(string symbol)
        => new(
            Symbol: symbol,
            PmsCycleId: "pms-paper-r010-delta-fields-20260525-001",
            QubesRunId: null,
            StrategyId: null,
            AccountId: null,
            PortfolioId: null,
            SandboxAccountProfile: "ExistingLmaxDemoProfile",
            RiskReviewId: "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001",
            OperatorApprovalId: "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001",
            SourceRebalanceIntentId: $"pms-paper-r010-delta-fields-20260525-001:oms-intent-preview:{symbol}",
            SourceExecutionIntentId: null,
            CanonicalTargetCloseUtc: DateTimeOffset.Parse("2025-12-17T02:00:00Z"),
            ExecutionTradableSymbol: symbol,
            NormalizedPortfolioSymbol: symbol,
            RequiresInversion: false,
            SecurityIDSource: "8");

    private static LedgerStateR004SandboxFill OpenAudusd()
        => Fill(LedgerStateR004FillRole.Open, "AUDUSD", "SELL", 0.71659m, "AAAEDAAAAABe+6uE", "aJBPhQAAAACaeGWx", "CRR008AUDS260526", "4007", "2026-05-26T15:04:43.698+00:00");

    private static LedgerStateR004SandboxFill OpenEurusd()
        => Fill(LedgerStateR004FillRole.Open, "EURUSD", "SELL", 1.16223m, "AAAESQAAAABe+6ua", "aJBPhQAAAACaeGXJ", "CRR008EURS260526", "4001", "2026-05-26T15:04:45.923+00:00");

    private static LedgerStateR004SandboxFill OpenGbpusd()
        => Fill(LedgerStateR004FillRole.Open, "GBPUSD", "BUY", 1.34457m, "AAAEQQAAAABe+6u6", "aJBPhQAAAACaeGXs", "CRR008GBPB260526", "4002", "2026-05-26T15:04:48.068+00:00");

    private static LedgerStateR004SandboxFill FlattenAudusd()
        => Fill(LedgerStateR004FillRole.Flatten, "AUDUSD", "BUY", 0.71661m, "AAAEDAAAAABe+6uP", "aJBPhQAAAACaeGW9", "CRR008AUDSF260526", "4007", "2026-05-26T15:04:44.821+00:00");

    private static LedgerStateR004SandboxFill FlattenEurusd()
        => Fill(LedgerStateR004FillRole.Flatten, "EURUSD", "BUY", 1.16228m, "AAAESQAAAABe+6ul", "aJBPhQAAAACaeGXV", "CRR008EURSF260526", "4001", "2026-05-26T15:04:46.98+00:00");

    private static LedgerStateR004SandboxFill FlattenGbpusd()
        => Fill(LedgerStateR004FillRole.Flatten, "GBPUSD", "SELL", 1.34447m, "AAAEQQAAAABe+6vG", "aJBPhQAAAACaeGX6", "CRR008GBPBF260526", "4002", "2026-05-26T15:04:49.181+00:00");

    private static LedgerStateR004SandboxFill Fill(
        LedgerStateR004FillRole role,
        string symbol,
        string side,
        decimal price,
        string sandboxOrderId,
        string executionReportId,
        string clientOrderId,
        string securityId,
        string timestamp)
        => new(
            role,
            symbol,
            side,
            Quantity: 0.1m,
            Price: price,
            TimestampUtc: DateTimeOffset.Parse(timestamp),
            sandboxOrderId,
            executionReportId,
            FillId: executionReportId,
            clientOrderId,
            securityId,
            SandboxOnly: true,
            ProductionFill: false,
            NotProductionPnl: true);
}
