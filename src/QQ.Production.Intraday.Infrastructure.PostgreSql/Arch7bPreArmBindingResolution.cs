using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

/// <summary>
/// Resolves the values which must be known before the no-order ARM workflow.
/// This is deliberately a pure binding operation: it neither creates ARM state
/// nor changes the database-derived universe it receives.
/// </summary>
public sealed record Arch7bPreArmBindingResolution(
    string ContractVersion,
    string Result,
    string RunId,
    string OwnerIdSha256,
    string FutureAuthorizationIdSha256,
    Guid SourceIngestionId,
    DateTimeOffset SourceIngestionCompletedAtUtc,
    string SourceSessionId,
    string RequiredUniverseSha256,
    string SourceSelectionAuthority,
    string TargetProfile,
    string TargetFingerprint,
    string RepositoryAuthorityContract,
    string RepositoryCommit,
    string BuildCommit,
    bool TransactionReadOnly,
    bool PendingModelChanges,
    bool NoDatabaseWrite,
    bool NoArmedState,
    bool NoOwnerLock,
    bool NoReadyMarker,
    bool NoLmaxAcquisition,
    bool NoFix,
    bool NoBroker,
    bool NoOrder);

public static class Arch7bPreArmBindingResolver
{
    public const string ContractVersion = "arch7b_prearm_binding_resolution_v1";
    public const string Resolved = "ARCH7B_PREARM_BINDINGS_RESOLVED";
    public const string ExpectedTargetFingerprint =
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4";

    public static Arch7bPreArmBindingResolution Resolve(
        string runId,
        string ownerId,
        string futureAuthorizationId,
        Arch7bRequiredPmsUniverse universe,
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository)
    {
        RequireIdentity(runId, "ARCH7B_PREARM_RUN_ID_INVALID");
        RequireIdentity(ownerId, "ARCH7B_PREARM_OWNER_ID_INVALID");
        RequireIdentity(futureAuthorizationId,
            "ARCH7B_PREARM_FUTURE_AUTHORIZATION_ID_INVALID");
        ArgumentNullException.ThrowIfNull(universe);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(repository);

        Require(universe.SourceIngestionId != Guid.Empty,
            "ARCH7B_PREARM_SOURCE_INGESTION_INVALID");
        Require(universe.IngestionCompletedAtUtc.Offset == TimeSpan.Zero,
            "ARCH7B_PREARM_SOURCE_INGESTION_COMPLETION_NOT_UTC");
        Require(!string.IsNullOrWhiteSpace(universe.SourceSessionId),
            "ARCH7B_PREARM_SOURCE_SESSION_MISSING");
        Require(Arch5bHashing.IsSha256(universe.RequiredUniverseSha256),
            "ARCH7B_PREARM_REQUIRED_UNIVERSE_SHA_INVALID");
        Require(universe.TransactionReadOnly && universe.NoDatabaseWrite,
            "ARCH7B_PREARM_TRANSACTION_NOT_READ_ONLY");
        Require(!universe.PendingModelChanges,
            "ARCH7B_PREARM_PENDING_MODEL_CHANGES");
        Require(target.TargetProfileId ==
                Arch7bBracketedGlobalFlatContract.TargetProfile &&
                target.TargetFingerprint ==
                ExpectedTargetFingerprint,
            "ARCH7B_PREARM_TARGET_FINGERPRINT_MISMATCH");
        Require(universe.TargetProfile == target.TargetProfileId &&
                universe.TargetFingerprint == target.TargetFingerprint,
            "ARCH7B_PREARM_UNIVERSE_TARGET_MISMATCH");
        Require(repository.ContractVersion ==
                GitArch7bRepositoryStateAuthority.ContractVersion &&
                GitCommitIdentityContract.IsValid(repository.HeadCommit, "sha1") &&
                repository.HeadCommit == repository.BuildCommit &&
                repository.IndexClean && repository.WorktreeClean,
            "ARCH7B_PREARM_REPOSITORY_AUTHORITY_INVALID");

        return new(
            ContractVersion,
            Resolved,
            runId,
            Sha256Utf8(ownerId),
            Sha256Utf8(futureAuthorizationId),
            universe.SourceIngestionId,
            universe.IngestionCompletedAtUtc,
            universe.SourceSessionId,
            universe.RequiredUniverseSha256,
            universe.SourceSelectionAuthority,
            target.TargetProfileId,
            target.TargetFingerprint,
            repository.ContractVersion,
            repository.HeadCommit,
            repository.BuildCommit,
            TransactionReadOnly: true,
            PendingModelChanges: false,
            NoDatabaseWrite: true,
            NoArmedState: true,
            NoOwnerLock: true,
            NoReadyMarker: true,
            NoLmaxAcquisition: true,
            NoFix: true,
            NoBroker: true,
            NoOrder: true);
    }

    private static string Sha256Utf8(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void RequireIdentity(string value, string code) =>
        Require(!string.IsNullOrWhiteSpace(value) &&
                value.Trim() == value &&
                value.All(character => !char.IsControl(character)),
            code);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public static class Arch7bPreArmBindingResolutionStore
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    public static string Publish(
        string outputDirectory,
        Arch7bPreArmBindingResolution resolution)
    {
        var directory = Path.GetFullPath(outputDirectory);
        var path = Path.Combine(directory, "arm-preconditions-resolution.json");
        Arch7bPositionImportAtomicFile.Publish(path, resolution, Json);
        return path;
    }
}
