using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bOperationalTemplateAuthorityProjection
{
    public static async Task<Arch7bOperationalTemplateFileMaterialization> WriteAsync(
        string sourceTemplatePath, string authorityManifestPath, string outputPath,
        CancellationToken cancellationToken = default)
    {
        sourceTemplatePath = Path.GetFullPath(sourceTemplatePath);
        authorityManifestPath = Path.GetFullPath(authorityManifestPath);
        outputPath = Path.GetFullPath(outputPath);
        var sourceTemplateBytes = await File.ReadAllBytesAsync(sourceTemplatePath,
            cancellationToken).ConfigureAwait(false);
        var authorityBytes = await File.ReadAllBytesAsync(authorityManifestPath,
            cancellationToken).ConfigureAwait(false);
        var sourceTemplate = JsonSerializer.Deserialize<Arch7bOneShotLivePlanTemplate>(
            sourceTemplateBytes, Arch7bJson.CanonicalOptions) ?? throw Mismatch("source-template");
        sourceTemplate.ValidateEvidence();
        var inventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder.Build(
            sourceTemplate);
        var manifest = Arch7bOperationalExecutionAuthorityManifestParser.ParseStrict(authorityBytes);
        if (Convert.ToHexStringLower(SHA256.HashData(sourceTemplateBytes)) !=
            manifest.SourceTemplateSha256)
            throw Mismatch("authority-source-template-sha256");
        var authorities = manifest.Project(inventory);
        Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(inventory, manifest,
            authorities);
        var staticAuthoritySet = Arch7bOneShotContracts.Sha256(string.Join('\n', authorities
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => string.Join(':', value.Key, value.Value.Path,
                value.Value.Sha256, value.Value.MustExist, value.Value.MustBeInsideRunRoot))));
        var provisional = sourceTemplate with
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
        var graph = Arch7bStageFactGraphValidator.RequireValid(template.StageContracts);
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
        var inventoryPath = Path.Combine(Path.GetDirectoryName(outputPath)!,
            Arch7bStageFactGraphValidator.InventoryFileName);
        var inventoryBytes = Arch7bStageFactGraphValidator.SerializeInventory(graph);
        await using (var stream = new FileStream(inventoryPath, FileMode.CreateNew,
                         FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await stream.WriteAsync(inventoryBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var inventorySha = Convert.ToHexStringLower(SHA256.HashData(inventoryBytes));
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n',
            Arch7bOperationalLivePlanTemplateMaterializer.FileVersion, sourceTemplatePath,
            sourceSha, outputPath, outputSha, inventoryPath, inventorySha,
            template.EvidenceSha256, graph.EvidenceSha256,
            sourceTemplate.CommandTemplates.Count,
            Arch7bOperationalLiveFactBindingCatalog.Build().Sum(value => value.Bindings.Count),
            regenerateCount, fakeNativeChildCount,
            syntheticAuthorityCount, unresolvedProducerCount, identical));
        return new(Arch7bOperationalLivePlanTemplateMaterializer.FileVersion,
            sourceTemplatePath, sourceSha, outputPath, outputSha,
            inventoryPath, inventorySha, template.EvidenceSha256,
            sourceTemplate.CommandTemplates.Count,
            Arch7bOperationalLiveFactBindingCatalog.Build().Sum(value => value.Bindings.Count),
            regenerateCount,
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
