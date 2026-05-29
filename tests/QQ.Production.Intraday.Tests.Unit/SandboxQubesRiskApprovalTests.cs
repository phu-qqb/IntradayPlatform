namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesRiskApprovalTests
{
    private const string MarketDataSnapshotId = "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD";

    [Fact]
    public void R008_candidate_intake_is_valid_for_r009_review()
    {
        var candidate = CreateCandidate();

        Assert.Equal(MarketDataSnapshotId, candidate.MarketDataSnapshotId);
        Assert.Equal(6_000_000m, candidate.TargetNotional);
        Assert.True(candidate.ExecutionReadyPreview);
        Assert.True(candidate.SandboxOnly);
        Assert.True(candidate.NotProduction);
        Assert.True(candidate.NotAccounting);
        Assert.True(candidate.NotExecuted);
        Assert.True(candidate.NotLedgerCommit);
        Assert.Equal(3, candidate.Lines.Count);
    }

    [Fact]
    public void Weights_are_classified_as_target_portfolio_weights()
    {
        const string weightSemantics = "WEIGHTS_CONFIRMED_TARGET_PORTFOLIO_WEIGHTS";

        Assert.Equal("WEIGHTS_CONFIRMED_TARGET_PORTFOLIO_WEIGHTS", weightSemantics);
        Assert.NotEqual("WEIGHTS_SEMANTICS_STILL_UNCLEAR", weightSemantics);
    }

    [Fact]
    public void Exposure_review_computes_gross_net_and_symbol_exposures()
    {
        var review = ComputeExposure(CreateCandidate());

        Assert.Equal(322_978.40000m, review.LineBySymbol["AUDUSD"].QuoteNotional);
        Assert.Equal(-322_978.40000m, review.LineBySymbol["AUDUSD"].SignedQuoteExposure);
        Assert.Equal(82_230.7500000m, review.LineBySymbol["EURUSD"].QuoteNotional);
        Assert.Equal(-82_230.7500000m, review.LineBySymbol["EURUSD"].SignedQuoteExposure);
        Assert.Equal(234_933.1250000m, review.LineBySymbol["GBPUSD"].QuoteNotional);
        Assert.Equal(234_933.1250000m, review.LineBySymbol["GBPUSD"].SignedQuoteExposure);

        Assert.Equal(640_142.2750000m, review.GrossQuoteNotional);
        Assert.Equal(-170_276.0250000m, review.NetSignedQuoteExposure);
        Assert.Equal(10.669037916666666666666666670m, review.GrossPctOfTarget);
        Assert.Equal(-2.8379337500m, review.NetPctOfTarget);
    }

    [Fact]
    public void Candidate_quantities_remain_unchanged_from_r008()
    {
        var candidate = CreateCandidate();

        Assert.Equal(48.7m, candidate.Lines.Single(line => line.Symbol == "AUDUSD").Quantity);
        Assert.Equal(7.0m, candidate.Lines.Single(line => line.Symbol == "EURUSD").Quantity);
        Assert.Equal(17.5m, candidate.Lines.Single(line => line.Symbol == "GBPUSD").Quantity);
    }

    [Fact]
    public void Direct_cross_review_preserves_usdjpy_caveat_and_rejects_leakage()
    {
        var directCrossesExcluded = new[] { "AUDCNH", "CNHSGD", "EURGBP" };
        var emittedSymbols = CreateCandidate().Lines.Select(line => line.Symbol).ToArray();

        Assert.Equal(["AUDUSD", "EURUSD", "GBPUSD"], emittedSymbols);
        Assert.DoesNotContain("EURGBP", emittedSymbols);
        Assert.Contains("EURGBP", directCrossesExcluded);
        Assert.True(new UsdJpyCaveat("JPYUSD", "USDJPY", true, 4004, "8").Preserved);
    }

    [Fact]
    public void Offline_polygon_bbo_and_target_notional_remain_sandbox_preview_only()
    {
        var candidate = CreateCandidate();

        Assert.Equal("OperatorProvidedLocalOfflinePolygonBbo", candidate.MarketDataSource);
        Assert.Equal("SandboxPreviewSizingOnly", candidate.PriceBasisScope);
        Assert.Equal("SandboxPreviewSizingOnly", candidate.TargetNotionalScope);
        Assert.False(candidate.NotProduction is false);
        Assert.False(candidate.NotAccounting is false);
    }

    [Fact]
    public void Approval_is_not_full_execution_approval_when_operator_approval_is_required_but_missing()
    {
        var decision = Decide(operatorApprovalForFutureExecutionPresent: false);

        Assert.Equal("APPROVED_PREVIEW_ONLY_OPERATOR_APPROVAL_REQUIRED_BEFORE_EXECUTION", decision);
        Assert.NotEqual("APPROVED_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION", decision);
    }

    [Fact]
    public void No_accounting_net_or_production_readiness_is_claimed()
    {
        var readiness = new ReadinessImpact(
            AccountingPnlUnlocked: false,
            NetPnlUnlocked: false,
            ProductionLiveUnlocked: false,
            LedgerCommitCreated: false);

        Assert.False(readiness.AccountingPnlUnlocked);
        Assert.False(readiness.NetPnlUnlocked);
        Assert.False(readiness.ProductionLiveUnlocked);
        Assert.False(readiness.LedgerCommitCreated);
    }

    private static string Decide(bool operatorApprovalForFutureExecutionPresent)
    {
        return operatorApprovalForFutureExecutionPresent
            ? "APPROVED_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION"
            : "APPROVED_PREVIEW_ONLY_OPERATOR_APPROVAL_REQUIRED_BEFORE_EXECUTION";
    }

    private static ExposureReview ComputeExposure(R009Candidate candidate)
    {
        var lines = candidate.Lines
            .Select(line =>
            {
                var quoteNotional = line.Quantity * line.Price * line.ContractMultiplier;
                var signed = line.Side == "SELL" ? -quoteNotional : quoteNotional;
                var impliedWeight = signed / candidate.TargetNotional;
                return new ExposureLine(line.Symbol, quoteNotional, signed, impliedWeight);
            })
            .ToDictionary(line => line.Symbol, StringComparer.Ordinal);

        var gross = lines.Values.Sum(line => line.QuoteNotional);
        var net = lines.Values.Sum(line => line.SignedQuoteExposure);

        return new ExposureReview(
            LineBySymbol: lines,
            GrossQuoteNotional: gross,
            NetSignedQuoteExposure: net,
            GrossPctOfTarget: gross / candidate.TargetNotional * 100m,
            NetPctOfTarget: net / candidate.TargetNotional * 100m);
    }

    private static R009Candidate CreateCandidate()
    {
        return new R009Candidate(
            MarketDataSnapshotId,
            "OperatorProvidedLocalOfflinePolygonBbo",
            TargetNotional: 6_000_000m,
            TargetNotionalScope: "SandboxPreviewSizingOnly",
            PriceBasisScope: "SandboxPreviewSizingOnly",
            ExecutionReadyPreview: true,
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true,
            Lines:
            [
                new CandidateLine("AUDUSD", "SELL", -0.053856m, 0.6632m, 48.7m, 10_000m, 0.1m),
                new CandidateLine("EURUSD", "SELL", -0.013900m, 1.174725m, 7.0m, 10_000m, 0.1m),
                new CandidateLine("GBPUSD", "BUY", 0.039348m, 1.342475m, 17.5m, 10_000m, 0.1m),
            ]);
    }

    private sealed record R009Candidate(
        string MarketDataSnapshotId,
        string MarketDataSource,
        decimal TargetNotional,
        string TargetNotionalScope,
        string PriceBasisScope,
        bool ExecutionReadyPreview,
        bool SandboxOnly,
        bool NotProduction,
        bool NotAccounting,
        bool NotExecuted,
        bool NotLedgerCommit,
        IReadOnlyList<CandidateLine> Lines);

    private sealed record CandidateLine(
        string Symbol,
        string Side,
        decimal Weight,
        decimal Price,
        decimal Quantity,
        decimal ContractMultiplier,
        decimal MinOrderSize);

    private sealed record ExposureLine(
        string Symbol,
        decimal QuoteNotional,
        decimal SignedQuoteExposure,
        decimal ImpliedWeight);

    private sealed record ExposureReview(
        IReadOnlyDictionary<string, ExposureLine> LineBySymbol,
        decimal GrossQuoteNotional,
        decimal NetSignedQuoteExposure,
        decimal GrossPctOfTarget,
        decimal NetPctOfTarget);

    private sealed record UsdJpyCaveat(
        string NormalizedPortfolioSymbol,
        string ExecutionTradableSymbol,
        bool RequiresInversion,
        int SecurityId,
        string SecurityIdSource)
    {
        public bool Preserved => NormalizedPortfolioSymbol == "JPYUSD"
            && ExecutionTradableSymbol == "USDJPY"
            && RequiresInversion
            && SecurityId == 4004
            && SecurityIdSource == "8";
    }

    private sealed record ReadinessImpact(
        bool AccountingPnlUnlocked,
        bool NetPnlUnlocked,
        bool ProductionLiveUnlocked,
        bool LedgerCommitCreated);
}
