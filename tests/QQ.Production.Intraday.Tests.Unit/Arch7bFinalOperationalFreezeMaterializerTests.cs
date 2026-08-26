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
