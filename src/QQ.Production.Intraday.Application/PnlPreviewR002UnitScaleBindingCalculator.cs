namespace QQ.Production.Intraday.Application;

public enum PnlPreviewR002Decision
{
    SandboxGrossRoundTripPnlPreviewV0AmountsComputed,
    SandboxGrossRoundTripPnlPreviewV0PartialUnitScaleMissing,
    SandboxGrossRoundTripPnlPreviewV0BlockedUnitScaleConflict,
    InconclusiveSafe
}

public enum PnlPreviewR002UnitScaleStatus
{
    UnitScaleBound,
    UnitScaleBoundWithWarnings,
    UnitScaleMissing,
    UnitScaleConflict
}

public sealed record PnlPreviewR002UnitScaleEvidence(
    string Symbol,
    PnlPreviewR002UnitScaleStatus Status,
    decimal? ContractSizeOrUnitScale,
    string QuantityUnit,
    decimal? MinOrderQuantity,
    decimal? QuantityStep,
    string SourceArtifact,
    string EvidenceConfidence,
    IReadOnlyList<string> Warnings);

public sealed record PnlPreviewR002Request(
    PnlPreviewR001Request R001Request,
    IReadOnlyDictionary<string, PnlPreviewR002UnitScaleEvidence> UnitScaleEvidenceBySymbol);

public sealed record PnlPreviewR002PerSymbolAmount(
    string Symbol,
    string OpenSide,
    string FlattenSide,
    decimal OpenPrice,
    decimal FlattenPrice,
    decimal Quantity,
    string QuantityUnit,
    decimal? ContractSizeOrUnitScale,
    PnlPreviewR002UnitScaleStatus UnitScaleStatus,
    decimal PriceDelta,
    decimal? GrossRoundTripPnlQuoteCurrency,
    string PnlCurrency,
    bool CostsApplied,
    bool FeesApplied,
    bool CommissionsApplied,
    bool FxConversionApplied,
    bool AccountCurrencyApplied,
    bool NetPnlReady,
    bool AccountingPnlReady,
    bool ProductionPnlReady,
    bool LedgerCommitAllowed,
    IReadOnlyList<string> Diagnostics);

public sealed record PnlPreviewR002AggregateAmount(
    bool AggregateAvailable,
    bool PartialAggregateOnly,
    decimal? GrossRoundTripPnlQuoteCurrency,
    string PnlCurrency,
    IReadOnlyList<string> IncludedSymbols,
    IReadOnlyList<string> ComputedSymbols,
    IReadOnlyList<string> MissingSymbols,
    IReadOnlyList<string> Diagnostics);

public sealed record PnlPreviewR002Result(
    PnlPreviewR002Decision Decision,
    IReadOnlyList<PnlPreviewR002PerSymbolAmount> PerSymbolAmounts,
    PnlPreviewR002AggregateAmount Aggregate,
    IReadOnlyList<string> Diagnostics,
    bool GrossOnly,
    bool QuoteCurrencyOnly,
    bool CostsApplied,
    bool FxConversionApplied,
    bool AccountCurrencyApplied,
    bool NetPnlReady,
    bool AccountingPnlReady,
    bool ProductionPnlReady,
    bool LedgerCommitReady,
    bool TradingStateMutation);

public sealed class PnlPreviewR002UnitScaleBindingCalculator
{
    public PnlPreviewR002Result Calculate(PnlPreviewR002Request request)
    {
        var r001 = new PnlPreviewR001SandboxGrossRoundTripPreviewCalculator().Calculate(request.R001Request);
        if (r001.Decision is PnlPreviewR001Decision.InconclusiveSafe or PnlPreviewR001Decision.SandboxGrossRoundTripPnlPreviewV0BlockedMissingFillPairEvidence)
        {
            return Empty(PnlPreviewR002Decision.InconclusiveSafe, r001.Diagnostics);
        }

        var amounts = r001.FillPairs
            .Select(pair => ToAmount(pair, request.UnitScaleEvidenceBySymbol))
            .ToArray();

        var conflicts = amounts.Any(x => x.UnitScaleStatus == PnlPreviewR002UnitScaleStatus.UnitScaleConflict);
        var missing = amounts.Any(x => x.GrossRoundTripPnlQuoteCurrency is null);
        var aggregate = BuildAggregate(amounts);
        var decision = conflicts
            ? PnlPreviewR002Decision.SandboxGrossRoundTripPnlPreviewV0BlockedUnitScaleConflict
            : missing
                ? PnlPreviewR002Decision.SandboxGrossRoundTripPnlPreviewV0PartialUnitScaleMissing
                : PnlPreviewR002Decision.SandboxGrossRoundTripPnlPreviewV0AmountsComputed;

        return new PnlPreviewR002Result(
            decision,
            amounts,
            aggregate,
            r001.Diagnostics,
            GrossOnly: true,
            QuoteCurrencyOnly: true,
            CostsApplied: false,
            FxConversionApplied: false,
            AccountCurrencyApplied: false,
            NetPnlReady: false,
            AccountingPnlReady: false,
            ProductionPnlReady: false,
            LedgerCommitReady: false,
            TradingStateMutation: false);
    }

    private static PnlPreviewR002PerSymbolAmount ToAmount(
        PnlPreviewR001FillPair pair,
        IReadOnlyDictionary<string, PnlPreviewR002UnitScaleEvidence> evidenceBySymbol)
    {
        var priceDelta = pair.OpenSide.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? pair.FlattenPrice - pair.OpenPrice
            : pair.OpenPrice - pair.FlattenPrice;

        if (!evidenceBySymbol.TryGetValue(pair.Symbol, out var evidence))
        {
            evidence = new PnlPreviewR002UnitScaleEvidence(
                pair.Symbol,
                PnlPreviewR002UnitScaleStatus.UnitScaleMissing,
                null,
                "SandboxQuantity",
                null,
                null,
                "",
                "Missing",
                ["UnitScaleMissing"]);
        }

        decimal? gross = null;
        if (evidence.Status is PnlPreviewR002UnitScaleStatus.UnitScaleBound or PnlPreviewR002UnitScaleStatus.UnitScaleBoundWithWarnings
            && evidence.ContractSizeOrUnitScale is not null)
        {
            gross = priceDelta * pair.OpenQuantity * evidence.ContractSizeOrUnitScale.Value;
        }

        return new PnlPreviewR002PerSymbolAmount(
            pair.Symbol,
            pair.OpenSide,
            pair.FlattenSide,
            pair.OpenPrice,
            pair.FlattenPrice,
            pair.OpenQuantity,
            evidence.QuantityUnit,
            evidence.ContractSizeOrUnitScale,
            evidence.Status,
            priceDelta,
            gross,
            "USD",
            CostsApplied: false,
            FeesApplied: false,
            CommissionsApplied: false,
            FxConversionApplied: false,
            AccountCurrencyApplied: false,
            NetPnlReady: false,
            AccountingPnlReady: false,
            ProductionPnlReady: false,
            LedgerCommitAllowed: false,
            evidence.Warnings);
    }

    private static PnlPreviewR002AggregateAmount BuildAggregate(IReadOnlyList<PnlPreviewR002PerSymbolAmount> amounts)
    {
        var computed = amounts.Where(x => x.GrossRoundTripPnlQuoteCurrency is not null).Select(x => x.Symbol).ToArray();
        var missing = amounts.Where(x => x.GrossRoundTripPnlQuoteCurrency is null).Select(x => x.Symbol).ToArray();
        if (missing.Length > 0)
        {
            return new PnlPreviewR002AggregateAmount(
                false,
                true,
                computed.Length == 0 ? null : amounts.Where(x => x.GrossRoundTripPnlQuoteCurrency is not null).Sum(x => x.GrossRoundTripPnlQuoteCurrency!.Value),
                "USD",
                amounts.Select(x => x.Symbol).ToArray(),
                computed,
                missing,
                ["PartialAggregateOnly", "UnitScaleMissing"]);
        }

        return new PnlPreviewR002AggregateAmount(
            true,
            false,
            amounts.Sum(x => x.GrossRoundTripPnlQuoteCurrency!.Value),
            "USD",
            amounts.Select(x => x.Symbol).ToArray(),
            computed,
            [],
            []);
    }

    private static PnlPreviewR002Result Empty(PnlPreviewR002Decision decision, IReadOnlyList<string> diagnostics)
        => new(
            decision,
            [],
            new PnlPreviewR002AggregateAmount(false, false, null, "USD", [], [], [], ["R001PreviewUnavailable"]),
            diagnostics,
            GrossOnly: true,
            QuoteCurrencyOnly: true,
            CostsApplied: false,
            FxConversionApplied: false,
            AccountCurrencyApplied: false,
            NetPnlReady: false,
            AccountingPnlReady: false,
            ProductionPnlReady: false,
            LedgerCommitReady: false,
            TradingStateMutation: false);
}
