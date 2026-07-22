using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public enum Arch7aSourceStatus { Completed, Incomplete, FailedClosed }
public enum Arch7aSourceFreshness { Fresh, Stale, Missing, Incomplete }
public enum Arch7aWorkingOrderAuthority { AuthoritativeComplete, UnavailableWithCurrentLmaxInterfaces, Incomplete, Stale }
public enum Arch7aShadowRiskOutcome { APPROVED_SHADOW, BLOCK_NEW_ORDERS, EMERGENCY_STOP }

public sealed record Arch7aExecutionSlot(
    string SlotId,
    DateOnly OperationalDate,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc)
{
    public void RequireCanonical()
    {
        RequireUtc(TargetCloseUtc, nameof(TargetCloseUtc));
        RequireUtc(EffectiveFromUtc, nameof(EffectiveFromUtc));
        RequireUtc(DeadlineUtc, nameof(DeadlineUtc));
        if (string.IsNullOrWhiteSpace(SlotId)) throw new InvalidOperationException("ARCH7A_SLOT_ID_REQUIRED");
        if (TargetCloseUtc.Second != 0 || TargetCloseUtc.Millisecond != 0 ||
            TargetCloseUtc.Minute is not (0 or 15 or 30 or 45))
            throw new InvalidOperationException("ARCH7A_TARGET_CLOSE_MUST_BE_CANONICAL_QUARTER_HOUR");
        if (EffectiveFromUtc >= DeadlineUtc || DeadlineUtc != TargetCloseUtc)
            throw new InvalidOperationException("ARCH7A_SLOT_WINDOW_INVALID");
    }

    private static void RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException($"ARCH7A_{name.ToUpperInvariant()}_MUST_BE_UTC");
    }
}

public sealed record Arch7aPmsTargetContribution(
    Guid ModelRunId,
    string StrategyId,
    Guid TargetPositionId,
    Guid DriftId,
    string SecurityId,
    string PortfolioSymbol,
    decimal TargetWeight,
    decimal CurrentQuantity,
    decimal TargetQuantity,
    decimal PositionOnlyDelta,
    string InputSha256,
    string OutputSha256,
    string CoreCommitId);

public sealed record Arch7aPmsExecutionSource(
    Guid IngestionId,
    string SourceSessionId,
    Arch7aExecutionSlot Slot,
    DateTimeOffset CompletedAtUtc,
    string Environment,
    string AccountScope,
    decimal NavUsd,
    Arch7aSourceStatus Status,
    Arch7aSourceFreshness Freshness,
    bool LineageComplete,
    bool PositionAuthority,
    Arch7aWorkingOrderAuthority WorkingOrderAuthority,
    bool AllowShadowSimulationWhenWorkingLeavesUnknown,
    bool HasCriticalConflict,
    IReadOnlyList<Arch7aPmsTargetContribution> Contributions,
    IReadOnlyDictionary<string, decimal> ExecutionMidPrices,
    IReadOnlyDictionary<string, decimal> ReconciledCurrentExecutionQuantities,
    IReadOnlyDictionary<string, decimal> QuantityIncrements);

public sealed record Arch7aFxCurrencyContribution(
    string StrategyId,
    Guid ModelRunId,
    Guid TargetPositionId,
    Guid DriftId,
    string SourceSymbol,
    string Currency,
    decimal SignedWeightContribution);

public sealed record Arch7aExecutionNettingLine(
    string PortfolioCurrency,
    string NormalizedPortfolioSymbol,
    string ExecutionTradableSymbol,
    bool RequiresInversion,
    string SecurityId,
    string SecurityIdSource,
    decimal NettedWeight,
    decimal TargetExecutionQuantity,
    decimal CurrentExecutionQuantity,
    decimal SignedDesiredDelta,
    decimal QuantityIncrement,
    IReadOnlyList<Arch7aFxCurrencyContribution> Contributions);

public sealed record Arch7aExecutionNettingManifest(
    string SourceSessionId,
    string SlotId,
    IReadOnlyDictionary<string, decimal> CurrencyExposureSums,
    IReadOnlyList<Arch7aFxCurrencyContribution> Contributions,
    IReadOnlyList<Arch7aExecutionNettingLine> ExecutionLines,
    IReadOnlyList<string> DirectCrossesExcluded,
    IReadOnlyList<string> UnsupportedCurrencies,
    string NettingSha256,
    bool DirectCrossExecutionDisabled,
    bool Deterministic);

public sealed record Arch7aTradeIntentEnvelope(
    TradeIntent Canonical,
    Guid SourceIngestionId,
    string SourceSessionId,
    string SlotId,
    DateOnly OperationalDate,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc,
    IReadOnlyList<Guid> ModelRunIds,
    IReadOnlyList<Guid> TargetPositionIds,
    IReadOnlyList<Guid> DriftIds,
    string SecurityId,
    string SecurityIdSource,
    string NormalizedPortfolioSymbol,
    string ExecutionTradableSymbol,
    bool RequiresInversion,
    decimal SignedDesiredDelta,
    decimal TargetQuantity,
    decimal CurrentQuantity,
    string AccountScope,
    string Environment,
    string Classification,
    bool Actionable,
    bool ExecutionAllowed,
    bool BrokerRouteAllowed,
    string? BlockingReason,
    string IdempotencyKey,
    string LineageSha256);

public sealed record Arch7aRiskDecisionEnvelope(
    RiskDecision Canonical,
    Arch7aShadowRiskOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> BlockingBreaks,
    bool SourceComplete,
    bool PositionAuthority,
    bool WorkingOrderAuthority,
    Arch7aSourceFreshness Freshness,
    IReadOnlyList<string> LimitsEvaluated,
    bool NoOrderInvariant,
    bool BrokerSendAllowed);

public sealed record Arch7aParentOrderEnvelope(
    ParentOrder Canonical,
    Guid RiskDecisionId,
    string Symbol,
    decimal TotalQuantity,
    DateTimeOffset TargetCloseUtc,
    string Status,
    bool RouteAllowed,
    string DeterministicIdentity);

public sealed record Arch7aChildOrderEnvelope(
    ChildOrder Canonical,
    string Tranche,
    decimal? SimulatedLimitPrice,
    DateTimeOffset EffectiveTimeUtc,
    DateTimeOffset DeadlineUtc,
    CloseSeekingPhaseName AlgoPhase,
    string Status,
    bool BrokerSendAllowed,
    string DeterministicIdentity);

public sealed record Arch7aShadowExecutionUnit(
    Arch7aTradeIntentEnvelope TradeIntent,
    Arch7aRiskDecisionEnvelope RiskDecision,
    Arch7aParentOrderEnvelope ParentOrder,
    Arch7aChildOrderEnvelope ChildOrder);

public sealed record Arch7aShadowExecutionPlan(
    Arch7aExecutionNettingManifest Netting,
    IReadOnlyList<Arch7aShadowExecutionUnit> Units,
    IReadOnlyList<CloseSeeking15mPhase> CloseSeekingPhases,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NetworkLedger,
    string PlanSha256,
    bool NoFixLogon,
    bool NoBrokerSend,
    bool NoAccountApi,
    bool NoDatabento,
    bool NoRealAccount,
    bool NoFill,
    bool NoPositionLedgerEvent);

public interface IArch7aPmsExecutionSourceReader
{
    Task<Arch7aPmsExecutionSource> ReadAsync(
        string sourceSessionId,
        Arch7aExecutionSlot slot,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record Arch7aShadowCoordinationResult(
    Arch7aShadowExecutionPlan Plan,
    Arch7aShadowStoreResult? StoreResult,
    bool Persisted);

public sealed class Arch7aShadowExecutionCoordinator(
    IArch7aPmsExecutionSourceReader sourceReader,
    Arch7aPmsShadowExecutionPipeline pipeline,
    IArch7aShadowExecutionStore store)
{
    public async Task<Arch7aShadowCoordinationResult> RunAsync(
        string sourceSessionId,
        Arch7aExecutionSlot slot,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var source = await sourceReader.ReadAsync(sourceSessionId, slot, nowUtc, cancellationToken);
        var plan = pipeline.Build(source);
        if (plan.Units.Count == 0)
            return new(plan, null, Persisted: false);

        var result = await store.PersistAsync(plan, cancellationToken);
        return new(plan, result, Persisted: true);
    }
}
public enum Arch7aShadowStoreResult { Persisted, AlreadyPersistedIdentical }

public interface IArch7aShadowExecutionStore
{
    Task<Arch7aShadowStoreResult> PersistAsync(
        Arch7aShadowExecutionPlan plan,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryArch7aShadowExecutionStore : IArch7aShadowExecutionStore
{
    private readonly Dictionary<string, string> plans = new(StringComparer.Ordinal);

    public Task<Arch7aShadowStoreResult> PersistAsync(
        Arch7aShadowExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (plans)
        {
            var key = $"{plan.Netting.SourceSessionId}|{plan.Netting.SlotId}";
            if (plans.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing, plan.PlanSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("ARCH7A_IDEMPOTENCY_CONFLICT");
                return Task.FromResult(Arch7aShadowStoreResult.AlreadyPersistedIdentical);
            }

            plans.Add(key, plan.PlanSha256);
            return Task.FromResult(Arch7aShadowStoreResult.Persisted);
        }
    }
}