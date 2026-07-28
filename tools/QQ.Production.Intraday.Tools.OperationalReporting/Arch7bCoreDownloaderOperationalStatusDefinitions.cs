using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class Arch7bCoreDownloaderOperationalStatusDefinitions
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
    [
        Code(
            Arch7bCoreDownloaderCompatibilityContract.HistoricalBlocker,
            "LINEAGE",
            OperationalBreakSeverity.Error,
            "The historical ARCH7B run stopped because the consumer did not yet qualify the Core downloader version."),
        Code(
            Arch7bCoreDownloaderCompatibilityContract.VersionRejected,
            "LINEAGE",
            OperationalBreakSeverity.Critical,
            "The Core downloader implementation version is not explicitly supported."),
        Code(
            Arch7bCoreDownloaderCompatibilityContract.ManifestContractMismatch,
            "LINEAGE",
            OperationalBreakSeverity.Critical,
            "The downloader version in the acquisition manifest differs from the bracket contract."),
        Code(
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid,
            "SECURITY",
            OperationalBreakSeverity.Critical,
            "The sanitized session-recovery provenance is missing, contradictory, or unsafe.")
    ];

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        OperationalBreakSeverity severity,
        string description) =>
        new(
            exactCode,
            "ARCH7B_CORE_BRACKET_CONSUMER",
            category,
            severity,
            "LMAX_DEMO",
            description,
            $"Reject the package and inspect {exactCode} against the exact compatibility profile.",
            AutomaticResolutionPossible: false,
            BlocksTrading: true,
            BlocksAccounting: false,
            EvidenceRequirements:
                "Exact Core commit, contract and manifest versions, content hashes, and sanitized recovery provenance.",
            Supersedes: null,
            IntroducedByContractVersion:
                Arch7bCoreDownloaderCompatibilityContract.Version);
}
