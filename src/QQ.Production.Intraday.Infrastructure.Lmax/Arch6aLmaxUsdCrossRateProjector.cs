using System.Globalization;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record Arch6aLmaxFxQuote(
    string InstrumentId,
    string SecurityId,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Bid,
    decimal Ask,
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset ReceivedAtUtc,
    string SourceResponseSha256,
    string SourceSystem = "LMAX");

public sealed record Arch6aLmaxFxProjectionLeg(
    string InstrumentId,
    string SecurityId,
    string SourceSymbol,
    bool Inverted,
    decimal NormalizedBid,
    decimal NormalizedAsk,
    DateTimeOffset SourceTimestampUtc,
    string SourceResponseSha256);

public sealed record Arch6aLmaxFxProjectedQuote(
    string Symbol,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    DateTimeOffset AsOfUtc,
    string SourceSystem,
    string ProjectionMethod,
    bool IsReconstructed,
    TimeSpan MaximumLegSkew,
    IReadOnlyList<Arch6aLmaxFxProjectionLeg> Provenance);

public sealed class Arch6aLmaxUsdCrossRateProjector
{
    public Arch6aLmaxFxProjectedQuote Project(
        string targetBaseCurrency,
        string targetQuoteCurrency,
        IReadOnlyList<Arch6aLmaxFxQuote> quotes,
        TimeSpan maximumLegSkew,
        TimeSpan? maximumQuoteAge = null)
    {
        var targetBase = NormalizeCurrency(targetBaseCurrency);
        var targetQuote = NormalizeCurrency(targetQuoteCurrency);
        if (targetBase == targetQuote) throw new InvalidOperationException("ARCH6A_LMAX_TARGET_PAIR_IDENTICAL_CURRENCIES");
        if (maximumLegSkew < TimeSpan.Zero) throw new InvalidOperationException("ARCH6A_LMAX_MAXIMUM_LEG_SKEW_INVALID");
        var effectiveMaximumQuoteAge = maximumQuoteAge ?? TimeSpan.FromSeconds(1);
        if (effectiveMaximumQuoteAge < TimeSpan.Zero) throw new InvalidOperationException("ARCH6A_LMAX_MAXIMUM_QUOTE_AGE_INVALID");
        foreach (var quote in quotes) ValidateQuote(quote, effectiveMaximumQuoteAge);

        var direct = FindUnique(quotes, targetBase, targetQuote);
        if (direct is not null)
        {
            return BuildSingleLeg(targetBase, targetQuote, direct, inverted: false, "LMAX_DIRECT");
        }

        var inverse = FindUnique(quotes, targetQuote, targetBase);
        if (inverse is not null)
        {
            return BuildSingleLeg(targetBase, targetQuote, inverse, inverted: true, "LMAX_DIRECT_INVERTED");
        }

        if (targetBase == "USD" || targetQuote == "USD")
        {
            throw new InvalidOperationException($"ARCH6A_LMAX_DIRECT_USD_LEG_MISSING:{targetBase}{targetQuote}");
        }

        var baseUsd = RequireUsdLeg(targetBase, quotes);
        var quoteUsd = RequireUsdLeg(targetQuote, quotes);
        var skew = (baseUsd.Source.SourceTimestampUtc - quoteUsd.Source.SourceTimestampUtc).Duration();
        if (skew > maximumLegSkew)
        {
            throw new InvalidOperationException($"ARCH6A_LMAX_USD_LEG_SKEW_EXCEEDED:{skew.TotalMilliseconds:0}");
        }

        var bid = baseUsd.Bid / quoteUsd.Ask;
        var ask = baseUsd.Ask / quoteUsd.Bid;
        if (ask < bid) throw new InvalidOperationException("ARCH6A_LMAX_CROSS_BID_ASK_INVALID");
        var provenance = new[]
        {
            ToProjectionLeg(baseUsd),
            ToProjectionLeg(quoteUsd)
        };
        return new(
            targetBase + targetQuote,
            bid,
            ask,
            (bid + ask) / 2m,
            provenance.Max(item => item.SourceTimestampUtc),
            "LMAX",
            "LMAX_USD_TWO_LEG_CROSS_V1",
            true,
            skew,
            provenance);
    }

    public static decimal ParseObservedPrice(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0m)
        {
            throw new InvalidDataException("ARCH6A_LMAX_PRICE_NOT_FINITE_OR_POSITIVE");
        }

        return parsed;
    }

    private static Arch6aLmaxFxProjectedQuote BuildSingleLeg(
        string targetBase,
        string targetQuote,
        Arch6aLmaxFxQuote quote,
        bool inverted,
        string method)
    {
        var bid = inverted ? 1m / quote.Ask : quote.Bid;
        var ask = inverted ? 1m / quote.Bid : quote.Ask;
        return new(
            targetBase + targetQuote,
            bid,
            ask,
            (bid + ask) / 2m,
            quote.SourceTimestampUtc,
            "LMAX",
            method,
            inverted,
            TimeSpan.Zero,
            [
                new(
                    quote.InstrumentId,
                    quote.SecurityId,
                    Symbol(quote),
                    inverted,
                    bid,
                    ask,
                    quote.SourceTimestampUtc,
                    quote.SourceResponseSha256)
            ]);
    }

    private static NormalizedUsdLeg RequireUsdLeg(
        string currency,
        IReadOnlyList<Arch6aLmaxFxQuote> quotes)
    {
        var direct = FindUnique(quotes, currency, "USD");
        var inverse = FindUnique(quotes, "USD", currency);
        if (direct is not null && inverse is not null)
        {
            throw new InvalidOperationException($"ARCH6A_LMAX_AMBIGUOUS_USD_LEG:{currency}");
        }
        if (direct is not null) return new(direct, false, direct.Bid, direct.Ask);
        if (inverse is not null) return new(inverse, true, 1m / inverse.Ask, 1m / inverse.Bid);
        throw new InvalidOperationException($"ARCH6A_LMAX_USD_LEG_MISSING:{currency}");
    }

    private static Arch6aLmaxFxQuote? FindUnique(
        IReadOnlyList<Arch6aLmaxFxQuote> quotes,
        string baseCurrency,
        string quoteCurrency)
    {
        var matches = quotes
            .Where(item =>
                NormalizeCurrency(item.BaseCurrency) == baseCurrency &&
                NormalizeCurrency(item.QuoteCurrency) == quoteCurrency)
            .ToList();
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException($"ARCH6A_LMAX_DUPLICATE_QUOTE:{baseCurrency}{quoteCurrency}")
        };
    }

    private static Arch6aLmaxFxProjectionLeg ToProjectionLeg(NormalizedUsdLeg leg)
        => new(
            leg.Source.InstrumentId,
            leg.Source.SecurityId,
            Symbol(leg.Source),
            leg.Inverted,
            leg.Bid,
            leg.Ask,
            leg.Source.SourceTimestampUtc,
            leg.Source.SourceResponseSha256);

    private static void ValidateQuote(Arch6aLmaxFxQuote quote, TimeSpan maximumQuoteAge)
    {
        if (!quote.SourceSystem.Equals("LMAX", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ARCH6A_LMAX_NON_LMAX_QUOTE_REJECTED");
        }
        if (string.IsNullOrWhiteSpace(quote.InstrumentId) ||
            string.IsNullOrWhiteSpace(quote.SecurityId) ||
            !IsSha256(quote.SourceResponseSha256))
        {
            throw new InvalidOperationException("ARCH6A_LMAX_QUOTE_PROVENANCE_INCOMPLETE");
        }
        if (quote.Bid <= 0m || quote.Ask <= 0m || quote.Ask < quote.Bid)
        {
            throw new InvalidOperationException($"ARCH6A_LMAX_QUOTE_INVALID:{Symbol(quote)}");
        }
        if (quote.SourceTimestampUtc.Offset != TimeSpan.Zero || quote.ReceivedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("ARCH6A_LMAX_QUOTE_TIMESTAMP_MUST_BE_UTC");
        }
        if (quote.SourceTimestampUtc > quote.ReceivedAtUtc)
        {
            throw new InvalidOperationException("ARCH6A_LMAX_QUOTE_TIMESTAMP_ORDER_INVALID");
        }
        if (quote.ReceivedAtUtc - quote.SourceTimestampUtc > maximumQuoteAge)
        {
            throw new InvalidOperationException($"ARCH6A_LMAX_QUOTE_STALE:{Symbol(quote)}");
        }
        var sourceBase = NormalizeCurrency(quote.BaseCurrency);
        var sourceQuote = NormalizeCurrency(quote.QuoteCurrency);
        if (sourceBase == sourceQuote)
        {
            throw new InvalidOperationException("ARCH6A_LMAX_SOURCE_PAIR_IDENTICAL_CURRENCIES");
        }
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(character => character is >= 'A' and <= 'Z')
            ? normalized
            : throw new InvalidOperationException($"ARCH6A_LMAX_CURRENCY_INVALID:{value}");
    }

    private static string Symbol(Arch6aLmaxFxQuote value)
        => NormalizeCurrency(value.BaseCurrency) + NormalizeCurrency(value.QuoteCurrency);

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private sealed record NormalizedUsdLeg(
        Arch6aLmaxFxQuote Source,
        bool Inverted,
        decimal Bid,
        decimal Ask);
}
