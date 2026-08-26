using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record CanonicalTradingReleaseControl(
    FundId FundId, VenueId VenueId, InstrumentId InstrumentId, string ControlSetId, long Revision,
    DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc, string Provenance,
    decimal MinimumExecutableBaseQuantity, decimal MaximumPerOrderNotionalUsd);

public interface ICanonicalTradingReleaseControlResolver
{
    CanonicalTradingReleaseControl Resolve(ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc);
}

public sealed class InMemoryCanonicalTradingReleaseControlResolver(IEnumerable<CanonicalTradingReleaseControl> controls) : ICanonicalTradingReleaseControlResolver
{
    public CanonicalTradingReleaseControl Resolve(ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc)
    {
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("An explicit UTC as-of time is required.", nameof(asOfUtc));
        var matches = controls.Where(x => x.FundId == context.MandateFund.FundId && x.VenueId == context.Instrument.VenueId && x.InstrumentId == context.Instrument.InstrumentId && Active(x, asOfUtc)).ToList();
        if (matches.Count != 1) throw new InvalidOperationException("Trading release control is missing or ambiguous.");
        return matches[0];
    }

    private static bool Active(CanonicalTradingReleaseControl x, DateTimeOffset at) =>
        !string.IsNullOrWhiteSpace(x.ControlSetId) && x.Revision > 0 && !string.IsNullOrWhiteSpace(x.Provenance) &&
        x.EffectiveFromUtc.Offset == TimeSpan.Zero && (x.EffectiveToUtc is null || (x.EffectiveToUtc.Value.Offset == TimeSpan.Zero && x.EffectiveToUtc > x.EffectiveFromUtc)) &&
        x.MinimumExecutableBaseQuantity >= 0m && x.MaximumPerOrderNotionalUsd > 0m && x.EffectiveFromUtc <= at && (x.EffectiveToUtc is null || at < x.EffectiveToUtc);
}
