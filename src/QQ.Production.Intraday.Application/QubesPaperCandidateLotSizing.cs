using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperInstrumentConventionStatus
{
    Available,
    RequiresInstrumentConvention,
    RequiresInstrumentConventionReview,
    InconclusiveSafe
}

public enum PaperQuantityRoundingMode
{
    RoundToNearestLot,
    FloorToLot,
    None
}

public enum PaperLotSizingStatus
{
    PaperSized,
    RequiresMark,
    RequiresLotSizing,
    RequiresInstrumentConvention,
    BlockedByRisk,
    NonExecutable,
    InconclusiveSafe
}

public sealed record PaperFxInstrumentConvention(
    string NormalizedSymbol,
    string PaperTradableSymbol,
    string BaseCurrency,
    string QuoteCurrency,
    string NormalizedQuoteCurrency,
    bool IsUsdQuoteNormalized,
    bool RequiresInversion,
    string QuantityCurrency,
    string NotionalCurrency,
    decimal? LotSize,
    decimal? MinQuantity,
    PaperQuantityRoundingMode QuantityRoundingMode,
    string ConventionSource,
    PaperInstrumentConventionStatus Status);

public sealed record PaperLotSizingFixtureMark(
    string NormalizedSymbol,
    decimal FixtureMid,
    string FixtureSource,
    bool IsNoExternalFixture,
    bool IsSafeSummary);

public sealed record PaperQuantityShape(
    PaperOrderCandidateId PaperOrderCandidateId,
    string NormalizedSymbol,
    decimal? DeltaNotionalUsd,
    decimal? FixtureMid,
    decimal? AbsoluteBaseQuantityUnrounded,
    decimal? AbsoluteBaseQuantityRounded,
    decimal? LotSize,
    PaperQuantityRoundingMode RoundingMode,
    string QuantityCurrency,
    PaperLotSizingStatus Status,
    string StatusReason,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record PaperLotSizedCandidate(
    PaperOrderCandidateId PaperOrderCandidateId,
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string SourceRebalanceIntentId,
    PaperOrderCandidateRiskReference RiskReviewReference,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    string? PaperTradableSymbol,
    IntentSide Side,
    decimal TargetWeight,
    decimal CurrentWeight,
    decimal DeltaWeight,
    decimal? TargetNotional,
    decimal? CurrentNotional,
    decimal? DeltaNotional,
    PaperFxInstrumentConvention? InstrumentConvention,
    PaperQuantityShape QuantityShape,
    PaperLotSizingStatus SizingStatus,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool CreatesFill,
    bool CreatesExecutionReport);

public sealed record PaperLotSizingFixtureContract(
    string FixtureSource,
    IReadOnlyDictionary<string, PaperLotSizingFixtureMark> MarksByNormalizedSymbol,
    IReadOnlyDictionary<string, PaperFxInstrumentConvention> ConventionsByNormalizedSymbol,
    bool NoExternalFixtureOnly,
    bool RawBrokerPricesSerialized);

public sealed record PaperLotSizedCandidateBatch(
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string PaperRiskReviewId,
    IReadOnlyList<PaperLotSizedCandidate> SizedCandidates,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    int PaperSizedCount,
    int RequiresMarkCount,
    int RequiresLotSizingCount,
    int RequiresInstrumentConventionCount,
    bool QubesLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool BlockedLinesPreserved,
    bool SizedCandidatesAreNonExecutable,
    bool SizedCandidatesAreNotOrders,
    bool SizedCandidatesAreNotSubmitted,
    bool SizedCandidatesHaveNoBrokerRoute,
    bool CreatedOmsOrder,
    bool CreatedParentOrder,
    bool CreatedChildOrder,
    bool CreatedBrokerOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool MutatedLiveTradingState);

public sealed record PaperLotSizingRequest(
    PaperOrderCandidateArchiveResult Archive,
    PaperLotSizingFixtureContract Fixture);

public sealed class PaperLotSizingService
{
    public PaperLotSizedCandidateBatch Size(PaperLotSizingRequest request)
    {
        var candidates = request.Archive.ArchiveRecord.CandidateLines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => SizeCandidate(request.Archive.ArchiveRecord, request.Fixture, x))
            .ToArray();

        return new PaperLotSizedCandidateBatch(
            request.Archive.ArchiveRecord.PaperOrderCandidateBatchId,
            request.Archive.ArchiveRecord.CycleRunId,
            request.Archive.ArchiveRecord.QubesRunId,
            request.Archive.ArchiveRecord.OperatorDecisionId,
            request.Archive.ArchiveRecord.PaperRiskReviewId,
            candidates,
            request.Archive.ArchiveRecord.BlockedLines,
            candidates.Count(x => x.SizingStatus == PaperLotSizingStatus.PaperSized),
            candidates.Count(x => x.SizingStatus == PaperLotSizingStatus.RequiresMark),
            candidates.Count(x => x.SizingStatus == PaperLotSizingStatus.RequiresLotSizing),
            candidates.Count(x => x.SizingStatus == PaperLotSizingStatus.RequiresInstrumentConvention),
            request.Archive.ArchiveRecord.QubesLineagePreserved,
            request.Archive.ArchiveRecord.OperatorDecisionLineagePreserved,
            request.Archive.ArchiveRecord.RiskLineagePreserved,
            request.Archive.ArchiveRecord.RebalanceIntentLineagePreserved,
            request.Archive.ArchiveRecord.MissingStaleMarkWarningsPreserved,
            request.Archive.ArchiveRecord.DriftAcknowledgementPreserved,
            request.Archive.ArchiveRecord.BlockedLines.Count > 0,
            candidates.All(x => x.NonExecutable),
            candidates.All(x => x.NotAnOrder),
            candidates.All(x => x.NotSubmitted),
            candidates.All(x => x.NoBrokerRoute),
            CreatedOmsOrder: false,
            CreatedParentOrder: false,
            CreatedChildOrder: false,
            CreatedBrokerOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            MutatedLiveTradingState: false);
    }

    public static PaperLotSizingFixtureContract CreateDefaultFixture(IEnumerable<string> normalizedSymbols)
    {
        var symbols = normalizedSymbols.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var conventions = CreateUsdQuoteConventions(symbols);
        var marks = new Dictionary<string, PaperLotSizingFixtureMark>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUDUSD"] = new("AUDUSD", 0.66m, "NoExternalLotSizingFixture", IsNoExternalFixture: true, IsSafeSummary: true),
            ["EURUSD"] = new("EURUSD", 1.08m, "NoExternalLotSizingFixture", IsNoExternalFixture: true, IsSafeSummary: true),
            ["GBPUSD"] = new("GBPUSD", 1.25m, "NoExternalLotSizingFixture", IsNoExternalFixture: true, IsSafeSummary: true)
        };

        return new PaperLotSizingFixtureContract(
            "NoExternalLotSizingFixture",
            marks.Where(x => symbols.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            conventions,
            NoExternalFixtureOnly: true,
            RawBrokerPricesSerialized: false);
    }

    public static IReadOnlyDictionary<string, PaperFxInstrumentConvention> CreateUsdQuoteConventions(IEnumerable<string> normalizedSymbols)
        => normalizedSymbols
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsUsdQuoteSymbol)
            .ToDictionary(
                x => x,
                x => new PaperFxInstrumentConvention(
                    x,
                    x,
                    x[..3],
                    "USD",
                    "USD",
                    IsUsdQuoteNormalized: true,
                    RequiresInversion: false,
                    QuantityCurrency: x[..3],
                    NotionalCurrency: "USD",
                    LotSize: 1_000m,
                    MinQuantity: 1_000m,
                    PaperQuantityRoundingMode.RoundToNearestLot,
                    "NoExternalPaperFxConventionFixture",
                    PaperInstrumentConventionStatus.Available),
                StringComparer.OrdinalIgnoreCase);

    private static PaperLotSizedCandidate SizeCandidate(
        PaperOrderCandidateBatchRecord archive,
        PaperLotSizingFixtureContract fixture,
        PaperOrderCandidateLineRecord candidate)
    {
        fixture.ConventionsByNormalizedSymbol.TryGetValue(candidate.NormalizedSymbol, out var convention);
        fixture.MarksByNormalizedSymbol.TryGetValue(candidate.NormalizedSymbol, out var mark);
        var (status, reason) = StatusFor(candidate, convention, mark);
        decimal? unrounded = status == PaperLotSizingStatus.PaperSized
            ? Math.Abs(candidate.DeltaNotional ?? 0m) / mark!.FixtureMid
            : null;
        decimal? rounded = status == PaperLotSizingStatus.PaperSized
            ? RoundQuantity(unrounded!.Value, convention!.LotSize!.Value, convention.QuantityRoundingMode)
            : null;
        var quantityShape = new PaperQuantityShape(
            candidate.PaperOrderCandidateId,
            candidate.NormalizedSymbol,
            candidate.DeltaNotional,
            status == PaperLotSizingStatus.PaperSized ? mark!.FixtureMid : null,
            unrounded,
            rounded,
            convention?.LotSize,
            convention?.QuantityRoundingMode ?? PaperQuantityRoundingMode.None,
            convention?.QuantityCurrency ?? "",
            status,
            reason,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true);

        return new PaperLotSizedCandidate(
            candidate.PaperOrderCandidateId,
            archive.PaperOrderCandidateBatchId,
            archive.CycleRunId,
            archive.QubesRunId,
            archive.OperatorDecisionId,
            candidate.SourceRebalanceIntentId,
            candidate.RiskReviewReference,
            candidate.InstrumentId,
            candidate.NormalizedSymbol,
            convention?.PaperTradableSymbol,
            candidate.Side,
            candidate.TargetWeight,
            candidate.CurrentWeight,
            candidate.DeltaWeight,
            candidate.TargetNotional,
            candidate.CurrentNotional,
            candidate.DeltaNotional,
            convention,
            quantityShape,
            status,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false);
    }

    private static (PaperLotSizingStatus Status, string Reason) StatusFor(
        PaperOrderCandidateLineRecord candidate,
        PaperFxInstrumentConvention? convention,
        PaperLotSizingFixtureMark? mark)
    {
        if (!candidate.PaperOnly || !candidate.NonExecutable || !candidate.NotAnOrder || !candidate.NotSubmitted || !candidate.NoBrokerRoute)
        {
            return (PaperLotSizingStatus.NonExecutable, "Candidate failed paper-only non-executable archive checks.");
        }

        if (convention is null || convention.Status != PaperInstrumentConventionStatus.Available)
        {
            return (PaperLotSizingStatus.RequiresInstrumentConvention, "No paper instrument convention is available for the normalized symbol.");
        }

        if (convention.LotSize is null or <= 0m || convention.MinQuantity is null or <= 0m || convention.QuantityRoundingMode == PaperQuantityRoundingMode.None)
        {
            return (PaperLotSizingStatus.RequiresLotSizing, "Paper lot-size convention is incomplete.");
        }

        if (mark is null || !mark.IsNoExternalFixture || !mark.IsSafeSummary || mark.FixtureMid <= 0m)
        {
            return (PaperLotSizingStatus.RequiresMark, "No safe no-external fixture mid is available for paper quantity shaping.");
        }

        return (PaperLotSizingStatus.PaperSized, "Paper quantity shape computed from safe no-external fixture convention and mark.");
    }

    private static decimal RoundQuantity(decimal quantity, decimal lotSize, PaperQuantityRoundingMode roundingMode)
        => roundingMode switch
        {
            PaperQuantityRoundingMode.RoundToNearestLot => Math.Round(quantity / lotSize, 0, MidpointRounding.AwayFromZero) * lotSize,
            PaperQuantityRoundingMode.FloorToLot => Math.Floor(quantity / lotSize) * lotSize,
            _ => quantity
        };

    private static bool IsUsdQuoteSymbol(string symbol)
        => symbol.Length == 6 &&
           symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase) &&
           symbol.All(c => c is >= 'A' and <= 'Z');
}
