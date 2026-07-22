using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowIntradayCadenceContract
{
    public const string Version = "pms_shadow_intraday_15m_cadence_v1";
    public const string Mode = "FRESH_DRIFT_EVERY_15_MINUTES_WITH_MODEL_SCHEDULE";
    public const string TimeZone = "UTC";
    public const string OperationalCalendar = "CONTINUOUS_WEEKDAYS_UTC_EXCLUDING_SATURDAY_SUNDAY";
    public const int SlotMinutes = 15;
    public const int MinimumRealConsecutiveQualificationSlots = 3;
    public const int MaximumStartDelayMinutes = 5;
    public const int MaximumFinalizationDelayMinutes = 14;
    public const int FreshnessMinutes = 20;
    public const int StaleMinutes = 30;
    public const int RetryCount = 0;
    public const string TargetClosePolicy = "SLOT_END_UTC_FOR_MARKET_DATA_TARGETS_AND_DRIFTS;MODEL_TARGET_CLOSE_FROM_TIER_1_DAILY_CONTRACT";
    public const string Treatments = "LMAX_CAPTURE,POLYGON_PROVEN_GAP_FILL,QUBES_INPUT,MODEL_SCHEDULE,TARGET_POSITIONS,POSITION_ONLY_DRIFTS,FINALIZED_HANDOFF,POSTGRESQL_INGESTION,READ_MODELS,ALERTS";
    public const string OverlapPolicy = "REJECT_SAME_SLOT_AND_FAIL_CLOSED_WHEN_PREVIOUS_SLOT_ACTIVE";
    public const string PreviousActivePolicy = "FAILED_CLOSED_WITH_RESTART_RECOVERY_REQUIRED";
    public const string LmaxIncompletePolicy = "FAILED_CLOSED_UNLESS_EACH_PROVEN_GAP_IS_FILLED_BY_POLYGON";
    public const string PolygonFailurePolicy = "FAILED_CLOSED_LMAX_GAP_UNFILLED";
    public const string EngineFailurePolicy = "FAILED_CLOSED_NO_ENGINE_RETRY";
    public const string WorkingLeavesStatus = PmsShadowStateContract.WorkingLeavesUnavailable;

    public static DateTimeOffset Floor(DateTimeOffset value)
    {
        RequireUtc(value);
        var ticks = TimeSpan.FromMinutes(SlotMinutes).Ticks;
        return new DateTimeOffset(value.Ticks - value.Ticks % ticks, TimeSpan.Zero);
    }

    public static DateTimeOffset Ceiling(DateTimeOffset value)
    {
        var floor = Floor(value);
        return value == floor ? floor : floor.AddMinutes(SlotMinutes);
    }

    public static PmsShadowIntradaySlotWindow ClosedSlotAt(DateTimeOffset nowUtc)
    {
        var end = Floor(nowUtc);
        return WindowEnding(end);
    }

    public static PmsShadowIntradaySlotWindow WindowEnding(DateTimeOffset slotEndUtc)
    {
        RequireUtc(slotEndUtc);
        if (slotEndUtc != Floor(slotEndUtc))
            throw new ArgumentException("SLOT_END_NOT_QUARTER_HOUR_UTC", nameof(slotEndUtc));
        var start = slotEndUtc.AddMinutes(-SlotMinutes);
        var operationalDate = DateOnly.FromDateTime(slotEndUtc.UtcDateTime);
        var slotId = $"pms-shadow-15m-{start:yyyyMMdd'T'HHmm'Z'}";
        return new(slotId, start, slotEndUtc, operationalDate);
    }

    public static bool IsOperational(PmsShadowIntradaySlotWindow slot) =>
        slot.OperationalDate.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    public static string CreateIdempotencyKey(string slotId, string handoffSha256)
    {
        RequireSha(handoffSha256, nameof(handoffSha256));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{Version}\n{slotId}\n{handoffSha256}")));
    }

    public static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("UTC_REQUIRED");
    }

    public static void RequireSha(string value, string name)
    {
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new ArgumentException("SHA256_LOWER_HEX_REQUIRED", name);
    }
}

public sealed record PmsShadowIntradayCadenceDecision(
    string ContractVersion,
    string Mode,
    string ContractualJustification,
    IReadOnlyList<string> Strategies,
    string StrategiesExecutedPerSlot,
    string WeightsFrequency,
    string TargetPositionsFrequency,
    string DriftsFrequency,
    string EstimatedGpuCost,
    string ExpectedLatency,
    string ExpectedLineage)
{
    public static PmsShadowIntradayCadenceDecision Authoritative { get; } = new(
        PmsShadowIntradayCadenceContract.Version,
        PmsShadowIntradayCadenceContract.Mode,
        "The qualified INFX7-INFX10 contracts are TIER_1_DAILY_TEST_ENV with distinct native target_close values; they do not authorize four fresh GPU runs every fifteen minutes.",
        ["INFX7", "INFX8", "INFX9", "INFX10"],
        "Only strategies whose explicit model target_close is due; otherwise reuse each strategy's latest finalized model run.",
        "At each strategy's explicit TIER_1_DAILY_TEST_ENV target_close.",
        "Every completed fifteen-minute slot using fresh LMAX-primary decision prices.",
        "Every completed fifteen-minute slot.",
        "Zero GPU invocations for non-model slots; up to four serialized invocations when the daily model schedule is due.",
        "Slot finalization no later than fourteen minutes after slot close.",
        "slot -> LMAX capture -> proven Polygon gap fills -> Qubes input -> produced or reused finalized ModelRun -> weights SHA -> targets -> position-only drifts -> handoff -> ingestion.");
}

public sealed record PmsShadowIntradaySlotWindow(string SlotId, DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc, DateOnly OperationalDate);

public enum PmsShadowIntradaySlotStatus
{
    Missed,
    Running,
    Completed,
    FailedClosed
}

public enum PmsShadowIntradayFreshness
{
    Fresh,
    Stale,
    Missing,
    Incomplete,
    FailedClosed
}

public sealed record PmsShadowIntradayNoOrderCounters(
    int TradeIntentCount,
    int OrderCount,
    int FillCount,
    int LedgerCount,
    int BrokerSendCount,
    int FixOrderEntryCount,
    int AccountApiCount,
    int RealAccountCount,
    int ProductionDatabaseConnectionCount,
    int DatabentoApiCallCount,
    int DatabentoDownloadCount,
    int DatabentoRequestCount)
{
    public bool IsValid => this == Zero;
    public static PmsShadowIntradayNoOrderCounters Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record PmsShadowIntradaySlotManifest(
    string SlotId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    DateOnly OperationalDate,
    string LmaxCaptureId,
    string LmaxCaptureSha256,
    int LmaxGapCount,
    IReadOnlyList<string> LmaxGapIds,
    int PolygonCallCount,
    IReadOnlyList<string> PolygonFilledGapIds,
    bool PolygonReplacedValidLmaxObservation,
    IReadOnlyList<Guid> QubesInputSnapshotIds,
    IReadOnlyList<Guid> ProducedModelRunIds,
    IReadOnlyList<Guid> ReusedModelRunIds,
    IReadOnlyDictionary<string, string> WeightsSha256ByStrategy,
    int TargetPositionCount,
    int PositionOnlyDriftCount,
    string BrokerAdjustedDriftBlocker,
    string HandoffSha256,
    string SourceSessionId,
    Guid? IngestionId,
    string IngestionStatus,
    IReadOnlyDictionary<string, int> PostgreSqlRowCounts,
    PmsShadowIntradayFreshness Freshness,
    PmsShadowIntradayNoOrderCounters NoOrderCounters,
    bool Finalized,
    DateTimeOffset FinalizedAtUtc);

public sealed record PmsShadowIntradayManifestValidation(bool IsValid, IReadOnlyList<string> Issues)
{
    public static PmsShadowIntradayManifestValidation Validate(PmsShadowIntradaySlotManifest value)
    {
        var issues = new List<string>();
        var expected = PmsShadowIntradayCadenceContract.WindowEnding(value.SlotEndUtc);
        Require(value.SlotId == expected.SlotId && value.SlotStartUtc == expected.SlotStartUtc &&
            value.OperationalDate == expected.OperationalDate, "SLOT_WINDOW_IDENTITY_MISMATCH", issues);
        Require(value.Finalized, "SLOT_HANDOFF_NOT_FINALIZED", issues);
        Require(value.FinalizedAtUtc.Offset == TimeSpan.Zero && value.FinalizedAtUtc >= value.SlotEndUtc &&
            value.FinalizedAtUtc <= value.SlotEndUtc.AddMinutes(PmsShadowIntradayCadenceContract.MaximumFinalizationDelayMinutes),
            "SLOT_FINALIZATION_DEADLINE_EXCEEDED", issues);
        Require(IsSha(value.LmaxCaptureSha256), "LMAX_CAPTURE_SHA_INVALID", issues);
        Require(!string.IsNullOrWhiteSpace(value.LmaxCaptureId), "LMAX_CAPTURE_ID_MISSING", issues);
        Require(value.LmaxGapCount == value.LmaxGapIds.Distinct(StringComparer.Ordinal).Count(), "LMAX_GAP_LEDGER_MISMATCH", issues);
        Require(value.PolygonCallCount == value.PolygonFilledGapIds.Count, "POLYGON_CALL_LEDGER_MISMATCH", issues);
        Require(value.PolygonFilledGapIds.All(gap => value.LmaxGapIds.Contains(gap, StringComparer.Ordinal)),
            "POLYGON_SOURCE_CONFLICT", issues);
        Require(!value.PolygonReplacedValidLmaxObservation, "POLYGON_SOURCE_CONFLICT", issues);
        Require(value.LmaxGapIds.All(gap => value.PolygonFilledGapIds.Contains(gap, StringComparer.Ordinal)),
            "LMAX_GAP_UNFILLED", issues);
        Require(value.QubesInputSnapshotIds.Count == 4 && value.QubesInputSnapshotIds.Distinct().Count() == 4,
            "QUBES_INPUT_INCOMPLETE", issues);
        Require(value.ProducedModelRunIds.Concat(value.ReusedModelRunIds).Distinct().Count() == 4,
            "MODEL_SCHEDULE_INCOMPLETE", issues);
        Require(value.WeightsSha256ByStrategy.Count == 4 && value.WeightsSha256ByStrategy.Values.All(IsSha),
            "WEIGHTS_LINEAGE_INCOMPLETE", issues);
        Require(value.TargetPositionCount == 288, "TARGET_POSITION_COUNT_MISMATCH", issues);
        Require(value.PositionOnlyDriftCount == 288, "POSITION_ONLY_DRIFT_COUNT_MISMATCH", issues);
        Require(value.BrokerAdjustedDriftBlocker == PmsShadowStateContract.BrokerAdjustedBlocker,
            "WORKING_LEAVES_CONTRACT_MISMATCH", issues);
        Require(IsSha(value.HandoffSha256), "HANDOFF_SHA_INVALID", issues);
        Require(value.IngestionId is not null && value.IngestionStatus is "COMPLETED" or "ALREADY_APPLIED_IDENTICAL",
            "INGESTION_FAILED", issues);
        Require(value.PostgreSqlRowCounts.Count > 0 && value.PostgreSqlRowCounts.Values.All(count => count >= 0),
            "POSTGRESQL_ROW_COUNTS_MISSING", issues);
        Require(value.NoOrderCounters.IsValid, "NO_ORDER_INVARIANT_VIOLATION", issues);
        Require(value.Freshness == PmsShadowIntradayFreshness.Fresh, "INTRADAY_SLOT_STALE", issues);
        return new(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsSha(string? value) => value is not null && value.Length == 64 &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static void Require(bool condition, string issue, ICollection<string> issues)
    {
        if (!condition) issues.Add(issue);
    }
}

public sealed record PmsShadowIntradaySlotRow(
    string SlotId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    DateOnly OperationalDate,
    string Status,
    string ContractVersion,
    string CadenceMode,
    string CoordinatorId,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? ManifestJson,
    string? ManifestSha256,
    Guid? IngestionId,
    string? SourceSessionId,
    string? FailureCode,
    bool NoOrder);

public enum PmsShadowIntradayClaimResult
{
    Claimed,
    AlreadyCompleted,
    OverlapRejected,
    RestartRecoveryRequired,
    FailedClosed
}

public sealed record PmsShadowIntradayClaim(PmsShadowIntradayClaimResult Result,
    PmsShadowIntradaySlotRow Slot, IReadOnlyList<PmsShadowOperationalAlert> Alerts);

public interface IPmsShadowIntradaySlotStore
{
    Task<PmsShadowIntradayClaim> ClaimAsync(PmsShadowIntradaySlotWindow slot, string coordinatorId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    Task<PmsShadowIntradaySlotRow> CompleteAsync(string slotId, string coordinatorId,
        PmsShadowIntradaySlotManifest manifest, CancellationToken cancellationToken = default);

    Task<PmsShadowIntradaySlotRow> FailClosedAsync(string slotId, string coordinatorId,
        string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default);

    Task<PmsShadowIntradaySlotRow> RecordMissedAsync(PmsShadowIntradaySlotWindow slot,
        string reason, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PmsShadowIntradaySlotRow>> ReadAllAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryPmsShadowIntradaySlotStore : IPmsShadowIntradaySlotStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, PmsShadowIntradaySlotRow> slots = new(StringComparer.Ordinal);

    public Task<PmsShadowIntradayClaim> ClaimAsync(PmsShadowIntradaySlotWindow slot, string coordinatorId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PmsShadowIntradayCadenceContract.RequireUtc(nowUtc);
        if (slot.SlotEndUtc > nowUtc) throw new InvalidOperationException("FUTURE_SLOT_REJECTED");
        lock (sync)
        {
            if (slots.TryGetValue(slot.SlotId, out var existing))
            {
                if (existing.Status == PmsShadowIntradaySlotStatus.Completed.ToString().ToUpperInvariant())
                    return Task.FromResult(new PmsShadowIntradayClaim(PmsShadowIntradayClaimResult.AlreadyCompleted, existing, []));
                if (existing.Status == PmsShadowIntradaySlotStatus.FailedClosed.ToString().ToUpperInvariant())
                    return Task.FromResult(new PmsShadowIntradayClaim(PmsShadowIntradayClaimResult.FailedClosed, existing, []));
                var age = nowUtc - existing.ClaimedAtUtc;
                var result = age > TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes)
                    ? PmsShadowIntradayClaimResult.RestartRecoveryRequired
                    : PmsShadowIntradayClaimResult.OverlapRejected;
                var code = result == PmsShadowIntradayClaimResult.RestartRecoveryRequired
                    ? "RESTART_RECOVERY_REQUIRED" : "SLOT_OVERLAP_REJECTED";
                if (result == PmsShadowIntradayClaimResult.RestartRecoveryRequired)
                {
                    existing = existing with { CoordinatorId = coordinatorId, ClaimedAtUtc = nowUtc };
                    slots[slot.SlotId] = existing;
                }
                return Task.FromResult(new PmsShadowIntradayClaim(result, existing,
                    [Alert(code, existing, nowUtc)]));
            }

            var row = new PmsShadowIntradaySlotRow(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
                slot.OperationalDate, "RUNNING", PmsShadowIntradayCadenceContract.Version,
                PmsShadowIntradayCadenceContract.Mode, coordinatorId, nowUtc, null, null, null,
                null, null, null, true);
            slots.Add(row.SlotId, row);
            return Task.FromResult(new PmsShadowIntradayClaim(PmsShadowIntradayClaimResult.Claimed, row, []));
        }
    }

    public Task<PmsShadowIntradaySlotRow> CompleteAsync(string slotId, string coordinatorId,
        PmsShadowIntradaySlotManifest manifest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = PmsShadowIntradayManifestValidation.Validate(manifest);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(';', validation.Issues));
        lock (sync)
        {
            var existing = RequiredRunning(slotId, coordinatorId);
            var json = JsonSerializer.Serialize(manifest);
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var completed = existing with { Status = "COMPLETED", CompletedAtUtc = manifest.FinalizedAtUtc,
                ManifestJson = json, ManifestSha256 = sha, IngestionId = manifest.IngestionId,
                SourceSessionId = manifest.SourceSessionId };
            slots[slotId] = completed;
            return Task.FromResult(completed);
        }
    }

    public Task<PmsShadowIntradaySlotRow> FailClosedAsync(string slotId, string coordinatorId,
        string failureCode, DateTimeOffset failedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            var existing = RequiredRunning(slotId, coordinatorId);
            var failed = existing with { Status = "FAILED_CLOSED", CompletedAtUtc = failedAtUtc,
                FailureCode = failureCode };
            slots[slotId] = failed;
            return Task.FromResult(failed);
        }
    }

    public Task<PmsShadowIntradaySlotRow> RecordMissedAsync(PmsShadowIntradaySlotWindow slot,
        string reason, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (slots.TryGetValue(slot.SlotId, out var existing)) return Task.FromResult(existing);
            var row = new PmsShadowIntradaySlotRow(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
                slot.OperationalDate, "MISSED", PmsShadowIntradayCadenceContract.Version,
                PmsShadowIntradayCadenceContract.Mode, "scheduler", observedAtUtc, observedAtUtc,
                null, null, null, null, reason, true);
            slots.Add(row.SlotId, row);
            return Task.FromResult(row);
        }
    }

    public Task<IReadOnlyList<PmsShadowIntradaySlotRow>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync) return Task.FromResult<IReadOnlyList<PmsShadowIntradaySlotRow>>(
            slots.Values.OrderBy(value => value.SlotStartUtc).ToArray());
    }

    private PmsShadowIntradaySlotRow RequiredRunning(string slotId, string coordinatorId)
    {
        if (!slots.TryGetValue(slotId, out var row)) throw new InvalidOperationException("SLOT_NOT_CLAIMED");
        if (row.Status != "RUNNING") throw new InvalidOperationException("SLOT_NOT_RUNNING");
        if (row.CoordinatorId != coordinatorId) throw new InvalidOperationException("SLOT_COORDINATOR_MISMATCH");
        return row;
    }

    private static PmsShadowOperationalAlert Alert(string code, PmsShadowIntradaySlotRow row,
        DateTimeOffset nowUtc) => new(code, code == "SLOT_OVERLAP_REJECTED" ? "ERROR" : "CRITICAL",
            row.SourceSessionId ?? row.SlotId, row.OperationalDate, nowUtc,
            row.ManifestSha256 ?? new string('0', 64), code);
}

public sealed record LatestIntradayShadowSlot(PmsShadowIntradaySlotRow? Slot);
public sealed record IntradayShadowSlotHistory(IReadOnlyList<PmsShadowIntradaySlotRow> Slots);
public sealed record LatestTargetPositionBySlot(string? SlotId, int Count,
    IReadOnlyList<LatestTargetPositionReadModel>? Positions = null);
public sealed record LatestPositionOnlyDriftBySlot(string? SlotId, int Count,
    IReadOnlyList<LatestPositionOnlyDriftReadModel>? Drifts = null);
public sealed record SlotFreshnessAndCompleteness(string? SlotId, PmsShadowIntradayFreshness Freshness,
    bool Complete, IReadOnlyList<string> Blockers);
public sealed record MissingSlotSummary(IReadOnlyList<string> SlotIds);
public sealed record FailedClosedSlotSummary(IReadOnlyList<string> SlotIds);
public sealed record SlotLineageSummary(string? SlotId, string? LmaxCaptureSha256,
    IReadOnlyList<Guid> QubesInputSnapshotIds, IReadOnlyList<Guid> ProducedModelRunIds,
    IReadOnlyList<Guid> ReusedModelRunIds, string? HandoffSha256, Guid? IngestionId);
public sealed record PmsShadowIntradayReadModels(LatestIntradayShadowSlot LatestIntradayShadowSlot,
    IntradayShadowSlotHistory IntradayShadowSlotHistory,
    LatestTargetPositionBySlot LatestTargetPositionBySlot,
    LatestPositionOnlyDriftBySlot LatestPositionOnlyDriftBySlot,
    SlotFreshnessAndCompleteness SlotFreshnessAndCompleteness,
    MissingSlotSummary MissingSlotSummary,
    FailedClosedSlotSummary FailedClosedSlotSummary,
    SlotLineageSummary SlotLineageSummary,
    IReadOnlyList<PmsShadowOperationalAlert> Alerts);

public static class PmsShadowIntradayProjection
{
    public static PmsShadowIntradayReadModels Build(IEnumerable<PmsShadowIntradaySlotRow> rows,
        DateTimeOffset nowUtc)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(nowUtc);
        var history = rows.OrderBy(value => value.SlotStartUtc).ThenBy(value => value.SlotId, StringComparer.Ordinal).ToArray();
        var completed = history.Where(value => value.Status == "COMPLETED" && value.ManifestJson is not null)
            .Select(value => (Row: value, Manifest: JsonSerializer.Deserialize<PmsShadowIntradaySlotManifest>(value.ManifestJson!)!))
            .Where(value => PmsShadowIntradayManifestValidation.Validate(value.Manifest).IsValid)
            .OrderByDescending(value => value.Row.SlotEndUtc).ThenByDescending(value => value.Row.SlotId, StringComparer.Ordinal)
            .ToArray();
        var latest = completed.FirstOrDefault();
        var freshness = latest == default ? PmsShadowIntradayFreshness.Missing :
            nowUtc - latest.Row.SlotEndUtc > TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes)
                ? PmsShadowIntradayFreshness.Stale : PmsShadowIntradayFreshness.Fresh;
        var expected = ExpectedSlots(history, nowUtc);
        var actual = history.Select(value => value.SlotId).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(value => !actual.Contains(value)).ToArray();
        var failed = history.Where(value => value.Status == "FAILED_CLOSED").Select(value => value.SlotId).ToArray();
        var blockers = new List<string>();
        if (latest == default) blockers.Add("INTRADAY_SLOT_MISSING");
        if (freshness == PmsShadowIntradayFreshness.Stale) blockers.Add("INTRADAY_SLOT_STALE");
        var alerts = BuildAlerts(history, missing, freshness, nowUtc);
        return new(new(latest.Row), new(history),
            new(latest.Row?.SlotId, latest.Manifest?.TargetPositionCount ?? 0),
            new(latest.Row?.SlotId, latest.Manifest?.PositionOnlyDriftCount ?? 0),
            new(latest.Row?.SlotId, freshness, latest != default, blockers), new(missing), new(failed),
            latest == default ? new(null, null, [], [], [], null, null) :
                new(latest.Row!.SlotId, latest.Manifest!.LmaxCaptureSha256, latest.Manifest.QubesInputSnapshotIds,
                    latest.Manifest.ProducedModelRunIds, latest.Manifest.ReusedModelRunIds,
                    latest.Manifest.HandoffSha256, latest.Manifest.IngestionId), alerts);
    }

    private static IReadOnlyList<string> ExpectedSlots(IReadOnlyList<PmsShadowIntradaySlotRow> rows,
        DateTimeOffset nowUtc)
    {
        if (rows.Count == 0) return [];
        var ids = new List<string>();
        for (var end = rows.Min(value => value.SlotEndUtc); end <= PmsShadowIntradayCadenceContract.Floor(nowUtc);
             end = end.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes))
        {
            var slot = PmsShadowIntradayCadenceContract.WindowEnding(end);
            if (PmsShadowIntradayCadenceContract.IsOperational(slot)) ids.Add(slot.SlotId);
        }
        return ids;
    }

    private static IReadOnlyList<PmsShadowOperationalAlert> BuildAlerts(
        IReadOnlyList<PmsShadowIntradaySlotRow> history, IReadOnlyList<string> missing,
        PmsShadowIntradayFreshness freshness, DateTimeOffset nowUtc)
    {
        var alerts = new List<PmsShadowOperationalAlert>();
        foreach (var slotId in missing) alerts.Add(Alert("INTRADAY_SLOT_MISSING", slotId, nowUtc));
        if (freshness == PmsShadowIntradayFreshness.Stale) alerts.Add(Alert("INTRADAY_SLOT_STALE", "latest", nowUtc));
        foreach (var row in history.Where(value => value.Status == "FAILED_CLOSED"))
            alerts.Add(Alert("INTRADAY_SLOT_FAILED_CLOSED", row.SlotId, nowUtc));
        foreach (var row in history.Where(value => value.Status == "RUNNING" &&
                     nowUtc - value.ClaimedAtUtc > TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes)))
            alerts.Add(Alert("RESTART_RECOVERY_REQUIRED", row.SlotId, nowUtc));
        return alerts.OrderBy(value => value.Code, StringComparer.Ordinal)
            .ThenBy(value => value.SourceSessionId, StringComparer.Ordinal).ToArray();
    }

    private static PmsShadowOperationalAlert Alert(string code, string slotId, DateTimeOffset nowUtc) =>
        new(code, "ERROR", slotId, DateOnly.FromDateTime(nowUtc.UtcDateTime), nowUtc,
            new string('0', 64), code);
}
