using Microsoft.EntityFrameworkCore;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public enum PmsShadowFreshnessStatus
{
    Fresh,
    Stale,
    MissingToday,
    Incomplete,
    FailedClosed
}

public sealed record PmsShadowFreshnessPolicy(DateOnly ExpectedOperationalDate, TimeSpan MaximumIngestionAge)
{
    public void RequireValid()
    {
        if (MaximumIngestionAge <= TimeSpan.Zero)
            throw new InvalidOperationException("FRESHNESS_POLICY_MAXIMUM_AGE_REQUIRED");
    }
}

public sealed record LatestShadowSessionReadModel(Guid IngestionId, string SourceSessionId,
    DateOnly OperationalDate, DateTimeOffset CompletedAtUtc, PmsShadowFreshnessStatus Freshness,
    string Environment, string Classification, string EvidenceSha256, int TotalModels,
    int TotalTargets, int TotalDrifts, bool NoOrder);

public sealed record ShadowModelRunSummaryReadModel(Guid ModelRunId, string StrategyId,
    decimal Parameter, DateTimeOffset TargetCloseUtc, Guid InputSnapshotId, string OutputSha256,
    string CoreCommitId, string CoreObjectFormat, string PackageSha256, string SemanticStatus,
    string Classification);

public sealed record LatestTargetPositionReadModel(string StrategyId, string SecurityId,
    decimal TargetQuantity, decimal? DecisionPrice, DateTimeOffset TargetCloseUtc,
    Guid AccountSnapshotId, Guid MarketSnapshotId, Guid ModelRunId, string InputSha256,
    string OutputSha256, string CoreCommitId);

public sealed record LatestPositionOnlyDriftReadModel(string StrategyId, string SecurityId,
    decimal CurrentQuantity, decimal TargetQuantity, decimal Delta, DateTimeOffset SnapshotAsOfUtc,
    string Status, Guid ModelRunId);

public sealed record BrokerAdjustedDriftStatusReadModel(string StrategyId, bool Calculated,
    string Blocker, bool EmptyStateObserved, bool EmptyStateInferred, bool BrokerAuthority,
    string Status, Guid ModelRunId);

public sealed record ShadowFreshnessAndCompletenessReadModel(DateOnly? LatestCompletedOperationalDate,
    TimeSpan? IngestionAge, int ExpectedStrategies, int ActualStrategies, int ExpectedTargetWeights,
    int ActualTargetWeights, int ExpectedTargetPositions, int ActualTargetPositions,
    int ExpectedDrifts, int ActualDrifts, PmsShadowFreshnessStatus Status,
    IReadOnlyList<string> Blockers);

public sealed record ShadowArtifactReferenceReadModel(string ArtifactType, string Sha256,
    string LogicalUri, string ContractVersion);

public sealed record ShadowLineageEntryReadModel(string StrategyId, Guid QubesInputSnapshotId,
    string SourceSnapshotSha256, string OverlaySha256, string InputSha256, Guid ModelRunId,
    string OutputSha256, string CoreCommitId, int TargetWeightCount, int TargetPositionCount,
    int DriftCount, string CycleStatus);

public sealed record ShadowLineageSummaryReadModel(string SourceSessionId,
    IReadOnlyList<ShadowArtifactReferenceReadModel> SourceArtifacts,
    IReadOnlyList<ShadowLineageEntryReadModel> Entries);

public sealed record PmsShadowOperationalReadSnapshot(LatestShadowSessionReadModel LatestSession,
    IReadOnlyList<ShadowModelRunSummaryReadModel> ModelRuns,
    IReadOnlyList<LatestTargetPositionReadModel> TargetPositions,
    IReadOnlyList<LatestPositionOnlyDriftReadModel> PositionOnlyDrifts,
    IReadOnlyList<BrokerAdjustedDriftStatusReadModel> BrokerAdjustedDrifts,
    ShadowFreshnessAndCompletenessReadModel Freshness,
    ShadowLineageSummaryReadModel Lineage,
    IReadOnlyList<PmsShadowOperationalAlert> Alerts);

public interface IPmsShadowOperationalReadService
{
    Task<PmsShadowOperationalReadSnapshot?> GetLatestAsync(PmsShadowFreshnessPolicy policy,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    Task<PmsShadowOperationalReadSnapshot?> GetSessionAsync(string sourceSessionId,
        PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class EfPmsShadowOperationalReadService(IDbContextFactory<PmsShadowDbContext> contextFactory)
    : IPmsShadowOperationalReadService
{
    public async Task<PmsShadowOperationalReadSnapshot?> GetLatestAsync(PmsShadowFreshnessPolicy policy,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        policy.RequireValid();
        RequireUtc(nowUtc);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ingestion = await context.Ingestions.AsNoTracking()
            .Where(value => value.Status == PmsShadowIngestionStatuses.Completed)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.SourceSessionId)
            .FirstOrDefaultAsync(cancellationToken);
        return ingestion is null ? null : await LoadAsync(context, ingestion, policy, nowUtc, cancellationToken);
    }

    public async Task<PmsShadowOperationalReadSnapshot?> GetSessionAsync(string sourceSessionId,
        PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        policy.RequireValid();
        RequireUtc(nowUtc);
        if (string.IsNullOrWhiteSpace(sourceSessionId))
            throw new ArgumentException("SOURCE_SESSION_ID_REQUIRED", nameof(sourceSessionId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ingestion = await context.Ingestions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.SourceSessionId == sourceSessionId &&
                value.Status == PmsShadowIngestionStatuses.Completed, cancellationToken);
        return ingestion is null ? null : await LoadAsync(context, ingestion, policy, nowUtc, cancellationToken);
    }

    private static async Task<PmsShadowOperationalReadSnapshot> LoadAsync(PmsShadowDbContext context,
        PmsShadowIngestionRow ingestion, PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var ingestionId = ingestion.IngestionId;
        var account = await context.AccountSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var market = await context.MarketDataSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var leaves = await context.WorkingLeavesObservations.AsNoTracking()
            .SingleAsync(value => value.IngestionId == ingestionId, cancellationToken);
        var models = await context.ModelRuns.AsNoTracking().Where(value => value.IngestionId == ingestionId)
            .OrderBy(value => value.StrategyId).ThenBy(value => value.ModelRunId).ToArrayAsync(cancellationToken);
        var modelIds = models.Select(value => value.ModelRunId).ToArray();
        var inputs = await context.QubesInputSnapshots.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId).ToArrayAsync(cancellationToken);
        var inputById = inputs.ToDictionary(value => value.SnapshotId);
        var targetWeights = await context.TargetWeights.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId)).ToArrayAsync(cancellationToken);
        var targetStages = await context.TargetPositionStages.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId)).ToArrayAsync(cancellationToken);
        var targetStageIds = targetStages.Select(value => value.StageId).ToArray();
        var targets = await context.TargetPositions.AsNoTracking()
            .Where(value => targetStageIds.Contains(value.StageId)).ToArrayAsync(cancellationToken);
        var driftStages = await context.PositionOnlyDriftStages.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId)).ToArrayAsync(cancellationToken);
        var driftStageIds = driftStages.Select(value => value.StageId).ToArray();
        var drifts = await context.PositionOnlyDrifts.AsNoTracking()
            .Where(value => driftStageIds.Contains(value.StageId)).ToArrayAsync(cancellationToken);
        var brokerStages = await context.BrokerAdjustedDriftStages.AsNoTracking()
            .Where(value => modelIds.Contains(value.ModelRunId)).ToArrayAsync(cancellationToken);
        var cycles = await context.CycleResults.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId).ToArrayAsync(cancellationToken);
        var observations = await context.MarketDataObservations.AsNoTracking()
            .Where(value => value.MarketDataSnapshotId == market.MarketDataSnapshotId)
            .ToArrayAsync(cancellationToken);
        var artifacts = await context.SourceArtifacts.AsNoTracking()
            .Where(value => value.IngestionId == ingestionId).ToArrayAsync(cancellationToken);

        var modelById = models.ToDictionary(value => value.ModelRunId);
        var targetStageById = targetStages.ToDictionary(value => value.StageId);
        var decisionPrices = observations.ToDictionary(value => value.InstrumentId,
            value => (value.Bid + value.Ask) / 2m);
        var modelSummaries = models.Select(value => new ShadowModelRunSummaryReadModel(
            value.ModelRunId, value.StrategyId, value.BenchmarkParameter, value.TargetCloseUtc,
            value.QubesInputSnapshotId, value.OutputSha256, value.CoreMasterCommitId,
            value.CoreMasterObjectFormat, value.PackageSha256, value.SemanticStatus,
            value.Classification)).ToArray();
        var targetReadModels = targets.Select(value =>
        {
            var stage = targetStageById[value.StageId];
            var model = modelById[value.ModelRunId];
            var input = inputById[model.QubesInputSnapshotId];
            return new LatestTargetPositionReadModel(model.StrategyId, value.SecurityId,
                value.TargetBaseQuantity, decisionPrices.GetValueOrDefault(value.InstrumentId),
                model.TargetCloseUtc, stage.AccountSnapshotId, stage.MarketDataSnapshotId,
                model.ModelRunId, input.InputSha256, model.OutputSha256, model.CoreMasterCommitId);
        }).OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal).ToArray();
        var driftReadModels = drifts.Select(value => new LatestPositionOnlyDriftReadModel(
            modelById[value.ModelRunId].StrategyId, value.SecurityId, value.CurrentBaseQuantity,
            value.TargetBaseQuantity, value.PositionOnlyDeltaBaseQuantity,
            driftStages.Single(stage => stage.StageId == value.StageId).AsOfUtc,
            value.Status, value.ModelRunId)).OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal).ToArray();
        var brokerReadModels = brokerStages.Select(value => new BrokerAdjustedDriftStatusReadModel(
            modelById[value.ModelRunId].StrategyId, value.Calculated, value.Blocker,
            leaves.EmptyStateObserved, leaves.EmptyStateInferred, leaves.BrokerAuthority,
            value.Status, value.ModelRunId)).OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();

        var complete = models.Length == 4 && targetWeights.Length == 288 && targets.Length == 288 &&
            drifts.Length == 288 && brokerStages.Length == 4 && cycles.Length == 4 &&
            cycles.All(IsNoOrderCycle);
        var freshness = BuildFreshness(account.ReportDate, ingestion.CompletedAtUtc!.Value,
            models.Length, targetWeights.Length, targets.Length, drifts.Length, complete, policy, nowUtc);
        var noOrder = cycles.Length == 4 && cycles.All(IsNoOrderCycle) &&
            models.All(value => !value.ExecutionAllowed && value.NotAnOrder && !value.AccountingEligible);
        var latest = new LatestShadowSessionReadModel(ingestionId, ingestion.SourceSessionId,
            account.ReportDate, ingestion.CompletedAtUtc.Value, freshness.Status, ingestion.Environment,
            ingestion.Classification, ingestion.SourceEvidenceSha256, models.Length, targets.Length,
            drifts.Length, noOrder);
        var cycleByModel = cycles.ToDictionary(value => value.ModelRunId);
        var lineageEntries = models.Select(model =>
        {
            var input = inputById[model.QubesInputSnapshotId];
            return new ShadowLineageEntryReadModel(model.StrategyId, input.SnapshotId,
                input.SourceSnapshotSha256, input.OverlaySha256, input.InputSha256, model.ModelRunId,
                model.OutputSha256, model.CoreMasterCommitId,
                targetWeights.Count(value => value.ModelRunId == model.ModelRunId),
                targets.Count(value => value.ModelRunId == model.ModelRunId),
                drifts.Count(value => value.ModelRunId == model.ModelRunId),
                cycleByModel[model.ModelRunId].ManualPaperCycleStatus);
        }).OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();
        var lineage = new ShadowLineageSummaryReadModel(ingestion.SourceSessionId,
            artifacts.OrderBy(value => value.ArtifactType, StringComparer.Ordinal)
                .ThenBy(value => value.Sha256, StringComparer.Ordinal)
                .Select(value => new ShadowArtifactReferenceReadModel(value.ArtifactType,
                    value.Sha256, value.LogicalUri, value.ContractVersion)).ToArray(), lineageEntries);
        var alerts = BuildAlerts(latest, freshness, brokerReadModels, nowUtc);
        return new(latest, modelSummaries, targetReadModels, driftReadModels,
            brokerReadModels, freshness, lineage, alerts);
    }

    private static ShadowFreshnessAndCompletenessReadModel BuildFreshness(DateOnly operationalDate,
        DateTimeOffset completedAtUtc, int models, int weights, int targets, int drifts, bool complete,
        PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc)
    {
        var blockers = new List<string>();
        if (models != 4) blockers.Add("EXPECTED_MODEL_RUNS_MISMATCH");
        if (weights != 288) blockers.Add("EXPECTED_TARGET_WEIGHTS_MISMATCH");
        if (targets != 288) blockers.Add("EXPECTED_TARGET_POSITIONS_MISMATCH");
        if (drifts != 288) blockers.Add("EXPECTED_POSITION_ONLY_DRIFTS_MISMATCH");
        var age = nowUtc - completedAtUtc;
        var status = !complete ? PmsShadowFreshnessStatus.Incomplete :
            operationalDate < policy.ExpectedOperationalDate ? PmsShadowFreshnessStatus.MissingToday :
            age > policy.MaximumIngestionAge ? PmsShadowFreshnessStatus.Stale : PmsShadowFreshnessStatus.Fresh;
        return new(operationalDate, age, 4, models, 288, weights, 288, targets, 288, drifts,
            status, blockers.Order(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<PmsShadowOperationalAlert> BuildAlerts(LatestShadowSessionReadModel latest,
        ShadowFreshnessAndCompletenessReadModel freshness,
        IReadOnlyList<BrokerAdjustedDriftStatusReadModel> brokerStatuses, DateTimeOffset nowUtc)
    {
        var alerts = new List<PmsShadowOperationalAlert>();
        if (freshness.Status == PmsShadowFreshnessStatus.MissingToday)
            alerts.Add(Alert("DAILY_SESSION_MISSING", "ERROR", latest, nowUtc,
                "No completed session exists for the expected operational date."));
        if (freshness.Status == PmsShadowFreshnessStatus.Stale)
            alerts.Add(Alert("SHADOW_DATA_STALE", "WARN", latest, nowUtc,
                "The latest completed ingestion exceeds the configured maximum age."));
        if (freshness.Status == PmsShadowFreshnessStatus.Incomplete)
            alerts.Add(Alert("ROW_COUNT_MISMATCH", "ERROR", latest, nowUtc,
                string.Join(';', freshness.Blockers)));
        if (!latest.NoOrder)
            alerts.Add(Alert("NO_ORDER_INVARIANT_VIOLATION", "CRITICAL", latest, nowUtc,
                "A stored cycle or model run violates the no-order invariant."));
        if (brokerStatuses.Any(value => value.Blocker == PmsShadowStateContract.BrokerAdjustedBlocker))
            alerts.Add(Alert("BROKER_WORKING_LEAVES_UNOBSERVABLE", "WARN", latest, nowUtc,
                "Broker-adjusted drift remains blocked because working leaves are unobservable."));
        return alerts.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
    }

    private static bool IsNoOrderCycle(PmsShadowCycleResultRow value) => !value.ExecutionAllowed &&
        value.NotAnOrder && value.NoBrokerRoute && value.NoFixMessage && !value.OrderEntryEnabled &&
        value.TradeIntentCount == 0 && value.BrokerSendStatus == PmsShadowStateContract.DisabledBrokerSend;

    private static PmsShadowOperationalAlert Alert(string code, string severity,
        LatestShadowSessionReadModel latest, DateTimeOffset nowUtc, string reason) => new(code,
            severity, latest.SourceSessionId, latest.OperationalDate, nowUtc,
            latest.EvidenceSha256, reason);

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("NOW_UTC_REQUIRED", nameof(value));
    }
}
