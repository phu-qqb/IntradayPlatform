using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesTargetNotionalPolicyTests
{
    [Fact]
    public void Operator_target_notional_is_accepted_only_as_sandbox_preview_sizing_basis()
    {
        var validation = SandboxTargetNotionalPolicyValidator.Validate(CreatePolicy());

        Assert.Equal("SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", validation.Classification);
        Assert.True(validation.ReadyForSandboxPreviewSizing);
        Assert.Empty(validation.Blockers);
    }

    [Fact]
    public void Operator_target_notional_is_not_account_currency_aum_nav_or_ledger_capital()
    {
        var policy = CreatePolicy();

        Assert.Equal(6_000_000m, policy.TargetNotionalAmount);
        Assert.Equal("USD", policy.TargetNotionalCurrency);
        Assert.Equal("SandboxPreviewSizingOnly", policy.TargetNotionalScope);
        Assert.True(policy.NotAccountCurrency);
        Assert.True(policy.NotAumAccounting);
        Assert.True(policy.NotNav);
        Assert.True(policy.NotLedgerCapital);
        Assert.True(policy.NotProduction);
        Assert.True(policy.NotAccounting);
    }

    [Fact]
    public void Quantities_are_not_derived_when_price_or_mark_basis_is_missing()
    {
        var transform = CreateTransform();
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            transform,
            new SandboxTargetNotionalSizingPolicy(6_000_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", "RoundDownToMinOrderSize"),
            CreateMetadata(),
            new Dictionary<string, SandboxPriceBasis>(StringComparer.Ordinal)));

        Assert.Equal("QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_OR_MARK_SOURCE", result.Classification);
        Assert.False(result.ExecutionReadyPreview);
        Assert.Contains("MissingPriceOrMarkSource:AUDUSD", result.Blockers);
        Assert.Contains("MissingPriceOrMarkSource:EURUSD", result.Blockers);
        Assert.Contains("MissingPriceOrMarkSource:GBPUSD", result.Blockers);
    }

    [Fact]
    public void Quantities_are_derived_only_with_explicit_price_basis_metadata_and_target_notional()
    {
        var transform = CreateTransform();
        var result = new SandboxQubesQuantityTransformer().Transform(new SandboxQuantityTransformRequest(
            transform,
            new SandboxTargetNotionalSizingPolicy(6_000_000m, "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED", "RoundDownToMinOrderSize"),
            CreateMetadata(),
            CreatePrices()));

        Assert.Equal("QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_TARGET_NOTIONAL_AND_METADATA", result.Classification);
        Assert.True(result.ExecutionReadyPreview);
        Assert.Empty(result.Blockers);
        Assert.All(result.Lines, line => Assert.True(line.RoundedQuantity >= 0.1m));
        Assert.All(result.Lines, line => Assert.Equal(0m, line.RoundedQuantity!.Value % 0.1m));
    }

    [Fact]
    public void Direct_cross_policy_and_usdjpy_caveat_are_preserved()
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

    [Fact]
    public void Preview_candidate_does_not_invent_identity_or_execution_flags()
    {
        var output = CreateOutput();
        var candidate = new SandboxQubesPmsIntentCandidateFactory().CreatePreviewOnlyCandidate(
            output,
            CreateTransform(),
            "pms-qubes-sandbox-target-notional-r007-cycle-20251217T020000Z-001",
            "ExistingLmaxDemoProfile",
            "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED_PRICE_BASIS_BLOCKED");

        Assert.False(candidate.ExecutionReady);
        Assert.True(candidate.SandboxOnly);
        Assert.True(candidate.NotProduction);
        Assert.True(candidate.NotAccounting);
        Assert.True(candidate.NotExecuted);
        Assert.True(candidate.NotLedgerCommit);
        Assert.All(candidate.Lines, line => Assert.Null(line.Quantity));
    }

    private static SandboxTargetNotionalPolicyEvidence CreatePolicy()
    {
        return new SandboxTargetNotionalPolicyEvidence(
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
