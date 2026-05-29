using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PnlPreviewR002UnitScaleBindingCalculatorTests
{
    [Fact]
    public void ComputesOnlySymbolsWithExplicitUnitScaleEvidence()
    {
        var result = Calculate(Evidence());

        Assert.Equal(PnlPreviewR002Decision.SandboxGrossRoundTripPnlPreviewV0PartialUnitScaleMissing, result.Decision);
        Assert.Contains(result.PerSymbolAmounts, x =>
            x.Symbol == "EURUSD"
            && x.UnitScaleStatus == PnlPreviewR002UnitScaleStatus.UnitScaleBound
            && x.ContractSizeOrUnitScale == 10000m
            && x.GrossRoundTripPnlQuoteCurrency == -0.05m);
        Assert.Contains(result.PerSymbolAmounts, x =>
            x.Symbol == "AUDUSD"
            && x.UnitScaleStatus == PnlPreviewR002UnitScaleStatus.UnitScaleMissing
            && x.GrossRoundTripPnlQuoteCurrency is null);
        Assert.Contains(result.PerSymbolAmounts, x =>
            x.Symbol == "GBPUSD"
            && x.UnitScaleStatus == PnlPreviewR002UnitScaleStatus.UnitScaleMissing
            && x.GrossRoundTripPnlQuoteCurrency is null);
        Assert.False(result.Aggregate.AggregateAvailable);
        Assert.True(result.Aggregate.PartialAggregateOnly);
        Assert.Equal(-0.05m, result.Aggregate.GrossRoundTripPnlQuoteCurrency);
        Assert.Equal(["EURUSD"], result.Aggregate.ComputedSymbols);
        Assert.Equal(["AUDUSD", "GBPUSD"], result.Aggregate.MissingSymbols);
    }

    [Fact]
    public void PreservesGrossQuoteCurrencyOnlyBoundaries()
    {
        var result = Calculate(Evidence());

        Assert.True(result.GrossOnly);
        Assert.True(result.QuoteCurrencyOnly);
        Assert.False(result.CostsApplied);
        Assert.False(result.FxConversionApplied);
        Assert.False(result.AccountCurrencyApplied);
        Assert.False(result.NetPnlReady);
        Assert.False(result.AccountingPnlReady);
        Assert.False(result.ProductionPnlReady);
        Assert.False(result.LedgerCommitReady);
        Assert.False(result.TradingStateMutation);
        Assert.All(result.PerSymbolAmounts, row =>
        {
            Assert.False(row.CostsApplied);
            Assert.False(row.FeesApplied);
            Assert.False(row.CommissionsApplied);
            Assert.False(row.FxConversionApplied);
            Assert.False(row.AccountCurrencyApplied);
            Assert.False(row.NetPnlReady);
            Assert.False(row.AccountingPnlReady);
            Assert.False(row.ProductionPnlReady);
            Assert.False(row.LedgerCommitAllowed);
        });
    }

    [Fact]
    public void BlocksConflictingUnitScaleEvidence()
    {
        var evidence = Evidence();
        evidence["EURUSD"] = evidence["EURUSD"] with
        {
            Status = PnlPreviewR002UnitScaleStatus.UnitScaleConflict,
            Warnings = ["UnitScaleConflict"]
        };

        var result = Calculate(evidence);

        Assert.Equal(PnlPreviewR002Decision.SandboxGrossRoundTripPnlPreviewV0BlockedUnitScaleConflict, result.Decision);
        Assert.Contains(result.PerSymbolAmounts, x => x.Symbol == "EURUSD" && x.GrossRoundTripPnlQuoteCurrency is null);
    }

    private static PnlPreviewR002Result Calculate(Dictionary<string, PnlPreviewR002UnitScaleEvidence> evidence)
        => new PnlPreviewR002UnitScaleBindingCalculator().Calculate(new PnlPreviewR002Request(Request(), evidence));

    private static Dictionary<string, PnlPreviewR002UnitScaleEvidence> Evidence()
        => new()
        {
            ["AUDUSD"] = Missing("AUDUSD"),
            ["EURUSD"] = new PnlPreviewR002UnitScaleEvidence(
                "EURUSD",
                PnlPreviewR002UnitScaleStatus.UnitScaleBound,
                10000m,
                "LMAXVenueOrderQtyContractUnit",
                0.1m,
                0.1m,
                "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r005-local-quantity-rule-discovery.json",
                "ExplicitSymbolLocalRule",
                []),
            ["GBPUSD"] = Missing("GBPUSD")
        };

    private static PnlPreviewR002UnitScaleEvidence Missing(string symbol)
        => new(
            symbol,
            PnlPreviewR002UnitScaleStatus.UnitScaleMissing,
            null,
            "SandboxQuantity",
            null,
            null,
            "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r006-quantity-control-contract.json",
            "SymbolSpecificRuleMissing",
            ["UnitScaleMissing", "NoExplicitSymbolSpecificContractSize"]);

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
