using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxAcceptedFillQuantityControlsTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Accepted_fill_review_requires_sandbox_fill_and_no_production_paths()
    {
        var review = _gate.ReviewAcceptedFill(
            "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json",
            "EURUSD",
            requestedQuantity: 0.1m,
            filledQuantity: 0.1m,
            fillPrice: 1.16407m,
            finalOrderStatus: "Filled",
            finalExecType: "Trade",
            sandboxOnly: true,
            productionOrderCreated: false,
            productionRouteCreated: false,
            productionFillOrReportCreated: false,
            productionLedgerMutation: false,
            productionStateMutation: false,
            credentialValuesPersisted: false);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, review.Status);
        Assert.Contains("SandboxOnlyFill", review.Findings);
        Assert.Contains("FinalStatusFilled", review.Findings);
        Assert.False(review.ProductionLedgerMutation);
    }

    [Fact]
    public void Quantity_control_rejects_below_min_quantity()
    {
        var result = _gate.NormalizeSandboxQuantity("EURUSD", 0.01m, Contract());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.Status);
        Assert.True(result.BelowMinRejected);
        Assert.Contains("QuantityBelowMinOrderQuantity", result.Reasons);
    }

    [Fact]
    public void Quantity_control_rejects_non_step_quantity()
    {
        var result = _gate.NormalizeSandboxQuantity("EURUSD", 0.15m, Contract());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.Status);
        Assert.True(result.NonStepQuantityRejected);
        Assert.Contains("QuantityNotAlignedToStep", result.Reasons);
    }

    [Fact]
    public void Quantity_control_rejects_unknown_symbol_rule()
    {
        var result = _gate.NormalizeSandboxQuantity("AUDUSD", 0.1m, Contract());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.Status);
        Assert.True(result.UnknownSymbolQuantityRuleRejected);
        Assert.Contains("UnknownSymbolQuantityRule", result.Reasons);
    }

    [Fact]
    public void Quantity_control_accepts_discovered_eurusd_minimum()
    {
        var result = _gate.NormalizeSandboxQuantity("EURUSD", 0.1m, Contract());

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.Status);
        Assert.Equal(0.1m, result.NormalizedQuantity);
    }

    [Fact]
    public void Market_order_price_control_does_not_request_live_market_data()
    {
        var review = _gate.ReviewMarketability(PriceContract(), "Market", explicitSandboxLimitPriceAvailable: false);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, review.Status);
        Assert.False(review.UsesLiveMarketData);
        Assert.False(review.PriceSensitiveOrderBlocked);
    }

    [Fact]
    public void Limit_order_price_control_requires_explicit_sandbox_price()
    {
        var review = _gate.ReviewMarketability(PriceContract(), "Limit", explicitSandboxLimitPriceAvailable: false);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, review.Status);
        Assert.True(review.PriceSensitiveOrderBlocked);
        Assert.Contains("ExplicitSandboxLimitPriceRequired", review.Reasons);
    }

    private static R009SandboxQuantityControlContract Contract()
        => new(
            MaxSandboxOrderCount: 3,
            MaxOrderQuantityPerSymbol: 0.1m,
            MaxTotalSandboxQuantity: 0.3m,
            RejectBelowMin: true,
            RejectNonStepQuantities: true,
            RejectAboveSandboxCap: true,
            RejectUnknownSymbolQuantityRules: true,
            Rules:
            [
                new R009SandboxSymbolQuantityRule(
                    "EURUSD",
                    MinOrderQuantity: 0.1m,
                    QuantityStep: 0.1m,
                    ContractSize: 10000m,
                    MaxDemoOrderQuantity: 0.1m,
                    QuantityPrecision: 1,
                    SourceEvidencePath: "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547")
            ]);

    private static R009SandboxPriceControlContract PriceContract()
        => new(
            MarketOrdersAllowedForSandboxSmoke: true,
            LimitOrdersRequireExplicitSandboxLimitPrice: true,
            LiveMarketDataRequestAllowed: false,
            ProductionAggressivePricingAllowed: false,
            AllowedMarketOrderReason: "R005 used sandbox market IOC safely without production pricing or live market data request.");
}
