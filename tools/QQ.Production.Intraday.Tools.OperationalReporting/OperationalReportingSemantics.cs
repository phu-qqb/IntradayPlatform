using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class ReportingSlotDueStatuses
{
    public const string Current = "CURRENT";
    public const string Due = "DUE";
    public const string NotDue = "NOT_DUE";
    public const string OutsideOperationalCalendar = "OUTSIDE_OPERATIONAL_CALENDAR";
    public const string Missed = "MISSED";
    public const string StaleAfterDueTime = "STALE_AFTER_DUE_TIME";
    public const string Unknown = "UNKNOWN";
}

public sealed record ReportingOperationalExpectation(
    DateTimeOffset AsOfUtc,
    bool IsOperationalNow,
    string? LatestExpectedClosedSlotId,
    DateTimeOffset? LatestExpectedClosedSlotEndUtc,
    string? NextExpectedSlotId,
    DateTimeOffset? NextExpectedSlotStartUtc,
    string MarketCalendarStatus,
    string SlotDueStatus,
    string Reason,
    string ContractVersion);

public static class ReportingOperationalCalendar
{
    public static ReportingOperationalExpectation Project(
        DateTimeOffset asOfUtc,
        IReadOnlyList<ReportingSlotFact> slots)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(asOfUtc);
        var candidate = PmsShadowIntradayCadenceContract.ClosedSlotAt(asOfUtc);
        var operational = PmsShadowIntradayCadenceContract.IsOperational(candidate);
        if (!operational)
            return new(
                asOfUtc,
                false,
                null,
                null,
                NextOperational(candidate.SlotEndUtc).SlotId,
                NextOperational(candidate.SlotEndUtc).SlotStartUtc,
                ReportingSlotDueStatuses.OutsideOperationalCalendar,
                ReportingSlotDueStatuses.OutsideOperationalCalendar,
                "The canonical UTC weekday calendar excludes Saturday and Sunday.",
                PmsShadowIntradayCadenceContract.Version);

        var latest = slots
            .Where(value => value.SlotEndUtc <= candidate.SlotEndUtc)
            .OrderByDescending(value => value.SlotEndUtc)
            .ThenByDescending(value => value.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();
        var next = PmsShadowIntradayCadenceContract.WindowEnding(
            candidate.SlotEndUtc.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes));
        var grace = TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes);
        var status = latest is null || latest.SlotEndUtc < candidate.SlotEndUtc
            ? asOfUtc <= candidate.SlotEndUtc.Add(grace)
                ? ReportingSlotDueStatuses.Due
                : ReportingSlotDueStatuses.Missed
            : latest.Status == "MISSED"
                ? ReportingSlotDueStatuses.Missed
                : latest.Status == "COMPLETED"
                    ? ReportingSlotDueStatuses.Current
                    : asOfUtc <= candidate.SlotEndUtc.Add(grace)
                        ? ReportingSlotDueStatuses.Due
                        : ReportingSlotDueStatuses.StaleAfterDueTime;
        return new(
            asOfUtc,
            true,
            candidate.SlotId,
            candidate.SlotEndUtc,
            next.SlotId,
            next.SlotStartUtc,
            "OPERATIONAL_WEEKDAY_UTC",
            status,
            status switch
            {
                ReportingSlotDueStatuses.Current => "The expected closed slot is completed.",
                ReportingSlotDueStatuses.Due => "The expected slot is inside its allowed start delay.",
                ReportingSlotDueStatuses.Missed => "The expected slot is absent or explicitly MISSED after its due time.",
                _ => "The expected slot has not reached a qualifying completed state after its due time."
            },
            PmsShadowIntradayCadenceContract.Version);
    }

    private static PmsShadowIntradaySlotWindow NextOperational(DateTimeOffset fromUtc)
    {
        var end = fromUtc.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes);
        for (var index = 0; index < 7 * 24 * 4; index++)
        {
            var slot = PmsShadowIntradayCadenceContract.WindowEnding(end);
            if (PmsShadowIntradayCadenceContract.IsOperational(slot)) return slot;
            end = end.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes);
        }
        throw new InvalidDataException("REPORTING_OPERATIONAL_CALENDAR_IMPOSSIBLE");
    }
}

public sealed record ReportingInfxSchedule(
    string StrategyId,
    TimeOnly DailyTargetCloseUtc,
    string ContractVersion);

public static class ReportingInfxSchedules
{
    public const string ContractVersion = "tier_1_daily_test_env_target_close_schedule_v1";

    public static IReadOnlyList<ReportingInfxSchedule> All { get; } =
    [
        new("INFX7", new TimeOnly(19, 36), ContractVersion),
        new("INFX8", new TimeOnly(19, 6), ContractVersion),
        new("INFX9", new TimeOnly(12, 36), ContractVersion),
        new("INFX10", new TimeOnly(11, 6), ContractVersion)
    ];

    public static DateTimeOffset ExpectedTargetClose(string strategyId, DateOnly operationalDate)
    {
        var schedule = All.Single(value => value.StrategyId == strategyId);
        return new DateTimeOffset(
            operationalDate.ToDateTime(schedule.DailyTargetCloseUtc, DateTimeKind.Utc));
    }

    public static string Status(
        string strategyId,
        DateTimeOffset asOfUtc,
        DateTimeOffset actualTargetCloseUtc,
        bool selected,
        string classification)
    {
        var expected = ExpectedTargetClose(strategyId, DateOnly.FromDateTime(asOfUtc.UtcDateTime));
        if (asOfUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || asOfUtc < expected)
            return selected
                ? classification.Contains("REUSED", StringComparison.Ordinal)
                    ? "SELECTED_REUSED_AS_SCHEDULED"
                    : "SELECTED_FRESH"
                : "NOT_DUE";
        if (!selected) return "DUE_MISSING";
        if (classification.Contains("REUSED", StringComparison.Ordinal))
            return "SELECTED_REUSED_AS_SCHEDULED";
        return actualTargetCloseUtc.Date == expected.Date
            ? "SELECTED_FRESH"
            : "STALE_AFTER_DUE";
    }
}

public sealed record ReportingSlotManifestProjection(
    string? ContractVersion,
    string? ArtifactSha256,
    int? BboSymbolCount,
    int? InSlotBboEventCount,
    int? PostCloseBboEventCount,
    IReadOnlyDictionary<string, int>? ExcludedPostCloseBySymbol,
    string? SelectionSha256,
    string? ClockPreflightStatus,
    string? ClockAuthoritySnapshotSha256,
    string? ClockPostCloseSnapshotSha256,
    string? ClockReferenceSource,
    double? ClockOffsetMs,
    double? ClockUncertaintyMs,
    double? MaximumLateReceiptAfterCloseMs,
    double? MaximumCrossClockLeadMs,
    string? CrossClockComparison,
    string AuthorityStatus);

public static class ReportingSlotManifestReader
{
    public static ReportingSlotManifestProjection Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Empty();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var contract = Text(root, "slot_bbo_selection_contract_version",
            "SlotBboSelectionContractVersion");
        var artifact = Text(root, "artifact_sha256", "LmaxCaptureSha256",
            "lmax_capture_sha256");
        var bbo = Integer(root, "bbo_symbol_count", "BboCoverageCount",
            "bbo_coverage_count");
        var inSlot = Integer(root, "in_slot_bbo_event_count", "InSlotEventCount",
            "in_slot_event_count");
        var postClose = Integer(root, "post_close_bbo_event_count",
            "PostCloseExclusionCount", "post_close_exclusion_count");
        var selection = Text(root, "selection_sha256");
        var preflight = Text(root, "clock_preflight_status", "ClockPreflightStatus");
        var authoritySha = Text(root, "clock_authority_snapshot_sha256");
        var postCloseSha = Text(root, "clock_post_close_snapshot_sha256");
        var reference = Text(root, "clock_reference_source");
        var offset = Number(root, "clock_offset_ms");
        var uncertainty = Number(root, "clock_uncertainty_ms");
        var late = Number(root, "maximum_late_receipt_after_close_ms");
        var lead = Number(root, "maximum_cross_clock_lead_ms");
        var comparison = Text(root, "cross_clock_comparison");
        var excluded = IntegerMap(root, "excluded_post_close_by_symbol");
        var future = !string.IsNullOrWhiteSpace(contract);
        var futureValid = !future || IsSha(artifact) && bbo == 49 &&
            inSlot.HasValue && postClose.HasValue && IsSha(selection) &&
            preflight == "PASS" && IsSha(authoritySha) && IsSha(postCloseSha) &&
            !string.IsNullOrWhiteSpace(reference) && offset.HasValue &&
            uncertainty.HasValue && late.HasValue && lead.HasValue &&
            !string.IsNullOrWhiteSpace(comparison) && excluded is not null;
        var authority = future
            ? futureValid ? ReportingAuthority.Proven : ReportingAuthority.Unknown
            : ReportingAuthority.Unknown;
        return new(contract, artifact, bbo, inSlot, postClose, excluded, selection,
            preflight, authoritySha, postCloseSha, reference, offset, uncertainty,
            late, lead, comparison, authority);
    }

    private static ReportingSlotManifestProjection Empty() => new(
        null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, ReportingAuthority.Absent);

    private static string? Text(JsonElement root, params string[] names)
    {
        var value = Property(root, names);
        return value is { ValueKind: JsonValueKind.String } ? value.Value.GetString() : null;
    }

    private static int? Integer(JsonElement root, params string[] names)
    {
        var value = Property(root, names);
        return value is { ValueKind: JsonValueKind.Number } &&
               value.Value.TryGetInt32(out var number) ? number : null;
    }

    private static double? Number(JsonElement root, params string[] names)
    {
        var value = Property(root, names);
        return value is { ValueKind: JsonValueKind.Number } &&
               value.Value.TryGetDouble(out var number) ? number : null;
    }

    private static IReadOnlyDictionary<string, int>? IntegerMap(
        JsonElement root,
        params string[] names)
    {
        var value = Property(root, names);
        if (value is not { ValueKind: JsonValueKind.Object }) return null;
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in value.Value.EnumerateObject())
            if (item.Value.TryGetInt32(out var number)) result[item.Name] = number;
            else return null;
        return result;
    }

    private static JsonElement? Property(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in root.EnumerateObject())
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                return property.Value;
        return null;
    }

    private static bool IsSha(string? value) =>
        value is { Length: 64 } &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));
}

public sealed record ReportingReadyMarkerFact(
    string SlotId,
    string Status,
    string AuthorityStatus,
    string? ArtifactSha256,
    DateTimeOffset? ObservedAtUtc,
    string SourceContractVersion);

public static class ReportingEvidenceHash
{
    public static string Canonical(params string?[] values)
    {
        var material = string.Join('\n', values.Select(value => value ?? string.Empty));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
