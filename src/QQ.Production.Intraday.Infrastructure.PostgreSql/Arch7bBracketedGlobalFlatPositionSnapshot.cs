using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bBracketedGlobalFlatContract
{
    public const string Version = "lmax_bracketed_global_flat_to_pms_position_snapshot_v1";
    public const string CoreContractVersion = "lmax_portal_bracketed_current_position_snapshot_v2";
    public const string AccountId = "1754288005";
    public const string Environment = "LMAX_LONDON_DEMO";
    public const string SessionMode = "manual-session";
    public const string ExecutionReportSchemaVersion = "lmax_individual_trades_report_schema_v1";
    public const string PositionReportSchemaVersion = "lmax_open_positions_report_schema_v1";
    public const string ExecutionHeaderSetSha256 =
        "aeb30d7c035dcd9ee10a610be23c596602d67c69365ae8ac9848143bedb92303";
    public const string PositionHeaderSetSha256 =
        "13945c1d743dfaad3ed3c9d6f34737860b3e4d563f0ca02ad749b82108576ba9";
    public const string EmptyPositionSetAuthority =
        "CURRENT_SNAPSHOT_DECISION_PLUS_ACCOUNT_AUTHORITY";
    public const string AccountAuthorityMode =
        "ACCOUNT_SCOPED_REPORT_FORM_PLUS_COMPLEMENTARY_ACCOUNT_REPORT";
    public const string CurrentSnapshotStatus = "PROVEN_CURRENT_BRACKETED_SNAPSHOT";
    public const string BrokerDateSequenceStatus = "MONOTONIC_NON_DECREASING";
    public const int MaximumBrokerBracketSpanSeconds = 30;
    public const string ProvenanceKind =
        "DERIVED_ZERO_FROM_PROVEN_COMPLETE_EMPTY_CURRENT_OPEN_POSITIONS_REPORT";
    public const string PositionAuthorityCode =
        "LMAX_PORTAL_BRACKETED_CURRENT_GLOBAL_FLAT_V1";
    public const string WorkingOrderAuthority = "INCONNU";
    public const string WorkingOrderBlocker =
        "ARCH7B_WORKING_ORDER_REPORT_AUTHORITY_UNAVAILABLE";
    public const string NonzeroPositionBlocker =
        "NO_GO_ARCH7B_NONZERO_CURRENT_POSITION_MAPPING_NOT_QUALIFIED";
    public const string TemporalLineageContractVersion =
        "arch7b_broker_snapshot_after_pms_source_v1";
    public const string ImportEligibility =
        "NOT_AUTHORIZED_REQUIRES_FRESH_BRACKET_AND_SEPARATE_IMPORT_PACKET";
    public const string ImportFreshnessStatus =
        "NOT_EVALUATED_FOR_FUTURE_IMPORT";
    public const string SourceSelectionAuthority =
        "LATEST_COMPLETED_INGESTION_FAIL_CLOSED_EXACT_AUTHORITATIVE_MODEL_SET_V1";
    public const string TargetCloseTemporalContract =
        "SCHEDULED_TARGET_CLOSE_MAY_FOLLOW_BROKER_SNAPSHOT_V1";
    public const string TargetProfile = "ARCH7B_RDS_TEST";
    public const string TargetDatabase = "qq_pms_shadow_arch7b_test";
    public const string TargetEnvironment = "TEST";
    public const int PostgreSqlMajor = 18;
    public static readonly IReadOnlyList<string> RequiredStrategies =
        ["INFX7", "INFX8", "INFX9", "INFX10"];
    public static readonly IReadOnlyDictionary<string, int> RequiredStrategyCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["INFX7"] = 66,
            ["INFX8"] = 66,
            ["INFX9"] = 78,
            ["INFX10"] = 78
        };
}

public static class Arch7bCoreDownloaderCompatibilityContract
{
    public const string Version = "arch7b_core_downloader_compatibility_v1";
    public const string LegacyDownloaderVersion = "0.5.0";
    public const string AwsRecoveryDownloaderVersion = "0.6.0";
    public const string LegacyProfile = "LEGACY_MANUAL_SESSION_V2";
    public const string AwsRecoveryProfile = "AWS_SECRET_RECOVERY_MANUAL_SESSION_V2";
    public const string AwsRecoveryMode = "AWS_SECRETS_MANAGER_AUTOMATED_BOOTSTRAP";
    public const string AwsSecretSource = "AWS_SECRETS_MANAGER";
    public const string CredentialSecretContractVersion =
        "lmax_portal_credential_secret_v1";
    public const string LoginFormContractVersion =
        "lmax_london_demo_portal_login_form_v1";
    public const string AutomatedBootstrapContractVersion =
        "lmax_portal_automated_session_recovery_v1";
    public const string ManualSessionReopenStatus =
        "AUTHENTICATED_REPORT_FORM_PRESENT";
    public const string ManualSessionReopenReportType = "account-summary";
    public const string ManualSessionReopenFormId = "account-summary-download";
    public const string VersionRejected =
        "ARCH7B_CORE_DOWNLOADER_VERSION_REJECTED";
    public const string ManifestContractMismatch =
        "ARCH7B_CORE_DOWNLOADER_MANIFEST_CONTRACT_MISMATCH";
    public const string RecoveryMetadataInvalid =
        "ARCH7B_CORE_SESSION_RECOVERY_METADATA_INVALID";
    public const string HistoricalBlocker =
        "NO_GO_ARCH7B_CORE_DOWNLOADER_VERSION_CONTRACT_MISMATCH";

    public static Arch7bCoreDownloaderCompatibilityEvidence LegacyEvidence() =>
        new(
            Version,
            LegacyProfile,
            null, null, null, null, null, null, null, null, null, null,
            null, null, null, null);

    public static string ResolveProfile(string downloaderVersion) =>
        downloaderVersion switch
        {
            LegacyDownloaderVersion => LegacyProfile,
            AwsRecoveryDownloaderVersion => AwsRecoveryProfile,
            _ => throw new InvalidDataException(VersionRejected)
        };
}

public sealed record Arch7bCoreDownloaderCompatibilityEvidence(
    string ContractVersion,
    string Profile,
    string? SessionRecoveryMode,
    bool? SessionAlreadyActive,
    bool? SecretFetched,
    bool? LoginPerformed,
    string? MfaMode,
    string? SecretReferenceSha256,
    bool? SecretVersionIdPresent,
    string? CredentialSecretContractVersion,
    string? LoginFormContractVersion,
    string? AutomatedBootstrapContractVersion,
    bool? ManualSessionReopenProven,
    bool? CredentialsRecorded,
    bool? SecretValuesRecorded,
    bool? TotpRecorded);

public sealed record Arch7bCoreEvidenceExpectations(
    string CoreRepositoryCommit,
    string EvidenceSha256,
    string ContractFileSha256,
    string FinalIndexSha256);

public sealed record Arch7bCoreBracketEvidence(
    string CoreRepositoryCommit,
    string CoreContractVersion,
    string DownloaderVersion,
    string AccountId,
    string Environment,
    string SessionMode,
    int PositionCount,
    int ExecutionCount,
    bool StableExecutionSet,
    bool StablePositionSet,
    string ExecutionReportSchemaVersion,
    string PositionReportSchemaVersion,
    string ExecutionHeaderSetSha256,
    string PositionHeaderSetSha256,
    string EmptyPositionSetAuthority,
    string AccountAuthorityMode,
    string CurrentSnapshotStatus,
    string BrokerDateSequenceStatus,
    int BrokerBracketSpanSeconds,
    int MaximumBrokerBracketSpanSeconds,
    DateTimeOffset BracketLowerBoundUtc,
    DateTimeOffset PositionReportP2Utc,
    DateTimeOffset BracketUpperBoundUtc,
    string RawArtifactSetSha256,
    string SemanticArtifactSetSha256,
    string EvidenceSha256,
    string ContractFileSha256,
    string FinalIndexSha256,
    bool NoOrder,
    bool NoFix,
    bool NoDatabaseWrite,
    bool NoAccountApi,
    bool NoDatabento,
    string EvidenceRoot,
    int IndexedFileCount,
    Arch7bCoreBracketReportSemanticVerification? RecomputedSemantics = null,
    Arch7bCoreDownloaderCompatibilityEvidence? DownloaderCompatibility = null);

public static class Arch7bCoreBracketEvidencePackageReader
{
    private static readonly Regex[] SecretPatterns =
    [
        new(@"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b", RegexOptions.CultureInvariant),
        new(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
            RegexOptions.CultureInvariant),
        new(@"(?i)\b(?:password|passwd|client_secret|secret_access_key|authorization)\b\s*[:=]\s*[""']?[^\s,""'}]{4,}",
            RegexOptions.CultureInvariant)
    ];

    private static readonly string[] RequiredIndexedFiles =
    [
        "acquisition-manifest.json",
        "complementary/account-statement.pdf",
        "complementary/account-summary.csv",
        "complementary/currency-wallets.csv",
        "complementary/trades.csv",
        "lmax-portal-bracketed-current-position-snapshot-v2.json",
        "validation/core-master-qualification-summary.json",
        "validation/runner-tests.stderr.log",
        "validation/runner-tests.stdout.log"
    ];

    public static Arch7bCoreBracketEvidence Read(
        string evidenceRoot,
        Arch7bCoreEvidenceExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        var root = Path.GetFullPath(evidenceRoot);
        Require(Directory.Exists(root), "ARCH7B_CORE_EVIDENCE_ROOT_MISSING");
        RequireNoReparsePoints(root, root);

        var indexPath = SafePath(root, "validation/final-evidence-index.json");
        var contractPath = SafePath(root,
            "lmax-portal-bracketed-current-position-snapshot-v2.json");
        var manifestPath = SafePath(root, "acquisition-manifest.json");
        var qualificationPath = SafePath(root,
            "validation/core-master-qualification-summary.json");
        Require(File.Exists(indexPath), "ARCH7B_CORE_FINAL_INDEX_MISSING");
        Require(File.Exists(contractPath), "ARCH7B_CORE_CONTRACT_FILE_MISSING");
        Require(File.Exists(manifestPath), "ARCH7B_CORE_ACQUISITION_MANIFEST_MISSING");
        Require(File.Exists(qualificationPath), "ARCH7B_CORE_QUALIFICATION_MISSING");

        var finalIndexSha = FileSha(indexPath);
        RequireSha(expectations.FinalIndexSha256, "ARCH7B_EXPECTED_FINAL_INDEX_SHA_INVALID");
        Require(finalIndexSha == expectations.FinalIndexSha256,
            "ARCH7B_CORE_FINAL_INDEX_SHA_MISMATCH");
        var contractFileSha = FileSha(contractPath);
        RequireSha(expectations.ContractFileSha256,
            "ARCH7B_EXPECTED_CONTRACT_FILE_SHA_INVALID");
        Require(contractFileSha == expectations.ContractFileSha256,
            "ARCH7B_CORE_CONTRACT_FILE_SHA_MISMATCH");

        var index = ParseObject(indexPath, "ARCH7B_CORE_FINAL_INDEX_JSON_INVALID");
        Require(Text(index, "contract") == "arch7b_core_master_final_evidence_index_v1",
            "ARCH7B_CORE_FINAL_INDEX_CONTRACT_INVALID");
        Require(Text(index, "core_repository_commit") == expectations.CoreRepositoryCommit,
            "ARCH7B_CORE_REPOSITORY_COMMIT_MISMATCH");
        Require(True(index, "no_order"), "ARCH7B_CORE_INDEX_NO_ORDER_INVALID");
        Require(True(index, "no_fix"), "ARCH7B_CORE_INDEX_NO_FIX_INVALID");
        Require(True(index, "no_account_api"), "ARCH7B_CORE_INDEX_ACCOUNT_API_INVALID");
        Require(True(index, "no_database_write"),
            "ARCH7B_CORE_INDEX_DATABASE_WRITE_INVALID");
        Require(True(index, "no_databento"), "ARCH7B_CORE_INDEX_DATABENTO_INVALID");
        Require(False(index, "secret_values_recorded"),
            "ARCH7B_CORE_INDEX_SECRET_RECORDED");

        var indexed = ValidateIndex(root, index);
        foreach (var required in RequiredIndexedFiles)
            Require(indexed.Contains(required), $"ARCH7B_CORE_INDEX_REQUIRED_FILE_MISSING:{required}");

        var contract = ParseObject(contractPath, "ARCH7B_CORE_CONTRACT_JSON_INVALID");
        var recomputed = Arch7bCoreBracketReportSemanticVerifier.Verify(
            root, contract, indexed);
        var manifest = ParseObject(manifestPath, "ARCH7B_CORE_MANIFEST_JSON_INVALID");
        var qualification = ParseObject(qualificationPath,
            "ARCH7B_CORE_QUALIFICATION_JSON_INVALID");
        var compatibility = ValidateManifest(
            manifest, contract, contractFileSha);
        Require(Text(qualification, "repository") == "phu-qqb/QQ.Production.Core",
            "ARCH7B_CORE_QUALIFICATION_REPOSITORY_INVALID");
        Require(Text(qualification, "merge_commit") == expectations.CoreRepositoryCommit,
            "ARCH7B_CORE_QUALIFICATION_COMMIT_MISMATCH");
        Require(True(qualification, "no_order") && True(qualification, "no_fix") &&
                True(qualification, "no_account_api") &&
                True(qualification, "no_database_write") &&
                True(qualification, "no_databento") &&
                False(qualification, "secret_values_recorded"),
            "ARCH7B_CORE_QUALIFICATION_SAFETY_INVALID");

        var evidence = BuildEvidence(root, contract, expectations.CoreRepositoryCommit,
            contractFileSha, finalIndexSha, indexed.Count, recomputed,
            compatibility);
        ValidateSemanticContract(evidence);
        ValidateRecalculableHashes(root, contract, evidence);
        RequireSha(expectations.EvidenceSha256, "ARCH7B_EXPECTED_EVIDENCE_SHA_INVALID");
        Require(evidence.EvidenceSha256 == expectations.EvidenceSha256,
            "ARCH7B_CORE_EVIDENCE_SHA_MISMATCH");
        return evidence;
    }

    public static void ValidateSemanticContract(Arch7bCoreBracketEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Require(value.CoreContractVersion == Arch7bBracketedGlobalFlatContract.CoreContractVersion,
            "ARCH7B_CORE_CONTRACT_VERSION_REJECTED");
        var compatibilityProfile =
            Arch7bCoreDownloaderCompatibilityContract.ResolveProfile(
                value.DownloaderVersion);
        var compatibility = value.DownloaderCompatibility ??
            throw new InvalidDataException(
                Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
        ValidateCompatibilityEvidence(compatibility, compatibilityProfile);
        Require(value.AccountId == Arch7bBracketedGlobalFlatContract.AccountId,
            "ARCH7B_CORE_ACCOUNT_ID_MISMATCH");
        Require(value.Environment == Arch7bBracketedGlobalFlatContract.Environment,
            "ARCH7B_CORE_ENVIRONMENT_MISMATCH");
        Require(value.SessionMode == Arch7bBracketedGlobalFlatContract.SessionMode,
            "ARCH7B_CORE_SESSION_MODE_MISMATCH");
        if (value.PositionCount != 0)
            throw new InvalidDataException(
                Arch7bBracketedGlobalFlatContract.NonzeroPositionBlocker);
        Require(value.ExecutionCount >= 0, "ARCH7B_CORE_EXECUTION_COUNT_INVALID");
        Require(value.StableExecutionSet, "ARCH7B_CORE_EXECUTION_SET_UNSTABLE");
        Require(value.StablePositionSet, "ARCH7B_CORE_POSITION_SET_UNSTABLE");
        Require(value.ExecutionReportSchemaVersion ==
                Arch7bBracketedGlobalFlatContract.ExecutionReportSchemaVersion,
            "ARCH7B_CORE_EXECUTION_SCHEMA_MISMATCH");
        Require(value.PositionReportSchemaVersion ==
                Arch7bBracketedGlobalFlatContract.PositionReportSchemaVersion,
            "ARCH7B_CORE_POSITION_SCHEMA_MISMATCH");
        Require(value.ExecutionHeaderSetSha256 ==
                Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256,
            "ARCH7B_CORE_EXECUTION_HEADER_SHA_MISMATCH");
        Require(value.PositionHeaderSetSha256 ==
                Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256,
            "ARCH7B_CORE_POSITION_HEADER_SHA_MISMATCH");
        Require(value.EmptyPositionSetAuthority ==
                Arch7bBracketedGlobalFlatContract.EmptyPositionSetAuthority,
            "ARCH7B_CORE_EMPTY_POSITION_AUTHORITY_MISMATCH");
        Require(value.AccountAuthorityMode ==
                Arch7bBracketedGlobalFlatContract.AccountAuthorityMode,
            "ARCH7B_CORE_ACCOUNT_AUTHORITY_MISMATCH");
        Require(value.CurrentSnapshotStatus ==
                Arch7bBracketedGlobalFlatContract.CurrentSnapshotStatus,
            "ARCH7B_CORE_CURRENT_SNAPSHOT_NOT_PROVEN");
        Require(value.BrokerDateSequenceStatus ==
                Arch7bBracketedGlobalFlatContract.BrokerDateSequenceStatus,
            "ARCH7B_CORE_BROKER_DATE_SEQUENCE_INVALID");
        Require(value.MaximumBrokerBracketSpanSeconds ==
                Arch7bBracketedGlobalFlatContract.MaximumBrokerBracketSpanSeconds,
            "ARCH7B_CORE_MAXIMUM_BRACKET_SPAN_INVALID");
        Require(value.BrokerBracketSpanSeconds is >= 0 and <=
                Arch7bBracketedGlobalFlatContract.MaximumBrokerBracketSpanSeconds,
            "ARCH7B_CORE_BRACKET_SPAN_EXCEEDED");
        Require(value.BracketLowerBoundUtc.Offset == TimeSpan.Zero &&
                value.PositionReportP2Utc.Offset == TimeSpan.Zero &&
                value.BracketUpperBoundUtc.Offset == TimeSpan.Zero &&
                value.BracketLowerBoundUtc <= value.PositionReportP2Utc &&
                value.PositionReportP2Utc <= value.BracketUpperBoundUtc,
            "ARCH7B_CORE_TEMPORAL_AUTHORITY_INVALID");
        RequireSha(value.CoreRepositoryCommit, "ARCH7B_CORE_COMMIT_INVALID", 40);
        RequireSha(value.RawArtifactSetSha256, "ARCH7B_CORE_RAW_ARTIFACT_SHA_INVALID");
        RequireSha(value.SemanticArtifactSetSha256,
            "ARCH7B_CORE_SEMANTIC_ARTIFACT_SHA_INVALID");
        RequireSha(value.EvidenceSha256, "ARCH7B_CORE_EVIDENCE_SHA_INVALID");
        RequireSha(value.ContractFileSha256, "ARCH7B_CORE_CONTRACT_FILE_SHA_INVALID");
        RequireSha(value.FinalIndexSha256, "ARCH7B_CORE_FINAL_INDEX_SHA_INVALID");
        Require(value.NoOrder && value.NoFix && value.NoDatabaseWrite &&
                value.NoAccountApi && value.NoDatabento,
            "ARCH7B_CORE_SAFETY_CONTRACT_INVALID");
    }

    private static void ValidateCompatibilityEvidence(
        Arch7bCoreDownloaderCompatibilityEvidence value,
        string expectedProfile)
    {
        Require(value.ContractVersion ==
                Arch7bCoreDownloaderCompatibilityContract.Version &&
                value.Profile == expectedProfile,
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
        if (expectedProfile == Arch7bCoreDownloaderCompatibilityContract.LegacyProfile)
        {
            Require(value.SessionRecoveryMode is null &&
                    value.SessionAlreadyActive is null &&
                    value.SecretFetched is null &&
                    value.LoginPerformed is null &&
                    value.MfaMode is null &&
                    value.SecretReferenceSha256 is null &&
                    value.SecretVersionIdPresent is null &&
                    value.CredentialSecretContractVersion is null &&
                    value.LoginFormContractVersion is null &&
                    value.AutomatedBootstrapContractVersion is null &&
                    value.ManualSessionReopenProven is null &&
                    value.CredentialsRecorded is null &&
                    value.SecretValuesRecorded is null &&
                    value.TotpRecorded is null,
                Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
            return;
        }

        Require(value.SessionRecoveryMode ==
                    Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryMode &&
                value.CredentialSecretContractVersion ==
                    Arch7bCoreDownloaderCompatibilityContract.CredentialSecretContractVersion &&
                value.LoginFormContractVersion ==
                    Arch7bCoreDownloaderCompatibilityContract.LoginFormContractVersion &&
                value.AutomatedBootstrapContractVersion ==
                    Arch7bCoreDownloaderCompatibilityContract.AutomatedBootstrapContractVersion &&
                value.CredentialsRecorded == false &&
                value.SecretValuesRecorded == false &&
                value.TotpRecorded == false,
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
        RequireSha(value.SecretReferenceSha256,
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
        ValidateRecoveryState(value);
    }

    private static HashSet<string> ValidateIndex(string root, JsonObject index)
    {
        var files = index["files"]?.AsArray()
            ?? throw new InvalidDataException("ARCH7B_CORE_FINAL_INDEX_FILES_MISSING");
        Require(Int32(index, "file_count_excluding_index") == files.Count,
            "ARCH7B_CORE_FINAL_INDEX_COUNT_MISMATCH");
        var indexed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in files)
        {
            var entry = item?.AsObject()
                ?? throw new InvalidDataException("ARCH7B_CORE_FINAL_INDEX_ENTRY_INVALID");
            var relative = NormalizeRelative(Text(entry, "relative_path"));
            Require(indexed.Add(relative), "ARCH7B_CORE_FINAL_INDEX_DUPLICATE_PATH");
            var file = SafePath(root, relative);
            Require(File.Exists(file), $"ARCH7B_CORE_INDEXED_ARTIFACT_MISSING:{relative}");
            RequireNoReparsePoints(root, file);
            var info = new FileInfo(file);
            Require(info.Length == Int64(entry, "bytes"),
                $"ARCH7B_CORE_INDEXED_ARTIFACT_SIZE_MISMATCH:{relative}");
            Require(FileSha(file) == Text(entry, "sha256"),
                $"ARCH7B_CORE_INDEXED_ARTIFACT_SHA_MISMATCH:{relative}");
            RejectSecrets(file, relative);
        }

        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelative(Path.GetRelativePath(root, path)))
            .Where(path => path != "validation/final-evidence-index.json")
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(indexed), "ARCH7B_CORE_FINAL_INDEX_INVENTORY_MISMATCH");
        return indexed;
    }

    private static Arch7bCoreBracketEvidence BuildEvidence(
        string root,
        JsonObject contract,
        string coreCommit,
        string contractFileSha,
        string finalIndexSha,
        int fileCount,
        Arch7bCoreBracketReportSemanticVerification recomputed,
        Arch7bCoreDownloaderCompatibilityEvidence compatibility)
    {
        var attempts = contract["Attempts"]?.AsArray()
            ?? throw new InvalidDataException("ARCH7B_CORE_ATTEMPTS_MISSING");
        Require(attempts.Count is >= 1 and <= 3, "ARCH7B_CORE_ATTEMPT_COUNT_INVALID");
        var last = attempts[^1]?.AsObject()
            ?? throw new InvalidDataException("ARCH7B_CORE_FINAL_ATTEMPT_MISSING");
        var p2 = Object(last, "P2");
        var decision = Object(contract, "OpenPositionsSnapshotSemanticDecision");
        return new(
            coreCommit,
            Text(contract, "ContractVersion"),
            Text(contract, "DownloaderVersion"),
            Text(contract, "AccountId"),
            Text(contract, "Environment"),
            Text(contract, "SessionMode"),
            Int32(contract, "PositionCount"),
            Int32(contract, "ExecutionCount"),
            True(contract, "StableExecutionSet"),
            True(contract, "StablePositionSet"),
            Text(contract, "ExecutionReportSchemaVersion"),
            Text(contract, "PositionReportSchemaVersion"),
            Text(contract, "T2HeaderSetSha256"),
            Text(contract, "P2HeaderSetSha256"),
            Text(contract, "EmptyPositionSetAuthority"),
            Text(contract, "AccountAuthorityMode"),
            Text(decision, "CurrentSnapshotStatus"),
            Text(contract, "BrokerDateSequenceStatus"),
            Int32(contract, "BrokerBracketSpanSeconds"),
            Int32(contract, "MaximumBrokerBracketSpanSeconds"),
            Date(contract, "AsOfLowerBoundUtc"),
            DateTimeOffset.Parse(Text(p2, "ResponseServerDateUtc"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            Date(contract, "AsOfUpperBoundUtc"),
            Text(contract, "RawArtifactSetSha256"),
            Text(contract, "SemanticArtifactSetSha256"),
            Text(contract, "EvidenceSha256"),
            contractFileSha,
            finalIndexSha,
            True(contract, "NoOrder"),
            True(contract, "NoFix"),
            True(contract, "NoDatabaseWrite"),
            true,
            true,
            root,
            fileCount,
            recomputed,
            compatibility);
    }

    private static Arch7bCoreDownloaderCompatibilityEvidence ValidateManifest(
        JsonObject manifest,
        JsonObject contract,
        string contractFileSha)
    {
        var downloaderVersion = Text(contract, "DownloaderVersion");
        var profile = Arch7bCoreDownloaderCompatibilityContract.ResolveProfile(
            downloaderVersion);
        Require(Text(manifest, "account_id") == Arch7bBracketedGlobalFlatContract.AccountId,
            "ARCH7B_CORE_MANIFEST_ACCOUNT_MISMATCH");
        Require(Text(manifest, "session_mode") == Arch7bBracketedGlobalFlatContract.SessionMode,
            "ARCH7B_CORE_MANIFEST_SESSION_MODE_MISMATCH");
        Require(False(manifest, "flow_capture_contains_headers") &&
                False(manifest, "flow_capture_contains_cookies") &&
                False(manifest, "flow_capture_contains_credentials"),
            "ARCH7B_CORE_MANIFEST_SENSITIVE_FLOW_CAPTURE");
        var safety = Object(manifest, "safety");
        Require(False(safety, "order_entry_enabled") &&
                False(safety, "operational_orders") &&
                False(safety, "production_live") &&
                False(safety, "lmax_order_entry_used") &&
                False(safety, "lmax_fix_order_entry_used") &&
                False(safety, "lmax_accountapi_used"),
            "ARCH7B_CORE_MANIFEST_SAFETY_INVALID");
        var artifact = Object(manifest, "bracketed_contract_artifact");
        Require(Path.GetFileName(Text(artifact, "path")) ==
                "lmax-portal-bracketed-current-position-snapshot-v2.json",
            "ARCH7B_CORE_MANIFEST_CONTRACT_PATH_INVALID");
        Require(Text(artifact, "sha256") == contractFileSha,
            "ARCH7B_CORE_MANIFEST_CONTRACT_SHA_MISMATCH");

        var manifestDownloaderVersion = OptionalText(
            manifest, "downloader_version",
            Arch7bCoreDownloaderCompatibilityContract.ManifestContractMismatch);
        Require(manifestDownloaderVersion is null ||
                manifestDownloaderVersion == downloaderVersion,
            Arch7bCoreDownloaderCompatibilityContract.ManifestContractMismatch);
        if (profile == Arch7bCoreDownloaderCompatibilityContract.LegacyProfile)
        {
            return new(
                Arch7bCoreDownloaderCompatibilityContract.Version,
                profile,
                null, null, null, null, null, null, null, null, null, null,
                null, null, null, null);
        }

        Require(manifestDownloaderVersion == downloaderVersion,
            Arch7bCoreDownloaderCompatibilityContract.ManifestContractMismatch);
        RejectSensitiveRecoveryProperties(manifest);
        var recoveryCode =
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid;
        Require(OptionalText(manifest, "session_recovery_mode", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryMode &&
                OptionalText(manifest, "secret_source", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.AwsSecretSource &&
                OptionalText(manifest, "secret_keys_contract_version", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.CredentialSecretContractVersion &&
                OptionalText(manifest, "login_form_contract", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.LoginFormContractVersion &&
                OptionalText(manifest, "automated_bootstrap_contract", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.AutomatedBootstrapContractVersion,
            recoveryCode);
        Require(OptionalBool(manifest, "credentials_recorded", recoveryCode) == false &&
                OptionalBool(manifest, "secret_values_recorded", recoveryCode) == false &&
                OptionalBool(manifest, "totp_recorded", recoveryCode) == false,
            recoveryCode);
        var secretReferenceSha = OptionalText(
            manifest, "secret_reference_sha256", recoveryCode);
        RequireSha(secretReferenceSha, recoveryCode);

        var sessionAlreadyActive = OptionalBool(
            manifest, "session_already_active", recoveryCode);
        var secretFetched = OptionalBool(manifest, "secret_fetched", recoveryCode);
        var loginPerformed = OptionalBool(manifest, "login_performed", recoveryCode);
        var mfaMode = OptionalText(manifest, "mfa_mode", recoveryCode);
        Require(mfaMode is null || !string.IsNullOrWhiteSpace(mfaMode), recoveryCode);
        var secretVersionId = OptionalText(manifest, "secret_version_id", recoveryCode);
        var reopenProof = manifest["manual_session_reopen_proof"] switch
        {
            null => null,
            JsonObject value => value,
            _ => throw new InvalidDataException(recoveryCode)
        };
        bool? reopenProven = null;
        if (reopenProof is not null)
        {
            reopenProven =
                OptionalText(reopenProof, "status", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenStatus &&
                OptionalText(reopenProof, "account_id", recoveryCode) ==
                    Arch7bBracketedGlobalFlatContract.AccountId &&
                OptionalText(reopenProof, "report_type", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenReportType &&
                OptionalText(reopenProof, "form_id", recoveryCode) ==
                    Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenFormId &&
                OptionalBool(reopenProof, "secret_read_during_probe", recoveryCode) == false &&
                OptionalBool(reopenProof, "credentials_recorded", recoveryCode) == false;
            Require(reopenProven == true, recoveryCode);
        }

        var compatibility = new Arch7bCoreDownloaderCompatibilityEvidence(
            Arch7bCoreDownloaderCompatibilityContract.Version,
            profile,
            Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryMode,
            sessionAlreadyActive,
            secretFetched,
            loginPerformed,
            mfaMode,
            secretReferenceSha,
            secretVersionId is null ? false : !string.IsNullOrWhiteSpace(secretVersionId),
            Arch7bCoreDownloaderCompatibilityContract.CredentialSecretContractVersion,
            Arch7bCoreDownloaderCompatibilityContract.LoginFormContractVersion,
            Arch7bCoreDownloaderCompatibilityContract.AutomatedBootstrapContractVersion,
            reopenProven,
            false,
            false,
            false);
        ValidateRecoveryState(compatibility);
        return compatibility;
    }

    private static void ValidateRecoveryState(
        Arch7bCoreDownloaderCompatibilityEvidence value)
    {
        var code = Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid;
        if (value.SecretFetched == true)
            Require(value.LoginPerformed == true &&
                    value.SecretVersionIdPresent == true &&
                    value.ManualSessionReopenProven == true,
                code);
        if (value.LoginPerformed == true)
            Require(value.SecretFetched == true &&
                    value.ManualSessionReopenProven == true,
                code);
        if (value.SessionAlreadyActive == true)
            Require(value.SecretFetched != true && value.LoginPerformed != true, code);
    }

    private static string? OptionalText(
        JsonObject value,
        string name,
        string code)
    {
        try
        {
            return value[name]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidDataException(code);
        }
    }

    private static bool? OptionalBool(
        JsonObject value,
        string name,
        string code)
    {
        try
        {
            return value[name]?.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidDataException(code);
        }
    }

    private static void RejectSensitiveRecoveryProperties(JsonNode node)
    {
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "username", "password", "totp_seed", "totp_code", "cookie",
            "cookies", "authorization", "secret_string", "secretstring"
        };
        if (node is JsonObject value)
        {
            foreach (var property in value)
            {
                Require(!sensitive.Contains(property.Key),
                    Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid);
                if (property.Value is not null)
                    RejectSensitiveRecoveryProperties(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                if (item is not null) RejectSensitiveRecoveryProperties(item);
        }
    }

    private static void ValidateRecalculableHashes(
        string root,
        JsonObject contract,
        Arch7bCoreBracketEvidence evidence)
    {
        var successful = evidence.RecomputedSemantics
            ?? throw new InvalidDataException("ARCH7B_CORE_RECOMPUTED_SEMANTICS_MISSING");
        var last = contract["Attempts"]!.AsArray()[successful.SuccessfulAttemptNumber - 1]!.AsObject();
        var labels = new[] { "T0", "P1", "T1", "P2", "T2" };
        var complementaryFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["account-statement"] = "account-statement.pdf",
            ["account-summary"] = "account-summary.csv",
            ["currency-wallets"] = "currency-wallets.csv",
            ["trades"] = "trades.csv"
        };
        var complementaryTypes = new HashSet<string>(StringComparer.Ordinal);
        var complementaryRaw = (contract["ComplementaryReports"]?.AsArray()
                ?? throw new InvalidDataException(
                    "ARCH7B_CORE_COMPLEMENTARY_REPORTS_MISSING"))
            .Select(node =>
            {
                var report = node?.AsObject()
                    ?? throw new InvalidDataException(
                        "ARCH7B_CORE_COMPLEMENTARY_REPORT_INVALID");
                var reportType = Text(report, "ReportType");
                Require(complementaryFiles.TryGetValue(reportType, out var fileName) &&
                        complementaryTypes.Add(reportType),
                    "ARCH7B_CORE_COMPLEMENTARY_REPORT_SET_INVALID");
                Require(Text(report, "SelectedAccountId") ==
                        Arch7bBracketedGlobalFlatContract.AccountId,
                    "ARCH7B_CORE_COMPLEMENTARY_REPORT_ACCOUNT_MISMATCH");
                var rawSha = Text(report, "RawSha256");
                var artifact = Object(report, "Artifact");
                Require(Text(artifact, "sha256") == rawSha &&
                        Path.GetFileName(Text(artifact, "path")) == fileName,
                    "ARCH7B_CORE_COMPLEMENTARY_REPORT_ARTIFACT_MISMATCH");
                var localPath = SafePath(root, $"complementary/{fileName}");
                Require(File.Exists(localPath) &&
                        new FileInfo(localPath).Length == Int64(artifact, "size") &&
                        FileSha(localPath) == rawSha,
                    "ARCH7B_CORE_COMPLEMENTARY_REPORT_HASH_MISMATCH");
                return rawSha;
            })
            .ToArray();
        Require(complementaryTypes.SetEquals(complementaryFiles.Keys),
            "ARCH7B_CORE_COMPLEMENTARY_REPORT_SET_INVALID");
        var raw = labels.Select(label => Text(Object(last, label), "RawSha256"))
            .Concat(complementaryRaw)
            .Order(StringComparer.Ordinal);
        Require(Sha256(string.Join("\n", raw)) == evidence.RawArtifactSetSha256,
            "ARCH7B_CORE_RAW_ARTIFACT_SET_SHA_MISMATCH");

        var complementary = Object(contract, "ComplementaryAccountEvidence");
        Require(HashJsonWithout(complementary, "EvidenceSha256") ==
                Text(complementary, "EvidenceSha256"),
            "ARCH7B_CORE_COMPLEMENTARY_ACCOUNT_EVIDENCE_SHA_MISMATCH");
        var decision = Object(contract, "OpenPositionsSnapshotSemanticDecision");
        Require(HashJsonWithout(decision, "EvidenceSha256") == Text(decision, "EvidenceSha256"),
            "ARCH7B_CORE_CURRENT_DECISION_EVIDENCE_SHA_MISMATCH");
        var semanticCore = new JsonObject
        {
            ["T0"] = Text(Object(last, "T0"), "SemanticSha256"),
            ["T1"] = Text(Object(last, "T1"), "SemanticSha256"),
            ["T2"] = Text(Object(last, "T2"), "SemanticSha256"),
            ["P1"] = Text(Object(last, "P1"), "SemanticSha256"),
            ["P2"] = Text(Object(last, "P2"), "SemanticSha256"),
            ["ExecutionHeaders"] = evidence.ExecutionHeaderSetSha256,
            ["PositionHeaders"] = evidence.PositionHeaderSetSha256,
            ["ComplementaryAccountEvidence"] = Text(complementary, "EvidenceSha256"),
            ["CurrentSnapshotDecision"] = Text(decision, "EvidenceSha256")
        };
        Require(Sha256(semanticCore.ToJsonString()) == evidence.SemanticArtifactSetSha256,
            "ARCH7B_CORE_SEMANTIC_ARTIFACT_SET_SHA_MISMATCH");
        Require(HashJsonWithout(contract, "EvidenceSha256") == evidence.EvidenceSha256,
            "ARCH7B_CORE_CONTRACT_EVIDENCE_SHA_MISMATCH");

    }


    private static void RejectSecrets(string path, string relative)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(bytes);
        if (SecretPatterns.Any(pattern => pattern.IsMatch(text)))
            throw new InvalidDataException($"ARCH7B_CORE_SECRET_PATTERN_DETECTED:{relative}");
    }

    private static string SafePath(string root, string relative)
    {
        var normalized = NormalizeRelative(relative);
        Require(!Path.IsPathRooted(relative) && !relative.Contains(':'),
            "ARCH7B_CORE_PATH_TRAVERSAL_REJECTED");
        var parts = normalized.Split('/');
        Require(parts.All(part => part is not "" and not "." and not ".."),
            "ARCH7B_CORE_PATH_TRAVERSAL_REJECTED");
        var full = Path.GetFullPath(Path.Combine(root,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            "ARCH7B_CORE_PATH_TRAVERSAL_REJECTED");
        return full;
    }

    private static void RequireNoReparsePoints(string root, string path)
    {
        var current = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        while (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "ARCH7B_CORE_REPARSE_POINT_REJECTED");
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
            current = Path.GetDirectoryName(current)!;
        }
        if (File.Exists(path))
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
                "ARCH7B_CORE_REPARSE_POINT_REJECTED");
    }

    private static string HashJsonWithout(JsonObject source, string property)
    {
        var clone = JsonNode.Parse(source.ToJsonString())!.AsObject();
        Require(clone.Remove(property), $"ARCH7B_CORE_HASH_FIELD_MISSING:{property}");
        return Sha256(clone.ToJsonString(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private static JsonObject ParseObject(string path, string code)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException(code);
        }
        catch (JsonException)
        {
            throw new InvalidDataException(code);
        }
    }

    private static JsonObject Object(JsonObject value, string name) =>
        value[name]?.AsObject()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static string Text(JsonObject value, string name) =>
        value[name]?.GetValue<string>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static int Int32(JsonObject value, string name) =>
        value[name]?.GetValue<int>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static long Int64(JsonObject value, string name) =>
        value[name]?.GetValue<long>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static bool True(JsonObject value, string name) =>
        value[name]?.GetValue<bool>() == true;
    private static bool False(JsonObject value, string name) =>
        value[name]?.GetValue<bool>() == false;
    private static DateTimeOffset Date(JsonObject value, string name) =>
        DateTimeOffset.Parse(Text(value, name), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    private static string NormalizeRelative(string value) => value.Replace('\\', '/');
    private static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void RequireSha(string? value, string code, int length = 64) =>
        Require(value is not null && value.Length == length &&
                value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character)),
            code);
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bRequiredInstrument(
    Guid InstrumentId,
    string SecurityId,
    string Symbol,
    string LmaxInstrumentId,
    string MappingSha256);

public sealed record Arch7bMappingCardinalities(
    int DistinctSymbols,
    int DistinctLmaxInstrumentIds,
    int SymbolCollisionCount,
    int LmaxInstrumentIdCollisionCount,
    string CollisionContract);

public sealed record Arch7bRequiredPmsUniverse(
    Guid SourceIngestionId,
    string SourceSessionId,
    Guid SourceAccountSnapshotId,
    decimal NavUsd,
    DateTimeOffset IngestionCompletedAtUtc,
    IReadOnlyList<PmsShadowQubesInputSnapshotRow> QubesInputs,
    DateTimeOffset EarliestModelAsOfUtc,
    DateTimeOffset LatestModelAsOfUtc,
    DateTimeOffset EarliestTargetCloseUtc,
    DateTimeOffset LatestTargetCloseUtc,
    IReadOnlyList<PmsShadowEconomicModel> Models,
    IReadOnlyList<PmsShadowEconomicMapping> Mappings,
    IReadOnlyList<Arch7bRequiredInstrument> Instruments,
    Arch7bMappingCardinalities MappingCardinalities,
    IReadOnlyDictionary<string, int> StrategyCounts,
    string RequiredUniverseSha256,
    string SourceSelectionAuthority,
    string TargetProfile,
    string TargetFingerprint,
    bool TransactionReadOnly,
    bool PendingModelChanges,
    bool NoDatabaseWrite);

public static class Arch7bRequiredPmsUniverseBuilder
{
    public static Arch7bRequiredPmsUniverse Build(
        PmsShadowIngestionRow ingestion,
        PmsShadowAccountSnapshotRow account,
        IReadOnlyList<PmsShadowModelRunRow> modelRows,
        IReadOnlyList<PmsShadowQubesInputSnapshotRow> qubesRows,
        IReadOnlyList<PmsShadowTargetWeightRow> weightRows,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappingRows,
        string targetProfile,
        string targetFingerprint,
        bool transactionReadOnly,
        bool pendingModelChanges)
    {
        ArgumentNullException.ThrowIfNull(ingestion);
        Require(ingestion.Status == PmsShadowIngestionStatuses.Completed &&
                ingestion.CompletedAtUtc is not null,
            "ARCH7B_PMS_INGESTION_NOT_QUALIFIED");
        Require(account.IngestionId == ingestion.IngestionId,
            "ARCH7B_PMS_ACCOUNT_LINEAGE_MISMATCH");
        Require(account.NavOrEquity > 0, "ARCH7B_PMS_NAV_INVALID");
        Require(transactionReadOnly, "ARCH7B_PMS_TRANSACTION_NOT_READ_ONLY");
        Require(!pendingModelChanges, "ARCH7B_PMS_PENDING_MODEL_CHANGES");

        var models = modelRows.OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();
        Require(models.Length == Arch7bBracketedGlobalFlatContract.RequiredStrategies.Count &&
                models.Select(value => value.StrategyId)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(Arch7bBracketedGlobalFlatContract.RequiredStrategies),
            "ARCH7B_LATEST_COMPLETED_PMS_INGESTION_INVALID");
        Require(models.All(value => value.ModelRunId != Guid.Empty &&
                                    value.QubesInputSnapshotId != Guid.Empty &&
                                    value.IngestionId == ingestion.IngestionId &&
                                    value.TargetCloseUtc.Offset == TimeSpan.Zero &&
                                    value.AsOfUtc.Offset == TimeSpan.Zero &&
                                    value.NotAnOrder &&
                                    !value.ExecutionAllowed &&
                                    !value.AccountingEligible &&
                                    Arch5bHashing.IsSha256(value.OutputSha256) &&
                                    GitCommitIdentityContract.IsValid(
                                        value.CoreMasterCommitId, value.CoreMasterObjectFormat)),
            "ARCH7B_PMS_MODEL_LINEAGE_INVALID");

        var qubesInputs = qubesRows
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ToArray();
        Require(qubesInputs.Length == models.Length &&
                qubesInputs.Select(value => value.SnapshotId).Distinct().Count() ==
                models.Length,
            "ARCH7B_PMS_QUBES_INPUT_MISSING");
        var qubesById = qubesInputs.ToDictionary(value => value.SnapshotId);
        Require(qubesInputs.All(value =>
                    value.SnapshotId != Guid.Empty &&
                    value.IngestionId == ingestion.IngestionId &&
                    value.TargetCloseUtc.Offset == TimeSpan.Zero &&
                    !string.IsNullOrWhiteSpace(value.StrategyId) &&
                    Arch5bHashing.IsSha256(value.InputSha256) &&
                    Arch5bHashing.IsSha256(value.SourceSnapshotSha256) &&
                    Arch5bHashing.IsSha256(value.OverlaySha256) &&
                    Arch5bHashing.IsSha256(value.MappingSha256) &&
                    (value.GapLedgerSha256 is null ||
                     Arch5bHashing.IsSha256(value.GapLedgerSha256))),
            "ARCH7B_PMS_QUBES_INPUT_LINEAGE_MISMATCH");
        Require(models.All(value =>
                    qubesById.TryGetValue(value.QubesInputSnapshotId, out var input) &&
                    input.IngestionId == value.IngestionId &&
                    input.StrategyId == value.StrategyId),
            "ARCH7B_PMS_QUBES_INPUT_LINEAGE_MISMATCH");
        Require(models.All(value =>
                    qubesById[value.QubesInputSnapshotId].TargetCloseUtc ==
                    value.TargetCloseUtc),
            "ARCH7B_PMS_TARGET_CLOSE_LINEAGE_MISMATCH");

        var modelIds = models.Select(value => value.ModelRunId).ToHashSet();
        var weights = weightRows
            .OrderBy(value => value.ModelRunId)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal)
            .ToArray();
        Require(weights.All(value => modelIds.Contains(value.ModelRunId)),
            "ARCH7B_PMS_UNEXPECTED_MODEL_WEIGHT");
        var modelById = models.ToDictionary(value => value.ModelRunId);
        Require(weights.All(value =>
                    value.TargetCloseUtc == modelById[value.ModelRunId].TargetCloseUtc &&
                    value.OutputSha256 == modelById[value.ModelRunId].OutputSha256),
            "ARCH7B_PMS_TARGET_CLOSE_LINEAGE_MISMATCH");
        Require(weights.GroupBy(value => (value.ModelRunId, value.InstrumentId))
                .All(group => group.Count() == 1),
            "ARCH7B_PMS_DUPLICATE_INSTRUMENT_ID");
        var counts = models.ToDictionary(
            value => value.StrategyId,
            value => weights.Count(weight => weight.ModelRunId == value.ModelRunId),
            StringComparer.Ordinal);
        Require(counts.Count == Arch7bBracketedGlobalFlatContract.RequiredStrategyCounts.Count &&
                counts.All(item =>
                    Arch7bBracketedGlobalFlatContract.RequiredStrategyCounts[item.Key] == item.Value),
            "ARCH7B_PMS_MODEL_WEIGHT_COUNTS_MISMATCH");

        var mappings = mappingRows
            .OrderBy(value => value.InstrumentId)
            .ToArray();
        Require(mappings.All(value =>
                    value.IngestionId == ingestion.IngestionId &&
                    value.InstrumentId != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(value.SecurityId) &&
                    Regex.IsMatch(value.Symbol, "^[A-Z]{6}$",
                        RegexOptions.CultureInvariant) &&
                    !string.IsNullOrWhiteSpace(value.LmaxInstrumentId) &&
                    Arch5bHashing.IsSha256(value.MappingSha256)),
            "ARCH7B_PMS_SECURITY_MAPPING_IDENTITY_MISMATCH");
        Require(mappings.GroupBy(value => value.InstrumentId).All(group => group.Count() == 1),
            "ARCH7B_PMS_DUPLICATE_INSTRUMENT_ID");
        Require(mappings.GroupBy(value => value.SecurityId, StringComparer.Ordinal)
                .All(group => group.Count() == 1),
            "ARCH7B_PMS_SECURITY_MAPPING_COLLISION");
        var mappingByInstrument = mappings.ToDictionary(value => value.InstrumentId);
        var requiredIds = weights.Select(value => value.InstrumentId)
            .Distinct().Order().ToArray();
        Require(requiredIds.Length == 99, "ARCH7B_PMS_REQUIRED_INSTRUMENT_COUNT_MISMATCH");
        Require(requiredIds.All(mappingByInstrument.ContainsKey),
            "ARCH7B_PMS_SECURITY_MAPPING_MISSING");
        Require(weights.All(value => !string.IsNullOrWhiteSpace(value.SecurityId) &&
                                     mappingByInstrument[value.InstrumentId].SecurityId ==
                                     value.SecurityId),
            "ARCH7B_PMS_SECURITY_MAPPING_IDENTITY_MISMATCH");

        var instruments = requiredIds.Select(id =>
        {
            var mapping = mappingByInstrument[id];
            return new Arch7bRequiredInstrument(mapping.InstrumentId, mapping.SecurityId,
                mapping.Symbol, mapping.LmaxInstrumentId, mapping.MappingSha256);
        }).ToArray();
        var mappingCardinalities = new Arch7bMappingCardinalities(
            instruments.Select(value => value.Symbol)
                .Distinct(StringComparer.Ordinal).Count(),
            instruments.Select(value => value.LmaxInstrumentId)
                .Distinct(StringComparer.Ordinal).Count(),
            instruments.GroupBy(value => value.Symbol, StringComparer.Ordinal)
                .Sum(group => Math.Max(0, group.Count() - 1)),
            instruments.GroupBy(value => value.LmaxInstrumentId, StringComparer.Ordinal)
                .Sum(group => Math.Max(0, group.Count() - 1)),
            "SYMBOL_AND_LMAX_IDENTITIES_MAY_BE_REUSED_COLLISIONS_INVENTORIED_V1");
        var universeSha = Arch5bHashing.HashCanonical(new
        {
            ContractVersion = Arch7bBracketedGlobalFlatContract.Version,
            ingestion.IngestionId,
            ingestion.SourceSessionId,
            SourceSelectionAuthority =
                Arch7bBracketedGlobalFlatContract.SourceSelectionAuthority,
            Models = models.Select(value => new
            {
                value.StrategyId,
                value.ModelRunId,
                value.QubesInputSnapshotId,
                value.TargetCloseUtc,
                value.AsOfUtc,
                value.OutputSha256,
                value.CoreMasterCommitId
            }),
            QubesInputs = qubesInputs.Select(value => new
            {
                value.SnapshotId,
                value.IngestionId,
                value.StrategyId,
                value.TargetCloseUtc,
                value.InputSha256,
                value.MappingSha256
            }),
            StrategyCounts = counts.OrderBy(value => value.Key, StringComparer.Ordinal),
            Instruments = instruments,
            MappingCardinalities = mappingCardinalities
        });
        var economicMappings = instruments.Select(value =>
        {
            var mapping = mappingByInstrument[value.InstrumentId];
            return new PmsShadowEconomicMapping(mapping.InstrumentId, mapping.VenueId,
                mapping.VenueInstrumentId, mapping.SecurityId, mapping.Symbol,
                mapping.LmaxInstrumentId, mapping.QuantityMultiplier,
                mapping.QuantityIncrement, mapping.PriceIncrement);
        }).ToArray();
        var weightsByModel = weights.GroupBy(value => value.ModelRunId)
            .ToDictionary(group => group.Key, group => group.Select(value =>
                new PmsShadowEconomicWeight(value.InstrumentId, value.SecurityId, value.Weight))
                .OrderBy(value => value.SecurityId, StringComparer.Ordinal).ToArray());
        var economicModels = models.Select(value => new PmsShadowEconomicModel(
            value.ModelRunId, value.QubesInputSnapshotId, value.StrategyId,
            value.TargetCloseUtc, value.AsOfUtc, value.OutputSha256,
            value.CoreMasterCommitId, weightsByModel[value.ModelRunId])).ToArray();
        return new(
            ingestion.IngestionId,
            ingestion.SourceSessionId,
            account.AccountSnapshotId,
            account.NavOrEquity,
            ingestion.CompletedAtUtc
                ?? throw new InvalidDataException("ARCH7B_PMS_INGESTION_COMPLETION_MISSING"),
            qubesInputs,
            models.Min(value => value.AsOfUtc),
            models.Max(value => value.AsOfUtc),
            models.Min(value => value.TargetCloseUtc),
            models.Max(value => value.TargetCloseUtc),
            economicModels,
            economicMappings,
            instruments,
            mappingCardinalities,
            counts,
            universeSha,
            Arch7bBracketedGlobalFlatContract.SourceSelectionAuthority,
            targetProfile,
            targetFingerprint,
            transactionReadOnly,
            pendingModelChanges,
            true);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed class Arch7bRequiredPmsUniverseReader(
    DbContextOptions<PmsShadowDbContext> options,
    PmsShadowPostgreSqlTarget target)
{
    public async Task<Arch7bRequiredPmsUniverse> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = new PmsShadowDbContext(options);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction,
                "SET TRANSACTION READ ONLY", cancellationToken);
            var transactionReadOnly = string.Equals(
                await ScalarStringAsync(connection, transaction,
                    "SHOW transaction_read_only", cancellationToken),
                "on", StringComparison.OrdinalIgnoreCase);
            Require(transactionReadOnly, "ARCH7B_PMS_TRANSACTION_NOT_READ_ONLY");
            Require(await ScalarStringAsync(connection, transaction,
                    "SELECT current_database()", cancellationToken) ==
                    Arch7bBracketedGlobalFlatContract.TargetDatabase,
                "ARCH7B_PMS_DATABASE_IDENTITY_MISMATCH");
            var major = int.Parse(await ScalarStringAsync(connection,
                    transaction,
                    "SELECT current_setting('server_version_num')", cancellationToken),
                CultureInfo.InvariantCulture) / 10000;
            Require(major == Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
                "ARCH7B_PMS_POSTGRESQL_MAJOR_MISMATCH");
            var pending = context.Database.HasPendingModelChanges();
            Require(!pending, "ARCH7B_PMS_PENDING_MODEL_CHANGES");

            var ingestion = await context.Ingestions.AsNoTracking()
                .Where(value => value.Status == PmsShadowIngestionStatuses.Completed &&
                                value.CompletedAtUtc != null)
                .OrderByDescending(value => value.CompletedAtUtc)
                .ThenByDescending(value => value.IngestionId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException("ARCH7B_PMS_QUALIFIED_INGESTION_MISSING");
            var account = await context.AccountSnapshots.AsNoTracking()
                .SingleAsync(value => value.IngestionId == ingestion.IngestionId,
                    cancellationToken);
            var models = await context.ModelRuns.AsNoTracking()
                .Where(value => value.IngestionId == ingestion.IngestionId)
                .ToArrayAsync(cancellationToken);
            var modelIds = models.Select(value => value.ModelRunId).ToArray();
            var qubesIds = models.Select(value => value.QubesInputSnapshotId).ToArray();
            var qubesInputs = await context.QubesInputSnapshots.AsNoTracking()
                .Where(value => qubesIds.Contains(value.SnapshotId))
                .ToArrayAsync(cancellationToken);
            var weights = await context.TargetWeights.AsNoTracking()
                .Where(value => modelIds.Contains(value.ModelRunId))
                .ToArrayAsync(cancellationToken);
            var mappings = await context.SecurityMappings.AsNoTracking()
                .Where(value => value.IngestionId == ingestion.IngestionId)
                .ToArrayAsync(cancellationToken);
            var result = Arch7bRequiredPmsUniverseBuilder.Build(
                ingestion, account, models, qubesInputs, weights, mappings,
                target.TargetProfileId, target.TargetFingerprint,
                transactionReadOnly, pending);
            await transaction.RollbackAsync(cancellationToken);
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

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ScalarStringAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken),
                   CultureInfo.InvariantCulture)
               ?? throw new InvalidDataException("ARCH7B_PMS_DATABASE_VALUE_MISSING");
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bNormalizedPositionLine(
    Guid PositionSnapshotLineId,
    Guid PositionSnapshotId,
    Guid InstrumentId,
    string SecurityId,
    string Symbol,
    string LmaxInstrumentId,
    string MappingSha256,
    Guid SourceIngestionId,
    string PmsSourceSessionId,
    decimal CurrentBaseQuantity,
    string ProvenanceKind,
    string PositionAuthorityCode,
    string AccountId,
    int BrokerPositionCount,
    string BracketEvidenceSha256,
    string RequiredUniverseSha256,
    string CoreRepositoryCommit,
    DateTimeOffset PositionSnapshotAsOfUtc,
    string EvidenceSha256);

public sealed record Arch7bPmsGlobalFlatPositionSnapshot(
    string ContractVersion,
    Guid AccountSnapshotId,
    Guid PositionSnapshotId,
    string AccountId,
    string Environment,
    string CoreRepositoryCommit,
    string CoreContractVersion,
    string BracketEvidenceSha256,
    DateTimeOffset BracketLowerBoundUtc,
    DateTimeOffset PositionReportP2Utc,
    DateTimeOffset BracketUpperBoundUtc,
    DateTimeOffset PositionSnapshotAsOfUtc,
    DateTimeOffset PmsSourceIngestionCompletedAtUtc,
    DateTimeOffset LatestModelAsOfUtc,
    DateTimeOffset LatestTargetCloseUtc,
    bool BrokerSnapshotAfterIngestion,
    string TemporalLineageStatus,
    string TemporalLineageContractVersion,
    string TargetCloseTemporalContract,
    string ImportEligibility,
    string ImportFreshnessStatus,
    Arch7bMappingCardinalities MappingCardinalities,
    int RawBrokerPositionCount,
    int RequiredInstrumentCount,
    int NormalizedLineCount,
    int DerivedZeroCount,
    int UnknownCount,
    string RequiredUniverseSha256,
    string NormalizedLineSetSha256,
    string PositionAuthorityCode,
    string WorkingOrderAuthority,
    string WorkingOrderBlocker,
    bool BrokerSendAllowed,
    bool NoOrder,
    bool NoFix,
    bool NoDatabaseWrite,
    IReadOnlyList<Arch7bNormalizedPositionLine> Lines,
    string EvidenceSha256);

public static class Arch7bGlobalFlatPositionSnapshotBuilder
{
    public static Arch7bPmsGlobalFlatPositionSnapshot Build(
        Arch7bCoreBracketEvidence core,
        Arch7bRequiredPmsUniverse universe)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(universe);
        Arch7bCoreBracketEvidencePackageReader.ValidateSemanticContract(core);
        if (core.PositionCount != 0)
            throw new InvalidDataException(
                Arch7bBracketedGlobalFlatContract.NonzeroPositionBlocker);
        Require(universe.Instruments.Count == 99,
            "ARCH7B_PMS_REQUIRED_INSTRUMENT_COUNT_MISMATCH");
        Require(universe.TransactionReadOnly && universe.NoDatabaseWrite &&
                !universe.PendingModelChanges,
            "ARCH7B_PMS_READ_ONLY_AUTHORITY_INVALID");
        Require(core.PositionReportP2Utc >= universe.IngestionCompletedAtUtc &&
                core.BracketUpperBoundUtc >= universe.IngestionCompletedAtUtc &&
                core.PositionReportP2Utc >= universe.LatestModelAsOfUtc,
            "ARCH7B_BROKER_POSITION_SNAPSHOT_PREDATES_PMS_SOURCE");
        var temporalLineageStatus =
            "PROVEN_BROKER_SNAPSHOT_AFTER_PMS_INGESTION_AND_MODEL_ASOF";

        var identity = string.Join(':',
            Arch7bBracketedGlobalFlatContract.Version,
            core.Environment,
            core.AccountId,
            core.CoreContractVersion,
            core.CoreRepositoryCommit,
            core.EvidenceSha256,
            core.PositionReportP2Utc.ToString("O", CultureInfo.InvariantCulture),
            universe.RequiredUniverseSha256);
        var accountSnapshotId = Arch5bHashing.GuidFromSha256(
            $"arch7b:account-snapshot:{identity}");
        var positionSnapshotId = Arch5bHashing.GuidFromSha256(
            $"arch7b:position-snapshot:{identity}");
        var lines = universe.Instruments.OrderBy(value => value.InstrumentId)
            .Select(value =>
            {
                var lineId = Arch5bHashing.GuidFromSha256(
                    $"arch7b:position-line:{identity}:{value.InstrumentId:D}:" +
                    $"{value.LmaxInstrumentId}:{value.MappingSha256}:" +
                    $"{universe.SourceIngestionId:D}:{universe.SourceSessionId}");
                var lineEvidence = Arch5bHashing.HashCanonical(new
                {
                    PositionSnapshotLineId = lineId,
                    PositionSnapshotId = positionSnapshotId,
                    value.InstrumentId,
                    value.SecurityId,
                    value.Symbol,
                    value.LmaxInstrumentId,
                    value.MappingSha256,
                    SourceIngestionId = universe.SourceIngestionId,
                    PmsSourceSessionId = universe.SourceSessionId,
                    CurrentBaseQuantity = 0m,
                    ProvenanceKind = Arch7bBracketedGlobalFlatContract.ProvenanceKind,
                    PositionAuthorityCode =
                        Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
                    core.AccountId,
                    BrokerPositionCount = core.PositionCount,
                    BracketEvidenceSha256 = core.EvidenceSha256,
                    universe.RequiredUniverseSha256,
                    core.CoreRepositoryCommit,
                    PositionSnapshotAsOfUtc = core.PositionReportP2Utc
                });
                return new Arch7bNormalizedPositionLine(
                    lineId, positionSnapshotId, value.InstrumentId, value.SecurityId,
                    value.Symbol, value.LmaxInstrumentId, value.MappingSha256,
                    universe.SourceIngestionId, universe.SourceSessionId, 0m,
                    Arch7bBracketedGlobalFlatContract.ProvenanceKind,
                    Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
                    core.AccountId, core.PositionCount, core.EvidenceSha256,
                    universe.RequiredUniverseSha256, core.CoreRepositoryCommit,
                    core.PositionReportP2Utc, lineEvidence);
            }).ToArray();
        var lineSetSha = Arch5bHashing.HashCanonical(lines);
        var snapshotCore = new
        {
            ContractVersion = Arch7bBracketedGlobalFlatContract.Version,
            AccountSnapshotId = accountSnapshotId,
            PositionSnapshotId = positionSnapshotId,
            core.AccountId,
            core.Environment,
            core.CoreRepositoryCommit,
            core.CoreContractVersion,
            BracketEvidenceSha256 = core.EvidenceSha256,
            BracketLowerBoundUtc = core.BracketLowerBoundUtc,
            PositionReportP2Utc = core.PositionReportP2Utc,
            BracketUpperBoundUtc = core.BracketUpperBoundUtc,
            PositionSnapshotAsOfUtc = core.PositionReportP2Utc,
            PmsSourceIngestionCompletedAtUtc = universe.IngestionCompletedAtUtc,
            LatestModelAsOfUtc = universe.LatestModelAsOfUtc,
            LatestTargetCloseUtc = universe.LatestTargetCloseUtc,
            BrokerSnapshotAfterIngestion = true,
            TemporalLineageStatus = temporalLineageStatus,
            TemporalLineageContractVersion =
                Arch7bBracketedGlobalFlatContract.TemporalLineageContractVersion,
            TargetCloseTemporalContract =
                Arch7bBracketedGlobalFlatContract.TargetCloseTemporalContract,
            ImportEligibility = Arch7bBracketedGlobalFlatContract.ImportEligibility,
            ImportFreshnessStatus =
                Arch7bBracketedGlobalFlatContract.ImportFreshnessStatus,
            universe.MappingCardinalities,
            RawBrokerPositionCount = core.PositionCount,
            RequiredInstrumentCount = universe.Instruments.Count,
            NormalizedLineCount = lines.Length,
            DerivedZeroCount = lines.Count(value => value.CurrentBaseQuantity == 0),
            UnknownCount = 0,
            universe.RequiredUniverseSha256,
            NormalizedLineSetSha256 = lineSetSha,
            PositionAuthorityCode = Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
            WorkingOrderAuthority = Arch7bBracketedGlobalFlatContract.WorkingOrderAuthority,
            WorkingOrderBlocker = Arch7bBracketedGlobalFlatContract.WorkingOrderBlocker,
            BrokerSendAllowed = false,
            NoOrder = true,
            NoFix = true,
            NoDatabaseWrite = true,
            Lines = lines
        };
        var evidenceSha = Arch5bHashing.HashCanonical(snapshotCore);
        return new(
            snapshotCore.ContractVersion,
            accountSnapshotId,
            positionSnapshotId,
            snapshotCore.AccountId,
            snapshotCore.Environment,
            snapshotCore.CoreRepositoryCommit,
            snapshotCore.CoreContractVersion,
            snapshotCore.BracketEvidenceSha256,
            snapshotCore.BracketLowerBoundUtc,
            snapshotCore.PositionReportP2Utc,
            snapshotCore.BracketUpperBoundUtc,
            snapshotCore.PositionSnapshotAsOfUtc,
            snapshotCore.PmsSourceIngestionCompletedAtUtc,
            snapshotCore.LatestModelAsOfUtc,
            snapshotCore.LatestTargetCloseUtc,
            snapshotCore.BrokerSnapshotAfterIngestion,
            snapshotCore.TemporalLineageStatus,
            snapshotCore.TemporalLineageContractVersion,
            snapshotCore.TargetCloseTemporalContract,
            snapshotCore.ImportEligibility,
            snapshotCore.ImportFreshnessStatus,
            snapshotCore.MappingCardinalities,
            snapshotCore.RawBrokerPositionCount,
            snapshotCore.RequiredInstrumentCount,
            snapshotCore.NormalizedLineCount,
            snapshotCore.DerivedZeroCount,
            snapshotCore.UnknownCount,
            snapshotCore.RequiredUniverseSha256,
            snapshotCore.NormalizedLineSetSha256,
            snapshotCore.PositionAuthorityCode,
            snapshotCore.WorkingOrderAuthority,
            snapshotCore.WorkingOrderBlocker,
            snapshotCore.BrokerSendAllowed,
            snapshotCore.NoOrder,
            snapshotCore.NoFix,
            snapshotCore.NoDatabaseWrite,
            lines,
            evidenceSha);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bGlobalFlatEconomicSmoke(
    string ContractVersion,
    string InputSnapshotEvidenceSha256,
    string RequiredUniverseSha256,
    string SlotId,
    DateTimeOffset PositionSnapshotAsOfUtc,
    DateTimeOffset MarketObservationAsOfUtc,
    int ObservationCount,
    int TargetPositionCount,
    int PositionOnlyDriftCount,
    IReadOnlyDictionary<string, int> StrategyCounts,
    int ZeroCurrentQuantityCount,
    int ExactDeltaCount,
    string ProjectionIntegrityStatus,
    string ProjectionIntegrityEvidenceSha256,
    string ProjectionManifestSha256,
    bool NoOrder,
    bool NoFix,
    bool NoDatabaseWrite,
    string EvidenceSha256);

public static class Arch7bGlobalFlatEconomicSmokeRunner
{
    public const string ContractVersion =
        "arch7b_bracketed_global_flat_offline_economic_smoke_v1";

    public static Arch7bGlobalFlatEconomicSmoke Run(
        Arch7bPmsGlobalFlatPositionSnapshot snapshot,
        Arch7bRequiredPmsUniverse universe)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(universe);
        var end = PmsShadowIntradayCadenceContract.Floor(
            snapshot.PositionSnapshotAsOfUtc.AddMinutes(30));
        if (end <= snapshot.PositionSnapshotAsOfUtc) end = end.AddMinutes(15);
        var window = PmsShadowIntradayCadenceContract.WindowEnding(end);
        var currencies = universe.Mappings
            .SelectMany(value =>
            {
                var pair = Pair(value.Symbol);
                return new[] { pair.Base, pair.Quote };
            })
            .Where(value => value != "USD")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var bbo = currencies.Select((currency, index) =>
        {
            var bid = decimal.Round(1m + (index + 1) / 100m, 6);
            return new PmsShadowRealSlotBbo(
                currency + "USD",
                "offline-" + currency.ToLowerInvariant() + "-usd",
                bid,
                bid + 0.0001m,
                end.AddSeconds(-2),
                end.AddSeconds(-1));
        }).ToArray();
        var captureSha = Arch5bHashing.HashCanonical(new
        {
            ContractVersion,
            snapshot.EvidenceSha256,
            universe.RequiredUniverseSha256,
            window.SlotId,
            Bbo = bbo
        });
        var capture = new PmsShadowRealSlotCapture(
            window.SlotId, window.SlotStartUtc, window.SlotEndUtc,
            "arch7b-offline-smoke", "content-addressed-offline-fixture",
            captureSha, bbo, true, 0, true, true);
        var source = new PmsShadowEconomicSource(
            universe.SourceIngestionId,
            universe.SourceSessionId,
            snapshot.AccountSnapshotId,
            universe.NavUsd,
            snapshot.PositionSnapshotId,
            snapshot.PositionSnapshotAsOfUtc,
            snapshot.PositionAuthorityCode,
            snapshot.Lines.ToDictionary(value => value.InstrumentId,
                value => value.CurrentBaseQuantity),
            universe.Mappings,
            universe.Models);
        var projection = new PmsShadowIntradayEconomicProjectionBuilder()
            .Build(capture, source, null);
        var integrity = PmsShadowEconomicProjectionIntegrityVerifier.Verify(projection);
        Require(integrity.Status == PmsShadowEconomicProjectionIntegrityVerifier.Proven,
            "ARCH7B_OFFLINE_PROJECTION_INTEGRITY_NOT_PROVEN");
        var strategyCounts = projection.TargetPositions
            .GroupBy(value => value.StrategyId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(projection.MarketData.Count == 99, "ARCH7B_OFFLINE_OBSERVATION_COUNT_MISMATCH");
        Require(projection.TargetPositions.Count == 288,
            "ARCH7B_OFFLINE_TARGET_COUNT_MISMATCH");
        Require(projection.PositionOnlyDrifts.Count == 288,
            "ARCH7B_OFFLINE_DRIFT_COUNT_MISMATCH");
        Require(strategyCounts.Count == 4 &&
                strategyCounts.All(item =>
                    Arch7bBracketedGlobalFlatContract.RequiredStrategyCounts[item.Key] ==
                    item.Value),
            "ARCH7B_OFFLINE_STRATEGY_COUNTS_MISMATCH");
        Require(projection.PositionOnlyDrifts.All(value => value.CurrentBaseQuantity == 0m),
            "ARCH7B_OFFLINE_CURRENT_QUANTITY_NONZERO");
        Require(projection.PositionOnlyDrifts.All(value =>
                value.Delta == value.TargetBaseQuantity),
            "ARCH7B_OFFLINE_DELTA_MISMATCH");
        Require(projection.NoOrder, "ARCH7B_OFFLINE_NO_ORDER_REGRESSION");
        var core = new
        {
            ContractVersion,
            InputSnapshotEvidenceSha256 = snapshot.EvidenceSha256,
            universe.RequiredUniverseSha256,
            projection.SlotId,
            snapshot.PositionSnapshotAsOfUtc,
            MarketObservationAsOfUtc = projection.MarketData.Max(value => value.EventTimeUtc),
            ObservationCount = projection.MarketData.Count,
            TargetPositionCount = projection.TargetPositions.Count,
            PositionOnlyDriftCount = projection.PositionOnlyDrifts.Count,
            StrategyCounts = strategyCounts.OrderBy(value => value.Key, StringComparer.Ordinal),
            ZeroCurrentQuantityCount = projection.PositionOnlyDrifts.Count(value =>
                value.CurrentBaseQuantity == 0m),
            ExactDeltaCount = projection.PositionOnlyDrifts.Count(value =>
                value.Delta == value.TargetBaseQuantity),
            ProjectionIntegrityStatus = integrity.Status,
            ProjectionIntegrityEvidenceSha256 = integrity.EvidenceSha256,
            ProjectionManifestSha256 = projection.ManifestSha256,
            NoOrder = true,
            NoFix = true,
            NoDatabaseWrite = true
        };
        return new(
            core.ContractVersion,
            core.InputSnapshotEvidenceSha256,
            core.RequiredUniverseSha256,
            core.SlotId,
            core.PositionSnapshotAsOfUtc,
            core.MarketObservationAsOfUtc,
            core.ObservationCount,
            core.TargetPositionCount,
            core.PositionOnlyDriftCount,
            strategyCounts,
            core.ZeroCurrentQuantityCount,
            core.ExactDeltaCount,
            core.ProjectionIntegrityStatus,
            core.ProjectionIntegrityEvidenceSha256,
            core.ProjectionManifestSha256,
            core.NoOrder,
            core.NoFix,
            core.NoDatabaseWrite,
            Arch5bHashing.HashCanonical(core));
    }

    private static (string Base, string Quote) Pair(string symbol)
    {
        var value = new string(symbol.ToUpperInvariant()
            .Where(char.IsAsciiLetterUpper).ToArray());
        if (value.Length != 6)
            throw new InvalidDataException($"ARCH7B_OFFLINE_FX_SYMBOL_INVALID:{symbol}");
        return (value[..3], value[3..]);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bGlobalFlatOutputBundle(
    string OutputDirectory,
    string ManifestSha256,
    IReadOnlyDictionary<string, string> FileSha256);

public static class Arch7bGlobalFlatOutputWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static byte[] SerializeSmoke(Arch7bGlobalFlatEconomicSmoke smoke) =>
        JsonSerializer.SerializeToUtf8Bytes(smoke, Json);

    public static Arch7bGlobalFlatOutputBundle Write(
        string outputDirectory,
        Arch7bCoreBracketEvidence core,
        Arch7bRequiredPmsUniverse universe,
        Arch7bPmsGlobalFlatPositionSnapshot snapshot,
        Arch7bGlobalFlatEconomicSmoke smokeA,
        Arch7bGlobalFlatEconomicSmoke smokeB)
    {
        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new InvalidDataException("ARCH7B_OUTPUT_DIRECTORY_ALREADY_EXISTS");
        var parent = Path.GetDirectoryName(root)
            ?? throw new InvalidDataException("ARCH7B_OUTPUT_PARENT_INVALID");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(root);
        try
        {
            WriteJson(root, "core-bracket-evidence-validation.json", new
            {
                ContractVersion = Arch7bBracketedGlobalFlatContract.Version,
                Status = "PROVEN",
                Core = core,
                NoOrder = true,
                NoFix = true,
                NoDatabaseWrite = true,
                NoAccountApi = true,
                NoDatabento = true
            });
            WriteJson(root, "required-pms-universe.json", universe);
            WriteCsv(root, snapshot.Lines);
            WriteJson(root, "pms-bracketed-global-flat-position-snapshot.json", snapshot);
            File.WriteAllBytes(Path.Combine(root, "offline-smoke-run-a.json"),
                SerializeSmoke(smokeA));
            File.WriteAllBytes(Path.Combine(root, "offline-smoke-run-b.json"),
                SerializeSmoke(smokeB));
            Require(File.ReadAllBytes(Path.Combine(root, "offline-smoke-run-a.json"))
                    .SequenceEqual(File.ReadAllBytes(
                        Path.Combine(root, "offline-smoke-run-b.json"))),
                "ARCH7B_OFFLINE_SMOKE_NONDETERMINISTIC");
            File.WriteAllText(Path.Combine(root, "report.md"), Report(core, universe,
                snapshot, smokeA), new UTF8Encoding(false));

            var files = Directory.EnumerateFiles(root)
                .Order(StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetFileName(path),
                    path => FileSha(path),
                    StringComparer.Ordinal);
            var manifest = new
            {
                ContractVersion = Arch7bBracketedGlobalFlatContract.Version,
                CoreRepositoryCommit = core.CoreRepositoryCommit,
                core.DownloaderVersion,
                DownloaderCompatibilityContract =
                    core.DownloaderCompatibility?.ContractVersion,
                DownloaderCompatibilityProfile = core.DownloaderCompatibility?.Profile,
                BracketEvidenceSha256 = core.EvidenceSha256,
                SuccessfulAttemptNumber =
                    core.RecomputedSemantics?.SuccessfulAttemptNumber,
                RecomputedExecutionReports =
                    core.RecomputedSemantics?.ExecutionReports,
                RecomputedPositionReports =
                    core.RecomputedSemantics?.PositionReports,
                universe.RequiredUniverseSha256,
                snapshot.NormalizedLineSetSha256,
                snapshot.AccountSnapshotId,
                snapshot.PositionSnapshotId,
                snapshot.PositionSnapshotAsOfUtc,
                snapshot.PmsSourceIngestionCompletedAtUtc,
                snapshot.LatestModelAsOfUtc,
                snapshot.LatestTargetCloseUtc,
                snapshot.BrokerSnapshotAfterIngestion,
                snapshot.TemporalLineageStatus,
                snapshot.TemporalLineageContractVersion,
                snapshot.TargetCloseTemporalContract,
                snapshot.ImportEligibility,
                snapshot.ImportFreshnessStatus,
                snapshot.MappingCardinalities,
                WorkingOrderAuthority =
                    Arch7bBracketedGlobalFlatContract.WorkingOrderAuthority,
                WorkingOrderBlocker =
                    Arch7bBracketedGlobalFlatContract.WorkingOrderBlocker,
                BrokerSendAllowed = false,
                SmokeOutputsByteForByteIdentical = true,
                Files = files,
                NoOrder = true,
                NoFix = true,
                NoDatabaseWrite = true,
                NoFill = true,
                NoLedgerWrite = true,
                NoAccountApi = true,
                NoDatabento = true
            };
            WriteJson(root, "manifest.json", manifest);
            files = Directory.EnumerateFiles(root)
                .Order(StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetFileName(path),
                    path => FileSha(path),
                    StringComparer.Ordinal);
            return new(root, files["manifest.json"], files);
        }
        catch
        {
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static void WriteJson(string root, string name, object value) =>
        File.WriteAllBytes(Path.Combine(root, name),
            JsonSerializer.SerializeToUtf8Bytes(value, Json));

    private static void WriteCsv(
        string root,
        IReadOnlyList<Arch7bNormalizedPositionLine> lines)
    {
        var path = Path.Combine(root, "normalized-position-lines.csv");
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine(
            "position_snapshot_line_id,position_snapshot_id,instrument_id,security_id,symbol,lmax_instrument_id,mapping_sha256,source_ingestion_id,pms_source_session_id,current_base_quantity,provenance_kind,position_authority_code,account_id,broker_position_count,bracket_evidence_sha256,required_universe_sha256,core_repository_commit,position_snapshot_as_of_utc,evidence_sha256");
        foreach (var value in lines.OrderBy(value => value.InstrumentId))
            writer.WriteLine(string.Join(',',
                value.PositionSnapshotLineId.ToString("D"),
                value.PositionSnapshotId.ToString("D"),
                value.InstrumentId.ToString("D"),
                Csv(value.SecurityId),
                Csv(value.Symbol),
                Csv(value.LmaxInstrumentId),
                value.MappingSha256,
                value.SourceIngestionId.ToString("D"),
                Csv(value.PmsSourceSessionId),
                value.CurrentBaseQuantity.ToString(CultureInfo.InvariantCulture),
                value.ProvenanceKind,
                value.PositionAuthorityCode,
                value.AccountId,
                value.BrokerPositionCount.ToString(CultureInfo.InvariantCulture),
                value.BracketEvidenceSha256,
                value.RequiredUniverseSha256,
                value.CoreRepositoryCommit,
                value.PositionSnapshotAsOfUtc.ToString("O", CultureInfo.InvariantCulture),
                value.EvidenceSha256));
    }

    private static string Report(
        Arch7bCoreBracketEvidence core,
        Arch7bRequiredPmsUniverse universe,
        Arch7bPmsGlobalFlatPositionSnapshot snapshot,
        Arch7bGlobalFlatEconomicSmoke smoke) => $"""
        # ARCH7B Bracketed Global-Flat Position Snapshot

        - Consumer contract: `{Arch7bBracketedGlobalFlatContract.Version}`
        - Core commit: `{core.CoreRepositoryCommit}`
        - Core evidence SHA-256: `{core.EvidenceSha256}`
        - Successful Core attempt: `{core.RecomputedSemantics?.SuccessfulAttemptNumber}`
        - Position report P2 UTC: `{core.PositionReportP2Utc:O}`
        - PMS ingestion ID: `{universe.SourceIngestionId:D}`
        - PMS ingestion completed UTC: `{universe.IngestionCompletedAtUtc:O}`
        - Model as-of range: `{universe.EarliestModelAsOfUtc:O}` to `{universe.LatestModelAsOfUtc:O}`
        - Target close range: `{universe.EarliestTargetCloseUtc:O}` to `{universe.LatestTargetCloseUtc:O}`
        - Temporal lineage: `{snapshot.TemporalLineageStatus}`
        - Target-close temporal contract: `{snapshot.TargetCloseTemporalContract}`
        - Raw broker position count: `{core.PositionCount}`
        - Required PMS instruments: `{universe.Instruments.Count}`
        - Required universe SHA-256: `{universe.RequiredUniverseSha256}`
        - Mapping cardinalities: `{universe.MappingCardinalities}`
        - Normalized lines: `{snapshot.NormalizedLineCount}`
        - Derived zero lines: `{snapshot.DerivedZeroCount}`
        - Unknown lines: `{snapshot.UnknownCount}`
        - Import eligibility: `{snapshot.ImportEligibility}`
        - Import freshness: `{snapshot.ImportFreshnessStatus}`
        - Position authority: `{snapshot.PositionAuthorityCode}`
        - Working-order authority: `{snapshot.WorkingOrderAuthority}`
        - Working-order blocker: `{snapshot.WorkingOrderBlocker}`
        - Broker send allowed: `{snapshot.BrokerSendAllowed}`
        - Offline smoke: `{smoke.ObservationCount}/{smoke.TargetPositionCount}/{smoke.PositionOnlyDriftCount}`
        - Projection integrity: `{smoke.ProjectionIntegrityStatus}`

        The candidate is local and immutable. A future append-only database import requires a
        separate authorization and contract. This run performed no database write, FIX logon,
        broker send, order, Fill, PositionLedgerEvent, Account API request, or Databento request.
        """;

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    private static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
