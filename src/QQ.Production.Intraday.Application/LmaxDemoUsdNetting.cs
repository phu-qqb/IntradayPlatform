using System.Globalization;
using QQ.Production.Intraday.Domain;
using QubesRunId = QQ.Production.Intraday.Domain.PmsEmsOmsFoundation.QubesRunId;

namespace QQ.Production.Intraday.Application;

/// <summary>Connects the existing Qubes currency netting to a genuinely observed
/// Demo USD-pair scope. The historical netting service is pure: its fixture batch
/// request is deliberately ignored; only its validated currency exposures are used.</summary>
public static class LmaxDemoUsdNetting
{
    public const string BatchPrefix = "legacy_anubis_portfolio_usdnet_v1_";
    public static bool IsNettedRun(ModelRun run)
        => run.SourceFileName.StartsWith("legacy-anubis:" + BatchPrefix, StringComparison.Ordinal);

    public static string Currency(string symbol)
    {
        if (symbol.Length != 6 || symbol.Any(c => c is < 'A' or > 'Z') || symbol == "USDUSD"
            || !(symbol.StartsWith("USD", StringComparison.Ordinal) ^ symbol.EndsWith("USD", StringComparison.Ordinal)))
            throw new DomainRuleViolationException("Demo netted execution requires native USD pairs: " + symbol);
        return symbol.StartsWith("USD", StringComparison.Ordinal) ? symbol[3..] : symbol[..3];
    }

    public static void ValidateScope(IReadOnlyList<string> scope)
    {
        if (scope.Count == 0 || scope.Select(Currency).Distinct(StringComparer.Ordinal).Count() != scope.Count)
            throw new DomainRuleViolationException("Demo netted execution scope is empty or has duplicate currency legs.");
    }

    public static IReadOnlyDictionary<string, decimal> Net(
        IEnumerable<(string Symbol, decimal Weight)> allContributions,
        IReadOnlyList<string> scope, DateTimeOffset decisionAt, DateTimeOffset effectiveAt, decimal navUsd)
    {
        ValidateScope(scope);
        // No pre-netting instrument filter, coefficient reapplication, scaling or carry-forward.
        var lines = allContributions.Select(x => x.Symbol + " Curncy;" + x.Weight.ToString("G29", CultureInfo.InvariantCulture)).ToArray();
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new(
            new QubesRunId("lmax-demo-usd-netting:" + decisionAt.ToString("O")), decisionAt, effectiveAt, 15,
            "QQ Intraday Fund", "IntradayFxModel", navUsd, TargetQuantityMode.PortfolioBaseCurrencyNotional, lines));
        if (!normalized.Succeeded)
            throw new DomainRuleViolationException("Existing FX netting rejected input: " + string.Join(',', normalized.Issues.Select(x => x.Code)));
        var byCurrency = scope.ToDictionary(Currency, StringComparer.Ordinal);
        var missing = normalized.CurrencyExposures.Keys.Where(x => x != "USD" && !byCurrency.ContainsKey(x)).Order().ToArray();
        if (missing.Length != 0)
            throw new DomainRuleViolationException("Full Anubis currency scope is not executable: " + string.Join(',', missing));
        // This is the same orientation as ARCH7A: direct target = w*NAV/mid;
        // inverted target = -w*NAV USD. Zero legs remain explicit to close old positions.
        return scope.Order(StringComparer.Ordinal).ToDictionary(x => x,
            x => normalized.CurrencyExposures.GetValueOrDefault(Currency(x)) * (x.StartsWith("USD", StringComparison.Ordinal) ? -1m : 1m),
            StringComparer.Ordinal);
    }

    public static decimal BaseUnitValueUsd(Instrument instrument, MarketDataSnapshot market)
    {
        _ = Currency(instrument.Symbol);
        if (instrument.Symbol != instrument.BaseCurrency.Code + instrument.QuoteCurrency.Code)
            throw new DomainRuleViolationException("Netted USD pair currency metadata is inconsistent.");
        market.Validate();
        if (market.InstrumentId != instrument.Id)
            throw new DomainRuleViolationException("Netted USD pair price belongs to another instrument.");
        return instrument.BaseCurrency.Code == "USD" ? 1m : market.Mid;
    }

    public static TargetPosition Size(ModelRun run, TargetWeight weight, Instrument instrument,
        MarketDataSnapshot market, VenueInstrumentMapping mapping)
    {
        if (!IsNettedRun(run) || run.TargetQuantityMode != TargetQuantityMode.PortfolioBaseCurrencyNotional
            || weight.InstrumentId != instrument.Id || mapping.InstrumentId != instrument.Id
            || mapping.VenueId != market.VenueId || mapping.ContractSize <= 0m
            || mapping.VenueSymbol != instrument.Symbol
            || mapping.VenueInstrumentCode != instrument.Symbol.Insert(3, "/"))
            throw new DomainRuleViolationException("Netted USD target provenance or contract is invalid.");
        var unitUsd = BaseUnitValueUsd(instrument, market);
        var notionalUsd = weight.Weight * run.NavUsd;
        var venueQuantity = QuantityRounding.RoundToStep(notionalUsd / unitUsd / mapping.ContractSize, mapping.QuantityStep);
        if (venueQuantity != 0m && Math.Abs(venueQuantity) < mapping.MinOrderQuantity)
            throw new DomainRuleViolationException("Netted USD target is below venue minimum quantity.");
        return new(run.Id, instrument.Id, notionalUsd, venueQuantity * mapping.ContractSize, venueQuantity, run.TargetQuantityMode);
    }

    public static decimal GrossExposure(PlatformState state, FundId fund, VenueId venue, DateTimeOffset now, TimeSpan maximumAge)
    {
        decimal gross = 0m;
        foreach (var position in state.PositionLedger.Where(x => x.FundId == fund && x.CreatedAtUtc <= now)
                     .GroupBy(x => x.InstrumentId).Select(x => (Id: x.Key, Quantity: x.Sum(y => y.BaseQuantityDelta))).Where(x => x.Quantity != 0m))
        {
            var instrument = state.Instruments.Single(x => x.Id == position.Id);
            var market = state.MarketData.Where(x => x.InstrumentId == position.Id && x.VenueId == venue).MaxBy(x => x.ReceivedAtUtc)
                ?? throw new DomainRuleViolationException("Netted USD gross exposure is missing an instrument price.");
            if (market.IsStale(maximumAge, now))
                throw new DomainRuleViolationException("Netted USD gross exposure has a stale instrument price.");
            gross += Math.Abs(position.Quantity) * BaseUnitValueUsd(instrument, market);
        }
        return gross;
    }
}
