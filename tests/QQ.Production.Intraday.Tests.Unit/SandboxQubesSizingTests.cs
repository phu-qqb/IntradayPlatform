using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesSizingTests
{
    [Fact]
    public void Quantity_transform_refuses_to_invent_target_notional()
    {
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            CreateTransform(),
            new SandboxTargetNotionalSizingPolicy(null, "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL", "RoundDownToMinOrderSize"),
            CreateMetadata(),
            CreatePrices()));

        Assert.Equal("QUANTITY_TRANSFORMATION_BLOCKED_MISSING_TARGET_NOTIONAL", result.Classification);
        Assert.False(result.ExecutionReadyPreview);
        Assert.Contains("MissingExplicitSandboxTargetNotional", result.Blockers);
        Assert.All(result.Lines, line => Assert.Null(line.Quantity));
    }

    [Fact]
    public void Quantity_transform_refuses_execution_ready_when_required_price_source_is_missing()
    {
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            CreateTransform(),
            new SandboxTargetNotionalSizingPolicy(10_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY", "RoundDownToMinOrderSize"),
            CreateMetadata(),
            new Dictionary<string, SandboxPriceBasis>(StringComparer.Ordinal)));

        Assert.Equal("QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_OR_MARK_SOURCE", result.Classification);
        Assert.False(result.ExecutionReadyPreview);
        Assert.Contains("MissingPriceOrMarkSource:AUDUSD", result.Blockers);
        Assert.Contains("MissingPriceOrMarkSource:EURUSD", result.Blockers);
        Assert.Contains("MissingPriceOrMarkSource:GBPUSD", result.Blockers);
    }

    [Fact]
    public void Quantity_transform_uses_explicit_target_notional_price_metadata_min_order_and_rounding()
    {
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            CreateTransform(),
            new SandboxTargetNotionalSizingPolicy(100_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY", "RoundDownToMinOrderSize"),
            CreateMetadata(),
            CreatePrices()));

        Assert.Equal("QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_TARGET_NOTIONAL_AND_METADATA", result.Classification);
        Assert.True(result.ExecutionReadyPreview);
        Assert.Empty(result.Blockers);
        Assert.All(result.Lines, line => Assert.True(line.RoundedQuantity >= 0.1m));
        Assert.All(result.Lines, line => Assert.Equal(0m, line.RoundedQuantity!.Value % 0.1m));
    }

    [Fact]
    public void Preview_candidate_remains_sandbox_only_and_non_executed_without_quantities()
    {
        var output = CreateOutput();
        var transform = CreateTransform();
        var candidate = new SandboxQubesPmsIntentCandidateFactory().CreatePreviewOnlyCandidate(
            output,
            transform,
            "pms-qubes-sandbox-sizing-r006-cycle-20251217T020000Z-001",
            "ExistingLmaxDemoProfile",
            "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL");

        Assert.False(candidate.ExecutionReady);
        Assert.True(candidate.SandboxOnly);
        Assert.True(candidate.NotProduction);
        Assert.True(candidate.NotAccounting);
        Assert.True(candidate.NotExecuted);
        Assert.True(candidate.NotLedgerCommit);
        Assert.All(candidate.Lines, line => Assert.Null(line.Quantity));
    }

    [Fact]
    public void Direct_cross_validation_preserves_usdjpy_caveat_and_rejects_execution_leakage()
    {
        var transform = CreateTransform();

        Assert.False(transform.DirectCrossExecutionLeakageFound);
        Assert.DoesNotContain(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "EURGBP");

        var usdJpy = Assert.Single(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "USDJPY");
        Assert.Equal("JPYUSD", usdJpy.NormalizedPortfolioSymbol);
        Assert.True(usdJpy.RequiresInversion);
        Assert.Equal(4004, usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
    }

    private static SandboxQubesOutput CreateOutput()
    {
        return new SandboxQubesOutput(
            SandboxQubesRunId: "sandbox-qubes-prototype-r005-20251217T020000Z-001",
            QubesOutputId: "qubes-operationalization-r005:prototype-output:20251217T020000Z:001",
            InputSnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            MarketDataSnapshotId: null,
            CanonicalTargetCloseUtc: new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            Weights:
            [
                new SandboxQubesOutputWeight("AUDUSD", -0.053856m),
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

    private static SandboxQubesExecutionTransformResult CreateTransform()
    {
        return new SandboxQubesExecutionUniverseTransformer().Transform(CreateOutput());
    }

    private static IReadOnlyDictionary<string, SandboxInstrumentMetadata> CreateMetadata()
    {
        return new Dictionary<string, SandboxInstrumentMetadata>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = new("AUDUSD", 10_000m, 0.1m, "USD"),
            ["EURUSD"] = new("EURUSD", 10_000m, 0.1m, "USD"),
            ["GBPUSD"] = new("GBPUSD", 10_000m, 0.1m, "USD"),
            ["USDJPY"] = new("USDJPY", 10_000m, 0.1m, "JPY"),
        };
    }

    private static IReadOnlyDictionary<string, SandboxPriceBasis> CreatePrices()
    {
        return new Dictionary<string, SandboxPriceBasis>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = new("AUDUSD", 0.65000m, "ExplicitUnitTestPrice"),
            ["EURUSD"] = new("EURUSD", 1.08000m, "ExplicitUnitTestPrice"),
            ["GBPUSD"] = new("GBPUSD", 1.25000m, "ExplicitUnitTestPrice"),
            ["USDJPY"] = new("USDJPY", 150.00000m, "ExplicitUnitTestPrice"),
        };
    }
}
