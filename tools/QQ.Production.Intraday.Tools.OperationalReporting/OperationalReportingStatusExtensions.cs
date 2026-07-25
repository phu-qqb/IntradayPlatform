namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class OperationalReportingStatusExtensions
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
    [
        Code("SLOT_MANIFEST_CONTRACT_UNKNOWN", "MANIFEST",
            OperationalBreakSeverity.Warning,
            "The persisted slot manifest predates or omits the typed BBO selection contract.",
            "Treat manifest fields as absent or unknown; never infer a qualifying pass."),
        Code("SLOT_MANIFEST_REQUIRED_FIELD_MISSING", "MANIFEST",
            OperationalBreakSeverity.Error,
            "A typed PR38 slot manifest is missing or has an invalid required field.",
            "Inspect the exact manifest field, SHA identity and clock authority evidence."),
        Code("READY_MARKER_NOT_PROVEN", "HANDOFF", OperationalBreakSeverity.Warning,
            "No authoritative ready-marker evidence source proves this slot ready.",
            "Keep ready-marker status ABSENT or INCONNU until direct evidence is available."),
        Code("SELECTED_STRATEGY_MISSING", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "A required INFX strategy is absent from SelectedModelRuns.",
            "Inspect the latest qualifying economic revision and its selected model set."),
        Code("SELECTED_STRATEGY_DUPLICATED", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "A required INFX strategy appears more than once in SelectedModelRuns.",
            "Inspect revision serialization and reject ambiguous selected model identity."),
        Code("SELECTED_STRATEGY_UNEXPECTED", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "SelectedModelRuns contains a strategy outside INFX7-INFX10.",
            "Reject the unexpected strategy and preserve the exact source revision."),
        Code("SELECTED_QUBES_INPUT_MISSING", "MODEL_LINEAGE", OperationalBreakSeverity.Error,
            "A selected model has no linked persisted Qubes input snapshot.",
            "Inspect ModelRun and QubesInputSnapshot identities without substituting another run."),
        Code("SELECTED_MODEL_WEIGHT_TARGET_COUNT_MISMATCH", "MODEL_LINEAGE",
            OperationalBreakSeverity.Error,
            "A selected model does not have equal contractual weight and target counts.",
            "Compare the per-model 66/66/78/78 contract; a global total is insufficient."),
        Code("SELECTED_MODEL_TARGET_DRIFT_COUNT_MISMATCH", "MODEL_LINEAGE",
            OperationalBreakSeverity.Error,
            "A selected model does not have equal target and drift counts.",
            "Inspect the selected revision per strategy and preserve model identities.")
    ];

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        OperationalBreakSeverity severity,
        string description,
        string operatorMeaning) =>
        new(
            exactCode,
            "QQ.Production.Intraday.Tools.OperationalReporting",
            category,
            severity,
            "PMS_SHADOW",
            description,
            operatorMeaning,
            AutomaticResolutionPossible: true,
            BlocksTrading: severity is OperationalBreakSeverity.Error or
                OperationalBreakSeverity.Critical,
            BlocksAccounting: false,
            EvidenceRequirements: "Exact source scope, timestamps and content-addressed evidence.",
            Supersedes: null,
            IntroducedByContractVersion: OperationalReportingContract.Version);
}
