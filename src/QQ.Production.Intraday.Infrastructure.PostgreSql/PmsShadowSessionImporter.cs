using System.Data;
using Microsoft.EntityFrameworkCore;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowIngestionStatuses
{
    public const string Applying = "APPLYING";
    public const string Completed = "COMPLETED";
}

public sealed record PmsShadowImportRequest(
    string Environment,
    bool NoOrder,
    string SchemaContractVersion);

public sealed record PmsShadowStorePreflight(
    string ProviderName,
    IReadOnlyList<string> AppliedMigrations);

public sealed record PmsShadowImportOutcome(
    PmsShadowApplyResult Result,
    Guid IngestionId,
    string SourceSessionId,
    string SourceEvidenceSha256,
    string RowsetSha256,
    IReadOnlyDictionary<string, int> RowCounts);

public interface IPmsShadowSessionImportStore
{
    Task<PmsShadowStorePreflight> InspectAsync(CancellationToken cancellationToken = default);

    Task<PmsShadowImportOutcome> ImportAtomicallyAsync(
        PmsShadowPersistencePlan plan,
        CancellationToken cancellationToken = default);
}

public sealed class Arch6bPmsShadowSessionImporter(IPmsShadowSessionImportStore store)
{
    public async Task<PmsShadowImportOutcome> ImportAsync(
        PmsShadowPersistencePlan plan,
        PmsShadowImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Environment, "TEST", StringComparison.Ordinal))
            throw new InvalidOperationException("PMS_SHADOW_IMPORT_REQUIRES_TEST_ENVIRONMENT");
        if (!request.NoOrder)
            throw new InvalidOperationException("PMS_SHADOW_IMPORT_REQUIRES_NO_ORDER");
        if (!string.Equals(request.SchemaContractVersion, PmsShadowStateContract.ContractVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("PMS_SHADOW_SCHEMA_CONTRACT_VERSION_MISMATCH");

        var validation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(';', validation.Issues));

        var preflight = await store.InspectAsync(cancellationToken);
        if (!string.Equals(preflight.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            throw new InvalidOperationException("POSTGRESQL_PROVIDER_REQUIRED");
        if (!preflight.AppliedMigrations.SequenceEqual(PmsShadowStateContract.MigrationIds, StringComparer.Ordinal))
            throw new InvalidOperationException("EXPECTED_PMS_SHADOW_MIGRATION_NOT_APPLIED");

        return await store.ImportAtomicallyAsync(plan, cancellationToken);
    }
}

public sealed class EfPmsShadowSessionImportStore(IDbContextFactory<PmsShadowDbContext> contextFactory)
    : IPmsShadowSessionImportStore
{
    public async Task<PmsShadowStorePreflight> InspectAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var migrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return new(context.Database.ProviderName ?? string.Empty, migrations.ToArray());
    }

    public async Task<PmsShadowImportOutcome> ImportAtomicallyAsync(
        PmsShadowPersistencePlan plan,
        CancellationToken cancellationToken = default)
    {
        var validation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(';', validation.Issues));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!string.Equals(context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            throw new InvalidOperationException("POSTGRESQL_PROVIDER_REQUIRED");

        var lockKey = BitConverter.ToInt64(plan.Ingestion.IngestionId.ToByteArray(), 0);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({lockKey})", cancellationToken);

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await context.Ingestions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceSessionId == plan.Ingestion.SourceSessionId, cancellationToken);
            if (existing is not null)
            {
                await EnsureExistingSessionMatchesAsync(context, existing, plan, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Outcome(PmsShadowApplyResult.AlreadyAppliedIdentical, plan);
            }

            await EnsureNoConflictingIdentitiesAsync(context, plan, cancellationToken);

            var applying = plan.Ingestion with
            {
                Status = PmsShadowIngestionStatuses.Applying,
                CompletedAtUtc = null
            };
            context.Ingestions.Add(applying);
            await context.SaveChangesAsync(cancellationToken);

            context.SourceArtifacts.AddRange(plan.SourceArtifacts);
            await context.SaveChangesAsync(cancellationToken);

            context.AccountSnapshots.Add(plan.AccountSnapshot);
            context.PositionSnapshots.Add(plan.PositionSnapshot);
            context.PositionSnapshotLines.AddRange(plan.PositionSnapshotLines);
            context.MarketDataSnapshots.Add(plan.MarketDataSnapshot);
            context.MarketDataObservations.AddRange(plan.MarketDataObservations);
            context.SecurityMappings.AddRange(plan.SecurityMappings);
            context.WorkingLeavesObservations.Add(plan.WorkingLeavesObservation);
            await context.SaveChangesAsync(cancellationToken);

            context.QubesInputSnapshots.AddRange(plan.QubesInputSnapshots);
            await context.SaveChangesAsync(cancellationToken);

            context.ModelRuns.AddRange(plan.ModelRuns);
            await context.SaveChangesAsync(cancellationToken);

            context.TargetWeights.AddRange(plan.TargetWeights);
            await context.SaveChangesAsync(cancellationToken);

            context.TargetPositionStages.AddRange(plan.TargetPositionStages);
            context.TargetPositions.AddRange(plan.TargetPositions);
            await context.SaveChangesAsync(cancellationToken);

            context.PositionOnlyDriftStages.AddRange(plan.PositionOnlyDriftStages);
            context.PositionOnlyDrifts.AddRange(plan.PositionOnlyDrifts);
            await context.SaveChangesAsync(cancellationToken);

            context.BrokerAdjustedDriftStages.AddRange(plan.BrokerAdjustedDriftStages);
            context.CycleResults.AddRange(plan.CycleResults);
            await context.SaveChangesAsync(cancellationToken);

            var ingestionEntry = context.Entry(applying);
            ingestionEntry.Property(x => x.Status).CurrentValue = PmsShadowIngestionStatuses.Completed;
            ingestionEntry.Property(x => x.CompletedAtUtc).CurrentValue = plan.Ingestion.CompletedAtUtc;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Outcome(PmsShadowApplyResult.Applied, plan);
        }
        finally
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({lockKey})", CancellationToken.None);
            await context.Database.CloseConnectionAsync();
        }
    }

    public static IReadOnlyDictionary<string, int> ExpectedRowCounts(PmsShadowPersistencePlan plan) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ingestions"] = 1,
            ["source_artifacts"] = plan.SourceArtifacts.Count,
            ["qubes_input_snapshots"] = plan.QubesInputSnapshots.Count,
            ["account_snapshots"] = 1,
            ["position_snapshots"] = 1,
            ["position_snapshot_lines"] = plan.PositionSnapshotLines.Count,
            ["market_data_snapshots"] = 1,
            ["market_data_observations"] = plan.MarketDataObservations.Count,
            ["security_mappings"] = plan.SecurityMappings.Count,
            ["working_leaves_observations"] = 1,
            ["model_runs"] = plan.ModelRuns.Count,
            ["target_weights"] = plan.TargetWeights.Count,
            ["target_position_stages"] = plan.TargetPositionStages.Count,
            ["target_positions"] = plan.TargetPositions.Count,
            ["position_only_drift_stages"] = plan.PositionOnlyDriftStages.Count,
            ["position_only_drifts"] = plan.PositionOnlyDrifts.Count,
            ["broker_adjusted_drift_stages"] = plan.BrokerAdjustedDriftStages.Count,
            ["cycle_results"] = plan.CycleResults.Count
        };

    private static PmsShadowImportOutcome Outcome(PmsShadowApplyResult result, PmsShadowPersistencePlan plan) =>
        new(result, plan.Ingestion.IngestionId, plan.Ingestion.SourceSessionId,
            plan.Ingestion.SourceEvidenceSha256, plan.RowsetSha256, ExpectedRowCounts(plan));

    private static async Task EnsureExistingSessionMatchesAsync(
        PmsShadowDbContext context,
        PmsShadowIngestionRow existing,
        PmsShadowPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.SourceEvidenceSha256, plan.Ingestion.SourceEvidenceSha256, StringComparison.Ordinal))
            throw new InvalidDataException("SOURCE_SESSION_EVIDENCE_SHA_CONFLICT");
        if (!string.Equals(existing.RowsetSha256, plan.RowsetSha256, StringComparison.Ordinal))
            throw new InvalidDataException("SOURCE_SESSION_ROWSET_SHA_CONFLICT");
        if (!string.Equals(existing.Status, PmsShadowIngestionStatuses.Completed, StringComparison.Ordinal))
            throw new InvalidDataException("SOURCE_SESSION_INCOMPLETE");

        var storedArtifacts = await context.SourceArtifacts.AsNoTracking()
            .Where(x => x.IngestionId == existing.IngestionId)
            .ToDictionaryAsync(x => x.ArtifactId, cancellationToken);
        foreach (var artifact in plan.SourceArtifacts)
        {
            if (!storedArtifacts.TryGetValue(artifact.ArtifactId, out var stored) ||
                stored.Sha256 != artifact.Sha256 || stored.SizeBytes != artifact.SizeBytes ||
                stored.LogicalUri != artifact.LogicalUri || stored.ContractVersion != artifact.ContractVersion)
                throw new InvalidDataException("SOURCE_ARTIFACT_CONTENT_CONFLICT");
        }

        var storedModels = await context.ModelRuns.AsNoTracking()
            .Where(x => x.IngestionId == existing.IngestionId)
            .Select(x => new { x.ModelRunId, x.OutputSha256, x.CoreMasterCommitId, x.CoreMasterObjectFormat })
            .ToDictionaryAsync(x => x.ModelRunId, cancellationToken);
        foreach (var model in plan.ModelRuns)
        {
            if (!storedModels.TryGetValue(model.ModelRunId, out var stored))
                throw new InvalidDataException("MODEL_RUN_IDENTITY_CONFLICT");
            if (stored.OutputSha256 != model.OutputSha256)
                throw new InvalidDataException("MODEL_RUN_OUTPUT_SHA_CONFLICT");
            if (stored.CoreMasterCommitId != model.CoreMasterCommitId)
                throw new InvalidDataException("MODEL_RUN_CORE_COMMIT_ID_CONFLICT");
            if (stored.CoreMasterObjectFormat != model.CoreMasterObjectFormat)
                throw new InvalidDataException("MODEL_RUN_CORE_OBJECT_FORMAT_CONFLICT");
        }

        var storedInputs = await context.QubesInputSnapshots.AsNoTracking()
            .Where(x => x.IngestionId == existing.IngestionId)
            .ToDictionaryAsync(x => x.SnapshotId, x => x.InputSha256, cancellationToken);
        if (plan.QubesInputSnapshots.Any(x => !storedInputs.TryGetValue(x.SnapshotId, out var sha) || sha != x.InputSha256))
            throw new InvalidDataException("QUBES_INPUT_SNAPSHOT_CONTENT_CONFLICT");

        var account = await context.AccountSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IngestionId == existing.IngestionId, cancellationToken);
        if (account?.AccountSnapshotId != plan.AccountSnapshot.AccountSnapshotId ||
            account.SnapshotSha256 != plan.AccountSnapshot.SnapshotSha256)
            throw new InvalidDataException("ACCOUNT_SNAPSHOT_CONTENT_CONFLICT");

        var positions = await context.PositionSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IngestionId == existing.IngestionId, cancellationToken);
        if (positions?.PositionSnapshotId != plan.PositionSnapshot.PositionSnapshotId ||
            positions.SnapshotSha256 != plan.PositionSnapshot.SnapshotSha256)
            throw new InvalidDataException("POSITION_SNAPSHOT_CONTENT_CONFLICT");

        var market = await context.MarketDataSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IngestionId == existing.IngestionId, cancellationToken);
        if (market?.MarketDataSnapshotId != plan.MarketDataSnapshot.MarketDataSnapshotId ||
            market.SnapshotSha256 != plan.MarketDataSnapshot.SnapshotSha256)
            throw new InvalidDataException("MARKET_DATA_SNAPSHOT_CONTENT_CONFLICT");

        var actualCounts = await ReadRowCountsAsync(context, plan, cancellationToken);
        foreach (var expected in ExpectedRowCounts(plan))
            if (!actualCounts.TryGetValue(expected.Key, out var actual) || actual != expected.Value)
                throw new InvalidDataException($"EXISTING_SESSION_ROW_COUNT_MISMATCH:{expected.Key}");
    }

    private static async Task EnsureNoConflictingIdentitiesAsync(
        PmsShadowDbContext context,
        PmsShadowPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var modelIds = plan.ModelRuns.Select(x => x.ModelRunId).ToArray();
        var existingModels = await context.ModelRuns.AsNoTracking()
            .Where(x => modelIds.Contains(x.ModelRunId))
            .Select(x => new { x.ModelRunId, x.OutputSha256, x.CoreMasterCommitId, x.CoreMasterObjectFormat })
            .ToArrayAsync(cancellationToken);
        foreach (var existing in existingModels)
        {
            var expected = plan.ModelRuns.Single(x => x.ModelRunId == existing.ModelRunId);
            if (existing.OutputSha256 != expected.OutputSha256)
                throw new InvalidDataException("MODEL_RUN_OUTPUT_SHA_CONFLICT");
            if (existing.CoreMasterCommitId != expected.CoreMasterCommitId)
                throw new InvalidDataException("MODEL_RUN_CORE_COMMIT_ID_CONFLICT");
            if (existing.CoreMasterObjectFormat != expected.CoreMasterObjectFormat)
                throw new InvalidDataException("MODEL_RUN_CORE_OBJECT_FORMAT_CONFLICT");
            throw new InvalidDataException("MODEL_RUN_ID_ALREADY_OWNED_BY_ANOTHER_SESSION");
        }

        var inputIds = plan.QubesInputSnapshots.Select(x => x.SnapshotId).ToArray();
        var existingInputs = await context.QubesInputSnapshots.AsNoTracking()
            .Where(x => inputIds.Contains(x.SnapshotId))
            .Select(x => new { x.SnapshotId, x.InputSha256 })
            .ToArrayAsync(cancellationToken);
        foreach (var existing in existingInputs)
        {
            var expected = plan.QubesInputSnapshots.Single(x => x.SnapshotId == existing.SnapshotId);
            if (existing.InputSha256 != expected.InputSha256)
                throw new InvalidDataException("QUBES_INPUT_SNAPSHOT_CONTENT_CONFLICT");
            throw new InvalidDataException("SNAPSHOT_ID_ALREADY_OWNED_BY_ANOTHER_SESSION");
        }
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadRowCountsAsync(
        PmsShadowDbContext context,
        PmsShadowPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var ingestionId = plan.Ingestion.IngestionId;
        var modelIds = plan.ModelRuns.Select(x => x.ModelRunId).ToArray();
        var targetStageIds = plan.TargetPositionStages.Select(x => x.StageId).ToArray();
        var driftStageIds = plan.PositionOnlyDriftStages.Select(x => x.StageId).ToArray();
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ingestions"] = await context.Ingestions.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["source_artifacts"] = await context.SourceArtifacts.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["qubes_input_snapshots"] = await context.QubesInputSnapshots.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["account_snapshots"] = await context.AccountSnapshots.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["position_snapshots"] = await context.PositionSnapshots.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["position_snapshot_lines"] = await context.PositionSnapshotLines.CountAsync(x => x.PositionSnapshotId == plan.PositionSnapshot.PositionSnapshotId, cancellationToken),
            ["market_data_snapshots"] = await context.MarketDataSnapshots.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["market_data_observations"] = await context.MarketDataObservations.CountAsync(x => x.MarketDataSnapshotId == plan.MarketDataSnapshot.MarketDataSnapshotId, cancellationToken),
            ["security_mappings"] = await context.SecurityMappings.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["working_leaves_observations"] = await context.WorkingLeavesObservations.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["model_runs"] = await context.ModelRuns.CountAsync(x => x.IngestionId == ingestionId, cancellationToken),
            ["target_weights"] = await context.TargetWeights.CountAsync(x => modelIds.Contains(x.ModelRunId), cancellationToken),
            ["target_position_stages"] = await context.TargetPositionStages.CountAsync(x => modelIds.Contains(x.ModelRunId), cancellationToken),
            ["target_positions"] = await context.TargetPositions.CountAsync(x => targetStageIds.Contains(x.StageId), cancellationToken),
            ["position_only_drift_stages"] = await context.PositionOnlyDriftStages.CountAsync(x => modelIds.Contains(x.ModelRunId), cancellationToken),
            ["position_only_drifts"] = await context.PositionOnlyDrifts.CountAsync(x => driftStageIds.Contains(x.StageId), cancellationToken),
            ["broker_adjusted_drift_stages"] = await context.BrokerAdjustedDriftStages.CountAsync(x => modelIds.Contains(x.ModelRunId), cancellationToken),
            ["cycle_results"] = await context.CycleResults.CountAsync(x => x.IngestionId == ingestionId, cancellationToken)
        };
    }
}
