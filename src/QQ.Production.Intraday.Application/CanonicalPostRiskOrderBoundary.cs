using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>Retained state obtained by the canonical path; callers cannot supply it to the public wire entry.</summary>
public sealed record CanonicalPostRiskRetainedState(
    bool KillSwitchActive,
    bool TradingWindowOpen,
    bool MarketDataFresh,
    bool PositionsReconciled,
    bool InstrumentEnabled,
    bool VenueEnabled,
    decimal CurrentBaseQuantity,
    VenueInstrumentMapping Mapping,
    MarketDataSnapshot MarketData,
    DateTimeOffset PositionAsOfUtc,
    string PositionReference,
    string MarketReference);

public interface ICanonicalPostRiskRetainedStateProvider
{
    CanonicalPostRiskRetainedState Resolve(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc);
}

public enum CanonicalBoundRiskFreshness { Fresh, Stale, Unknown }

public sealed record CanonicalBoundRiskFreshnessAttestation(
    string RiskDecisionId,
    DateTimeOffset RiskRecordedAtUtc,
    DateTimeOffset KnowledgeCutoffUtc,
    string Provenance,
    string PositionReference,
    DateTimeOffset PositionAsOfUtc,
    string MarketReference,
    MarketDataSnapshotId MarketDataSnapshotId,
    DateTimeOffset MarketDataReceivedAtUtc,
    DateTimeOffset VerifiedAtUtc,
    CanonicalBoundRiskFreshness Result);

public interface ICanonicalBoundRiskFreshnessGate
{
    CanonicalBoundRiskFreshnessAttestation Verify(
        CanonicalPostRiskInput input,
        ResolvedCanonicalExecutionContext context,
        CanonicalPostRiskRetainedState state,
        DateTimeOffset asOfUtc);
}

public sealed record CanonicalPostRiskOrderEvidence(
    CanonicalPostRiskInput Input,
    ResolvedCanonicalExecutionContext Context,
    CanonicalTradingReleaseControl ReleaseControl,
    CanonicalBoundRiskFreshnessAttestation Freshness,
    CanonicalPostRiskRetainedState State,
    RetainedTargetQuantity Target,
    decimal DriftBaseQuantity,
    decimal DriftVenueQuantity,
    ParentOrder ParentOrder,
    ChildOrder ChildOrder);

public sealed record CanonicalPostRiskOrderBoundaryResult(bool Duplicate, CanonicalPostRiskOrderEvidence Evidence);

public interface ICanonicalPostRiskOrderBoundary
{
    CanonicalPostRiskOrderBoundaryResult Create(CanonicalPostRiskOrderEvidence evidence);
}

/// <summary>
/// Test-only retained order boundary. It materializes the existing ParentOrder/ChildOrder domain objects,
/// but deliberately has no persistence or venue gateway dependency.
/// </summary>
public sealed class InMemoryCanonicalPostRiskOrderBoundary : ICanonicalPostRiskOrderBoundary
{
    private readonly Dictionary<(string AdapterInputId, long Revision), (string Fingerprint, CanonicalPostRiskOrderEvidence Evidence)> orders = [];

    public CanonicalPostRiskOrderBoundaryResult Create(CanonicalPostRiskOrderEvidence evidence)
    {
        var key = (evidence.Input.AdapterInputId, evidence.Input.Revision);
        if (orders.TryGetValue(key, out var existing))
        {
            if (existing.Fingerprint != evidence.Input.Fingerprint)
                throw new InvalidOperationException("Conflicting canonical input fingerprint reached order boundary.");
            return new(true, existing.Evidence);
        }

        orders.Add(key, (evidence.Input.Fingerprint, evidence));
        return new(false, evidence);
    }

    public int CreatedOrderCount => orders.Count;
}

public sealed record CanonicalPostRiskConsumptionResult(
    bool Duplicate,
    bool Allowed,
    string Reason,
    CanonicalPostRiskOrderEvidence? Evidence);

/// <summary>
/// The only public release-capable canonical-v1 application entry. It owns wire validation, receipt
/// idempotency, retained state/freshness binding, sizing, release controls, and the dormant order boundary.
/// </summary>
public sealed class CanonicalPostRiskConsumptionService(
    ICanonicalInputReceiptStore receipts,
    ICanonicalExecutionContextResolver contextResolver,
    ICanonicalTradingReleaseControlResolver releaseControls,
    ICanonicalPostRiskRetainedStateProvider retainedState,
    ICanonicalBoundRiskFreshnessGate freshnessGate,
    ICanonicalPostRiskOrderBoundary orderBoundary)
{
    public CanonicalPostRiskConsumptionResult Consume(string canonicalWire, DateTimeOffset asOfUtc)
    {
        if (asOfUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("An explicit UTC as-of time is required.", nameof(asOfUtc));

        var input = CanonicalPostRiskInputParser.Parse(canonicalWire);
        if (receipts.Record(input) == CanonicalInputReceiptResult.Duplicate)
            return new(true, false, "Canonical input was already consumed.", null);

        var context = contextResolver.Resolve(input, asOfUtc);
        var control = releaseControls.Resolve(context, asOfUtc);
        var state = retainedState.Resolve(input, context, asOfUtc);
        var freshness = freshnessGate.Verify(input, context, state, asOfUtc);
        if (!FreshnessIsBound(input, state, freshness, asOfUtc))
            return Block("Fresh canonical Risk required.");
        if (!StateLineageIsUsable(input, context, state, asOfUtc))
            return Block("Retained position or market state is invalid.");
        if (state.KillSwitchActive) return Block("Kill switch is active.");
        if (!state.TradingWindowOpen) return Block("Trading window is closed.");
        if (!state.MarketDataFresh) return Block("Market data is stale.");
        if (!state.PositionsReconciled) return Block("Positions are not reconciled.");
        if (!state.InstrumentEnabled || !state.VenueEnabled || !state.Mapping.IsEnabled)
            return Block("Instrument or venue is disabled.");

        RetainedTargetQuantity target;
        try
        {
            target = RetainedTargetPositionSizing.Calculate(
                context.Execution.TargetQuantityMode,
                input.RiskApprovedTargetWeight,
                context.Execution.NavUsd,
                state.MarketData,
                state.Mapping);
        }
        catch (DomainRuleViolationException exception)
        {
            return Block(exception.Message);
        }

        var driftBase = target.TargetBaseQuantity - state.CurrentBaseQuantity;
        var driftVenue = driftBase / state.Mapping.ContractSize;
        if (driftBase == 0m || driftVenue == 0m) return Block("No executable drift.");
        if (QuantityRounding.RoundToStep(driftVenue, state.Mapping.QuantityStep) != driftVenue)
            return Block("Drift venue quantity is not executable at the retained quantity step.");
        if (Math.Abs(driftVenue) < state.Mapping.MinOrderQuantity)
            return Block("Drift venue quantity is below the retained minimum order quantity.");
        if (Math.Abs(driftBase) < control.MinimumExecutableBaseQuantity)
            return Block("Below minimum executable quantity.");
        if (Math.Abs(driftBase * state.MarketData.Mid) > control.MaximumPerOrderNotionalUsd)
            return Block("Maximum per-order notional requires slicing; release blocked.");

        var side = driftBase > 0m ? OrderSide.Buy : OrderSide.Sell;
        var identity = string.Join("|", input.AdapterInputId, input.Revision, input.Fingerprint, context.Execution.ContextId, context.Execution.Revision, control.ControlSetId, control.Revision);
        var tradeIntentId = new TradeIntentId(DeterministicGuid("intent|" + identity));
        var parent = new ParentOrder(
            new ParentOrderId(DeterministicGuid("parent|" + identity)), tradeIntentId,
            new ClientOrderId("CPR-P-" + input.Fingerprint[..16].ToUpperInvariant()), side,
            Math.Abs(driftBase), ExecutionAlgo.MarketImmediate, OrderStatus.Created, asOfUtc);
        var child = new ChildOrder(
            new ChildOrderId(DeterministicGuid("child|" + identity)), parent.Id, context.Instrument.VenueId,
            new ClientOrderId("CPR-C-" + input.Fingerprint[..16].ToUpperInvariant()), side,
            OrderType.Market, TimeInForce.IOC, Math.Abs(driftBase), Math.Abs(driftVenue),
            OrderStatus.PendingNew, asOfUtc);
        var evidence = new CanonicalPostRiskOrderEvidence(input, context, control, freshness, state, target, driftBase, driftVenue, parent, child);
        var boundary = orderBoundary.Create(evidence);
        return new(boundary.Duplicate, true, "Retained ParentOrder/ChildOrder created at the dormant OMS/EMS boundary; no venue send occurred.", boundary.Evidence);
    }

    private static CanonicalPostRiskConsumptionResult Block(string reason) => new(false, false, reason, null);

    private static bool FreshnessIsBound(CanonicalPostRiskInput input, CanonicalPostRiskRetainedState state, CanonicalBoundRiskFreshnessAttestation freshness, DateTimeOffset asOfUtc)
        => freshness.Result == CanonicalBoundRiskFreshness.Fresh &&
           input.RiskRecordedAtUtc <= input.KnowledgeCutoffUtc && input.KnowledgeCutoffUtc <= asOfUtc &&
           freshness.RiskDecisionId == input.RiskDecisionId &&
           freshness.RiskRecordedAtUtc == input.RiskRecordedAtUtc &&
           freshness.KnowledgeCutoffUtc == input.KnowledgeCutoffUtc &&
           freshness.Provenance == input.Provenance &&
           freshness.PositionReference == state.PositionReference &&
           freshness.PositionAsOfUtc == state.PositionAsOfUtc &&
           freshness.MarketReference == state.MarketReference &&
           freshness.MarketDataSnapshotId == state.MarketData.Id &&
           freshness.MarketDataReceivedAtUtc == state.MarketData.ReceivedAtUtc &&
           freshness.VerifiedAtUtc.Offset == TimeSpan.Zero && freshness.VerifiedAtUtc <= asOfUtc &&
           freshness.RiskRecordedAtUtc.Offset == TimeSpan.Zero && freshness.KnowledgeCutoffUtc.Offset == TimeSpan.Zero;

    private static bool StateLineageIsUsable(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, CanonicalPostRiskRetainedState state, DateTimeOffset asOfUtc)
        => state.PositionAsOfUtc.Offset == TimeSpan.Zero && state.PositionAsOfUtc >= input.KnowledgeCutoffUtc && state.PositionAsOfUtc <= asOfUtc &&
           !string.IsNullOrWhiteSpace(state.PositionReference) && !string.IsNullOrWhiteSpace(state.MarketReference) &&
           state.MarketData.Id.Value != Guid.Empty && !string.IsNullOrWhiteSpace(state.MarketData.Source) &&
           state.MarketData.SourceTimestampUtc.Offset == TimeSpan.Zero && state.MarketData.ReceivedAtUtc.Offset == TimeSpan.Zero &&
           state.MarketData.ReceivedAtUtc >= input.KnowledgeCutoffUtc && state.MarketData.ReceivedAtUtc <= asOfUtc &&
           state.Mapping.Id.Value != Guid.Empty && state.Mapping.InstrumentId == context.Instrument.InstrumentId && state.Mapping.VenueId == context.Instrument.VenueId &&
           state.MarketData.InstrumentId == context.Instrument.InstrumentId && state.MarketData.VenueId == context.Instrument.VenueId &&
           state.Mapping.ContractSize > 0m && state.Mapping.QuantityStep > 0m && state.Mapping.MinOrderQuantity >= 0m;

    private static Guid DeterministicGuid(string material)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return new Guid(bytes[..16]);
    }
}
