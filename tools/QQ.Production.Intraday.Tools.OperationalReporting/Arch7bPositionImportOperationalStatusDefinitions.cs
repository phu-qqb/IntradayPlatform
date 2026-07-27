using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class Arch7bPositionImportOperationalStatusDefinitions
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
    [
        Code("ARCH7B_POSITION_BRACKET_STALE", "FRESHNESS",
            OperationalBreakSeverity.Error,
            "The bracketed broker position snapshot exceeds the canonical 300-second import deadline."),
        Code("ARCH7B_POSITION_IMPORT_PACKAGE_MISSING", "EVIDENCE",
            OperationalBreakSeverity.Error,
            "The content-addressed consumer package root is missing."),
        Code("ARCH7B_POSITION_IMPORT_PACKAGE_INCOMPLETE", "EVIDENCE",
            OperationalBreakSeverity.Error,
            "One or more required consumer package files are missing."),
        Code("ARCH7B_POSITION_IMPORT_CONSUMER_CONTRACT_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Error,
            "The package does not declare the qualified global-flat consumer contract."),
        Code("ARCH7B_POSITION_IMPORT_MANIFEST_FILE_MISSING", "EVIDENCE",
            OperationalBreakSeverity.Error,
            "A file declared by the consumer manifest is absent."),
        Code("ARCH7B_POSITION_IMPORT_MANIFEST_FILE_SHA_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Critical,
            "A declared package file differs from its manifest SHA-256."),
        Code("ARCH7B_POSITION_IMPORT_SNAPSHOT_CONTRACT_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Error,
            "The position snapshot contract differs from the qualified consumer contract."),
        Code("ARCH7B_POSITION_IMPORT_LINEAGE_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Critical,
            "Snapshot lines do not bind exactly to the source ingestion, session, universe, or bracket."),
        Code("ARCH7B_POSITION_IMPORT_LINE_SET_SHA_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Critical,
            "The normalized 99-line set differs from its content address."),
        Code("ARCH7B_POSITION_IMPORT_MANIFEST_BINDING_MISMATCH", "LINEAGE",
            OperationalBreakSeverity.Critical,
            "Manifest snapshot, universe, or line-set identity differs from the payload."),
        Code("ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISSING", "PMS_IMPORT",
            OperationalBreakSeverity.Error,
            "The referenced completed PMS ingestion is absent."),
        Code("ARCH7B_POSITION_IMPORT_SOURCE_ACCOUNT_MISSING", "PMS_IMPORT",
            OperationalBreakSeverity.Error,
            "The referenced authoritative PMS account snapshot is absent."),
        Code("ARCH7B_POSITION_IMPORT_ROW_DELTA_MISMATCH", "PMS_IMPORT",
            OperationalBreakSeverity.Critical,
            "The atomic append produced a row count other than one snapshot plus 99 lines."),
        Code("ARCH7B_POSITION_IMPORT_TARGET_REJECTED", "SECURITY",
            OperationalBreakSeverity.Critical,
            "The requested target is outside the exact ARCH7B RDS TEST contract."),
        Code("ARCH7B_POSITION_IMPORT_DATABASE_IDENTITY_MISMATCH", "SECURITY",
            OperationalBreakSeverity.Critical,
            "The connected database identity differs from qq_pms_shadow_arch7b_test."),
        Code("ARCH7B_POSITION_IMPORT_POSTGRESQL_MAJOR_MISMATCH", "SECURITY",
            OperationalBreakSeverity.Critical,
            "The connected PostgreSQL major version differs from 18."),
        Code("ARCH7B_POSITION_IMPORT_DATABASE_VALUE_MISSING", "RDS",
            OperationalBreakSeverity.Error,
            "A mandatory database identity or transaction fact was not returned.")
    ];

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        OperationalBreakSeverity severity,
        string description) =>
        new(
            exactCode,
            "ARCH7B_POSITION_IMPORT",
            category,
            severity,
            "PMS_SHADOW",
            description,
            $"Stop the import and inspect {exactCode} at the exact source package and target.",
            AutomaticResolutionPossible: severity is not OperationalBreakSeverity.Critical,
            BlocksTrading: true,
            BlocksAccounting: category is "PMS_IMPORT",
            EvidenceRequirements:
                "Exact package SHA identities, source lineage, target fingerprint and read-only plan.",
            Supersedes: null,
            IntroducedByContractVersion: Arch7bPositionImportContract.Version);
}
