namespace QQ.Production.Intraday.Application;

public enum PnlPreviewR001Decision
{
    SandboxGrossRoundTripPnlPreviewV0Computed,
    SandboxGrossRoundTripPnlPreviewV0Partial,
    SandboxGrossRoundTripPnlPreviewV0BlockedMissingFillPairEvidence,
    InconclusiveSafe
}

public sealed record PnlPreviewR001Fill(
    string Symbol,
    string Side,
    decimal Quantity,
    string QuantityUnit,
    decimal Price,
    DateTimeOffset? TimestampUtc,
    string SourceFillId,
    string SourceExecutionReportId,
    string SourceRebalanceIntentId,
    bool SandboxOnly,
    bool ProductionFill,
    bool NotProductionPnl);

public sealed record PnlPreviewR001Request(
    IReadOnlyList<PnlPreviewR001Fill> OpenFills,
    IReadOnlyList<PnlPreviewR001Fill> FlattenFills,
    IReadOnlyDictionary<string, decimal> ResidualBySymbol,
    decimal? ContractSizeOrUnitScale,
    bool PreviewOnly,
    bool CostsApplied,
    bool FeesApplied,
    bool CommissionsApplied,
    bool FxConversionApplied,
    bool AccountCurrencyApplied,
    bool LedgerCommitAllowed,
    bool TradingStateMutationAllowed,
    bool NetPnlClaimed,
    bool AccountingPnlClaimed,
    bool ProductionPnlClaimed);

public sealed record PnlPreviewR001FillPair(
    string Symbol,
    string OpenSide,
    string FlattenSide,
    decimal OpenQuantity,
    decimal FlattenQuantity,
    decimal OpenPrice,
    decimal FlattenPrice,
    decimal Residual,
    bool Complete,
    IReadOnlyList<string> Diagnostics);

public sealed record PnlPreviewR001PerSymbolPreview(
    string Symbol,
    string OpenSide,
    string FlattenSide,
    decimal OpenPrice,
    decimal FlattenPrice,
    decimal Quantity,
    string QuantityUnit,
    decimal? ContractSizeOrUnitScale,
    decimal PriceDelta,
    decimal? GrossRoundTripPnlQuoteCurrency,
    string PnlCurrency,
    bool CostsApplied,
    bool FxConversionApplied,
    bool AccountCurrencyApplied,
    bool NetPnlReady,
    bool AccountingPnlReady,
    bool ProductionPnlReady,
    bool LedgerCommitAllowed,
    bool SandboxOnly,
    bool NotProductionPnl,
    IReadOnlyList<string> Warnings);

public sealed record PnlPreviewR001AggregatePreview(
    bool AggregateAvailable,
    decimal? GrossRoundTripPnlQuoteCurrency,
    string PnlCurrency,
    IReadOnlyList<string> IncludedSymbols,
    IReadOnlyList<string> Diagnostics);

public sealed record PnlPreviewR001Result(
    PnlPreviewR001Decision Decision,
    IReadOnlyList<PnlPreviewR001FillPair> FillPairs,
    IReadOnlyList<PnlPreviewR001PerSymbolPreview> PerSymbolPreview,
    PnlPreviewR001AggregatePreview AggregatePreview,
    IReadOnlyList<string> Diagnostics,
    bool PreviewOnly,
    bool NetPnlReady,
    bool AccountingPnlReady,
    bool ProductionPnlReady,
    bool LedgerCommitReady,
    bool TradingStateMutation);

public sealed class PnlPreviewR001SandboxGrossRoundTripPreviewCalculator
{
    public static readonly string[] ExpectedSymbols = ["AUDUSD", "EURUSD", "GBPUSD"];

    public PnlPreviewR001Result Calculate(PnlPreviewR001Request request)
    {
        var diagnostics = ValidateRequest(request).ToArray();
        var forbidden = diagnostics.Any(x => x.EndsWith("Forbidden", StringComparison.Ordinal));
        var pairs = ExpectedSymbols.Select(symbol => Pair(request, symbol)).ToArray();
        var pairBlocked = pairs.Any(x => !x.Complete);

        var perSymbol = forbidden || pairBlocked
            ? Array.Empty<PnlPreviewR001PerSymbolPreview>()
            : pairs.Select(pair => ToPreview(pair, request.ContractSizeOrUnitScale)).ToArray();

        var incompleteScale = perSymbol.Any(x => x.GrossRoundTripPnlQuoteCurrency is null);
        var aggregate = BuildAggregate(perSymbol);

        var decision = forbidden
            ? PnlPreviewR001Decision.InconclusiveSafe
            : pairBlocked
                ? PnlPreviewR001Decision.SandboxGrossRoundTripPnlPreviewV0BlockedMissingFillPairEvidence
                : incompleteScale
                    ? PnlPreviewR001Decision.SandboxGrossRoundTripPnlPreviewV0Partial
                    : PnlPreviewR001Decision.SandboxGrossRoundTripPnlPreviewV0Computed;

        return new PnlPreviewR001Result(
            decision,
            pairs,
            perSymbol,
            aggregate,
            diagnostics,
            PreviewOnly: true,
            NetPnlReady: false,
            AccountingPnlReady: false,
            ProductionPnlReady: false,
            LedgerCommitReady: false,
            TradingStateMutation: false);
    }

    private static IEnumerable<string> ValidateRequest(PnlPreviewR001Request request)
    {
        if (!request.PreviewOnly) yield return "PreviewOnlyRequiredForbidden";
        if (request.CostsApplied) yield return "CostsAppliedForbidden";
        if (request.FeesApplied) yield return "FeesAppliedForbidden";
        if (request.CommissionsApplied) yield return "CommissionsAppliedForbidden";
        if (request.FxConversionApplied) yield return "FxConversionAppliedForbidden";
        if (request.AccountCurrencyApplied) yield return "AccountCurrencyAppliedForbidden";
        if (request.LedgerCommitAllowed) yield return "LedgerCommitAllowedForbidden";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationForbidden";
        if (request.NetPnlClaimed) yield return "NetPnlClaimedForbidden";
        if (request.AccountingPnlClaimed) yield return "AccountingPnlClaimedForbidden";
        if (request.ProductionPnlClaimed) yield return "ProductionPnlClaimedForbidden";

        foreach (var fill in request.OpenFills.Concat(request.FlattenFills))
        {
            if (!fill.SandboxOnly) yield return $"SandboxOnlyRequiredForbidden:{fill.Symbol}";
            if (fill.ProductionFill) yield return $"ProductionFillForbidden:{fill.Symbol}";
            if (!fill.NotProductionPnl) yield return $"NotProductionPnlRequiredForbidden:{fill.Symbol}";
        }
    }

    private static PnlPreviewR001FillPair Pair(PnlPreviewR001Request request, string symbol)
    {
        var diagnostics = new List<string>();
        var open = request.OpenFills.SingleOrDefault(x => x.Symbol.Equals(symbol, StringComparison.Ordinal));
        var flatten = request.FlattenFills.SingleOrDefault(x => x.Symbol.Equals(symbol, StringComparison.Ordinal));

        if (open is null) diagnostics.Add("MissingOpenFill");
        if (flatten is null) diagnostics.Add("MissingFlattenFill");
        if (open is null || flatten is null)
        {
            return new PnlPreviewR001FillPair(symbol, "", "", 0m, 0m, 0m, 0m, 0m, Complete: false, diagnostics);
        }

        if (!OppositeSides(open.Side, flatten.Side)) diagnostics.Add("OpenFlattenSidesNotOpposite");
        if (open.Quantity != flatten.Quantity) diagnostics.Add("OpenFlattenQuantityMismatch");
        if (open.Price <= 0m || flatten.Price <= 0m) diagnostics.Add("MissingFillPrice");
        if (!request.ResidualBySymbol.TryGetValue(symbol, out var residual)) diagnostics.Add("MissingResidualEvidence");
        if (residual != 0m) diagnostics.Add("ResidualNotZero");
        if (!open.SourceRebalanceIntentId.Equals(flatten.SourceRebalanceIntentId, StringComparison.Ordinal)) diagnostics.Add("CrossRailLineageMismatch");

        return new PnlPreviewR001FillPair(
            symbol,
            open.Side,
            flatten.Side,
            open.Quantity,
            flatten.Quantity,
            open.Price,
            flatten.Price,
            residual,
            Complete: diagnostics.Count == 0,
            diagnostics);
    }

    private static PnlPreviewR001PerSymbolPreview ToPreview(PnlPreviewR001FillPair pair, decimal? scale)
    {
        var priceDelta = pair.OpenSide.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? pair.FlattenPrice - pair.OpenPrice
            : pair.OpenPrice - pair.FlattenPrice;

        var warnings = new List<string>();
        decimal? gross = null;
        if (scale is null)
        {
            warnings.Add("UnitScaleMissing");
            warnings.Add("GrossRoundTripPnlQuoteCurrencyNotComputed");
        }
        else
        {
            gross = priceDelta * pair.OpenQuantity * scale.Value;
        }

        return new PnlPreviewR001PerSymbolPreview(
            pair.Symbol,
            pair.OpenSide,
            pair.FlattenSide,
            pair.OpenPrice,
            pair.FlattenPrice,
            pair.OpenQuantity,
            QuantityUnit: "SandboxQuantity",
            ContractSizeOrUnitScale: scale,
            PriceDelta: priceDelta,
            GrossRoundTripPnlQuoteCurrency: gross,
            PnlCurrency: "USD",
            CostsApplied: false,
            FxConversionApplied: false,
            AccountCurrencyApplied: false,
            NetPnlReady: false,
            AccountingPnlReady: false,
            ProductionPnlReady: false,
            LedgerCommitAllowed: false,
            SandboxOnly: true,
            NotProductionPnl: true,
            Warnings: warnings);
    }

    private static PnlPreviewR001AggregatePreview BuildAggregate(IReadOnlyList<PnlPreviewR001PerSymbolPreview> previews)
    {
        if (previews.Count != ExpectedSymbols.Length)
        {
            return new PnlPreviewR001AggregatePreview(false, null, "USD", previews.Select(x => x.Symbol).ToArray(), ["MissingPerSymbolPreview"]);
        }

        if (previews.Any(x => x.GrossRoundTripPnlQuoteCurrency is null))
        {
            return new PnlPreviewR001AggregatePreview(false, null, "USD", previews.Select(x => x.Symbol).ToArray(), ["PartialAggregateOnly", "UnitScaleMissing"]);
        }

        return new PnlPreviewR001AggregatePreview(
            true,
            previews.Sum(x => x.GrossRoundTripPnlQuoteCurrency!.Value),
            "USD",
            previews.Select(x => x.Symbol).ToArray(),
            []);
    }

    private static bool OppositeSides(string left, string right)
        => left.Equals("BUY", StringComparison.OrdinalIgnoreCase) && right.Equals("SELL", StringComparison.OrdinalIgnoreCase)
            || left.Equals("SELL", StringComparison.OrdinalIgnoreCase) && right.Equals("BUY", StringComparison.OrdinalIgnoreCase);
}
