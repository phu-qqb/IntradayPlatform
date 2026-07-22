namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowOperationalProjection
{
    public static PmsShadowOperationalReadSnapshot? Latest(IEnumerable<PmsShadowPersistencePlan> plans,
        PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc)
    {
        policy.RequireValid();
        var latest = plans.Where(plan => plan.Ingestion.Status == PmsShadowIngestionStatuses.Completed)
            .OrderByDescending(plan => plan.Ingestion.CompletedAtUtc)
            .ThenByDescending(plan => plan.Ingestion.SourceSessionId, StringComparer.Ordinal)
            .FirstOrDefault();
        return latest is null ? null : Build(latest, policy, nowUtc);
    }

    public static PmsShadowOperationalReadSnapshot Build(PmsShadowPersistencePlan plan,
        PmsShadowFreshnessPolicy policy, DateTimeOffset nowUtc)
    {
        policy.RequireValid();
        if (nowUtc.Offset != TimeSpan.Zero) throw new ArgumentException("NOW_UTC_REQUIRED", nameof(nowUtc));
        if (plan.Ingestion.Status != PmsShadowIngestionStatuses.Completed || plan.Ingestion.CompletedAtUtc is null)
            throw new InvalidOperationException("COMPLETED_INGESTION_REQUIRED");

        var models = plan.ModelRuns.OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();
        var modelById = models.ToDictionary(value => value.ModelRunId);
        var inputById = plan.QubesInputSnapshots.ToDictionary(value => value.SnapshotId);
        var stageById = plan.TargetPositionStages.ToDictionary(value => value.StageId);
        var driftStageById = plan.PositionOnlyDriftStages.ToDictionary(value => value.StageId);
        var priceByInstrument = plan.MarketDataObservations.ToDictionary(value => value.InstrumentId,
            value => (value.Bid + value.Ask) / 2m);
        var cyclesByModel = plan.CycleResults.ToDictionary(value => value.ModelRunId);
        var noOrder = models.All(value => !value.ExecutionAllowed && value.NotAnOrder && !value.AccountingEligible) &&
            plan.CycleResults.Count == 4 && plan.CycleResults.All(IsNoOrderCycle);
        var complete = models.Length == 4 && plan.TargetWeights.Count == 288 &&
            plan.TargetPositions.Count == 288 && plan.PositionOnlyDrifts.Count == 288 &&
            plan.BrokerAdjustedDriftStages.Count == 4 && plan.CycleResults.Count == 4 && noOrder;
        var age = nowUtc - plan.Ingestion.CompletedAtUtc.Value;
        var blockers = new List<string>();
        AddMismatch(models.Length, 4, "EXPECTED_MODEL_RUNS_MISMATCH", blockers);
        AddMismatch(plan.TargetWeights.Count, 288, "EXPECTED_TARGET_WEIGHTS_MISMATCH", blockers);
        AddMismatch(plan.TargetPositions.Count, 288, "EXPECTED_TARGET_POSITIONS_MISMATCH", blockers);
        AddMismatch(plan.PositionOnlyDrifts.Count, 288, "EXPECTED_POSITION_ONLY_DRIFTS_MISMATCH", blockers);
        var freshnessStatus = !complete ? PmsShadowFreshnessStatus.Incomplete :
            plan.AccountSnapshot.ReportDate < policy.ExpectedOperationalDate ? PmsShadowFreshnessStatus.MissingToday :
            age > policy.MaximumIngestionAge ? PmsShadowFreshnessStatus.Stale : PmsShadowFreshnessStatus.Fresh;
        var freshness = new ShadowFreshnessAndCompletenessReadModel(plan.AccountSnapshot.ReportDate,
            age, 4, models.Length, 288, plan.TargetWeights.Count, 288, plan.TargetPositions.Count,
            288, plan.PositionOnlyDrifts.Count, freshnessStatus,
            blockers.Order(StringComparer.Ordinal).ToArray());
        var latest = new LatestShadowSessionReadModel(plan.Ingestion.IngestionId,
            plan.Ingestion.SourceSessionId, plan.AccountSnapshot.ReportDate,
            plan.Ingestion.CompletedAtUtc.Value, freshnessStatus, plan.Ingestion.Environment,
            plan.Ingestion.Classification, plan.Ingestion.SourceEvidenceSha256, models.Length,
            plan.TargetPositions.Count, plan.PositionOnlyDrifts.Count, noOrder);
        var modelRuns = models.Select(value => new ShadowModelRunSummaryReadModel(value.ModelRunId,
            value.StrategyId, value.BenchmarkParameter, value.TargetCloseUtc, value.QubesInputSnapshotId,
            value.OutputSha256, value.CoreMasterCommitId, value.CoreMasterObjectFormat, value.PackageSha256,
            value.SemanticStatus, value.Classification)).ToArray();
        var targets = plan.TargetPositions.Select(value =>
        {
            var model = modelById[value.ModelRunId];
            var stage = stageById[value.StageId];
            var input = inputById[model.QubesInputSnapshotId];
            return new LatestTargetPositionReadModel(model.StrategyId, value.SecurityId,
                value.TargetBaseQuantity, priceByInstrument.GetValueOrDefault(value.InstrumentId),
                model.TargetCloseUtc, stage.AccountSnapshotId, stage.MarketDataSnapshotId,
                model.ModelRunId, input.InputSha256, model.OutputSha256, model.CoreMasterCommitId);
        }).OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal).ToArray();
        var drifts = plan.PositionOnlyDrifts.Select(value => new LatestPositionOnlyDriftReadModel(
            modelById[value.ModelRunId].StrategyId, value.SecurityId, value.CurrentBaseQuantity,
            value.TargetBaseQuantity, value.PositionOnlyDeltaBaseQuantity,
            driftStageById[value.StageId].AsOfUtc, value.Status, value.ModelRunId))
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal).ToArray();
        var broker = plan.BrokerAdjustedDriftStages.Select(value =>
            new BrokerAdjustedDriftStatusReadModel(modelById[value.ModelRunId].StrategyId,
                value.Calculated, value.Blocker, plan.WorkingLeavesObservation.EmptyStateObserved,
                plan.WorkingLeavesObservation.EmptyStateInferred, plan.WorkingLeavesObservation.BrokerAuthority,
                value.Status, value.ModelRunId)).OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();
        var lineageEntries = models.Select(model =>
        {
            var input = inputById[model.QubesInputSnapshotId];
            return new ShadowLineageEntryReadModel(model.StrategyId, input.SnapshotId,
                input.SourceSnapshotSha256, input.OverlaySha256, input.InputSha256, model.ModelRunId,
                model.OutputSha256, model.CoreMasterCommitId,
                plan.TargetWeights.Count(value => value.ModelRunId == model.ModelRunId),
                plan.TargetPositions.Count(value => value.ModelRunId == model.ModelRunId),
                plan.PositionOnlyDrifts.Count(value => value.ModelRunId == model.ModelRunId),
                cyclesByModel[model.ModelRunId].ManualPaperCycleStatus);
        }).ToArray();
        var lineage = new ShadowLineageSummaryReadModel(plan.Ingestion.SourceSessionId,
            plan.SourceArtifacts.OrderBy(value => value.ArtifactType, StringComparer.Ordinal)
                .ThenBy(value => value.Sha256, StringComparer.Ordinal)
                .Select(value => new ShadowArtifactReferenceReadModel(value.ArtifactType, value.Sha256,
                    value.LogicalUri, value.ContractVersion)).ToArray(), lineageEntries);
        var alerts = new List<PmsShadowOperationalAlert>();
        if (freshnessStatus == PmsShadowFreshnessStatus.MissingToday)
            alerts.Add(Alert("DAILY_SESSION_MISSING", "ERROR", latest, nowUtc));
        if (freshnessStatus == PmsShadowFreshnessStatus.Stale)
            alerts.Add(Alert("SHADOW_DATA_STALE", "WARN", latest, nowUtc));
        if (freshnessStatus == PmsShadowFreshnessStatus.Incomplete)
            alerts.Add(Alert("ROW_COUNT_MISMATCH", "ERROR", latest, nowUtc));
        if (!noOrder) alerts.Add(Alert("NO_ORDER_INVARIANT_VIOLATION", "CRITICAL", latest, nowUtc));
        if (broker.Any(value => value.Blocker == PmsShadowStateContract.BrokerAdjustedBlocker))
            alerts.Add(Alert("BROKER_WORKING_LEAVES_UNOBSERVABLE", "WARN", latest, nowUtc));
        return new(latest, modelRuns, targets, drifts, broker, freshness, lineage,
            alerts.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray());
    }

    private static bool IsNoOrderCycle(PmsShadowCycleResultRow value) => !value.ExecutionAllowed &&
        value.NotAnOrder && value.NoBrokerRoute && value.NoFixMessage && !value.OrderEntryEnabled &&
        value.TradeIntentCount == 0 && value.BrokerSendStatus == PmsShadowStateContract.DisabledBrokerSend;

    private static void AddMismatch(int actual, int expected, string issue, ICollection<string> blockers)
    {
        if (actual != expected) blockers.Add(issue);
    }

    private static PmsShadowOperationalAlert Alert(string code, string severity,
        LatestShadowSessionReadModel latest, DateTimeOffset nowUtc) => new(code, severity,
            latest.SourceSessionId, latest.OperationalDate, nowUtc, latest.EvidenceSha256, code);
}
