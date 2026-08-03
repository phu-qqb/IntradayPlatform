using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed class EfPmsShadowIntradaySlotStore : IPmsShadowIntradaySlotStore
{
    private readonly IDbContextFactory<PmsShadowDbContext> contextFactory;
    private readonly IPmsShadowIntradayImportObserver observer;

    public EfPmsShadowIntradaySlotStore(IDbContextFactory<PmsShadowDbContext> contextFactory)
        : this(contextFactory, NullPmsShadowIntradayImportObserver.Instance) { }

    public EfPmsShadowIntradaySlotStore(IDbContextFactory<PmsShadowDbContext> contextFactory,
        IPmsShadowIntradayImportObserver observer)
    {
        this.contextFactory = contextFactory;
        this.observer = observer;
    }

    public async Task<PmsShadowIntradayClaim> ClaimAsync(PmsShadowIntradaySlotWindow slot,
        string coordinatorId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(nowUtc);
        if (slot.SlotEndUtc > nowUtc) throw new InvalidOperationException("FUTURE_SLOT_REJECTED");
        return await WithLockAsync<PmsShadowIntradayClaim>(slot.SlotId, async (connection, transaction) =>
        {
            var existing = await ReadAsync(connection, transaction, slot.SlotId, cancellationToken);
            if (existing is not null)
            {
                if (existing.Status == "COMPLETED")
                    return new(PmsShadowIntradayClaimResult.AlreadyCompleted, existing, []);
                if (existing.Status is "FAILED_CLOSED" or "MISSED")
                    return new(PmsShadowIntradayClaimResult.FailedClosed, existing, []);
                if (nowUtc - existing.ClaimedAtUtc <= TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes))
                    return new(PmsShadowIntradayClaimResult.OverlapRejected, existing,
                        [Alert("SLOT_OVERLAP_REJECTED", existing, nowUtc)]);

                await ExecuteAsync(connection, transaction,
                    "UPDATE pms_shadow.intraday_slots SET coordinator_id=@coordinator_id, claimed_at_utc=@claimed_at_utc " +
                    "WHERE slot_id=@slot_id AND status='RUNNING'", cancellationToken,
                    ("coordinator_id", coordinatorId), ("claimed_at_utc", nowUtc), ("slot_id", slot.SlotId));
                var recovered = existing with { CoordinatorId = coordinatorId, ClaimedAtUtc = nowUtc };
                return new(PmsShadowIntradayClaimResult.RestartRecoveryRequired, recovered,
                    [Alert("RESTART_RECOVERY_REQUIRED", recovered, nowUtc)]);
            }

            await ExecuteAsync(connection, transaction,
                "INSERT INTO pms_shadow.intraday_slots " +
                "(slot_id,slot_start_utc,slot_end_utc,operational_date,status,contract_version,cadence_mode," +
                "coordinator_id,claimed_at_utc,no_order) VALUES " +
                "(@slot_id,@slot_start_utc,@slot_end_utc,@operational_date,'RUNNING',@contract_version," +
                "@cadence_mode,@coordinator_id,@claimed_at_utc,TRUE)", cancellationToken,
                ("slot_id", slot.SlotId), ("slot_start_utc", slot.SlotStartUtc),
                ("slot_end_utc", slot.SlotEndUtc), ("operational_date", slot.OperationalDate),
                ("contract_version", PmsShadowIntradayCadenceContract.Version),
                ("cadence_mode", PmsShadowIntradayCadenceContract.Mode),
                ("coordinator_id", coordinatorId), ("claimed_at_utc", nowUtc));
            var row = new PmsShadowIntradaySlotRow(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
                slot.OperationalDate, "RUNNING", PmsShadowIntradayCadenceContract.Version,
                PmsShadowIntradayCadenceContract.Mode, coordinatorId, nowUtc, null, null, null,
                null, null, null, true);
            return new(PmsShadowIntradayClaimResult.Claimed, row, []);
        }, cancellationToken);
    }

    public async Task<PmsShadowIntradaySlotRow> CompleteAsync(string slotId, string coordinatorId,
        PmsShadowIntradaySlotManifest manifest, CancellationToken cancellationToken = default)
    {
        var validation = PmsShadowIntradayManifestValidation.Validate(manifest);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(';', validation.Issues));
        return await WithLockAsync(slotId, async (connection, transaction) =>
        {
            var row = await RequiredRunningAsync(connection, transaction, slotId, coordinatorId, cancellationToken);
            var json = JsonSerializer.Serialize(manifest);
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            await ExecuteAsync(connection, transaction,
                "UPDATE pms_shadow.intraday_slots SET status='COMPLETED',completed_at_utc=@completed_at_utc," +
                "manifest_json=CAST(@manifest_json AS jsonb),manifest_sha256=@manifest_sha256,ingestion_id=@ingestion_id," +
                "source_session_id=@source_session_id WHERE slot_id=@slot_id AND status='RUNNING' AND coordinator_id=@coordinator_id",
                cancellationToken, ("completed_at_utc", manifest.FinalizedAtUtc), ("manifest_json", json),
                ("manifest_sha256", sha), ("ingestion_id", manifest.IngestionId!.Value),
                ("source_session_id", manifest.SourceSessionId), ("slot_id", slotId),
                ("coordinator_id", coordinatorId));
            return row with { Status = "COMPLETED", CompletedAtUtc = manifest.FinalizedAtUtc,
                ManifestJson = json, ManifestSha256 = sha, IngestionId = manifest.IngestionId,
                SourceSessionId = manifest.SourceSessionId };
        }, cancellationToken);
    }

    public async Task<PmsShadowIntradaySlotRow> FailClosedAsync(string slotId, string coordinatorId,
        string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default) =>
        await WithLockAsync(slotId, async (connection, transaction) =>
        {
            var row = await RequiredRunningAsync(connection, transaction, slotId, coordinatorId, cancellationToken);
            await ExecuteAsync(connection, transaction,
                "UPDATE pms_shadow.intraday_slots SET status='FAILED_CLOSED',completed_at_utc=@completed_at_utc," +
                "failure_code=@failure_code WHERE slot_id=@slot_id AND status='RUNNING' AND coordinator_id=@coordinator_id",
                cancellationToken, ("completed_at_utc", failedAtUtc), ("failure_code", failureCode),
                ("slot_id", slotId), ("coordinator_id", coordinatorId));
            return row with { Status = "FAILED_CLOSED", CompletedAtUtc = failedAtUtc, FailureCode = failureCode };
        }, cancellationToken);

    public async Task<IReadOnlyList<PmsShadowIntradaySlotRow>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        var ownsConnectionLifecycle = connection.State != ConnectionState.Open;
        if (ownsConnectionLifecycle)
            await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = SelectSql + " ORDER BY slot_start_utc,slot_id";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<PmsShadowIntradaySlotRow>();
            while (await reader.ReadAsync(cancellationToken)) rows.Add(Map(reader));
            return rows;
        }
        finally
        {
            if (ownsConnectionLifecycle)
                await connection.CloseAsync();
        }
    }

    public async Task<PmsShadowIntradaySlotRow> RecordMissedAsync(PmsShadowIntradaySlotWindow slot,
        string reason, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default) =>
        await WithLockAsync(slot.SlotId, async (connection, transaction) =>
        {
            var existing = await ReadAsync(connection, transaction, slot.SlotId, cancellationToken);
            if (existing is not null) return existing;
            await ExecuteAsync(connection, transaction,
                "INSERT INTO pms_shadow.intraday_slots " +
                "(slot_id,slot_start_utc,slot_end_utc,operational_date,status,contract_version,cadence_mode," +
                "coordinator_id,claimed_at_utc,completed_at_utc,failure_code,no_order) VALUES " +
                "(@slot_id,@slot_start_utc,@slot_end_utc,@operational_date,'MISSED',@contract_version," +
                "@cadence_mode,'scheduler',@observed_at_utc,@observed_at_utc,@failure_code,TRUE)", cancellationToken,
                ("slot_id", slot.SlotId), ("slot_start_utc", slot.SlotStartUtc),
                ("slot_end_utc", slot.SlotEndUtc), ("operational_date", slot.OperationalDate),
                ("contract_version", PmsShadowIntradayCadenceContract.Version),
                ("cadence_mode", PmsShadowIntradayCadenceContract.Mode),
                ("observed_at_utc", observedAtUtc), ("failure_code", reason));
            return new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, slot.OperationalDate,
                "MISSED", PmsShadowIntradayCadenceContract.Version, PmsShadowIntradayCadenceContract.Mode,
                "scheduler", observedAtUtc, observedAtUtc, null, null, null, null, reason, true);
        }, cancellationToken);

    private async Task<T> WithLockAsync<T>(string slotId,
        Func<DbConnection, DbTransaction, Task<T>> action, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        observer.Record(PmsShadowFreshSlotHandoffEvents.PostgreSqlConnectionStarted, slotId);
        await connection.OpenAsync(cancellationToken);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(slotId)), 0);
        observer.Record(PmsShadowFreshSlotHandoffEvents.PostgreSqlTransactionStarted, slotId);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction, "SELECT pg_advisory_xact_lock(@lock_key)",
                cancellationToken, ("lock_key", lockKey));
            var result = await action(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<PmsShadowIntradaySlotRow> RequiredRunningAsync(DbConnection connection,
        DbTransaction transaction, string slotId, string coordinatorId, CancellationToken cancellationToken)
    {
        var row = await ReadAsync(connection, transaction, slotId, cancellationToken)
            ?? throw new InvalidOperationException("SLOT_NOT_CLAIMED");
        if (row.Status != "RUNNING") throw new InvalidOperationException("SLOT_NOT_RUNNING");
        if (row.CoordinatorId != coordinatorId) throw new InvalidOperationException("SLOT_COORDINATOR_MISMATCH");
        return row;
    }

    private static async Task<PmsShadowIntradaySlotRow?> ReadAsync(DbConnection connection,
        DbTransaction transaction, string slotId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SelectSql + " WHERE slot_id=@slot_id";
        Add(command, "slot_id", slotId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction,
        string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) Add(command, parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static PmsShadowIntradaySlotRow Map(DbDataReader reader) => new(
        reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetFieldValue<DateTimeOffset>(2),
        reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetGuid(12), reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14), reader.GetBoolean(15));

    private static PmsShadowOperationalAlert Alert(string code, PmsShadowIntradaySlotRow row,
        DateTimeOffset nowUtc) => new(code, "ERROR", row.SourceSessionId ?? row.SlotId,
            row.OperationalDate, nowUtc, row.ManifestSha256 ?? new string('0', 64), code);

    private const string SelectSql = "SELECT slot_id,slot_start_utc,slot_end_utc,operational_date,status," +
        "contract_version,cadence_mode,coordinator_id,claimed_at_utc,completed_at_utc," +
        "manifest_json::text,manifest_sha256,ingestion_id,source_session_id,failure_code,no_order " +
        "FROM pms_shadow.intraday_slots";
}

public interface IPmsShadowIntradaySlotPipeline
{
    Task<PmsShadowIntradaySlotManifest> ExecuteAsync(PmsShadowIntradaySlotWindow slot,
        CancellationToken cancellationToken = default);
}

public sealed record PmsShadowIntradaySchedulerTick(PmsShadowIntradaySlotWindow Slot,
    PmsShadowIntradayClaimResult ClaimResult, PmsShadowIntradaySlotStatus FinalStatus,
    IReadOnlyList<PmsShadowOperationalAlert> Alerts);

public sealed class PmsShadowIntradayScheduler(IPmsShadowIntradaySlotStore store,
    IPmsShadowIntradaySlotPipeline pipeline)
{
    public async Task<PmsShadowIntradaySchedulerTick> RunClosedSlotAsync(DateTimeOffset nowUtc,
        string coordinatorId, CancellationToken cancellationToken = default)
    {
        var slot = PmsShadowIntradayCadenceContract.ClosedSlotAt(nowUtc);
        if (!PmsShadowIntradayCadenceContract.IsOperational(slot) ||
            nowUtc - slot.SlotEndUtc > TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes))
        {
            var reason = PmsShadowIntradayCadenceContract.IsOperational(slot)
                ? "INTRADAY_SLOT_MISSING" : "OUTSIDE_OPERATIONAL_CALENDAR";
            await store.RecordMissedAsync(slot, reason, nowUtc, cancellationToken);
            var alerts = reason == "INTRADAY_SLOT_MISSING"
                ? PmsShadowIntradayAlertPolicy.ForIssues(slot.SlotId, slot.OperationalDate, nowUtc,
                    new string('0', 64), [reason]) : [];
            return new(slot, PmsShadowIntradayClaimResult.FailedClosed,
                PmsShadowIntradaySlotStatus.Missed, alerts);
        }

        var claim = await store.ClaimAsync(slot, coordinatorId, nowUtc, cancellationToken);
        if (claim.Result == PmsShadowIntradayClaimResult.AlreadyCompleted)
            return new(slot, claim.Result, PmsShadowIntradaySlotStatus.Completed, claim.Alerts);
        if (claim.Result is PmsShadowIntradayClaimResult.OverlapRejected or PmsShadowIntradayClaimResult.FailedClosed)
            return new(slot, claim.Result, claim.Slot.Status == "FAILED_CLOSED"
                ? PmsShadowIntradaySlotStatus.FailedClosed : PmsShadowIntradaySlotStatus.Running, claim.Alerts);

        try
        {
            var manifest = await pipeline.ExecuteAsync(slot, cancellationToken);
            await store.CompleteAsync(slot.SlotId, coordinatorId, manifest, cancellationToken);
            return new(slot, claim.Result, PmsShadowIntradaySlotStatus.Completed, claim.Alerts);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            var persisted = (await store.ReadAllAsync(cancellationToken))
                .SingleOrDefault(value => value.SlotId == slot.SlotId);
            if (persisted?.Status == "COMPLETED")
            {
                var recoveryAlert = PmsShadowIntradayAlertPolicy.ForIssues(slot.SlotId,
                    slot.OperationalDate, nowUtc, persisted.ManifestSha256 ?? new string('0', 64),
                    ["RESTART_RECOVERY_REQUIRED"]);
                return new(slot, claim.Result, PmsShadowIntradaySlotStatus.Completed,
                    [.. claim.Alerts, .. recoveryAlert]);
            }

            var issues = exception.Message.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var failureCode = issues.FirstOrDefault() ?? "INTRADAY_SLOT_FAILED_CLOSED";
            if (persisted?.Status == "RUNNING")
                await store.FailClosedAsync(slot.SlotId, coordinatorId, failureCode, nowUtc, cancellationToken);
            var issueAlerts = PmsShadowIntradayAlertPolicy.ForIssues(slot.SlotId, slot.OperationalDate,
                nowUtc, new string('0', 64), [.. issues, "INTRADAY_SLOT_FAILED_CLOSED"]);
            return new(slot, claim.Result, PmsShadowIntradaySlotStatus.FailedClosed,
                [.. claim.Alerts, .. issueAlerts]);
        }
    }
}
