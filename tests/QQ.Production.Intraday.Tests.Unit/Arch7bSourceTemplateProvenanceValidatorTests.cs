using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bSourceTemplateProvenanceValidatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-source-template-provenance", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Exact_normalized_source_is_accepted_and_deterministic()
    {
        var (path, template, sha) = await WriteTemplateAsync();

        var first = await Arch7bSourceTemplateProvenanceValidator.ValidateAsync(
            path, sha, template.IntradayCommit, template.IntradayTree);
        var second = await Arch7bSourceTemplateProvenanceValidator.ValidateAsync(
            path, sha, template.IntradayCommit, template.IntradayTree);

        Assert.Equal(first.EvidenceSha256, second.EvidenceSha256);
        Assert.Equal(40, first.StageCount);
        Assert.Equal(0, first.LegacyAliasCount);
        Assert.Equal(0, first.MissingProducerCount);
        Assert.Equal(0, first.DuplicateProducerCount);
        Assert.Equal(0, first.FutureProducerCount);
        Assert.Equal(3, first.ClockContractCount);
        Assert.Equal("PASS", first.GraphValidationStatus);
    }

    [Theory]
    [InlineData("sha")]
    [InlineData("commit")]
    [InlineData("tree")]
    public async Task Expected_source_identity_mismatch_is_rejected(string field)
    {
        var (path, template, sha) = await WriteTemplateAsync();
        var expectedSha = field == "sha" ? new string('0', 64) : sha;
        var expectedCommit = field == "commit" ? new string('0', 40) : template.IntradayCommit;
        var expectedTree = field == "tree" ? new string('0', 40) : template.IntradayTree;

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bSourceTemplateProvenanceValidator.ValidateAsync(
                path, expectedSha, expectedCommit, expectedTree));

        Assert.Equal(Arch7bV2Blockers.SourceTemplateProvenanceMismatch,
            error.BlockerCode);
    }

    [Fact]
    public async Task Legacy_source_is_rejected_before_materialization_side_effects()
    {
        var fixture = CreateFixture();
        var stageContracts = fixture.Template.StageContracts.Select(stage =>
            stage.StageId == "PORTAL_SESSION_PROVEN"
                ? stage with
                {
                    RequiredFactTypes = stage.RequiredFactTypes.Append(
                        Arch7bClockFactContracts.LegacyPreflightFactType).ToArray()
                }
                : stage).ToArray();
        var provisional = fixture.Template with
        {
            StageContracts = stageContracts,
            EvidenceSha256 = string.Empty
        };
        var stale = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
        var path = Path.Combine(root, "legacy-source-template.json");
        Directory.CreateDirectory(root);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(stale, Arch7bJson.CanonicalOptions);
        await File.WriteAllBytesAsync(path, bytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var outputRoot = Path.Combine(root, "must-not-exist");

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            new Arch7bOperationalExecutionAuthorityMaterializer().MaterializeFilesAsync(
                path, sha, stale.IntradayCommit, stale.IntradayTree,
                Path.Combine(root, "unread-path-map.json"), outputRoot));

        Assert.Equal(Arch7bV2Blockers.LegacyStageFactAliasPresent,
            error.BlockerCode);
        Assert.False(Directory.Exists(outputRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private async Task<(string Path, Arch7bOneShotLivePlanTemplate Template, string Sha)>
        WriteTemplateAsync()
    {
        var fixture = CreateFixture();
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "source-template.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            fixture.Template, Arch7bJson.CanonicalOptions);
        await File.WriteAllBytesAsync(path, bytes);
        return (path, fixture.Template,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private Arch7bV2QualificationFixture CreateFixture() =>
        Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location,
            Path.Combine(root, "run-root"));
}
