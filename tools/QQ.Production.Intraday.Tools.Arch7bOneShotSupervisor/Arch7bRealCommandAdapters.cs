using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bNativeArtifact(string Path, string Sha256, string ArtifactType);

public sealed record Arch7bNormalizedChildResult(
    string ContractVersion,
    string AdapterId,
    string AdapterContractVersion,
    string NativeContractVersion,
    string ResultCode,
    IReadOnlyList<string> ArtifactPaths,
    IReadOnlyList<string> ArtifactSha256,
    int NativeArtifactCount,
    string EvidenceSha256);

public interface IArch7bChildResultAdapter
{
    string AdapterId { get; }
    string ContractVersion { get; }
    string ExpectedNativeOutputContract { get; }
    Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default);
}

public sealed record Arch7bChildAdapterProfile(
    string AdapterId,
    string NativeContract,
    IReadOnlyCollection<string> SuccessCodes,
    IReadOnlyCollection<string> ExpectedBlockerCodes,
    int MinimumArtifacts,
    int MaximumArtifacts,
    string ResultProperty);

public sealed class Arch7bStrictJsonResultAdapter(Arch7bChildAdapterProfile profile) : IArch7bChildResultAdapter
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "contract", "result", "status", "artifacts", "evidence_sha256", "counts", "no_order"
    };

    public string AdapterId => profile.AdapterId;
    public string ContractVersion => Arch7bV2Contracts.ChildResultAdapterVersion;
    public string ExpectedNativeOutputContract => profile.NativeContract;

    public async Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default)
    {
        using var document = Parse(nativeOutput);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(value =>
                !AllowedProperties.Contains(value.Name)))
            throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputInvalid, AdapterId);
        var contract = RequiredString(root, "contract");
        if (contract != ExpectedNativeOutputContract || command.ExpectedNativeOutputContract != contract ||
            command.AdapterId != AdapterId || command.AdapterContractVersion != ContractVersion)
            throw new Arch7bQualificationException(Arch7bV2Blockers.ChildAdapterContractMismatch, AdapterId);
        var result = RequiredString(root, profile.ResultProperty);
        if (!profile.SuccessCodes.Contains(result) && !profile.ExpectedBlockerCodes.Contains(result))
            throw new Arch7bQualificationException(Arch7bV2Blockers.ChildNativeStatusUnknown, result);
        if (!root.TryGetProperty("artifacts", out var artifactElement) ||
            artifactElement.ValueKind != JsonValueKind.Array)
            throw new Arch7bQualificationException(Arch7bV2Blockers.ChildNativeArtifactCardinality, AdapterId);
        var artifacts = artifactElement.Deserialize<Arch7bNativeArtifact[]>(Arch7bJson.CanonicalOptions) ?? [];
        if (artifacts.Length < profile.MinimumArtifacts || artifacts.Length > profile.MaximumArtifacts)
            throw new Arch7bQualificationException(Arch7bV2Blockers.ChildNativeArtifactCardinality, AdapterId);
        var paths = new List<string>(artifacts.Length);
        var hashes = new List<string>(artifacts.Length);
        foreach (var artifact in artifacts)
        {
            Arch7bOneShotAuthorityLoader.RequireAbsolute(artifact.Path);
            Arch7bOneShotAuthorityLoader.RequireInside(runRoot, artifact.Path);
            if (!File.Exists(artifact.Path))
                throw new Arch7bQualificationException(Arch7bBlockers.ChildEvidenceMissing, artifact.ArtifactType);
            var actual = Convert.ToHexStringLower(SHA256.HashData(
                await File.ReadAllBytesAsync(artifact.Path, cancellationToken).ConfigureAwait(false)));
            if (actual != artifact.Sha256)
                throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputShaMismatch,
                    artifact.ArtifactType);
            paths.Add(artifact.Path);
            hashes.Add(actual);
        }
        var canonical = string.Join('\n', ContractVersion, AdapterId, contract, result,
            string.Join('|', paths), string.Join('|', hashes));
        return new(ContractVersion, AdapterId, ContractVersion, contract, result, paths, hashes,
            artifacts.Length, Arch7bOneShotContracts.Sha256(canonical));
    }

    private static JsonDocument Parse(string value)
    {
        try
        {
            return JsonDocument.Parse(value, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
        }
        catch (JsonException exception)
        {
            throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputInvalid,
                exception.GetType().Name);
        }
    }

    private static string RequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);
}

public sealed class Arch7bRealCommandAdapterRegistry
{
    private readonly Dictionary<string, IArch7bChildResultAdapter> adapters;

    public Arch7bRealCommandAdapterRegistry(IEnumerable<IArch7bChildResultAdapter>? values = null)
    {
        values ??= CreateDefault();
        adapters = values.ToDictionary(value => value.AdapterId, StringComparer.Ordinal);
        EvidenceSha256 = Arch7bOneShotContracts.Sha256(string.Join('\n', adapters.Values
            .OrderBy(value => value.AdapterId, StringComparer.Ordinal)
            .Select(value => $"{value.AdapterId}|{value.ContractVersion}|{value.ExpectedNativeOutputContract}")));
    }

    public string EvidenceSha256 { get; }
    public IReadOnlyCollection<IArch7bChildResultAdapter> Adapters => adapters.Values;

    public IArch7bChildResultAdapter Require(string adapterId) => adapters.TryGetValue(adapterId, out var value)
        ? value : throw new Arch7bQualificationException(Arch7bV2Blockers.ChildAdapterMissing, adapterId);

    public static IReadOnlyList<IArch7bChildResultAdapter> CreateDefault() =>
    [
        Adapter("core-prequalification-v1", "arch7b_core_runtime_prequalification_v1",
            ["ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED"], 1),
        Adapter("portal-session-v1", "arch7b_portal_session_recovery_v1",
            ["ARCH7B_PORTAL_SESSION_PROVEN"], 1),
        Adapter("rds-arm-orchestrator-v1", "arch7b_operational_orchestrator_lifecycle_v1",
            ["ARCH7B_ARM_IMPORT_OPERATIONAL_ORCHESTRATOR_QUALIFIED"], 2),
        Adapter("rds-preloaded-lease-v1", "arch7b_rds_secret_preloaded_lease_v1",
            ["ARCH7B_RDS_SECRET_PRELOADED_LEASE_READY"], 2),
        Adapter("lmax-bracket-v1", "lmax_portal_bracketed_current_position_snapshot_v2",
            ["ARCH7B_BRACKETED_GLOBAL_FLAT_POSITION_SNAPSHOT_CREATED"], 3),
        Adapter("core-fast-seal-v1", "arch7b_lmax_bracket_fast_seal_v1",
            ["ARCH7B_CORE_FAST_SEAL_QUALIFIED"], 4),
        Adapter("handoff-v3", "arch7b_lmax_portal_core_to_intraday_preloaded_rds_secret_handoff_v3",
            ["CORE_BRACKET_HANDOFF_PRELOADED_RDS_SECRET_LEASE_QUALIFIED"], 3),
        Adapter("position-import-v1", "arch7b_fresh_position_import_fast_path_v1",
            ["ARCH7B_POSITION_IMPORT_APPLIED", "READY"], 2),
        Adapter("runtime-selection-v1", "arch7b_position_snapshot_runtime_selection_v1",
            ["ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED"], 1),
        Adapter("market-recorder-v1", "arch6f_lmax_market_data_slot_capture_v1",
            ["ARCH7B_MARKET_CAPTURE_QUALIFIED"], 2),
        Adapter("pms-economic-replay-v1", "arch6f_economic_replay_v2",
            ["ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED"], 2),
        Adapter("arch7a-shadow-v1", "arch7a_arch7b_shadow_qualification_v1",
            ["ARCH7A_SHADOW_QUALIFICATION_PERSISTED"], 2),
        Adapter("operational-reporting-v1", "anubis_infx_readonly_reporting_bundle_v1",
            ["ANUBIS_INFX_READONLY_REPORTING_BUNDLE_CREATED"], 2),
        Adapter("working-order-preflight-v1", "arch7b_working_order_preflight_v1", [], 1,
            [Arch7bOneShotContracts.ExpectedFinalBlocker])
    ];

    private static IArch7bChildResultAdapter Adapter(string id, string contract,
        IReadOnlyCollection<string> success, int artifactCount,
        IReadOnlyCollection<string>? blockers = null) =>
        new Arch7bStrictJsonResultAdapter(new(id, contract, success,
            blockers ?? new HashSet<string>(StringComparer.Ordinal), artifactCount, artifactCount, "result"));
}
