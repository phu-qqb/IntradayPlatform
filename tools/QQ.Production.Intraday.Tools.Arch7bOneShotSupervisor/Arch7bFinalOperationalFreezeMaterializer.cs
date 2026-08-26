using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

/// <summary>
/// Content-addresses the complete semantic template before the two downstream
/// physical freeze artifacts are known.  It deliberately excludes only the two
/// physical bindings and the normal template evidence binding.
/// </summary>
public static class Arch7bPreFreezeTemplateIdentity
{
    public const string ContractVersion = "ARCH7B_PRE_FREEZE_TEMPLATE_IDENTITY_V1";

    public static string Compute(Arch7bOneShotLivePlanTemplate template) =>
        Arch7bOneShotContracts.Sha256(Canonical(template));

    public static string Canonical(Arch7bOneShotLivePlanTemplate template)
    {
        var canonical = string.Join('\n', ContractVersion, template.ContractVersion,
            template.SupervisorCommit, template.SupervisorTree, template.CoreCommit,
            template.CoreTree, template.IntradayCommit, template.IntradayTree,
            template.RuntimeInventorySha256, template.CoreRepositoryAuthoritySha256,
            template.CoreTrackedInventorySha256, template.StaticAuthoritySetSha256,
            template.CommandTemplateSetSha256, template.AdapterSetSha256,
            template.RootCaAuthoritySha256, template.PrivilegeAuthoritySha256,
            template.CalendarAuthoritySha256, template.SloRegistrySha256,
            template.ChronologySha256, template.CleanupAuthoritySha256,
            template.TargetEnvironment, template.AccountId, template.NoOrder,
            template.MaximumSlots, template.MaximumRdsReads, template.MaximumCaptures,
            template.MaximumRetries, string.Join('|', template.FileAuthorities
                .OrderBy(value => value.Key, StringComparer.Ordinal).Select(value =>
                    $"{value.Key}:{value.Value.AuthorityId}:{value.Value.Path}:{value.Value.Sha256}:" +
                    $"{value.Value.MustExist}:{value.Value.MustBeInsideRunRoot}")),
            string.Join('|', template.CommandTemplates.Select(value => value.EvidenceSha256)),
            string.Join('|', template.StageContracts.Select(value => value.EvidenceSha256)));
        return template.SelectedBrowser is null ? canonical : string.Join('\n', canonical,
            template.SelectedBrowser);
    }
}

public sealed record Arch7bFinalOperationalFreezeManifest(
    string ContractVersion,
    string PreFreezeTemplateIdentitySha256,
    string IntradayCommit,
    string IntradayTree,
    string CoreCommit,
    string CoreTree,
    string RuntimeInventorySha256,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    string ExpectedFinalBlocker,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion,
        PreFreezeTemplateIdentitySha256, IntradayCommit, IntradayTree, CoreCommit, CoreTree,
        RuntimeInventorySha256, TargetEnvironment, AccountId, NoOrder, MaximumSlots,
        MaximumRdsReads, MaximumCaptures, MaximumRetries, ExpectedFinalBlocker);

    public void ValidateEvidence() => Require(EvidenceSha256 ==
        Arch7bOneShotContracts.Sha256(Canonical()), "manifest-evidence");

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, detail);
    }
}

public sealed record Arch7bFinalOperationalFreezePacket(
    string ContractVersion,
    string PreFreezeTemplateIdentitySha256,
    string FreezeManifestSha256,
    string IntradayCommit,
    string IntradayTree,
    string CoreCommit,
    string CoreTree,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    string ExpectedFinalBlocker,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion,
        PreFreezeTemplateIdentitySha256, FreezeManifestSha256, IntradayCommit, IntradayTree,
        CoreCommit, CoreTree, TargetEnvironment, AccountId, NoOrder, MaximumSlots,
        MaximumRdsReads, MaximumCaptures, MaximumRetries, ExpectedFinalBlocker);

    public void ValidateEvidence() => Require(EvidenceSha256 ==
        Arch7bOneShotContracts.Sha256(Canonical()), "packet-evidence");

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, detail);
    }
}

public sealed record Arch7bFinalOperationalFreezeClosure(
    string ContractVersion,
    string PreFreezeTemplateIdentitySha256,
    string FreezeManifestSha256,
    string FreezePacketSha256,
    string GovernedSourceTemplateSha256,
    string IntradayCommit,
    string IntradayTree,
    string ClosureStatus,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion,
        PreFreezeTemplateIdentitySha256, FreezeManifestSha256, FreezePacketSha256,
        GovernedSourceTemplateSha256, IntradayCommit, IntradayTree, ClosureStatus);

    public void ValidateEvidence()
    {
        if (EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch,
                "closure-evidence");
    }
}

public sealed record Arch7bFinalOperationalFreezeMaterialization(
    string ContractVersion,
    string FreezeRoot,
    string PreFreezeTemplateIdentitySha256,
    string ManifestPath,
    string ManifestSha256,
    string PacketPath,
    string PacketSha256,
    string TemplatePath,
    string TemplateSha256,
    string ClosurePath,
    string ClosureSha256,
    string EvidenceSha256);

public static class Arch7bFinalOperationalFreezeMaterializer
{
    public const string ContractVersion = "arch7b_final_operational_freeze_materialization_v1";
    public const string ManifestContractVersion = "arch7b_final_operational_freeze_manifest_v1";
    public const string PacketContractVersion = "arch7b_final_operational_freeze_packet_v1";
    public const string ClosureContractVersion = "arch7b_final_operational_freeze_closure_v1";
    public const string ManifestFileName = "arch7b-final-operational-freeze-v7-manifest.json";
    public const string PacketFileName = "ARCH7B-next-operational-run-packet-v7.json";
    public const string ClosureFileName = "arch7b-final-operational-freeze-closure-v1.json";

    public static async Task<Arch7bFinalOperationalFreezeMaterialization> MaterializeAsync(
        Arch7bOneShotLivePlanTemplate semanticTemplate, string templatePath,
        CancellationToken cancellationToken = default)
    {
        semanticTemplate.ValidateEvidence();
        templatePath = Path.GetFullPath(templatePath);
        var freezeRoot = Path.GetDirectoryName(templatePath) ?? throw new ArgumentException(
            "FREEZE_ROOT_REQUIRED", nameof(templatePath));
        Require(Path.GetFileName(templatePath) == Arch7bLiveAuthorityMaterializer.TemplateFileName,
            "template-file-name");
        if (Directory.Exists(freezeRoot) && Directory.EnumerateFileSystemEntries(freezeRoot).Any())
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootNotEmpty, freezeRoot);
        Directory.CreateDirectory(freezeRoot);

        var preFreezeIdentity = Arch7bPreFreezeTemplateIdentity.Compute(semanticTemplate);
        var manifest = CreateManifest(semanticTemplate, preFreezeIdentity);
        var manifestPath = Path.Combine(freezeRoot, ManifestFileName);
        var manifestSha = await WriteCreateNewAsync(manifestPath, manifest, cancellationToken)
            .ConfigureAwait(false);
        var packet = CreatePacket(semanticTemplate, preFreezeIdentity, manifestSha);
        var packetPath = Path.Combine(freezeRoot, PacketFileName);
        var packetSha = await WriteCreateNewAsync(packetPath, packet, cancellationToken)
            .ConfigureAwait(false);
        var finalTemplate = semanticTemplate with
        {
            FreezeManifestSha256 = manifestSha,
            FreezePacketSha256 = packetSha,
            EvidenceSha256 = string.Empty
        };
        finalTemplate = finalTemplate with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(finalTemplate.Canonical())
        };
        var templateSha = await WriteCreateNewAsync(templatePath, finalTemplate, cancellationToken)
            .ConfigureAwait(false);
        await ValidateCorePhysicalFreezeAsync(freezeRoot, finalTemplate, cancellationToken)
            .ConfigureAwait(false);
        var closure = CreateClosure(preFreezeIdentity, manifestSha, packetSha, templateSha,
            finalTemplate);
        var closurePath = Path.Combine(freezeRoot, ClosureFileName);
        var closureSha = await WriteCreateNewAsync(closurePath, closure, cancellationToken)
            .ConfigureAwait(false);
        await ValidatePhysicalFreezeAsync(freezeRoot, finalTemplate, cancellationToken)
            .ConfigureAwait(false);
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n', ContractVersion,
            preFreezeIdentity, manifestSha, packetSha, templateSha, closureSha));
        return new(ContractVersion, freezeRoot, preFreezeIdentity, manifestPath, manifestSha,
            packetPath, packetSha, templatePath, templateSha, closurePath, closureSha, evidence);
    }

    public static async Task ValidateCorePhysicalFreezeAsync(string freezeRoot,
        Arch7bOneShotLivePlanTemplate template, CancellationToken cancellationToken = default)
    {
        freezeRoot = Path.GetFullPath(freezeRoot);
        var manifest = await ReadAndValidateAsync<Arch7bFinalOperationalFreezeManifest>(
            Path.Combine(freezeRoot, ManifestFileName), template.FreezeManifestSha256,
            cancellationToken).ConfigureAwait(false);
        var packet = await ReadAndValidateAsync<Arch7bFinalOperationalFreezePacket>(
            Path.Combine(freezeRoot, PacketFileName), template.FreezePacketSha256,
            cancellationToken).ConfigureAwait(false);
        manifest.ValidateEvidence();
        packet.ValidateEvidence();
        var identity = Arch7bPreFreezeTemplateIdentity.Compute(template);
        Require(manifest.ContractVersion == ManifestContractVersion &&
            packet.ContractVersion == PacketContractVersion &&
            manifest.PreFreezeTemplateIdentitySha256 == identity &&
            packet.PreFreezeTemplateIdentitySha256 == identity &&
            packet.FreezeManifestSha256 == template.FreezeManifestSha256,
            "physical-freeze-cross-binding");
        RequireManifestMatchesTemplate(manifest, template);
        RequirePacketMatchesTemplate(packet, template);
    }

    public static async Task ValidatePhysicalFreezeAsync(string freezeRoot,
        Arch7bOneShotLivePlanTemplate template, CancellationToken cancellationToken = default)
    {
        freezeRoot = Path.GetFullPath(freezeRoot);
        await ValidateCorePhysicalFreezeAsync(freezeRoot, template, cancellationToken)
            .ConfigureAwait(false);
        var templatePath = Path.Combine(freezeRoot,
            Arch7bLiveAuthorityMaterializer.TemplateFileName);
        if (!File.Exists(templatePath)) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, templatePath);
        var templateSha = Convert.ToHexStringLower(SHA256.HashData(
            await File.ReadAllBytesAsync(templatePath, cancellationToken).ConfigureAwait(false)));
        var closure = await ReadAndValidateAsync<Arch7bFinalOperationalFreezeClosure>(
            Path.Combine(freezeRoot, ClosureFileName), cancellationToken).ConfigureAwait(false);
        closure.ValidateEvidence();
        Require(closure.ContractVersion == ClosureContractVersion && closure.ClosureStatus == "PASS" &&
            closure.PreFreezeTemplateIdentitySha256 == Arch7bPreFreezeTemplateIdentity.Compute(template) &&
            closure.FreezeManifestSha256 == template.FreezeManifestSha256 &&
            closure.FreezePacketSha256 == template.FreezePacketSha256 &&
            closure.GovernedSourceTemplateSha256 == templateSha &&
            closure.IntradayCommit == template.IntradayCommit && closure.IntradayTree == template.IntradayTree,
            "closure-cross-binding");
    }

    private static Arch7bFinalOperationalFreezeManifest CreateManifest(
        Arch7bOneShotLivePlanTemplate template, string identity)
    {
        var value = new Arch7bFinalOperationalFreezeManifest(ManifestContractVersion, identity,
            template.IntradayCommit, template.IntradayTree, template.CoreCommit, template.CoreTree,
            template.RuntimeInventorySha256, template.TargetEnvironment, template.AccountId,
            template.NoOrder, template.MaximumSlots, template.MaximumRdsReads,
            template.MaximumCaptures, template.MaximumRetries,
            Arch7bOneShotContracts.ExpectedFinalBlocker, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bFinalOperationalFreezePacket CreatePacket(
        Arch7bOneShotLivePlanTemplate template, string identity, string manifestSha)
    {
        var value = new Arch7bFinalOperationalFreezePacket(PacketContractVersion, identity,
            manifestSha, template.IntradayCommit, template.IntradayTree, template.CoreCommit,
            template.CoreTree, template.TargetEnvironment, template.AccountId, template.NoOrder,
            template.MaximumSlots, template.MaximumRdsReads, template.MaximumCaptures,
            template.MaximumRetries, Arch7bOneShotContracts.ExpectedFinalBlocker, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bFinalOperationalFreezeClosure CreateClosure(string identity,
        string manifestSha, string packetSha, string templateSha,
        Arch7bOneShotLivePlanTemplate template)
    {
        var value = new Arch7bFinalOperationalFreezeClosure(ClosureContractVersion, identity,
            manifestSha, packetSha, templateSha, template.IntradayCommit, template.IntradayTree,
            "PASS", string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static void RequireManifestMatchesTemplate(Arch7bFinalOperationalFreezeManifest value,
        Arch7bOneShotLivePlanTemplate template) => Require(value.IntradayCommit == template.IntradayCommit &&
        value.IntradayTree == template.IntradayTree && value.CoreCommit == template.CoreCommit &&
        value.CoreTree == template.CoreTree && value.RuntimeInventorySha256 == template.RuntimeInventorySha256 &&
        value.TargetEnvironment == template.TargetEnvironment && value.AccountId == template.AccountId &&
        value.NoOrder == template.NoOrder && value.MaximumSlots == template.MaximumSlots &&
        value.MaximumRdsReads == template.MaximumRdsReads && value.MaximumCaptures == template.MaximumCaptures &&
        value.MaximumRetries == template.MaximumRetries && value.ExpectedFinalBlocker ==
        Arch7bOneShotContracts.ExpectedFinalBlocker, "manifest-template-binding");

    private static void RequirePacketMatchesTemplate(Arch7bFinalOperationalFreezePacket value,
        Arch7bOneShotLivePlanTemplate template) => Require(value.IntradayCommit == template.IntradayCommit &&
        value.IntradayTree == template.IntradayTree && value.CoreCommit == template.CoreCommit &&
        value.CoreTree == template.CoreTree && value.TargetEnvironment == template.TargetEnvironment &&
        value.AccountId == template.AccountId && value.NoOrder == template.NoOrder &&
        value.MaximumSlots == template.MaximumSlots && value.MaximumRdsReads == template.MaximumRdsReads &&
        value.MaximumCaptures == template.MaximumCaptures && value.MaximumRetries == template.MaximumRetries &&
        value.ExpectedFinalBlocker == Arch7bOneShotContracts.ExpectedFinalBlocker,
        "packet-template-binding");

    private static async Task<T> ReadAndValidateAsync<T>(string path, string expectedSha,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        Require(Convert.ToHexStringLower(SHA256.HashData(bytes)) == expectedSha, path);
        var options = new JsonSerializerOptions(Arch7bJson.CanonicalOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        return DeserializeStrict<T>(bytes, options, path);
    }

    private static async Task<T> ReadAndValidateAsync<T>(string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var options = new JsonSerializerOptions(Arch7bJson.CanonicalOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        return DeserializeStrict<T>(bytes, options, path);
    }

    private static T DeserializeStrict<T>(byte[] bytes, JsonSerializerOptions options, string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, options) ??
                throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch, path);
        }
        catch (JsonException exception)
        {
            throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch,
                path + ":" + exception.Message);
        }
    }

    private static async Task<string> WriteCreateNewAsync<T>(string path, T value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Arch7bJson.CanonicalOptions);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                         FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var readback = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        Require(bytes.AsSpan().SequenceEqual(readback), path + ":readback");
        return Convert.ToHexStringLower(SHA256.HashData(readback));
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new Arch7bQualificationException(
            Arch7bBlockers.FreezeAuthorityMismatch, detail);
    }
}
