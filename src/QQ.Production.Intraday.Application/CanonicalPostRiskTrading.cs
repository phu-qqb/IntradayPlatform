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
public sealed record CanonicalRiskFreshnessAttestation(string RiskDecisionId, DateTimeOffset RiskRecordedAtUtc, DateTimeOffset KnowledgeCutoffUtc, string Provenance, CanonicalRiskFreshness Result);
public interface ICanonicalRiskFreshnessGate { CanonicalRiskFreshnessAttestation Verify(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc); }

public sealed record CanonicalPostRiskTradingSafety(
    bool KillSwitchActive, bool TradingWindowOpen, bool MarketDataFresh, bool PositionsReconciled,
    bool InstrumentEnabled, bool VenueEnabled, decimal CurrentBaseQuantity, VenueInstrumentMapping Mapping,
    MarketDataSnapshot MarketData, DateTimeOffset PositionAsOfUtc, string PositionReference);

public sealed record CanonicalPostRiskTradingResult(
    bool Allowed, string Reason, decimal TargetBaseQuantity, decimal TargetVenueQuantity, decimal DriftBaseQuantity, decimal DriftVenueQuantity,
    CanonicalPostRiskInput Input, ResolvedCanonicalExecutionContext Context,
    CanonicalTradingReleaseControl ReleaseControl, CanonicalRiskFreshnessAttestation Freshness, CanonicalOmsEmsRelease? Release);

/// <summary>Explicit non-routing hand-off to the retained OMS/EMS boundary; it is not an order-side entry.</summary>
public sealed record CanonicalOmsEmsRelease(
    string AdapterInputId, long Revision, string Fingerprint, string RiskDecisionId,
    FundId FundId, VenueId VenueId, InstrumentId InstrumentId, VenueInstrumentId VenueInstrumentId,
    string ExecutionContextId, long ExecutionContextRevision, string ReleaseControlSetId, long ReleaseControlRevision,
    TradeSide Side, decimal BaseQuantity, decimal VenueQuantity, DateTimeOffset EffectiveAtUtc, string Provenance);

public sealed record CanonicalPostRiskConsumptionResult(bool Duplicate, CanonicalPostRiskTradingResult? TradingResult);

/// <summary>Non-authoritative retained Trading release boundary; it never creates a RiskDecision or evaluates exposure.</summary>
public sealed class CanonicalPostRiskConsumptionService(ICanonicalInputReceiptStore receipts, CanonicalPostRiskTradingService trading)
{
    public CanonicalPostRiskConsumptionResult Consume(CanonicalPostRiskInput input, DateTimeOffset asOfUtc, CanonicalPostRiskTradingSafety safety)
        => receipts.Record(input) == CanonicalInputReceiptResult.Duplicate
            ? new(true, null)
            : new(false, trading.Evaluate(input, asOfUtc, safety));
}

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
        if (!FreshnessIsUsable(input, freshness, asOfUtc)) return Block("Fresh canonical Risk required.", input, context, control, freshness);
        if (!SafetyLineageIsUsable(safety, context)) return Block("Position or market lineage is invalid.", input, context, control, freshness);
        if (safety.KillSwitchActive) return Block("Kill switch is active.", input, context, control, freshness);
        if (!safety.TradingWindowOpen) return Block("Trading window is closed.", input, context, control, freshness);
        if (!safety.MarketDataFresh) return Block("Market data is stale.", input, context, control, freshness);
        if (!safety.PositionsReconciled) return Block("Positions are not reconciled.", input, context, control, freshness);
        if (!safety.InstrumentEnabled || !safety.VenueEnabled || !safety.Mapping.IsEnabled || safety.MarketData.Mid <= 0m) return Block("Instrument, venue, or market safety is invalid.", input, context, control, freshness);
        var targetBase = DerivedTargetBaseQuantity(input.RiskApprovedTargetWeight, context.Execution, safety.MarketData.Mid, safety.Mapping);
        var targetVenue = targetBase / safety.Mapping.ContractSize;
        var driftBase = targetBase - safety.CurrentBaseQuantity;
        var driftVenue = driftBase / safety.Mapping.ContractSize;
        if (Math.Abs(driftBase) < control.MinimumExecutableBaseQuantity) return Block("Below minimum executable quantity.", input, context, control, freshness, targetBase, targetVenue, driftBase, driftVenue);
        if (Math.Abs(driftBase * safety.MarketData.Mid) > control.MaximumPerOrderNotionalUsd) return Block("Maximum per-order notional requires slicing; release blocked.", input, context, control, freshness, targetBase, targetVenue, driftBase, driftVenue);
        var release = new CanonicalOmsEmsRelease(input.AdapterInputId, input.Revision, input.Fingerprint, input.RiskDecisionId, context.MandateFund.FundId, context.Instrument.VenueId, context.Instrument.InstrumentId, context.Instrument.VenueInstrumentId, context.Execution.ContextId, context.Execution.Revision, control.ControlSetId, control.Revision, driftBase > 0m ? TradeSide.Buy : TradeSide.Sell, Math.Abs(driftBase), Math.Abs(driftVenue), asOfUtc, input.Provenance);
        return new(true, "Released to the retained OMS/EMS hand-off; no order was created or routed.", targetBase, targetVenue, driftBase, driftVenue, input, context, control, freshness, release);
    }

    private static CanonicalPostRiskTradingResult Block(string reason, CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, CanonicalTradingReleaseControl control, CanonicalRiskFreshnessAttestation freshness, decimal targetBase = 0m, decimal targetVenue = 0m, decimal driftBase = 0m, decimal driftVenue = 0m) => new(false, reason, targetBase, targetVenue, driftBase, driftVenue, input, context, control, freshness, null);

    private static bool FreshnessIsUsable(CanonicalPostRiskInput input, CanonicalRiskFreshnessAttestation freshness, DateTimeOffset asOfUtc)
        => freshness.Result == CanonicalRiskFreshness.Fresh && freshness.RiskDecisionId == input.RiskDecisionId && freshness.RiskRecordedAtUtc == input.RiskRecordedAtUtc && freshness.KnowledgeCutoffUtc == input.KnowledgeCutoffUtc && freshness.Provenance == input.Provenance && !string.IsNullOrWhiteSpace(freshness.Provenance) && freshness.RiskRecordedAtUtc.Offset == TimeSpan.Zero && freshness.KnowledgeCutoffUtc.Offset == TimeSpan.Zero && freshness.RiskRecordedAtUtc <= asOfUtc && freshness.KnowledgeCutoffUtc <= asOfUtc;

    private static bool SafetyLineageIsUsable(CanonicalPostRiskTradingSafety safety, ResolvedCanonicalExecutionContext context)
        => safety.PositionAsOfUtc.Offset == TimeSpan.Zero && !string.IsNullOrWhiteSpace(safety.PositionReference) && !string.IsNullOrWhiteSpace(safety.MarketData.Source) && safety.MarketData.Id.Value != Guid.Empty && safety.MarketData.SourceTimestampUtc.Offset == TimeSpan.Zero && safety.MarketData.ReceivedAtUtc.Offset == TimeSpan.Zero && safety.Mapping.Id.Value != Guid.Empty && safety.Mapping.InstrumentId == context.Instrument.InstrumentId && safety.Mapping.VenueId == context.Instrument.VenueId && safety.MarketData.InstrumentId == context.Instrument.InstrumentId && safety.MarketData.VenueId == context.Instrument.VenueId && safety.Mapping.ContractSize > 0m && safety.Mapping.QuantityStep > 0m && safety.Mapping.MinOrderQuantity >= 0m;

    private static decimal DerivedTargetBaseQuantity(decimal approvedWeight, RetainedExecutionContext execution, decimal mid, VenueInstrumentMapping mapping)
    {
        var targetBase = execution.TargetQuantityMode == TargetQuantityMode.PortfolioBaseCurrencyNotional
            ? approvedWeight * execution.NavUsd / mid
            : approvedWeight * execution.NavUsd;
        var venueQuantity = QuantityRounding.RoundToStep(targetBase / mapping.ContractSize, mapping.QuantityStep);
        if (venueQuantity != 0m && Math.Abs(venueQuantity) < mapping.MinOrderQuantity)
            throw new DomainRuleViolationException("Rounded target venue quantity is below minimum order quantity.");
        return venueQuantity * mapping.ContractSize;
    }
}
