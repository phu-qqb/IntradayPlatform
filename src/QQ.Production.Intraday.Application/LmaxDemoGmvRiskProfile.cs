using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>Philippe's 18 September Demo amendment. Limits are USD GMV after
/// currency netting. The order bound permits a full reversal between two legal
/// positions; it does not permit a position larger than PositionGmvUsd.</summary>
public static class LmaxDemoGmvRiskProfile
{
    public const string Approval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5731020741";
    public const string OwnerQuote = "tous les jours de la semaine doivent être autorisé. Nouvelles limites: 2M par position GMV, 10M total ptf GMV";
    public static readonly Guid PolicyId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffff20260918");
    public const decimal PositionGmvUsd = 2_000_000m;
    public const decimal PortfolioGmvUsd = 10_000_000m;
    public const decimal MaximumReversalOrderUsd = 2m * PositionGmvUsd;

    public static IReadOnlyList<string> ConfigurationIssues(PlatformState state, DateTimeOffset now)
    {
        var issues = new List<string>();
        var account = state.BrokerAccounts.SingleOrDefault(x => x.IsEnabled && x.ExternalAccountId == "1754288005");
        if (account is null || account.AccountCode != "LMAX_DEMO_LOCAL" || state.BrokerAccounts.Count(x => x.IsEnabled) != 1)
            return ["GMV_EXACT_DEMO_ACCOUNT_REQUIRED"];
        var sets = state.RiskLimitSets.Where(x => x.FundId == account.FundId && x.ModelName == "IntradayFxModel"
            && x.IsActive && x.Status == RiskLimitSetStatus.Active).ToArray();
        if (sets.Length != 1 || sets[0].Id != PolicyId || sets[0].Version != 2 || !sets[0].GlobalTradingEnabled
            || sets[0].MaxGrossExposureUsd != PortfolioGmvUsd
            || sets[0].EffectiveFromUtc > now || sets[0].EffectiveToUtc <= now)
            issues.Add("APPROVED_10M_GMV_POLICY_REQUIRED");
        foreach (var symbol in LmaxDemoUsdExecutionUniverse.Symbols)
        {
            var instrument = state.Instruments.SingleOrDefault(x => x.Symbol == symbol);
            var limits = state.InstrumentRiskLimits.Where(x => x.RiskLimitSetId == PolicyId
                && x.InstrumentId == instrument?.Id && x.IsEnabled).ToArray();
            if (instrument is null || limits.Length != 1 || !limits[0].IsTradingEnabled
                || limits[0].MaxExposureUsd != PositionGmvUsd || limits[0].MaxTradeNotionalUsd != MaximumReversalOrderUsd)
                issues.Add(symbol + ":APPROVED_2M_GMV_RULE_REQUIRED");
        }
        var venues = state.Venues.Where(x => x.Name == "LMAX" && x.IsEnabled).ToArray();
        var venueRules = state.VenueRiskLimits.Where(x => x.RiskLimitSetId == PolicyId && x.IsEnabled).ToArray();
        if (venues.Length != 1 || venueRules.Length != 1 || venueRules[0].VenueId != venues[0].Id
            || !venueRules[0].IsVenueEnabled || venueRules[0].MaxTradeNotionalUsd != MaximumReversalOrderUsd)
            issues.Add("APPROVED_REVERSAL_ORDER_BOUND_REQUIRED");
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var windows = state.TradingWindows.Where(x => x.FundId == account.FundId && x.ModelName == "IntradayFxModel"
                && x.DayOfWeek == day && x.IsEnabled).ToArray();
            if (windows.Length != 1 || !windows[0].TradingEnabled || windows[0].TimeZoneId != "UTC"
                || windows[0].OpensAtUtc != TimeOnly.MinValue || windows[0].ClosesAtUtc != new TimeOnly(23,59,59)
                || windows[0].NoNewOrdersAfterUtc != new TimeOnly(23,59,59))
                issues.Add(day + ":AUTHORIZED_CALENDAR_DAY_REQUIRED");
        }
        return issues;
    }
}
