namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class Arch7bClockCutoffOperationalStatusDefinitions
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
    [
        Code("CLOCK_AUTHORITY_INVARIANT_FAILED", "CLOCK",
            "The capture clock authority snapshot violates its versioned invariant."),
        Code("CLOCK_AUTHORITY_SNAPSHOT_CONFLICT", "CLOCK",
            "A clock snapshot identity was reused with conflicting content."),
        Code("HANDOFF_READY_MARKER_CLOCK_AUTHORITY_MISMATCH", "HANDOFF",
            "The ready marker is not bound to the qualified clock authority snapshot."),
        Code("RAW_SLOT_BBO_SELECTION_VERSION_MISMATCH", "MARKET_DATA",
            "The slot BBO selection contract version differs from the qualified version."),
        Code("RAW_SLOT_CLOCK_AUTHORITY_MANIFEST_MISMATCH", "CLOCK",
            "The slot manifest clock authority differs from the qualified snapshot."),
        Code("RAW_SLOT_BBO_CLOCK_ENVELOPE_VIOLATION", "CLOCK",
            "A selected BBO lies outside the qualified capture clock envelope."),
        Code("RAW_SLOT_BBO_SELECTION_SHA_MISMATCH", "MARKET_DATA",
            "The selected BBO set differs from its content address."),
        Code("RAW_SLOT_POST_CLOSE_DIAGNOSTIC_MISMATCH", "MARKET_DATA",
            "The post-close exclusion diagnostics differ from the selected BBO evidence."),
        Code("RAW_SLOT_BBO_SELECTED_TIMESTAMP_RANGE_MISMATCH", "MARKET_DATA",
            "The selected BBO timestamp range differs from the manifest."),
        Code("RAW_SLOT_BBO_CLOCK_AUTHORITY_FIELDS_MISMATCH", "CLOCK",
            "The selected BBO clock fields differ from the qualified authority."),
        Code("RAW_SLOT_CLOCK_AUTHORITY_SHA_MISMATCH", "CLOCK",
            "The slot clock authority content differs from its SHA-256."),
        Code("RAW_SLOT_REQUIRED_INSTRUMENT_SET_INCOMPLETE", "MARKET_DATA",
            "The slot does not declare the complete required instrument set."),
        Code("RAW_SLOT_BBO_INSTRUMENT_IDENTITY_MISMATCH", "MARKET_DATA",
            "A selected BBO instrument identity differs from the required set."),
        Code("RAW_SLOT_ARTIFACT_MANIFEST_ROOT_MISMATCH", "LINEAGE",
            "The slot artifact root differs from the manifest root.")
    ];

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        string description) =>
        new(
            exactCode,
            "ARCH7B_CLOCK_CUTOFF",
            category,
            OperationalBreakSeverity.Critical,
            "PMS_SHADOW",
            description,
            $"Stop progression and inspect {exactCode} against the clock and slot evidence.",
            AutomaticResolutionPossible: false,
            BlocksTrading: true,
            BlocksAccounting: false,
            EvidenceRequirements:
                "Clock snapshot SHA, slot manifest SHA, BBO selection SHA and exact UTC timestamps.",
            Supersedes: null,
            IntroducedByContractVersion: "pms_shadow_capture_clock_authority_v1");
}
