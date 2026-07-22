using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record PmsShadowTradeIntentRow(
    Guid TradeIntentId,
    Guid IngestionId,
    string SourceSessionId,
    string SlotId,
    DateOnly OperationalDate,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc,
    string ModelRunIdsJson,
    string TargetPositionIdsJson,
    string DriftIdsJson,
    string SecurityId,
    string SecurityIdSource,
    string NormalizedPortfolioSymbol,
    string ExecutionTradableSymbol,
    bool RequiresInversion,
    string Side,
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
    string LineageSha256,
    string PlanSha256,
    DateTimeOffset CreatedAtUtc);

public sealed record PmsShadowRiskDecisionRow(
    Guid RiskDecisionId,
    Guid TradeIntentId,
    string Outcome,
    string ReasonCodesJson,
    string BlockingBreaksJson,
    bool SourceComplete,
    bool PositionAuthority,
    bool WorkingOrderAuthority,
    string Freshness,
    string LimitsEvaluatedJson,
    bool NoOrderInvariant,
    bool BrokerSendAllowed,
    string PlanSha256,
    DateTimeOffset CreatedAtUtc);

public sealed record PmsShadowParentOrderRow(
    Guid ParentOrderId,
    Guid TradeIntentId,
    Guid RiskDecisionId,
    string ClientOrderId,
    string Symbol,
    string Side,
    decimal TotalQuantity,
    DateTimeOffset TargetCloseUtc,
    string ExecutionAlgo,
    string Status,
    bool RouteAllowed,
    string DeterministicIdentity,
    string PlanSha256,
    DateTimeOffset CreatedAtUtc);

public sealed record PmsShadowChildOrderRow(
    Guid ChildOrderId,
    Guid ParentOrderId,
    string ClientOrderId,
    string Venue,
    string Tranche,
    string Side,
    decimal Quantity,
    decimal? SimulatedLimitPrice,
    DateTimeOffset EffectiveTimeUtc,
    DateTimeOffset DeadlineUtc,
    string AlgoPhase,
    string Status,
    bool BrokerSendAllowed,
    string DeterministicIdentity,
    string PlanSha256,
    DateTimeOffset CreatedAtUtc);

public sealed class EfArch7aPmsExecutionSourceReader(
    IDbContextFactory<PmsShadowDbContext> contextFactory) : IArch7aPmsExecutionSourceReader
{
    public async Task<Arch7aPmsExecutionSource> ReadAsync(
        string sourceSessionId,
        Arch7aExecutionSlot slot,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        slot.RequireCanonical();
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("ARCH7A_NOW_MUST_BE_UTC");

        var policy = new PmsShadowFreshnessPolicy(slot.OperationalDate, TimeSpan.FromMinutes(20));
        var snapshot = await new EfPmsShadowOperationalReadService(contextFactory)
            .GetSessionAsync(sourceSessionId, policy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("ARCH7A_COMPLETED_SOURCE_SESSION_NOT_FOUND");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ingestionId = snapshot.LatestSession.IngestionId;
        var account = await context.AccountSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var positions = await context.PositionSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var leaves = await context.WorkingLeavesObservations.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var models = await context.ModelRuns.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId)
            .ToArrayAsync(cancellationToken);
        var modelIds = models.Select(value => value.ModelRunId).ToArray();
        var weights = await context.TargetWeights.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId))
            .ToArrayAsync(cancellationToken);
        var targets = await context.TargetPositions.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId))
            .ToArrayAsync(cancellationToken);
        var drifts = await context.PositionOnlyDrifts.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId))
            .ToArrayAsync(cancellationToken);
        var mappings = await context.SecurityMappings.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId)
            .ToArrayAsync(cancellationToken);
        var marketSnapshot = await context.MarketDataSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var observations = await context.MarketDataObservations.AsNoTracking()
            .Where(value => value.MarketDataSnapshotId == marketSnapshot.MarketDataSnapshotId)
            .ToArrayAsync(cancellationToken);

        var modelById = models.ToDictionary(value => value.ModelRunId);
        var mappingByInstrument = mappings.GroupBy(value => value.InstrumentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(value => value.SecurityId, StringComparer.Ordinal).First());
        var targetByKey = targets.ToDictionary(value => (value.ModelRunId, value.InstrumentId));
        var driftByKey = drifts.ToDictionary(value => (value.ModelRunId, value.InstrumentId));
        var contributions = weights.OrderBy(value => value.ModelRunId)
            .ThenBy(value => value.SourceOrder)
            .Select(weight =>
            {
                var key = (weight.ModelRunId, weight.InstrumentId);
                var model = modelById[weight.ModelRunId];
                var target = targetByKey[key];
                var drift = driftByKey[key];
                var symbol = mappingByInstrument.GetValueOrDefault(weight.InstrumentId)?.Symbol ?? weight.SecurityId;
                var lineage = snapshot.Lineage.Entries.Single(value => value.ModelRunId == weight.ModelRunId);
                return new Arch7aPmsTargetContribution(
                    weight.ModelRunId,
                    model.StrategyId,
                    Arch7aPmsShadowExecutionPipeline.DeterministicGuid(
                        $"target-position|{weight.ModelRunId:D}|{weight.InstrumentId:D}"),
                    Arch7aPmsShadowExecutionPipeline.DeterministicGuid(
                        $"position-only-drift|{weight.ModelRunId:D}|{weight.InstrumentId:D}"),
                    weight.SecurityId,
                    symbol,
                    weight.Weight,
                    drift.CurrentBaseQuantity,
                    target.TargetBaseQuantity,
                    drift.PositionOnlyDeltaBaseQuantity,
                    lineage.InputSha256,
                    model.OutputSha256,
                    model.CoreMasterCommitId);
            }).ToArray();

        var prices = observations
            .Where(value => value.Bid > 0m && value.Ask > 0m)
            .GroupBy(value => NormalizeSymbol(value.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(value => value.EventTimeUtc)
                    .Select(value => (value.Bid + value.Ask) / 2m).First(),
                StringComparer.OrdinalIgnoreCase);
        var increments = mappings
            .GroupBy(value => NormalizeSymbol(value.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(value => value.SecurityId, StringComparer.Ordinal)
                    .Select(value => value.QuantityIncrement).First(),
                StringComparer.OrdinalIgnoreCase);
        var current = positions.BrokerAuthority
            ? await CurrentExecutionQuantities(context, positions.PositionSnapshotId, mappings, cancellationToken)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var status = snapshot.LatestSession.NoOrder &&
                     snapshot.LatestSession.TotalModels == 4 &&
                     snapshot.LatestSession.TotalTargets == 288 &&
                     snapshot.LatestSession.TotalDrifts == 288
            ? Arch7aSourceStatus.Completed
            : Arch7aSourceStatus.Incomplete;
        var freshness = snapshot.Freshness.Status switch
        {
            PmsShadowFreshnessStatus.Fresh => Arch7aSourceFreshness.Fresh,
            PmsShadowFreshnessStatus.Stale => Arch7aSourceFreshness.Stale,
            PmsShadowFreshnessStatus.Incomplete => Arch7aSourceFreshness.Incomplete,
            _ => Arch7aSourceFreshness.Missing
        };
        var workingAuthority = leaves.BrokerAuthority &&
                               leaves.ObservationAttempted &&
                               !leaves.EmptyStateInferred
            ? Arch7aWorkingOrderAuthority.AuthoritativeComplete
            : Arch7aWorkingOrderAuthority.UnavailableWithCurrentLmaxInterfaces;

        return new(
            ingestionId,
            sourceSessionId,
            slot,
            snapshot.LatestSession.CompletedAtUtc,
            NormalizeExecutionEnvironment(snapshot.LatestSession.Environment),
            account.AccountId,
            account.NavOrEquity,
            status,
            freshness,
            LineageComplete: snapshot.Lineage.Entries.Count == models.Length &&
                             snapshot.Lineage.Entries.All(value =>
                                 value.TargetPositionCount == 72 && value.DriftCount == 72),
            PositionAuthority: positions.BrokerAuthority,
            workingAuthority,
            AllowShadowSimulationWhenWorkingLeavesUnknown: true,
            HasCriticalConflict: false,
            contributions,
            prices,
            current,
            increments);
    }

    public static string NormalizeExecutionEnvironment(string sourceEnvironment)
        => sourceEnvironment.Equals(PmsShadowStateContract.TestEnvironment, StringComparison.OrdinalIgnoreCase)
            ? "TEST"
            : sourceEnvironment;
    private static async Task<IReadOnlyDictionary<string, decimal>> CurrentExecutionQuantities(
        PmsShadowDbContext context,
        Guid positionSnapshotId,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappings,
        CancellationToken cancellationToken)
    {
        var lines = await context.PositionSnapshotLines.AsNoTracking()
            .Where(value => value.PositionSnapshotId == positionSnapshotId)
            .ToArrayAsync(cancellationToken);
        var symbolByInstrument = mappings.GroupBy(value => value.InstrumentId)
            .ToDictionary(group => group.Key, group => NormalizeSymbol(group.First().Symbol));
        return lines.Where(value => symbolByInstrument.ContainsKey(value.InstrumentId))
            .GroupBy(value => symbolByInstrument[value.InstrumentId], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.CurrentBaseQuantity),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeSymbol(string value)
        => value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();
}

public sealed class EfArch7aShadowExecutionStore(
    IDbContextFactory<PmsShadowDbContext> contextFactory) : IArch7aShadowExecutionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<Arch7aShadowStoreResult> PersistAsync(
        Arch7aShadowExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (plan.Units.Count == 0)
            throw new InvalidOperationException("ARCH7A_EMPTY_PLAN_NOT_PERSISTED");
        if (!plan.NoBrokerSend || !plan.NoFixLogon || !plan.NoFill || !plan.NoPositionLedgerEvent ||
            plan.NetworkLedger.Count != 0)
            throw new InvalidOperationException("ARCH7A_NO_ORDER_INVARIANT_REQUIRED");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ShadowTradeIntents.AsNoTracking()
            .Where(value => value.SourceSessionId == plan.Netting.SourceSessionId &&
                            value.SlotId == plan.Netting.SlotId)
            .ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            if (existing.All(value => value.PlanSha256 == plan.PlanSha256) &&
                existing.Length == plan.Units.Count)
                return Arch7aShadowStoreResult.AlreadyPersistedIdentical;
            throw new InvalidOperationException("ARCH7A_IDEMPOTENCY_CONFLICT");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var unit in plan.Units)
        {
            var intent = unit.TradeIntent;
            var risk = unit.RiskDecision;
            var parent = unit.ParentOrder;
            var child = unit.ChildOrder;
            context.ShadowTradeIntents.Add(new(
                intent.Canonical.Id.Value,
                intent.SourceIngestionId,
                intent.SourceSessionId,
                intent.SlotId,
                intent.OperationalDate,
                intent.TargetCloseUtc,
                intent.EffectiveFromUtc,
                intent.DeadlineUtc,
                JsonSerializer.Serialize(intent.ModelRunIds, JsonOptions),
                JsonSerializer.Serialize(intent.TargetPositionIds, JsonOptions),
                JsonSerializer.Serialize(intent.DriftIds, JsonOptions),
                intent.SecurityId,
                intent.SecurityIdSource,
                intent.NormalizedPortfolioSymbol,
                intent.ExecutionTradableSymbol,
                intent.RequiresInversion,
                intent.Canonical.Side.ToString().ToUpperInvariant(),
                intent.SignedDesiredDelta,
                intent.TargetQuantity,
                intent.CurrentQuantity,
                intent.AccountScope,
                intent.Environment,
                intent.Classification,
                intent.Actionable,
                intent.ExecutionAllowed,
                intent.BrokerRouteAllowed,
                intent.BlockingReason,
                intent.IdempotencyKey,
                intent.LineageSha256,
                plan.PlanSha256,
                intent.Canonical.CreatedAtUtc));
            context.ShadowRiskDecisions.Add(new(
                risk.Canonical.Id,
                intent.Canonical.Id.Value,
                risk.Outcome.ToString(),
                JsonSerializer.Serialize(risk.ReasonCodes, JsonOptions),
                JsonSerializer.Serialize(risk.BlockingBreaks, JsonOptions),
                risk.SourceComplete,
                risk.PositionAuthority,
                risk.WorkingOrderAuthority,
                risk.Freshness.ToString().ToUpperInvariant(),
                JsonSerializer.Serialize(risk.LimitsEvaluated, JsonOptions),
                risk.NoOrderInvariant,
                risk.BrokerSendAllowed,
                plan.PlanSha256,
                risk.Canonical.CreatedAtUtc));
            context.ShadowParentOrders.Add(new(
                parent.Canonical.Id.Value,
                intent.Canonical.Id.Value,
                risk.Canonical.Id,
                parent.Canonical.ClientOrderId.Value,
                parent.Symbol,
                parent.Canonical.Side.ToString().ToUpperInvariant(),
                parent.TotalQuantity,
                parent.TargetCloseUtc,
                parent.Canonical.Algo.ToString(),
                parent.Status,
                parent.RouteAllowed,
                parent.DeterministicIdentity,
                plan.PlanSha256,
                parent.Canonical.CreatedAtUtc));
            context.ShadowChildOrders.Add(new(
                child.Canonical.Id.Value,
                parent.Canonical.Id.Value,
                child.Canonical.ClientOrderId.Value,
                "LMAX",
                child.Tranche,
                child.Canonical.Side.ToString().ToUpperInvariant(),
                child.Canonical.BaseQuantity,
                child.SimulatedLimitPrice,
                child.EffectiveTimeUtc,
                child.DeadlineUtc,
                child.AlgoPhase.ToString(),
                child.Status,
                child.BrokerSendAllowed,
                child.DeterministicIdentity,
                plan.PlanSha256,
                child.Canonical.CreatedAtUtc));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Arch7aShadowStoreResult.Persisted;
    }
}