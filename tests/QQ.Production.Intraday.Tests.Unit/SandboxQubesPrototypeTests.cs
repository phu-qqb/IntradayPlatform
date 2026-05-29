using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesPrototypeTests
{
    [Fact]
    public void Runner_emits_deterministic_sandbox_output_from_prototype_snapshot()
    {
        var snapshot = CreateSnapshot();
        var runner = new SandboxQubesPrototypeRunner();

        var first = runner.Run(new SandboxQubesPrototypeRunRequest(snapshot, "r005"));
        var second = runner.Run(new SandboxQubesPrototypeRunRequest(snapshot, "r005"));

        Assert.Equal("sandbox-qubes-prototype-r005-20251217T020000Z-001", first.SandboxQubesRunId);
        Assert.Equal(first.SandboxQubesRunId, second.SandboxQubesRunId);
        Assert.Equal(first.QubesOutputId, second.QubesOutputId);
        Assert.Equal(
            first.Weights.Select(weight => (weight.Symbol, weight.Weight)).ToArray(),
            second.Weights.Select(weight => (weight.Symbol, weight.Weight)).ToArray());
        Assert.True(first.SandboxOnly);
        Assert.True(first.NotProduction);
        Assert.True(first.NotAccounting);
        Assert.True(first.NotExecuted);
        Assert.True(first.NotLedgerCommit);
        Assert.Equal("SandboxQubesPrototype", first.RunnerType);
        Assert.True(first.DirectCrossesPresent);
    }

    [Fact]
    public void Transformer_excludes_direct_crosses_and_preserves_usdjpy_caveat()
    {
        var output = new SandboxQubesPrototypeRunner().Run(new SandboxQubesPrototypeRunRequest(CreateSnapshot(), "r005"));

        var transform = new SandboxQubesExecutionUniverseTransformer().Transform(output);

        Assert.Equal("DIRECT_CROSS_POLICY_PRESERVED_BUT_SIZING_MISSING", transform.Classification);
        Assert.True(transform.DirectCrossesPresent);
        Assert.False(transform.DirectCrossExecutionLeakageFound);
        Assert.Contains("EURGBP", transform.DirectCrossSymbolsExcluded);
        Assert.DoesNotContain(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "EURGBP");

        var usdJpy = Assert.Single(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "USDJPY");
        Assert.Equal("JPYUSD", usdJpy.NormalizedPortfolioSymbol);
        Assert.True(usdJpy.RequiresInversion);
        Assert.Equal(4004, usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
    }

    [Fact]
    public void Pms_intent_candidate_is_preview_only_when_quantities_missing()
    {
        var output = new SandboxQubesPrototypeRunner().Run(new SandboxQubesPrototypeRunRequest(CreateSnapshot(), "r005"));
        var transform = new SandboxQubesExecutionUniverseTransformer().Transform(output);

        var candidate = new SandboxQubesPmsIntentCandidateFactory().CreatePreviewOnlyCandidate(
            output,
            transform,
            "qubes-operationalization-r005-cycle-20251217T020000Z-001",
            "ExistingLmaxDemoProfile",
            "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL");

        Assert.Equal("PMS_REBALANCE_INTENT_CANDIDATE_PREVIEW_ONLY_QUANTITIES_MISSING", candidate.CandidateStatus);
        Assert.False(candidate.ExecutionReady);
        Assert.True(candidate.SandboxOnly);
        Assert.True(candidate.NotProduction);
        Assert.True(candidate.NotAccounting);
        Assert.True(candidate.NotExecuted);
        Assert.True(candidate.NotLedgerCommit);
        Assert.All(candidate.Lines, line => Assert.Null(line.Quantity));
        Assert.Contains(candidate.Lines, line => line.Symbol == "AUDUSD" && line.Side == "SELL");
        Assert.Contains(candidate.Lines, line => line.Symbol == "EURUSD" && line.Side == "SELL");
        Assert.Contains(candidate.Lines, line => line.Symbol == "GBPUSD" && line.Side == "BUY");
    }

    [Fact]
    public void Runner_rejects_non_quarter_hour_close()
    {
        var snapshot = CreateSnapshot(canonicalTargetCloseUtc: new DateTimeOffset(2025, 12, 17, 2, 6, 0, TimeSpan.Zero));
        var runner = new SandboxQubesPrototypeRunner();

        Assert.Throws<InvalidOperationException>(() => runner.Run(new SandboxQubesPrototypeRunRequest(snapshot, "r005")));
    }

    private static SandboxQubesInputSnapshot CreateSnapshot(DateTimeOffset? canonicalTargetCloseUtc = null)
    {
        return new SandboxQubesInputSnapshot(
            SnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            SnapshotType: SandboxQubesSnapshotType.PrototypeDeterministicInputSnapshot,
            SandboxOnly: true,
            NotProduction: true,
            CanonicalTargetCloseUtc: canonicalTargetCloseUtc ?? new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            CreatedUtc: new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero),
            Signals:
            [
                new SandboxQubesInputSignal("AUDCNH", -0.036436m),
                new SandboxQubesInputSignal("AUDUSD", -0.053856m),
                new SandboxQubesInputSignal("CNHSGD", 0.336970m),
                new SandboxQubesInputSignal("EURGBP", 0.094338m),
                new SandboxQubesInputSignal("EURUSD", -0.013900m),
                new SandboxQubesInputSignal("GBPUSD", 0.039348m),
                new SandboxQubesInputSignal("JPYUSD", 0.001663m),
            ],
            SourceType: "RepoLocalDeterministicPrototypeSignals",
            SourceArtifactPath: "src/QQ.Production.Intraday.Application/SandboxQubesPrototype.cs",
            SourceArtifactHash: "pending",
            ContainsMarketPrices: false,
            ContainsReturns: false,
            ContainsSignals: true,
            ContainsRiskInputs: false,
            ContainsCovariance: false,
            ContainsWeights: false,
            MarketDataSnapshotId: null,
            MarketDataSnapshotStatus: "NOT_BOUND_PROTOTYPE_INPUT_NO_MARKETDATA",
            FixtureOrPrototypeStatus: "PROTOTYPE_ONLY_NOT_PRODUCTION_QUBES",
            DirectCrossPolicy: "DirectCrossSignalOnlyNettingFirstExecutionDisabled",
            ExecutionUniversePolicy: "USDPairOnlyExecutionUniverse");
    }
}
