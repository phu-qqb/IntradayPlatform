using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PnlPreviewR001SandboxGrossRoundTripPreviewCalculatorTests
{
    [Fact]
    public void PairsCrossRailR014OpenAndFlattenFills()
    {
        var result = Calculate();

        Assert.Equal(3, result.FillPairs.Count);
        Assert.All(result.FillPairs, pair =>
        {
            Assert.True(pair.Complete);
            Assert.Equal(0m, pair.Residual);
            Assert.Empty(pair.Diagnostics);
            Assert.Equal(pair.OpenQuantity, pair.FlattenQuantity);
        });
    }

    [Fact]
    public void ProducesPriceDeltaOnlyWhenUnitScaleIsMissing()
    {
        var result = Calculate();

        Assert.Equal(PnlPreviewR001Decision.SandboxGrossRoundTripPnlPreviewV0Partial, result.Decision);
        Assert.Contains(result.PerSymbolPreview, x => x.Symbol == "AUDUSD" && x.PriceDelta == -0.00002m);
        Assert.Contains(result.PerSymbolPreview, x => x.Symbol == "EURUSD" && x.PriceDelta == -0.00005m);
        Assert.Contains(result.PerSymbolPreview, x => x.Symbol == "GBPUSD" && x.PriceDelta == -0.00010m);
        Assert.All(result.PerSymbolPreview, row =>
        {
            Assert.Null(row.ContractSizeOrUnitScale);
            Assert.Null(row.GrossRoundTripPnlQuoteCurrency);
            Assert.Contains("UnitScaleMissing", row.Warnings);
            Assert.Equal("USD", row.PnlCurrency);
            Assert.False(row.CostsApplied);
            Assert.False(row.FxConversionApplied);
            Assert.False(row.AccountCurrencyApplied);
        });
        Assert.False(result.AggregatePreview.AggregateAvailable);
        Assert.Contains("UnitScaleMissing", result.AggregatePreview.Diagnostics);
    }

    [Fact]
    public void PreservesGrossOnlyPreviewBoundaries()
    {
        var result = Calculate();

        Assert.False(result.NetPnlReady);
        Assert.False(result.AccountingPnlReady);
        Assert.False(result.ProductionPnlReady);
        Assert.False(result.LedgerCommitReady);
        Assert.False(result.TradingStateMutation);
        Assert.All(result.PerSymbolPreview, row =>
        {
            Assert.True(row.SandboxOnly);
            Assert.True(row.NotProductionPnl);
            Assert.False(row.NetPnlReady);
            Assert.False(row.AccountingPnlReady);
            Assert.False(row.ProductionPnlReady);
            Assert.False(row.LedgerCommitAllowed);
        });
    }

    [Fact]
    public void BlocksUnsafeNetPnlClaim()
    {
        var request = Request() with { NetPnlClaimed = true };

        var result = new PnlPreviewR001SandboxGrossRoundTripPreviewCalculator().Calculate(request);

        Assert.Equal(PnlPreviewR001Decision.InconclusiveSafe, result.Decision);
        Assert.Contains("NetPnlClaimedForbidden", result.Diagnostics);
        Assert.Empty(result.PerSymbolPreview);
    }

    private static PnlPreviewR001Result Calculate()
        => new PnlPreviewR001SandboxGrossRoundTripPreviewCalculator().Calculate(Request());

    private static PnlPreviewR001Request Request()
        => new(
            OpenFills: [Open("AUDUSD", "SELL", 0.71659m), Open("EURUSD", "SELL", 1.16223m), Open("GBPUSD", "BUY", 1.34457m)],
            FlattenFills: [Flatten("AUDUSD", "BUY", 0.71661m), Flatten("EURUSD", "BUY", 1.16228m), Flatten("GBPUSD", "SELL", 1.34447m)],
            ResidualBySymbol: new Dictionary<string, decimal>
            {
                ["AUDUSD"] = 0m,
                ["EURUSD"] = 0m,
                ["GBPUSD"] = 0m
            },
            ContractSizeOrUnitScale: null,
            PreviewOnly: true,
            CostsApplied: false,
            FeesApplied: false,
            CommissionsApplied: false,
            FxConversionApplied: false,
            AccountCurrencyApplied: false,
            LedgerCommitAllowed: false,
            TradingStateMutationAllowed: false,
            NetPnlClaimed: false,
            AccountingPnlClaimed: false,
            ProductionPnlClaimed: false);

    private static PnlPreviewR001Fill Open(string symbol, string side, decimal price)
        => Fill(symbol, side, price, $"source-rebalance:{symbol}");

    private static PnlPreviewR001Fill Flatten(string symbol, string side, decimal price)
        => Fill(symbol, side, price, $"source-rebalance:{symbol}");

    private static PnlPreviewR001Fill Fill(string symbol, string side, decimal price, string sourceRebalanceIntentId)
        => new(
            symbol,
            side,
            Quantity: 0.1m,
            QuantityUnit: "SandboxQuantity",
            Price: price,
            TimestampUtc: DateTimeOffset.Parse("2026-05-26T15:04:43.698+00:00"),
            SourceFillId: $"{symbol}:{side}:fill",
            SourceExecutionReportId: $"{symbol}:{side}:report",
            sourceRebalanceIntentId,
            SandboxOnly: true,
            ProductionFill: false,
            NotProductionPnl: true);
}
