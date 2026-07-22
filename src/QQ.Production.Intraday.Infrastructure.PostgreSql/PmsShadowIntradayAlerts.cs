namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowIntradayAlertCodes
{
    public static readonly IReadOnlyList<string> Required =
    [
        "INTRADAY_SLOT_MISSING",
        "INTRADAY_SLOT_STALE",
        "INTRADAY_SLOT_INCOMPLETE",
        "INTRADAY_SLOT_FAILED_CLOSED",
        "SLOT_OVERLAP_REJECTED",
        "RESTART_RECOVERY_REQUIRED",
        "LMAX_GAP_UNFILLED",
        "POLYGON_SOURCE_CONFLICT",
        "INGESTION_FAILED",
        "NO_ORDER_INVARIANT_VIOLATION"
    ];
}

public static class PmsShadowIntradayAlertPolicy
{
    public static IReadOnlyList<PmsShadowOperationalAlert> ForIssues(string slotId,
        DateOnly operationalDate, DateTimeOffset nowUtc, string evidenceReference,
        IEnumerable<string> issues)
    {
        var alerts = issues.Select(issue => new PmsShadowOperationalAlert(
            Code(issue), Severity(Code(issue)), slotId, operationalDate, nowUtc,
            evidenceReference, issue)).DistinctBy(value => value.Code)
            .OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
        return alerts;
    }

    private static string Code(string issue) => issue switch
    {
        "INTRADAY_SLOT_MISSING" => "INTRADAY_SLOT_MISSING",
        "INTRADAY_SLOT_STALE" => "INTRADAY_SLOT_STALE",
        "INTRADAY_SLOT_INCOMPLETE" or "SLOT_HANDOFF_NOT_FINALIZED" or
            "SLOT_FINALIZATION_DEADLINE_EXCEEDED" or "QUBES_INPUT_INCOMPLETE" or
            "MODEL_SCHEDULE_INCOMPLETE" or "WEIGHTS_LINEAGE_INCOMPLETE" or
            "TARGET_POSITION_COUNT_MISMATCH" or "POSITION_ONLY_DRIFT_COUNT_MISMATCH" or
            "POSTGRESQL_ROW_COUNTS_MISSING" => "INTRADAY_SLOT_INCOMPLETE",
        "INTRADAY_SLOT_FAILED_CLOSED" => "INTRADAY_SLOT_FAILED_CLOSED",
        "SLOT_OVERLAP_REJECTED" => "SLOT_OVERLAP_REJECTED",
        "RESTART_RECOVERY_REQUIRED" => "RESTART_RECOVERY_REQUIRED",
        "LMAX_GAP_UNFILLED" => "LMAX_GAP_UNFILLED",
        "POLYGON_SOURCE_CONFLICT" or "POLYGON_CALL_LEDGER_MISMATCH" => "POLYGON_SOURCE_CONFLICT",
        "INGESTION_FAILED" => "INGESTION_FAILED",
        "NO_ORDER_INVARIANT_VIOLATION" => "NO_ORDER_INVARIANT_VIOLATION",
        _ => "INTRADAY_SLOT_FAILED_CLOSED"
    };

    private static string Severity(string code) => code switch
    {
        "NO_ORDER_INVARIANT_VIOLATION" => "CRITICAL",
        "RESTART_RECOVERY_REQUIRED" => "CRITICAL",
        "INTRADAY_SLOT_STALE" => "WARN",
        _ => "ERROR"
    };
}
