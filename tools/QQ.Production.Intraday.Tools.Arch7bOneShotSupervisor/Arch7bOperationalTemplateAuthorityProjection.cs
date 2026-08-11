using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bOperationalTemplateAuthorityProjection
{
    public static async Task<Arch7bOperationalTemplateFileMaterialization> WriteAsync(
        string sourceTemplatePath, string sourceManifestPath, string authorityManifestPath,
        string outputPath, CancellationToken cancellationToken = default)
    {
        sourceTemplatePath = Path.GetFullPath(sourceTemplatePath);
        sourceManifestPath = Path.GetFullPath(sourceManifestPath);
        authorityManifestPath = Path.GetFullPath(authorityManifestPath);
        outputPath = Path.GetFullPath(outputPath);
        var sourceTemplateBytes = await File.ReadAllBytesAsync(sourceTemplatePath,
            cancellationToken).ConfigureAwait(false);
        var sourceCommandBytes = await File.ReadAllBytesAsync(sourceManifestPath,
            cancellationToken).ConfigureAwait(false);
        var authorityBytes = await File.ReadAllBytesAsync(authorityManifestPath,
            cancellationToken).ConfigureAwait(false);
        var skeleton = JsonSerializer.Deserialize<Arch7bOneShotLivePlanTemplate>(
            sourceTemplateBytes, Arch7bJson.CanonicalOptions) ?? throw Mismatch("source-template");
        skeleton.ValidateEvidence();
        var compiled = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            skeleton, sourceCommandBytes);
        var inventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder.Build(
            compiled.Template);
        var manifest = Arch7bOperationalExecutionAuthorityManifestParser.ParseStrict(authorityBytes);
        var compiledTemplateBytes = JsonSerializer.SerializeToUtf8Bytes(compiled.Template,
            Arch7bJson.CanonicalOptions);
        if (Convert.ToHexStringLower(SHA256.HashData(compiledTemplateBytes)) !=
            manifest.SourceTemplateSha256)
            throw Mismatch("authority-source-template-sha256");
        var authorities = manifest.Project(inventory);
        Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(inventory, manifest,
            authorities);
        var staticAuthoritySet = Arch7bOneShotContracts.Sha256(string.Join('\n', authorities
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => string.Join(':', value.Key, value.Value.Path,
                value.Value.Sha256, value.Value.MustExist, value.Value.MustBeInsideRunRoot))));
        var provisional = compiled.Template with
        {
            FileAuthorities = authorities,
            StaticAuthoritySetSha256 = staticAuthoritySet,
            EvidenceSha256 = string.Empty
        };
        var template = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
        Arch7bOperationalExecutionAuthorityValidator.RequireExactProjection(authorities,
            template.FileAuthorities, "template");
        var outputBytes = JsonSerializer.SerializeToUtf8Bytes(template, Arch7bJson.CanonicalOptions);
        var outputText = Encoding.UTF8.GetString(outputBytes);
        var regenerateCount = Count(outputText, Arch7bOperationalLiveFactBindingCatalog.Marker);
        var fakeNativeChildCount = Count(outputText, "fake-native-child") + Count(outputText, "fake-child");
        var syntheticAuthorityCount = Count(outputText, "synthetic-authority") +
            Count(outputText, "synthetic_authority");
        var unresolvedProducerCount = Arch7bOperationalBindingProducerAudit.Build().MissingProducerCount;
        if (regenerateCount != 0 || fakeNativeChildCount != 0 || syntheticAuthorityCount != 0 ||
            unresolvedProducerCount != 0) throw Mismatch("operational-template-counts");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write,
                         FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await stream.WriteAsync(outputBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var readback = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
        var identical = outputBytes.AsSpan().SequenceEqual(readback);
        if (!identical) throw Mismatch("template-readback");
        var sourceSha = Convert.ToHexStringLower(SHA256.HashData(sourceTemplateBytes));
        var outputSha = Convert.ToHexStringLower(SHA256.HashData(outputBytes));
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n',
            Arch7bOperationalLivePlanTemplateMaterializer.FileVersion, sourceTemplatePath,
            sourceSha, outputPath, outputSha, template.EvidenceSha256, compiled.CommandCount,
            compiled.BindingCount, regenerateCount, fakeNativeChildCount,
            syntheticAuthorityCount, unresolvedProducerCount, identical));
        return new(Arch7bOperationalLivePlanTemplateMaterializer.FileVersion,
            sourceTemplatePath, sourceSha, outputPath, outputSha, template.EvidenceSha256,
            compiled.CommandCount, compiled.BindingCount, regenerateCount,
            fakeNativeChildCount, syntheticAuthorityCount, unresolvedProducerCount,
            identical, evidence);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static Arch7bQualificationException Mismatch(string detail) =>
        new(Arch7bV2Contracts.OperationalAuthorityMismatch, detail);
}
