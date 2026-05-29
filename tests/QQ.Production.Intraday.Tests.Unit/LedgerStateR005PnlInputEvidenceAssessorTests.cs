using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LedgerStateR005PnlInputEvidenceAssessorTests
{
    [Fact]
    public void BlocksTheoreticalPnlWhenEconomicInputsRemainMissing()
    {
        var result = Assess();

        Assert.Equal(LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlBlockedMissingEconomicInputs, result.ReadinessStatus);
        Assert.Equal(LedgerStateR005Decision.SandboxTheoreticalPnlBlockedMissingInputs, result.Decision);
        Assert.True(result.SandboxPriceDeltaOnlyReady);
        Assert.False(result.FullTheoreticalPnlReady);
        Assert.False(result.ProductionPnlReady);
        Assert.False(result.AccountingPnlReady);
        Assert.Contains("MissingMarkPrices", result.RemainingBlockers);
        Assert.Contains("MissingCostSpreadCommissionModel", result.RemainingBlockers);
        Assert.Contains("MissingFxConversion", result.RemainingBlockers);
        Assert.Contains("MissingPositionCostBasisModel", result.RemainingBlockers);
        Assert.Contains("MissingAccountCurrency", result.RemainingBlockers);
        Assert.Contains("MissingAttributionPolicy", result.RemainingBlockers);
    }

    [Fact]
    public void PreservesNoCommitAndNoStateMutationBoundary()
    {
        var result = Assess();

        Assert.False(result.CommitAllowed);
        Assert.False(result.LedgerMutation);
        Assert.False(result.TradingStateMutation);
        Assert.DoesNotContain(result.Diagnostics, x => x.EndsWith("Forbidden", StringComparison.Ordinal));
    }

    [Fact]
    public void KeepsPmsQubesLineageFieldsBlockedRatherThanInvented()
    {
        var result = Assess();

        Assert.Contains("MissingAccountId", result.RemainingBlockers);
        Assert.Contains("MissingPortfolioId", result.RemainingBlockers);
        Assert.Contains("MissingStrategyId", result.RemainingBlockers);
        Assert.Contains("MissingQubesRunId", result.RemainingBlockers);
        Assert.Contains("MissingSourceExecutionIntentId", result.RemainingBlockers);
    }

    [Fact]
    public void FlagsInventedMarketDataOrEconomicInputsAsForbidden()
    {
        var request = Request() with
        {
            MarkPricesInvented = true,
            FxConversionInvented = true,
            MarketDataDbReadinessClaimedComplete = true
        };

        var result = new LedgerStateR005PnlInputEvidenceAssessor().Assess(request);

        Assert.Equal(LedgerStateR005TheoreticalPnlReadinessStatus.InconclusiveSafe, result.ReadinessStatus);
        Assert.Equal(LedgerStateR005Decision.InconclusiveSafe, result.Decision);
        Assert.Contains("MarkPricesInventedForbidden", result.Diagnostics);
        Assert.Contains("FxConversionInventedForbidden", result.Diagnostics);
        Assert.Contains("MarketDataDbReadinessClaimedCompleteForbidden", result.Diagnostics);
    }

    private static LedgerStateR005AssessmentResult Assess()
        => new LedgerStateR005PnlInputEvidenceAssessor().Assess(Request());

    private static LedgerStateR005AssessmentRequest Request()
        => new(
            RequestId: "ledger-state-r005-test",
            EvidenceItems:
            [
                Item("OpenAndFlattenFillPrices", LedgerStateR005EvidenceClassification.Present, "LEDGER-STATE-R004", "Existing sandbox open/flatten fill prices are present for price-delta preview.", null),
                Item("MarkPrices", LedgerStateR005EvidenceClassification.BlockedByMarketData, "SYSTEM-AUDIT-R004 MarketData WARN", "No mark-price evidence is available without live market data or DB row evidence.", "MissingMarkPrices"),
                Item("CostSpreadCommissionModel", LedgerStateR005EvidenceClassification.Missing, "CROSS-RAIL-R010 cost model readiness", "No explicit spread, commission, slippage, or transaction-cost model exists for theoretical PnL.", "MissingCostSpreadCommissionModel"),
                Item("FxConversion", LedgerStateR005EvidenceClassification.Missing, "CROSS-RAIL-R010 FX conversion readiness", "No account currency or conversion-rate evidence exists.", "MissingFxConversion"),
                Item("PositionCostBasis", LedgerStateR005EvidenceClassification.Missing, "CROSS-RAIL-R010 position cost basis readiness", "No FIFO, average-cost, or flat-only cost-basis policy is approved for PnL.", "MissingPositionCostBasisModel"),
                Item("AccountCurrency", LedgerStateR005EvidenceClassification.Missing, "CROSS-RAIL-R010 account currency readiness", "Account currency remains null.", "MissingAccountCurrency"),
                Item("AttributionPolicy", LedgerStateR005EvidenceClassification.RequiresOperatorPolicy, "CROSS-RAIL-R010 attribution readiness", "No strategy/account/PMS/Qubes/execution-intent attribution policy is approved.", "MissingAttributionPolicy"),
                Item("AccountId", LedgerStateR005EvidenceClassification.BlockedByPmsQubes, "LEDGER-STATE-R004 lineage binding matrix", "AccountId remains null.", "MissingAccountId"),
                Item("PortfolioId", LedgerStateR005EvidenceClassification.BlockedByPmsQubes, "LEDGER-STATE-R004 lineage binding matrix", "PortfolioId remains null.", "MissingPortfolioId"),
                Item("StrategyId", LedgerStateR005EvidenceClassification.BlockedByPmsQubes, "LEDGER-STATE-R004 lineage binding matrix", "StrategyId remains null.", "MissingStrategyId"),
                Item("QubesRunId", LedgerStateR005EvidenceClassification.BlockedByPmsQubes, "LEDGER-STATE-R004 lineage binding matrix", "QubesRunId remains null; Qubes 4E is historical only.", "MissingQubesRunId"),
                Item("SourceExecutionIntentId", LedgerStateR005EvidenceClassification.Missing, "LEDGER-STATE-R004 lineage binding matrix", "SourceExecutionIntentId remains null.", "MissingSourceExecutionIntentId")
            ],
            SandboxPriceDeltaOnlyReady: true,
            ProductionPnlAllowed: false,
            AccountingPnlAllowed: false,
            RealPnlComputed: false,
            LedgerCommitAllowed: false,
            LedgerMutationAllowed: false,
            TradingStateMutationAllowed: false,
            MarkPricesInvented: false,
            CostModelInvented: false,
            FxConversionInvented: false,
            AccountCurrencyInvented: false,
            AttributionInvented: false,
            MissingPmsQubesFieldsInvented: false,
            MarketDataDbReadinessClaimedComplete: false);

    private static LedgerStateR005EvidenceItem Item(
        string evidenceName,
        LedgerStateR005EvidenceClassification classification,
        string source,
        string statusReason,
        string? blocker)
        => new(evidenceName, classification, source, statusReason, blocker);
}
