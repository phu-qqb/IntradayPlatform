using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

internal static class Arch7bNativeAdapterJson
{
    public static JsonDocument Parse(string value, string adapterId)
    {
        try
        {
            return JsonDocument.Parse(value, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        }
        catch (JsonException exception)
        {
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid,
                adapterId + ":" + exception.GetType().Name);
        }
    }

    public static string String(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);

    public static int Integer(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var result)
            ? result
            : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);

    public static bool Boolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);

    public static JsonElement Object(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);

    public static JsonElement Array(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array
            ? property
            : throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);

    public static void RequireSha(string value, string name)
    {
        if (!Arch7bOneShotContracts.IsSha256(value))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);
    }

    public static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    public static string ShaText(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Canonical(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" +
                                    Canonical(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",",
                value.EnumerateArray().Select(Canonical)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, "json-kind")
        };
    }

    public static string WithoutPropertyPreservingOrder(JsonElement value, string omitted)
    {
        return "{" + string.Join(",", value.EnumerateObject()
            .Where(property => property.Name != omitted)
            .Select(property => JsonSerializer.Serialize(property.Name) + ":" +
                                CompactPreservingOrder(property.Value))) + "}";
    }

    private static string CompactPreservingOrder(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject()
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" +
                                    CompactPreservingOrder(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",",
                value.EnumerateArray().Select(CompactPreservingOrder)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, "json-kind")
        };
    public static string Option(IReadOnlyList<string> arguments, string name)
    {
        var index = arguments.IndexOf(name);
        if (index < 0 || index + 1 >= arguments.Count)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, name);
        return arguments[index + 1];
    }

    private static int IndexOf(this IReadOnlyList<string> values, string item)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == item) return index;
        return -1;
    }
}

public sealed class Arch7bCoreRuntimePrequalificationAdapter(
    TimeProvider? timeProvider = null) : IArch7bChildResultAdapter
{
    public const string NativeContract =
        "lmax_portal_core_runtime_prequalification_v1";
    public const string ManifestContract =
        "lmax_portal_core_runtime_prequalification_manifest_v1";
    private static readonly string[] ExpectedFiles =
    [
        "core-runtime-prequalification.json",
        "runner-tests.stderr.log",
        "runner-tests.stdout.log"
    ];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public string AdapterId => "core-prequalification-v1";
    public string ContractVersion => Arch7bV2Contracts.ChildResultAdapterVersion;
    public string ExpectedNativeOutputContract => NativeContract;

    public async Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandId.StartsWith("offline-", StringComparison.Ordinal))
            return await OfflineAdapter(NativeContract,
                "ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED", 1)
                .AdaptAsync(nativeOutput, command, runRoot, cancellationToken)
                .ConfigureAwait(false);
        RequireCommand(command);
        using var document = Arch7bNativeAdapterJson.Parse(nativeOutput, AdapterId);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Select(value => value.Name)
                .Order(StringComparer.Ordinal).SequenceEqual(
                    new[] { "manifest", "qualification" }, StringComparer.Ordinal) is false)
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, AdapterId);
        var qualification = Arch7bNativeAdapterJson.Object(root, "qualification");
        var manifest = Arch7bNativeAdapterJson.Object(root, "manifest");
        RequireQualification(qualification);
        RequireManifest(manifest, qualification);

        var outputRoot = ResolveOutputRoot(command);
        Arch7bOneShotAuthorityLoader.RequireAbsolute(outputRoot);
        Arch7bOneShotAuthorityLoader.RequireInside(runRoot, outputRoot);
        var manifestPath = Path.Combine(outputRoot, "prequalification-manifest.json");
        if (!File.Exists(manifestPath))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildEvidenceMissing, "prequalification-manifest.json");
        using (var stored = JsonDocument.Parse(
                   await File.ReadAllBytesAsync(manifestPath, cancellationToken)
                       .ConfigureAwait(false)))
        {
            if (Arch7bNativeAdapterJson.Canonical(stored.RootElement) !=
                Arch7bNativeAdapterJson.Canonical(manifest))
                throw new Arch7bQualificationException(
                    Arch7bBlockers.ChildOutputShaMismatch,
                    "prequalification-manifest.json");
        }
        var qualificationPath = Path.Combine(outputRoot,
            "core-runtime-prequalification.json");
        if (!File.Exists(qualificationPath))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildEvidenceMissing,
                "core-runtime-prequalification.json");
        using (var stored = JsonDocument.Parse(
                   await File.ReadAllBytesAsync(qualificationPath, cancellationToken)
                       .ConfigureAwait(false)))
        {
            if (Arch7bNativeAdapterJson.Canonical(stored.RootElement) !=
                Arch7bNativeAdapterJson.Canonical(qualification))
                throw new Arch7bQualificationException(
                    Arch7bBlockers.ChildOutputShaMismatch,
                    "core-runtime-prequalification.json");
        }

        var files = Arch7bNativeAdapterJson.Array(manifest, "files");
        var paths = new List<string>(ExpectedFiles.Length);
        var hashes = new List<string>(ExpectedFiles.Length);
        foreach (var entry in files.EnumerateArray())
        {
            var relative = Arch7bNativeAdapterJson.String(entry, "relative_path");
            var path = Path.GetFullPath(Path.Combine(outputRoot, relative));
            Arch7bOneShotAuthorityLoader.RequireInside(outputRoot, path);
            if (!File.Exists(path) || new FileInfo(path).Length !=
                Arch7bNativeAdapterJson.Integer(entry, "bytes"))
                throw new Arch7bQualificationException(
                    Arch7bBlockers.ChildEvidenceMissing, relative);
            var expectedSha = Arch7bNativeAdapterJson.String(entry, "sha256");
            Arch7bNativeAdapterJson.RequireSha(expectedSha, relative);
            var actualSha = Arch7bNativeAdapterJson.ShaFile(path);
            if (actualSha != expectedSha)
                throw new Arch7bQualificationException(
                    Arch7bBlockers.ChildOutputShaMismatch, relative);
            paths.Add(path);
            hashes.Add(actualSha);
        }
        var prequalificationSha = Arch7bNativeAdapterJson.String(
            manifest, "prequalification_sha256");
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n',
            ContractVersion, AdapterId, NativeContract,
            "ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED",
            prequalificationSha, string.Join('|', paths), string.Join('|', hashes)));
        return new(ContractVersion, AdapterId, ContractVersion, NativeContract,
            "ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED", paths, hashes,
            paths.Count, evidence);
    }

    private void RequireQualification(JsonElement value)
    {
        Require(Arch7bNativeAdapterJson.String(value, "contract") == NativeContract,
            "qualification.contract");
        Require(Arch7bNativeAdapterJson.String(value, "repository") ==
                "phu-qqb/QQ.Production.Core", "qualification.repository");
        Require(Arch7bNativeAdapterJson.String(value, "core_head") ==
                Arch7bOneShotContracts.CoreCommit, "qualification.core_head");
        Require(Arch7bNativeAdapterJson.String(value, "core_tree") ==
                Arch7bOneShotContracts.CoreTree, "qualification.core_tree");
        Require(Arch7bNativeAdapterJson.Boolean(value, "worktree_clean") &&
                Arch7bNativeAdapterJson.Boolean(value, "index_clean"),
            "qualification.clean");
        Require(Arch7bNativeAdapterJson.String(value, "downloader_version") == "0.6.0" &&
                Arch7bNativeAdapterJson.String(value, "bracket_contract") ==
                "lmax_portal_bracketed_current_position_snapshot_v2",
            "qualification.runtime");
        foreach (var name in new[] { "package_json_sha256", "package_lock_sha256",
                     "runtime_source_set_sha256", "node_executable_sha256" })
            Arch7bNativeAdapterJson.RequireSha(
                Arch7bNativeAdapterJson.String(value, name), name);
        var sourceFiles = Arch7bNativeAdapterJson.Array(value, "runtime_source_files");
        Require(sourceFiles.GetArrayLength() > 0 && sourceFiles.EnumerateArray().All(file =>
            Arch7bNativeAdapterJson.String(file, "relative_path").StartsWith(
                "src/", StringComparison.Ordinal) &&
            Arch7bOneShotContracts.IsSha256(
                Arch7bNativeAdapterJson.String(file, "sha256"))),
            "qualification.runtime_source_files");
        Require(Arch7bNativeAdapterJson.String(value,
                "runtime_source_set_sha256") ==
            Arch7bNativeAdapterJson.ShaText(
                Arch7bNativeAdapterJson.Canonical(sourceFiles)),
            "qualification.runtime_source_set_sha256");
        foreach (var name in new[] { "node_version", "npm_version", "playwright_version",
                     "aws_sdk_secrets_manager_version", "host" })
            _ = Arch7bNativeAdapterJson.String(value, name);
        var browser = Arch7bNativeAdapterJson.Object(value, "browser_runtime");
        Require(Arch7bNativeAdapterJson.String(browser, "channel") == "msedge" &&
                !string.IsNullOrWhiteSpace(
                    Arch7bNativeAdapterJson.String(browser, "version")),
            "qualification.browser_runtime");
        Require(Arch7bNativeAdapterJson.String(value, "exact_test_command") == "npm test" &&
                Arch7bNativeAdapterJson.Integer(value, "tests_passed") ==
                Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount &&
                Arch7bNativeAdapterJson.Integer(value, "tests_total") ==
                Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount &&
                Arch7bNativeAdapterJson.String(value, "syntax_checks") == "PASS" &&
                Arch7bNativeAdapterJson.Integer(value,
                    "npm_audit_omit_dev_vulnerabilities") == 0 &&
                Arch7bNativeAdapterJson.String(value, "secret_sentinel_scan") == "PASS" &&
                Arch7bNativeAdapterJson.String(value, "forbidden_route_scan") == "PASS" &&
                Arch7bNativeAdapterJson.String(value, "git_diff_check") == "PASS",
            "qualification.tests");
        RequireSafety(value);
        RequireFreshness(value, "completed_utc", "valid_until_utc",
            "maximum_age_seconds");
    }

    private void RequireManifest(JsonElement value, JsonElement qualification)
    {
        Require(Arch7bNativeAdapterJson.String(value, "contract") == ManifestContract &&
                Arch7bNativeAdapterJson.String(value, "repository") ==
                "phu-qqb/QQ.Production.Core" &&
                Arch7bNativeAdapterJson.String(value, "core_head") ==
                Arch7bNativeAdapterJson.String(qualification, "core_head") &&
                Arch7bNativeAdapterJson.String(value, "core_tree") ==
                Arch7bNativeAdapterJson.String(qualification, "core_tree"),
            "manifest.identity");
        RequireSafety(value);
        RequireFreshness(value, "created_utc", "valid_until_utc",
            "maximum_age_seconds");
        var files = Arch7bNativeAdapterJson.Array(value, "files");
        Require(Arch7bNativeAdapterJson.Integer(value, "file_count") == 3 &&
                files.GetArrayLength() == 3 &&
                files.EnumerateArray().Select(file =>
                        Arch7bNativeAdapterJson.String(file, "relative_path"))
                    .Order(StringComparer.Ordinal).SequenceEqual(
                        ExpectedFiles.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "manifest.files");
        var expected = Arch7bNativeAdapterJson.String(value,
            "prequalification_sha256");
        Arch7bNativeAdapterJson.RequireSha(expected, "prequalification_sha256");
        using var core = JsonDocument.Parse(
            Arch7bNativeAdapterJson.WithoutPropertyPreservingOrder(value,
                "prequalification_sha256"));
        Require(expected == Arch7bNativeAdapterJson.ShaText(
            Arch7bNativeAdapterJson.Canonical(core.RootElement)),
            "manifest.prequalification_sha256");
    }

    private void RequireFreshness(JsonElement value, string observedName,
        string validUntilName, string maximumAgeName)
    {
        Require(DateTimeOffset.TryParse(
                    Arch7bNativeAdapterJson.String(value, observedName), out var observed) &&
                DateTimeOffset.TryParse(
                    Arch7bNativeAdapterJson.String(value, validUntilName), out var validUntil) &&
                Arch7bNativeAdapterJson.Integer(value, maximumAgeName) == 1800 &&
                observed <= clock.GetUtcNow() && clock.GetUtcNow() <= validUntil &&
                validUntil - observed == TimeSpan.FromSeconds(1800),
            observedName);
    }

    private static void RequireSafety(JsonElement value)
    {
        foreach (var name in new[] { "no_order", "no_fix", "no_account_api",
                     "no_database_write", "no_databento" })
            Require(Arch7bNativeAdapterJson.Boolean(value, name), name);
        Require(!Arch7bNativeAdapterJson.Boolean(value,
            "contains_lmax_report_data"), "contains_lmax_report_data");
    }

    private string ResolveOutputRoot(Arch7bOneShotMaterializedCommand command)
    {
        try
        {
            return Path.GetFullPath(Arch7bNativeAdapterJson.Option(
                command.ArgumentList, "--output-root"));
        }
        catch (Arch7bQualificationException)
        {
            var configPath = Arch7bNativeAdapterJson.Option(
                command.ArgumentList, "--config");
            using var config = JsonDocument.Parse(File.ReadAllBytes(configPath));
            return Path.GetFullPath(Arch7bNativeAdapterJson.String(
                config.RootElement, "outputRoot"));
        }
    }

    private Arch7bStrictJsonResultAdapter OfflineAdapter(string contract,
        string success, int artifactCount) => new(new Arch7bChildAdapterProfile(
            AdapterId, contract, [success], [], artifactCount, artifactCount, "result"));

    private void RequireCommand(Arch7bOneShotMaterializedCommand command)
    {
        Require(command.AdapterId == AdapterId &&
                command.AdapterContractVersion == ContractVersion &&
                command.ExpectedNativeOutputContract == NativeContract,
            "command");
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new Arch7bQualificationException(
            Arch7bV2Blockers.ChildAdapterContractMismatch, detail);
    }
}

public sealed class Arch7bPortalSessionProofAdapter(
    TimeProvider? timeProvider = null) : IArch7bChildResultAdapter
{
    public const string NativeContract = "lmax_portal_demo_session_proof_v1";
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public string AdapterId => "portal-session-v1";
    public string ContractVersion => Arch7bV2Contracts.ChildResultAdapterVersion;
    public string ExpectedNativeOutputContract => NativeContract;

    public Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandId.StartsWith("offline-", StringComparison.Ordinal))
            return OfflineAdapter(NativeContract, "ARCH7B_PORTAL_SESSION_PROVEN", 1)
                .AdaptAsync(nativeOutput, command, runRoot, cancellationToken);
        if (command.AdapterId != AdapterId ||
            command.AdapterContractVersion != ContractVersion ||
            command.ExpectedNativeOutputContract != NativeContract)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, AdapterId);
        using var document = Arch7bNativeAdapterJson.Parse(nativeOutput, AdapterId);
        var root = document.RootElement;
        Require(Arch7bNativeAdapterJson.String(root, "contract") == NativeContract,
            "contract");
        var status = Arch7bNativeAdapterJson.String(root, "status");
        Require(status == "ARCH7B_PORTAL_SESSION_PROVEN", "status");
        Require(Arch7bNativeAdapterJson.String(root, "environment") ==
                "LMAX_LONDON_DEMO" &&
                Arch7bNativeAdapterJson.String(root, "account_id") == "1754288005" &&
                Arch7bNativeAdapterJson.String(root, "portal_origin") ==
                "https://account.london-demo.lmax.com" &&
                Arch7bNativeAdapterJson.String(root, "session_mode") == "manual-session" &&
                Arch7bNativeAdapterJson.Boolean(root, "authenticated") &&
                Arch7bNativeAdapterJson.Boolean(root, "browser_context_closed"),
            "identity");
        foreach (var name in new[] { "no_bracket", "no_fix", "no_order_entry",
                     "no_order", "no_account_api" })
            Require(Arch7bNativeAdapterJson.Boolean(root, name), name);
        foreach (var name in new[] { "credentials_recorded", "cookies_recorded",
                     "tokens_recorded", "html_recorded" })
            Require(!Arch7bNativeAdapterJson.Boolean(root, name), name);
        Require(Arch7bNativeAdapterJson.Array(root, "artifacts").GetArrayLength() == 0,
            "artifacts");
        Require(DateTimeOffset.TryParse(
                    Arch7bNativeAdapterJson.String(root, "observed_at_utc"), out var observed) &&
                DateTimeOffset.TryParse(
                    Arch7bNativeAdapterJson.String(root, "valid_until_utc"), out var validUntil) &&
                Arch7bNativeAdapterJson.Integer(root, "maximum_age_seconds") == 300 &&
                observed <= clock.GetUtcNow() && clock.GetUtcNow() <= validUntil &&
                validUntil - observed == TimeSpan.FromSeconds(300),
            "freshness");
        var evidence = Arch7bNativeAdapterJson.String(root, "evidence_sha256");
        Arch7bNativeAdapterJson.RequireSha(evidence, "evidence_sha256");
        Require(evidence == Arch7bNativeAdapterJson.ShaText(
            Arch7bNativeAdapterJson.WithoutPropertyPreservingOrder(root,
                "evidence_sha256")), "evidence_sha256");
        return Task.FromResult(new Arch7bNormalizedChildResult(
            ContractVersion, AdapterId, ContractVersion, NativeContract, status,
            [], [], 0, evidence));
    }

    private Arch7bStrictJsonResultAdapter OfflineAdapter(string contract,
        string success, int artifactCount) => new(new Arch7bChildAdapterProfile(
            AdapterId, contract, [success], [], artifactCount, artifactCount, "result"));

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new Arch7bQualificationException(
            Arch7bV2Blockers.ChildAdapterContractMismatch, detail);
    }
}
public sealed class Arch7bPrearmedFreshSlotHandoffAdapter : IArch7bChildResultAdapter
{
    public const string NativeContract =
        "arch7b_prearmed_fresh_slot_handoff_cli_v1";
    public string AdapterId => "prearmed-handoff-v1";
    public string ContractVersion => Arch7bV2Contracts.ChildResultAdapterVersion;
    public string ExpectedNativeOutputContract => NativeContract;

    public async Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandId.StartsWith("offline-", StringComparison.Ordinal))
            return await new Arch7bStrictJsonResultAdapter(new(
                AdapterId, NativeContract,
                ["ARCH7B_MARKET_CAPTURE_QUALIFIED",
                 "ARCH7B_MARKET_FINALIZATION_QUALIFIED",
                 "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED"], [], 2, 2, "result"))
                .AdaptAsync(nativeOutput, command, runRoot, cancellationToken)
                .ConfigureAwait(false);
        if (command.AdapterId != AdapterId ||
            command.AdapterContractVersion != ContractVersion ||
            command.ExpectedNativeOutputContract != NativeContract)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, AdapterId);
        var profile = Profile(command);
        using var document = Arch7bNativeAdapterJson.Parse(nativeOutput, AdapterId);
        var root = document.RootElement;
        if (Arch7bNativeAdapterJson.String(root, "contract") != NativeContract ||
            !Arch7bNativeAdapterJson.Boolean(root, "no_order"))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, command.StageId);
        var status = Arch7bNativeAdapterJson.String(root, "status");
        if (!profile.NativeStatuses.Contains(status))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildNativeStatusUnknown, status);
        var artifacts = Arch7bNativeAdapterJson.Array(root, "artifacts");
        if (artifacts.GetArrayLength() != 1)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildNativeArtifactCardinality, command.StageId);
        var artifact = artifacts[0];
        var path = Path.GetFullPath(Arch7bNativeAdapterJson.String(artifact, "path"));
        Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
        Arch7bOneShotAuthorityLoader.RequireInside(runRoot, path);
        if (!File.Exists(path) ||
            Arch7bNativeAdapterJson.String(artifact, "artifact_type") !=
            profile.ArtifactType ||
            (profile.RequiredPath is not null && !string.Equals(path,
                profile.RequiredPath, StringComparison.OrdinalIgnoreCase)) ||
            (profile.RequiredFileName is not null && !string.Equals(
                Path.GetFileName(path), profile.RequiredFileName,
                StringComparison.OrdinalIgnoreCase)))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildEvidenceMissing, profile.ArtifactType);
        var expectedSha = Arch7bNativeAdapterJson.String(artifact, "sha256");
        var actualSha = Arch7bNativeAdapterJson.ShaFile(path);
        if (expectedSha != actualSha)
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputShaMismatch, profile.ArtifactType);
        var evidence = Arch7bNativeAdapterJson.String(root, "evidence_sha256");
        Arch7bNativeAdapterJson.RequireSha(evidence, "evidence_sha256");
        if (evidence != Arch7bNativeAdapterJson.ShaText(
                Arch7bNativeAdapterJson.WithoutPropertyPreservingOrder(
                    root, "evidence_sha256")))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputShaMismatch, "evidence_sha256");
        return new(ContractVersion, AdapterId, ContractVersion, NativeContract,
            profile.NormalizedResult, [path], [actualSha], 1, evidence);
    }

    private static ProfileValue Profile(Arch7bOneShotMaterializedCommand command)
    {
        var mode = Arch7bNativeAdapterJson.Option(command.ArgumentList, "--mode");
        return (command.StageId, mode) switch
        {
            ("CLOCK_CAPTURE_START", "assert-prearmed") => new(
                ["ARCH7B_POSITION_MARKET_SLOT_BINDING_DRAFT_READY"],
                "clock-authority-capture", null,
                "clock_authority_capture.json",
                "ARCH7B_CAPTURE_START_PREARMED"),
            ("MARKET_FINALIZATION", "publish-ready") => new(
                ["READY_MARKER_PUBLISHED", "READY_MARKER_ALREADY_PUBLISHED_IDENTICAL"],
                "position-market-lineage", Path.GetFullPath(
                    Arch7bNativeAdapterJson.Option(command.ArgumentList,
                        "--position-market-lineage-path")), null,
                "ARCH7B_MARKET_FINALIZATION_QUALIFIED"),
            ("PMS_IMPORT", "prearm-and-import") => new(["COMPLETED"],
                "position-market-revision-binding", Path.GetFullPath(
                    Arch7bNativeAdapterJson.Option(command.ArgumentList,
                        "--position-market-revision-binding-path")), null,
                "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED"),
            _ => throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, command.StageId)
        };
    }

    private sealed record ProfileValue(IReadOnlyList<string> NativeStatuses,
        string ArtifactType, string? RequiredPath, string? RequiredFileName,
        string NormalizedResult);
}


public sealed class Arch7bRuntimeSelectionAdapter : IArch7bChildResultAdapter
{
    public const string NativeContract =
        "arch7b_position_snapshot_runtime_selection_v1";
    public string AdapterId => "runtime-selection-v1";
    public string ContractVersion => Arch7bV2Contracts.ChildResultAdapterVersion;
    public string ExpectedNativeOutputContract => NativeContract;

    public async Task<Arch7bNormalizedChildResult> AdaptAsync(string nativeOutput,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandId.StartsWith("offline-", StringComparison.Ordinal))
            return await OfflineAdapter(NativeContract,
                "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED", 1)
                .AdaptAsync(nativeOutput, command, runRoot, cancellationToken)
                .ConfigureAwait(false);
        if (command.AdapterId != AdapterId ||
            command.AdapterContractVersion != ContractVersion ||
            command.ExpectedNativeOutputContract != NativeContract)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, AdapterId);
        using var document = Arch7bNativeAdapterJson.Parse(nativeOutput, AdapterId);
        var root = document.RootElement;
        RequireIdentity(root);
        var artifacts = Arch7bNativeAdapterJson.Array(root, "artifacts");
        if (artifacts.GetArrayLength() != 1)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildNativeArtifactCardinality, AdapterId);
        var entry = artifacts[0];
        var path = Path.GetFullPath(Arch7bNativeAdapterJson.String(entry, "path"));
        Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
        Arch7bOneShotAuthorityLoader.RequireInside(runRoot, path);
        if (!File.Exists(path) || Path.GetFileName(path) != "runtime-selection.json")
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildEvidenceMissing, "runtime-selection.json");
        var expectedSha = Arch7bNativeAdapterJson.String(entry, "sha256");
        var actualSha = Arch7bNativeAdapterJson.ShaFile(path);
        if (actualSha != expectedSha ||
            Arch7bNativeAdapterJson.String(entry, "artifact_type") != "runtime-selection")
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputShaMismatch, "runtime-selection.json");
        using var artifact = JsonDocument.Parse(
            await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
        RequireIdentity(artifact.RootElement);
        foreach (var name in new[] { "selected_position_snapshot_id",
                     "selected_position_snapshot_sha256", "account_id",
                     "target_fingerprint", "source_session_id", "source_ingestion_id",
                     "position_line_count", "evidence_sha256" })
            if (root.StringOrRaw(name) !=
                artifact.RootElement.StringOrRaw(name))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.ChildAdapterContractMismatch, name);
        var evidence = Arch7bNativeAdapterJson.String(root, "evidence_sha256");
        return new(ContractVersion, AdapterId, ContractVersion, NativeContract,
            "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED", [path], [actualSha], 1,
            evidence);
    }

    private Arch7bStrictJsonResultAdapter OfflineAdapter(string contract,
        string success, int artifactCount) => new(new Arch7bChildAdapterProfile(
            AdapterId, contract, [success], [], artifactCount, artifactCount, "result"));

    private static void RequireIdentity(JsonElement value)
    {
        if (Arch7bNativeAdapterJson.String(value, "contract") != NativeContract ||
            Arch7bNativeAdapterJson.String(value, "status") !=
            "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED" ||
            Arch7bNativeAdapterJson.String(value, "account_id") != "1754288005" ||
            Arch7bNativeAdapterJson.Integer(value, "position_line_count") != 99 ||
            !Guid.TryParseExact(Arch7bNativeAdapterJson.String(value,
                "selected_position_snapshot_id"), "D", out var snapshotId) ||
            snapshotId == Guid.Empty ||
            !Guid.TryParseExact(Arch7bNativeAdapterJson.String(value,
                "source_ingestion_id"), "D", out var ingestionId) ||
            ingestionId == Guid.Empty)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.ChildAdapterContractMismatch, "runtime-selection");
        Arch7bNativeAdapterJson.RequireSha(Arch7bNativeAdapterJson.String(value,
            "selected_position_snapshot_sha256"), "selected_position_snapshot_sha256");
        Arch7bNativeAdapterJson.RequireSha(Arch7bNativeAdapterJson.String(value,
            "target_fingerprint"), "target_fingerprint");
        Arch7bNativeAdapterJson.RequireSha(Arch7bNativeAdapterJson.String(value,
            "evidence_sha256"), "evidence_sha256");
        foreach (var name in new[] { "no_database_read", "no_database_write",
                     "no_secret_read", "no_fix", "no_order" })
            if (!Arch7bNativeAdapterJson.Boolean(value, name))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.ChildAdapterContractMismatch, name);
    }
}

internal static class Arch7bNativeAdapterJsonExtensions
{
    public static string StringOrRaw(this JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property))
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid, name);
        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.GetRawText();
    }
}
