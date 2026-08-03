using System.Globalization;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bSloDefinition(
    string SloId,
    string Stage,
    decimal Threshold,
    string Unit,
    string Comparator,
    string StartEvent,
    string EndEvent,
    string ClockDomain,
    string SourceRepository,
    string SourceCommit,
    string SourceFile,
    string SourceSymbol,
    string SourceFileSha256,
    string BlockerCode,
    string Metric);

public sealed class Arch7bGlobalSloRegistry
{
    public const int GlobalMinimumPreparationMarginSeconds = 600;
    public const int GlobalPreparationSafetyReserveSeconds = 60;
    public const int GlobalTerminalCleanupDeadlineSeconds = 60;
    public const int GlobalSchedulerMaximumWakeLatenessMilliseconds = 1000;

    private readonly IReadOnlyList<Arch7bSloDefinition> entries;

    public Arch7bGlobalSloRegistry(IEnumerable<Arch7bSloDefinition> entries)
    {
        this.entries = entries.OrderBy(value => value.SloId, StringComparer.Ordinal).ToArray();
        Validate(this.entries);
        var canonical = string.Join('\n', this.entries.Select(value => string.Create(CultureInfo.InvariantCulture,
            $"{value.SloId}|{value.Stage}|{value.Threshold}|{value.Unit}|{value.Comparator}|" +
            $"{value.StartEvent}|{value.EndEvent}|{value.ClockDomain}|{value.SourceRepository}|" +
            $"{value.SourceCommit}|{value.SourceFile}|{value.SourceSymbol}|{value.SourceFileSha256}|" +
            $"{value.BlockerCode}|{value.Metric}")));
        EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical);
    }

    public string EvidenceSha256 { get; }

    public IReadOnlyList<Arch7bSloDefinition> Entries => entries;

    public Arch7bSloDefinition Required(string sloId) => entries.SingleOrDefault(value => value.SloId == sloId)
        ?? throw new Arch7bQualificationException(Arch7bBlockers.CriticalPathSloMissing, sloId);

    public static Arch7bGlobalSloRegistry CreateDefault(IReadOnlyDictionary<string, string>? sourceHashes = null,
        string? supervisorSourceCommit = null)
    {
        sourceHashes ??= new Dictionary<string, string>(StringComparer.Ordinal);
        string Hash(string path) => sourceHashes.TryGetValue(path, out var hash) ? hash : new string('0', 64);
        const string intraday = Arch7bOneShotContracts.IntradayRepository;
        const string core = Arch7bOneShotContracts.CoreRepository;
        const string iCommit = Arch7bOneShotContracts.IntradayBaseCommit;
        const string cCommit = Arch7bOneShotContracts.CoreCommit;
        var supervisorCommit = supervisorSourceCommit ?? iCommit;
        const string clockFile = "src/QQ.Production.Intraday.Infrastructure.PostgreSql/PmsShadowCaptureClockAuthority.cs";
        const string handoffFile = "src/QQ.Production.Intraday.Infrastructure.PostgreSql/PmsShadowFreshSlotHandoff.cs";
        const string importFile = "src/QQ.Production.Intraday.Infrastructure.PostgreSql/Arch7bFreshPositionImportFastPath.cs";
        const string pinnedFile = "src/QQ.Production.Intraday.Infrastructure.PostgreSql/Arch7bPostgreSqlPinnedSession.cs";
        const string cadenceFile = "src/QQ.Production.Intraday.Infrastructure.PostgreSql/PmsShadowIntradayOperations.cs";
        const string supervisorFile = "tools/QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor/Arch7bSchedulingAndSlo.cs";
        const string coreRoot = "tools/lmax_portal_reports_downloader/src/";

        var values = new List<Arch7bSloDefinition>
        {
            Entry("CLOCK_MAXIMUM_ABSOLUTE_OFFSET_MILLISECONDS", "CLOCK_PREFLIGHT", PmsShadowCaptureClockAuthorityContract.MaximumAbsoluteOffsetMilliseconds, "milliseconds", clockFile, nameof(PmsShadowCaptureClockAuthorityContract.MaximumAbsoluteOffsetMilliseconds), Hash(clockFile), "ARCH7B_CLOCK_OFFSET_EXCEEDED", "absolute_offset", intraday, iCommit),
            Entry("CLOCK_MAXIMUM_UNCERTAINTY_MILLISECONDS", "CLOCK_PREFLIGHT", PmsShadowCaptureClockAuthorityContract.MaximumUncertaintyMilliseconds, "milliseconds", clockFile, nameof(PmsShadowCaptureClockAuthorityContract.MaximumUncertaintyMilliseconds), Hash(clockFile), "ARCH7B_CLOCK_UNCERTAINTY_EXCEEDED", "uncertainty", intraday, iCommit),
            Entry("CLOCK_MAXIMUM_SNAPSHOT_AGE_SECONDS", "CLOCK_PREFLIGHT", PmsShadowCaptureClockAuthorityContract.MaximumSnapshotAgeSeconds, "seconds", clockFile, nameof(PmsShadowCaptureClockAuthorityContract.MaximumSnapshotAgeSeconds), Hash(clockFile), "ARCH7B_CLOCK_SNAPSHOT_STALE", "snapshot_age", intraday, iCommit),
            Entry("HANDOFF_READY_MARKER_SECONDS", "POSITION_READY", PmsShadowFreshSlotHandoffContract.ReadyMarkerSloSeconds, "seconds", handoffFile, nameof(PmsShadowFreshSlotHandoffContract.ReadyMarkerSloSeconds), Hash(handoffFile), "ARCH7B_POSITION_READY_DEADLINE_EXCEEDED", "handoff_ready_marker", intraday, iCommit),
            Entry("HANDOFF_MARKER_DETECTION_SECONDS", "POSITION_READY", PmsShadowFreshSlotHandoffContract.MarkerDetectionSloSeconds, "seconds", handoffFile, nameof(PmsShadowFreshSlotHandoffContract.MarkerDetectionSloSeconds), Hash(handoffFile), "ARCH7B_POSITION_MARKER_DETECTION_DEADLINE_EXCEEDED", "marker_detection", intraday, iCommit),
            Entry("POSTGRESQL_CONNECTION_SECONDS", "RDS_READ_1", PmsShadowFreshSlotHandoffContract.PostgreSqlConnectionSloSeconds, "seconds", handoffFile, nameof(PmsShadowFreshSlotHandoffContract.PostgreSqlConnectionSloSeconds), Hash(handoffFile), "ARCH7B_POSTGRESQL_OPEN_DEADLINE_EXCEEDED", "postgresql_connection", intraday, iCommit),
            Entry("INDISPENSABLE_HASHING_SECONDS", "POSITION_PACKAGE", PmsShadowFreshSlotHandoffContract.IndispensableHashingSloSeconds, "seconds", handoffFile, nameof(PmsShadowFreshSlotHandoffContract.IndispensableHashingSloSeconds), Hash(handoffFile), "ARCH7B_HASHING_DEADLINE_EXCEEDED", "hashing", intraday, iCommit),
            Entry("POSITION_PACKAGE_READY_SECONDS", "POSITION_PACKAGE", Arch7bFreshPositionImportFastPathContract.PackageReadySloSeconds, "seconds", importFile, nameof(Arch7bFreshPositionImportFastPathContract.PackageReadySloSeconds), Hash(importFile), "ARCH7B_POSITION_PACKAGE_DEADLINE_EXCEEDED", "package_ready", intraday, iCommit),
            Entry("POSITION_READY_SECONDS", "POSITION_READY", Arch7bFreshPositionImportFastPathContract.ReadySloSeconds, "seconds", importFile, nameof(Arch7bFreshPositionImportFastPathContract.ReadySloSeconds), Hash(importFile), "ARCH7B_POSITION_READY_DEADLINE_EXCEEDED", "position_ready", intraday, iCommit),
            Entry("POSITION_PLAN_SECONDS", "POSITION_PLAN", Arch7bFreshPositionImportFastPathContract.PlanSloSeconds, "seconds", importFile, nameof(Arch7bFreshPositionImportFastPathContract.PlanSloSeconds), Hash(importFile), "ARCH7B_POSITION_PLAN_DEADLINE_EXCEEDED", "position_plan", intraday, iCommit),
            Entry("POSITION_APPLY_START_SECONDS", "POSITION_APPLY", Arch7bFreshPositionImportFastPathContract.ApplyStartSloSeconds, "seconds", importFile, nameof(Arch7bFreshPositionImportFastPathContract.ApplyStartSloSeconds), Hash(importFile), "ARCH7B_POSITION_APPLY_DEADLINE_EXCEEDED", "position_apply", intraday, iCommit),
            Entry("PINNED_POSTGRESQL_COLD_OPEN_SECONDS", "RDS_READ_1", Arch7bPostgreSqlPinnedTransportProfile.ColdConnectionTimeoutSeconds, "seconds", pinnedFile, nameof(Arch7bPostgreSqlPinnedTransportProfile.ColdConnectionTimeoutSeconds), Hash(pinnedFile), "ARCH7B_POSTGRESQL_OPEN_DEADLINE_EXCEEDED", "pinned_open", intraday, iCommit),
            Entry("MARKET_SLOT_MAXIMUM_START_DELAY_SECONDS", "MARKET_PREARM", PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes * 60, "seconds", cadenceFile, nameof(PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes), Hash(cadenceFile), "ARCH7B_MARKET_PREARM_DEADLINE_EXCEEDED", "start_delay", intraday, iCommit),
            Entry("CORE_PREQUALIFICATION_MAXIMUM_AGE_SECONDS", "CORE_PREQUALIFICATION", 1800, "seconds", coreRoot + "core-runtime-prequalification.mjs", "PREQUALIFICATION_MAX_AGE_SECONDS", Hash(coreRoot + "core-runtime-prequalification.mjs"), "ARCH7B_CORE_PREQUALIFICATION_STALE", "freshness", core, cCommit),
            Entry("RDS_SECRET_CLIENT_DEADLINE_SECONDS", "RDS_READ_1", 20, "seconds", coreRoot + "rds-secret-client.mjs", "RDS_SECRET_DEADLINE_MS", Hash(coreRoot + "rds-secret-client.mjs"), "ARCH7B_RDS_SECRET_DEADLINE_EXCEEDED", "secret_fetch", core, cCommit),
            Entry("RDS_SECRET_LEASE_MAXIMUM_AGE_SECONDS", "PRELOADED_LEASE_READY", 600, "seconds", coreRoot + "rds-secret-client.mjs", "RDS_SECRET_LEASE_MAX_AGE_MS", Hash(coreRoot + "rds-secret-client.mjs"), "ARCH7B_RDS_SECRET_LEASE_STALE", "freshness", core, cCommit),
            Entry("BRACKET_MAXIMUM_SPAN_SECONDS", "BRACKET_T2", 30, "seconds", coreRoot + "bracketed-snapshot.mjs", "BRACKET_MAXIMUM_BROKER_SPAN_SECONDS", Hash(coreRoot + "bracketed-snapshot.mjs"), "ARCH7B_BRACKET_SPAN_EXCEEDED", "broker_span", core, cCommit),
            Entry("FAST_SEAL_BRACKET_CONTRACT_SECONDS", "CORE_FAST_SEAL", 2, "seconds", coreRoot + "bracket-fast-seal.mjs", "bracket_contract_seconds", Hash(coreRoot + "bracket-fast-seal.mjs"), "ARCH7B_FAST_SEAL_DEADLINE_EXCEEDED", "bracket_contract", core, cCommit),
            Entry("FAST_SEAL_ACQUISITION_MANIFEST_SECONDS", "CORE_FAST_SEAL", 3, "seconds", coreRoot + "bracket-fast-seal.mjs", "acquisition_manifest_seconds", Hash(coreRoot + "bracket-fast-seal.mjs"), "ARCH7B_FAST_SEAL_DEADLINE_EXCEEDED", "acquisition_manifest", core, cCommit),
            Entry("FAST_SEAL_QUALIFICATION_SUMMARY_SECONDS", "CORE_FAST_SEAL", 5, "seconds", coreRoot + "bracket-fast-seal.mjs", "qualification_summary_seconds", Hash(coreRoot + "bracket-fast-seal.mjs"), "ARCH7B_FAST_SEAL_DEADLINE_EXCEEDED", "qualification_summary", core, cCommit),
            Entry("FAST_SEAL_FINAL_EVIDENCE_INDEX_SECONDS", "CORE_FAST_SEAL", 8, "seconds", coreRoot + "bracket-fast-seal.mjs", "final_evidence_index_seconds", Hash(coreRoot + "bracket-fast-seal.mjs"), "ARCH7B_FAST_SEAL_DEADLINE_EXCEEDED", "evidence_index", core, cCommit),
            Entry("GLOBAL_MINIMUM_PREPARATION_MARGIN_SECONDS", "SLOT_SELECTED", GlobalMinimumPreparationMarginSeconds, "seconds", supervisorFile, nameof(GlobalMinimumPreparationMarginSeconds), Hash(supervisorFile), Arch7bBlockers.PreparationMarginInsufficient, "preparation_margin", intraday, supervisorCommit),
            Entry("GLOBAL_PREPARATION_SAFETY_RESERVE_SECONDS", "SLOT_SELECTED", GlobalPreparationSafetyReserveSeconds, "seconds", supervisorFile, nameof(GlobalPreparationSafetyReserveSeconds), Hash(supervisorFile), Arch7bBlockers.PreparationMarginInsufficient, "preparation_reserve", intraday, supervisorCommit),
            Entry("GLOBAL_TERMINAL_CLEANUP_DEADLINE_SECONDS", "TERMINAL_CLEANUP", GlobalTerminalCleanupDeadlineSeconds, "seconds", supervisorFile, nameof(GlobalTerminalCleanupDeadlineSeconds), Hash(supervisorFile), Arch7bBlockers.CleanupDeadlineExceeded, "cleanup_duration", intraday, supervisorCommit),
            Entry("GLOBAL_SCHEDULER_MAXIMUM_WAKE_LATENESS_MILLISECONDS", "SLOT_LOCKED", GlobalSchedulerMaximumWakeLatenessMilliseconds, "milliseconds", supervisorFile, nameof(GlobalSchedulerMaximumWakeLatenessMilliseconds), Hash(supervisorFile), Arch7bBlockers.SchedulerWakeLatenessExceeded, "wake_lateness", intraday, supervisorCommit)
        };
        return new(values);
    }

    public static void Validate(IEnumerable<Arch7bSloDefinition> definitions)
    {
        var values = definitions.ToArray();
        foreach (var group in values.GroupBy(value => value.SloId, StringComparer.Ordinal))
        {
            if (group.Select(value => (value.Threshold, value.Unit, value.Comparator)).Distinct().Count() != 1)
                throw new Arch7bQualificationException(Arch7bBlockers.SloContradiction, group.Key);
        }
        foreach (var group in values.GroupBy(value => (value.Stage, value.Metric)))
        {
            if (group.Select(value => (value.Threshold, value.Unit, value.Comparator)).Distinct().Count() > 1)
                throw new Arch7bQualificationException(Arch7bBlockers.SloContradiction, $"{group.Key.Stage}/{group.Key.Metric}");
        }
    }

    private static Arch7bSloDefinition Entry(string id, string stage, decimal threshold, string unit,
        string file, string symbol, string sha, string blocker, string metric, string repository, string commit) =>
        new(id, stage, threshold, unit, "<=", $"{stage}_STARTED", $"{stage}_COMPLETED", "MONOTONIC",
            repository, commit, file, symbol, sha, blocker, metric);
}

public sealed record Arch7bSlotLock(
    string ContractVersion,
    string CalendarContractVersion,
    string SlotId,
    DateTimeOffset ObservedUtc,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    int PreSlotCriticalPathSloSeconds,
    int RequiredPreparationMarginSeconds,
    string LockSha256);

public sealed class Arch7bOperationalSlotSelector
{
    private Arch7bSlotLock? published;

    public Arch7bSlotLock SelectAndLock(DateTimeOffset observedUtc, int preSlotCriticalPathSloSeconds,
        bool calendarAuthoritative = true, bool ambiguous = false)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(observedUtc);
        if (published is not null)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotLockAlreadyPublished);
        if (!calendarAuthoritative)
            throw new Arch7bQualificationException(Arch7bBlockers.CalendarNotAuthoritative);
        if (ambiguous)
            throw new Arch7bQualificationException(Arch7bBlockers.CalendarAmbiguous);

        var requiredMargin = Math.Max(Arch7bGlobalSloRegistry.GlobalMinimumPreparationMarginSeconds,
            checked(preSlotCriticalPathSloSeconds + Arch7bGlobalSloRegistry.GlobalPreparationSafetyReserveSeconds));
        var earliest = observedUtc.AddSeconds(requiredMargin);
        var start = PmsShadowIntradayCadenceContract.Ceiling(earliest);
        PmsShadowIntradaySlotWindow? slot = null;
        for (var index = 0; index < 7 * 24 * 4; index++, start = start.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes))
        {
            var candidate = PmsShadowIntradayCadenceContract.WindowEnding(start.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes));
            if (PmsShadowIntradayCadenceContract.IsOperational(candidate))
            {
                slot = candidate;
                break;
            }
        }

        if (slot is null)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotOutsideOperationalSession);
        if (slot.SlotStartUtc < earliest)
            throw new Arch7bQualificationException(Arch7bBlockers.PreparationMarginInsufficient);
        if (slot.SlotStartUtc <= observedUtc)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotAlreadyStarted);

        var canonical = string.Join('\n', Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
            PmsShadowIntradayCadenceContract.Version, slot.SlotId, observedUtc.ToString("O"),
            slot.SlotStartUtc.ToString("O"), slot.SlotEndUtc.ToString("O"), preSlotCriticalPathSloSeconds,
            requiredMargin);
        published = new(Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
            PmsShadowIntradayCadenceContract.Version, slot.SlotId, observedUtc, slot.SlotStartUtc,
            slot.SlotEndUtc, preSlotCriticalPathSloSeconds, requiredMargin, Arch7bOneShotContracts.Sha256(canonical));
        return published;
    }

    public static async Task WaitUntilAsync(DateTimeOffset targetUtc, TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        while (timeProvider.GetUtcNow() < targetUtc)
        {
            var remaining = targetUtc - timeProvider.GetUtcNow();
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1),
                timeProvider, cancellationToken).ConfigureAwait(false);
        }
        var lateness = timeProvider.GetUtcNow() - targetUtc;
        if (lateness > TimeSpan.FromMilliseconds(Arch7bGlobalSloRegistry.GlobalSchedulerMaximumWakeLatenessMilliseconds))
            throw new Arch7bQualificationException(Arch7bBlockers.SchedulerWakeLatenessExceeded);
    }
}
