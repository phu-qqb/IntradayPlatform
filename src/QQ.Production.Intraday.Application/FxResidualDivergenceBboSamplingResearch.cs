namespace QQ.Production.Intraday.Application;

public enum FxBboSamplingRejectReasonResearch
{
    AcceptedSynchronizedMidpoint,
    MissingSymbol,
    MissingQuoteAtOrBeforeGridTime,
    QuoteTooStale,
    NonPositiveBid,
    NonPositiveAsk,
    CrossedQuote,
    UnknownTimestampSemantics,
    AmbiguousDuplicateQuoteTimestamp,
    MissingRequiredPeer,
    SpreadTooWide,
    UnsafeDataSurface,
    BlockedNoSafeBboLoader
}

public sealed record FxBboQuoteResearch(
    string Symbol,
    DateTimeOffset TimestampUtc,
    decimal Bid,
    decimal Ask,
    long? SequenceId = null,
    string? SourceEventId = null,
    DateTimeOffset? AvailableAtUtc = null,
    string? Source = null,
    string? Venue = null,
    bool IsTimestampUtcKnown = true,
    bool IsHistorical = true,
    bool IsCompletedEvent = true);

public sealed record FxBboSamplingParametersResearch(
    IReadOnlyList<string> Symbols,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeSpan GridInterval,
    TimeSpan MaxQuoteAge,
    bool RequireAllSymbols = true,
    bool RequireExactGridAlignment = true,
    decimal? MaxSpreadBps = null)
{
    public static FxBboSamplingParametersResearch OneMinute(
        IReadOnlyList<string> symbols,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
        => new(symbols, startUtc, endUtc, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
}

public sealed record FxSynchronizedMidpointObservationResearch(
    DateTimeOffset TimestampUtc,
    IReadOnlyDictionary<string, decimal> Midpoints,
    IReadOnlyDictionary<string, decimal> Bids,
    IReadOnlyDictionary<string, decimal> Asks,
    IReadOnlyDictionary<string, DateTimeOffset> SourceQuoteTimestampsUtc,
    IReadOnlyDictionary<string, TimeSpan> QuoteAges,
    IReadOnlyDictionary<string, decimal> SpreadBps);

public sealed record FxBboSamplingDiagnosticResearch(
    DateTimeOffset GridTimestampUtc,
    string? Symbol,
    FxBboSamplingRejectReasonResearch Reason,
    DateTimeOffset? SourceQuoteTimestampUtc = null,
    DateTimeOffset? AvailableAtUtc = null,
    TimeSpan? QuoteAge = null);

public sealed record FxBboSamplingResultResearch(
    IReadOnlyList<FxSynchronizedMidpointObservationResearch> Observations,
    IReadOnlyList<FxBboSamplingDiagnosticResearch> Diagnostics)
{
    public IReadOnlyDictionary<FxBboSamplingRejectReasonResearch, int> ReasonCounts
        => Diagnostics
            .GroupBy(x => x.Reason)
            .ToDictionary(x => x.Key, x => x.Count());
}

public sealed class FxBboToSynchronizedMidpointSamplerResearch
{
    public FxBboSamplingResultResearch Sample(
        IReadOnlyList<FxBboQuoteResearch> quotes,
        FxBboSamplingParametersResearch parameters)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(parameters);

        var symbols = parameters.Symbols
            .Select(NormalizeSymbol)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (symbols.Length == 0 ||
            parameters.GridInterval <= TimeSpan.Zero ||
            parameters.MaxQuoteAge < TimeSpan.Zero ||
            parameters.StartUtc.Offset != TimeSpan.Zero ||
            parameters.EndUtc.Offset != TimeSpan.Zero ||
            parameters.EndUtc < parameters.StartUtc)
        {
            return new([], [new(parameters.StartUtc, null, FxBboSamplingRejectReasonResearch.UnsafeDataSurface)]);
        }

        var indexedQuotes = quotes
            .Select((quote, index) => new IndexedQuote(quote with { Symbol = NormalizeSymbol(quote.Symbol) }, index))
            .Where(x => x.Quote.Symbol.Length > 0)
            .ToArray();

        var quotesBySymbol = indexedQuotes
            .GroupBy(x => x.Quote.Symbol, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var observations = new List<FxSynchronizedMidpointObservationResearch>();
        var diagnostics = new List<FxBboSamplingDiagnosticResearch>();

        foreach (var symbol in symbols)
        {
            if (!quotesBySymbol.ContainsKey(symbol))
            {
                diagnostics.Add(new(parameters.StartUtc, symbol, FxBboSamplingRejectReasonResearch.MissingSymbol));
            }
        }

        for (var grid = parameters.StartUtc; grid <= parameters.EndUtc; grid = grid.Add(parameters.GridInterval))
        {
            var selected = new Dictionary<string, FxBboQuoteResearch>(StringComparer.Ordinal);
            var sourceTimes = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            var ages = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
            var midpoints = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var bids = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var asks = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var spreads = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var acceptedGrid = true;

            foreach (var symbol in symbols)
            {
                if (!quotesBySymbol.TryGetValue(symbol, out var symbolQuotes))
                {
                    diagnostics.Add(new(grid, symbol, FxBboSamplingRejectReasonResearch.MissingRequiredPeer));
                    acceptedGrid = false;
                    continue;
                }

                var selection = SelectQuote(symbolQuotes, grid, parameters);
                if (selection.Reason != FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint || selection.Quote is null)
                {
                    diagnostics.Add(new(
                        grid,
                        symbol,
                        selection.Reason,
                        selection.Quote?.TimestampUtc,
                        selection.Quote?.AvailableAtUtc,
                        selection.Quote is null ? null : grid - selection.Quote.TimestampUtc));
                    acceptedGrid = false;
                    continue;
                }

                var quote = selection.Quote;
                var midpoint = (quote.Bid + quote.Ask) / 2m;
                var spreadBps = midpoint == 0m ? 0m : (quote.Ask - quote.Bid) / midpoint * 10000m;

                selected[symbol] = quote;
                sourceTimes[symbol] = quote.TimestampUtc;
                ages[symbol] = grid - quote.TimestampUtc;
                midpoints[symbol] = midpoint;
                bids[symbol] = quote.Bid;
                asks[symbol] = quote.Ask;
                spreads[symbol] = spreadBps;
            }

            if (!acceptedGrid && parameters.RequireAllSymbols)
            {
                continue;
            }

            if (selected.Count == symbols.Length)
            {
                observations.Add(new(
                    grid,
                    midpoints,
                    bids,
                    asks,
                    sourceTimes,
                    ages,
                    spreads));
                diagnostics.Add(new(grid, null, FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint));
            }
        }

        return new(observations, diagnostics);
    }

    public IReadOnlyList<FxResidualDivergenceBar> ToResidualDivergenceBars(
        IEnumerable<FxSynchronizedMidpointObservationResearch> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        return observations
            .OrderBy(x => x.TimestampUtc)
            .SelectMany(x => x.Midpoints.Keys.Order(StringComparer.Ordinal).Select(symbol => new FxResidualDivergenceBar(
                symbol,
                x.TimestampUtc,
                x.Midpoints[symbol],
                IsCompletedBar: true,
                Bid: x.Bids[symbol],
                Ask: x.Asks[symbol],
                SpreadBps: x.SpreadBps[symbol],
                ExchangeTimeZone: "UTC")))
            .ToArray();
    }

    private static QuoteSelection SelectQuote(
        IReadOnlyList<IndexedQuote> symbolQuotes,
        DateTimeOffset grid,
        FxBboSamplingParametersResearch parameters)
    {
        var eligible = symbolQuotes
            .Where(x => x.Quote.TimestampUtc <= grid)
            .Where(x => x.Quote.AvailableAtUtc is null || x.Quote.AvailableAtUtc.Value <= grid)
            .GroupBy(x => x.Quote.TimestampUtc)
            .OrderByDescending(x => x.Key)
            .ToArray();

        if (eligible.Length == 0)
        {
            return new(null, FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
        }

        foreach (var timestampGroup in eligible)
        {
            var duplicateResolution = ResolveDuplicateTimestamp(timestampGroup.ToArray());
            if (duplicateResolution.Reason != FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint)
            {
                return duplicateResolution;
            }

            var quote = duplicateResolution.Quote!;
            var validationReason = ValidateQuoteAtGrid(quote, grid, parameters);
            return validationReason == FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint
                ? new(quote, validationReason)
                : new(quote, validationReason);
        }

        return new(null, FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
    }

    private static QuoteSelection ResolveDuplicateTimestamp(IReadOnlyList<IndexedQuote> quotes)
    {
        if (quotes.Count == 1)
        {
            return new(quotes[0].Quote, FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint);
        }

        if (quotes.All(x => x.Quote.SequenceId.HasValue))
        {
            var ordered = quotes
                .OrderBy(x => x.Quote.SequenceId!.Value)
                .ThenBy(x => x.SourceOrder)
                .ToArray();
            var maxSequence = ordered[^1].Quote.SequenceId!.Value;
            var maxSequenceQuotes = ordered.Where(x => x.Quote.SequenceId!.Value == maxSequence).ToArray();
            if (maxSequenceQuotes.Select(x => (x.Quote.Bid, x.Quote.Ask, x.Quote.AvailableAtUtc)).Distinct().Count() > 1)
            {
                return new(maxSequenceQuotes[0].Quote, FxBboSamplingRejectReasonResearch.AmbiguousDuplicateQuoteTimestamp);
            }

            return new(maxSequenceQuotes[^1].Quote, FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint);
        }

        var distinctValues = quotes
            .Select(x => (x.Quote.Bid, x.Quote.Ask, x.Quote.AvailableAtUtc))
            .Distinct()
            .Count();
        if (distinctValues > 1)
        {
            return new(quotes[0].Quote, FxBboSamplingRejectReasonResearch.AmbiguousDuplicateQuoteTimestamp);
        }

        return new(quotes.OrderBy(x => x.SourceOrder).First().Quote, FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint);
    }

    private static FxBboSamplingRejectReasonResearch ValidateQuoteAtGrid(
        FxBboQuoteResearch quote,
        DateTimeOffset grid,
        FxBboSamplingParametersResearch parameters)
    {
        if (!quote.IsTimestampUtcKnown ||
            !quote.IsHistorical ||
            !quote.IsCompletedEvent ||
            quote.TimestampUtc.Offset != TimeSpan.Zero ||
            quote.AvailableAtUtc is { Offset: var availableOffset } && availableOffset != TimeSpan.Zero)
        {
            return FxBboSamplingRejectReasonResearch.UnknownTimestampSemantics;
        }

        if (quote.Bid <= 0m)
        {
            return FxBboSamplingRejectReasonResearch.NonPositiveBid;
        }

        if (quote.Ask <= 0m)
        {
            return FxBboSamplingRejectReasonResearch.NonPositiveAsk;
        }

        if (quote.Ask < quote.Bid)
        {
            return FxBboSamplingRejectReasonResearch.CrossedQuote;
        }

        var age = grid - quote.TimestampUtc;
        if (age < TimeSpan.Zero)
        {
            return FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime;
        }

        if (age > parameters.MaxQuoteAge)
        {
            return FxBboSamplingRejectReasonResearch.QuoteTooStale;
        }

        var midpoint = (quote.Bid + quote.Ask) / 2m;
        var spreadBps = midpoint == 0m ? 0m : (quote.Ask - quote.Bid) / midpoint * 10000m;
        if (parameters.MaxSpreadBps is not null && spreadBps > parameters.MaxSpreadBps.Value)
        {
            return FxBboSamplingRejectReasonResearch.SpreadTooWide;
        }

        return FxBboSamplingRejectReasonResearch.AcceptedSynchronizedMidpoint;
    }

    private static string NormalizeSymbol(string? symbol)
        => string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.Trim().ToUpperInvariant();

    private sealed record IndexedQuote(FxBboQuoteResearch Quote, int SourceOrder);

    private sealed record QuoteSelection(FxBboQuoteResearch? Quote, FxBboSamplingRejectReasonResearch Reason);
}
