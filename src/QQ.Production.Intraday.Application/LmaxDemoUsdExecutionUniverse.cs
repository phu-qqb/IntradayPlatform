using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>The full union of the existing INFX7/8/9/10 currency exposures.
/// Contract facts: official LMAX London instrument list retrieved 18 September 2026.
/// QuantityStep is QQ's conservative order quantum (one published minimum lot),
/// not a claim that the CSV contains a separate venue increment field.</summary>
public static class LmaxDemoUsdExecutionUniverse
{
    public const string SourceUrl = "https://assets.lmaxstatic.com/csv/LMAXGlobal-uk-Instruments-LD4.csv";
    public const string SourceSha256 = "5fe68bab4715034d85cb355a74f40f2d27b656acf7ff3c50418f1bdaf70be309";
    public sealed record Leg(string Symbol, string SecurityId, decimal ContractSize, decimal MinQuantity, decimal QuantityStep, decimal TickSize)
    {
        public string SlashSymbol => Symbol[..3] + "/" + Symbol[3..];
        public LmaxDemoSessionInstrument SessionInstrument => new(Symbol, SecurityId, ContractSize);
    }
    public static IReadOnlyList<Leg> Legs { get; } = Array.AsReadOnly(new[]
    {
        new Leg("EURUSD", "4001", 10000m, .1m, .1m, .00001m),
        new Leg("GBPUSD", "4002", 10000m, .1m, .1m, .00001m),
        new Leg("AUDUSD", "4007", 10000m, .1m, .1m, .00001m),
        new Leg("NZDUSD", "100613", 10000m, .1m, .1m, .00001m),
        new Leg("USDJPY", "4004", 10000m, .1m, .1m, .001m),
        new Leg("USDCHF", "4010", 10000m, .1m, .1m, .00001m),
        new Leg("USDCAD", "4013", 10000m, .1m, .1m, .00001m),
        new Leg("USDHUF", "100501", 10000m, .1m, .1m, .001m),
        new Leg("USDMXN", "100507", 10000m, .1m, .1m, .00001m),
        new Leg("USDNOK", "100513", 10000m, .1m, .1m, .00001m),
        new Leg("USDPLN", "100523", 10000m, .1m, .1m, .00001m),
        new Leg("USDRON", "100931", 10000m, .1m, .1m, .00001m),
        new Leg("USDSEK", "100529", 10000m, .1m, .1m, .00001m),
        new Leg("USDZAR", "100547", 10000m, .1m, .1m, .00001m)
    });
    public static IReadOnlyList<string> Symbols => Legs.Select(x => x.Symbol).ToArray();
    public static IReadOnlyList<LmaxDemoSessionInstrument> SessionInstruments => Legs.Select(x => x.SessionInstrument).ToArray();
    public static bool Matches(IReadOnlyList<LmaxDemoSessionInstrument> instruments) =>
        instruments.Count == Legs.Count && instruments.Distinct().Count() == Legs.Count
        && instruments.ToHashSet().SetEquals(SessionInstruments);

    public static IReadOnlyList<string> ConfigurationIssues(PlatformState state, DateTimeOffset now)
    {
        var issues = new List<string>();
        var account = state.BrokerAccounts.SingleOrDefault(x => x.IsEnabled && x.ExternalAccountId == LmaxDemoControlledSession.DemoAccountId);
        var venue = state.Venues.SingleOrDefault(x => x.Name == "LMAX" && x.IsEnabled && x.IsTradingEnabled && x.IsReportImportEnabled && x.IsMarketDataEnabled);
        if (account is null || account.AccountCode != "LMAX_DEMO_LOCAL" || state.BrokerAccounts.Count(x => x.IsEnabled) != 1 || venue is null)
            return ["EXACT_DEMO_ACCOUNT_AND_VENUE_REQUIRED"];
        var sets = state.RiskLimitSets.Where(x => x.FundId == account.FundId && x.ModelName == "IntradayFxModel"
            && x.IsActive && x.Status == RiskLimitSetStatus.Active && x.GlobalTradingEnabled
            && (!x.EffectiveFromUtc.HasValue || x.EffectiveFromUtc <= now) && (!x.EffectiveToUtc.HasValue || now < x.EffectiveToUtc)).ToArray();
        if (sets.Length != 1) issues.Add("ONE_ACTIVE_RISK_POLICY_REQUIRED");
        foreach (var leg in Legs)
        {
            var instruments = state.Instruments.Where(x => x.Symbol == leg.Symbol).ToArray();
            if (instruments.Length != 1) { issues.Add(leg.Symbol + ":INSTRUMENT_MISSING_OR_AMBIGUOUS"); continue; }
            var instrument = instruments[0];
            if (!instrument.IsEnabled || !instrument.IsTradingEnabled || !instrument.IsReportImportEnabled || !instrument.IsMarketDataEnabled
                || instrument.BaseCurrency.Code != leg.Symbol[..3] || instrument.QuoteCurrency.Code != leg.Symbol[3..]) issues.Add(leg.Symbol + ":INSTRUMENT_BINDING_INVALID");
            var mappings = state.VenueInstrumentMappings.Where(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id && x.IsEnabled).ToArray();
            if (mappings.Length != 1 || mappings[0].VenueSymbol != leg.Symbol || mappings[0].VenueInstrumentCode != leg.SlashSymbol
                || mappings[0].ContractSize != leg.ContractSize || mappings[0].MinOrderQuantity != leg.MinQuantity
                || mappings[0].QuantityStep != leg.QuantityStep || mappings[0].PriceTickSize != leg.TickSize) issues.Add(leg.Symbol + ":EXECUTION_MAPPING_INVALID");
            var aliases = state.InstrumentAliases.Where(x => x.IsEnabled && x.Source == "LMAX_REPORT" && x.InstrumentId == instrument.Id).ToArray();
            if (aliases.Length != 1 || aliases[0].ExternalSymbol != leg.SlashSymbol || aliases[0].ExternalInstrumentId != leg.SecurityId)
                issues.Add(leg.Symbol + ":REPORT_ALIAS_INVALID");
            if (sets.Length == 1 && state.InstrumentRiskLimits.Count(x => x.RiskLimitSetId == sets[0].Id && x.InstrumentId == instrument.Id
                && x.IsEnabled && x.IsTradingEnabled && x.MaxTradeNotionalUsd > 0m && x.MaxExposureUsd > 0m) != 1)
                issues.Add(leg.Symbol + ":APPROVED_RISK_RULE_MISSING");
        }
        return issues;
    }
}
