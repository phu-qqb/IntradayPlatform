namespace QQ.Production.Intraday.Application;

public enum LedgerStateR004Decision
{
    CrossRailPmsLinkedSandboxLedgerPreviewReadyWithPnlGaps,
    CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly,
    CrossRailPmsLinkedSandboxLedgerPreviewBlockedMissingCoreLineage,
    InconclusiveSafe
}

public enum LedgerStateR004FillRole
{
    Open,
    Flatten
}

public sealed record LedgerStateR004SandboxFill(
    LedgerStateR004FillRole Role,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset? TimestampUtc,
    string SandboxOrderId,
    string ExecutionReportId,
    string FillId,
    string ClientOrderId,
    string SecurityId,
    bool SandboxOnly,
    bool ProductionFill,
    bool NotProductionPnl);

public sealed record LedgerStateR004LineageBinding(
    string Symbol,
    string PmsCycleId,
    string? QubesRunId,
    string? StrategyId,
    string? AccountId,
    string? PortfolioId,
    string SandboxAccountProfile,
    string RiskReviewId,
    string OperatorApprovalId,
    string SourceRebalanceIntentId,
    string? SourceExecutionIntentId,
    DateTimeOffset CanonicalTargetCloseUtc,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string SecurityIDSource);

public sealed record LedgerStateR004MapperRequest(
    string RequestId,
    IReadOnlyList<LedgerStateR004SandboxFill> OpenFills,
    IReadOnlyList<LedgerStateR004SandboxFill> FlattenFills,
    IReadOnlyList<LedgerStateR004LineageBinding> LineageBindings,
    IReadOnlyDictionary<string, decimal> ExpectedResidualsBySymbol,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed);

public sealed record LedgerStateR004PaperLedgerPreviewLine(
    string LineId,
    string SourceExecutionReportId,
    string SourceFillId,
    string SourceSandboxOrderId,
    string ClOrdID,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string SecurityID,
    string SecurityIDSource,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset? TimestampUtc,
    string PmsCycleId,
    string? QubesRunId,
    string? StrategyId,
    string? AccountId,
    string? PortfolioId,
    string SandboxAccountProfile,
    string RiskReviewId,
    string OperatorApprovalId,
    string SourceRebalanceIntentId,
    string? SourceExecutionIntentId,
    DateTimeOffset CanonicalTargetCloseUtc,
    bool SandboxOnly,
    bool ProductionFill,
    bool NotProductionPnl,
    bool NoLedgerCommit,
    bool NoTradingStateMutation,
    bool PreviewOnly,
    bool CommitAllowed);

public sealed record LedgerStateR004SandboxPriceDeltaPreview(
    string Symbol,
    decimal OpenFillPrice,
    decimal OpenQuantity,
    string OpenSide,
    decimal FlattenFillPrice,
    decimal FlattenQuantity,
    string FlattenSide,
    decimal RawPriceDelta,
    decimal SideAdjustedPriceDelta,
    decimal? GrossSandboxPriceDelta,
    bool SandboxPriceDeltaOnly,
    bool NotProductionPnl,
    bool NotAccountingPnl,
    bool FullTheoreticalPnlProduced);

public sealed record LedgerStateR004SandboxPnlPreviewInput(
    string Symbol,
    decimal OpenFillPrice,
    decimal OpenQuantity,
    decimal FlattenFillPrice,
    decimal FlattenQuantity,
    decimal SideAdjustedPriceDelta,
    bool SandboxOnly,
    bool ProductionFill,
    bool NotProductionPnl,
    bool NotAccountingPnl,
    bool FullTheoreticalPnlProduced,
    IReadOnlyList<string> MissingFullPnlInputs);

public sealed record LedgerStateR004PreviewReconciliation(
    int ExpectedOrders,
    int ActualOrders,
    int ExpectedFills,
    int ActualFills,
    int ExpectedFlattenOrders,
    int ActualFlattenOrders,
    int ExpectedFlattenFills,
    int ActualFlattenFills,
    IReadOnlyDictionary<string, decimal> ResidualBySymbol,
    bool AllResidualsZero,
    IReadOnlyList<string> Breaks);

public sealed record LedgerStateR004MapperResult(
    LedgerStateR004Decision Decision,
    IReadOnlyList<LedgerStateR004PaperLedgerPreviewLine> PreviewLines,
    IReadOnlyList<LedgerStateR004SandboxPriceDeltaPreview> PriceDeltaPreview,
    IReadOnlyList<LedgerStateR004SandboxPnlPreviewInput> PnlPreviewInputs,
    LedgerStateR004PreviewReconciliation Reconciliation,
    IReadOnlyList<string> PnlGapDiagnostics,
    IReadOnlyList<string> CommitBlockers,
    IReadOnlyList<string> Diagnostics,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed class LedgerStateR004CrossRailPmsLinkedPreviewMapper
{
    public static readonly string[] MissingFullPnlInputs =
    [
        "MissingMarkPrices",
        "MissingCostSpreadCommissionModel",
        "MissingFxConversion",
        "MissingPositionCostBasisModel",
        "MissingAccountCurrency",
        "MissingAttributionPolicy"
    ];

    public LedgerStateR004MapperResult Map(LedgerStateR004MapperRequest request)
    {
        var diagnostics = ValidateRequest(request).ToArray();
        var blocked = diagnostics.Any(x => x.StartsWith("MissingCore", StringComparison.Ordinal) || x.EndsWith("Forbidden", StringComparison.Ordinal));

        var previewLines = blocked
            ? Array.Empty<LedgerStateR004PaperLedgerPreviewLine>()
            : request.OpenFills.Concat(request.FlattenFills)
                .OrderBy(x => x.Symbol, StringComparer.Ordinal)
                .ThenBy(x => x.Role)
                .Select((fill, index) => ToPreviewLine(fill, BindingFor(request, fill.Symbol), index))
                .ToArray();

        var priceDeltas = blocked ? Array.Empty<LedgerStateR004SandboxPriceDeltaPreview>() : BuildPriceDeltas(request).ToArray();
        var pnlInputs = priceDeltas.Select(ToPnlInput).ToArray();
        var reconciliation = new LedgerStateR004PreviewReconciliation(
            ExpectedOrders: 3,
            ActualOrders: request.OpenFills.Count,
            ExpectedFills: 3,
            ActualFills: request.OpenFills.Count,
            ExpectedFlattenOrders: 3,
            ActualFlattenOrders: request.FlattenFills.Count,
            ExpectedFlattenFills: 3,
            ActualFlattenFills: request.FlattenFills.Count,
            ResidualBySymbol: request.ExpectedResidualsBySymbol,
            AllResidualsZero: request.ExpectedResidualsBySymbol.Values.All(x => x == 0m),
            Breaks: []);

        var lineageBlockers = LineageBlockers(request.LineageBindings).ToArray();
        var commitBlockers = MissingFullPnlInputs.Concat(lineageBlockers).Concat(["CommitSafeIdempotencyPolicyIncomplete"])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var decision = blocked
            ? LedgerStateR004Decision.CrossRailPmsLinkedSandboxLedgerPreviewBlockedMissingCoreLineage
            : priceDeltas.Length == 3
                ? LedgerStateR004Decision.CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly
                : LedgerStateR004Decision.CrossRailPmsLinkedSandboxLedgerPreviewReadyWithPnlGaps;

        return new LedgerStateR004MapperResult(
            decision,
            previewLines,
            priceDeltas,
            pnlInputs,
            reconciliation,
            MissingFullPnlInputs,
            commitBlockers,
            diagnostics,
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static IEnumerable<string> ValidateRequest(LedgerStateR004MapperRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) yield return "MissingCoreRequestId";
        if (!request.PreviewOnly) yield return "PreviewOnlyRequiredForbidden";
        if (request.CommitAllowed) yield return "CommitAllowedForbidden";
        if (request.LedgerMutationAllowed) yield return "LedgerMutationForbidden";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationForbidden";
        if (request.OpenFills.Count != 3) yield return "MissingCoreThreeOpenFills";
        if (request.FlattenFills.Count != 3) yield return "MissingCoreThreeFlattenFills";

        foreach (var fill in request.OpenFills.Concat(request.FlattenFills))
        {
            if (!fill.SandboxOnly) yield return $"SandboxOnlyRequiredForbidden:{fill.ClientOrderId}";
            if (fill.ProductionFill) yield return $"ProductionFillForbidden:{fill.ClientOrderId}";
            if (!fill.NotProductionPnl) yield return $"NotProductionPnlRequiredForbidden:{fill.ClientOrderId}";
            if (string.IsNullOrWhiteSpace(fill.ExecutionReportId)) yield return $"MissingCoreExecutionReportId:{fill.ClientOrderId}";
            if (string.IsNullOrWhiteSpace(fill.FillId)) yield return $"MissingCoreFillId:{fill.ClientOrderId}";
            if (fill.Quantity <= 0m) yield return $"MissingCoreQuantity:{fill.ClientOrderId}";
            if (fill.Price <= 0m) yield return $"MissingCorePrice:{fill.ClientOrderId}";
        }

        foreach (var symbol in request.OpenFills.Select(x => x.Symbol).Distinct(StringComparer.Ordinal))
        {
            if (!request.FlattenFills.Any(x => x.Symbol.Equals(symbol, StringComparison.Ordinal))) yield return $"MissingCoreFlattenFill:{symbol}";
            if (!request.LineageBindings.Any(x => x.Symbol.Equals(symbol, StringComparison.Ordinal))) yield return $"MissingCoreLineage:{symbol}";
        }
    }

    private static LedgerStateR004PaperLedgerPreviewLine ToPreviewLine(LedgerStateR004SandboxFill fill, LedgerStateR004LineageBinding binding, int index)
        => new(
            LineId: $"CROSS-RAIL-R014:{fill.Symbol}:{fill.Role}:{index + 1}:paper-ledger-preview-line",
            SourceExecutionReportId: fill.ExecutionReportId,
            SourceFillId: fill.FillId,
            SourceSandboxOrderId: fill.SandboxOrderId,
            ClOrdID: fill.ClientOrderId,
            Symbol: fill.Symbol,
            ExecutionTradableSymbol: binding.ExecutionTradableSymbol,
            NormalizedPortfolioSymbol: binding.NormalizedPortfolioSymbol,
            RequiresInversion: binding.RequiresInversion,
            SecurityID: fill.SecurityId,
            SecurityIDSource: binding.SecurityIDSource,
            Side: fill.Side,
            Quantity: fill.Quantity,
            Price: fill.Price,
            TimestampUtc: fill.TimestampUtc,
            PmsCycleId: binding.PmsCycleId,
            QubesRunId: binding.QubesRunId,
            StrategyId: binding.StrategyId,
            AccountId: binding.AccountId,
            PortfolioId: binding.PortfolioId,
            SandboxAccountProfile: binding.SandboxAccountProfile,
            RiskReviewId: binding.RiskReviewId,
            OperatorApprovalId: binding.OperatorApprovalId,
            SourceRebalanceIntentId: binding.SourceRebalanceIntentId,
            SourceExecutionIntentId: binding.SourceExecutionIntentId,
            CanonicalTargetCloseUtc: binding.CanonicalTargetCloseUtc,
            SandboxOnly: fill.SandboxOnly,
            ProductionFill: fill.ProductionFill,
            NotProductionPnl: fill.NotProductionPnl,
            NoLedgerCommit: true,
            NoTradingStateMutation: true,
            PreviewOnly: true,
            CommitAllowed: false);

    private static IEnumerable<LedgerStateR004SandboxPriceDeltaPreview> BuildPriceDeltas(LedgerStateR004MapperRequest request)
    {
        foreach (var open in request.OpenFills.OrderBy(x => x.Symbol, StringComparer.Ordinal))
        {
            var flatten = request.FlattenFills.Single(x => x.Symbol.Equals(open.Symbol, StringComparison.Ordinal));
            var raw = flatten.Price - open.Price;
            var sideAdjusted = open.Side.Equals("SELL", StringComparison.OrdinalIgnoreCase) ? -raw : raw;

            yield return new LedgerStateR004SandboxPriceDeltaPreview(
                open.Symbol,
                open.Price,
                open.Quantity,
                open.Side,
                flatten.Price,
                flatten.Quantity,
                flatten.Side,
                raw,
                sideAdjusted,
                GrossSandboxPriceDelta: Math.Abs(sideAdjusted),
                SandboxPriceDeltaOnly: true,
                NotProductionPnl: true,
                NotAccountingPnl: true,
                FullTheoreticalPnlProduced: false);
        }
    }

    private static LedgerStateR004SandboxPnlPreviewInput ToPnlInput(LedgerStateR004SandboxPriceDeltaPreview priceDelta)
        => new(
            priceDelta.Symbol,
            priceDelta.OpenFillPrice,
            priceDelta.OpenQuantity,
            priceDelta.FlattenFillPrice,
            priceDelta.FlattenQuantity,
            priceDelta.SideAdjustedPriceDelta,
            SandboxOnly: true,
            ProductionFill: false,
            NotProductionPnl: true,
            NotAccountingPnl: true,
            FullTheoreticalPnlProduced: false,
            MissingFullPnlInputs);

    private static LedgerStateR004LineageBinding BindingFor(LedgerStateR004MapperRequest request, string symbol)
        => request.LineageBindings.Single(x => x.Symbol.Equals(symbol, StringComparison.Ordinal));

    private static IEnumerable<string> LineageBlockers(IReadOnlyList<LedgerStateR004LineageBinding> bindings)
    {
        if (bindings.Any(x => string.IsNullOrWhiteSpace(x.AccountId))) yield return "MissingAccountId";
        if (bindings.Any(x => string.IsNullOrWhiteSpace(x.PortfolioId))) yield return "MissingPortfolioId";
        if (bindings.Any(x => string.IsNullOrWhiteSpace(x.StrategyId))) yield return "MissingStrategyId";
        if (bindings.Any(x => string.IsNullOrWhiteSpace(x.QubesRunId))) yield return "MissingQubesRunId";
        if (bindings.Any(x => string.IsNullOrWhiteSpace(x.SourceExecutionIntentId))) yield return "MissingSourceExecutionIntentId";
    }
}
