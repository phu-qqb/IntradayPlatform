using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesPriceBasisQuantityTests
{
    private const string MarketDataSnapshotId = "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD";

    [Fact]
    public void R008_quantities_are_derived_from_r001_price_basis_target_notional_and_metadata()
    {
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            CreateR008Transform(),
            new SandboxTargetNotionalSizingPolicy(6_000_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", "RoundDownToNearestMinOrderSizeIncrement_DoNotRoundUpExposure"),
            CreateR008Metadata(),
            CreateR008PriceBasis()));

        Assert.Equal("QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_TARGET_NOTIONAL_AND_METADATA", result.Classification);
        Assert.True(result.ExecutionReadyPreview);
        Assert.Empty(result.Blockers);

        Assert.Equal(48.7m, Assert.Single(result.Lines, line => line.Symbol == "AUDUSD").RoundedQuantity);
        Assert.Equal(7.0m, Assert.Single(result.Lines, line => line.Symbol == "EURUSD").RoundedQuantity);
        Assert.Equal(17.5m, Assert.Single(result.Lines, line => line.Symbol == "GBPUSD").RoundedQuantity);
    }

    [Fact]
    public void R008_rounding_policy_never_rounds_up_exposure()
    {
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            CreateR008Transform(),
            new SandboxTargetNotionalSizingPolicy(6_000_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", "RoundDownToNearestMinOrderSizeIncrement_DoNotRoundUpExposure"),
            CreateR008Metadata(),
            CreateR008PriceBasis()));

        Assert.All(result.Lines, line =>
        {
            Assert.NotNull(line.Quantity);
            Assert.NotNull(line.RoundedQuantity);
            Assert.True(line.RoundedQuantity <= line.Quantity);
            Assert.Equal(0m, line.RoundedQuantity!.Value % 0.1m);
        });
    }

    [Fact]
    public void R008_price_basis_is_bound_to_marketdata_snapshot_only_for_sandbox_preview_sizing()
    {
        var prices = CreateR008PriceBasis();

        Assert.All(prices.Values, priceBasis =>
        {
            Assert.Contains(MarketDataSnapshotId, priceBasis.Source, StringComparison.Ordinal);
            Assert.Contains("NearestBeforeCloseQuoteMid", priceBasis.Source, StringComparison.Ordinal);
            Assert.Contains("SandboxPreviewSizingOnly", priceBasis.Source, StringComparison.Ordinal);
            Assert.DoesNotContain("Production", priceBasis.Source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Accounting", priceBasis.Source, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void R008_does_not_emit_direct_crosses_and_preserves_usdjpy_caveat_without_pricing_it()
    {
        var fullTransform = new SandboxQubesExecutionUniverseTransformer().Transform(CreateFullR005Output());

        Assert.False(fullTransform.DirectCrossExecutionLeakageFound);
        Assert.Equal(["AUDCNH", "CNHSGD", "EURGBP"], fullTransform.DirectCrossSymbolsExcluded);

        var usdJpy = Assert.Single(fullTransform.ExecutionLines, line => line.ExecutionTradableSymbol == "USDJPY");
        Assert.Equal("JPYUSD", usdJpy.NormalizedPortfolioSymbol);
        Assert.True(usdJpy.RequiresInversion);
        Assert.Equal(4004, usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);

        var r008Symbols = CreateR008Transform().ExecutionLines.Select(line => line.ExecutionTradableSymbol).ToArray();
        Assert.Equal(["AUDUSD", "EURUSD", "GBPUSD"], r008Symbols);
    }

    [Fact]
    public void R008_target_notional_is_not_account_currency_aum_nav_or_production_capital()
    {
        var policy = new SandboxTargetNotionalPolicyEvidence(
            PolicyId: "pms-qubes-sandbox-target-notional-r007:operator-policy:20251217T020000Z:001",
            PolicySource: "OperatorProvidedInChat",
            TargetNotionalAmount: 6_000_000m,
            TargetNotionalCurrency: "USD",
            TargetNotionalScope: "SandboxPreviewSizingOnly",
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotAccountCurrency: true,
            NotAumAccounting: true,
            NotNav: true,
            NotLedgerCapital: true);

        var validation = SandboxTargetNotionalPolicyValidator.Validate(policy);

        Assert.Equal("SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", validation.Classification);
        Assert.True(policy.NotAccountCurrency);
        Assert.True(policy.NotAumAccounting);
        Assert.True(policy.NotNav);
        Assert.True(policy.NotLedgerCapital);
    }

    private static SandboxQubesOutput CreateFullR005Output()
    {
        return new SandboxQubesOutput(
            SandboxQubesRunId: "sandbox-qubes-prototype-r005-20251217T020000Z-001",
            QubesOutputId: "qubes-operationalization-r005:prototype-output:20251217T020000Z:001",
            InputSnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            MarketDataSnapshotId: null,
            CanonicalTargetCloseUtc: new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            Weights:
            [
                new SandboxQubesOutputWeight("AUDCNH", -0.036436m),
                new SandboxQubesOutputWeight("AUDUSD", -0.053856m),
                new SandboxQubesOutputWeight("CNHSGD", 0.336970m),
                new SandboxQubesOutputWeight("EURGBP", 0.094338m),
                new SandboxQubesOutputWeight("EURUSD", -0.013900m),
                new SandboxQubesOutputWeight("GBPUSD", 0.039348m),
                new SandboxQubesOutputWeight("JPYUSD", 0.001663m),
            ],
            WeightUnits: "PrototypeSignalWeight",
            DirectCrossesPresent: true,
            DirectCrossPolicy: "DirectCrossSignalOnlyNettingFirstExecutionDisabled",
            RunnerType: "SandboxQubesPrototype",
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true);
    }

    private static SandboxQubesExecutionTransformResult CreateR008Transform()
    {
        var executionLines = new[]
        {
            new SandboxQubesExecutionTransformLine("AUDUSD", "AUDUSD", "AUDUSD", "SELL", -0.053856m, false, null, null),
            new SandboxQubesExecutionTransformLine("EURUSD", "EURUSD", "EURUSD", "SELL", -0.013900m, false, null, null),
            new SandboxQubesExecutionTransformLine("GBPUSD", "GBPUSD", "GBPUSD", "BUY", 0.039348m, false, null, null),
        };

        return new SandboxQubesExecutionTransformResult(
            "DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY",
            DirectCrossesPresent: true,
            DirectCrossExecutionLeakageFound: false,
            DirectCrossSymbolsExcluded: ["AUDCNH", "CNHSGD", "EURGBP"],
            ExecutionLines: executionLines);
    }

    private static IReadOnlyDictionary<string, SandboxInstrumentMetadata> CreateR008Metadata()
    {
        return new Dictionary<string, SandboxInstrumentMetadata>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = new("AUDUSD", 10_000m, 0.1m, "USD"),
            ["EURUSD"] = new("EURUSD", 10_000m, 0.1m, "USD"),
            ["GBPUSD"] = new("GBPUSD", 10_000m, 0.1m, "USD"),
        };
    }

    private static IReadOnlyDictionary<string, SandboxPriceBasis> CreateR008PriceBasis()
    {
        const string source = $"{MarketDataSnapshotId}|NearestBeforeCloseQuoteMid|SandboxPreviewSizingOnly";

        return new Dictionary<string, SandboxPriceBasis>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = new("AUDUSD", 0.6632m, source),
            ["EURUSD"] = new("EURUSD", 1.174725m, source),
            ["GBPUSD"] = new("GBPUSD", 1.342475m, source),
        };
    }
}
