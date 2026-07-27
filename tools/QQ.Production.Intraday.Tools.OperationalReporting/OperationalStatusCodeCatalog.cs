namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class OperationalStatusCodeCatalog
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
        Build()
            .Concat(OperationalStatusCodeInventory.Additional)
            .Concat(OperationalReportingStatusExtensions.All)
            .Concat(Arch7bPositionImportOperationalStatusDefinitions.All)
            .OrderBy(value => value.ExactCode, StringComparer.Ordinal).ToArray();

    public static OperationalStatusCodeDefinition Get(string exactCode)
        => All.SingleOrDefault(value => value.ExactCode == exactCode)
           ?? All.Single(value => value.ExactCode == "REPORTING_UNCATALOGUED_SOURCE_CODE");

    private static IEnumerable<OperationalStatusCodeDefinition> Build()
    {
        yield return Code("ARCH7B_CAPTURE_HOST_CLOCK_NOT_QUALIFIED", "CLOCK", OperationalBreakSeverity.Error,
            "Capture host clock has no contemporary qualified authority snapshot.",
            "Synchronize and remeasure the capture host before any new capture.", true, false,
            "Clock snapshot SHA, source, offset, uncertainty and freshness.", "pms_shadow_capture_clock_authority_v1");
        yield return Code("INTRADAY_SLOT_MISSING", "CAPTURE", OperationalBreakSeverity.Error,
            "An expected intraday slot is absent or marked MISSED.",
            "Inspect scheduler ownership, capture evidence and the slot calendar.", true, false,
            "Slot identity, operational date and scheduler evidence.", "pms_shadow_intraday_cadence_v1");
        yield return Code("INTRADAY_SLOT_FAILED_CLOSED", "CAPTURE", OperationalBreakSeverity.Error,
            "The slot failed closed.", "Inspect the persisted failure code and content-addressed evidence.",
            true, false, "Slot row, failure code and manifest SHA.", "pms_shadow_intraday_cadence_v1");
        yield return Code("INTRADAY_SLOT_STALE", "CAPTURE", OperationalBreakSeverity.Warning,
            "The latest qualifying slot is stale.", "Obtain a fresh authorized source before progressing.",
            true, false, "Latest slot end and reporting as-of.", "pms_shadow_intraday_cadence_v1");
        yield return Code("INTRADAY_SLOT_INCOMPLETE", "CAPTURE", OperationalBreakSeverity.Error,
            "A slot lacks required completion facts.", "Inspect manifest, ingestion and lineage fields.",
            true, false, "Slot row and manifest.", "pms_shadow_intraday_cadence_v1");
        yield return Code("SLOT_OVERLAP_REJECTED", "CAPTURE", OperationalBreakSeverity.Warning,
            "A concurrent coordinator claim was rejected.", "Inspect coordinator ownership and overlap evidence.",
            true, false, "Slot and coordinator timeline.", "pms_shadow_intraday_cadence_v1");
        yield return Code("NONQUALIFYING_SLOT_ATTEMPT_MISSING", "CAPTURE",
            OperationalBreakSeverity.Warning, "A nonqualifying slot has no persisted attempt evidence.",
            "Inspect the historical slot manifest and coordinator attempt evidence.", false, false,
            "Slot identity, status and attempt evidence.", "pms_shadow_intraday_cadence_v1");
        yield return Code("RESTART_RECOVERY_REQUIRED", "CAPTURE", OperationalBreakSeverity.Critical,
            "A stale claim requires deterministic restart recovery.", "Run the existing recovery contract.",
            true, false, "Claim age and coordinator identity.", "pms_shadow_intraday_cadence_v1");
        yield return Code("HANDOFF_IMPORT_ABSOLUTE_DEADLINE_EXCEEDED", "HANDOFF", OperationalBreakSeverity.Error,
            "Import did not start inside the 300-second absolute deadline.",
            "Inspect ready-marker detection, hashing and PostgreSQL connection timings.", true, false,
            "Handoff timeline and ready marker.", "pms_shadow_fresh_slot_handoff_v2");
        yield return Code("RAW_SLOT_IN_WINDOW_BBO_COVERAGE_INCOMPLETE", "MARKET_DATA",
            OperationalBreakSeverity.Error, "The slot does not contain the required 49 in-window BBO observations.",
            "Inspect the LMAX artifact and bounded Polygon gap ledger.", true, false,
            "Artifact SHA and per-instrument selection evidence.", "pms_shadow_real_slot_bbo_selection_v1");
        yield return Code("LMAX_GAP_UNFILLED", "MARKET_DATA", OperationalBreakSeverity.Error,
            "A proven LMAX market-data gap was not filled.", "Inspect the gap ledger and bounded fallback evidence.",
            true, false, "LMAX and Polygon gap identifiers.", "pms_shadow_intraday_cadence_v1");
        yield return Code("POLYGON_SOURCE_CONFLICT", "MARKET_DATA", OperationalBreakSeverity.Error,
            "Polygon replaced or conflicted with valid LMAX evidence.", "Inspect source precedence and gap identity.",
            false, false, "Manifest source ledger.", "pms_shadow_intraday_cadence_v1");
        yield return Code("MODEL_SCHEDULE_INCOMPLETE", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "The four INFX model schedule is incomplete.", "Inspect selected and reused finalized ModelRuns.",
            true, false, "ModelRun IDs, schedule and target close.", "pms_shadow_intraday_cadence_v1");
        yield return Code("FOUR_REQUIRED_MODEL_RUNS_MISSING", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "One or more INFX7-INFX10 ModelRuns are absent.", "Inspect Anubis/Qubes handoff lineage.",
            true, false, "ModelRun and Qubes input identities.", "postgresql_pms_shadow_state_contract_v1");
        yield return Code("MODEL_RUN_LINEAGE_INCOMPLETE", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "A ModelRun has incomplete content-addressed lineage.", "Inspect Qubes input, output SHA and Core commit.",
            false, false, "ModelRun row and referenced artifacts.", "postgresql_pms_shadow_state_contract_v1");
        yield return Code("TARGET_POSITION_COUNT_MISMATCH", "TARGET_POSITION", OperationalBreakSeverity.Error,
            "The qualifying revision does not contain 288 TargetPositions.",
            "Inspect model weights, mappings and target projection.", true, true,
            "Revision identity and target SHA.", "pms_shadow_intraday_economic_projection_v1");
        yield return Code("POSITION_ONLY_DRIFT_COUNT_MISMATCH", "DRIFT", OperationalBreakSeverity.Error,
            "The qualifying revision does not contain 288 PositionOnlyDrifts.",
            "Inspect target and position snapshot lineage.", true, true,
            "Revision identity and drift SHA.", "pms_shadow_intraday_economic_projection_v1");
        yield return Code("MARKET_DATA_OBSERVATION_COUNT_MISMATCH", "ECONOMIC_PROJECTION",
            OperationalBreakSeverity.Error, "The qualifying revision does not contain 99 market observations.",
            "Inspect mappings and cross-rate projection coverage.", true, true,
            "Revision identity and market snapshot SHA.", OperationalReportingContract.Version);
        yield return Code("BROKER_WORKING_LEAVES_UNOBSERVABLE", "AUTHORITY", OperationalBreakSeverity.Warning,
            "Broker working leaves are not observable through the current source interfaces.",
            "Do not infer zero working leaves.", false, true,
            "Working-leaves observation row.", "postgresql_pms_shadow_state_contract_v1");
        yield return Code("ARCH7A_QUALIFYING_REVISION_FACTS_INCOMPLETE", "ARCH7A",
            OperationalBreakSeverity.Error, "ARCH7A source facts are incomplete.",
            "Inspect economic revision, target, drift and market lineage.", true, false,
            "ARCH7A qualification and plan SHA.", "arch7a_shadow_execution_v1");
        yield return Code("ARCH7A_NO_ORDER_INVARIANT_REQUIRED", "ARCH7A", OperationalBreakSeverity.Critical,
            "ARCH7A no-order invariants are not proven.", "Stop progression and inspect all route/send flags.",
            false, false, "TradeIntent, RiskDecision, ParentOrder and ChildOrder rows.", "arch7a_shadow_execution_v1");
        yield return Code("ARCH7B_UNKNOWN_CLORDID", "ORDER", OperationalBreakSeverity.Critical,
            "An execution message references an unknown client order ID.",
            "Stop and reconcile known-order registry and broker state.", false, true,
            "ExecutionReport SHA and known-order registry.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_UNKNOWN_ORDERID", "ORDER", OperationalBreakSeverity.Critical,
            "An execution message references an unknown broker order ID.",
            "Stop and reconcile broker identity before any further action.", false, true,
            "ExecutionReport SHA and known-order registry.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_KNOWN_WORKING_LEAVES_REMAIN", "RECONCILIATION",
            OperationalBreakSeverity.Critical, "Known working leaves remain after the lifecycle.",
            "Stop and query only known orders until a terminal state is proven.", false, true,
            "Known-order ExecutionReports and final reconciliation.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_INTERNAL_POSITION_NOT_FLAT", "POSITION_LEDGER",
            OperationalBreakSeverity.Critical, "The internal position ledger is not flat.",
            "Stop and reconcile deduplicated fills against ledger events.", false, true,
            "Fill and PositionLedgerEvent hashes.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_KNOWN_ORDER_NOT_TERMINAL", "ORDER",
            OperationalBreakSeverity.Critical, "A known order has no proven terminal state.",
            "Continue bounded status requests for known orders only.", false, true,
            "ExecutionReport sequence and known-order identity.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_DUPLICATE_MESSAGE_SHA_CONFLICT", "EXECUTION_REPORT",
            OperationalBreakSeverity.Critical, "A duplicate FIX message hash has conflicting identity.",
            "Stop and inspect raw message and ExecID deduplication.", false, true,
            "Raw FIX SHA and execution report.", "arch7b_known_order_lifecycle_v1");
        yield return Code("NO_ORDER_INVARIANT_VIOLATION", "SECURITY",
            OperationalBreakSeverity.Critical, "A no-order counter or route invariant is non-zero.",
            "Stop immediately and inspect order, FIX, Fill and ledger counters.", false, false,
            "No-order counters and manifest SHA.", "pms_shadow_intraday_cadence_v1");
        yield return Code("LINEAGE_INCOMPLETE", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "Persisted source lineage is incomplete.", "Inspect source artifact and model identities.",
            true, true, "Source and output SHA identities.", "postgresql_pms_shadow_state_contract_v1");
        yield return Code("ARCH7B_FINAL_RECONCILIATION_NOT_FLAT", "RECONCILIATION",
            OperationalBreakSeverity.Critical, "Final broker/internal reconciliation is not flat.",
            "Maintain the kill switch and inspect leaves, fills and ledger quantities.", false, true,
            "Final reconciliation evidence SHA.", "arch7b_known_order_lifecycle_v1");
        yield return Code("ARCH7B_FLATTEN_NOT_CONFIRMED", "RECONCILIATION",
            OperationalBreakSeverity.Critical, "Flatten completion is not proven.",
            "Inspect known-order status, execution reports and final broker evidence.", false, true,
            "Flatten order and reconciliation evidence.", "arch7b_known_order_lifecycle_v1");
        yield return Code("REPORTING_TARGET_IDENTITY_MISMATCH", "SECURITY", OperationalBreakSeverity.Critical,
            "The reporting target does not match the authorized TEST fingerprint.",
            "Stop and correct the target arguments; never continue on an ambiguous database.", false, false,
            "Target profile and fingerprint.", OperationalReportingContract.Version);
        yield return Code("REPORTING_UNCATALOGUED_SOURCE_CODE", "OPERATIONAL", OperationalBreakSeverity.Warning,
            "A persisted source code is not yet described by the versioned reporting catalog.",
            "Add the exact source code and operator meaning without changing the source fact.", true, false,
            "Exact source code and source table.", OperationalReportingContract.Version);
    }

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        OperationalBreakSeverity severity,
        string description,
        string operatorMeaning,
        bool automaticResolution,
        bool blocksAccounting,
        string evidence,
        string introducedBy)
        => new(exactCode, "QQ.Production.Intraday", category, severity, "PMS_SHADOW",
            description, operatorMeaning, automaticResolution,
            severity is OperationalBreakSeverity.Error or OperationalBreakSeverity.Critical,
            blocksAccounting, evidence, null, introducedBy);
}
