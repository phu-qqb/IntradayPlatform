using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record PmsShadowTradeIntentRow(
    Guid TradeIntentId,
    Guid IngestionId,
    string SourceSessionId,
    string SlotId,
    Guid EconomicRevisionId,
    int EconomicRevisionNumber,
    string MarketDataSnapshotSha256,
    string SourceLineageSha256,
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

public sealed record PmsShadowExecutionQualificationRunRow(
    Guid QualificationRunId,
    Guid EconomicRevisionId,
    string SourceSessionId,
    string SlotId,
    DateTimeOffset EvaluationAsOfUtc,
    string PlanSha256,
    string NettingSha256,
    int IntentCount,
    int RiskDecisionCount,
    int ParentOrderCount,
    int ChildOrderCount,
    string Status,
    string SourceLineageSha256,
    bool NoFixLogon,
    bool NoBrokerSend,
    bool NoFill,
    bool NoPositionLedgerEvent,
    DateTimeOffset CompletedAtUtc);

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

        var economicStore = new EfPmsShadowIntradayEconomicProjectionStore(contextFactory);
        var projections = await economicStore.ReadAllAsync(cancellationToken);
        var selected = SelectLatestQualifyingRevision(projections, slot.SlotId);
        if (!selected.SourceSessionId.Equals(sourceSessionId, StringComparison.Ordinal))
            throw new InvalidOperationException("ARCH7A_SOURCE_SESSION_REVISION_MISMATCH");
        if (selected.SlotEndUtc != slot.TargetCloseUtc || selected.SlotStartUtc != slot.EffectiveFromUtc)
            throw new InvalidOperationException("ARCH7A_EXECUTION_SLOT_REVISION_WINDOW_MISMATCH");

        var slotRows = await new EfPmsShadowIntradaySlotStore(contextFactory).ReadAllAsync(cancellationToken);
        var intraday = PmsShadowIntradayProjection.Build(slotRows, nowUtc);
        if (intraday.LatestIntradayShadowSlot.Slot?.SlotId != selected.SlotId)
            throw new InvalidOperationException("ARCH7A_SOURCE_NOT_LATEST_INTRADAY_SLOT");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ingestionId = selected.SourceIngestionId;
        var account = await context.AccountSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var positions = await context.PositionSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var leaves = await context.WorkingLeavesObservations.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var modelIds = selected.ReusedModelRunIds.Distinct().Order().ToArray();
        var models = await context.ModelRuns.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId))
            .ToArrayAsync(cancellationToken);
        var mappings = await context.SecurityMappings.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId)
            .ToArrayAsync(cancellationToken);

        var driftByKey = selected.PositionOnlyDrifts.ToDictionary(
            value => (value.ModelRunId, value.InstrumentId));
        var marketByInstrument = selected.MarketData.ToDictionary(value => value.InstrumentId);
        var contributions = selected.TargetPositions
            .OrderBy(value => value.ModelRunId)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal)
            .Select(target =>
            {
                var key = (target.ModelRunId, target.InstrumentId);
                var drift = driftByKey.GetValueOrDefault(key)
                    ?? throw new InvalidOperationException("ARCH7A_REVISION_DRIFT_LINEAGE_MISSING");
                var market = marketByInstrument.GetValueOrDefault(target.InstrumentId)
                    ?? throw new InvalidOperationException("ARCH7A_REVISION_MARKET_LINEAGE_MISSING");
                return new Arch7aPmsTargetContribution(
                    target.ModelRunId,
                    target.StrategyId,
                    target.TargetPositionId,
                    drift.DriftId,
                    target.SecurityId,
                    market.Symbol,
                    target.TargetNotionalUsd / account.NavOrEquity,
                    drift.CurrentBaseQuantity,
                    target.TargetBaseQuantity,
                    drift.Delta,
                    target.InputSha256,
                    target.OutputSha256,
                    target.CoreCommitId);
            }).ToArray();

        var prices = selected.MarketData
            .Where(value => value.DecisionPrice > 0m)
            .GroupBy(value => NormalizeSymbol(value.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(value => value.EventTimeUtc)
                    .Select(value => value.DecisionPrice).First(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var value in prices.ToArray())
        {
            if (value.Key.Length != 6 || value.Value <= 0m)
                continue;
            var inverse = value.Key[3..] + value.Key[..3];
            prices.TryAdd(inverse, 1m / value.Value);
        }

        var increments = mappings
            .GroupBy(value => NormalizeSymbol(value.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(value => value.SecurityId, StringComparer.Ordinal)
                    .Select(value => value.QuantityIncrement).First(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var value in increments.ToArray())
        {
            if (value.Key.Length != 6)
                continue;
            increments.TryAdd(value.Key[3..] + value.Key[..3], value.Value);
        }

        var current = positions.BrokerAuthority
            ? await CurrentExecutionQuantities(context, positions.PositionSnapshotId, mappings, cancellationToken)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var status = selected.Status == "COMPLETED" && selected.Qualifying && selected.NoOrder &&
                     selected.RevisionNumber == 2 && selected.TargetPositions.Count == 288 &&
                     selected.PositionOnlyDrifts.Count == 288 && models.Length == 4
            ? Arch7aSourceStatus.Completed
            : Arch7aSourceStatus.Incomplete;
        var freshness = intraday.SlotFreshnessAndCompleteness.Freshness switch
        {
            PmsShadowIntradayFreshness.Fresh => Arch7aSourceFreshness.Fresh,
            PmsShadowIntradayFreshness.Stale => Arch7aSourceFreshness.Stale,
            PmsShadowIntradayFreshness.Incomplete or PmsShadowIntradayFreshness.FailedClosed =>
                Arch7aSourceFreshness.Incomplete,
            _ => Arch7aSourceFreshness.Missing
        };
        var workingAuthority = leaves.BrokerAuthority && leaves.ObservationAttempted &&
                               !leaves.EmptyStateInferred
            ? Arch7aWorkingOrderAuthority.AuthoritativeComplete
            : Arch7aWorkingOrderAuthority.UnavailableWithCurrentLmaxInterfaces;
        var lineageComplete = models.Length == 4 && modelIds.Length == 4 &&
                              selected.TargetPositions.Count == 288 &&
                              selected.PositionOnlyDrifts.Count == 288 &&
                              selected.MarketData.Count == 99 &&
                              selected.TargetPositions.All(value => modelIds.Contains(value.ModelRunId)) &&
                              selected.PositionOnlyDrifts.All(value => modelIds.Contains(value.ModelRunId));

        return new(
            ingestionId,
            sourceSessionId,
            selected.ProjectionRevisionId,
            selected.RevisionNumber,
            selected.MarketDataSnapshotSha256,
            selected.ManifestSha256,
            nowUtc,
            selected.SelectedModelRuns.Max(value => value.AsOfUtc),
            slot,
            selected.CompletedAtUtc,
            NormalizeExecutionEnvironment(PmsShadowStateContract.TestEnvironment),
            account.AccountId,
            account.NavOrEquity,
            status,
            freshness,
            lineageComplete,
            PositionAuthority: positions.BrokerAuthority,
            workingAuthority,
            AllowShadowSimulationWhenWorkingLeavesUnknown: true,
            HasCriticalConflict: false,
            contributions,
            prices,
            current,
            increments);
    }

    public static PmsShadowIntradayEconomicProjection SelectLatestQualifyingRevision(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> projections,
        string slotId)
    {
        var qualifying = projections.Where(value => value.Status == "COMPLETED" &&
                value.Qualifying && value.NoOrder)
            .OrderBy(value => value.SlotEndUtc)
            .ThenBy(value => value.RevisionNumber)
            .ThenBy(value => value.ProjectionRevisionId)
            .ToArray();
        var selected = qualifying.LastOrDefault(value => value.SlotId == slotId)
            ?? throw new InvalidOperationException("ARCH7A_QUALIFYING_ECONOMIC_REVISION_NOT_FOUND");
        if (selected.RevisionNumber != 2)
            throw new InvalidOperationException("ARCH7A_ECONOMIC_REVISION_TWO_REQUIRED");
        if (qualifying[^1].ProjectionRevisionId != selected.ProjectionRevisionId)
            throw new InvalidOperationException("ARCH7A_SOURCE_NOT_LATEST_QUALIFYING_REVISION");
        if (selected.TargetPositions.Count != 288 || selected.PositionOnlyDrifts.Count != 288 ||
            selected.ReusedModelRunIds.Distinct().Count() != 4)
            throw new InvalidOperationException("ARCH7A_QUALIFYING_REVISION_FACTS_INCOMPLETE");
        return selected;
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
        if (Arch7aPmsShadowExecutionPipeline.ComputePlanSha256(
                plan.Netting, plan.Units, plan.Blockers) != plan.PlanSha256)
            throw new InvalidOperationException("ARCH7A_PLAN_FINGERPRINT_CONFLICT");
        if (!plan.NoBrokerSend || !plan.NoFixLogon || !plan.NoAccountApi || !plan.NoDatabento ||
            !plan.NoRealAccount || !plan.NoFill || !plan.NoPositionLedgerEvent ||
            plan.NetworkLedger.Count != 0)
            throw new InvalidOperationException("ARCH7A_NO_ORDER_INVARIANT_REQUIRED");
        if (plan.Netting.EconomicRevisionNumber != 2 || plan.Netting.EconomicRevisionId == Guid.Empty ||
            plan.Units.Any(value => value.TradeIntent.EconomicRevisionId != plan.Netting.EconomicRevisionId ||
                                    value.TradeIntent.EconomicRevisionNumber != 2))
            throw new InvalidOperationException("ARCH7A_QUALIFYING_ECONOMIC_REVISION_REQUIRED");

        return await Arch7aPostgreSqlSerializationRetry.ExecuteAsync(
            () => PersistOnceAsync(plan, cancellationToken));
    }

    private async Task<Arch7aShadowStoreResult> PersistOnceAsync(
        Arch7aShadowExecutionPlan plan,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"arch7a|{plan.Netting.EconomicRevisionId:D}")), 0);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);

        var existing = await context.ShadowTradeIntents.AsNoTracking()
            .Where(value => value.EconomicRevisionId == plan.Netting.EconomicRevisionId)
            .ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            var intentIds = existing.Select(value => value.TradeIntentId).ToArray();
            var parentIds = await context.ShadowParentOrders.AsNoTracking()
                .Where(value => intentIds.Contains(value.TradeIntentId))
                .Select(value => value.ParentOrderId)
                .ToArrayAsync(cancellationToken);
            var riskCount = await context.ShadowRiskDecisions.AsNoTracking()
                .CountAsync(value => intentIds.Contains(value.TradeIntentId), cancellationToken);
            var parentCount = parentIds.Length;
            var childCount = await context.ShadowChildOrders.AsNoTracking()
                .CountAsync(value => parentIds.Contains(value.ParentOrderId), cancellationToken);
            var run = await context.ShadowExecutionQualificationRuns.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.EconomicRevisionId == plan.Netting.EconomicRevisionId, cancellationToken);
            if (existing.All(value => value.PlanSha256 == plan.PlanSha256) &&
                existing.Length == plan.Units.Count && riskCount == plan.Units.Count &&
                parentCount == plan.Units.Count && childCount == plan.Units.Count &&
                run is not null && run.PlanSha256 == plan.PlanSha256 && run.Status == "COMPLETED")
            {
                await transaction.CommitAsync(cancellationToken);
                return Arch7aShadowStoreResult.AlreadyPersistedIdentical;
            }
            throw new InvalidOperationException("ARCH7A_IDEMPOTENCY_CONFLICT");
        }

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
                intent.EconomicRevisionId,
                intent.EconomicRevisionNumber,
                intent.MarketDataSnapshotSha256,
                intent.SourceLineageSha256,
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
        var qualificationRunId = Arch7aPmsShadowExecutionPipeline.DeterministicGuid(
            $"arch7a-qualification|{plan.Netting.EconomicRevisionId:D}|{plan.PlanSha256}");
        context.ShadowExecutionQualificationRuns.Add(new(
            qualificationRunId,
            plan.Netting.EconomicRevisionId,
            plan.Netting.SourceSessionId,
            plan.Netting.SlotId,
            plan.Netting.EvaluationAsOfUtc,
            plan.PlanSha256,
            plan.Netting.NettingSha256,
            plan.Units.Count,
            plan.Units.Count,
            plan.Units.Count,
            plan.Units.Count,
            "COMPLETED",
            plan.Netting.SourceLineageSha256,
            plan.NoFixLogon,
            plan.NoBrokerSend,
            plan.NoFill,
            plan.NoPositionLedgerEvent,
            plan.Netting.EvaluationAsOfUtc));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Arch7aShadowStoreResult.Persisted;
    }
}

public static class Arch7aPostgreSqlSerializationRetry
{
    public const int MaxRetries = 1;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception exception) when (attempt < MaxRetries && IsSerializationFailure(exception))
            {
                // PostgreSQL requires a fresh serializable transaction after a concurrent writer commits.
            }
        }
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
                return true;
        return false;
    }
}
