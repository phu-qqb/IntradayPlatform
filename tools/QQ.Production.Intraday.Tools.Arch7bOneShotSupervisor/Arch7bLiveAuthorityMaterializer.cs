using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bLiveAuthorityMaterializationEvidence(
    string ContractVersion,
    string TemplatePath,
    string TemplateSha256,
    string OperatorAuthorizationPath,
    string OperatorAuthorizationSha256,
    string LiveExecutionAuthorityPath,
    string LiveExecutionAuthoritySha256,
    string ManifestPath,
    string ManifestSha256,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public static class Arch7bLiveAuthorityMaterializer
{
    public const string ContractVersion = "arch7b_live_authority_materialization_v1";
    public const string TemplateFileName = "arch7b-one-shot-live-plan-template.json";
    public const string OperatorAuthorizationFileName = "arch7b-one-shot-operator-authorization.json";
    public const string LiveExecutionAuthorityFileName = "arch7b-one-shot-live-execution-authority.json";
    public const string ManifestFileName = "arch7b-live-authority-materialization-manifest.json";

    public static async Task<Arch7bLiveAuthorityMaterializationEvidence> MaterializeAsync(
        string freezeRoot, string expectedFreezeManifestSha256, string expectedFreezePacketSha256,
        string expectedTemplateSha256, string operatorAuthorizationId, DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc, string outputRoot, string targetEnvironment, string accountId,
        bool noOrder, CancellationToken cancellationToken = default)
    {
        RequireStaticArguments(freezeRoot, expectedFreezeManifestSha256, expectedFreezePacketSha256,
            expectedTemplateSha256, operatorAuthorizationId, issuedAtUtc, expiresAtUtc, outputRoot,
            targetEnvironment, accountId, noOrder);
        var templatePath = Path.Combine(Path.GetFullPath(freezeRoot), TemplateFileName);
        var manifestPath = Path.Combine(Path.GetFullPath(freezeRoot),
            "arch7b-final-operational-freeze-v7-manifest.json");
        var packetPath = Path.Combine(Path.GetFullPath(freezeRoot), "ARCH7B-next-operational-run-packet-v7.json");
        var template = await Arch7bLiveAuthorityLoaderV2.LoadTemplateAsync(templatePath, expectedTemplateSha256,
            cancellationToken).ConfigureAwait(false);
        RequireHash(manifestPath, expectedFreezeManifestSha256, Arch7bBlockers.FreezeAuthorityMismatch);
        RequireHash(packetPath, expectedFreezePacketSha256, Arch7bBlockers.FreezeAuthorityMismatch);
        if (template.Value.FreezeManifestSha256 != expectedFreezeManifestSha256 ||
            template.Value.FreezePacketSha256 != expectedFreezePacketSha256)
            throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch);
        await Arch7bFinalOperationalFreezeMaterializer.ValidatePhysicalFreezeAsync(freezeRoot,
            template.Value, cancellationToken).ConfigureAwait(false);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        Arch7bLiveTemplateValidator.Validate(template.Value, adapters);

        outputRoot = Path.GetFullPath(outputRoot);
        if (Directory.Exists(outputRoot) && Directory.EnumerateFileSystemEntries(outputRoot).Any())
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootNotEmpty);
        Directory.CreateDirectory(outputRoot);
        var authorization = CreateAuthorization(operatorAuthorizationId, issuedAtUtc, expiresAtUtc);
        var authority = CreateAuthority(template.Value, template.FileSha256, authorization);
        var authorizationPath = Path.Combine(outputRoot, OperatorAuthorizationFileName);
        var authorityPath = Path.Combine(outputRoot, LiveExecutionAuthorityFileName);
        var manifestOutputPath = Path.Combine(outputRoot, ManifestFileName);
        var authorizationSha = await WriteCreateNewAsync(authorizationPath, authorization, cancellationToken)
            .ConfigureAwait(false);
        var authoritySha = await WriteCreateNewAsync(authorityPath, authority, cancellationToken)
            .ConfigureAwait(false);
        var manifest = new
        {
            contract = ContractVersion,
            template_path = templatePath,
            template_sha256 = template.FileSha256,
            operator_authorization_path = authorizationPath,
            operator_authorization_sha256 = authorizationSha,
            live_execution_authority_path = authorityPath,
            live_execution_authority_sha256 = authoritySha,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
        var manifestSha = await WriteCreateNewAsync(manifestOutputPath, manifest, cancellationToken)
            .ConfigureAwait(false);
        var canonical = string.Join('\n', ContractVersion, template.FileSha256, authorizationSha, authoritySha,
            manifestSha);
        return new(ContractVersion, templatePath, template.FileSha256, authorizationPath, authorizationSha,
            authorityPath, authoritySha, manifestOutputPath, manifestSha, Arch7bNoLiveSafetyCounters.Zero,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private static void RequireStaticArguments(string freezeRoot, string expectedManifest, string expectedPacket,
        string expectedTemplate, string operatorAuthorizationId, DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc, string outputRoot, string targetEnvironment, string accountId, bool noOrder)
    {
        foreach (var path in new[] { freezeRoot, outputRoot }) Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
        foreach (var sha in new[] { expectedManifest, expectedPacket, expectedTemplate })
            if (!Arch7bOneShotContracts.IsSha256(sha))
                throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch);
        var nowUtc = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(operatorAuthorizationId) || operatorAuthorizationId.Length > 128 ||
            targetEnvironment != "TEST" || accountId != "1754288005" || !noOrder ||
            issuedAtUtc > nowUtc || expiresAtUtc <= issuedAtUtc || expiresAtUtc <= nowUtc)
            throw new Arch7bQualificationException(Arch7bBlockers.OperatorAuthorizationMismatch);
    }

    private static Arch7bOneShotOperatorAuthorizationV2 CreateAuthorization(string id, DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        var value = new Arch7bOneShotOperatorAuthorizationV2(Arch7bV2Contracts.OperatorAuthorizationVersion, id,
            "TEST", "1754288005", true, 1, 2, 1, 0, issuedAtUtc, expiresAtUtc, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bOneShotLiveExecutionAuthorityV3 CreateAuthority(Arch7bOneShotLivePlanTemplate template,
        string templateSha256, Arch7bOneShotOperatorAuthorizationV2 authorization)
    {
        var value = new Arch7bOneShotLiveExecutionAuthorityV3(Arch7bV2Contracts.LiveExecutionAuthorityVersion,
            template.SupervisorCommit, template.SupervisorTree, template.CoreCommit, template.CoreTree,
            template.IntradayCommit, template.IntradayTree, template.FreezeManifestSha256,
            template.FreezePacketSha256, templateSha256, template.RuntimeInventorySha256,
            template.CoreRepositoryAuthoritySha256, template.CoreTrackedInventorySha256,
            template.StaticAuthoritySetSha256, template.CommandTemplateSetSha256, template.AdapterSetSha256,
            template.RootCaAuthoritySha256, template.PrivilegeAuthoritySha256, template.CalendarAuthoritySha256,
            template.SloRegistrySha256, template.ChronologySha256, template.CleanupAuthoritySha256,
            authorization.OperatorAuthorizationId, template.TargetEnvironment, template.AccountId, template.NoOrder,
            template.MaximumSlots, template.MaximumRdsReads, template.MaximumCaptures, template.MaximumRetries,
            template.FileAuthorities, authorization.IssuedAtUtc, authorization.ExpiresAtUtc, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static void RequireHash(string path, string expectedSha256, string blocker)
    {
        if (!File.Exists(path) || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != expectedSha256)
            throw new Arch7bQualificationException(blocker, path);
    }

    private static async Task<string> WriteCreateNewAsync<T>(string path, T value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Arch7bJson.CanonicalOptions);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough | FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
