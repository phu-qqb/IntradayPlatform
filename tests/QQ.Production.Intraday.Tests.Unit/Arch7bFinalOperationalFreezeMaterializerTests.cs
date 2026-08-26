using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bFinalOperationalFreezeMaterializerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "qq-arch7b-final-freeze", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Pre_freeze_identity_excludes_only_downstream_physical_bindings_and_evidence()
    {
        var template = Fixture();
        var changedDownstream = template with
        {
            FreezeManifestSha256 = new string('a', 64),
            FreezePacketSha256 = new string('b', 64),
            EvidenceSha256 = new string('c', 64)
        };
        var changedSemantic = template with { AccountId = "1754288006" };

        Assert.Equal(Arch7bPreFreezeTemplateIdentity.Compute(template),
            Arch7bPreFreezeTemplateIdentity.Compute(changedDownstream));
        Assert.NotEqual(Arch7bPreFreezeTemplateIdentity.Compute(template),
            Arch7bPreFreezeTemplateIdentity.Compute(changedSemantic));
    }

    [Fact]
    public async Task Materialization_is_deterministic_and_binds_the_final_template_to_real_files()
    {
        var template = Fixture();
        var first = await MaterializeAsync(template, "first");
        var second = await MaterializeAsync(template, "second");
        var firstTemplate = await ReadTemplateAsync(first.TemplatePath);
        var manifest = await ReadAsync<Arch7bFinalOperationalFreezeManifest>(first.ManifestPath);
        var packet = await ReadAsync<Arch7bFinalOperationalFreezePacket>(first.PacketPath);
        var closure = await ReadAsync<Arch7bFinalOperationalFreezeClosure>(first.ClosurePath);

        Assert.Equal(FileHash(first.ManifestPath), firstTemplate.FreezeManifestSha256);
        Assert.Equal(FileHash(first.PacketPath), firstTemplate.FreezePacketSha256);
        Assert.NotEqual(template.FreezeManifestSha256, firstTemplate.FreezeManifestSha256);
        Assert.NotEqual(template.FreezePacketSha256, firstTemplate.FreezePacketSha256);
        Assert.Equal(first.PreFreezeTemplateIdentitySha256, manifest.PreFreezeTemplateIdentitySha256);
        Assert.Equal(first.PreFreezeTemplateIdentitySha256, packet.PreFreezeTemplateIdentitySha256);
        Assert.Equal(first.ManifestSha256, packet.FreezeManifestSha256);
        Assert.Equal(first.PreFreezeTemplateIdentitySha256,
            Arch7bPreFreezeTemplateIdentity.Compute(firstTemplate));
        Assert.Equal(first.PreFreezeTemplateIdentitySha256, closure.PreFreezeTemplateIdentitySha256);
        Assert.Equal(first.ManifestSha256, closure.FreezeManifestSha256);
        Assert.Equal(first.PacketSha256, closure.FreezePacketSha256);
        Assert.Equal(first.TemplateSha256, closure.GovernedSourceTemplateSha256);
        Assert.Equal(await File.ReadAllBytesAsync(first.ManifestPath),
            await File.ReadAllBytesAsync(second.ManifestPath));
        Assert.Equal(await File.ReadAllBytesAsync(first.PacketPath),
            await File.ReadAllBytesAsync(second.PacketPath));
        Assert.Equal(await File.ReadAllBytesAsync(first.TemplatePath),
            await File.ReadAllBytesAsync(second.TemplatePath));
        Assert.Equal(first.ManifestSha256, second.ManifestSha256);
        Assert.Equal(first.PacketSha256, second.PacketSha256);
        using var manifestDocument = JsonDocument.Parse(await File.ReadAllBytesAsync(first.ManifestPath));
        using var packetDocument = JsonDocument.Parse(await File.ReadAllBytesAsync(first.PacketPath));
        Assert.False(manifestDocument.RootElement.TryGetProperty(
            "governed_source_template_sha256", out _));
        Assert.False(packetDocument.RootElement.TryGetProperty(
            "governed_source_template_sha256", out _));
        await Arch7bFinalOperationalFreezeMaterializer.ValidatePhysicalFreezeAsync(
            first.FreezeRoot, firstTemplate);
    }

    [Fact]
    public async Task Target_semantic_binding_change_materializes_a_distinct_target_freeze()
    {
        var source = Fixture();
        var targetAuthorities = new Dictionary<string, Arch7bFileAuthority>(source.FileAuthorities,
            StringComparer.Ordinal)
        {
            ["supervisor_executable"] = source.FileAuthorities["supervisor_executable"] with
            {
                Path = Path.Combine(root, "target", "supervisor.exe"),
                Sha256 = new string('d', 64)
            }
        };
        var targetProvisional = source with
        {
            FileAuthorities = targetAuthorities,
            StaticAuthoritySetSha256 = new string('e', 64),
            EvidenceSha256 = string.Empty
        };
        var target = targetProvisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(targetProvisional.Canonical())
        };
        var sourceFreeze = await MaterializeAsync(source, "source-freeze");
        var targetFreeze = await MaterializeAsync(target, "target-freeze");
        var targetManifest = await ReadAsync<Arch7bFinalOperationalFreezeManifest>(
            targetFreeze.ManifestPath);
        var targetTemplate = await ReadTemplateAsync(targetFreeze.TemplatePath);

        Assert.NotEqual(sourceFreeze.PreFreezeTemplateIdentitySha256,
            targetFreeze.PreFreezeTemplateIdentitySha256);
        Assert.Equal(targetFreeze.PreFreezeTemplateIdentitySha256,
            targetManifest.PreFreezeTemplateIdentitySha256);
        Assert.Equal(targetFreeze.ManifestSha256, targetTemplate.FreezeManifestSha256);
        Assert.Equal(targetFreeze.PacketSha256, targetTemplate.FreezePacketSha256);
        Assert.NotEqual(sourceFreeze.ManifestSha256, targetTemplate.FreezeManifestSha256);
        Assert.NotEqual(sourceFreeze.PacketSha256, targetTemplate.FreezePacketSha256);
    }

    [Theory]
    [InlineData(Arch7bFinalOperationalFreezeMaterializer.ManifestFileName)]
    [InlineData(Arch7bFinalOperationalFreezeMaterializer.PacketFileName)]
    public async Task Physical_freeze_artifact_mutation_fails_closed(string fileName)
    {
        var materialized = await MaterializeAsync(Fixture(), "mutated-" + fileName[0]);
        var template = await ReadTemplateAsync(materialized.TemplatePath);
        await File.AppendAllTextAsync(Path.Combine(materialized.FreezeRoot, fileName), "x");

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bFinalOperationalFreezeMaterializer.ValidatePhysicalFreezeAsync(
                materialized.FreezeRoot, template));

        Assert.Equal(Arch7bBlockers.FreezeAuthorityMismatch, error.BlockerCode);
    }

    [Fact]
    public async Task Missing_physical_freeze_artifact_fails_closed()
    {
        var materialized = await MaterializeAsync(Fixture(), "missing");
        var template = await ReadTemplateAsync(materialized.TemplatePath);
        File.Delete(materialized.ManifestPath);

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bFinalOperationalFreezeMaterializer.ValidatePhysicalFreezeAsync(
                materialized.FreezeRoot, template));

        Assert.Equal(Arch7bBlockers.FreezeAuthorityMismatch, error.BlockerCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("mutated")]
    [InlineData("unknown")]
    [InlineData("template")]
    [InlineData("manifest")]
    [InlineData("packet")]
    [InlineData("identity")]
    public async Task Complete_freeze_validation_requires_a_valid_closure(string mutation)
    {
        var materialized = await MaterializeAsync(Fixture(), "closure-" + mutation);
        var template = await ReadTemplateAsync(materialized.TemplatePath);
        if (mutation == "missing")
        {
            File.Delete(materialized.ClosurePath);
        }
        else if (mutation == "mutated")
        {
            await File.AppendAllTextAsync(materialized.ClosurePath, "x");
        }
        else if (mutation == "unknown")
        {
            var document = System.Text.Json.Nodes.JsonNode.Parse(
                await File.ReadAllBytesAsync(materialized.ClosurePath))!.AsObject();
            document["unexpected"] = "forbidden";
            await File.WriteAllTextAsync(materialized.ClosurePath, document.ToJsonString());
        }
        else
        {
            var closure = await ReadAsync<Arch7bFinalOperationalFreezeClosure>(materialized.ClosurePath);
            closure = mutation switch
            {
                "template" => closure with { GovernedSourceTemplateSha256 = new string('0', 64) },
                "manifest" => closure with { FreezeManifestSha256 = new string('0', 64) },
                "packet" => closure with { FreezePacketSha256 = new string('0', 64) },
                "identity" => closure with { PreFreezeTemplateIdentitySha256 = new string('0', 64) },
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            closure = closure with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(closure.Canonical()) };
            await File.WriteAllBytesAsync(materialized.ClosurePath, JsonSerializer.SerializeToUtf8Bytes(
                closure, Arch7bJson.CanonicalOptions));
        }

        await Arch7bFinalOperationalFreezeMaterializer.ValidateCorePhysicalFreezeAsync(
            materialized.FreezeRoot, template);
        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bFinalOperationalFreezeMaterializer.ValidatePhysicalFreezeAsync(
                materialized.FreezeRoot, template));

        Assert.Equal(Arch7bBlockers.FreezeAuthorityMismatch, error.BlockerCode);
    }

    [Fact]
    public async Task Live_authority_issuance_rejects_a_missing_closure_before_writing_authorities()
    {
        var materialized = await MaterializeAsync(Fixture(), "live-authority-closure");
        File.Delete(materialized.ClosurePath);
        var now = DateTimeOffset.UtcNow;
        var outputRoot = Path.Combine(root, "live-authority-output");

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bLiveAuthorityMaterializer.MaterializeAsync(materialized.FreezeRoot,
                materialized.ManifestSha256, materialized.PacketSha256, materialized.TemplateSha256,
                "closure-required", now.AddSeconds(-1), now.AddMinutes(5), outputRoot,
                "TEST", "1754288005", true));

        Assert.Equal(Arch7bBlockers.FreezeAuthorityMismatch, error.BlockerCode);
        Assert.False(Directory.Exists(outputRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private Task<Arch7bFinalOperationalFreezeMaterialization> MaterializeAsync(
        Arch7bOneShotLivePlanTemplate template, string name) =>
        Arch7bFinalOperationalFreezeMaterializer.MaterializeAsync(template, Path.Combine(root,
            name, Arch7bLiveAuthorityMaterializer.TemplateFileName));

    private Arch7bOneShotLivePlanTemplate Fixture() => Arch7bV2QualificationFactory.Create(
        typeof(Program).Assembly.Location, Path.Combine(root, "fixture-run")).Template;

    private static async Task<Arch7bOneShotLivePlanTemplate> ReadTemplateAsync(string path) =>
        await ReadAsync<Arch7bOneShotLivePlanTemplate>(path);

    private static async Task<T> ReadAsync<T>(string path) => JsonSerializer.Deserialize<T>(
        await File.ReadAllBytesAsync(path), Arch7bJson.CanonicalOptions) ??
        throw new InvalidDataException(path);

    private static string FileHash(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));
}
