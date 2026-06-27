using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public enum M3EodEvidenceSourceKind
{
    LMAX_OFFICIAL_EOD_REPORT_FILE,
    LMAX_OFFICIAL_EOD_REPORT_PROGRAMMATIC_PENDING_VENDOR,
    QQ_INTERNAL_EOD_RECONSTRUCTION,
    QQ_INTERNAL_INTRADAY_OPERATIONAL_TRUTH,
    MANUAL_EVIDENCE,
    RECONSTRUCTED
}

public enum LmaxEodAcquisitionMethod
{
    PORTAL_MANUAL_UPLOAD,
    SFTP_PENDING_VENDOR,
    OFFICIAL_REPORT_API_PENDING_VENDOR
}

public enum InternalEodReconstructionStatus
{
    INTERNAL_EOD_RECONSTRUCTED_COMPLETE,
    INTERNAL_EOD_RECONSTRUCTED_PARTIAL,
    INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ANCHOR,
    INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ACCOUNT_MAPPING,
    INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_SYMBOL_ALIAS,
    INTERNAL_EOD_RECONSTRUCTION_NON_AUTHORITY
}

public enum DualSourceEodBreakType
{
    MISSING_LMAX_REPORT,
    MISSING_INTERNAL_FILL,
    EXTRA_INTERNAL_FILL,
    EXTRA_LMAX_FILL,
    AMOUNT_MISMATCH,
    QUANTITY_MISMATCH,
    PRICE_MISMATCH,
    COMMISSION_MISMATCH,
    CASH_BALANCE_MISMATCH,
    SYMBOL_ALIAS_MISSING,
    ACCOUNT_MAPPING_MISSING,
    REPORT_SET_PARTIAL,
    SOURCE_NOT_AUTHORITY,
    UNKNOWN_RECONSTRUCTION_COVERAGE
}

public enum DualSourceEodBreakSeverity
{
    Info,
    Warning,
    Blocking
}

public sealed record M3EodSourceClassification(
    M3EodEvidenceSourceKind SourceKind,
    bool IsBrokerAuthorityCandidate,
    bool IsBrokerAuthority,
    bool IsPositionAuthority,
    bool IsOpenOrderAuthority,
    bool IsOperationalTruth,
    string Reason);

public static class M3EodSourceTaxonomyPolicy
{
    public static M3EodSourceClassification Classify(M3EodEvidenceSourceKind sourceKind)
        => sourceKind switch
        {
            M3EodEvidenceSourceKind.LMAX_OFFICIAL_EOD_REPORT_FILE => new(sourceKind, true, false, false, false, false, "Broker-provided offline EOD report file; candidate authority only after completeness, scope, hash and row provenance validation."),
            M3EodEvidenceSourceKind.LMAX_OFFICIAL_EOD_REPORT_PROGRAMMATIC_PENDING_VENDOR => new(sourceKind, false, false, false, false, false, "Programmatic delivery is pending vendor confirmation and is not authority until approved and validated."),
            M3EodEvidenceSourceKind.QQ_INTERNAL_EOD_RECONSTRUCTION => new(sourceKind, false, false, false, false, true, "Derived from internal OMS/PMS/execution data; useful as an independent control, not broker authority by itself."),
            M3EodEvidenceSourceKind.QQ_INTERNAL_INTRADAY_OPERATIONAL_TRUTH => new(sourceKind, false, false, false, false, true, "Internal operational state; not broker live authority and not pre-trade broker authority."),
            M3EodEvidenceSourceKind.MANUAL_EVIDENCE => new(sourceKind, false, false, false, false, false, "Operator-provided evidence can support exception review, but is never authority by default."),
            M3EodEvidenceSourceKind.RECONSTRUCTED => new(sourceKind, false, false, false, false, true, "Reconstructed evidence is never broker position or open-order authority without a future explicit lead decision."),
            _ => new(sourceKind, false, false, false, false, false, "Unknown source kind.")
        };

    public static bool CanPromoteToBrokerPositionAuthority(M3EodEvidenceSourceKind sourceKind)
        => Classify(sourceKind).IsPositionAuthority;

    public static bool CanPromoteToBrokerOpenOrderAuthority(M3EodEvidenceSourceKind sourceKind)
        => Classify(sourceKind).IsOpenOrderAuthority;
}

public sealed record LmaxEodReportAcquisitionRequest(
    LmaxReportType ReportType,
    string BrokerAccount,
    DateOnly StartDate,
    DateOnly EndDate,
    LmaxEodAcquisitionMethod AcquisitionMethod);

public sealed record LmaxEodReportAcquisitionDescriptor(
    LmaxReportType ReportType,
    string BrokerAccount,
    DateOnly ReportDate,
    string SourcePathOrObjectKey,
    string SourceFileSha256,
    LmaxEodAcquisitionMethod AcquisitionMethod,
    string AuthenticationMethodDocumentedOnly,
    string RetentionPeriodDocumentedOnly,
    string VendorPermissionScopeDocumentedOnly,
    IReadOnlyList<string> ExpectedRowProvenanceFields);

public interface ILmaxEodReportAcquisitionService
{
    Task<IReadOnlyList<LmaxEodReportAcquisitionDescriptor>> ListAvailableReportsAsync(LmaxEodReportAcquisitionRequest request, CancellationToken cancellationToken);
}

public static class LmaxEodReportAcquisitionSafetyPolicy
{
    public static IReadOnlyList<string> DeniedOperations { get; } =
    [
        "FIX logon",
        "NewOrderSingle 35=D",
        "OrderCancelRequest 35=F",
        "OrderCancelReplaceRequest 35=G",
        "Account mutation",
        "Funds movement",
        "Settings mutation",
        "Browser automation",
        "Scraping",
        "LMAX Java client invocation",
        "LMAX .NET client invocation",
        "AccountAPI invocation"
    ];
}

public sealed record InternalEodPriorPositionAnchor(
    InstrumentId InstrumentId,
    decimal BaseQuantity,
    M3EodEvidenceSourceKind SourceKind,
    string SourceHash,
    DateTimeOffset AsOfUtc);

public sealed record InternalEodFillRow(
    DateOnly ReportDate,
    VenueId VenueId,
    BrokerAccountId BrokerAccountId,
    string BrokerExecutionId,
    InstrumentId InstrumentId,
    string InternalSymbol,
    string LmaxSymbol,
    string? LmaxInstrumentId,
    TradeSide Side,
    decimal BaseQuantity,
    decimal SignedBaseQuantity,
    decimal VenueQuantity,
    decimal Price,
    decimal AbsNotional,
    decimal? Commission,
    DateTimeOffset TradeDateUtc,
    DateTimeOffset ReceivedAtUtc,
    string SourceHash);

public sealed record InternalEodTradeSummaryRow(
    DateOnly ReportDate,
    BrokerAccountId BrokerAccountId,
    InstrumentId InstrumentId,
    string LmaxSymbol,
    TradeSide Side,
    decimal FillCount,
    decimal BaseQuantity,
    decimal SignedBaseQuantity,
    decimal AbsNotional,
    decimal? Commission);

public sealed record InternalEodPositionRow(
    DateOnly ReportDate,
    BrokerAccountId BrokerAccountId,
    InstrumentId InstrumentId,
    string LmaxSymbol,
    decimal PriorBaseQuantity,
    decimal SignedFillQuantity,
    decimal DerivedEodBaseQuantity,
    string PriorAnchorHash,
    bool HasBrokerConfirmedPriorAnchor);

public sealed record InternalEodReconstructionResult(
    DateOnly ReportDate,
    VenueId? VenueId,
    BrokerAccountId? BrokerAccountId,
    InternalEodReconstructionStatus Status,
    bool IsBrokerAuthority,
    bool CanProvideOpenOrderAuthority,
    IReadOnlyList<InternalEodFillRow> Fills,
    IReadOnlyList<InternalEodTradeSummaryRow> TradeSummaries,
    IReadOnlyList<InternalEodPositionRow> DerivedPositions,
    IReadOnlyList<string> CoverageGaps,
    IReadOnlyList<string> BlockingReasons);

public sealed record DualSourceEodBreak(
    DualSourceEodBreakType BreakType,
    DualSourceEodBreakSeverity Severity,
    string Description,
    string? ExecutionId,
    InstrumentId? InstrumentId,
    decimal? InternalValue,
    decimal? LmaxValue);

public sealed record DualSourceEodReconciliationResult(
    DateOnly ReportDate,
    BrokerAccountId? BrokerAccountId,
    int InternalFillCount,
    int LmaxFillCount,
    IReadOnlyList<DualSourceEodBreak> Breaks)
{
    public bool HasBlockingBreaks => Breaks.Any(x => x.Severity == DualSourceEodBreakSeverity.Blocking);
}

public static class InternalEodReconstructionBuilder
{
    private const string LmaxReportAliasSource = "LMAX_REPORT";

    public static InternalEodReconstructionResult Build(
        DateOnly reportDate,
        Venue venue,
        BrokerAccount? brokerAccount,
        IReadOnlyList<Instrument> instruments,
        IReadOnlyList<InstrumentAlias> instrumentAliases,
        IReadOnlyList<Fill> fills,
        IReadOnlyList<InternalEodPriorPositionAnchor> priorPositionAnchors,
        decimal? commissionRateAbsNotional = null)
    {
        if (brokerAccount is null)
        {
            return Blocked(reportDate, venue.Id, null, InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ACCOUNT_MAPPING, "Broker account mapping is missing.");
        }

        var fillsForDate = fills
            .Where(x => x.VenueId == venue.Id && DateOnly.FromDateTime(x.TradeDateUtc.UtcDateTime) == reportDate)
            .OrderBy(x => x.ReceivedAtUtc)
            .ToList();
        var touchedInstruments = fillsForDate.Select(x => x.InstrumentId).Distinct().ToList();
        var aliasesByInstrument = instrumentAliases
            .Where(x => x.IsEnabled && x.Source.Equals(LmaxReportAliasSource, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(x => x.Key, x => x.First());
        var missingAliases = touchedInstruments.Where(x => !aliasesByInstrument.ContainsKey(x)).ToList();
        if (missingAliases.Count > 0)
        {
            return Blocked(reportDate, venue.Id, brokerAccount.Id, InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_SYMBOL_ALIAS, $"Missing {LmaxReportAliasSource} alias for {missingAliases.Count} instrument(s).");
        }

        var instrumentsById = instruments.ToDictionary(x => x.Id);
        var rows = fillsForDate.Select(fill =>
        {
            var alias = aliasesByInstrument[fill.InstrumentId];
            var instrumentSymbol = instrumentsById.TryGetValue(fill.InstrumentId, out var instrument) ? instrument.Symbol : alias.ExternalSymbol.Replace("/", "", StringComparison.Ordinal);
            var signedBase = fill.Side == TradeSide.Buy ? fill.BaseQuantity : -fill.BaseQuantity;
            var absNotional = Math.Abs(fill.BaseQuantity * fill.Price);
            decimal? commission = commissionRateAbsNotional.HasValue ? Math.Round(absNotional * commissionRateAbsNotional.Value, 8) : null;
            return new InternalEodFillRow(reportDate, venue.Id, brokerAccount.Id, fill.BrokerExecutionId, fill.InstrumentId, instrumentSymbol, alias.ExternalSymbol, alias.ExternalInstrumentId, fill.Side, fill.BaseQuantity, signedBase, fill.VenueQuantity, fill.Price, absNotional, commission, fill.TradeDateUtc, fill.ReceivedAtUtc, HashRow(reportDate, fill.BrokerExecutionId, alias.ExternalSymbol, signedBase, fill.Price));
        }).ToList();

        var summaries = rows
            .GroupBy(x => new { x.InstrumentId, x.LmaxSymbol, x.Side })
            .Select(x => new InternalEodTradeSummaryRow(reportDate, brokerAccount.Id, x.Key.InstrumentId, x.Key.LmaxSymbol, x.Key.Side, x.Count(), x.Sum(row => row.BaseQuantity), x.Sum(row => row.SignedBaseQuantity), x.Sum(row => row.AbsNotional), SumNullable(x.Select(row => row.Commission))))
            .OrderBy(x => x.LmaxSymbol)
            .ThenBy(x => x.Side)
            .ToList();

        var anchorsByInstrument = priorPositionAnchors
            .Where(x => x.SourceKind == M3EodEvidenceSourceKind.LMAX_OFFICIAL_EOD_REPORT_FILE)
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(anchor => anchor.AsOfUtc).First());
        var missingAnchors = touchedInstruments.Where(x => !anchorsByInstrument.ContainsKey(x)).ToList();
        if (missingAnchors.Count > 0)
        {
            return new InternalEodReconstructionResult(reportDate, venue.Id, brokerAccount.Id, InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ANCHOR, false, false, rows, summaries, [], StandardCoverageGaps(), [$"Missing broker-confirmed prior position anchor for {missingAnchors.Count} instrument(s)."]);
        }

        var positions = touchedInstruments.Select(instrumentId =>
        {
            var anchor = anchorsByInstrument[instrumentId];
            var alias = aliasesByInstrument[instrumentId];
            var signedFillQuantity = rows.Where(x => x.InstrumentId == instrumentId).Sum(x => x.SignedBaseQuantity);
            return new InternalEodPositionRow(reportDate, brokerAccount.Id, instrumentId, alias.ExternalSymbol, anchor.BaseQuantity, signedFillQuantity, anchor.BaseQuantity + signedFillQuantity, anchor.SourceHash, true);
        }).OrderBy(x => x.LmaxSymbol).ToList();

        return new InternalEodReconstructionResult(reportDate, venue.Id, brokerAccount.Id, InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTED_COMPLETE, false, false, rows, summaries, positions, StandardCoverageGaps(), []);
    }

    private static InternalEodReconstructionResult Blocked(DateOnly reportDate, VenueId? venueId, BrokerAccountId? brokerAccountId, InternalEodReconstructionStatus status, string reason)
        => new(reportDate, venueId, brokerAccountId, status, false, false, [], [], [], StandardCoverageGaps(), [reason]);

    private static IReadOnlyList<string> StandardCoverageGaps()
        => ["cash_movements_not_reconstructed_from_broker_wallet", "fees_financing_dividends_require_official_lmax_eod_reports", "open_orders_not_authoritative_from_eod_reconstruction"];

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        var material = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return material.Count == 0 ? null : material.Sum();
    }

    private static string HashRow(DateOnly reportDate, string executionId, string lmaxSymbol, decimal signedBaseQuantity, decimal price)
    {
        var text = string.Join("|", reportDate.ToString("yyyy-MM-dd"), executionId, lmaxSymbol, signedBaseQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture), price.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}

public static class DualSourceEodReconciler
{
    public static DualSourceEodReconciliationResult Compare(
        DateOnly reportDate,
        BrokerAccount? brokerAccount,
        IReadOnlyList<LmaxIndividualTrade> lmaxTrades,
        InternalEodReconstructionResult internalReconstruction,
        decimal quantityTolerance = 0.0001m,
        decimal priceTolerance = 0.0000001m,
        decimal amountTolerance = 0.01m,
        decimal commissionTolerance = 0.01m)
    {
        var breaks = new List<DualSourceEodBreak>();
        if (internalReconstruction.Status == InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ACCOUNT_MAPPING)
        {
            breaks.Add(New(DualSourceEodBreakType.ACCOUNT_MAPPING_MISSING, "Broker account mapping is missing.", null, null));
        }

        if (internalReconstruction.Status == InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_SYMBOL_ALIAS)
        {
            breaks.Add(New(DualSourceEodBreakType.SYMBOL_ALIAS_MISSING, "LMAX_REPORT symbol alias is missing.", null, null));
        }

        if (internalReconstruction.Status is InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTED_PARTIAL or InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ANCHOR)
        {
            breaks.Add(New(DualSourceEodBreakType.UNKNOWN_RECONSTRUCTION_COVERAGE, "Internal reconstruction lacks complete broker-confirmed prior anchors or coverage.", null, null));
        }

        if (lmaxTrades.Count == 0 && internalReconstruction.Fills.Count > 0)
        {
            breaks.Add(New(DualSourceEodBreakType.MISSING_LMAX_REPORT, "No LMAX official individual-trades rows are present for internal fills.", null, null));
        }

        var lmaxByExecutionId = lmaxTrades.Where(x => x.ReportDate == reportDate).GroupBy(x => x.ExecutionId).ToDictionary(x => x.Key, x => x.First());
        var internalByExecutionId = internalReconstruction.Fills.GroupBy(x => x.BrokerExecutionId).ToDictionary(x => x.Key, x => x.First());

        foreach (var internalFill in internalReconstruction.Fills)
        {
            if (!lmaxByExecutionId.TryGetValue(internalFill.BrokerExecutionId, out var lmaxTrade))
            {
                breaks.Add(New(DualSourceEodBreakType.EXTRA_INTERNAL_FILL, $"Internal fill {internalFill.BrokerExecutionId} has no LMAX official EOD row.", internalFill.BrokerExecutionId, internalFill.InstrumentId, internalFill.BaseQuantity, null));
                continue;
            }

            if (Math.Abs(Math.Abs(lmaxTrade.UnitsBoughtSold) - internalFill.BaseQuantity) > quantityTolerance)
            {
                breaks.Add(New(DualSourceEodBreakType.QUANTITY_MISMATCH, $"Quantity mismatch for {internalFill.BrokerExecutionId}.", internalFill.BrokerExecutionId, internalFill.InstrumentId, internalFill.BaseQuantity, Math.Abs(lmaxTrade.UnitsBoughtSold)));
            }

            if (Math.Abs(lmaxTrade.TradePrice - internalFill.Price) > priceTolerance)
            {
                breaks.Add(New(DualSourceEodBreakType.PRICE_MISMATCH, $"Price mismatch for {internalFill.BrokerExecutionId}.", internalFill.BrokerExecutionId, internalFill.InstrumentId, internalFill.Price, lmaxTrade.TradePrice));
            }

            if (Math.Abs(Math.Abs(lmaxTrade.NotionalValue) - internalFill.AbsNotional) > amountTolerance)
            {
                breaks.Add(New(DualSourceEodBreakType.AMOUNT_MISMATCH, $"Notional amount mismatch for {internalFill.BrokerExecutionId}.", internalFill.BrokerExecutionId, internalFill.InstrumentId, internalFill.AbsNotional, Math.Abs(lmaxTrade.NotionalValue)));
            }

            if (internalFill.Commission.HasValue && Math.Abs(Math.Abs(lmaxTrade.TotalCommission) - Math.Abs(internalFill.Commission.Value)) > commissionTolerance)
            {
                breaks.Add(New(DualSourceEodBreakType.COMMISSION_MISMATCH, $"Commission mismatch for {internalFill.BrokerExecutionId}.", internalFill.BrokerExecutionId, internalFill.InstrumentId, Math.Abs(internalFill.Commission.Value), Math.Abs(lmaxTrade.TotalCommission)));
            }
        }

        foreach (var lmaxTrade in lmaxByExecutionId.Values.Where(x => !internalByExecutionId.ContainsKey(x.ExecutionId)))
        {
            breaks.Add(New(DualSourceEodBreakType.EXTRA_LMAX_FILL, $"LMAX official EOD fill {lmaxTrade.ExecutionId} has no internal fill.", lmaxTrade.ExecutionId, lmaxTrade.InstrumentId, null, Math.Abs(lmaxTrade.UnitsBoughtSold)));
        }

        return new DualSourceEodReconciliationResult(reportDate, brokerAccount?.Id ?? internalReconstruction.BrokerAccountId, internalReconstruction.Fills.Count, lmaxTrades.Count, breaks);

        static DualSourceEodBreak New(DualSourceEodBreakType type, string description, string? executionId, InstrumentId? instrumentId, decimal? internalValue = null, decimal? lmaxValue = null)
            => new(type, type is DualSourceEodBreakType.UNKNOWN_RECONSTRUCTION_COVERAGE ? DualSourceEodBreakSeverity.Warning : DualSourceEodBreakSeverity.Blocking, description, executionId, instrumentId, internalValue, lmaxValue);
    }
}
