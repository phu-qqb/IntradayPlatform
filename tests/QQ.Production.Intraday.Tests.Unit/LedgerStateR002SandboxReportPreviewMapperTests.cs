using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LedgerStateR002SandboxReportPreviewMapperTests
{
    private readonly LedgerStateR002SandboxReportPreviewMapper _mapper = new();

    [Fact]
    public void Maps_existing_sandbox_fills_to_preview_only_lines_with_economic_gaps()
    {
        var result = _mapper.Map(Request());

        Assert.Equal(LedgerStateR002PreviewDecision.PaperLedgerPreviewMapperReadyWithEconomicFieldGaps, result.Decision);
        Assert.Equal(14, result.PreviewLines.Count);
        Assert.All(result.PreviewLines, line =>
        {
            Assert.True(line.PreviewOnly);
            Assert.False(line.CommitAllowed);
            Assert.True(line.NoLedgerCommit);
            Assert.False(line.LedgerMutation);
            Assert.False(line.TradingStateMutation);
            Assert.Contains("AccountId", line.MissingFields);
            Assert.Contains("CommissionFees", line.MissingFields);
        });
        Assert.Contains("MissingAccountId", result.CommitBlockers);
        Assert.Contains("MissingCommissionFeeModel", result.CommitBlockers);
        Assert.False(result.CommitAllowed);
        Assert.False(result.LedgerMutation);
        Assert.False(result.TradingStateMutation);
    }

    [Fact]
    public void Builds_position_and_exposure_preview_from_open_and_flatten_pairs()
    {
        var result = _mapper.Map(Request());

        Assert.Equal(14, result.PositionDeltas.Count);
        Assert.Equal(0.7m, result.Reconciliation.OpenQuantity);
        Assert.Equal(0.7m, result.Reconciliation.FlattenQuantity);
        Assert.Equal(0m, result.Reconciliation.PreviewResidualQuantity);
        Assert.True(result.Reconciliation.ResidualMatchesEvidence);
        Assert.True(result.ExposurePreview.FlattenedPairNetsToZero);
        Assert.True(result.ExposurePreview.HypotheticalOnly);
        Assert.False(result.ExposurePreview.LedgerMutation);
    }

    [Fact]
    public void Cash_impact_is_incomplete_when_account_fee_and_fx_models_are_missing()
    {
        var result = _mapper.Map(Request());

        Assert.Equal(LedgerStateR002CashImpactStatus.Incomplete, result.CashImpact.Status);
        Assert.NotNull(result.CashImpact.GrossCashDelta);
        Assert.Null(result.CashImpact.NetCashDelta);
        Assert.Contains("MissingAccountId", result.CashImpact.MissingReasons);
        Assert.Contains("MissingFxConversionModel", result.CashImpact.MissingReasons);
        Assert.False(result.CashImpact.LedgerMutation);
    }

    [Fact]
    public void Usdjpy_caveat_is_preserved()
    {
        var result = _mapper.Map(Request());

        var open = Assert.Single(result.PreviewLines, x => x.ClOrdId == "R007USDJPY2605261515");
        Assert.Equal("USDJPY", open.ExecutionTradableSymbol);
        Assert.Equal("JPYUSD", open.NormalizedPortfolioSymbol);
        Assert.True(open.RequiresInversion);
        Assert.Equal("4004", open.SecurityID);
        Assert.Equal("8", open.SecurityIDSource);
    }

    [Fact]
    public void Direct_cross_is_blocked_before_preview_line_creation()
    {
        var request = Request(openFills: [Fill("EXEC-SANDBOX-R007", "R007EURGBP2605261515", "EURGBP", "Buy", 0.1m, 0.86m, "9999", false, "EURGBP")]);

        var result = _mapper.Map(request);

        Assert.Equal(LedgerStateR002PreviewDecision.PaperLedgerPreviewMapperBlockedMissingCoreReportFields, result.Decision);
        Assert.Empty(result.PreviewLines);
        Assert.Contains(result.MappingDiagnostics, x => x.Contains("DirectCrossExecutionForbidden:EURGBP", StringComparison.Ordinal));
        Assert.False(result.LedgerMutation);
    }

    [Fact]
    public void Same_source_same_input_has_stable_preview_hash_and_different_input_conflicts()
    {
        var first = _mapper.Map(Request());
        var second = _mapper.Map(Request());
        var changed = _mapper.Map(Request(openFills: SevenOpenFills().Select(x => x.ClOrdId == "R007EURUSD2605261515" ? x with { Price = x.Price + 0.0001m } : x).ToArray()));

        Assert.Equal(first.Idempotency.InputHash, second.Idempotency.InputHash);
        Assert.Equal(first.Idempotency.PreviewHash, second.Idempotency.PreviewHash);
        Assert.NotEqual(first.Idempotency.InputHash, changed.Idempotency.InputHash);
        Assert.True(first.Idempotency.SameSourceSameInputSamePreviewHash);
        Assert.True(first.Idempotency.SameSourceDifferentInputConflict);
        Assert.False(first.Idempotency.DuplicateCommitCandidateCreated);
    }

    private static LedgerStateR002MapperRequest Request(
        IReadOnlyList<LedgerStateR002SandboxFillEvidence>? openFills = null,
        IReadOnlyList<LedgerStateR002SandboxFillEvidence>? flattenFills = null)
        => new(
            RequestId: "ledger-state-r002-preview",
            OpenFills: openFills ?? SevenOpenFills(),
            FlattenFills: flattenFills ?? SevenFlattenFills(),
            Reconciliation: new LedgerStateR002SandboxReconciliationEvidence(
                SourcePhase: "EXEC-SANDBOX-R009",
                ExpectedResidualQuantity: 0m,
                FlatByFillReportDerivedAudit: true,
                ProductionMutationDetected: false,
                SandboxOnly: true),
            CanonicalTargetCloseUtc: null,
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutationAllowed: false,
            TradingStateMutationAllowed: false);

    private static IReadOnlyList<LedgerStateR002SandboxFillEvidence> SevenOpenFills()
        =>
        [
            Fill("EXEC-SANDBOX-R007", "R007EURUSD2605261515", "EURUSD", "Buy", 0.1m, 1.16343m, "4001", false, "EURUSD"),
            Fill("EXEC-SANDBOX-R007", "R007USDJPY2605261515", "USDJPY", "Buy", 0.1m, 159.202m, "4004", true, "JPYUSD"),
            Fill("EXEC-SANDBOX-R007", "R007AUDUSD2605261515", "AUDUSD", "Buy", 0.1m, 0.71614m, "4007", false, "AUDUSD"),
            Fill("EXEC-SANDBOX-R007", "R007GBPUSD2605261515", "GBPUSD", "Buy", 0.1m, 1.34734m, "4002", false, "GBPUSD"),
            Fill("EXEC-SANDBOX-R007", "R007NZDUSD2605261515", "NZDUSD", "Buy", 0.1m, 0.58447m, "100613", false, "NZDUSD"),
            Fill("EXEC-SANDBOX-R007", "R007USDCAD2605261515", "USDCAD", "Buy", 0.1m, 1.38052m, "4013", true, "CADUSD"),
            Fill("EXEC-SANDBOX-R007", "R007USDCHF2605261515", "USDCHF", "Buy", 0.1m, 0.7844m, "4010", true, "CHFUSD")
        ];

    private static IReadOnlyList<LedgerStateR002SandboxFillEvidence> SevenFlattenFills()
        =>
        [
            Fill("EXEC-SANDBOX-R008", "R008FEURUSD260526", "EURUSD", "Sell", 0.1m, 1.1641m, "4001", false, "EURUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FUSDJPY260526", "USDJPY", "Sell", 0.1m, 159.185m, "4004", true, "JPYUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FAUDUSD260526", "AUDUSD", "Sell", 0.1m, 0.71635m, "4007", false, "AUDUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FGBPUSD260526", "GBPUSD", "Sell", 0.1m, 1.34815m, "4002", false, "GBPUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FNZDUSD260526", "NZDUSD", "Sell", 0.1m, 0.58445m, "100613", false, "NZDUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FUSDCAD260526", "USDCAD", "Sell", 0.1m, 1.38036m, "4013", true, "CADUSD"),
            Fill("EXEC-SANDBOX-R008", "R008FUSDCHF260526", "USDCHF", "Sell", 0.1m, 0.78416m, "4010", true, "CHFUSD")
        ];

    private static LedgerStateR002SandboxFillEvidence Fill(
        string phase,
        string clOrdId,
        string symbol,
        string side,
        decimal quantity,
        decimal price,
        string securityId,
        bool requiresInversion,
        string normalizedPortfolioSymbol)
        => new(
            SourcePhase: phase,
            SourceFillId: $"{phase}:{clOrdId}:fill",
            SourceExecutionReportId: $"{phase}:{clOrdId}:execution-report",
            SourceSandboxOrderId: $"{phase}:{clOrdId}:sandbox-order",
            ClOrdId: clOrdId,
            Symbol: symbol,
            ExecutionTradableSymbol: symbol,
            NormalizedPortfolioSymbol: normalizedPortfolioSymbol,
            RequiresInversion: requiresInversion,
            SecurityID: securityId,
            SecurityIDSource: "8",
            Side: side,
            Quantity: quantity,
            Price: price,
            TimestampUtc: null,
            SandboxOnly: true,
            ProductionOrder: false,
            ProductionFill: false,
            SideEvidenceSource: $"{phase} order intent");
}
