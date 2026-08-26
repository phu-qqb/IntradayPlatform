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

public enum CanonicalRiskFreshness { Fresh, Stale, Unknown }
public sealed record CanonicalRiskFreshnessAttestation(string Id, DateTimeOffset AsOfUtc, string Provenance, CanonicalRiskFreshness Result);
public interface ICanonicalRiskFreshnessGate { CanonicalRiskFreshnessAttestation Verify(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc); }

public sealed record CanonicalPostRiskTradingSafety(
    bool KillSwitchActive, bool TradingWindowOpen, bool MarketDataFresh, bool PositionsReconciled,
    bool InstrumentEnabled, bool VenueEnabled, decimal CurrentBaseQuantity, decimal MarketMid,
    DateTimeOffset PositionAsOfUtc, DateTimeOffset MarketAsOfUtc, string PositionReference, string MarketReference);

public sealed record CanonicalPostRiskTradingResult(
    bool Allowed, string Reason, decimal TargetBaseQuantity, decimal DriftBaseQuantity,
    CanonicalPostRiskInput Input, ResolvedCanonicalExecutionContext Context,
    CanonicalTradingReleaseControl ReleaseControl, CanonicalRiskFreshnessAttestation Freshness);

/// <summary>Non-authoritative retained Trading release boundary; it never creates a RiskDecision or evaluates exposure.</summary>
public sealed class CanonicalPostRiskTradingService(
    ICanonicalExecutionContextResolver contextResolver,
    ICanonicalTradingReleaseControlResolver releaseControls,
    ICanonicalRiskFreshnessGate freshnessGate)
{
    public CanonicalPostRiskTradingResult Evaluate(CanonicalPostRiskInput input, DateTimeOffset asOfUtc, CanonicalPostRiskTradingSafety safety)
    {
        var context = contextResolver.Resolve(input, asOfUtc);
        var control = releaseControls.Resolve(context, asOfUtc);
        var freshness = freshnessGate.Verify(input, context, asOfUtc);
        if (freshness.Result != CanonicalRiskFreshness.Fresh) return Block("Fresh canonical Risk required.", input, context, control, freshness);
        if (safety.PositionAsOfUtc.Offset != TimeSpan.Zero || safety.MarketAsOfUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(safety.PositionReference) || string.IsNullOrWhiteSpace(safety.MarketReference)) return Block("Position or market lineage is invalid.", input, context, control, freshness);
        if (safety.KillSwitchActive) return Block("Kill switch is active.", input, context, control, freshness);
        if (!safety.TradingWindowOpen) return Block("Trading window is closed.", input, context, control, freshness);
        if (!safety.MarketDataFresh) return Block("Market data is stale.", input, context, control, freshness);
        if (!safety.PositionsReconciled) return Block("Positions are not reconciled.", input, context, control, freshness);
        if (!safety.InstrumentEnabled || !safety.VenueEnabled || safety.MarketMid <= 0m) return Block("Instrument, venue, or market safety is invalid.", input, context, control, freshness);
        var target = input.RiskApprovedTargetWeight * context.Execution.NavUsd / safety.MarketMid;
        var drift = target - safety.CurrentBaseQuantity;
        if (Math.Abs(drift) < control.MinimumExecutableBaseQuantity) return Block("Below minimum executable quantity.", input, context, control, freshness, target, drift);
        if (Math.Abs(drift * safety.MarketMid) > control.MaximumPerOrderNotionalUsd) return Block("Maximum per-order notional requires slicing; release blocked.", input, context, control, freshness, target, drift);
        return new(true, "Released for OMS/EMS order-boundary creation.", target, drift, input, context, control, freshness);
    }

    private static CanonicalPostRiskTradingResult Block(string reason, CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, CanonicalTradingReleaseControl control, CanonicalRiskFreshnessAttestation freshness, decimal target = 0m, decimal drift = 0m) => new(false, reason, target, drift, input, context, control, freshness);
}
